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
        const int UninstallStripHeight = 50;   // the strip under the log that holds "remove"

        TabBar tabBar;
        StatusPane statusPane;
        DetailList details;
        CardPanel logCard;

        IconButton toggleButton;
        IconButton uninstallButton;
        IconButton reportButton;
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
            logCard.Size = new Size(WindowWidth - SideMargin * 2,
                                    WindowHeight - ContentTop - TabBarHeight - 18 - 24 - UninstallStripHeight);
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

            // Bottom right of the log tab, away from everything else. Removing the
            // application is not part of using it, and the control should not sit
            // anywhere a mis-click could reach.
            // Left of the strip, as far from the removal button as the width allows.
            reportButton = new IconButton();
            reportButton.Size = new Size(158, 36);
            reportButton.Location = new Point(SideMargin, logCard.Bottom + 14);
            reportButton.Appearance = IconButton.Style.Outline;
            reportButton.Radius = 11f;
            reportButton.Font = UiTheme.Semibold(10f);
            reportButton.Text = Strings.Get("button.saveReport");
            reportButton.Visible = false;
            reportButton.Click += new EventHandler(OnSaveReportClicked);
            Controls.Add(reportButton);

            uninstallButton = new IconButton();
            uninstallButton.Size = new Size(158, 36);
            uninstallButton.Location = new Point(SideMargin + logCard.Width - 158,
                                                 logCard.Bottom + 14);
            uninstallButton.Appearance = IconButton.Style.Danger;
            uninstallButton.Radius = 11f;
            uninstallButton.Font = UiTheme.Semibold(10f);
            uninstallButton.Text = Strings.Get("button.uninstall");
            uninstallButton.Visible = false;
            uninstallButton.Click += new EventHandler(OnUninstallClicked);
            Controls.Add(uninstallButton);
        }

        void ShowTab(int index)
        {
            statusPane.Visible = (index == 0);
            details.Visible = (index == 0);
            logCard.Visible = (index == 1);
            uninstallButton.Visible = (index == 1);
            reportButton.Visible = (index == 1);
        }

        // ------------------------------------------------------------------
        // state
        // ------------------------------------------------------------------

        void OnWindowShown(object sender, EventArgs e)
        {
            if (!File.Exists(AppPaths.GoodbyeDpiExe)) Report(Strings.Get("warn.missingBypassBinary"));
            if (!File.Exists(AppPaths.DnscryptExe)) Report(Strings.Get("warn.missingDnsBinary"));

            UpdateDisplay();

            if (Settings.CheckForUpdates)
                UpdateCheck.InBackground(delegate(UpdateCheck.Release release)
                {
                    try { Invoke(new Action<UpdateCheck.Release>(OnNewerVersionFound), new object[] { release }); }
                    catch { }   // the window may already be closing
                });

            if (TuneOnStartup)
            {
                TuneOnStartup = false;
                StartAutoTune();
            }
        }

        /// <summary>
        /// Tells the user once per release, not once per launch. A notice that reappears
        /// every time teaches people to dismiss it without reading, which is the opposite
        /// of what it is for.
        /// </summary>
        void OnNewerVersionFound(UpdateCheck.Release release)
        {
            Report(Strings.Get("update.available", release.Version));

            if (Settings.UpdateNotifiedVersion == release.Version) return;
            Settings.UpdateNotifiedVersion = release.Version;

            // Never create config.ini just to record this: its absence is what marks a
            // first run, and writing one here would skip the introduction next time.
            if (File.Exists(AppPaths.ConfigFile)) Settings.Save();

            // An update that cannot be verified is not offered as an install. The page
            // is offered instead, so the person can look at it and decide for themselves.
            string question = release.CanInstall
                ? Strings.Get("update.prompt", release.Version, AppInfo.Version)
                : Strings.Get("update.promptManual", release.Version, AppInfo.Version);

            DialogResult answer = MessageBox.Show(this, question,
                Strings.Get("update.title"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (answer != DialogResult.Yes) return;

            if (!release.CanInstall)
            {
                try { Process.Start(release.PageUrl); }
                catch (Exception error) { Report(Strings.Get("log.error", error.Message)); }
                return;
            }

            StartUpdate(release);
        }

        /// <summary>
        /// Downloads and verifies the new version, then closes so the files can be
        /// replaced. Everything up to the swap is reversible: a failure at any point
        /// leaves the installation untouched and the application running.
        /// </summary>
        void StartUpdate(UpdateCheck.Release release)
        {
            tabBar.SelectedIndex = 1;   // the log is where the progress shows
            SetWorking(true, Strings.Get("button.updating"));

            bool wasProtected = BypassController.IsActive;

            RunInBackground(delegate
            {
                string staged = Updater.Stage(release, Report);
                if (staged == null) return;   // Stage reported why, and changed nothing

                if (!Updater.ScheduleSwap(staged, wasProtected))
                {
                    Report(Strings.Get("update.swapFailed"));
                    return;
                }

                Report(Strings.Get("update.restarting"));
                try { BeginInvoke(new Action(CloseForUpdate)); }
                catch { Application.Exit(); }
            });
        }

        void CloseForUpdate()
        {
            refreshTimer.Stop();
            Application.DoEvents();
            Thread.Sleep(1200);
            Application.Exit();
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

            string stamped = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            activityLog.AppendText(stamped + Environment.NewLine);
            ActivityLog.Append(stamped);

            // Blank lines are spacing in the log, not something worth surfacing elsewhere.
            if (message.Trim().Length > 0) lastActivity = message.Trim();
        }

        void SetWorking(bool busy, string buttonText)
        {
            if (IsDisposed || Disposing) return;

            if (InvokeRequired)
            {
                try { Invoke(new Action<bool, string>(SetWorking), new object[] { busy, buttonText }); }
                catch { }   // the window can close while a background step is finishing
                return;
            }

            working = busy;
            toggleButton.Enabled = !busy;
            autoTuneLink.Enabled = !busy;
            uninstallButton.Enabled = !busy;
            reportButton.Enabled = !busy;

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

        /// <summary>
        /// Writes everything a bug report needs to one file and shows the user where it
        /// landed, so answering "what does your log say" is a matter of attaching it.
        /// </summary>
        void OnSaveReportClicked(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = Strings.Get("report.dialogTitle");
                dialog.Filter = Strings.Get("report.fileType") + " (*.txt)|*.txt";
                dialog.FileName = DiagnosticReport.SuggestedFileName();
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    DiagnosticReport.Save(dialog.FileName, activityLog.Text);
                    Report(Strings.Get("report.saved", dialog.FileName));
                    RevealInExplorer(dialog.FileName);
                }
                catch (Exception error)
                {
                    Report(Strings.Get("report.failed", error.Message));
                }
            }
        }

        /// <summary>Opens the containing folder with the file already selected.</summary>
        static void RevealInExplorer(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch { }
        }

        /// <summary>
        /// Asks once, plainly, and then removes everything. There is no undo and no
        /// second dialog: a confirmation people have learned to click through twice
        /// protects nobody, so the one prompt says exactly what is about to disappear.
        /// </summary>
        void OnUninstallClicked(object sender, EventArgs e)
        {
            if (working) return;

            DialogResult answer = MessageBox.Show(this,
                Strings.Get("uninstall.confirmBody", AppPaths.Root.TrimEnd(Path.DirectorySeparatorChar)),
                Strings.Get("uninstall.confirmTitle"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes) return;

            tabBar.SelectedIndex = 1;   // so the removal can be watched as it happens
            SetWorking(true, Strings.Get("button.uninstalling"));

            RunInBackground(delegate
            {
                Uninstaller.Run(Report);

                // The files go once this process releases them, so leaving the window
                // open would only give the deletion something to fail against. Posting
                // the close rather than waiting on it lets this worker finish first:
                // the code that runs after it touches the window, and by then a
                // synchronous Application.Exit would already have disposed it.
                try { BeginInvoke(new Action(CloseAfterUninstall)); }
                catch { Application.Exit(); }
            });
        }

        void CloseAfterUninstall()
        {
            refreshTimer.Stop();
            Report(Strings.Get("uninstall.closing"));
            Application.DoEvents();
            Thread.Sleep(1500);
            Application.Exit();
        }

        void OnSettingsClicked(object sender, EventArgs e)
        {
            PopupMenu menu = new PopupMenu();
            menu.AddItem(ScopeMenuText(), new EventHandler(delegate { ToggleScope(); }));
            menu.AddItem(Strings.Get("menu.showIntro"), new EventHandler(delegate { ShowIntroduction(); }));
            menu.AddItem(Strings.Get("menu.openConfig"), new EventHandler(delegate { OpenConfigFolder(); }));

            // Hangs from the right edge of the window, just under the gear
            menu.ShowAlignedRight(this, new Point(WindowWidth - 18, ContentTop + TabBarHeight - 4));
        }

        static string ScopeMenuText()
        {
            return (Settings.TargetedScope ? "✓  " : "     ") + Strings.Get("menu.targetedScope");
        }

        /// <summary>
        /// Switches between reshaping everything and reshaping only the addresses the last
        /// measurement found blocked, and re-applies immediately when protection is on —
        /// a setting that takes effect at some unspecified later point is a setting people
        /// cannot tell they have changed.
        /// </summary>
        void ToggleScope()
        {
            if (working) return;

            Settings.TargetedScope = !Settings.TargetedScope;
            Settings.Save();

            if (Settings.TargetedScope && Settings.BlacklistCount() == 0)
            {
                // Nothing measured yet, so there is no list to narrow to and the engine
                // would quietly go on reshaping everything.
                Report(Strings.Get("scope.noList"));
                return;
            }

            Report(Strings.Get(Settings.TargetedScope ? "scope.targeted" : "scope.all",
                               Settings.BlacklistCount()));

            if (!BypassController.IsActive) return;

            SetWorking(true, Strings.Get("button.turningOn"));
            RunInBackground(delegate { BypassController.TurnOn(Report); });
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
            uninstallButton.Text = Strings.Get("button.uninstall");
            uninstallButton.Invalidate();
            reportButton.Text = Strings.Get("button.saveReport");
            reportButton.Invalidate();
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
