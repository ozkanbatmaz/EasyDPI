using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// A small dropdown drawn to match the rest of the interface.
    ///
    /// It lives inside the window rather than in a popup window of its own: a separate
    /// window has to fight for activation, and loses it the moment the click that opened
    /// it finishes. As a child control there is nothing to fight over.
    /// </summary>
    public class PopupMenu : Control, IMessageFilter
    {
        class Entry
        {
            public string Text;
            public EventHandler Action;
        }

        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_RBUTTONDOWN = 0x0204;

        const int ItemHeight = 38;
        const int VerticalPadding = 7;
        const int HorizontalPadding = 16;
        const int MinimumWidth = 190;
        const int CornerRadius = 10;

        readonly List<Entry> entries = new List<Entry>();
        int hovered = -1;
        bool filtering;

        public PopupMenu()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = UiTheme.Surface;
            Font = UiTheme.Regular(9.75f);
            Visible = false;
        }

        public void AddItem(string text, EventHandler action)
        {
            Entry entry = new Entry();
            entry.Text = text;
            entry.Action = action;
            entries.Add(entry);
        }

        /// <summary>
        /// Opens the menu with its top right corner at the given point, in the
        /// coordinates of the host control. Menus hanging off a right aligned
        /// button should grow leftwards.
        /// </summary>
        public void ShowAlignedRight(Control host, Point anchor)
        {
            int width = MinimumWidth;
            foreach (Entry entry in entries)
            {
                int needed = UiTheme.Measure(entry.Text, Font).Width + HorizontalPadding * 2;
                if (needed > width) width = needed;
            }

            int height = entries.Count * ItemHeight + VerticalPadding * 2;

            Size = new Size(width, height);
            Location = new Point(anchor.X - width, anchor.Y);

            using (GraphicsPath path = UiTheme.Rounded(new RectangleF(0, 0, width, height), CornerRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }

            if (!host.Controls.Contains(this)) host.Controls.Add(this);

            Visible = true;
            BringToFront();

            if (!filtering) { Application.AddMessageFilter(this); filtering = true; }
        }

        public void CloseMenu()
        {
            if (filtering) { Application.RemoveMessageFilter(this); filtering = false; }

            Visible = false;
            hovered = -1;

            if (Parent != null) Parent.Controls.Remove(this);
            Dispose();
        }

        /// <summary>Any click that lands outside the menu dismisses it.</summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_LBUTTONDOWN && m.Msg != WM_RBUTTONDOWN && m.Msg != WM_NCLBUTTONDOWN)
                return false;

            if (!Visible || IsDisposed) return false;

            Point cursor = Control.MousePosition;
            Rectangle onScreen = new Rectangle(PointToScreen(Point.Empty), Size);

            if (!onScreen.Contains(cursor)) { CloseMenu(); return false; }
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = HitTest(e.Location);
            if (index != hovered)
            {
                hovered = index;
                Cursor = (index >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hovered != -1) { hovered = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int index = HitTest(e.Location);
            base.OnMouseClick(e);

            if (index < 0) return;

            EventHandler action = entries[index].Action;
            CloseMenu();

            // Run after closing, so the menu is gone before anything it opens appears
            if (action != null) action(this, EventArgs.Empty);
        }

        int HitTest(Point point)
        {
            if (point.Y < VerticalPadding) return -1;

            int index = (point.Y - VerticalPadding) / ItemHeight;
            return (index >= 0 && index < entries.Count) ? index : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            RectangleF bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            UiTheme.DrawCard(g, bounds, CornerRadius, UiTheme.Surface, UiTheme.Border);

            for (int i = 0; i < entries.Count; i++)
            {
                int top = VerticalPadding + i * ItemHeight;

                if (i == hovered)
                    UiTheme.FillRounded(g,
                        new RectangleF(6, top + 2, Width - 12, ItemHeight - 4), 7f, UiTheme.AccentTint);

                Size size = UiTheme.Measure(entries[i].Text, Font);
                UiTheme.Text(g, entries[i].Text, Font,
                             i == hovered ? UiTheme.Accent : UiTheme.TextPrimary,
                             HorizontalPadding, top + (ItemHeight - size.Height) / 2);
            }
        }
    }
}
