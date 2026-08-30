using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace EasyDPI
{
    /// <summary>
    /// Thin wrapper around the external tools we shell out to
    /// (sc.exe, netsh.exe, ipconfig.exe).
    /// </summary>
    static class ProcessRunner
    {
        public static int Run(string executablePath, string arguments, out string output, int timeoutMs)
        {
            output = "";
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(executablePath, arguments);
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;

                using (Process process = Process.Start(startInfo))
                {
                    string standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();

                    if (!process.WaitForExit(timeoutMs)) { try { process.Kill(); } catch { } }

                    output = standardOutput + standardError;
                    return process.HasExited ? process.ExitCode : -1;
                }
            }
            catch (Exception ex) { output = ex.Message; return -1; }
        }

        public static int Sc(string arguments) { string output; return Run("sc.exe", arguments, out output, 20000); }
        public static int Netsh(string arguments) { string output; return Run("netsh.exe", arguments, out output, 20000); }
        public static void FlushDnsCache() { string output; Run("ipconfig.exe", "/flushdns", out output, 15000); }
    }

    /// <summary>
    /// Installs and controls the two Windows services EasyDPI manages:
    /// the DPI bypass engine and the encrypted DNS resolver.
    /// </summary>
    static class ServiceManager
    {
        public const string BypassService = "GoodbyeDPI";
        public const string DnsService = "dnscrypt-proxy";

        public static bool Exists(string serviceName)
        {
            try
            {
                foreach (ServiceController service in ServiceController.GetServices())
                    if (string.Equals(service.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Whether a name is registered as either a service or a driver.
        ///
        /// WinDivert is a kernel driver, and ServiceController.GetServices() lists only
        /// Win32 services — never a driver — so the plain check reports a loaded driver
        /// as "not installed". Drivers live in a separate list.
        /// </summary>
        public static bool ExistsIncludingDrivers(string serviceName)
        {
            if (Exists(serviceName)) return true;

            try
            {
                foreach (ServiceController device in ServiceController.GetDevices())
                    if (string.Equals(device.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            return false;
        }

        /// <summary>Localized, human readable state for the details panel.</summary>
        public static string DescribeState(string serviceName)
        {
            if (!Exists(serviceName)) return Strings.Get("service.notInstalled");

            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    switch (service.Status)
                    {
                        case ServiceControllerStatus.Running: return Strings.Get("service.running");
                        case ServiceControllerStatus.Stopped: return Strings.Get("service.stopped");
                        case ServiceControllerStatus.StartPending: return Strings.Get("service.starting");
                        case ServiceControllerStatus.StopPending: return Strings.Get("service.stopping");
                        default: return service.Status.ToString();
                    }
                }
            }
            catch { return Strings.Get("service.unknown"); }
        }

        public static bool IsRunning(string serviceName)
        {
            if (!Exists(serviceName)) return false;
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                    return service.Status == ServiceControllerStatus.Running;
            }
            catch { return false; }
        }

        public static bool WaitFor(string serviceName, ServiceControllerStatus desired, int timeoutMs)
        {
            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                {
                    service.WaitForStatus(desired, TimeSpan.FromMilliseconds(timeoutMs));
                    return true;
                }
            }
            catch { return false; }
        }

        public static void SetStartupAutomatic(string serviceName) { ProcessRunner.Sc("config " + serviceName + " start= auto"); }
        public static void SetStartupDisabled(string serviceName) { ProcessRunner.Sc("config " + serviceName + " start= disabled"); }
        public static void Start(string serviceName) { ProcessRunner.Sc("start " + serviceName); }
        public static void Stop(string serviceName) { ProcessRunner.Sc("stop " + serviceName); }

        /// <summary>
        /// How long the resolver gets to come up. The old fifteen seconds were not
        /// enough: dnscrypt-proxy waits for the network to look usable before it starts
        /// serving, and on a first run it may also have to fetch its server list, so a
        /// perfectly healthy start can take most of a minute.
        /// </summary>
        const int DnsStartTimeoutMs = 60000;

        /// <summary>
        /// Registers the resolver service, and repairs the registration afterwards.
        ///
        /// Two things go wrong here that both look identical from the outside — the
        /// service exists, and it never starts.
        ///
        /// The first is quoting. dnscrypt-proxy registers itself with an unquoted
        /// ImagePath: dns\dnscrypt-proxy.exe -config dns\dnscrypt-proxy.toml. If the
        /// application sits anywhere with a space in the path — "New folder (2)" on the
        /// desktop, say, which is exactly where people put a downloaded zip — Windows
        /// reads the executable as everything up to the first space and cannot find it.
        /// The service is registered and permanently unstartable.
        ///
        /// The second is staleness. Copying a new version into a new folder and deleting
        /// the old one leaves the registration pointing at an executable that no longer
        /// exists, and simply checking "does a service by this name exist" concludes that
        /// there is nothing to do.
        ///
        /// So the registration is compared against this copy and rewritten when it does
        /// not match, rather than trusted because the name is taken.
        /// </summary>
        public static void EnsureDnsServiceInstalled()
        {
            if (!File.Exists(AppPaths.DnscryptExe)) return;

            if (Exists(DnsService) && !RegistrationPointsHere())
            {
                Stop(DnsService);
                ProcessRunner.Sc("delete " + DnsService);
                Thread.Sleep(1200);
            }

            if (!Exists(DnsService))
            {
                string output;
                ProcessRunner.Run(AppPaths.DnscryptExe,
                    "-config \"" + AppPaths.DnscryptConfig + "\" -service install",
                    out output, 60000);
            }

            RepairDnsImagePath();
        }

        /// <summary>The command line Windows should be running for this copy.</summary>
        static string CorrectDnsImagePath()
        {
            return "\"" + AppPaths.DnscryptExe + "\" -config \"" + AppPaths.DnscryptConfig + "\"";
        }

        static bool RegistrationPointsHere()
        {
            string registered = RegisteredImagePath(DnsService);
            if (registered == null) return false;

            return registered.IndexOf(AppPaths.DnscryptExe,
                                      StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void RepairDnsImagePath()
        {
            try
            {
                string registered = RegisteredImagePath(DnsService);
                string correct = CorrectDnsImagePath();

                if (registered == null || registered == correct) return;

                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                           ServiceRegistryPath + DnsService, true))
                {
                    if (key != null) key.SetValue("ImagePath", correct,
                                                  Microsoft.Win32.RegistryValueKind.ExpandString);
                }
            }
            catch { }
        }

        const string ServiceRegistryPath = @"SYSTEM\CurrentControlSet\Services\";

        /// <summary>The command line a registered service actually runs, or null.</summary>
        public static string RegisteredImagePath(string serviceName)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                           ServiceRegistryPath + serviceName))
                {
                    if (key == null) return null;
                    return key.GetValue("ImagePath") as string;
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Brings the encrypted resolver up, and when it will not come up, says why.
        ///
        /// The reason matters more than it looks. Encrypted DNS is only switched on when
        /// the provider has been caught returning forged addresses, so a resolver that
        /// fails to start leaves every name on the machine resolving to whatever the
        /// provider wants. Reporting "did not start" and carrying on — which is what this
        /// used to do — sends the user off to blame the bypass settings for a problem
        /// those settings cannot cause and cannot fix.
        /// </summary>
        public static bool TryStartDnsService(out string reason)
        {
            reason = null;

            if (!File.Exists(AppPaths.DnscryptExe))
            {
                reason = Strings.Get("dnsfail.missingBinary");
                return false;
            }

            EnsureDnsServiceInstalled();

            if (!Exists(DnsService))
            {
                reason = Strings.Get("dnsfail.notInstalled");
                return false;
            }

            SetStartupAutomatic(DnsService);

            string output;
            ProcessRunner.Run("sc.exe", "start " + DnsService, out output, 30000);

            if (WaitFor(DnsService, ServiceControllerStatus.Running, DnsStartTimeoutMs) && IsRunning(DnsService))
                return true;

            // The most common cause by far is something else already holding the DNS
            // port — a second resolver, a filtering tool, or Internet Connection Sharing.
            string occupant = DescribeDnsPortOwner();
            reason = occupant != null
                ? Strings.Get("dnsfail.portTaken", occupant)
                : Strings.Get("dnsfail.didNotStart");
            return false;
        }

        /// <summary>
        /// Names the process listening on the DNS port, if it is not ours. Returns null
        /// when the port is free, which means the resolver failed for another reason.
        /// </summary>
        public static string DescribeDnsPortOwner()
        {
            try
            {
                string output;
                ProcessRunner.Run("netstat.exe", "-a -n -o -p UDP", out output, 15000);

                foreach (string line in output.Split('\n'))
                {
                    string[] parts = line.Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3) continue;
                    if (!parts[1].EndsWith(":53")) continue;

                    int processId;
                    if (!int.TryParse(parts[parts.Length - 1], out processId)) continue;

                    try
                    {
                        Process owner = Process.GetProcessById(processId);
                        if (string.Equals(owner.ProcessName, "dnscrypt-proxy", StringComparison.OrdinalIgnoreCase))
                            continue;
                        return owner.ProcessName + " (PID " + processId + ")";
                    }
                    catch { return "PID " + processId; }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Recreates the bypass service with the given arguments.
        /// The arguments live inside the service binary path, so changing them means
        /// deleting and recreating the service rather than just reconfiguring it.
        /// </summary>
        public static void InstallBypassService(string bypassArguments)
        {
            if (Exists(BypassService))
            {
                Stop(BypassService);
                Thread.Sleep(1200);
                ProcessRunner.Sc("delete " + BypassService);
                Thread.Sleep(1200);
            }

            string binaryPath = "\\\"" + AppPaths.GoodbyeDpiExe + "\\\" " + bypassArguments;

            ProcessRunner.Sc("create " + BypassService + " binPath= \"" + binaryPath +
                             "\" DisplayName= \"GoodbyeDPI (EasyDPI)\" start= auto");
            ProcessRunner.Sc("description " + BypassService + " \"Managed by EasyDPI (" + bypassArguments + ")\"");
            ProcessRunner.Sc("failure " + BypassService + " reset= 0 actions= restart/5000/restart/5000/restart/5000");
        }

        /// <summary>
        /// Kills bypass processes that are not owned by the service — for instance the
        /// short lived ones the tuner starts while trying out candidate settings.
        /// Two instances running at once corrupt each other's packet handling.
        /// </summary>
        public static void KillOrphanedBypassProcesses()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("goodbyedpi"))
                { try { process.Kill(); process.WaitForExit(3000); } catch { } }
            }
            catch { }
        }
    }
}
