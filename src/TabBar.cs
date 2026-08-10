using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// The row of tabs across the top of the main window, plus a settings button on
    /// the right. Drawn by hand rather than using TabControl, whose chrome cannot be
    /// styled into anything resembling a modern interface.
    /// </summary>
    public class TabBar : Control
    {
        public class Tab
        {
            public string IconName;
            public string Text;
            public Rectangle Bounds;   // filled in while painting, reused for hit testing
        }

        readonly List<Tab> tabs = new List<Tab>();

        public event EventHandler SelectedIndexChanged;
        public event EventHandler SettingsClicked;

        int selectedIndex = 0;
        int hoveredIndex = -1;
        bool hoveredSettings = false;
        Rectangle settingsBounds = Rectangle.Empty;

        const int IconSize = 19;
        const int IconGap = 9;
        const int TabSpacing = 30;
        const int LeftPadding = 30;

        public TabBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = UiTheme.Surface;
            Font = UiTheme.Regular(10.5f);
        }

        public void AddTab(string iconName, string text)
        {
            Tab tab = new Tab();
            tab.IconName = iconName;
            tab.Text = text;
            tabs.Add(tab);
            Invalidate();
        }

        public void SetTabText(int index, string text)
        {
            if (index < 0 || index >= tabs.Count) return;
            tabs[index].Text = text;
            Invalidate();
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value == selectedIndex || value < 0 || value >= tabs.Count) return;
                selectedIndex = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hit = -1;
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Bounds.Contains(e.Location)) { hit = i; break; }

            bool overSettings = settingsBounds.Contains(e.Location);

            if (hit != hoveredIndex || overSettings != hoveredSettings)
            {
                hoveredIndex = hit;
                hoveredSettings = overSettings;
                Cursor = (hit >= 0 || overSettings) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoveredIndex = -1;
            hoveredSettings = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (settingsBounds.Contains(e.Location))
            {
                if (SettingsClicked != null) SettingsClicked(this, EventArgs.Empty);
                base.OnMouseClick(e);
                return;
            }

            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Bounds.Contains(e.Location)) { SelectedIndex = i; break; }

            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(UiTheme.Surface);

            int x = LeftPadding;
            int centreY = Height / 2 - 2;

            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                bool active = (i == selectedIndex);

                Size textSize = UiTheme.Measure(tab.Text, Font);
                int width = IconSize + IconGap + textSize.Width;

                tab.Bounds = new Rectangle(x - 8, 0, width + 16, Height - 1);

                Bitmap icon = EmbeddedAssets.Load(tab.IconName, IconSize * 2);
                if (icon != null)
                {
                    // Inactive tabs get a faded icon so the active one stands out
                    if (active) g.DrawImage(icon, x, centreY - IconSize / 2, IconSize, IconSize);
                    else DrawFaded(g, icon, new Rectangle(x, centreY - IconSize / 2, IconSize, IconSize), 0.45f);
                }

                UiTheme.Text(g, tab.Text, Font,
                             active ? UiTheme.Accent : UiTheme.TextMuted,
                             x + IconSize + IconGap, centreY - textSize.Height / 2);

                if (active)
                    using (SolidBrush b = new SolidBrush(UiTheme.Accent))
                        g.FillRectangle(b, tab.Bounds.Left, Height - 3, tab.Bounds.Width, 3);
                else if (i == hoveredIndex)
                    using (SolidBrush b = new SolidBrush(UiTheme.Border))
                        g.FillRectangle(b, tab.Bounds.Left, Height - 3, tab.Bounds.Width, 3);

                x += width + TabSpacing;
            }

            // Settings button, right aligned
            int gearSize = 19;
            settingsBounds = new Rectangle(Width - 30 - gearSize - 8, centreY - gearSize / 2 - 8, gearSize + 16, gearSize + 16);

            if (hoveredSettings)
                UiTheme.FillRounded(g, settingsBounds, 8f, UiTheme.AccentTint);

            Bitmap gear = EmbeddedAssets.Load(EmbeddedAssets.IconGear, gearSize * 2);
            if (gear != null)
                DrawFaded(g, gear, new Rectangle(settingsBounds.X + 8, settingsBounds.Y + 8, gearSize, gearSize),
                          hoveredSettings ? 0.85f : 0.5f);

            using (Pen pen = new Pen(UiTheme.Border))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }

        /// <summary>Draws an image at reduced opacity, for inactive tabs.</summary>
        static void DrawFaded(Graphics g, Image image, Rectangle bounds, float opacity)
        {
            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix();
            matrix.Matrix33 = opacity;

            using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                g.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
