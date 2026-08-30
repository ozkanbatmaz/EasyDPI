using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EasyDPI
{
    /// <summary>Identity of this build. Reported in diagnostics so a bug report says which one it is.</summary>
    static class AppInfo
    {
        static string version;

        /// <summary>
        /// Read from the assembly rather than kept as a constant here, so that the number
        /// in a diagnostic report is by construction the number in the file's properties.
        /// Two places to edit is one place too many for something the update check
        /// compares against.
        /// </summary>
        public static string Version
        {
            get
            {
                if (version != null) return version;

                try
                {
                    System.Version assembly = System.Reflection.Assembly
                        .GetExecutingAssembly().GetName().Version;

                    version = assembly.Major + "." + assembly.Minor + "." + assembly.Build;
                }
                catch { version = "0.0.0"; }

                return version;
            }
        }
    }

    /// <summary>
    /// Every path is derived from the folder EasyDPI.exe lives in, so the whole
    /// application stays portable — copy the folder anywhere and it still works.
    /// </summary>
    static class AppPaths
    {
        public static string Root
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\"; }
        }

        public static string BinFolder { get { return Path.Combine(Root, "bin"); } }
        public static string DnsFolder { get { return Path.Combine(Root, "dns"); } }

        public static string GoodbyeDpiExe { get { return Path.Combine(BinFolder, "goodbyedpi.exe"); } }
        public static string DnscryptExe { get { return Path.Combine(DnsFolder, "dnscrypt-proxy.exe"); } }
        public static string DnscryptConfig { get { return Path.Combine(DnsFolder, "dnscrypt-proxy.toml"); } }

        public static string ConfigFile { get { return Path.Combine(BinFolder, "config.ini"); } }
        public static string LogFile { get { return Path.Combine(Root, "easydpi.log"); } }
    }

    /// <summary>
    /// Persisted configuration. Written by the automatic tuner and, for the language,
    /// by the onboarding screen. Users can also edit config.ini by hand.
    /// </summary>
    static class Settings
    {
        /// <summary>Command line passed to GoodbyeDPI. Chosen by the tuner.</summary>
        public static string BypassArguments = "-5 -q --frag-by-sni";

        /// <summary>Whether this network needs encrypted DNS to resolve names correctly.</summary>
        public static bool UseEncryptedDns = true;

        /// <summary>UI language: a two-letter code, or "auto" to follow the operating system.</summary>
        public static string Language = "auto";

        /// <summary>
        /// Domains the tuner uses to detect blocking. Empty means "use the built-in list".
        /// Users on networks the defaults do not cover can list their own targets here,
        /// which is what makes the tuner useful outside the regions we tested.
        /// </summary>
        public static List<string> CustomProbeDomains = new List<string>();

        /// <summary>
        /// Whether to ask GitHub about newer releases on startup. Turning this off leaves
        /// the application making no network calls of its own.
        /// </summary>
        public static bool CheckForUpdates = true;

        /// <summary>
        /// The newest version the user has already been told about, so the prompt appears
        /// once per release rather than at every launch.
        /// </summary>
        public static string UpdateNotifiedVersion = "";

        /// <summary>True when no configuration exists yet, i.e. this is the first launch.</summary>
        public static bool IsFirstRun { get { return !File.Exists(AppPaths.ConfigFile); } }

        public static void Load()
        {
            try
            {
                if (!File.Exists(AppPaths.ConfigFile)) return;

                foreach (string line in File.ReadAllLines(AppPaths.ConfigFile))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#")) continue;

                    int separator = trimmed.IndexOf('=');
                    if (separator <= 0) continue;

                    string key = trimmed.Substring(0, separator).Trim().ToLowerInvariant();
                    string value = trimmed.Substring(separator + 1).Trim();

                    if (key == "arguments" && value.Length > 0) BypassArguments = value;
                    else if (key == "encrypteddns") UseEncryptedDns = IsTruthy(value);
                    else if (key == "language" && value.Length > 0) Language = value;
                    else if (key == "probedomains") CustomProbeDomains = SplitDomains(value);
                    else if (key == "updatecheck") CheckForUpdates = IsTruthy(value);
                    else if (key == "updatenotified") UpdateNotifiedVersion = value;
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine("# EasyDPI configuration");
                text.AppendLine("# Updated automatically when you run \"find the best settings\".");
                text.AppendLine();
                text.AppendLine("# GoodbyeDPI command line arguments.");
                text.AppendLine("arguments=" + BypassArguments);
                text.AppendLine();
                text.AppendLine("# 1 = route DNS through the local encrypted resolver, 0 = leave DNS alone.");
                text.AppendLine("encryptedDns=" + (UseEncryptedDns ? "1" : "0"));
                text.AppendLine();
                text.AppendLine("# UI language: auto, or a two-letter code (" + string.Join(", ", Strings.AvailableLanguages) + ").");
                text.AppendLine("language=" + Language);
                text.AppendLine();
                text.AppendLine("# Optional. Comma separated domains to test for blocking on your network.");
                text.AppendLine("# Leave empty to use the built-in list. Add the sites you actually need,");
                text.AppendLine("# for example: probeDomains=example.com, another.org");
                text.AppendLine("probeDomains=" + string.Join(", ", CustomProbeDomains.ToArray()));
                text.AppendLine();
                text.AppendLine("# 1 = check GitHub for a newer release when the window opens, 0 = never.");
                text.AppendLine("# With this off, EasyDPI makes no network calls of its own.");
                text.AppendLine("updateCheck=" + (CheckForUpdates ? "1" : "0"));
                text.AppendLine();
                text.AppendLine("# The release you were last told about. Cleared by hand if you want the");
                text.AppendLine("# notice again.");
                text.AppendLine("updateNotified=" + UpdateNotifiedVersion);

                string folder = Path.GetDirectoryName(AppPaths.ConfigFile);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                File.WriteAllText(AppPaths.ConfigFile, text.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static bool IsTruthy(string value)
        {
            string v = value.ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes";
        }

        static List<string> SplitDomains(string value)
        {
            List<string> domains = new List<string>();
            foreach (string part in value.Split(new char[] { ',', ';', ' ' }))
            {
                string domain = part.Trim().ToLowerInvariant();
                if (domain.Length > 0 && domain.Contains(".") && !domains.Contains(domain))
                    domains.Add(domain);
            }
            return domains;
        }
    }
}
