using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace EasyDPI
{
    /// <summary>
    /// Installs a newer release over this one.
    ///
    /// The sequence exists to survive the awkward fact that an application cannot
    /// overwrite itself while it is running:
    ///
    ///   1. download the release archive to a temporary folder
    ///   2. verify it against the SHA-256 the API published for that asset
    ///   3. unpack it, and check the unpacked copy actually contains an application
    ///   4. hand a copy command to cmd.exe, which waits for this process to exit
    ///   5. close, let the copy happen, and start the new version
    ///
    /// Nothing is touched in the installation folder until step four, so a download that
    /// fails, a checksum that does not match, or an archive that unpacks into something
    /// unexpected all end with the existing installation exactly as it was.
    ///
    /// The verification is the part worth being strict about. This tool is used on
    /// connections that are being actively interfered with, which is precisely where an
    /// unverified binary download is a bad idea, so a missing or mismatched checksum
    /// aborts the update rather than falling back to installing it anyway.
    /// </summary>
    static class Updater
    {
        /// <summary>
        /// Downloads and stages the update. Returns the folder holding the unpacked new
        /// version, or null if anything went wrong — in which case nothing was changed.
        /// </summary>
        public static string Stage(UpdateCheck.Release release, Action<string> report)
        {
            string workspace = Path.Combine(Path.GetTempPath(),
                                            "easydpi-update-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(workspace);

                string archive = Path.Combine(workspace, release.ArchiveName ?? "update.zip");

                report(Strings.Get("update.downloading", release.Version));
                if (!Download(release.ArchiveUrl, archive))
                {
                    report(Strings.Get("update.downloadFailed"));
                    Discard(workspace);
                    return null;
                }

                report(Strings.Get("update.verifying"));
                string actual = Sha256Of(archive);

                if (actual == null || actual != release.Sha256)
                {
                    // Either the download was damaged or it is not the file GitHub
                    // published. Both mean the same thing here: do not install it.
                    report(Strings.Get("update.checksumFailed"));
                    Discard(workspace);
                    return null;
                }

                string unpacked = Path.Combine(workspace, "unpacked");
                ZipFile.ExtractToDirectory(archive, unpacked);

                // An archive that does not contain the application is not an update,
                // whatever it claims to be.
                if (!File.Exists(Path.Combine(unpacked, "EasyDPI.exe")))
                {
                    report(Strings.Get("update.contentsUnexpected"));
                    Discard(workspace);
                    return null;
                }

                try { File.Delete(archive); } catch { }
                return unpacked;
            }
            catch (Exception error)
            {
                report(Strings.Get("update.failed", error.Message));
                Discard(workspace);
                return null;
            }
        }

        /// <summary>
        /// Hands the swap to cmd.exe and returns. The caller must close the application
        /// immediately: the copy waits a few seconds for this process to let go of its
        /// own executable, and the services are stopped for the same reason — the bypass
        /// engine holds bin\goodbyedpi.exe open while it runs.
        ///
        /// Whatever was running is started again afterwards, so an update does not
        /// quietly leave the machine unprotected.
        /// </summary>
        public static bool ScheduleSwap(string unpackedFolder, bool wasProtected)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe",
                    BuildSwapCommand(unpackedFolder, AppPaths.Root, wasProtected));

                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = Path.GetTempPath();

                Process.Start(startInfo);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// The command that replaces the installation. Separated from starting it so the
        /// exact string can be exercised against a scratch folder rather than only ever
        /// being run for real, once, on somebody's machine.
        /// </summary>
        public static string BuildSwapCommand(string unpackedFolder, string root, bool wasProtected)
        {
            root = root.TrimEnd(Path.DirectorySeparatorChar);
            string workspace = Path.GetDirectoryName(unpackedFolder);

            StringBuilder command = new StringBuilder();
            command.Append("/c ping -n 3 127.0.0.1 >nul");
            command.Append(" & sc stop ").Append(ServiceManager.BypassService).Append(" >nul 2>&1");
            command.Append(" & sc stop ").Append(ServiceManager.DnsService).Append(" >nul 2>&1");
            command.Append(" & ping -n 3 127.0.0.1 >nul");

            // /E all folders, /Y overwrite without asking, /I treat the target as a
            // folder. config.ini is not in the archive, so settings survive the copy.
            AppendCopy(command, unpackedFolder, root);

            // Once more, a moment later. The first pass can arrive while Windows is
            // still releasing the old executable, and a copy that skipped EasyDPI.exe
            // would leave the new files running under the old application.
            command.Append(" & ping -n 3 127.0.0.1 >nul");
            AppendCopy(command, unpackedFolder, root);

            if (wasProtected)
            {
                command.Append(" & sc start ").Append(ServiceManager.DnsService).Append(" >nul 2>&1");
                command.Append(" & sc start ").Append(ServiceManager.BypassService).Append(" >nul 2>&1");
            }

            command.Append(" & start \"\" \"").Append(Path.Combine(root, "EasyDPI.exe")).Append("\"");
            command.Append(" & rd /s /q \"").Append(workspace).Append("\"");

            return command.ToString();
        }

        static void AppendCopy(StringBuilder command, string from, string to)
        {
            command.Append(" & xcopy \"").Append(from).Append("\\*\" \"")
                   .Append(to).Append("\\\" /E /Y /I /Q >nul");
        }

        // ---------------------------------------------------------------

        static bool Download(string url, string destination)
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 30000;
                request.ReadWriteTimeout = 60000;
                request.Proxy = null;
                request.UserAgent = "EasyDPI";
                request.AllowAutoRedirect = true;   // the asset lives on a separate host

                using (WebResponse response = request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[65536];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        output.Write(buffer, 0, read);
                }

                return new FileInfo(destination).Length > 0;
            }
            catch { return false; }
        }

        static string Sha256Of(string path)
        {
            try
            {
                using (SHA256 algorithm = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    byte[] hash = algorithm.ComputeHash(stream);
                    StringBuilder text = new StringBuilder(hash.Length * 2);
                    foreach (byte value in hash) text.Append(value.ToString("x2"));
                    return text.ToString();
                }
            }
            catch { return null; }
        }

        static void Discard(string folder)
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            catch { }
        }
    }
}
