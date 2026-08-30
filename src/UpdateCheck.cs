using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace EasyDPI
{
    /// <summary>
    /// Asks GitHub whether a newer release exists.
    ///
    /// The application has no installer and no update service, so without this the only
    /// way anyone learns that a fix exists is by going back to the project page and
    /// noticing. That is a poor way to ship a fix: 1.1.1 repaired a failure that left
    /// the encrypted resolver dead on exactly the networks this tool is for, and every
    /// copy of 1.1.0 in the wild had no way of finding out.
    ///
    /// The update is downloaded and installed in place, but never without asking and
    /// never without checking: the archive is verified against the SHA-256 the API
    /// publishes for it before a single file is replaced, and an update that fails that
    /// check is discarded. An unsigned executable fetched over a connection that is by
    /// definition being interfered with is exactly the thing this application exists to
    /// worry about, so the checksum is not optional and there is no fallback that skips it.
    ///
    /// The check can be turned off with updateCheck=0 in config.ini, and turning it off
    /// leaves the application making no network calls of its own at all.
    /// </summary>
    static class UpdateCheck
    {
        const string LatestReleaseApi =
            "https://api.github.com/repos/ozkanbatmaz/EasyDPI/releases/latest";

        public sealed class Release
        {
            public string Version;      // "1.3.0"
            public string PageUrl;      // the release page, for when installing is not possible
            public string ArchiveUrl;   // the .zip asset
            public string ArchiveName;
            public long ArchiveSize;

            /// <summary>
            /// The asset's SHA-256 as GitHub reports it. Nothing is installed without it:
            /// an update that cannot be checked against the value the API published is an
            /// executable of unknown provenance, and this application is the wrong place
            /// to be relaxed about that.
            /// </summary>
            public string Sha256;

            public bool CanInstall
            {
                get
                {
                    return !string.IsNullOrEmpty(ArchiveUrl) &&
                           !string.IsNullOrEmpty(Sha256);
                }
            }
        }

        /// <summary>
        /// Runs the check on its own thread and calls back only when there is something
        /// worth saying. Failures are silent on purpose: an unreachable GitHub is the
        /// normal state of affairs on a blocked network and is not the user's problem
        /// to solve.
        /// </summary>
        public static void InBackground(Action<Release> onNewerFound)
        {
            Thread worker = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Release release = FindLatest();
                    if (release == null) return;
                    if (Compare(release.Version, AppInfo.Version) <= 0) return;

                    onNewerFound(release);
                }
                catch { }
            }));

            worker.IsBackground = true;
            worker.Start();
        }

        public static Release FindLatest()
        {
            try
            {
                // Set explicitly rather than inherited from whatever ran first: the
                // framework default still offers TLS 1.0, which GitHub refuses outright,
                // and the failure looks exactly like "no newer version".
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
                request.Timeout = 12000;
                request.ReadWriteTimeout = 12000;
                request.Proxy = null;
                request.Accept = "application/vnd.github+json";
                // GitHub rejects requests without one. It carries no version and no
                // identifier, so the request says nothing except that somebody asked.
                request.UserAgent = "EasyDPI";

                string body;
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    body = reader.ReadToEnd();

                Match tag = Regex.Match(body, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                if (!tag.Success) return null;

                Release release = new Release();
                release.Version = tag.Groups[1].Value.TrimStart('v', 'V');

                // Assets carry html_url fields of their own, so the release page is
                // matched by shape rather than by being the first one in the document.
                Match page = Regex.Match(body,
                    "\"html_url\"\\s*:\\s*\"(https://github\\.com/[^\"]+/releases/tag/[^\"]+)\"");

                release.PageUrl = page.Success
                    ? page.Groups[1].Value
                    : "https://github.com/ozkanbatmaz/EasyDPI/releases/latest";

                ReadArchiveAsset(body, release);
                return release;
            }
            catch { return null; }
        }

        /// <summary>
        /// Picks the .zip out of the release's assets.
        ///
        /// The assets are walked as whole objects rather than scraped field by field:
        /// a release carries several of them and every one has a name, a size and a URL,
        /// so matching fields across the whole document would happily pair one asset's
        /// name with another's checksum.
        /// </summary>
        static void ReadArchiveAsset(string body, Release release)
        {
            int listStart = body.IndexOf("\"assets\"");
            if (listStart < 0) return;

            int bracket = body.IndexOf('[', listStart);
            if (bracket < 0) return;

            int depth = 0;
            int objectStart = -1;

            for (int index = bracket; index < body.Length; index++)
            {
                char character = body[index];

                if (character == ']' && depth == 0) break;

                if (character == '{')
                {
                    if (depth == 0) objectStart = index;
                    depth++;
                }
                else if (character == '}')
                {
                    depth--;
                    if (depth != 0 || objectStart < 0) continue;

                    string asset = body.Substring(objectStart, index - objectStart + 1);
                    objectStart = -1;

                    string name = Field(asset, "name");
                    if (name == null || !name.ToLowerInvariant().EndsWith(".zip")) continue;

                    release.ArchiveName = name;
                    release.ArchiveUrl = Field(asset, "browser_download_url");

                    string digest = Field(asset, "digest");
                    if (digest != null && digest.StartsWith("sha256:"))
                        release.Sha256 = digest.Substring(7).ToLowerInvariant();

                    Match size = Regex.Match(asset, @"""size""\s*:\s*([0-9]+)");
                    if (size.Success)
                    {
                        long value;
                        if (long.TryParse(size.Groups[1].Value, out value)) release.ArchiveSize = value;
                    }
                    return;
                }
            }
        }

        static string Field(string json, string name)
        {
            Match match = Regex.Match(json, "\"" + name + "\"" + @"\s*:\s*" + "\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Compares dotted version numbers. Returns a positive number when the left side
        /// is newer. Anything unparseable counts as zero, so a malformed tag can never
        /// look newer than a real version.
        /// </summary>
        public static int Compare(string left, string right)
        {
            string[] leftParts = (left ?? "").Split('.');
            string[] rightParts = (right ?? "").Split('.');
            int length = Math.Max(leftParts.Length, rightParts.Length);

            for (int index = 0; index < length; index++)
            {
                int a = PartAt(leftParts, index);
                int b = PartAt(rightParts, index);
                if (a != b) return a - b;
            }
            return 0;
        }

        static int PartAt(string[] parts, int index)
        {
            if (index >= parts.Length) return 0;

            int value;
            if (int.TryParse(parts[index].Trim(), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out value))
                return value;

            return 0;
        }
    }
}
