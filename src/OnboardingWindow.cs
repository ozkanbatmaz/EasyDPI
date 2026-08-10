using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDPI
{
    /// <summary>
    /// First run: three short pages that show what the application does, let the user
    /// pick a language, and hand off to the automatic tuner.
    ///
    /// Portrait, matching the main window, so both feel like the same application.
    /// Artwork on top, words underneath. Someone opening this for the first time
    /// should understand it in a few seconds, not read a manual.
    /// </summary>
    public class OnboardingWindow : RoundedForm
    {
        const int WindowWidth = 440;
        const int CaptionHeight = 44;
        const int ArtHeight = 300;
        const int ContentTop = CaptionHeight + ArtHeight;
        const int ContentHeight = 356;
        const int WindowHeight = ContentTop + ContentHeight;
        const int PageCount = 3;

        ArtworkPanel art;
        ContentPane content;
        IconButton nextButton;
        LinkText skipLink;
        ComboBox languageBox;

        int currentPage = 0;
        bool suppressLanguageEvent = false;

        /// <summary>True when the user finished onboarding and asked to tune the network.</summary>
        public bool RunAutoTune { get; private set; }

        public OnboardingWindow()
        {
            Text = "EasyDPI";
            ClientSize = new Size(WindowWidth, WindowHeight);
            MaximizeBox = false;
            MinimizeBox = false;
            Font = UiTheme.Regular(9f);

            Caption.Text = "EasyDPI";
            Caption.ShowMinimise = false;

            Icon icon = EmbeddedAssets.LoadIcon(32);
            if (icon != null) Icon = icon;

            art = new ArtworkPanel();
            art.Location = new Point(0, CaptionHeight);
            art.Size = new Size(WindowWidth, ArtHeight);
            Controls.Add(art);

            content = new ContentPane();
            content.Location = new Point(0, ContentTop);
            content.Size = new Size(WindowWidth, ContentHeight);
            content.TotalSteps = PageCount;
            Controls.Add(content);

            nextButton = new IconButton();
            nextButton.Size = new Size(300, 54);
            nextButton.Location = new Point((WindowWidth - 300) / 2, 214);
            nextButton.Radius = 14f;
            nextButton.Font = UiTheme.Semibold(11.5f);
            nextButton.Click += new EventHandler(OnNextClicked);
            content.Controls.Add(nextButton);

            skipLink = new LinkText();
            skipLink.Size = new Size(300, 26);
            skipLink.Location = new Point((WindowWidth - 300) / 2, 280);
            skipLink.Font = UiTheme.Regular(10f);
            skipLink.Colour = UiTheme.TextMuted;
            skipLink.Centred = true;
            skipLink.Click += new EventHandler(OnSkipClicked);
            content.Controls.Add(skipLink);

            languageBox = new ComboBox();
            languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageBox.DrawMode = DrawMode.OwnerDrawFixed;
            languageBox.ItemHeight = 22;
            languageBox.Size = new Size(170, 26);
            languageBox.Location = new Point((WindowWidth - 170) / 2, 316);
            languageBox.FlatStyle = FlatStyle.Flat;
            languageBox.Font = UiTheme.Regular(9.5f);
            languageBox.TabStop = false;   // keeps it from grabbing focus and rendering fully highlighted
            languageBox.DrawItem += new DrawItemEventHandler(OnDrawLanguageItem);
            languageBox.SelectedIndexChanged += new EventHandler(OnLanguageChanged);
            content.Controls.Add(languageBox);
            FillLanguages();

            ShowPage(0);
            ActiveControl = nextButton;
        }

        // ------------------------------------------------------------------
        // language picker
        // ------------------------------------------------------------------

        /// <summary>Each language is named in its own language, so its speakers recognise it.</summary>
        static string DisplayNameOf(string code)
        {
            switch (code)
            {
                case "en": return "English";
                case "tr": return "Türkçe";
                case "ru": return "Русский";
                default: return code;
            }
        }

        void FillLanguages()
        {
            suppressLanguageEvent = true;
            languageBox.Items.Clear();

            List<string> codes = new List<string>(Strings.AvailableLanguages);
            foreach (string code in codes) languageBox.Items.Add(DisplayNameOf(code));

            int index = codes.IndexOf(Strings.CurrentLanguage);
            languageBox.SelectedIndex = (index >= 0) ? index : 0;
            suppressLanguageEvent = false;
        }

        void OnDrawLanguageItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            e.DrawBackground();

            string[] codes = Strings.AvailableLanguages;
            string code = (e.Index < codes.Length) ? codes[e.Index] : "en";

            RectangleF flag = new RectangleF(e.Bounds.X + 8, e.Bounds.Y + (e.Bounds.Height - 14) / 2f, 20, 14);
            FlagRenderer.Draw(e.Graphics, code, flag);

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush brush = new SolidBrush(selected ? SystemColors.HighlightText : UiTheme.TextPrimary))
                e.Graphics.DrawString((string)languageBox.Items[e.Index], languageBox.Font, brush,
                                      e.Bounds.X + 36, e.Bounds.Y + 3);
        }

        void OnLanguageChanged(object sender, EventArgs e)
        {
            if (suppressLanguageEvent) return;

            string[] codes = Strings.AvailableLanguages;
            if (languageBox.SelectedIndex < 0 || languageBox.SelectedIndex >= codes.Length) return;

            Settings.Language = codes[languageBox.SelectedIndex];
            Strings.Initialize(Settings.Language);
            ShowPage(currentPage);
        }

        // ------------------------------------------------------------------
        // pages
        // ------------------------------------------------------------------

        void ShowPage(int page)
        {
            currentPage = page;

            string[] titleKeys = { "onboarding.welcome.title", "onboarding.how.title", "onboarding.ready.title" };
            string[] bodyKeys = { "onboarding.welcome.body", "onboarding.how.body", "onboarding.ready.body" };

            content.Step = page + 1;
            content.Title = Strings.Get(titleKeys[page]);
            content.Body = Strings.Get(bodyKeys[page]);
            content.Invalidate();

            // All three illustrations are text free, so they work in every language.
            string[] artwork = {
                EmbeddedAssets.OnboardingWelcome,
                EmbeddedAssets.OnboardingHow,
                EmbeddedAssets.OnboardingReady
            };
            art.SetImage(EmbeddedAssets.Load(artwork[page]));

            languageBox.Visible = (page == 0);

            bool lastPage = (page == PageCount - 1);
            nextButton.Text = Strings.Get(lastPage ? "onboarding.finish" : "onboarding.next");
            nextButton.Invalidate();

            skipLink.Text = Strings.Get("onboarding.skip");
            skipLink.Invalidate();
        }

        void OnNextClicked(object sender, EventArgs e)
        {
            if (currentPage < PageCount - 1) { ShowPage(currentPage + 1); return; }
            RunAutoTune = true;
            Finish();
        }

        void OnSkipClicked(object sender, EventArgs e)
        {
            RunAutoTune = false;
            Finish();
        }

        /// <summary>
        /// Writing the configuration is what marks onboarding as done — the presence of
        /// config.ini is the "not a first run any more" signal.
        /// </summary>
        void Finish()
        {
            Settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        // ==================================================================

        /// <summary>
        /// Everything below the artwork: the progress bar, the step counter, the
        /// headline and the body copy. Buttons are real controls on top of it.
        /// </summary>
        class ContentPane : Panel
        {
            public int Step = 1;
            public int TotalSteps = 3;
            public string Title = "";
            public string Body = "";

            public ContentPane()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);

                BackColor = UiTheme.Surface;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(UiTheme.Surface);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                int centre = Width / 2;

                // Progress runs along the seam between the artwork and the text
                using (SolidBrush track = new SolidBrush(Color.FromArgb(233, 238, 245)))
                    g.FillRectangle(track, 0, 0, Width, 4);

                float filled = Width * Step / (float)TotalSteps;
                using (LinearGradientBrush fill = new LinearGradientBrush(
                    new RectangleF(0, 0, Math.Max(1f, filled), 4), UiTheme.Accent, UiTheme.AccentBright, 0f))
                    g.FillRectangle(fill, 0, 0, filled, 4);

                // Step counter
                string stepText = Step + " / " + TotalSteps;
                using (Font stepFont = UiTheme.Semibold(10.5f))
                {
                    Size size = UiTheme.Measure(stepText, stepFont);
                    RectangleF pill = new RectangleF(centre - (size.Width + 34) / 2f, 20, size.Width + 34, 32);

                    UiTheme.DrawCard(g, pill, pill.Height / 2f, UiTheme.Background, UiTheme.Border);

                    int textX = (int)(pill.X + (pill.Width - size.Width) / 2f);
                    int textY = (int)(pill.Y + (pill.Height - size.Height) / 2f);

                    string current = Step.ToString();
                    Size currentSize = UiTheme.Measure(current, stepFont);
                    UiTheme.Text(g, current, stepFont, UiTheme.Accent, textX, textY);
                    UiTheme.Text(g, stepText.Substring(current.Length), stepFont, UiTheme.TextMuted,
                                 textX + currentSize.Width, textY);
                }

                using (Font titleFont = new Font("Segoe UI", 19f, FontStyle.Bold))
                {
                    Size size = UiTheme.Measure(Title, titleFont);
                    UiTheme.Text(g, Title, titleFont, UiTheme.TextPrimary, centre - size.Width / 2, 68);
                }

                using (Font bodyFont = UiTheme.Regular(10.5f))
                {
                    Rectangle area = new Rectangle(34, 112, Width - 68, 84);
                    TextRenderer.DrawText(g, Body, bodyFont, area, UiTheme.TextMuted,
                        TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                }
            }
        }
    }
}
