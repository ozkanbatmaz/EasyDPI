using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EasyDPI
{
    /// <summary>
    /// Draws the small flags shown next to language names.
    ///
    /// They are drawn with GDI+ rather than shipped as images for two reasons: it keeps
    /// the executable self-contained, and Windows does not render regional indicator
    /// emoji as flags, so the obvious shortcut is not available.
    /// </summary>
    static class FlagRenderer
    {
        static readonly Color Border = Color.FromArgb(60, 0, 0, 0);

        public static void Draw(Graphics g, string languageCode, RectangleF bounds)
        {
            SmoothingMode previous = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Region clip = g.Clip;
            g.SetClip(bounds);

            switch (languageCode)
            {
                case "tr": DrawTurkey(g, bounds); break;
                case "ru": DrawRussia(g, bounds); break;
                default: DrawUnitedKingdom(g, bounds); break;
            }

            g.Clip = clip;

            // A hairline keeps light flags from bleeding into a light background
            using (Pen pen = new Pen(Border))
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            g.SmoothingMode = previous;
        }

        static void DrawRussia(Graphics g, RectangleF r)
        {
            float band = r.Height / 3f;
            using (SolidBrush white = new SolidBrush(Color.White))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(0, 57, 166)))
            using (SolidBrush red = new SolidBrush(Color.FromArgb(213, 43, 30)))
            {
                g.FillRectangle(white, r.X, r.Y, r.Width, band);
                g.FillRectangle(blue, r.X, r.Y + band, r.Width, band);
                g.FillRectangle(red, r.X, r.Y + band * 2f, r.Width, band + 0.5f);
            }
        }

        static void DrawTurkey(Graphics g, RectangleF r)
        {
            using (SolidBrush red = new SolidBrush(Color.FromArgb(227, 10, 23)))
                g.FillRectangle(red, r);

            float cy = r.Y + r.Height / 2f;

            // Crescent: a white disc with a red disc punched out of it, offset to the right
            float outerRadius = r.Height * 0.30f;
            float innerRadius = r.Height * 0.24f;
            float outerCx = r.X + r.Width * 0.34f;
            float innerCx = r.X + r.Width * 0.40f;

            using (SolidBrush white = new SolidBrush(Color.White))
                g.FillEllipse(white, outerCx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);

            using (SolidBrush red = new SolidBrush(Color.FromArgb(227, 10, 23)))
                g.FillEllipse(red, innerCx - innerRadius, cy - innerRadius, innerRadius * 2f, innerRadius * 2f);

            // Star
            using (SolidBrush white = new SolidBrush(Color.White))
                g.FillPolygon(white, StarPoints(r.X + r.Width * 0.60f, cy, r.Height * 0.17f));
        }

        static void DrawUnitedKingdom(Graphics g, RectangleF r)
        {
            Color navy = Color.FromArgb(1, 33, 105);
            Color crimson = Color.FromArgb(200, 16, 46);

            using (SolidBrush blue = new SolidBrush(navy))
                g.FillRectangle(blue, r);

            PointF topLeft = new PointF(r.Left, r.Top);
            PointF topRight = new PointF(r.Right, r.Top);
            PointF bottomLeft = new PointF(r.Left, r.Bottom);
            PointF bottomRight = new PointF(r.Right, r.Bottom);

            // Diagonals: white saltire with a thinner red one over it. At this size the
            // real counterchanged offset is invisible, so centred strokes read correctly.
            using (Pen white = new Pen(Color.White, r.Height * 0.30f))
            {
                g.DrawLine(white, topLeft, bottomRight);
                g.DrawLine(white, topRight, bottomLeft);
            }
            using (Pen red = new Pen(crimson, r.Height * 0.13f))
            {
                g.DrawLine(red, topLeft, bottomRight);
                g.DrawLine(red, topRight, bottomLeft);
            }

            // Upright cross
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;

            using (Pen white = new Pen(Color.White, r.Height * 0.36f))
            {
                g.DrawLine(white, r.Left, cy, r.Right, cy);
                g.DrawLine(white, cx, r.Top, cx, r.Bottom);
            }
            using (Pen red = new Pen(crimson, r.Height * 0.20f))
            {
                g.DrawLine(red, r.Left, cy, r.Right, cy);
                g.DrawLine(red, cx, r.Top, cx, r.Bottom);
            }
        }

        /// <summary>Five pointed star, first point facing right, matching the Turkish flag.</summary>
        static PointF[] StarPoints(float cx, float cy, float radius)
        {
            PointF[] points = new PointF[10];
            float inner = radius * 0.42f;

            for (int i = 0; i < 10; i++)
            {
                float length = (i % 2 == 0) ? radius : inner;
                double angle = -Math.PI / 2.0 + i * Math.PI / 5.0;
                points[i] = new PointF(
                    cx + (float)(Math.Cos(angle) * length),
                    cy + (float)(Math.Sin(angle) * length));
            }
            return points;
        }
    }
}
