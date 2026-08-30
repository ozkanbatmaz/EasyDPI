using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
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
        const int RowHeight = 38;

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
            int y = 14;

            // -- mode -----------------------------------------------------------
            modeCard = new TitledCard();
            modeCard.TitleKey = "advanced.modeTitle";
            modeCard.Location = new Point(SideMargin, y);
            modeCard.Size = new Size(cardWidth, 88);
            modeCard.Radius = 14f;
            Controls.Add(modeCard);

            int buttonWidth = (cardWidth - 16 * 2 - 10) / 2;

            automaticButton = new IconButton();
            automaticButton.Size = new Size(buttonWidth, 36);
            automaticButton.Location = new Point(16, 34);
            automaticButton.Radius = 11f;
            automaticButton.Font = UiTheme.Semibold(10f);
            automaticButton.Click += new EventHandler(delegate { SetMode(false); });
            modeCard.Controls.Add(automaticButton);

            advancedButton = new IconButton();
            advancedButton.Size = new Size(buttonWidth, 36);
            advancedButton.Location = new Point(16 + buttonWidth + 10, 34);
            advancedButton.Radius = 11f;
            advancedButton.Font = UiTheme.Semibold(10f);
            advancedButton.Click += new EventHandler(delegate { SetMode(true); });
            modeCard.Controls.Add(advancedButton);

            modeHint = new Label();
            modeHint.Location = new Point(16, 68);
            modeHint.Size = new Size(cardWidth - 32, 16);
            modeHint.Font = UiTheme.Regular(8.75f);
            modeHint.ForeColor = UiTheme.TextMuted;
            modeHint.BackColor = Color.Transparent;
            modeCard.Controls.Add(modeHint);

            y += modeCard.Height + 10;

            // -- services -------------------------------------------------------
            List<string> groups = ProbeList.SelectableGroups();

            servicesCard = new TitledCard();
            servicesCard.TitleKey = "advanced.servicesTitle";
            servicesCard.Location = new Point(SideMargin, y);
            servicesCard.Size = new Size(cardWidth, 42 + groups.Count * RowHeight + 10);
            servicesCard.Radius = 14f;
            Controls.Add(servicesCard);

            services = new ServiceList(groups);
            services.Location = new Point(10, 38);
            services.Size = new Size(cardWidth - 20, groups.Count * RowHeight);
            servicesCard.Controls.Add(services);

            y += servicesCard.Height + 10;

            // -- addresses of your own ------------------------------------------
            customCard = new TitledCard();
            customCard.TitleKey = "advanced.customTitle";
            customCard.Location = new Point(SideMargin, y);
            customCard.Size = new Size(cardWidth, 80);
            customCard.Radius = 14f;
            Controls.Add(customCard);

            customTargets = new TextBox();
            customTargets.Location = new Point(16, 36);
            customTargets.Size = new Size(cardWidth - 32, 22);
            customTargets.Font = UiTheme.Regular(9.5f);
            customTargets.BorderStyle = BorderStyle.FixedSingle;
            customTargets.HandleCreated += new EventHandler(delegate { ShowPlaceholder(customTargets); });
            customCard.Controls.Add(customTargets);

            customHint = new Label();
            customHint.Location = new Point(16, 60);
            customHint.Size = new Size(cardWidth - 32, 16);
            customHint.Font = UiTheme.Regular(8.75f);
            customHint.ForeColor = UiTheme.TextMuted;
            customHint.BackColor = Color.Transparent;
            customCard.Controls.Add(customHint);

            y += customCard.Height + 14;

            applyButton = new IconButton();
            applyButton.Size = new Size(cardWidth, 40);
            applyButton.Location = new Point(SideMargin, y);
            applyButton.Radius = 12f;
            applyButton.Font = UiTheme.Semibold(11f);
            applyButton.Appearance = IconButton.Style.Filled;
            applyButton.Click += new EventHandler(OnApplyClicked);
            Controls.Add(applyButton);

            Height = y + applyButton.Height + 16;
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
            List<string> rejected;

            Settings.SelectedServices = services.Checked();
            Settings.CustomTargets = SplitTargets(customTargets.Text, out rejected);

            // Written back into the box, so the person sees what was actually kept rather
            // than wondering whether their pasted link counted.
            customTargets.Text = string.Join(", ", Settings.CustomTargets.ToArray());

            if (rejected.Count > 0 && Report != null)
                Report(Strings.Get("advanced.rejected", string.Join(", ", rejected.ToArray())));

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string text);

        /// <summary>
        /// The grey example inside an empty box. Windows draws this itself; describing a
        /// format in a hint underneath and leaving the box blank makes people guess.
        /// </summary>
        static void ShowPlaceholder(TextBox box)
        {
            const int SetCueBanner = 0x1501;
            try { SendMessage(box.Handle, SetCueBanner, (IntPtr)1, Strings.Get("advanced.customExample")); }
            catch { }
        }

        /// <summary>
        /// Reads the addresses out of whatever the person typed.
        ///
        /// What belongs here is a host name — the part between the scheme and the first
        /// slash — because that is what the engine reads out of the traffic and matches
        /// against. What people actually type is whatever was in the address bar, so a
        /// pasted "https://www.example.com/page?x=1" becomes "www.example.com" rather
        /// than being stored verbatim and never matching anything: a silent failure,
        /// indistinguishable from the feature not working.
        ///
        /// Entries that cannot be host names are dropped and named. An IP address is the
        /// common one, and it can never match — the engine looks at the name inside the
        /// connection, and a numeric address is not a name.
        /// </summary>
        static List<string> SplitTargets(string text, out List<string> rejected)
        {
            List<string> targets = new List<string>();
            rejected = new List<string>();

            foreach (string part in text.Split(new char[] { ',', ';', ' ', '\t', '\r', '\n' }))
            {
                string raw = part.Trim();
                if (raw.Length == 0) continue;

                string target = Normalise(raw);

                if (target == null)
                {
                    if (!rejected.Contains(raw)) rejected.Add(raw);
                    continue;
                }

                if (!targets.Contains(target)) targets.Add(target);
            }

            return targets;
        }

        static bool IsAscii(string value)
        {
            foreach (char character in value)
                if (character > 127) return false;

            return true;
        }

        /// <summary>Turns anything carrying a host name into that host name, or null.</summary>
        static string Normalise(string entry)
        {
            string value = entry.Trim().ToLowerInvariant();

            int scheme = value.IndexOf("://");
            if (scheme >= 0) value = value.Substring(scheme + 3);

            int slash = value.IndexOf('/');
            if (slash >= 0) value = value.Substring(0, slash);

            int at = value.IndexOf('@');            // an email address was pasted
            if (at >= 0) value = value.Substring(at + 1);

            int colon = value.IndexOf(':');         // a port
            if (colon >= 0) value = value.Substring(0, colon);

            value = value.Trim('.', ' ');

            if (value.Length == 0 || value.IndexOf('.') < 0) return null;

            System.Net.IPAddress address;
            if (System.Net.IPAddress.TryParse(value, out address)) return null;

            // A Turkish or Russian domain reaches the wire as punycode: the name inside
            // the connection is xn--trke-3ra.com, never türkçe.com, so storing what was
            // typed would store something that can never match. Converting is the whole
            // difference between the entry working and silently doing nothing.
            if (!IsAscii(value))
            {
                try { value = new System.Globalization.IdnMapping().GetAscii(value); }
                catch { return null; }
            }

            foreach (char character in value)
            {
                bool allowed = (character >= 'a' && character <= 'z') ||
                               (character >= '0' && character <= '9') ||
                               character == '.' || character == '-' || character == '_';
                if (!allowed) return null;
            }

            return value;
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

                        RectangleF box = new RectangleF(10, top + 9, 18, 18);
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
                        UiTheme.Text(g, ProbeList.DisplayName(groups[index]), labelFont, textColour,
                                     38, top + 4);

                        // The second line is the point of the row: a name like "Social"
                        // and a count of addresses tells nobody whether the thing they
                        // care about is inside it.
                        UiTheme.Text(g, ProbeList.Examples(groups[index]), countFont,
                                     Enabled ? UiTheme.TextMuted : Color.FromArgb(178, 186, 198),
                                     38, top + 20);

                        string note = Blocked.Contains(groups[index])
                            ? Strings.Get("advanced.blockedHere")
                            : ProbeList.HostsInGroup(groups[index]).Count + " " + Strings.Get("advanced.addresses");

                        Color noteColour = Blocked.Contains(groups[index]) && Enabled
                            ? UiTheme.Danger : UiTheme.TextMuted;

                        Size noteSize = UiTheme.Measure(note, countFont);
                        UiTheme.Text(g, note, countFont, noteColour,
                                     Width - noteSize.Width - 12, top + 5);
                    }
                }
            }
        }
    }
}
