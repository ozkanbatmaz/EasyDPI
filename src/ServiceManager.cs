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

        /// <summary>dnscrypt-proxy registers its own service, so we only invoke it when missing.</summary>
        public static void EnsureDnsServiceInstalled()
        {
            if (Exists(DnsService)) return;
            if (!File.Exists(AppPaths.DnscryptExe)) return;

            string output;
            ProcessRunner.Run(AppPaths.DnscryptExe,
                "-config \"" + AppPaths.DnscryptConfig + "\" -service install",
                out output, 60000);
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
