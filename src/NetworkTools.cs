using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace EasyDPI
{
    /// <summary>
    /// Network diagnostics: finding the active adapter, switching DNS, resolving names
    /// over encrypted DNS, and measuring whether a domain is actually being blocked.
    /// </summary>
    static class NetworkTools
    {
        /// <summary>
        /// Probes identify as a browser. Some providers hand a plain "no user agent"
        /// request a different answer than a browser gets, which would make the
        /// measurement describe a request nobody actually makes.
        /// </summary>
        const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

        static NetworkTools()
        {
            try
            {
                // The framework default still offers TLS 1.0 first, which a growing number
                // of sites refuse outright — indistinguishable from blocking in a measurement.
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.Expect100Continue = false;
                // Probes run in parallel; the default of two connections per host would
                // serialise them behind each other.
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            catch { }
        }

        // ---------------------------------------------------------------
        // Adapter
        // ---------------------------------------------------------------

        /// <summary>
        /// Names that appear in the description of a VPN's virtual adapter. The list is
        /// matched loosely, because every client names its adapter differently and a
        /// missed one is not a cosmetic problem: it is how DNS ends up being written to
        /// a tunnel that disappears when the VPN disconnects.
        /// </summary>
        static readonly string[] VpnAdapterMarkers =
        {
            "wireguard", "wintun", "tap-windows", "tap adapter", "openvpn", "wan miniport",
            "vpn", "nordlynx", "proton", "mullvad", "expressvpn", "surfshark", "cloudflare warp",
            "tailscale", "zerotier", "softether", "tunnel", "tun interface", "utun"
        };

        static bool LooksLikeVpn(NetworkInterface adapter)
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return true;
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Ppp) return true;

            string label = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
            foreach (string marker in VpnAdapterMarkers)
                if (label.Contains(marker)) return true;

            return false;
        }

        static bool LooksVirtual(NetworkInterface adapter)
        {
            string label = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
            return label.Contains("virtual") || label.Contains("vmware") || label.Contains("hyper-v") ||
                   label.Contains("vethernet") || label.Contains("loopback") || label.Contains("bluetooth");
        }

        /// <summary>
        /// Whether a VPN tunnel is currently up.
        ///
        /// This matters because the two tools do overlapping work. A VPN already carries
        /// traffic past the provider's inspection, and the packet tricks that defeat that
        /// inspection are computed for the route between this machine and the equipment
        /// doing the inspecting — a route the traffic no longer takes once it is inside a
        /// tunnel. Fake packets in particular are aimed at a specific number of hops away;
        /// sent through a tunnel they can reach the real server instead of dying on the way,
        /// which breaks the connection rather than saving it.
        /// </summary>
        public static bool IsVpnActive()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (!LooksLikeVpn(adapter)) continue;

                    // A VPN adapter that exists but carries no route is a client sitting
                    // idle, not a tunnel in use.
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    if (properties.GatewayAddresses != null && properties.GatewayAddresses.Count > 0)
                        return true;

                    foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>Description of the active tunnel, for the log. Null when there is none.</summary>
        public static string DescribeVpnAdapter()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (LooksLikeVpn(adapter)) return adapter.Description;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Finds the adapter that actually reaches the internet, skipping virtual adapters
        /// and VPN tunnels.
        ///
        /// Excluding tunnels is the point. Pointing a VPN adapter's DNS at 127.0.0.1 puts
        /// the setting somewhere that vanishes the moment the VPN disconnects, and leaves
        /// the real adapter still resolving through the provider in the meantime.
        /// </summary>
        public static NetworkInterface FindActiveAdapter()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (LooksLikeVpn(adapter)) continue;
                    if (LooksVirtual(adapter)) continue;

                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    if (properties.GatewayAddresses == null || properties.GatewayAddresses.Count == 0) continue;

                    return adapter;
                }
            }
            catch { }
            return null;
        }

        public static string DescribeCurrentDns()
        {
            NetworkInterface adapter = FindActiveAdapter();
            if (adapter == null) return Strings.Get("dns.noAdapter");

            try
            {
                List<string> servers = new List<string>();
                foreach (IPAddress address in adapter.GetIPProperties().DnsAddresses)
                    servers.Add(address.ToString());

                if (servers.Count == 0) return Strings.Get("dns.notSet");
                return string.Join(", ", servers.ToArray());
            }
            catch { return Strings.Get("dns.unreadable"); }
        }

        /// <summary>
        /// Points DNS at the local encrypted resolver.
        ///
        /// IPv6 matters here: if the router's link-local DNS address is still configured,
        /// Windows will happily query it and get the provider's tampered answers back,
        /// even though IPv4 is pointed at localhost.
        /// </summary>
        public static void PointDnsToLocalhost()
        {
            NetworkInterface adapter = FindActiveAdapter();
            if (adapter == null) return;

            string name = adapter.Name;
            ProcessRunner.Netsh("interface ipv4 set dnsservers name=\"" + name + "\" source=static address=127.0.0.1 register=primary validate=no");
            ProcessRunner.Netsh("interface ipv6 set dnsservers name=\"" + name + "\" source=static address=::1 register=primary validate=no");
            ProcessRunner.FlushDnsCache();
        }

        /// <summary>
        /// Hands DNS back to the router or provider.
        ///
        /// Only adapters we actually changed are touched, and they are recognised by what
        /// they are set to: an adapter whose DNS server is 127.0.0.1 or ::1 was pointed
        /// there by this application, because nothing else has a reason to. The previous
        /// version forced every adapter on the machine back to DHCP, which also wiped the
        /// DNS settings of any VPN that happened to be connected — a tool that quietly
        /// undoes another tool's configuration on the way out.
        /// </summary>
        public static void RestoreDefaultDns()
        {
            bool restoredSomething = false;

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (!PointsAtLocalResolver(adapter)) continue;

                    RestoreAdapter(adapter.Name);
                    restoredSomething = true;
                }
            }
            catch { }

            // A machine that was never pointed at the local resolver has nothing to undo;
            // one where the adapter has since been renamed or replaced does, and the
            // fallback keeps that case from leaving a dead setting behind.
            if (!restoredSomething)
            {
                NetworkInterface adapter = FindActiveAdapter();
                if (adapter != null) RestoreAdapter(adapter.Name);
            }

            ProcessRunner.FlushDnsCache();
        }

        static void RestoreAdapter(string name)
        {
            ProcessRunner.Netsh("interface ipv4 set dnsservers name=\"" + name + "\" source=dhcp");
            ProcessRunner.Netsh("interface ipv6 set dnsservers name=\"" + name + "\" source=dhcp");
        }

        /// <summary>Whether this adapter is resolving through the resolver we install.</summary>
        static bool PointsAtLocalResolver(NetworkInterface adapter)
        {
            try
            {
                foreach (IPAddress address in adapter.GetIPProperties().DnsAddresses)
                    if (IPAddress.IsLoopback(address)) return true;
            }
            catch { }
            return false;
        }

        // ---------------------------------------------------------------
        // Reachability
        // ---------------------------------------------------------------

        /// <summary>
        /// Fetches the head of a page over HTTPS and reports whether the request
        /// completed, along with how long it took.
        ///
        /// This is a stricter test than a bare TLS handshake, and measurably so: on the
        /// network this was written against, several hosts (a Roblox CDN edge, Bing)
        /// failed a raw handshake from a hand-rolled SslStream while a normal HTTPS
        /// request to the same name succeeded every time. A tuner scoring on handshakes
        /// would have called those sites blocked and chased a setting to "fix" them.
        /// Asking for a page the way an application does removes that whole class of
        /// false positives, and answers the question that actually matters: can a
        /// program on this machine talk to this service?
        ///
        /// An HTTP error status is success. 403 and 404 come from the server, so the
        /// connection plainly got there; only a transport failure — refused, reset,
        /// timed out, cut off mid-handshake — counts as blocked. Certificate validation
        /// is deliberately left on: an interceptor presenting its own certificate is
        /// not a working connection and should not be scored as one.
        /// </summary>
        public static bool ProbeHttps(string hostName, int timeoutMs, out int elapsedMs)
        {
            Stopwatch watch = Stopwatch.StartNew();
            WebResponse response = null;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://" + hostName + "/");
                request.Method = "HEAD";
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.AllowAutoRedirect = false;
                request.KeepAlive = false;
                // Without this Windows looks for a proxy configuration script first,
                // which can add seconds to every single probe.
                request.Proxy = null;
                request.UserAgent = BrowserUserAgent;

                response = request.GetResponse();
                watch.Stop();
                elapsedMs = (int)watch.ElapsedMilliseconds;
                return true;
            }
            catch (WebException failure)
            {
                watch.Stop();
                elapsedMs = (int)watch.ElapsedMilliseconds;
                if (failure.Response != null) { try { failure.Response.Close(); } catch { } }
                return failure.Response != null;
            }
            catch
            {
                watch.Stop();
                elapsedMs = (int)watch.ElapsedMilliseconds;
                return false;
            }
            finally { if (response != null) { try { response.Close(); } catch { } } }
        }

        /// <summary>
        /// Downloads a fixed block of data and reports the rate in kilobits per second,
        /// or zero if the measurement could not be taken.
        ///
        /// Latency alone does not separate the candidate settings. Splitting packets and
        /// shrinking payloads costs throughput rather than round-trip time, so two
        /// settings that both connect quickly can differ by a wide margin once real data
        /// starts moving. Cloudflare's speed endpoint is used because it serves an exact
        /// requested byte count, which makes the number comparable between runs.
        /// </summary>
        public static int MeasureDownloadKbps(int byteCount, int timeoutMs)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                    "https://speed.cloudflare.com/__down?bytes=" + byteCount);
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.KeepAlive = false;
                request.Proxy = null;
                request.UserAgent = BrowserUserAgent;

                Stopwatch watch = Stopwatch.StartNew();
                long received = 0;

                using (WebResponse response = request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    byte[] buffer = new byte[16384];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        received += read;
                        if (watch.ElapsedMilliseconds > timeoutMs) break;
                    }
                }

                watch.Stop();
                long elapsed = Math.Max(1, watch.ElapsedMilliseconds);
                if (received < byteCount / 4) return 0;
                return (int)((received * 8) / elapsed);
            }
            catch { return 0; }
        }

        // ---------------------------------------------------------------
        // Name resolution
        // ---------------------------------------------------------------

        /// <summary>
        /// Resolves over DNS-over-HTTPS. Because the query travels inside a TLS session,
        /// providers cannot read or filter it, which gives us a trustworthy answer to
        /// compare the system resolver against.
        /// </summary>
        public static List<string> ResolveEncrypted(string hostName)
        {
            List<string> addresses = QueryDnsOverHttps(
                "https://cloudflare-dns.com/dns-query?name=" + Uri.EscapeDataString(hostName) + "&type=A");

            if (addresses.Count == 0)
                addresses = QueryDnsOverHttps(
                    "https://dns.google/resolve?name=" + Uri.EscapeDataString(hostName) + "&type=A");

            return addresses;
        }

        static List<string> QueryDnsOverHttps(string url)
        {
            List<string> addresses = new List<string>();
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Accept = "application/dns-json";
                request.UserAgent = "EasyDPI";
                request.Timeout = 12000;
                request.ReadWriteTimeout = 12000;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string body = reader.ReadToEnd();

                    foreach (Match record in Regex.Matches(body, "\\{[^{}]*\\}"))
                    {
                        // Type 1 is an A record. The question section also carries type 1
                        // but has no "data" field, so it drops out naturally below.
                        if (!Regex.IsMatch(record.Value, "\"type\"\\s*:\\s*1\\b")) continue;

                        Match data = Regex.Match(record.Value,
                            "\"data\"\\s*:\\s*\"([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\"");

                        if (data.Success && !addresses.Contains(data.Groups[1].Value))
                            addresses.Add(data.Groups[1].Value);
                    }
                }
            }
            catch { }
            return addresses;
        }

        /// <summary>Resolves through Windows, i.e. through whatever the provider serves us.</summary>
        public static string ResolveWithSystemDns(string hostName)
        {
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(hostName))
                    if (address.AddressFamily == AddressFamily.InterNetwork) return address.ToString();
            }
            catch { }
            return null;
        }
    }
}
