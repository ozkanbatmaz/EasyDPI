using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// The Advanced tab: where the bypass is applied.
    ///
    /// Two modes, and automatic is the default because it is the right answer for almost
    /// everybody. Automatic covers every connection the machine makes, which needs no
    /// decisions and cannot miss anything.
    ///
    /// Advanced exists for the person who does not want a tool they installed for two
    /// services rewriting the packets of everything else on the computer — a reasonable
    /// objection, and the usual reason people run it alongside a VPN and find the two
    /// interfering. Choosing services here narrows the engine to their addresses; nothing
    /// else on the machine is touched.
    ///
    /// The services the last measurement found blocked arrive already ticked. The
    /// application has just finished working out which ones are blocked on this
    /// connection, and asking somebody to rediscover that by hand would be a strange way
    /// to use what it knows.
    ///
    /// What this cannot offer is a list of Windows applications, which is what people ask
    /// for first. The engine filters on host names read out of the traffic; it has no idea
    /// which program a packet came from, and no option to find out. Picking a service is
    /// the closest honest equivalent: choosing Discord covers the addresses Discord talks
    /// to, whichever program is talking to them.
    /// </summary>
    public class AdvancedPane : Control
    {
        const int SideMargin = 24;
        const int RowHeight = 32;

        /// <summary>Raised when the user applies a change that needs the engine restarted.</summary>
        public event EventHandler Applied;

        /// <summary>Where progress and results are written.</summary>
        public Action<string> Report;

        IconButton automaticButton, advancedButton, applyButton;
        ServiceList services;
        TextBox customTargets;
        TitledCard modeCard, servicesCard, customCard;
        Label modeHint, customHint;

        public AdvancedPane(int width)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = UiTheme.Background;

            int cardWidth = width - SideMargin * 2;
            int y = 18;

            // -- mode -----------------------------------------------------------
            modeCard = new TitledCard();
            modeCard.TitleKey = "advanced.modeTitle";
            modeCard.Location = new Point(SideMargin, y);
            modeCard.Size = new Size(cardWidth, 96);
            modeCard.Radius = 14f;
            Controls.Add(modeCard);

            int buttonWidth = (cardWidth - 16 * 2 - 10) / 2;

            automaticButton = new IconButton();
            automaticButton.Size = new Size(buttonWidth, 38);
            automaticButton.Location = new Point(16, 36);
            automaticButton.Radius = 11f;
            automaticButton.Font = UiTheme.Semibold(10f);
            automaticButton.Click += new EventHandler(delegate { SetMode(false); });
            modeCard.Controls.Add(automaticButton);

            advancedButton = new IconButton();
            advancedButton.Size = new Size(buttonWidth, 38);
            advancedButton.Location = new Point(16 + buttonWidth + 10, 36);
            advancedButton.Radius = 11f;
            advancedButton.Font = UiTheme.Semibold(10f);
            advancedButton.Click += new EventHandler(delegate { SetMode(true); });
            modeCard.Controls.Add(advancedButton);

            modeHint = new Label();
            modeHint.Location = new Point(16, 76);
            modeHint.Size = new Size(cardWidth - 32, 18);
            modeHint.Font = UiTheme.Regular(8.75f);
            modeHint.ForeColor = UiTheme.TextMuted;
            modeHint.BackColor = Color.Transparent;
            modeCard.Controls.Add(modeHint);

            y += modeCard.Height + 12;

            // -- services -------------------------------------------------------
            List<string> groups = ProbeList.SelectableGroups();

            servicesCard = new TitledCard();
            servicesCard.TitleKey = "advanced.servicesTitle";
            servicesCard.Location = new Point(SideMargin, y);
            servicesCard.Size = new Size(cardWidth, 44 + groups.Count * RowHeight + 12);
            servicesCard.Radius = 14f;
            Controls.Add(servicesCard);

            services = new ServiceList(groups);
            services.Location = new Point(10, 40);
            services.Size = new Size(cardWidth - 20, groups.Count * RowHeight);
            servicesCard.Controls.Add(services);

            y += servicesCard.Height + 12;

            // -- addresses of your own ------------------------------------------
            customCard = new TitledCard();
            customCard.TitleKey = "advanced.customTitle";
            customCard.Location = new Point(SideMargin, y);
            customCard.Size = new Size(cardWidth, 96);
            customCard.Radius = 14f;
            Controls.Add(customCard);

            customTargets = new TextBox();
            customTargets.Location = new Point(16, 40);
            customTargets.Size = new Size(cardWidth - 32, 22);
            customTargets.Font = UiTheme.Regular(9.5f);
            customTargets.BorderStyle = BorderStyle.FixedSingle;
            customCard.Controls.Add(customTargets);

            customHint = new Label();
            customHint.Location = new Point(16, 68);
            customHint.Size = new Size(cardWidth - 32, 18);
            customHint.Font = UiTheme.Regular(8.75f);
            customHint.ForeColor = UiTheme.TextMuted;
            customHint.BackColor = Color.Transparent;
            customCard.Controls.Add(customHint);

            y += customCard.Height + 16;

            applyButton = new IconButton();
            applyButton.Size = new Size(cardWidth, 42);
            applyButton.Location = new Point(SideMargin, y);
            applyButton.Radius = 12f;
            applyButton.Font = UiTheme.Semibold(11f);
            applyButton.Appearance = IconButton.Style.Filled;
            applyButton.Click += new EventHandler(OnApplyClicked);
            Controls.Add(applyButton);

            Height = y + applyButton.Height + 24;
            Width = width;

            LoadFromSettings();
        }

        // ------------------------------------------------------------------

        public void LoadFromSettings()
        {
            services.SetChecked(Settings.SelectedServices.Count > 0
                ? Settings.SelectedServices
                : Settings.BlockedServices);   // first visit: what the measurement found

            customTargets.Text = string.Join(", ", Settings.CustomTargets.ToArray());
            services.Blocked = Settings.BlockedServices;

            ApplyLanguage();
            UpdateModeButtons();
        }

        public void ApplyLanguage()
        {
            automaticButton.Text = Strings.Get("advanced.automatic");
            advancedButton.Text = Strings.Get("advanced.manual");
            applyButton.Text = Strings.Get("advanced.apply");
            customHint.Text = Strings.Get("advanced.customHint");
            Invalidate();
            modeCard.Invalidate();
            servicesCard.Invalidate();
            customCard.Invalidate();
        }

        void SetMode(bool advanced)
        {
            Settings.AdvancedMode = advanced;
            UpdateModeButtons();
        }

        void UpdateModeButtons()
        {
            bool advanced = Settings.AdvancedMode;

            automaticButton.Appearance = advanced ? IconButton.Style.Outline : IconButton.Style.Filled;
            advancedButton.Appearance = advanced ? IconButton.Style.Filled : IconButton.Style.Outline;
            automaticButton.Invalidate();
            advancedButton.Invalidate();

            modeHint.Text = Strings.Get(advanced ? "advanced.manualHint" : "advanced.automaticHint");

            // The choices stay visible while automatic is selected, greyed out, so the
            // tab still explains what the other mode would do rather than going blank.
            services.Enabled = advanced;
            customTargets.Enabled = advanced;
            applyButton.Enabled = advanced;
            services.Invalidate();
        }

        void OnApplyClicked(object sender, EventArgs e)
        {
            Settings.SelectedServices = services.Checked();
            Settings.CustomTargets = SplitTargets(customTargets.Text);

            if (Settings.SelectedServices.Count == 0 && Settings.CustomTargets.Count == 0)
            {
                // An empty list would leave the engine running and covering nothing at
                // all, which looks identical to it being broken.
                if (Report != null) Report(Strings.Get("advanced.nothingChosen"));
                return;
            }

            int count = Settings.SaveAdvancedBlacklist();
            Settings.Save();

            if (Report != null)
                Report(Strings.Get("advanced.applied", Settings.SelectedServices.Count, count));

            if (Applied != null) Applied(this, EventArgs.Empty);
        }

        static List<string> SplitTargets(string text)
        {
            List<string> targets = new List<string>();

            foreach (string part in text.Split(new char[] { ',', ';', ' ', '\t' }))
            {
                string target = part.Trim().ToLowerInvariant();
                if (target.Length > 0 && target.Contains(".") && !targets.Contains(target))
                    targets.Add(target);
            }

            return targets;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// A card with a heading. The heading has to be drawn by the card itself: painting
        /// it on the pane behind puts it underneath the card, where nobody can read it.
        /// </summary>
        class TitledCard : CardPanel
        {
            public string TitleKey;

            protected override void OnPaintContent(Graphics g)
            {
                if (string.IsNullOrEmpty(TitleKey)) return;

                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (Font titleFont = UiTheme.Semibold(10.5f))
                    UiTheme.Text(g, Strings.Get(TitleKey), titleFont, UiTheme.TextPrimary, 16, 14);
            }
        }

        /// <summary>
        /// The list of services with a tick box each. Hand drawn like the rest of the
        /// interface; a CheckedListBox would be the only Windows 95 control in the window.
        /// </summary>
        class ServiceList : Control
        {
            readonly List<string> groups;
            readonly List<bool> ticked = new List<bool>();

            /// <summary>Groups the last measurement found blocked, marked in the list.</summary>
            public List<string> Blocked = new List<string>();

            int hovered = -1;

            public ServiceList(List<string> groupNames)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw |
                         ControlStyles.SupportsTransparentBackColor, true);

                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;

                groups = groupNames;
                foreach (string unused in groups) ticked.Add(false);
            }

            public void SetChecked(List<string> chosen)
            {
                for (int index = 0; index < groups.Count; index++)
                    ticked[index] = chosen.Contains(groups[index]);

                Invalidate();
            }

            public List<string> Checked()
            {
                List<string> chosen = new List<string>();

                for (int index = 0; index < groups.Count; index++)
                    if (ticked[index]) chosen.Add(groups[index]);

                return chosen;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                int row = e.Y / RowHeight;
                if (row >= groups.Count) row = -1;

                if (row != hovered) { hovered = row; Invalidate(); }
                base.OnMouseMove(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                hovered = -1;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (!Enabled) return;

                int row = e.Y / RowHeight;
                if (row < 0 || row >= groups.Count) return;

                ticked[row] = !ticked[row];
                Invalidate();
                base.OnMouseDown(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (Font labelFont = UiTheme.Regular(9.75f))
                using (Font countFont = UiTheme.Regular(8.75f))
                {
                    for (int index = 0; index < groups.Count; index++)
                    {
                        int top = index * RowHeight;

                        if (Enabled && index == hovered)
                            UiTheme.FillRounded(g, new RectangleF(2, top + 2, Width - 4, RowHeight - 4),
                                                8f, UiTheme.AccentTint);

                        RectangleF box = new RectangleF(10, top + (RowHeight - 18) / 2f, 18, 18);
                        bool on = ticked[index];

                        Color boxFill = !Enabled ? Color.FromArgb(236, 239, 244)
                                      : on ? UiTheme.Accent : UiTheme.Surface;

                        UiTheme.FillRounded(g, box, 5f, boxFill);

                        if (!on)
                            using (GraphicsPath path = UiTheme.Rounded(box, 5f))
                            using (Pen pen = new Pen(UiTheme.Border))
                                g.DrawPath(pen, path);
                        else
                            using (Pen tick = new Pen(Enabled ? Color.White : UiTheme.TextMuted, 2f))
                            {
                                tick.StartCap = LineCap.Round;
                                tick.EndCap = LineCap.Round;
                                g.DrawLines(tick, new PointF[] {
                                    new PointF(box.Left + 4.5f, box.Top + 9f),
                                    new PointF(box.Left + 7.5f, box.Top + 12.5f),
                                    new PointF(box.Left + 13.5f, box.Top + 5.5f) });
                            }

                        Color textColour = Enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
                        Size labelSize = UiTheme.Measure(groups[index], labelFont);
                        UiTheme.Text(g, groups[index], labelFont, textColour,
                                     38, top + (RowHeight - labelSize.Height) / 2);

                        // The measurement's verdict, so the list is not just names
                        string note = Blocked.Contains(groups[index])
                            ? Strings.Get("advanced.blockedHere")
                            : ProbeList.HostsInGroup(groups[index]).Count + " " + Strings.Get("advanced.addresses");

                        Color noteColour = Blocked.Contains(groups[index]) && Enabled
                            ? UiTheme.Danger : UiTheme.TextMuted;

                        Size noteSize = UiTheme.Measure(note, countFont);
                        UiTheme.Text(g, note, countFont, noteColour,
                                     Width - noteSize.Width - 12, top + (RowHeight - noteSize.Height) / 2);
                    }
                }
            }
        }
    }
}
