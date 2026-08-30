using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EasyDPI
{
    /// <summary>
    /// Removes EasyDPI from the machine completely: both services, the packet driver,
    /// the DNS setting, and finally the application's own files.
    ///
    /// This exists because deleting the folder is not uninstalling. Doing that by hand
    /// leaves two registered services pointing at files that no longer exist, a kernel
    /// driver still registered, and — if protection was on at the time — a network
    /// adapter whose DNS server is 127.0.0.1 with nothing listening there, which takes
    /// name resolution down entirely and looks like the machine has lost its internet
    /// connection. Everything this application sets up, it should be able to take apart.
    /// </summary>
    static class Uninstaller
    {
        /// <summary>
        /// Only what EasyDPI ships is deleted, never the containing folder as a whole.
        /// Portable applications get extracted into folders that already hold other
        /// things — Downloads, the desktop, a repository checkout — and a recursive
        /// delete of whatever directory the executable happens to sit in is how a
        /// cleanup feature turns into a data-loss feature. The folder itself is removed
        /// at the end only if it is empty by then.
        /// </summary>
        static readonly string[] OwnedFolders = { "bin", "dns", "licenses" };

        static readonly string[] OwnedFiles =
        {
            "LICENSE", "README.md", "README.tr.md",
            "TRANSPARENCY.md", "TRANSPARENCY.tr.md", "easydpi.log"
        };

        /// <summary>
        /// Tears everything down and schedules the files for deletion. The caller is
        /// expected to close the application immediately afterwards: the executable
        /// cannot delete itself while it is still running, so the last step waits for
        /// this process to exit.
        /// </summary>
        public static void Run(Action<string> report)
        {
            report(Strings.Get("uninstall.started"));

            // 1. The bypass service, plus any engine process left running outside it.
            ServiceManager.Stop(ServiceManager.BypassService);
            ServiceManager.KillOrphanedBypassProcesses();
            Thread.Sleep(800);
            ProcessRunner.Sc("delete " + ServiceManager.BypassService);
            report(Strings.Get("uninstall.bypassRemoved"));

            // 2. The packet driver GoodbyeDPI registers the first time it runs. It is a
            //    kernel driver, so of everything here this is the leftover that matters
            //    most, and nothing else on the machine will clean it up.
            ProcessRunner.Sc("stop WinDivert");
            ProcessRunner.Sc("delete WinDivert");
            report(Strings.Get("uninstall.driverRemoved"));

            // 3. The resolver, through its own uninstaller so that its service
            //    registration goes with it.
            if (ServiceManager.Exists(ServiceManager.DnsService))
            {
                ServiceManager.Stop(ServiceManager.DnsService);
                Thread.Sleep(800);

                if (File.Exists(AppPaths.DnscryptExe))
                {
                    string output;
                    ProcessRunner.Run(AppPaths.DnscryptExe,
                        "-config \"" + AppPaths.DnscryptConfig + "\" -service uninstall",
                        out output, 30000);
                }

                if (ServiceManager.Exists(ServiceManager.DnsService))
                    ProcessRunner.Sc("delete " + ServiceManager.DnsService);

                report(Strings.Get("uninstall.dnsRemoved"));
            }

            KillResolverProcesses();

            // 4. Hand DNS back before the resolver it points at disappears. In this order
            //    the machine is never left resolving names against nothing.
            NetworkTools.RestoreDefaultDns();
            report(Strings.Get("uninstall.dnsRestored"));

            // 5. The files, once this process is gone.
            if (ScheduleFileRemoval())
                report(Strings.Get("uninstall.filesScheduled"));
            else
                report(Strings.Get("uninstall.filesFailed", AppPaths.Root));
        }

        static void KillResolverProcesses()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("dnscrypt-proxy"))
                { try { process.Kill(); process.WaitForExit(3000); } catch { } }
            }
            catch { }
        }

        /// <summary>
        /// Builds the command that deletes the installation. It is handed to cmd.exe as
        /// arguments rather than written to a batch file on purpose: a batch file would
        /// have to be saved in an encoding that matches the console code page, and paths
        /// with non-ASCII characters in them — which is most people's user folder outside
        /// English-speaking countries — come out mangled and delete nothing.
        ///
        /// The executable is still locked while this runs, so the delete is attempted,
        /// given a few seconds, and attempted again.
        /// </summary>
        public static string BuildRemovalCommand(string root)
        {
            root = root.TrimEnd('\\');

            System.Text.StringBuilder command = new System.Text.StringBuilder();
            command.Append("/c ping -n 3 127.0.0.1 >nul");

            foreach (string folder in OwnedFolders)
                command.Append(" & rd /s /q \"").Append(Path.Combine(root, folder)).Append("\"");

            foreach (string file in OwnedFiles)
                command.Append(" & del /f /q \"").Append(Path.Combine(root, file)).Append("\"");

            string executable = Path.Combine(root, "EasyDPI.exe");

            // Two passes: the first runs while this process may still be shutting down.
            command.Append(" & del /f /q \"").Append(executable).Append("\"");
            command.Append(" & ping -n 4 127.0.0.1 >nul");
            command.Append(" & del /f /q \"").Append(executable).Append("\"");

            // No /s here. The folder goes only if nothing of the user's is left in it.
            command.Append(" & rd \"").Append(root).Append("\"");

            return command.ToString();
        }

        static bool ScheduleFileRemoval()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", BuildRemovalCommand(AppPaths.Root));
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                // Anywhere but the folder being deleted: a working directory cannot be
                // removed while a process is sitting in it.
                startInfo.WorkingDirectory = Path.GetTempPath();

                Process.Start(startInfo);
                return true;
            }
            catch { return false; }
        }
    }
}
