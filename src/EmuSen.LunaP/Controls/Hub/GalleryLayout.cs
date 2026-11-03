using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>What is focused on a gallery: a cover by its key, or a key of the docked keyboard.</summary>
    /// <param name="InKeyboard">Whether the focus is on the docked keyboard.</param>
    /// <param name="Key">The focused cover's key; null where none is.</param>
    public readonly record struct GalleryFocus(bool InKeyboard, string? Key);

    // A big screen's library: sections on the shoulders, a grid of covers in whole rows, the focused item's name and facts, and search with the keyboard docked under it - see docs/LunaP.md §212.5.
    /// <summary>A big screen's library of covers declared as data: a strip of sections with a count at its right, a grid of covers in whole rows on one baseline per row, the focused item's name and facts under it, and a search field with the keyboard docked at the foot.</summary>
    public class GalleryLayout : Border
    {
        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<GalleryLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>The design height; the gallery is laid out in units of it and scaled by MenuPanel.Scale.</summary>
        public const double DesignHeight = 800;

        /// <summary>The safe area's side, the strip's top and height.</summary>
        public const double Side = 64, StripTop = 40, StripHeight = 44;

        /// <summary>A cover's cell, the gap between cells, and the grid's top with and without the search field.</summary>
        public const double CellWidth = 180, CellHeight = 240, Gap = 24, GridTop = 108, SearchGridTop = 156;

        /// <summary>The search field's top and height, and the docked keyboard's top.</summary>
        public const double SearchTop = 100, SearchHeight = 44, KeyboardTop = 398;

        /// <summary>The focused item's name and facts under the grid.</summary>
        public const double NameTop = 628, FactsTop = 670;

        // The focused game's two lines as the text scale grows: the facts rise toward the grid, the name above them never into it (docs/LunaP.md §214).
        private static double FactsAt(double scale) => FactsTop - 26.7 * (scale - 1);

        private static double NameAt(double scale) => Math.Max(GridTop + 2 * CellHeight + Gap, FactsAt(scale) - 42 - 38.4 * (scale - 1));

        private readonly Panel _root = new();
        private readonly HintBar _hintBar = new();
        private readonly WholeRow _strip = new() { Spacing = 8, Name = "GallerySections", Selects = Avalonia.Automation.Peers.AutomationControlType.Tab };
        private readonly TextBox _search = new() { Name = "GallerySearch", PlaceholderText = "Search" };
        private IReadOnlyList<AdaptiveSection> _sections = Array.Empty<AdaptiveSection>();
        private IReadOnlyList<CoverItem> _items = Array.Empty<CoverItem>();
        private string? _selected, _focusKey;
        private string _count = string.Empty;
        private int _firstRow;
        private double _unitsWide = 1280;
        private OnScreenKeyboard? _keyboard;
        private bool _inKeyboard;

        /// <summary>An empty gallery.</summary>
        public GalleryLayout()
        {
            Focusable = true;
            ClipToBounds = true;
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            AdaptiveLayout.StyleStrip(_strip);
            AutomationProperties.SetName(_strip, "Sections");
            AutomationProperties.SetName(_search, "Search");
            _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && _keyboard is not null) SearchChanged?.Invoke(_search.Text ?? string.Empty); };
            Child = _root;
            Fill();
        }

        /// <summary>The pad whose printing the help bar draws.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        /// <summary>The sections, in order; set with SetSections.</summary>
        public IReadOnlyList<AdaptiveSection> Sections => _sections;

        /// <summary>The chosen section's key, or null when there are none.</summary>
        public string? SelectedSection => _selected;

        /// <summary>The covers, in order; set with SetItems.</summary>
        public IReadOnlyList<CoverItem> Items => _items;

        /// <summary>One line at the strip's right, such as how many items the section holds.</summary>
        public string Count { get => _count; set { _count = value ?? string.Empty; Fill(); } }

        /// <summary>Gives the help bar's entries for what is focused, generated from what the host declares.</summary>
        public Func<GalleryFocus, IReadOnlyList<PageHint>>? HintsFor { get; set; }

        /// <summary>Gives a cover's picture when it is drawn, so a long library decodes only what shows; null uses the item's own.</summary>
        public Func<CoverItem, IImage?>? PictureOf { get; set; }

        /// <summary>Draws the gallery again from what it holds, such as after a picture has been decoded.</summary>
        public void Refresh() => Fill();

        /// <summary>Raised when a person chooses a section with the shoulders or a click, never by SetSections or SelectSection.</summary>
        public event Action<string>? SectionChosen;

        /// <summary>Raised when a cover is chosen with A or a click, with its key.</summary>
        public event Action<string>? ItemChosen;

        /// <summary>Raised when a pad button the hints give a key to is pressed, with that key.</summary>
        public event Action<string>? HintInvoked;

        /// <summary>Raised when the focus moves to another cover or between the grid and the keyboard.</summary>
        public event Action<GalleryFocus>? FocusChanged;

        /// <summary>Raised as the search's text changes while the keyboard is docked.</summary>
        public event Action<string>? SearchChanged;

        /// <summary>Raised when the docked keyboard closes, with whether it was finished with Done rather than closed by B on an empty field.</summary>
        public event Action<bool>? SearchClosed;

        /// <summary>What is focused now.</summary>
        public GalleryFocus Focused => new(_inKeyboard && _keyboard is not null, _focusKey);

        /// <summary>The help bar's entries as drawn now.</summary>
        public IReadOnlyList<HintEntry> Hints => _hintBar.Entries ?? Array.Empty<HintEntry>();

        /// <summary>Whether the search field and its docked keyboard are on screen.</summary>
        public bool IsSearchOpen => _keyboard is not null;

        /// <summary>The docked keyboard while the search is open, else null.</summary>
        public OnScreenKeyboard? Keyboard => _keyboard;

        /// <summary>The search's text.</summary>
        public string SearchText => _search.Text ?? string.Empty;

        /// <summary>The number of covers across at the present width.</summary>
        public int Columns => Math.Max(1, (int)Math.Floor((_unitsWide - 2 * Side + Gap) / (CellWidth + Gap)));

        /// <summary>The whole rows drawn at once: two, or one under the search field.</summary>
        public int RowsShown => _keyboard is null ? 2 : 1;

        /// <summary>The covers drawn now, first to last.</summary>
        public IReadOnlyList<string> Shown => _items.Skip(_firstRow * Columns).Take(RowsShown * Columns).Select(i => i.Key).ToList();

        /// <summary>Gives the gallery its sections and the chosen one; raises nothing.</summary>
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

        /// <summary>Steps the chosen section by one, wrapping, raising SectionChosen.</summary>
        /// <param name="by">-1 for the previous section, 1 for the next.</param>
        /// <returns>Whether there were two or more sections to step between.</returns>
        public bool StepSection(int by)
        {
            if (_sections.Count < 2) return false;
            int at = Math.Max(0, _sections.ToList().FindIndex(s => s.Key == _selected));
            _selected = _sections[((at + by) % _sections.Count + _sections.Count) % _sections.Count].Key;
            Fill();
            SectionChosen?.Invoke(_selected);
            return true;
        }

        /// <summary>Gives the gallery its covers, keeping the focused one where it still is, else the first.</summary>
        /// <param name="items">The covers in order; their keys must be distinct.</param>
        public void SetItems(IEnumerable<CoverItem> items)
        {
            _items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
            if (_items.Select(i => i.Key).Distinct().Count() != _items.Count) throw new ArgumentException("Two covers share a key.", nameof(items));
            if (_focusKey is null || !_items.Any(i => i.Key == _focusKey))
            {
                _focusKey = _items.FirstOrDefault()?.Key;
                _firstRow = 0;
            }
            Fill();
        }

        /// <summary>Puts the focus on a cover, in the grid.</summary>
        /// <param name="key">The cover's key.</param>
        /// <returns>Whether there is such a cover.</returns>
        public bool FocusOn(string key)
        {
            if (!_items.Any(i => i.Key == key)) return false;
            _focusKey = key;
            _inKeyboard = false;
            Refocused();
            return true;
        }

        /// <summary>Shows the search field over the grid and docks the keyboard under it, the focus on its keys.</summary>
        /// <param name="text">The text the search starts with.</param>
        /// <param name="layouts">The keyboard's layouts, the first shown first.</param>
        public void OpenSearch(string text, IReadOnlyList<KeyboardLayout> layouts)
        {
            if (_keyboard is not null) return;
            _search.Text = text ?? string.Empty;
            _search.CaretIndex = _search.Text.Length;
            _keyboard = OnScreenKeyboard.CreateDocked(_search, layouts);
            _keyboard.Name = "GalleryKeyboard";
            OnScreenKeyboard docked = _keyboard;
            _ = docked.Closed.ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() => Undocked(docked, t.Result)), System.Threading.Tasks.TaskScheduler.Default);
            _inKeyboard = true;
            Refocused();
        }

        /// <summary>Closes the search field and its keyboard, keeping what was typed.</summary>
        /// <returns>Whether it was open.</returns>
        public bool CloseSearch()
        {
            if (_keyboard is not { } keyboard) return false;
            keyboard.Finish();
            Undocked(keyboard, true);
            return true;
        }

        private void Undocked(OnScreenKeyboard keyboard, bool done)
        {
            if (!ReferenceEquals(_keyboard, keyboard)) return;
            _keyboard = null;
            _inKeyboard = false;
            Refocused();
            SearchClosed?.Invoke(done);
        }

        /// <summary>Moves the focus with the directional pad or the arrow keys: through the grid by a cover or a row, or across the docked keyboard's keys.</summary>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        /// <returns>Whether anything moved.</returns>
        public bool Move(int dx, int dy)
        {
            if (_inKeyboard && _keyboard is { } keyboard)
            {
                keyboard.Move(dx, dy);
                RefreshHints();
                return true;
            }
            if (_items.Count == 0) return false;
            int at = Math.Max(0, _items.ToList().FindIndex(i => i.Key == _focusKey));
            int to = dx != 0 ? at + dx : at + dy * Columns;
            if (dy > 0 && to >= _items.Count && at / Columns < (_items.Count - 1) / Columns) to = _items.Count - 1;
            if (to < 0 || to >= _items.Count) return false;
            _focusKey = _items[to].Key;
            Refocused();
            return true;
        }

        /// <summary>A pad button: A chooses the cover or types the key, the shoulders step the sections or move between the keyboard and the grid, and a button the hints give a key raises HintInvoked.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the gallery used it; one declined is the host's.</returns>
        public bool Press(PadGlyphButton button)
        {
            if (_keyboard is { } keyboard)
            {
                if (button is PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder or PadGlyphButton.Shoulders)
                {
                    _inKeyboard = !_inKeyboard || _items.Count == 0;
                    Refocused();
                    return true;
                }
                if (_inKeyboard)
                {
                    switch (button)
                    {
                        case PadGlyphButton.South: keyboard.Press(); return true;
                        case PadGlyphButton.North: keyboard.Type(KeyboardLayout.Space); return true;
                        case PadGlyphButton.West: CloseSearch(); return true;
                        case PadGlyphButton.East:
                            if (!keyboard.Erase())
                            {
                                keyboard.Finish();
                                Undocked(keyboard, false);
                            }
                            return true;
                    }
                }
            }
            else if (button is PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder) return StepSection(button == PadGlyphButton.LeftShoulder ? -1 : 1);
            if (button == PadGlyphButton.South && !_inKeyboard && _focusKey is not null)
            {
                ItemChosen?.Invoke(_focusKey);
                return true;
            }
            if (CurrentHints().FirstOrDefault(h => h.Button == button && h.Key is not null) is { } hint)
            {
                HintInvoked?.Invoke(hint.Key!);
                return true;
            }
            return false;
        }

        private IReadOnlyList<PageHint> CurrentHints() => HintsFor?.Invoke(Focused) ?? Array.Empty<PageHint>();

        private void RefreshHints()
        {
            var entries = CurrentHints().Select(h => new HintEntry(h.Text) { Button = h.Button }).ToList();
            _hintBar.Entries = entries;
            AutomationProperties.SetName(_hintBar, string.Join(", ", entries.Select(e => e.Label)));
        }

        private void Refocused()
        {
            Fill();
            FocusChanged?.Invoke(Focused);
        }

        // The window of rows moves only when the focus leaves it, a whole row at a time.
        private void KeepRowShown()
        {
            int at = Math.Max(0, _items.ToList().FindIndex(i => i.Key == _focusKey)) / Columns;
            int rows = (_items.Count + Columns - 1) / Columns;
            if (at < _firstRow) _firstRow = at;
            if (at >= _firstRow + RowsShown) _firstRow = at - RowsShown + 1;
            _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, rows - RowsShown));
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
            if (change.Property == MenuPanel.ScaleProperty) Fill();
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Pane, () => _sections.FirstOrDefault(s => s.Key == _selected)?.Label);

        private static double Unit(double u) => double.IsFinite(u) && u > 0 ? u : 1;

        private static LunaTypeScale Type => LunaTokens.BigScreenType;

        private void Fill()
        {
            KeepRowShown();
            foreach (Control c in new Control[] { _strip, _search, _hintBar }) Detach(c);
            if (_keyboard is not null) Detach(_keyboard);
            var surface = new Panel { Width = _unitsWide, Height = DesignHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            surface[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            FillStrip(surface);
            if (_keyboard is not null) FillSearch(surface);
            FillGrid(surface);
            if (_keyboard is null) FillName(surface);
            surface.Children.Add(HelpBand.Make(_hintBar, "GalleryHelpBar"));
            if (_keyboard is { } keyboard)
            {
                keyboard.Margin = new Thickness(0, KeyboardTop, 0, 0);
                keyboard.Height = HelpBand.Top - KeyboardTop;
                keyboard.VerticalAlignment = VerticalAlignment.Top;
                keyboard.ShowsCurrentKey = _inKeyboard;
                surface.Children.Add(keyboard);
            }
            var scaled = new LayoutTransformControl { Child = surface, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            scaled.Bind(LayoutTransformControl.LayoutTransformProperty, this.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(Unit(u), Unit(u))));
            _root.Children.Clear();
            _root.Children.Add(scaled);
            RefreshHints();
        }

        private static void Detach(Control control)
        {
            switch (control.Parent)
            {
                case Panel panel: panel.Children.Remove(control); break;
                case Decorator decorator: decorator.Child = null; break;
            }
        }

        private void FillStrip(Panel surface)
        {
            _strip.Children.Clear();
            int chosen = 0, n = 0;
            foreach (AdaptiveSection s in _sections)
            {
                string key = s.Key;
                bool Choose()
                {
                    if (key == _selected) return false;
                    _selected = key;
                    Fill();
                    SectionChosen?.Invoke(key);
                    return true;
                }
                var button = new SectionTab(() => key == _selected, () => Choose()) { Content = s.Label.ToUpperInvariant(), IsChecked = s.Key == _selected, Focusable = false, IsTabStop = false };
                AutomationProperties.SetName(button, s.Status is { Length: > 0 } status ? $"{s.Label}, {status}" : s.Label);
                button.Click += (_, _) => { if (!Choose()) button.IsChecked = true; };
                if (s.Key == _selected) chosen = n;
                n++;
                _strip.Children.Add(button);
            }
            _strip.Chosen = chosen;
            var count = new TextBlock { Name = "GalleryCount", Text = _count, FontFamily = LunaFonts.Body, FontSize = Type.Caption, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 0, 0) };
            count[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
            var row = new DockPanel { Height = StripHeight, Margin = new Thickness(Side, StripTop, Side, 0), VerticalAlignment = VerticalAlignment.Top, LastChildFill = true };
            DockPanel.SetDock(count, Dock.Right);
            row.Children.Add(count);
            row.Children.Add(_strip);
            surface.Children.Add(row);
        }

        private void FillSearch(Panel surface)
        {
            _search.FontFamily = LunaFonts.Body;
            _search.FontSize = Type.Body;
            _search.Height = SearchHeight;
            _search.Margin = new Thickness(Side, SearchTop, Side, 0);
            _search.VerticalAlignment = VerticalAlignment.Top;
            _search.VerticalContentAlignment = VerticalAlignment.Center;
            _search.Padding = new Thickness(16, 0);
            _search.CornerRadius = new CornerRadius(4);
            _search.IsReadOnly = true;
            _search.Focusable = false;
            _search[!TextBox.BackgroundProperty] = new DynamicResourceExtension("LunaInputSurface");
            _search[!TextBox.ForegroundProperty] = new DynamicResourceExtension("LunaTextStrong");
            _search[!TextBox.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            _search.BorderThickness = new Thickness(1);
            surface.Children.Add(_search);
        }

        // Whole rows of cells, centred, each cover standing on its cell's foot.
        private void FillGrid(Panel surface)
        {
            int columns = Columns;
            double width = columns * CellWidth + (columns - 1) * Gap, left = (_unitsWide - width) / 2;
            double top = _keyboard is null ? GridTop : SearchGridTop;
            var grid = new Panel { Name = "GalleryGrid", Width = width, Height = RowsShown * CellHeight + (RowsShown - 1) * Gap, Margin = new Thickness(left, top, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(grid, _sections.FirstOrDefault(s => s.Key == _selected)?.Label ?? "Items");
            int i = 0;
            foreach (CoverItem item in _items.Skip(_firstRow * columns).Take(RowsShown * columns))
            {
                CoverItem drawn = item.Picture is null && PictureOf?.Invoke(item) is { } picture ? item with { Picture = picture } : item;
                var card = new CoverCard { CellWidth = CellWidth, CellHeight = CellHeight, Item = drawn, ShowsFocus = !_inKeyboard && item.Key == _focusKey, Name = "GalleryTile." + item.Key, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                card.Margin = new Thickness(i % columns * (CellWidth + Gap), i / columns * (CellHeight + Gap), 0, 0);
                string key = item.Key;
                card.PointerPressed += (_, _) => { if (key == _focusKey) ItemChosen?.Invoke(key); else FocusOn(key); };
                grid.Children.Add(card);
                i++;
            }
            surface.Children.Add(grid);
        }

        private void FillName(Panel surface)
        {
            if (_items.FirstOrDefault(i => i.Key == _focusKey) is not { } item) return;
            var name = new TextBlock { Name = "GalleryName", Text = item.Title, FontFamily = LunaFonts.Condensed, FontSize = Type.Title, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(Side, NameAt(LunaTokens.TextScale), Side, 0), VerticalAlignment = VerticalAlignment.Top };
            name[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaTextStrong");
            surface.Children.Add(name);
            if (item.Facts is not { Length: > 0 } facts) return;
            var line = new TextBlock { Name = "GalleryFacts", Text = facts, FontFamily = LunaFonts.Body, FontSize = Type.Body, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(Side, FactsAt(LunaTokens.TextScale), Side, 0), VerticalAlignment = VerticalAlignment.Top };
            line[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
            surface.Children.Add(line);
        }
    }
}
