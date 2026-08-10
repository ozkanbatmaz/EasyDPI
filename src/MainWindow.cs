using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// The main window, laid out as a portrait panel in the shape people already expect
    /// from a connection tool: state in the middle, one large action under it, details
    /// listed at the bottom.
    ///
    /// The activity log lives behind its own tab and never opens by itself. Turning
    /// protection on or off is a single click and should not rearrange the window.
    /// </summary>
    public class MainWindow : RoundedForm
    {
        const int WindowWidth = 440;
        const int ContentTop = 44;          // height of the hand drawn title bar
        const int WindowHeight = ContentTop + 700;
        const int TabBarHeight = 62;
        const int SideMargin = 24;

        TabBar tabBar;
        StatusPane statusPane;
        DetailList details;
        CardPanel logCard;

        IconButton toggleButton;
        LinkText autoTuneLink;
        TextBox activityLog;

        System.Windows.Forms.Timer refreshTimer;

        volatile bool working = false;
        volatile string lastActivity = null;

        /// <summary>Set by onboarding: run the tuner as soon as the window appears.</summary>
        public bool TuneOnStartup { get; set; }

        public MainWindow()
        {
            Text = "EasyDPI";
            ClientSize = new Size(WindowWidth, WindowHeight);
            MaximizeBox = false;
            Font = UiTheme.Regular(9f);
            Caption.Text = "EasyDPI";

            Icon windowIcon = EmbeddedAssets.LoadIcon(32);
            if (windowIcon != null) Icon = windowIcon;

            BuildTabBar();
            BuildStatusPane();
            BuildDetailList();
            BuildLogCard();

            ShowTab(0);

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 1500;
            refreshTimer.Tick += new EventHandler(OnRefreshTick);
            refreshTimer.Start();

            Shown += new EventHandler(OnWindowShown);
        }

        // ------------------------------------------------------------------
        // layout
        // ------------------------------------------------------------------

        void BuildTabBar()
        {
            tabBar = new TabBar();
            tabBar.Location = new Point(0, ContentTop);
            tabBar.Size = new Size(WindowWidth, TabBarHeight);
            tabBar.AddTab(EmbeddedAssets.HeroShield, Strings.Get("tab.status"));
            tabBar.AddTab(EmbeddedAssets.IconLog, Strings.Get("tab.log"));
            tabBar.SelectedIndexChanged += new EventHandler(delegate { ShowTab(tabBar.SelectedIndex); });
            tabBar.SettingsClicked += new EventHandler(OnSettingsClicked);
            Controls.Add(tabBar);
        }

        void BuildStatusPane()
        {
            statusPane = new StatusPane();
            statusPane.Location = new Point(0, ContentTop + TabBarHeight);
            statusPane.Size = new Size(WindowWidth, 444);
            statusPane.BackColor = UiTheme.Background;
            Controls.Add(statusPane);

            toggleButton = new IconButton();
            toggleButton.Size = new Size(300, 56);
            toggleButton.Location = new Point((WindowWidth - 300) / 2, 332);
            toggleButton.Icon = EmbeddedAssets.Load(EmbeddedAssets.IconPower, 44);
            toggleButton.IconSize = 21;
            toggleButton.Radius = 15f;
            toggleButton.Font = UiTheme.Semibold(12f);
            toggleButton.Click += new EventHandler(OnToggleClicked);
            statusPane.Controls.Add(toggleButton);

            autoTuneLink = new LinkText();
            autoTuneLink.Size = new Size(300, 30);
            autoTuneLink.Location = new Point((WindowWidth - 300) / 2, 402);
            autoTuneLink.Font = UiTheme.Regular(10.5f);
            autoTuneLink.Suffix = "  ›";
            autoTuneLink.Centred = true;
            autoTuneLink.Click += new EventHandler(OnAutoTuneClicked);
            statusPane.Controls.Add(autoTuneLink);
        }

        void BuildDetailList()
        {
            details = new DetailList();
            details.Location = new Point(SideMargin, ContentTop + TabBarHeight + 444);
            details.Size = new Size(WindowWidth - SideMargin * 2, 168);
            details.Radius = 14f;
            Controls.Add(details);
        }

        void BuildLogCard()
        {
            logCard = new CardPanel();
            logCard.Location = new Point(SideMargin, ContentTop + TabBarHeight + 18);
            logCard.Size = new Size(WindowWidth - SideMargin * 2, WindowHeight - ContentTop - TabBarHeight - 18 - 24);
            logCard.Radius = 14f;
            logCard.Visible = false;
            Controls.Add(logCard);

            activityLog = new TextBox();
            activityLog.Location = new Point(16, 14);
            activityLog.Size = new Size(logCard.Width - 32, logCard.Height - 28);
            activityLog.Multiline = true;
            activityLog.ReadOnly = true;
            activityLog.ScrollBars = ScrollBars.Vertical;
            activityLog.BackColor = UiTheme.Surface;
            activityLog.ForeColor = Color.FromArgb(71, 85, 105);
            activityLog.Font = new Font("Consolas", 8.5f);
            activityLog.BorderStyle = BorderStyle.None;
            logCard.Controls.Add(activityLog);
        }

        void ShowTab(int index)
        {
            statusPane.Visible = (index == 0);
            details.Visible = (index == 0);
            logCard.Visible = (index == 1);
        }

        // ------------------------------------------------------------------
        // state
        // ------------------------------------------------------------------

        void OnWindowShown(object sender, EventArgs e)
        {
            if (!File.Exists(AppPaths.GoodbyeDpiExe)) Report(Strings.Get("warn.missingBypassBinary"));
            if (!File.Exists(AppPaths.DnscryptExe)) Report(Strings.Get("warn.missingDnsBinary"));

            UpdateDisplay();

            if (TuneOnStartup)
            {
                TuneOnStartup = false;
                StartAutoTune();
            }
        }

        void OnRefreshTick(object sender, EventArgs e) { UpdateDisplay(); }

        void UpdateDisplay()
        {
            bool active = BypassController.IsActive;

            statusPane.Protected = active;
            statusPane.PillText = Strings.Get(active ? "pill.on" : "pill.off");
            statusPane.Title = Strings.Get(active ? "status.on" : "status.off");
            statusPane.Subtitle = Strings.Get(active ? "subtitle.on" : "subtitle.off");
            statusPane.Invalidate();

            if (!working)
            {
                // Kept filled in both states: the power glyph is a light outline and
                // would disappear against a white button.
                toggleButton.Text = Strings.Get(active ? "button.turnOff" : "button.turnOn");
                toggleButton.Appearance = IconButton.Style.Filled;
                toggleButton.Icon = EmbeddedAssets.Load(EmbeddedAssets.IconPower, 44);
                toggleButton.Invalidate();
            }

            autoTuneLink.Text = Strings.Get("link.autoTune");

            // While work is running the list is replaced by a live progress line, so
            // there is feedback without switching to the log.
            details.Activity = working ? lastActivity : null;

            details.SetRows(
                Strings.Get("details.bypassService"), ServiceManager.DescribeState(ServiceManager.BypassService),
                Strings.Get("details.dnsService"), ServiceManager.DescribeState(ServiceManager.DnsService),
                Strings.Get("details.dnsShort"), NetworkTools.DescribeCurrentDns(),
                Strings.Get("details.activeSettings"), Settings.BypassArguments);
        }

        void Report(string message)
        {
            if (activityLog.InvokeRequired)
            {
                activityLog.Invoke(new Action<string>(Report), new object[] { message });
                return;
            }

            activityLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);

            // Blank lines are spacing in the log, not something worth surfacing elsewhere.
            if (message.Trim().Length > 0) lastActivity = message.Trim();
        }

        void SetWorking(bool busy, string buttonText)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool, string>(SetWorking), new object[] { busy, buttonText });
                return;
            }

            working = busy;
            toggleButton.Enabled = !busy;
            autoTuneLink.Enabled = !busy;

            if (buttonText != null)
            {
                toggleButton.Text = buttonText;
                toggleButton.Icon = null;
                toggleButton.Invalidate();
            }

            if (!busy) lastActivity = null;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        /// <summary>
        /// Service and network work blocks for many seconds, so it never runs on the
        /// UI thread. Progress arrives through Report(), which marshals itself.
        /// </summary>
        void RunInBackground(ThreadStart work)
        {
            Thread worker = new Thread(new ThreadStart(delegate
            {
                try { work(); }
                catch (Exception ex) { Report(Strings.Get("log.error", ex.Message)); }
                finally
                {
                    SetWorking(false, null);
                    try { Invoke(new Action(UpdateDisplay)); } catch { }
                }
            }));
            worker.IsBackground = true;
            worker.Start();
        }

        // ------------------------------------------------------------------
        // events
        // ------------------------------------------------------------------

        void OnToggleClicked(object sender, EventArgs e)
        {
            if (working) return;

            bool turningOn = !BypassController.IsActive;
            SetWorking(true, Strings.Get(turningOn ? "button.turningOn" : "button.turningOff"));

            RunInBackground(delegate
            {
                if (turningOn) BypassController.TurnOn(Report);
                else BypassController.TurnOff(Report);
            });
        }

        void OnAutoTuneClicked(object sender, EventArgs e)
        {
            if (working) return;

            DialogResult answer = MessageBox.Show(
                Strings.Get("confirm.autoTune.body"),
                Strings.Get("confirm.autoTune.title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;
            StartAutoTune();
        }

        void StartAutoTune()
        {
            SetWorking(true, Strings.Get("button.testing"));
            RunInBackground(delegate { AutoTuner.Run(Report); });
        }

        void OnSettingsClicked(object sender, EventArgs e)
        {
            PopupMenu menu = new PopupMenu();
            menu.AddItem(Strings.Get("menu.showIntro"), new EventHandler(delegate { ShowIntroduction(); }));
            menu.AddItem(Strings.Get("menu.openConfig"), new EventHandler(delegate { OpenConfigFolder(); }));

            // Hangs from the right edge of the window, just under the gear
            menu.ShowAlignedRight(this, new Point(WindowWidth - 18, ContentTop + TabBarHeight - 4));
        }

        /// <summary>
        /// Replays the first-run introduction. Useful for reviewing the wording and the
        /// artwork without deleting the configuration.
        /// </summary>
        public void ShowIntroduction()
        {
            bool tune = false;

            using (OnboardingWindow onboarding = new OnboardingWindow())
            {
                onboarding.ShowDialog(this);
                tune = onboarding.RunAutoTune;
            }

            // The language may have changed while the introduction was open.
            tabBar.SetTabText(0, Strings.Get("tab.status"));
            tabBar.SetTabText(1, Strings.Get("tab.log"));
            UpdateDisplay();

            if (tune && !working) StartAutoTune();
        }

        void OpenConfigFolder()
        {
            try
            {
                if (File.Exists(AppPaths.ConfigFile))
                    Process.Start("explorer.exe", "/select,\"" + AppPaths.ConfigFile + "\"");
                else
                    Process.Start("explorer.exe", "\"" + AppPaths.Root + "\"");
            }
            catch { }
        }

        // ==================================================================
        // Status pane
        // ==================================================================

        /// <summary>
        /// Shield, state pill, headline and subtitle, stacked and centred.
        /// The button and the link are real controls placed on top.
        /// </summary>
        class StatusPane : Control
        {
            public bool Protected;
            public string PillText = "";
            public string Title = "";
            public string Subtitle = "";

            public StatusPane()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                int centre = Width / 2;

                Bitmap shield = EmbeddedAssets.Load(EmbeddedAssets.HeroShield, 380);
                if (shield != null)
                {
                    int size = 176;
                    Rectangle target = new Rectangle(centre - size / 2, 12, size, size);

                    if (Protected) g.DrawImage(shield, target);
                    else DrawDesaturated(g, shield, target);
                }

                using (Font pillFont = UiTheme.Semibold(9.5f))
                {
                    Size textSize = UiTheme.Measure(PillText, pillFont);
                    float pillWidth = textSize.Width + 44;
                    RectangleF pill = new RectangleF(centre - pillWidth / 2f, 200, pillWidth, 32);

                    UiTheme.DrawStatusPill(g, pill, PillText, pillFont,
                        Protected ? UiTheme.Success : UiTheme.TextMuted,
                        Protected ? UiTheme.Success : UiTheme.TextMuted,
                        Protected ? UiTheme.SuccessTint : Color.FromArgb(238, 241, 246));
                }

                using (Font titleFont = new Font("Segoe UI", 23f, FontStyle.Bold))
                {
                    Size size = UiTheme.Measure(Title, titleFont);
                    UiTheme.Text(g, Title, titleFont, UiTheme.TextPrimary, centre - size.Width / 2, 246);
                }

                using (Font subFont = UiTheme.Regular(10.5f))
                {
                    Size size = UiTheme.Measure(Subtitle, subFont);
                    UiTheme.Text(g, Subtitle, subFont, UiTheme.TextMuted, centre - size.Width / 2, 292);
                }
            }

            /// <summary>Greys the shield out when protection is off, so state reads at a glance.</summary>
            static void DrawDesaturated(Graphics g, Image image, Rectangle bounds)
            {
                float[][] matrix = {
                    new float[] { 0.32f, 0.32f, 0.32f, 0, 0 },
                    new float[] { 0.42f, 0.42f, 0.42f, 0, 0 },
                    new float[] { 0.16f, 0.16f, 0.16f, 0, 0 },
                    new float[] { 0, 0, 0, 0.5f, 0 },
                    new float[] { 0.2f, 0.2f, 0.2f, 0, 1 }
                };

                using (System.Drawing.Imaging.ImageAttributes attributes = new System.Drawing.Imaging.ImageAttributes())
                {
                    attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(matrix));
                    g.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                }
            }
        }

        // ==================================================================
        // Detail list
        // ==================================================================

        /// <summary>
        /// Four labelled values stacked in a card. Replaced by a single progress line
        /// while something is running.
        /// </summary>
        class DetailList : CardPanel
        {
            readonly string[] labels = new string[4];
            readonly string[] values = new string[4];

            public string Activity;

            static readonly string[] IconNames = {
                EmbeddedAssets.IconService,
                EmbeddedAssets.IconDns,
                EmbeddedAssets.IconGlobe,
                EmbeddedAssets.IconTune
            };

            public void SetRows(string l1, string v1, string l2, string v2,
                                string l3, string v3, string l4, string v4)
            {
                labels[0] = l1; values[0] = v1;
                labels[1] = l2; values[1] = v2;
                labels[2] = l3; values[2] = v3;
                labels[3] = l4; values[3] = v4;
                Invalidate();
            }

            protected override void OnPaintContent(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                if (!string.IsNullOrEmpty(Activity)) { DrawActivity(g); return; }

                int rowHeight = (Height - 12) / 4;

                using (Font labelFont = UiTheme.Regular(9.75f))
                using (Font valueFont = UiTheme.Semibold(9.75f))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int top = 6 + i * rowHeight;
                        int centre = top + rowHeight / 2;

                        if (i > 0)
                            using (Pen pen = new Pen(UiTheme.Divider))
                                g.DrawLine(pen, 18, top, Width - 18, top);

                        int icon = 26;
                        Bitmap image = EmbeddedAssets.Load(IconNames[i], 64);
                        if (image != null) g.DrawImage(image, 18, centre - icon / 2, icon, icon);

                        Size labelSize = UiTheme.Measure(labels[i], labelFont);
                        UiTheme.Text(g, labels[i], labelFont, UiTheme.TextMuted, 56, centre - labelSize.Height / 2);

                        int available = Width - 18 - (56 + labelSize.Width + 12);
                        string value = Fit(values[i], valueFont, available);

                        Size valueSize = UiTheme.Measure(value, valueFont);
                        UiTheme.Text(g, value, valueFont, ValueColour(i),
                                     Width - 18 - valueSize.Width, centre - valueSize.Height / 2);
                    }
                }
            }

            void DrawActivity(Graphics g)
            {
                using (Font font = UiTheme.Regular(10f))
                {
                    Bitmap icon = EmbeddedAssets.Load(EmbeddedAssets.IconTune, 80);
                    if (icon != null) g.DrawImage(icon, 18, Height / 2 - 20, 40, 40);

                    Rectangle area = new Rectangle(70, 20, Width - 88, Height - 40);
                    TextRenderer.DrawText(g, Activity, font, area, UiTheme.TextPrimary,
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                }
            }

            Color ValueColour(int index)
            {
                // Service states read green when running, muted otherwise; the rest stay blue.
                if (index <= 1)
                    return values[index] == Strings.Get("service.running") ? UiTheme.Success : UiTheme.TextMuted;
                return UiTheme.Accent;
            }

            /// <summary>Trims with an ellipsis so a long value never collides with its label.</summary>
            static string Fit(string text, Font font, int maxWidth)
            {
                if (string.IsNullOrEmpty(text)) return "";
                if (UiTheme.Measure(text, font).Width <= maxWidth) return text;

                string trimmed = text;
                while (trimmed.Length > 1 && UiTheme.Measure(trimmed + "…", font).Width > maxWidth)
                    trimmed = trimmed.Substring(0, trimmed.Length - 1);

                return trimmed + "…";
            }
        }
    }
}
