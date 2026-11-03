using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // Settings as one description: sections or the trail of pages on the strip, whole rows each with a value and a sentence, the pane with its preview, the help bar generated - see docs/LunaP.md §213.
    /// <summary>A settings screen declared as data: a strip of sections, or the trail of the pages open under one; rows each with one value and one sentence; a pane explaining the focused row with a preview where it has a picture; and the help bar from the focus.</summary>
    public partial class SettingsLayout : Border
    {
        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<SettingsLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>Defines the Form property.</summary>
        public static readonly StyledProperty<AdaptiveForm> FormProperty = AvaloniaProperty.Register<SettingsLayout, AdaptiveForm>(nameof(Form), AdaptiveForm.BigScreen);

        /// <summary>The form drawn: the big screen's, or the desktop's.</summary>
        public AdaptiveForm Form { get => GetValue(FormProperty); set => SetValue(FormProperty, value); }

        /// <summary>The design height; the screen is laid out in units of it and scaled by MenuPanel.Scale.</summary>
        public const double DesignHeight = 800;

        /// <summary>The safe area's side, the strip's top and height.</summary>
        public const double Side = 64, StripTop = 40, StripHeight = 44;

        /// <summary>The rows' and the pane's top, the pane's width and height, and the gap between the rows and the pane.</summary>
        public const double ContentTop = 108, PaneWidth = 416, PaneHeight = 578, PaneGap = 32;

        /// <summary>A row's height.</summary>
        public const double RowHeight = PageRowLook.RowHeight;

        /// <summary>The pane's inset, the preview's box and the gap under it.</summary>
        public const double PaneInset = 24, PreviewWidth = 368, PreviewHeight = 276, PreviewGap = 12;

        /// <summary>A meter's height, and one variant's box.</summary>
        public const double MeterHeight = 16, VariantWidth = 176, VariantHeight = 160;

        private readonly Panel _root = new();
        private readonly HintBar _hintBar = new();
        private readonly WholeRow _strip = new() { Spacing = 8, Name = "SettingsSections" };
        private IReadOnlyList<AdaptiveSection> _sections = Array.Empty<AdaptiveSection>();
        private IReadOnlyList<string> _trail = Array.Empty<string>();
        private IReadOnlyList<PageRow> _rows = Array.Empty<PageRow>();
        private string? _selected, _focusKey;
        private int _first;
        private double _unitsWide = 1280;
        private Func<string, SettingsPane?>? _paneFor;
        private Func<string?, IReadOnlyList<PageHint>>? _hintsFor;

        /// <summary>An empty settings screen.</summary>
        public SettingsLayout()
        {
            Focusable = true;
            ClipToBounds = true;
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            AdaptiveLayout.StyleStrip(_strip);
            AutomationProperties.SetName(_strip, "Sections");
            WireDesktop();
            Child = _root;
            Fill();
        }

        /// <summary>The pad whose printing the help bar draws.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        /// <summary>The sections, in order; set with SetSections.</summary>
        public IReadOnlyList<AdaptiveSection> Sections => _sections;

        /// <summary>The chosen section's key, or null when there are none.</summary>
        public string? SelectedSection => _selected;

        /// <summary>The titles of the pages open under the section, outermost first; empty at the section's own rows.</summary>
        public IReadOnlyList<string> Trail => _trail;

        /// <summary>The rows, in order; set with SetRows.</summary>
        public IReadOnlyList<PageRow> Rows => _rows;

        /// <summary>The focused row's key, or null when there are no rows.</summary>
        public string? Focused => _focusKey;

        /// <summary>Gives the pane's preview and lines for a row's key; null, or no delegate, draws the row's sentence.</summary>
        public Func<string, SettingsPane?>? PaneFor { get => _paneFor; set { _paneFor = value; Fill(); } }

        /// <summary>Gives the help bar's entries for the focused row's key, generated from what the host declares.</summary>
        public Func<string?, IReadOnlyList<PageHint>>? HintsFor { get => _hintsFor; set { _hintsFor = value; RefreshHints(); } }

        /// <summary>The help bar's entries as drawn now.</summary>
        public IReadOnlyList<HintEntry> Hints => _hintBar.Entries ?? Array.Empty<HintEntry>();

        /// <summary>How many whole rows fit under the strip: as many as the pane is tall.</summary>
        public int RowsShown => (int)Math.Floor(PaneHeight / RowHeight);

        /// <summary>The rows drawn now, first to last.</summary>
        public IReadOnlyList<string> Shown => _rows.Skip(_first).Take(RowsShown).Select(r => r.Key).ToList();

        /// <summary>Raised when a section is chosen with the shoulders or a click, never by SetSections or SelectSection.</summary>
        public event Action<string>? SectionChosen;

        /// <summary>Raised when A or a click chooses the focused row, if it is enabled.</summary>
        public event Action<string>? RowChosen;

        /// <summary>Raised when Left or Right steps a Steps row, with its key and the step.</summary>
        public event Action<string, int>? RowStepped;

        /// <summary>Raised when a pad button the hints give a key to is pressed, with that key.</summary>
        public event Action<string>? HintInvoked;

        /// <summary>Raised when the focus moves to another row.</summary>
        public event Action<string?>? FocusChanged;

        /// <summary>Draws the screen again from what it holds, such as after a preview's picture is ready.</summary>
        public void Refresh() => Fill();

        /// <summary>Gives the screen its sections and the chosen one; raises nothing.</summary>
        /// <param name="sections">The sections in order; their keys must be distinct.</param>
        /// <param name="selected">The chosen section's key; null or an unknown key chooses the first.</param>
        public void SetSections(IEnumerable<AdaptiveSection> sections, string? selected = null)
        {
            _sections = sections?.ToList() ?? throw new ArgumentNullException(nameof(sections));
            if (_sections.Select(s => s.Key).Distinct().Count() != _sections.Count) throw new ArgumentException("Two sections share a key.", nameof(sections));
            _selected = _sections.Any(s => s.Key == selected) ? selected : _sections.FirstOrDefault()?.Key;
            Fill();
        }

        /// <summary>Chooses a section without raising SectionChosen.</summary>
        /// <param name="key">The section's key.</param>
        /// <returns>Whether there is such a section.</returns>
        public bool SelectSection(string key)
        {
            if (!_sections.Any(s => s.Key == key)) return false;
            _selected = key;
            Fill();
            return true;
        }

        /// <summary>Steps the chosen section by one, wrapping, raising SectionChosen; nothing while a page is open.</summary>
        /// <param name="by">-1 for the previous section, 1 for the next.</param>
        /// <returns>Whether it stepped.</returns>
        public bool StepSection(int by)
        {
            if (_trail.Count > 0 || _sections.Count < 2) return false;
            int at = Math.Max(0, _sections.ToList().FindIndex(s => s.Key == _selected));
            _selected = _sections[((at + by) % _sections.Count + _sections.Count) % _sections.Count].Key;
            Fill();
            SectionChosen?.Invoke(_selected);
            return true;
        }

        /// <summary>Gives the titles of the pages open under the section, drawn on the strip in place of the sections; empty for the section's own rows.</summary>
        /// <param name="titles">The titles, outermost first.</param>
        public void SetTrail(IEnumerable<string> titles)
        {
            _trail = titles?.ToList() ?? throw new ArgumentNullException(nameof(titles));
            Fill();
        }

        /// <summary>Gives the screen its rows, the focus on a key where given and present, else kept where it still is, else on the first row.</summary>
        /// <param name="rows">The rows in order; their keys must be distinct.</param>
        /// <param name="focus">The row to focus; null keeps the focus where it can.</param>
        public void SetRows(IEnumerable<PageRow> rows, string? focus = null)
        {
            _rows = rows?.ToList() ?? throw new ArgumentNullException(nameof(rows));
            if (_rows.Select(r => r.Key).Distinct().Count() != _rows.Count) throw new ArgumentException("Two rows share a key.", nameof(rows));
            string? before = _focusKey;
            if (focus is not null && _rows.Any(r => r.Key == focus)) _focusKey = focus;
            else if (_focusKey is null || !_rows.Any(r => r.Key == _focusKey)) { _focusKey = _rows.FirstOrDefault()?.Key; _first = 0; }
            Fill();
            if (before != _focusKey) FocusChanged?.Invoke(_focusKey);
        }

        /// <summary>Puts the focus on a row.</summary>
        /// <param name="key">The row's key.</param>
        /// <returns>Whether there is such a row.</returns>
        public bool FocusOn(string key)
        {
            if (!_rows.Any(r => r.Key == key)) return false;
            _focusKey = key;
            Fill();
            FocusChanged?.Invoke(_focusKey);
            return true;
        }

        /// <summary>Moves the focus with the directional pad or the arrow keys: Up and Down between rows, Left and Right stepping a Steps row.</summary>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        /// <returns>Whether anything moved or stepped.</returns>
        public bool Move(int dx, int dy)
        {
            if (_rows.Count == 0) return false;
            int at = Math.Max(0, _rows.ToList().FindIndex(r => r.Key == _focusKey));
            if (dx != 0)
            {
                if (_rows[at] is not { Kind: PageRowKind.Steps, IsEnabled: true } stepped) return false;
                RowStepped?.Invoke(stepped.Key, Math.Sign(dx));
                return true;
            }
            int to = at + Math.Sign(dy);
            if (to < 0 || to >= _rows.Count) return false;
            _focusKey = _rows[to].Key;
            Fill();
            FocusChanged?.Invoke(_focusKey);
            return true;
        }

        /// <summary>A pad button: A chooses the focused row, the shoulders step the sections at the section's own rows, and a button the hints give a key raises HintInvoked.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the screen used it; one declined is the host's, as B and Start are.</returns>
        public bool Press(PadGlyphButton button)
        {
            if (button is PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder && _trail.Count == 0) return StepSection(button == PadGlyphButton.LeftShoulder ? -1 : 1);
            if (button == PadGlyphButton.South && _rows.FirstOrDefault(r => r.Key == _focusKey) is { } row)
            {
                if (row.IsEnabled) RowChosen?.Invoke(row.Key);
                return true;
            }
            if (CurrentHints().FirstOrDefault(h => h.Button == button && h.Key is not null) is { } hint)
            {
                HintInvoked?.Invoke(hint.Key!);
                return true;
            }
            return false;
        }

        private IReadOnlyList<PageHint> CurrentHints() => HintsFor?.Invoke(_focusKey) ?? Array.Empty<PageHint>();

        private void RefreshHints()
        {
            var entries = CurrentHints().Select(h => new HintEntry(h.Text) { Button = h.Button }).ToList();
            _hintBar.Entries = entries;
            AutomationProperties.SetName(_hintBar, string.Join(", ", entries.Select(e => e.Label)));
        }

        // The window of rows moves only when the focus leaves it, a row at a time.
        private void KeepRowShown()
        {
            int at = Math.Max(0, _rows.ToList().FindIndex(r => r.Key == _focusKey));
            if (at < _first) _first = at;
            if (at >= _first + RowsShown) _first = at - RowsShown + 1;
            _first = Math.Clamp(_first, 0, Math.Max(0, _rows.Count - RowsShown));
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            double units = e.NewSize.Width / Unit(MenuPanel.GetScale(this));
            if (Math.Abs(units - _unitsWide) < 0.5) return;
            _unitsWide = units;
            Fill();
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MenuPanel.ScaleProperty || change.Property == FormProperty) Fill();
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Pane, () => _trail.LastOrDefault() ?? _sections.FirstOrDefault(s => s.Key == _selected)?.Label);

        private static double Unit(double u) => double.IsFinite(u) && u > 0 ? u : 1;

        private static LunaTypeScale Type => LunaTokens.BigScreenType;

        private double PaneLeft => _unitsWide - Side - PaneWidth;

        private void Fill()
        {
            if (Form == AdaptiveForm.Desktop)
            {
                FillDesktop();
                return;
            }
            KeepRowShown();
            foreach (Control c in new Control[] { _strip, _hintBar })
                switch (c.Parent)
                {
                    case Panel panel: panel.Children.Remove(c); break;
                    case Decorator decorator: decorator.Child = null; break;
                }
            var surface = new Panel { Width = _unitsWide, Height = DesignHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            surface[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            FillStrip(surface);
            FillRows(surface);
            FillPane(surface);
            surface.Children.Add(HelpBand.Make(_hintBar, "SettingsHelpBar"));
            var scaled = new LayoutTransformControl { Child = surface, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            scaled.Bind(LayoutTransformControl.LayoutTransformProperty, this.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(Unit(u), Unit(u))));
            _root.Children.Clear();
            _root.Children.Add(scaled);
            RefreshHints();
        }

        // The sections, or the trail of open pages with the last chosen; only whole names drawn.
        private void FillStrip(Panel surface)
        {
            _strip.Children.Clear();
            int chosen = 0, n = 0;
            if (_trail.Count > 0)
            {
                foreach (string title in _trail)
                {
                    bool last = n == _trail.Count - 1;
                    var crumb = new ToggleButton { Content = title.ToUpperInvariant(), IsChecked = last, Focusable = false, IsTabStop = false, IsHitTestVisible = false };
                    AutomationProperties.SetName(crumb, title);
                    _strip.Children.Add(crumb);
                    n++;
                }
                chosen = _trail.Count - 1;
            }
            else
                foreach (AdaptiveSection s in _sections)
                {
                    var button = new ToggleButton { Content = s.Label.ToUpperInvariant(), IsChecked = s.Key == _selected, Focusable = false, IsTabStop = false };
                    AutomationProperties.SetName(button, s.Label);
                    string key = s.Key;
                    button.Click += (_, _) =>
                    {
                        if (key != _selected) { _selected = key; Fill(); SectionChosen?.Invoke(key); }
                        else button.IsChecked = true;
                    };
                    if (s.Key == _selected) chosen = n;
                    n++;
                    _strip.Children.Add(button);
                }
            _strip.Chosen = chosen;
            _strip.Height = StripHeight;
            _strip.Margin = new Thickness(Side, StripTop, Side, 0);
            _strip.VerticalAlignment = VerticalAlignment.Top;
            surface.Children.Add(_strip);
        }

        private void FillRows(Panel surface)
        {
            var stack = new StackPanel { Name = "SettingsRows", Width = Math.Max(0, PaneLeft - PaneGap - Side), Margin = new Thickness(Side, ContentTop, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(stack, _trail.LastOrDefault() ?? _sections.FirstOrDefault(s => s.Key == _selected)?.Label ?? "Settings");
            foreach (PageRow row in _rows.Skip(_first).Take(RowsShown))
            {
                string key = row.Key;
                stack.Children.Add(PageRowLook.Big(row, row.Key == _focusKey, "SettingsRow.", () =>
                {
                    if (key != _focusKey) FocusOn(key);
                    else if (row.IsEnabled) RowChosen?.Invoke(key);
                }));
            }
            surface.Children.Add(stack);
        }

        private void FillPane(Panel surface)
        {
            var pane = new Border { Name = "SettingsPane", Width = PaneWidth, Height = PaneHeight, Margin = new Thickness(Math.Max(0, PaneLeft), ContentTop, 0, 0), CornerRadius = new CornerRadius(8), Padding = new Thickness(PaneInset), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true };
            pane[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            var stack = new StackPanel { Spacing = 8 };
            PageRow? row = _rows.FirstOrDefault(r => r.Key == _focusKey);
            SettingsPane? said = _focusKey is null ? null : PaneFor?.Invoke(_focusKey);
            if (said is null && row?.Sentence is { Length: > 0 } sentence) said = new SettingsPane([new SettingsLine(sentence)]);
            if (said?.Preview is { } preview) stack.Children.Add(Preview(preview, PreviewWidth, PreviewHeight, Type));
            foreach (SettingsLine line in said?.Lines ?? Array.Empty<SettingsLine>()) stack.Children.Add(Line(line, Type.Body));
            pane.Child = stack;
            surface.Children.Add(pane);
        }

        private static Control Line(SettingsLine line, double size)
        {
            var text = new TextBlock { FontFamily = LunaFonts.Body, FontSize = line.Size ?? size, TextWrapping = TextWrapping.Wrap, Name = "SettingsLine" };
            string tone = line.Tone == SettingsTone.Muted ? "LunaMuted" : "LunaText";
            if (line.Mark is { Length: > 0 } mark)
            {
                var run = new Avalonia.Controls.Documents.Run(mark + " ");
                run[!Avalonia.Controls.Documents.TextElement.ForegroundProperty] = new DynamicResourceExtension(line.Tone switch { SettingsTone.Success => "LunaSuccess", SettingsTone.Attention => "LunaAttention", _ => tone });
                var rest = new Avalonia.Controls.Documents.Run(line.Text);
                rest[!Avalonia.Controls.Documents.TextElement.ForegroundProperty] = new DynamicResourceExtension(tone);
                text.Inlines = [run, rest];
            }
            else
            {
                text.Text = line.Text;
                text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(tone);
            }
            return text;
        }

        private static Control Preview(SettingsPreview preview, double width, double height, LunaTypeScale type)
        {
            switch (preview)
            {
                case SettingsMeter meter:
                {
                    double f = Math.Clamp(double.IsFinite(meter.Fraction) ? meter.Fraction : 0, 0, 1);
                    var fill = new Border { Width = width * f, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(MeterHeight / 2) };
                    fill[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaAccent");
                    var bar = new Border { Name = "SettingsMeter", Width = width, Height = MeterHeight, CornerRadius = new CornerRadius(MeterHeight / 2), Child = fill, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, PreviewGap - 8) };
                    bar[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaBorder");
                    return bar;
                }
                case SettingsVariants variants:
                {
                    var row = new StackPanel { Name = "SettingsVariants", Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(0, 0, 0, PreviewGap - 8) };
                    row.Children.Add(Variant(variants.DarkName, ThemeVariant.Dark, (width - 16) / 2, type));
                    row.Children.Add(Variant(variants.LightName, ThemeVariant.Light, (width - 16) / 2, type));
                    return row;
                }
                case SettingsPicture picture:
                {
                    var box = new Panel { Name = "SettingsPicture", Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, PreviewGap - 8) };
                    var back = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true };
                    back[!Border.BackgroundProperty] = new DynamicResourceExtension(picture.Picture is null ? "LunaSurface" : "LunaVoid");
                    if (picture.Picture is { } image) back.Child = new Image { Source = image, Stretch = Stretch.Uniform };
                    box.Children.Add(back);
                    if (picture.Caption is { Length: > 0 } caption)
                    {
                        var words = new TextBlock { Name = "SettingsCaption", IsHitTestVisible = false, Text = caption, FontFamily = LunaFonts.Condensed, FontSize = type.Title, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxLines = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0) };
                        words[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(picture.Picture is null ? "LunaTextStrong" : "LunaOnArt");
                        if (picture.Picture is not null) words.Effect = new DropShadowEffect { BlurRadius = 6, OffsetX = 0, OffsetY = 1, Opacity = 0.8 };
                        box.Children.Add(words);
                    }
                    return box;
                }
                default:
                    return new Panel();
            }
        }

        // One variant drawn through its own palette, so the swatch is the tokens of that variant rather than literal colours.
        private static Control Variant(string name, ThemeVariant variant, double width, LunaTypeScale type)
        {
            var words = new TextBlock { Text = name, FontFamily = LunaFonts.Body, FontSize = type.Caption, Margin = new Thickness(12, 12, 12, 0) };
            var swatch = new Border { Width = width, Height = width * VariantHeight / VariantWidth, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = words };
            // The colour keys, not the shared brushes, since only those resolve through the scope's own variant.
            words.Bind(TextBlock.ForegroundProperty, words.GetResourceObservable("LunaTextColor", c => c is Color col ? new SolidColorBrush(col) : null));
            swatch.Bind(Border.BackgroundProperty, swatch.GetResourceObservable("LunaSurfaceColor", c => c is Color col ? new SolidColorBrush(col) : null));
            swatch.Bind(Border.BorderBrushProperty, swatch.GetResourceObservable("LunaBorderColor", c => c is Color col ? new SolidColorBrush(col) : null));
            return new ThemeVariantScope { RequestedThemeVariant = variant, Child = swatch, Name = "SettingsVariant." + name };
        }
    }
}
