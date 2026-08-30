using System;
using System.Globalization;
using System.IO;
using System.ServiceProcess;
using System.Text;

namespace EasyDPI
{
    /// <summary>
    /// Keeps the activity log on disk between runs.
    ///
    /// Without this the log exists only in the window, and it is gone the moment the
    /// person closes it — which is invariably before anyone asks them what it said.
    /// </summary>
    static class ActivityLog
    {
        const long MaxBytes = 1024 * 1024;
        static bool checkedSize;

        public static void Append(string line)
        {
            try
            {
                if (!checkedSize) { checkedSize = true; TrimIfHuge(); }
                File.AppendAllText(AppPaths.LogFile, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }   // a log that cannot be written must never break the application
        }

        /// <summary>Keeps the newer half. The interesting part of a log is always the end.</summary>
        static void TrimIfHuge()
        {
            try
            {
                FileInfo file = new FileInfo(AppPaths.LogFile);
                if (!file.Exists || file.Length <= MaxBytes) return;

                string all = File.ReadAllText(AppPaths.LogFile);
                File.WriteAllText(AppPaths.LogFile, all.Substring(all.Length / 2), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    /// <summary>
    /// Builds the file a user sends when something does not work.
    ///
    /// "It still does not connect" is impossible to act on, and the questions that
    /// would make it actionable — which settings, which services are actually running,
    /// whether the resolver came up, what the log said at the time — are exactly the
    /// ones a person having trouble is least equipped to answer. This collects them in
    /// one file so that a bug report is one button rather than an interview.
    ///
    /// The report is written in English regardless of the interface language. It is read
    /// by whoever maintains the project, not by the person sending it, and a fixed
    /// language keeps the wording searchable and comparable between reports.
    /// </summary>
    static class DiagnosticReport
    {
        public static string SuggestedFileName()
        {
            return "easydpi-report-" +
                   DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture) + ".txt";
        }

        /// <summary>
        /// <paramref name="sessionLogFallback"/> is the text shown in the window. It is
        /// used only when the log file is missing or unreadable: every line in the window
        /// was already written to that file, so including both would print this session
        /// twice.
        /// </summary>
        public static string Build(string sessionLogFallback)
        {
            StringBuilder text = new StringBuilder();

            Line(text, "EasyDPI diagnostic report");
            Line(text, "=========================");
            Line(text, "");
            Line(text, "Generated   : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
            Line(text, "Version     : " + AppInfo.Version);
            Line(text, "Windows     : " + DescribeWindows());
            Line(text, "Installed at: " + AppPaths.Root.TrimEnd(Path.DirectorySeparatorChar));
            Line(text, "Interface   : " + Strings.CurrentLanguage);
            Line(text, "");

            // Stated plainly, because the person sending this file is about to hand it
            // to someone else and deserves to know what is in it before they do.
            Line(text, "This file holds the application's own activity log, its settings, and the");
            Line(text, "state of the services it manages. It does not contain your browsing history,");
            Line(text, "the sites you visit, or anything about your traffic. The install path above");
            Line(text, "may include your Windows user name.");
            Line(text, "");

            Section(text, "Settings");
            if (File.Exists(AppPaths.ConfigFile))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(AppPaths.ConfigFile))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;
                        Line(text, "  " + trimmed);
                    }
                }
                catch (Exception error) { Line(text, "  could not be read: " + error.Message); }
            }
            else Line(text, "  no config.ini yet (never tuned on this machine)");
            Line(text, "");

            Section(text, "Services");
            Line(text, "  GoodbyeDPI      : " + RawState(ServiceManager.BypassService));
            Line(text, "  dnscrypt-proxy  : " + RawState(ServiceManager.DnsService));
            Line(text, "  WinDivert       : " + RawState("WinDivert"));

            string occupant = ServiceManager.DescribeDnsPortOwner();
            Line(text, "  DNS port 53     : " + (occupant == null ? "not held by anything else" : "held by " + occupant));
            Line(text, "");

            // What Windows actually runs for each service, which is where a service that
            // is registered but never starts gives itself away: a path left behind by an
            // older copy, or one with a space in it that was registered without quotes.
            Section(text, "Service registration");
            Line(text, "  GoodbyeDPI      : " + (ServiceManager.RegisteredImagePath(ServiceManager.BypassService) ?? "not registered"));
            Line(text, "  dnscrypt-proxy  : " + (ServiceManager.RegisteredImagePath(ServiceManager.DnsService) ?? "not registered"));
            Line(text, "");

            Section(text, "Network");
            try { Line(text, "  DNS servers     : " + NetworkTools.DescribeCurrentDns()); }
            catch (Exception error) { Line(text, "  DNS servers     : unreadable (" + error.Message + ")"); }
            Line(text, "");

            Section(text, "Files");
            Line(text, "  goodbyedpi.exe  : " + Present(AppPaths.GoodbyeDpiExe));
            Line(text, "  WinDivert.dll   : " + Present(Path.Combine(AppPaths.BinFolder, "WinDivert.dll")));
            Line(text, "  WinDivert64.sys : " + Present(Path.Combine(AppPaths.BinFolder, "WinDivert64.sys")));
            Line(text, "  dnscrypt-proxy  : " + Present(AppPaths.DnscryptExe));
            Line(text, "  dnscrypt config : " + Present(AppPaths.DnscryptConfig));
            // A missing resolver list is the difference between the encrypted resolver
            // starting instantly and never starting at all on a tampered network, so it
            // is worth knowing whether this copy has one.
            Line(text, "  resolver list   : " + Present(Path.Combine(AppPaths.DnsFolder, "public-resolvers.md")));
            Line(text, "");

            Section(text, "Log");
            string history = ReadStoredLog();

            if (history.Trim().Length > 0)
            {
                text.Append(history);
            }
            else if (sessionLogFallback != null && sessionLogFallback.Trim().Length > 0)
            {
                Line(text, "  (log file unavailable; showing this session only)");
                Line(text, "");
                text.Append(sessionLogFallback);
            }
            else Line(text, "  (empty)");

            return text.ToString();
        }

        public static void Save(string path, string sessionLogFallback)
        {
            File.WriteAllText(path, Build(sessionLogFallback), new UTF8Encoding(false));
        }

        // ---------------------------------------------------------------

        static void Line(StringBuilder text, string value)
        {
            text.Append(value).Append(Environment.NewLine);
        }

        static void Section(StringBuilder text, string title)
        {
            Line(text, "--- " + title + " " + new string('-', Math.Max(0, 60 - title.Length)));
        }

        /// <summary>Service state in English, so reports stay comparable across languages.</summary>
        static string RawState(string serviceName)
        {
            if (!ServiceManager.ExistsIncludingDrivers(serviceName)) return "not installed";

            try
            {
                using (ServiceController service = new ServiceController(serviceName))
                    return service.Status.ToString();
            }
            catch (Exception error) { return "unreadable (" + error.Message + ")"; }
        }

        static string Present(string path)
        {
            try
            {
                if (!File.Exists(path)) return "MISSING";
                return "present (" + new FileInfo(path).Length + " bytes)";
            }
            catch { return "unreadable"; }
        }

        /// <summary>
        /// The edition and build, read from the registry.
        ///
        /// Environment.OSVersion cannot tell Windows 10 from Windows 11 — both report
        /// 10.0 — and it lies outright to any executable whose manifest does not list
        /// the newer compatibility GUIDs. The build number is what actually identifies
        /// the system, and the registry always has the true one.
        /// </summary>
        static string DescribeWindows()
        {
            string bitness = IntPtr.Size == 8 ? " (64-bit)" : " (32-bit)";

            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                           @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        string product = key.GetValue("ProductName") as string;
                        string display = key.GetValue("DisplayVersion") as string;
                        string build = key.GetValue("CurrentBuild") as string;
                        object update = key.GetValue("UBR");

                        if (product != null)
                        {
                            // Windows 11 still calls itself "Windows 10 Pro" in this key.
                            // The build number is the only honest field: 22000 and above
                            // is 11, and reporting otherwise sends people looking for
                            // problems in the wrong operating system.
                            int buildNumber;
                            if (build != null && int.TryParse(build, out buildNumber) && buildNumber >= 22000)
                                product = product.Replace("Windows 10", "Windows 11");

                            string described = product;
                            if (display != null) described += " " + display;
                            if (build != null) described += " (build " + build +
                                                            (update != null ? "." + update : "") + ")";
                            return described + bitness;
                        }
                    }
                }
            }
            catch { }

            return Environment.OSVersion.VersionString + bitness;
        }

        static string ReadStoredLog()
        {
            try
            {
                if (!File.Exists(AppPaths.LogFile)) return "";
                using (FileStream stream = new FileStream(AppPaths.LogFile, FileMode.Open,
                                                          FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
            catch (Exception error) { return "  log file could not be read: " + error.Message + Environment.NewLine; }
        }
    }
}
