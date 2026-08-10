using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// Shows the illustration at the top of an onboarding page, crossfading when the
    /// page changes so moving between steps feels continuous rather than abrupt.
    /// </summary>
    public class ArtworkPanel : Control
    {
        const int FrameIntervalMs = 16;    // ~60 fps for the length of the fade
        const int FadeDurationMs = 220;

        readonly Timer ticker;

        Image current;
        Image outgoing;
        float progress = 1f;

        public ArtworkPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            ticker = new Timer();
            ticker.Interval = FrameIntervalMs;
            ticker.Tick += new EventHandler(OnTick);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && ticker != null) { ticker.Stop(); ticker.Dispose(); }
            base.Dispose(disposing);
        }

        /// <summary>Swaps in a new illustration, fading it over the previous one.</summary>
        public void SetImage(Image image)
        {
            if (image == current) return;

            outgoing = current;
            current = image;

            if (outgoing == null) { progress = 1f; Invalidate(); return; }

            progress = 0f;
            ticker.Start();
            Invalidate();
        }

        void OnTick(object sender, EventArgs e)
        {
            progress += FrameIntervalMs / (float)FadeDurationMs;

            if (progress >= 1f)
            {
                progress = 1f;
                outgoing = null;
                ticker.Stop();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // The illustrations carry their own gradient, so the backdrop only shows
            // through in the moment before the first one is set.
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                Color.FromArgb(250, 252, 255), Color.FromArgb(227, 238, 253), 55f))
                g.FillRectangle(brush, 0, 0, Width, Height);

            if (outgoing != null) DrawCover(g, outgoing, 1f);
            if (current != null) DrawCover(g, current, progress);
        }

        /// <summary>Scales the image to cover the panel without distorting it.</summary>
        void DrawCover(Graphics g, Image image, float opacity)
        {
            float scale = Math.Max((float)Width / image.Width, (float)Height / image.Height);
            float w = image.Width * scale;
            float h = image.Height * scale;

            RectangleF target = new RectangleF((Width - w) / 2f, (Height - h) / 2f, w, h);

            if (opacity >= 0.999f)
            {
                g.DrawImage(image, target);
                return;
            }

            ColorMatrix matrix = new ColorMatrix();
            matrix.Matrix33 = Math.Max(0f, opacity);

            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                g.DrawImage(image,
                    new Rectangle((int)target.X, (int)target.Y, (int)target.Width, (int)target.Height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
