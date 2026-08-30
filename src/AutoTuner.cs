using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace EasyDPI
{
    /// <summary>
    /// Measures the network you are on and works out the configuration that gets past it.
    ///
    /// This exists because there is no single correct answer: the packet manipulation that
    /// defeats one provider's inspection does nothing against another's, and the settings
    /// that work in one country routinely fail in the next. Rather than shipping a guess,
    /// the tuner tries candidates against your own connection and keeps what actually works.
    ///
    /// Four steps:
    ///   1. DNS      — compare the system resolver against encrypted DNS to spot tampering
    ///   2. Scan     — request every address in the probe list and record what is cut off
    ///   3. Screen   — try every candidate setting against the blocked addresses
    ///   4. Measure  — re-test the leaders more carefully and rank them on speed
    ///
    /// Two decisions in here are worth stating outright, because both were wrong before.
    ///
    /// The scan tests service endpoints, not sites. Blocking is applied to names, so
    /// "roblox.com opens" says nothing about setup.roblox.com or clientsettingscdn.roblox.com,
    /// and a setting can score a perfect result while leaving the installer unable to fetch
    /// its settings and profile pages half empty. Only per-endpoint measurement sees that.
    ///
    /// The search does not stop at the first setting that works. Several settings usually
    /// clear the same blocks, and they are not equally good — fragmenting every packet and
    /// resending fake ones costs throughput. So every candidate is screened, and the
    /// leaders are then measured for latency and download rate before one is chosen.
    /// </summary>
    static class AutoTuner
    {
        /// <summary>
        /// Candidate settings. Each is a real GoodbyeDPI command line; the numbered
        /// presets expand as documented upstream:
        ///
        ///   -1  -p -r -s -f 2 -k 2 -n -e 2          (most compatible)
        ///   -2  -p -r -s -f 2 -k 2 -n -e 40
        ///   -3  -p -r -s -e 40
        ///   -4  -p -r -s                            (fastest legacy mode)
        ///   -5  -f 2 -e 2 --auto-ttl --reverse-frag --max-payload
        ///   -6  -f 2 -e 2 --wrong-seq --reverse-frag --max-payload
        ///   -7  -f 2 -e 2 --wrong-chksum --reverse-frag --max-payload
        ///   -8  -f 2 -e 2 --wrong-seq --wrong-chksum --reverse-frag --max-payload
        ///   -9  -8 plus -q                          (upstream default)
        ///
        /// The hand-written entries exist because the presets leave gaps. The fake-packet
        /// modes have to land between this machine and the inspecting equipment: --auto-ttl
        /// estimates that distance from the route to the site, so a service whose CDN edge
        /// sits a different number of hops away — which is exactly the case for the Roblox
        /// setup and client-settings hosts — can stay blocked while everything else opens.
        /// Fixed TTLs and a wider auto-ttl range cover that. The --native-frag entries cover
        /// the opposite situation, where fake packets are what the inspector is looking for
        /// and splitting the real packet is enough on its own.
        /// </summary>
        static readonly string[] Candidates =
        {
            "-9 --frag-by-sni",
            "-9",
            "-5 -q --frag-by-sni",
            "-5 -q",
            "-6 -q --frag-by-sni",
            "-6 -q",
            "-7 -q --frag-by-sni",
            "-7 -q",
            "-8 -q --frag-by-sni",
            "-8 -q",
            "-f 2 -e 2 --set-ttl 3 --reverse-frag --max-payload -q --frag-by-sni",
            "-f 2 -e 2 --set-ttl 5 --reverse-frag --max-payload -q --frag-by-sni",
            "-f 2 -e 2 --auto-ttl 1-4-10 --reverse-frag --max-payload -q --frag-by-sni",
            "-f 2 -e 2 --native-frag --frag-by-sni -q",
            "-f 2 -e 2 --native-frag --reverse-frag --frag-by-sni -q",
            "--native-frag --frag-by-sni -q",
            "-9 --frag-by-sni --fake-resend 3",
            "-1",
            "-2",
            "-3",
            "-4"
        };

        const int ScanTimeoutMs = 8000;
        const int ScreenTimeoutMs = 3500;
        const int MeasureTimeoutMs = 6000;

        const int ScanRepeats = 2;      // one failure can be a fluke; two in a row is a block
        const int ScreenRepeats = 1;
        const int MeasureRepeats = 3;

        const int WorkerCount = 6;      // fewer parallel requests than a browser opens
        const int EngineWarmupMs = 2500;
        const int DriverReleaseMs = 1500;

        const int DnsSampleSize = 8;
        const int MaxScreenTargets = 10;
        const int MaxMeasureTargets = 20;
        const int FinalistCount = 3;

        const int ThroughputBytes = 2000000;
        const int ThroughputTimeoutMs = 15000;

        const int ScorePerUnblockedSite = 10;
        const int PenaltyPerBrokenSite = 25;

        // ---------------------------------------------------------------
        // Measurement
        // ---------------------------------------------------------------

        /// <summary>What repeated probing of one address produced.</summary>
        sealed class Measurement
        {
            public int Attempts;
            public int Successes;
            public int MedianMs = int.MaxValue;

            public bool Reachable { get { return Successes > 0; } }
        }

        /// <summary>How one candidate setting performed.</summary>
        sealed class CandidateResult
        {
            public string Arguments;
            public bool Started = true;
            public int TargetsOpen;
            public int TargetsTested;
            public int ControlsBroken;
            public int MedianMs = int.MaxValue;
            public int Kbps;

            /// <summary>Addresses this setting left unreachable, recorded while it was running.</summary>
            public List<string> StillClosed = new List<string>();

            public int Score
            {
                get { return TargetsOpen * ScorePerUnblockedSite - ControlsBroken * PenaltyPerBrokenSite; }
            }
        }

        /// <summary>
        /// Whether a setting sends fake packets.
        ///
        /// Fake packets are aimed: they carry a manipulated TTL, sequence number or
        /// checksum so that the inspecting equipment sees them and the real server never
        /// does. That aim is calculated for the route out of this machine. Inside a VPN
        /// the traffic takes a different route entirely, so the packet meant to die on the
        /// way can arrive at the real server and break the connection instead. Presets -5
        /// to -9 all use them; -1 to -4 and the fragmentation-only settings do not.
        /// </summary>
        static bool UsesFakePackets(string arguments)
        {
            string flags = arguments.ToLowerInvariant();

            if (flags.Contains("--set-ttl") || flags.Contains("--auto-ttl") ||
                flags.Contains("--wrong-seq") || flags.Contains("--wrong-chksum") ||
                flags.Contains("--fake-")) return true;

            // Matched as whole arguments: "--auto-ttl 1-4-10" contains "-4" and would
            // otherwise be read as the -4 preset.
            foreach (string token in flags.Split(' '))
                for (int preset = 5; preset <= 9; preset++)
                    if (token == "-" + preset) return true;

            return false;
        }

        /// <summary>
        /// The candidates worth trying on this machine. With a VPN connected the ones that
        /// send fake packets are left out: they are the settings most likely to break the
        /// tunnel, and a setting that wins the measurement by breaking the connection the
        /// user actually wanted is not a win.
        /// </summary>
        static List<string> CandidatesFor(bool vpnActive)
        {
            List<string> chosen = new List<string>();

            foreach (string candidate in Candidates)
                if (!vpnActive || !UsesFakePackets(candidate)) chosen.Add(candidate);

            return chosen;
        }

        static string Pad(string text, int width)
        {
            if (text.Length >= width) return text;
            return text + new string(' ', width - text.Length);
        }

        static int Median(List<int> values)
        {
            if (values.Count == 0) return int.MaxValue;
            values.Sort();
            return values[values.Count / 2];
        }

        static string DescribeMs(int ms)
        {
            return ms == int.MaxValue ? "-" : ms + " ms";
        }

        static string DescribeKbps(int kbps)
        {
            if (kbps <= 0) return "-";
            return (kbps / 1000.0).ToString("0.0") + " Mbps";
        }

        /// <summary>
        /// Probes a set of addresses in parallel.
        ///
        /// Sequential probing is dominated by timeouts: a blocked address costs the whole
        /// timeout, so a scan of eighty addresses would run for minutes and a search over
        /// twenty candidate settings would be unusable. Six workers cut that by roughly
        /// the same factor while keeping the traffic pattern similar to a browser's.
        /// </summary>
        static Dictionary<string, Measurement> ProbeAll(IList<string> hosts, int repeats, int timeoutMs)
        {
            Dictionary<string, Measurement> results = new Dictionary<string, Measurement>();
            foreach (string host in hosts) results[host] = new Measurement();

            if (hosts.Count == 0) return results;

            int cursor = -1;
            int workerCount = Math.Min(WorkerCount, hosts.Count);
            Thread[] workers = new Thread[workerCount];

            for (int index = 0; index < workerCount; index++)
            {
                workers[index] = new Thread(delegate()
                {
                    while (true)
                    {
                        int position = Interlocked.Increment(ref cursor);
                        if (position >= hosts.Count) return;

                        string host = hosts[position];
                        List<int> timings = new List<int>();
                        int successes = 0;

                        for (int attempt = 0; attempt < repeats; attempt++)
                        {
                            int elapsed;
                            if (NetworkTools.ProbeHttps(host, timeoutMs, out elapsed))
                            {
                                successes++;
                                timings.Add(elapsed);
                            }
                        }

                        // Each worker writes to its own entry, and no entry is added or
                        // removed after this point, so the dictionary needs no locking.
                        Measurement measurement = results[host];
                        measurement.Attempts = repeats;
                        measurement.Successes = successes;
                        measurement.MedianMs = Median(timings);
                    }
                });

                workers[index].IsBackground = true;
                workers[index].Start();
            }

            foreach (Thread worker in workers) worker.Join();
            return results;
        }

        static List<string> HostNames(IList<ProbeHost> hosts)
        {
            List<string> names = new List<string>();
            foreach (ProbeHost host in hosts) names.Add(host.Host);
            return names;
        }

        /// <summary>
        /// Picks the addresses to screen candidates against, taking them in turn from each
        /// service rather than in list order. Ten addresses from one service would tune for
        /// that service alone; ten spread across the blocked services tune for the network.
        /// </summary>
        static List<string> SpreadAcrossGroups(List<ProbeHost> blocked, int limit)
        {
            List<string> chosen = new List<string>();
            List<string> groups = ProbeList.GroupsOf(blocked);
            int round = 0;

            while (chosen.Count < limit)
            {
                bool addedAny = false;

                foreach (string group in groups)
                {
                    int seen = 0;
                    foreach (ProbeHost host in blocked)
                    {
                        if (host.Group != group) continue;
                        if (seen++ != round) continue;

                        chosen.Add(host.Host);
                        addedAny = true;
                        break;
                    }
                    if (chosen.Count >= limit) break;
                }

                if (!addedAny) break;
                round++;
            }

            return chosen;
        }

        /// <summary>
        /// Runs GoodbyeDPI with one candidate command line and measures the given addresses
        /// while it is up. Returns null if the engine refused to start, which is how an
        /// unsupported option in a candidate reveals itself.
        /// </summary>
        static CandidateResult Evaluate(string candidate, IList<string> targets, IList<string> controls,
                                        int repeats, int timeoutMs, bool measureThroughput)
        {
            CandidateResult result = new CandidateResult();
            result.Arguments = candidate;
            result.TargetsTested = targets.Count;

            Process engine = null;
            try
            {
                // Measured with the same scope it will run with, so the numbers describe
                // the configuration the user ends up with rather than a different one.
                ProcessStartInfo startInfo = new ProcessStartInfo(AppPaths.GoodbyeDpiExe,
                    candidate + Settings.BlacklistArgument(false));
                startInfo.WorkingDirectory = AppPaths.BinFolder;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;

                engine = Process.Start(startInfo);
                Thread.Sleep(EngineWarmupMs);

                if (engine.HasExited)
                {
                    result.Started = false;
                    return result;
                }

                Dictionary<string, Measurement> targetResults = ProbeAll(targets, repeats, timeoutMs);
                Dictionary<string, Measurement> controlResults = ProbeAll(controls, repeats, timeoutMs);

                List<int> timings = new List<int>();

                foreach (string host in targets)
                {
                    Measurement measurement = targetResults[host];
                    if (!measurement.Reachable) { result.StillClosed.Add(host); continue; }

                    result.TargetsOpen++;
                    timings.Add(measurement.MedianMs);
                }

                foreach (string host in controls)
                {
                    Measurement measurement = controlResults[host];
                    if (measurement.Reachable) timings.Add(measurement.MedianMs);
                    else result.ControlsBroken++;
                }

                result.MedianMs = Median(timings);

                if (measureThroughput)
                {
                    // Repeated measurements of the same setting land within about ten
                    // percent of each other, which is enough noise to reorder two close
                    // candidates. Taking the better of two runs discards the sample that
                    // caught a moment of congestion rather than the setting's real cost.
                    int first = NetworkTools.MeasureDownloadKbps(ThroughputBytes, ThroughputTimeoutMs);
                    int second = NetworkTools.MeasureDownloadKbps(ThroughputBytes, ThroughputTimeoutMs);
                    result.Kbps = Math.Max(first, second);
                }

                return result;
            }
            catch
            {
                result.Started = false;
                return result;
            }
            finally
            {
                try { if (engine != null && !engine.HasExited) { engine.Kill(); engine.WaitForExit(3000); } }
                catch { }

                ServiceManager.KillOrphanedBypassProcesses();
                Thread.Sleep(DriverReleaseMs); // let the packet driver unload
            }
        }

        /// <summary>
        /// Ranks two results. Opening more of what is blocked wins first, keeping ordinary
        /// sites working is weighted more heavily than opening one more blocked one, and
        /// only when settings are equally correct does speed decide — first the download
        /// rate, then response time. Speed is a tie-breaker on purpose: a fast setting that
        /// leaves a service unreachable has not solved anything.
        /// </summary>
        static bool IsBetter(CandidateResult candidate, CandidateResult best)
        {
            if (best == null) return true;
            if (candidate.Score != best.Score) return candidate.Score > best.Score;
            if (candidate.Kbps != best.Kbps) return candidate.Kbps > best.Kbps;
            return candidate.MedianMs < best.MedianMs;
        }

        // ---------------------------------------------------------------
        // The run
        // ---------------------------------------------------------------

        public static void Run(Action<string> report)
        {
            Stopwatch clock = Stopwatch.StartNew();
            report(Strings.Get("tune.start"));

            bool vpnActive = NetworkTools.IsVpnActive();
            if (vpnActive)
            {
                report(Strings.Get("tune.vpnDetected", NetworkTools.DescribeVpnAdapter() ?? "VPN"));
                report(Strings.Get("tune.vpnCandidates"));
            }

            List<ProbeHost> probes = ProbeList.Build();
            List<ProbeHost> targets = new List<ProbeHost>();
            List<ProbeHost> controls = new List<ProbeHost>();

            foreach (ProbeHost probe in probes)
            {
                if (probe.IsControl) controls.Add(probe);
                else targets.Add(probe);
            }

            // Nothing may interfere with the measurements, so tear everything down first.
            ServiceManager.Stop(ServiceManager.BypassService);
            ServiceManager.KillOrphanedBypassProcesses();
            if (ServiceManager.Exists(ServiceManager.DnsService)) ServiceManager.Stop(ServiceManager.DnsService);
            NetworkTools.RestoreDefaultDns();
            Thread.Sleep(DriverReleaseMs);

            // -----------------------------------------------------------
            // 1. Is DNS being tampered with?
            // -----------------------------------------------------------
            report("");
            report(Strings.Get("tune.step1"));

            int tamperedCount = 0;
            int checkedCount = 0;

            // A sample is enough to characterise the resolver, and every extra name costs
            // an encrypted lookup. It is taken across services rather than from the head
            // of the list, because a provider may redirect one service and leave the rest.
            foreach (string sample in SpreadAcrossGroups(targets, DnsSampleSize))
            {
                List<string> truth = NetworkTools.ResolveEncrypted(sample);
                if (truth.Count == 0) continue;

                checkedCount++;
                string viaSystem = NetworkTools.ResolveWithSystemDns(sample);

                if (viaSystem == null)
                {
                    tamperedCount++;
                    report("   " + Pad(sample, 32) + Strings.Get("tune.dnsNoAnswer"));
                }
                else if (!truth.Contains(viaSystem))
                {
                    // The system resolver returned an address the authoritative answer
                    // does not contain — typically the provider's block page.
                    tamperedCount++;
                    report("   " + Pad(sample, 32) + Strings.Get("tune.dnsFakeAddress"));
                }
                else report("   " + Pad(sample, 32) + Strings.Get("tune.dnsClean"));
            }

            if (checkedCount == 0)
            {
                report(Strings.Get("tune.noInternet"));
                return;
            }

            Settings.UseEncryptedDns = tamperedCount > 0;

            if (Settings.UseEncryptedDns)
            {
                report(Strings.Get("tune.dnsTamperedResult"));

                string reason;
                if (ServiceManager.TryStartDnsService(out reason))
                {
                    NetworkTools.PointDnsToLocalhost();
                }
                else
                {
                    // Stop here rather than measure through a resolver that is known to
                    // be lying. Every name would resolve to the provider's address, every
                    // probe would fail, and the search would pick whichever setting failed
                    // most gracefully — a confident answer built on nothing. The earlier
                    // version carried on and did exactly that.
                    report(Strings.Get("tune.warnDnsStart"));
                    report("   " + reason);
                    report("");
                    report(Strings.Get("tune.dnsAbort"));
                    report(Strings.Get("tune.dnsAbortHint"));
                    return;
                }
            }
            else report(Strings.Get("tune.dnsCleanResult"));

            // -----------------------------------------------------------
            // 2. What is actually cut off, endpoint by endpoint?
            // -----------------------------------------------------------
            report("");
            report(Strings.Get("tune.step2", probes.Count, ProbeList.GroupsOf(probes).Count));

            Dictionary<string, Measurement> baseline = ProbeAll(HostNames(probes), ScanRepeats, ScanTimeoutMs);
            List<ProbeHost> blocked = new List<ProbeHost>();
            List<string> deadControls = new List<string>();

            foreach (string group in ProbeList.GroupsOf(probes))
            {
                int open = 0, total = 0;
                List<string> closed = new List<string>();

                foreach (ProbeHost probe in probes)
                {
                    if (probe.Group != group) continue;
                    total++;

                    if (baseline[probe.Host].Reachable) open++;
                    else
                    {
                        closed.Add(probe.Host);
                        if (probe.IsControl) deadControls.Add(probe.Host);
                        else blocked.Add(probe);
                    }
                }

                report("   " + Pad(group, 16) + Pad(open + "/" + total, 10) +
                       (closed.Count == 0 ? Strings.Get("tune.allOpen")
                                          : Strings.Get("tune.closedList", string.Join(", ", closed.ToArray()))));
            }

            // Written whether or not targeted scope is on: switching it on later should not
            // require running the whole measurement again just to produce this file.
            List<string> blockedHosts = new List<string>();
            foreach (ProbeHost probe in blocked) blockedHosts.Add(probe.Host);
            Settings.SaveBlacklist(blockedHosts);

            if (Settings.TargetedScope)
                report(Strings.Get("tune.targetedScope", blockedHosts.Count));

            // A control that is down before anything is switched on cannot judge damage
            // later, so it is dropped rather than counted against every candidate.
            foreach (string host in deadControls)
                for (int index = controls.Count - 1; index >= 0; index--)
                    if (controls[index].Host == host) controls.RemoveAt(index);

            List<string> controlNames = HostNames(controls);

            // -----------------------------------------------------------
            // 3. Screen every candidate
            // -----------------------------------------------------------
            List<string> screenTargets = SpreadAcrossGroups(blocked, MaxScreenTargets);
            bool nothingBlocked = blocked.Count == 0;

            if (nothingBlocked)
            {
                // Nothing is blocked, so there is nothing to unblock — but the settings
                // still differ in what they cost, and one of them is about to run all the
                // time. The search continues with speed as the only thing being compared.
                report("");
                report(Strings.Get("tune.noBlocking"));
                report(Strings.Get("tune.speedOnly"));
            }

            report("");
            List<string> candidates = CandidatesFor(vpnActive);
            report(Strings.Get("tune.step3", candidates.Count));
            report("   " + Pad(Strings.Get("tune.colSetting"), 74) + Strings.Get("tune.colOpen"));

            List<CandidateResult> screened = new List<CandidateResult>();

            foreach (string candidate in candidates)
            {
                CandidateResult result = Evaluate(candidate, screenTargets, controlNames,
                                                  ScreenRepeats, ScreenTimeoutMs, false);

                if (!result.Started)
                {
                    report("   " + Pad(candidate, 74) + Strings.Get("tune.candidateFailed"));
                    continue;
                }

                screened.Add(result);
                report("   " + Pad(candidate, 74) +
                       Pad(result.TargetsOpen + "/" + result.TargetsTested, 8) +
                       (result.ControlsBroken > 0 ? Strings.Get("tune.sitesBroken", result.ControlsBroken) : ""));
            }

            if (screened.Count == 0)
            {
                report("");
                report(Strings.Get("tune.nothingWorked"));
                Settings.Save();
                return;
            }

            // -----------------------------------------------------------
            // 4. Measure the leaders properly
            // -----------------------------------------------------------
            screened.Sort(delegate(CandidateResult left, CandidateResult right)
            {
                if (left.Score != right.Score) return right.Score - left.Score;
                return left.MedianMs - right.MedianMs;
            });

            List<string> measureTargets = SpreadAcrossGroups(blocked, MaxMeasureTargets);
            int finalists = Math.Min(FinalistCount, screened.Count);

            report("");
            report(Strings.Get("tune.step4", finalists));
            report("   " + Pad(Strings.Get("tune.colSetting"), 74) + Pad(Strings.Get("tune.colOpen"), 8) +
                   Pad(Strings.Get("tune.colLatency"), 10) + Strings.Get("tune.colSpeed"));

            CandidateResult best = null;

            for (int index = 0; index < finalists; index++)
            {
                CandidateResult measured = Evaluate(screened[index].Arguments, measureTargets, controlNames,
                                                    MeasureRepeats, MeasureTimeoutMs, true);
                if (!measured.Started) continue;

                report("   " + Pad(measured.Arguments, 74) +
                       Pad(measured.TargetsOpen + "/" + measured.TargetsTested, 8) +
                       Pad(DescribeMs(measured.MedianMs), 10) +
                       DescribeKbps(measured.Kbps) +
                       (measured.ControlsBroken > 0 ? Strings.Get("tune.sitesBroken", measured.ControlsBroken) : ""));

                if (IsBetter(measured, best)) best = measured;
            }

            report("");

            if (best == null || (!nothingBlocked && best.TargetsOpen == 0))
            {
                report(Strings.Get("tune.nothingWorked"));
                Settings.Save();
                return;
            }

            Settings.BypassArguments = best.Arguments;
            Settings.Save();

            report(Strings.Get("tune.best", best.Arguments));
            report(Strings.Get("tune.bestSpeed", DescribeMs(best.MedianMs), DescribeKbps(best.Kbps)));
            report(Strings.Get("tune.encryptedDnsVerdict",
                Strings.Get(Settings.UseEncryptedDns ? "tune.required" : "tune.notRequired")));

            // What the chosen setting could not fix is the most useful line in the whole
            // report, and the one a "best settings found" message would otherwise hide.
            if (best.StillClosed.Count > 0)
            {
                report(Strings.Get("tune.stillBlocked", string.Join(", ", best.StillClosed.ToArray())));
                report(Strings.Get("tune.stillBlockedHint"));
            }

            clock.Stop();
            report(Strings.Get("tune.elapsed", (int)clock.Elapsed.TotalMinutes, clock.Elapsed.Seconds));
            report("");

            BypassController.TurnOn(report);
        }
    }
}
