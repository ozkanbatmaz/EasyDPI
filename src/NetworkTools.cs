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
        /// Finds the adapter that actually reaches the internet, skipping virtual and
        /// tunnel adapters. Requiring a default gateway filters out most of the noise.
        /// </summary>
        public static NetworkInterface FindActiveAdapter()
        {
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    string label = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
                    if (label.Contains("virtual") || label.Contains("vmware") || label.Contains("hyper-v") ||
                        label.Contains("vethernet") || label.Contains("loopback") || label.Contains("bluetooth"))
                        continue;

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

        /// <summary>Hands DNS back to the router / provider (DHCP supplied).</summary>
        public static void RestoreDefaultDns()
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                string name = adapter.Name;
                ProcessRunner.Netsh("interface ipv4 set dnsservers name=\"" + name + "\" source=dhcp");
                ProcessRunner.Netsh("interface ipv6 set dnsservers name=\"" + name + "\" source=dhcp");
            }
            ProcessRunner.FlushDnsCache();
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
