using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace EasyDPI
{
    /// <summary>
    /// Images compiled into the executable, so the application stays a single file
    /// with no artwork to lose or ship alongside it. Everything is cached after the
    /// first load; the interface asks for the same handful of images repeatedly.
    /// </summary>
    static class EmbeddedAssets
    {
        // Resource names, matching the /resource: switches in build.cmd
        public const string Logo = "logo";
        public const string HeroShield = "hero-shield";
        public const string IconService = "ic-service";
        public const string IconDns = "ic-dns";
        public const string IconGlobe = "ic-globe";
        public const string IconTune = "ic-tune";
        public const string IconLog = "ic-log";
        public const string IconGear = "ic-gear";
        public const string IconPower = "ic-power";
        public const string OnboardingWelcome = "ob-1";
        public const string OnboardingHow = "ob-2";
        public const string OnboardingReady = "ob-3";

        static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        static readonly Dictionary<string, Bitmap> ScaledCache = new Dictionary<string, Bitmap>();
        static byte[] iconBytes;

        /// <summary>Window and taskbar icon at the requested size.</summary>
        public static Icon LoadIcon(int size)
        {
            try
            {
                if (iconBytes == null)
                {
                    Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("appicon");
                    if (stream == null) return null;
                    iconBytes = ReadAll(stream);
                }
                return new Icon(new MemoryStream(iconBytes), new Size(size, size));
            }
            catch { return null; }
        }

        /// <summary>Full resolution image for the given resource name, or null if missing.</summary>
        public static Bitmap Load(string name)
        {
            try
            {
                Bitmap cached;
                if (Cache.TryGetValue(name, out cached)) return cached;

                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
                if (stream == null) return null;

                Bitmap image = new Bitmap(stream);
                Cache[name] = image;
                return image;
            }
            catch { return null; }
        }

        /// <summary>
        /// Image scaled to a square of the given size, with the result cached.
        /// Scaling once and reusing keeps repaints cheap and the edges clean.
        /// </summary>
        public static Bitmap Load(string name, int size)
        {
            string key = name + "@" + size;

            Bitmap cached;
            if (ScaledCache.TryGetValue(key, out cached)) return cached;

            Bitmap source = Load(name);
            if (source == null) return null;

            Bitmap scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(source, new Rectangle(0, 0, size, size));
            }

            ScaledCache[key] = scaled;
            return scaled;
        }

        static byte[] ReadAll(Stream stream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] chunk = new byte[8192];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0) buffer.Write(chunk, 0, read);
                return buffer.ToArray();
            }
        }
    }
}
