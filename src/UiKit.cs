using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// Shared colours, fonts and drawing helpers so every surface in the application
    /// looks like it came from the same place.
    /// </summary>
    static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(245, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(228, 233, 240);
        public static readonly Color Divider = Color.FromArgb(235, 239, 245);

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);

        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentBright = Color.FromArgb(59, 130, 246);
        public static readonly Color AccentSoft = Color.FromArgb(219, 232, 254);
        public static readonly Color AccentTint = Color.FromArgb(238, 243, 255);

        public static readonly Color Success = Color.FromArgb(22, 163, 74);
        public static readonly Color SuccessTint = Color.FromArgb(236, 253, 243);

        // Reserved for actions that cannot be undone. Muted on purpose: a destructive
        // control should be recognisable without competing with the primary button.
        public static readonly Color Danger = Color.FromArgb(190, 40, 40);
        public static readonly Color DangerTint = Color.FromArgb(254, 243, 243);
        public static readonly Color DangerBorder = Color.FromArgb(246, 205, 205);

        public static readonly Color HeroFrom = Color.FromArgb(246, 250, 255);
        public static readonly Color HeroTo = Color.FromArgb(232, 240, 253);

        public static Font Regular(float size) { return new Font("Segoe UI", size); }
        public static Font Semibold(float size) { return new Font("Segoe UI Semibold", size); }
        public static Font Bold(float size) { return new Font("Segoe UI", size, FontStyle.Bold); }

        /// <summary>Rounded rectangle path. Used for every card, pill and button.</summary>
        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;

            if (d <= 0 || d > Math.Min(r.Width, r.Height))
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, RectangleF r, float radius, Color fill)
        {
            using (GraphicsPath p = Rounded(r, radius))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, p);
        }

        public static void FillRoundedGradient(Graphics g, RectangleF r, float radius, Color from, Color to, float angle)
        {
            using (GraphicsPath p = Rounded(r, radius))
            using (LinearGradientBrush b = new LinearGradientBrush(r, from, to, angle))
                g.FillPath(b, p);
        }

        public static void DrawCard(Graphics g, RectangleF r, float radius, Color fill, Color border)
        {
            using (GraphicsPath p = Rounded(r, radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
        }

        /// <summary>Crisp text. GDI rendering reads better than GDI+ at interface sizes.</summary>
        public static void Text(Graphics g, string text, Font font, Color colour, int x, int y)
        {
            TextRenderer.DrawText(g, text, font, new Point(x, y), colour, TextFormatFlags.NoPadding);
        }

        public static Size Measure(string text, Font font)
        {
            return TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        }

        /// <summary>Small rounded label with a coloured dot, as used for the protection state.</summary>
        public static void DrawStatusPill(Graphics g, RectangleF bounds, string text, Font font, Color dot, Color textColour, Color fill)
        {
            FillRounded(g, bounds, bounds.Height / 2f, fill);

            float dotSize = 8f;
            float dotX = bounds.X + 14f;
            float dotY = bounds.Y + (bounds.Height - dotSize) / 2f;

            using (SolidBrush b = new SolidBrush(dot))
                g.FillEllipse(b, dotX, dotY, dotSize, dotSize);

            Size size = Measure(text, font);
            Text(g, text, font, textColour,
                 (int)(dotX + dotSize + 8f),
                 (int)(bounds.Y + (bounds.Height - size.Height) / 2f));
        }
    }

    /// <summary>A rounded panel that hosts other controls. Optionally gradient filled.</summary>
    public class CardPanel : Panel
    {
        public Color FillFrom = UiTheme.Surface;
        public Color FillTo = Color.Empty;      // Empty means a flat fill
        public Color BorderColour = UiTheme.Border;
        public float Radius = 14f;
        public float GradientAngle = 45f;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // Painted inset by half a pixel so the border stroke stays inside the control
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            if (FillTo.IsEmpty) UiTheme.FillRounded(g, r, Radius, FillFrom);
            else UiTheme.FillRoundedGradient(g, r, Radius, FillFrom, FillTo, GradientAngle);

            if (!BorderColour.IsEmpty)
                using (GraphicsPath p = UiTheme.Rounded(r, Radius))
                using (Pen pen = new Pen(BorderColour))
                    g.DrawPath(pen, p);

            OnPaintContent(g);
        }

        /// <summary>Hook for subclasses that draw their own content on top of the card.</summary>
        protected virtual void OnPaintContent(Graphics g) { }
    }

    /// <summary>
    /// A flat button drawn by hand, because the stock control cannot do a rounded
    /// gradient with an icon beside the label.
    /// </summary>
    public class IconButton : Control
    {
        public enum Style { Filled, Outline, Ghost, Danger }

        public Style Appearance = Style.Filled;
        public Image Icon;
        public int IconSize = 20;
        public float Radius = 12f;

        bool hovered, pressed;

        public IconButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = UiTheme.Semibold(11f);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            Color textColour;

            if (Appearance == Style.Filled)
            {
                Color from = UiTheme.Accent, to = UiTheme.AccentBright;

                if (!Enabled) { from = Color.FromArgb(178, 196, 224); to = Color.FromArgb(190, 206, 231); }
                else if (pressed) { from = Color.FromArgb(29, 78, 186); to = Color.FromArgb(37, 99, 235); }
                else if (hovered) { from = Color.FromArgb(45, 110, 245); to = Color.FromArgb(74, 143, 251); }

                UiTheme.FillRoundedGradient(g, r, Radius, from, to, 90f);
                textColour = Color.White;
            }
            else if (Appearance == Style.Outline)
            {
                Color fill = hovered ? Color.FromArgb(248, 250, 253) : UiTheme.Surface;
                UiTheme.FillRounded(g, r, Radius, fill);
                using (GraphicsPath p = UiTheme.Rounded(r, Radius))
                using (Pen pen = new Pen(UiTheme.Border))
                    g.DrawPath(pen, p);
                textColour = Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
            }
            else if (Appearance == Style.Danger)
            {
                Color fill = hovered ? UiTheme.DangerTint : UiTheme.Surface;
                UiTheme.FillRounded(g, r, Radius, fill);
                using (GraphicsPath p = UiTheme.Rounded(r, Radius))
                using (Pen pen = new Pen(UiTheme.DangerBorder))
                    g.DrawPath(pen, p);
                textColour = Enabled ? UiTheme.Danger : UiTheme.TextMuted;
            }
            else
            {
                if (hovered) UiTheme.FillRounded(g, r, Radius, UiTheme.AccentTint);
                textColour = Enabled ? UiTheme.Accent : UiTheme.TextMuted;
            }

            // Icon and label are laid out as one group and centred together
            Size textSize = UiTheme.Measure(Text, Font);
            int gap = (Icon != null && Text.Length > 0) ? 10 : 0;
            int iconW = (Icon != null) ? IconSize : 0;
            int total = iconW + gap + textSize.Width;
            int startX = (Width - total) / 2;

            if (Icon != null)
                g.DrawImage(Icon, startX, (Height - IconSize) / 2, IconSize, IconSize);

            UiTheme.Text(g, Text, Font, textColour,
                         startX + iconW + gap, (Height - textSize.Height) / 2);
        }
    }

    /// <summary>A text link that behaves like a button. Used for secondary actions.</summary>
    public class LinkText : Control
    {
        public bool Underline = false;
        public bool Centred = false;
        public Color Colour = UiTheme.Accent;
        public string Suffix = "";

        bool hovered;

        public LinkText()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = UiTheme.Regular(10f);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            string label = Text + Suffix;
            Color colour = Enabled ? Colour : UiTheme.TextMuted;

            Size size = UiTheme.Measure(label, Font);
            int y = (Height - size.Height) / 2;
            int x = Centred ? (Width - size.Width) / 2 : 0;

            UiTheme.Text(g, label, Font, colour, x, y);

            if (Underline || hovered)
                using (Pen pen = new Pen(Color.FromArgb(hovered ? 200 : 120, colour)))
                    g.DrawLine(pen, x, y + size.Height, x + size.Width, y + size.Height);
        }
    }
}
