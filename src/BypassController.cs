using System;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace EasyDPI
{
    /// <summary>
    /// The single entry point for turning protection on and off.
    /// It drives both layers together — encrypted DNS and DPI bypass — because
    /// solving only one of them leaves the user with sites that still do not load.
    /// </summary>
    static class BypassController
    {
        public static bool IsActive
        {
            get { return ServiceManager.IsRunning(ServiceManager.BypassService); }
        }

        public static void TurnOn(Action<string> report)
        {
            report(Strings.Get("log.turningOn"));

            if (!File.Exists(AppPaths.GoodbyeDpiExe))
            {
                report(Strings.Get("log.errorMissingBinary"));
                return;
            }

            // Start from a clean slate: no old service, no leftover processes.
            ServiceManager.Stop(ServiceManager.BypassService);
            ServiceManager.KillOrphanedBypassProcesses();
            Thread.Sleep(1200);

            // A tunnel already carries traffic past the provider's inspection, so the
            // packet tricks are at best redundant there and at worst aimed at a route the
            // traffic no longer takes. Said plainly rather than left to be discovered.
            if (NetworkTools.IsVpnActive())
                report(Strings.Get("log.vpnActive", NetworkTools.DescribeVpnAdapter() ?? "VPN"));

            // Layer 1 — encrypted DNS
            if (Settings.UseEncryptedDns)
            {
                string reason;
                if (ServiceManager.TryStartDnsService(out reason))
                {
                    NetworkTools.PointDnsToLocalhost();
                    report(Strings.Get("log.encryptedDnsActive"));
                }
                else
                {
                    // Deliberately leave DNS alone here. Pointing it at a resolver that
                    // is not running would take the user's name resolution down entirely.
                    report(Strings.Get("log.dnsServiceFailed"));
                    report("   " + reason);
                    // Without it the provider's forged answers stand, and no amount of
                    // packet reshaping reaches a site whose address is already wrong.
                    report(Strings.Get("log.dnsFailedConsequence"));
                }
            }
            else
            {
                report(Strings.Get("log.dnsNotNeeded"));
            }

            // Layer 2 — DPI bypass
            ServiceManager.InstallBypassService(Settings.BypassArguments);
            ServiceManager.Start(ServiceManager.BypassService);
            ServiceManager.WaitFor(ServiceManager.BypassService, ServiceControllerStatus.Running, 15000);
            ProcessRunner.FlushDnsCache();

            if (ServiceManager.IsRunning(ServiceManager.BypassService))
                report(Strings.Get("log.turnedOn", Settings.BypassArguments));
            else
                report(Strings.Get("log.serviceFailed"));
        }

        public static void TurnOff(Action<string> report)
        {
            report(Strings.Get("log.turningOff"));

            // Disable rather than merely stop, so a reboot does not silently switch
            // protection back on after the user explicitly turned it off.
            ServiceManager.Stop(ServiceManager.BypassService);
            ServiceManager.SetStartupDisabled(ServiceManager.BypassService);
            ServiceManager.KillOrphanedBypassProcesses();

            if (ServiceManager.Exists(ServiceManager.DnsService))
            {
                ServiceManager.Stop(ServiceManager.DnsService);
                ServiceManager.SetStartupDisabled(ServiceManager.DnsService);
            }

            NetworkTools.RestoreDefaultDns();
            report(Strings.Get("log.turnedOff"));
        }
    }
}
