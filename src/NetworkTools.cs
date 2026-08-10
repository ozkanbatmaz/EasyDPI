using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace EasyDPI
{
    /// <summary>
    /// Network diagnostics: finding the active adapter, switching DNS, resolving names
    /// over encrypted DNS, and measuring whether a domain is actually being blocked.
    /// </summary>
    static class NetworkTools
    {
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
        /// Connects to an address and completes a TLS handshake using the given host name
        /// as SNI. Inspection systems that watch for SNI inject a reset here, so a failed
        /// handshake against a known-good address is a reliable signal of blocking.
        ///
        /// The TLS versions must be listed explicitly. AuthenticateAsClient(host) negotiates
        /// with an ancient default that ServicePointManager.SecurityProtocol does not affect,
        /// and servers requiring TLS 1.2 then fail in a way that looks exactly like blocking.
        /// </summary>
        public static bool CanCompleteTlsHandshake(string ipAddress, string hostName, int timeoutMs)
        {
            TcpClient client = new TcpClient();
            try
            {
                IAsyncResult connection = client.BeginConnect(ipAddress, 443, null, null);
                if (!connection.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                client.EndConnect(connection);

                using (SslStream secureStream = new SslStream(client.GetStream(), false, delegate { return true; }))
                {
                    secureStream.AuthenticateAsClient(hostName, null,
                        SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                    return secureStream.IsAuthenticated;
                }
            }
            catch { return false; }
            finally { try { client.Close(); } catch { } }
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
