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
    /// Three steps:
    ///   1. DNS       — compare the system resolver against encrypted DNS to spot tampering
    ///   2. Blocking  — attempt real TLS handshakes to find which sites are cut off
    ///   3. Search    — try candidate settings in order, stop at the first clean success
    /// </summary>
    static class AutoTuner
    {
        /// <summary>
        /// Default probes, chosen to cover several censorship regimes rather than one country.
        /// If none of these are blocked the tuner says so and points the user at the
        /// probeDomains setting, where they can name the sites they actually care about.
        /// </summary>
        static readonly string[] DefaultProbeDomains = {
            "discord.com",
            "roblox.com",
            "x.com",
            "medium.com",
            "www.instagram.com",
            "rutracker.org",
            "www.bbc.com",
            "www.linkedin.com"
        };

        /// <summary>
        /// These must keep working. A candidate that unblocks the targets but breaks ordinary
        /// browsing is worse than doing nothing, so damaging one costs more than fixing one.
        /// </summary>
        static readonly string[] ControlDomains = { "google.com", "github.com" };

        /// <summary>
        /// Candidate settings, most promising first. Within each family the more robust
        /// variant (--frag-by-sni) comes first, because the search stops at the first clean
        /// success and the ordering therefore decides which working setting we keep.
        /// </summary>
        static readonly string[] Candidates = {
            "-9 --frag-by-sni",
            "-9",
            "-5 -q --frag-by-sni",
            "-5 -q",
            "-6 -q --frag-by-sni",
            "-6 -q",
            "-7 -q --frag-by-sni",
            "-7 -q",
            "-8 -q",
            "-1", "-2", "-3", "-4"
        };

        const int HandshakeTimeoutMs = 5000;
        const int MaxTargetsToTrack = 3;
        const int EngineWarmupMs = 2500;
        const int DriverReleaseMs = 1500;

        const int ScorePerUnblockedSite = 10;
        const int PenaltyPerBrokenSite = 25;

        static string Pad(string text, int width)
        {
            if (text.Length >= width) return text;
            return text + new string(' ', width - text.Length);
        }

        static string[] ProbeDomains
        {
            get
            {
                if (Settings.CustomProbeDomains != null && Settings.CustomProbeDomains.Count > 0)
                    return Settings.CustomProbeDomains.ToArray();
                return DefaultProbeDomains;
            }
        }

        public static void Run(Action<string> report)
        {
            report(Strings.Get("tune.start"));

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

            Dictionary<string, List<string>> realAddresses = new Dictionary<string, List<string>>();
            List<string> resolvedDomains = new List<string>();
            int tamperedCount = 0;

            foreach (string domain in ProbeDomains)
            {
                List<string> truth = NetworkTools.ResolveEncrypted(domain);
                if (truth.Count == 0) { report("   " + Pad(domain, 20) + Strings.Get("tune.skipped")); continue; }

                realAddresses[domain] = truth;
                resolvedDomains.Add(domain);

                string viaSystem = NetworkTools.ResolveWithSystemDns(domain);

                if (viaSystem == null)
                {
                    tamperedCount++;
                    report("   " + Pad(domain, 20) + Strings.Get("tune.dnsNoAnswer"));
                }
                else if (!truth.Contains(viaSystem))
                {
                    // The system resolver returned an address the authoritative answer
                    // does not contain — typically the provider's block page.
                    tamperedCount++;
                    report("   " + Pad(domain, 20) + Strings.Get("tune.dnsFakeAddress"));
                }
                else report("   " + Pad(domain, 20) + Strings.Get("tune.dnsClean"));
            }

            if (resolvedDomains.Count == 0)
            {
                report(Strings.Get("tune.noInternet"));
                return;
            }

            Settings.UseEncryptedDns = tamperedCount > 0;

            if (Settings.UseEncryptedDns)
            {
                report(Strings.Get("tune.dnsTamperedResult"));
                ServiceManager.EnsureDnsServiceInstalled();

                if (ServiceManager.Exists(ServiceManager.DnsService))
                {
                    ServiceManager.SetStartupAutomatic(ServiceManager.DnsService);
                    ServiceManager.Start(ServiceManager.DnsService);
                    ServiceManager.WaitFor(ServiceManager.DnsService, ServiceControllerStatus.Running, 15000);

                    if (ServiceManager.IsRunning(ServiceManager.DnsService)) NetworkTools.PointDnsToLocalhost();
                    else report(Strings.Get("tune.warnDnsStart"));
                }
                else report(Strings.Get("tune.warnDnsMissing"));
            }
            else report(Strings.Get("tune.dnsCleanResult"));

            // -----------------------------------------------------------
            // 2. Which sites are cut off by inspection?
            // -----------------------------------------------------------
            report("");
            report(Strings.Get("tune.step2"));

            List<string> blockedTargets = new List<string>();

            foreach (string domain in resolvedDomains)
            {
                bool reachable = NetworkTools.CanCompleteTlsHandshake(
                    realAddresses[domain][0], domain, HandshakeTimeoutMs);

                report("   " + Pad(domain, 20) + Strings.Get(reachable ? "tune.reachable" : "tune.blocked"));

                if (!reachable)
                {
                    blockedTargets.Add(domain);
                    // A handful of confirmed targets is enough to evaluate candidates,
                    // and stopping early keeps the whole run short.
                    if (blockedTargets.Count >= MaxTargetsToTrack) break;
                }
            }

            Dictionary<string, string> controlAddresses = new Dictionary<string, string>();
            foreach (string domain in ControlDomains)
            {
                List<string> addresses = NetworkTools.ResolveEncrypted(domain);
                if (addresses.Count > 0) controlAddresses[domain] = addresses[0];
            }

            if (blockedTargets.Count == 0)
            {
                report(Strings.Get("tune.noBlocking"));
                report(Strings.Get("tune.addYourOwn"));
                Settings.BypassArguments = "-9";
                Settings.Save();
                return;
            }

            // -----------------------------------------------------------
            // 3. Try candidates until one works cleanly
            // -----------------------------------------------------------
            report("");
            report(Strings.Get("tune.step3", Candidates.Length));

            string bestArguments = null;
            int bestScore = int.MinValue;

            foreach (string candidate in Candidates)
            {
                Process engine = null;
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(AppPaths.GoodbyeDpiExe, candidate);
                    startInfo.WorkingDirectory = AppPaths.BinFolder;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;

                    engine = Process.Start(startInfo);
                    Thread.Sleep(EngineWarmupMs);

                    if (engine.HasExited)
                    {
                        report("   " + Pad(candidate, 22) + Strings.Get("tune.candidateFailed"));
                        continue;
                    }

                    int unblocked = 0;
                    foreach (string domain in blockedTargets)
                        if (NetworkTools.CanCompleteTlsHandshake(realAddresses[domain][0], domain, HandshakeTimeoutMs))
                            unblocked++;

                    int broken = 0;
                    foreach (KeyValuePair<string, string> control in controlAddresses)
                        if (!NetworkTools.CanCompleteTlsHandshake(control.Value, control.Key, HandshakeTimeoutMs))
                            broken++;

                    int score = unblocked * ScorePerUnblockedSite - broken * PenaltyPerBrokenSite;

                    report("   " + Pad(candidate, 22) + unblocked + "/" + blockedTargets.Count +
                           (broken > 0 ? Strings.Get("tune.sitesBroken", broken) : ""));

                    if (score > bestScore) { bestScore = score; bestArguments = candidate; }
                    if (unblocked == blockedTargets.Count && broken == 0) break;
                }
                catch (Exception ex)
                {
                    report("   " + Pad(candidate, 22) + ex.Message);
                }
                finally
                {
                    try { if (engine != null && !engine.HasExited) { engine.Kill(); engine.WaitForExit(3000); } }
                    catch { }

                    ServiceManager.KillOrphanedBypassProcesses();
                    Thread.Sleep(DriverReleaseMs); // let the packet driver unload
                }
            }

            report("");

            if (bestArguments == null || bestScore <= 0)
            {
                report(Strings.Get("tune.nothingWorked"));
                Settings.Save();
                return;
            }

            Settings.BypassArguments = bestArguments;
            Settings.Save();

            report(Strings.Get("tune.best", bestArguments));
            report(Strings.Get("tune.encryptedDnsVerdict",
                Strings.Get(Settings.UseEncryptedDns ? "tune.required" : "tune.notRequired")));
            report("");

            BypassController.TurnOn(report);
        }
    }
}
