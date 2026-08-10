using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// A hand drawn title bar.
    ///
    /// The window is borderless so its corners can be rounded — Windows 10 does not
    /// round them for us — which means the caption, the drag behaviour and the window
    /// buttons all have to be provided here.
    /// </summary>
    public class TitleBar : Control
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, int wParam, int lParam);

        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 0x2;

        public bool ShowMinimise = true;

        Rectangle minimiseBounds, closeBounds;
        bool hoverMinimise, hoverClose;

        public TitleBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = UiTheme.Surface;
            Height = 44;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (minimiseBounds.Contains(e.Location) || closeBounds.Contains(e.Location)) return;

            // Hand the drag to the window manager so snapping and double-click still work
            Form form = FindForm();
            if (form == null) return;

            ReleaseCapture();
            SendMessage(form.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool overMinimise = ShowMinimise && minimiseBounds.Contains(e.Location);
            bool overClose = closeBounds.Contains(e.Location);

            if (overMinimise != hoverMinimise || overClose != hoverClose)
            {
                hoverMinimise = overMinimise;
                hoverClose = overClose;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hoverMinimise = hoverClose = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Form form = FindForm();
            if (form == null) { base.OnMouseClick(e); return; }

            if (ShowMinimise && minimiseBounds.Contains(e.Location)) form.WindowState = FormWindowState.Minimized;
            else if (closeBounds.Contains(e.Location)) form.Close();

            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(UiTheme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            Bitmap logo = EmbeddedAssets.Load(EmbeddedAssets.Logo, 48);
            if (logo != null) g.DrawImage(logo, 16, (Height - 22) / 2, 22, 22);

            using (Font font = UiTheme.Semibold(10f))
            {
                Size size = UiTheme.Measure(Text, font);
                UiTheme.Text(g, Text, font, UiTheme.TextPrimary, 46, (Height - size.Height) / 2);
            }

            int buttonWidth = 44;
            closeBounds = new Rectangle(Width - buttonWidth, 0, buttonWidth, Height);
            minimiseBounds = ShowMinimise
                ? new Rectangle(Width - buttonWidth * 2, 0, buttonWidth, Height)
                : Rectangle.Empty;

            if (hoverClose)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(232, 17, 35)))
                    g.FillRectangle(b, closeBounds);

            if (hoverMinimise)
                using (SolidBrush b = new SolidBrush(Color.FromArgb(238, 241, 246)))
                    g.FillRectangle(b, minimiseBounds);

            using (Pen pen = new Pen(hoverClose ? Color.White : UiTheme.TextPrimary, 1.4f))
            {
                int cx = closeBounds.X + closeBounds.Width / 2;
                int cy = Height / 2;
                g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
            }

            if (ShowMinimise)
                using (Pen pen = new Pen(UiTheme.TextPrimary, 1.4f))
                {
                    int cx = minimiseBounds.X + minimiseBounds.Width / 2;
                    g.DrawLine(pen, cx - 5, Height / 2, cx + 5, Height / 2);
                }
        }
    }

    /// <summary>
    /// Shared behaviour for the application's windows: no system frame, rounded
    /// corners, a drop shadow, and a title bar of our own.
    /// </summary>
    public class RoundedForm : Form
    {
        const int CornerRadius = 11;

        protected TitleBar Caption;

        public RoundedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Background;
            DoubleBuffered = true;

            Caption = new TitleBar();
            Caption.Text = "EasyDPI";
            Caption.Location = new Point(0, 0);
            Controls.Add(Caption);
        }

        /// <summary>A shadow keeps a frameless window from bleeding into the desktop.</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return parameters;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (Caption != null) Caption.Width = ClientSize.Width;
            ApplyRoundedRegion();
        }

        void ApplyRoundedRegion()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            using (GraphicsPath path = UiTheme.Rounded(
                new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), CornerRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }
    }
}
