using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// Entry point.
    ///
    /// Headless usage (run as administrator):
    ///   EasyDPI.exe /auto   measure the network, find working settings, apply them
    ///   EasyDPI.exe /on     turn protection on using the saved settings
    ///   EasyDPI.exe /off    turn protection off and restore DNS
    ///
    /// In those modes output goes to easydpi.log next to the executable.
    /// </summary>
    static class Program
    {
        [STAThread]
        static void Main(string[] arguments)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 32;

            bool firstRun = Settings.IsFirstRun;

            Settings.Load();
            Strings.Initialize(Settings.Language);

            foreach (string argument in arguments)
            {
                string flag = argument.ToLowerInvariant();
                if (flag != "/auto" && flag != "/on" && flag != "/off") continue;

                RunHeadless(flag);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool tuneOnStartup = false;

            // /intro replays the first-run introduction on demand, which is handy for
            // reviewing the wording and artwork without deleting the configuration.
            foreach (string argument in arguments)
                if (argument.ToLowerInvariant() == "/intro") firstRun = true;

            if (firstRun)
            {
                using (OnboardingWindow onboarding = new OnboardingWindow())
                {
                    onboarding.ShowDialog();
                    tuneOnStartup = onboarding.RunAutoTune;
                }
            }

            MainWindow window = new MainWindow();
            window.TuneOnStartup = tuneOnStartup;
            Application.Run(window);
        }

        static void RunHeadless(string flag)
        {
            StreamWriter log = new StreamWriter(AppPaths.LogFile, false, new UTF8Encoding(false));
            log.AutoFlush = true;

            Action<string> report = delegate(string message)
            {
                log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            };

            try
            {
                if (flag == "/auto") AutoTuner.Run(report);
                else if (flag == "/on") BypassController.TurnOn(report);
                else BypassController.TurnOff(report);
            }
            catch (Exception ex) { report(Strings.Get("log.error", ex.ToString())); }
            finally { log.Close(); }
        }
    }
}
