using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // A hub declared as data: the way back to what was left, a shelf of covers and a row of places with a live line each, in both forms - see docs/LunaP.md §212.
    /// <summary>A hub screen declared as data: Continue with a paused picture in a poster slot shaped for video, a shelf of covers on one baseline, and a row of places each with one live line; arranged for the big screen or the desktop.</summary>
    public partial class HubLayout : Border
    {
        /// <summary>Defines the Form property.</summary>
        public static readonly StyledProperty<AdaptiveForm> FormProperty = AvaloniaProperty.Register<HubLayout, AdaptiveForm>(nameof(Form));

        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<HubLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>The big screen's design height; the hub is laid out in units of it and scaled by MenuPanel.Scale.</summary>
        public const double DesignHeight = 800;

        /// <summary>The safe area's side and top on the big screen.</summary>
        public const double Side = 64, Top = 40;

        /// <summary>Continue's picture on the big screen: its left, top, width and height.</summary>
        public const double ContinueTop = 84, ContinueWidth = 373, ContinueHeight = 280;

        /// <summary>The shelf's top, a cover's cell and the step from one cell to the next on the big screen.</summary>
        public const double ShelfTop = 416, TileWidth = 120, TileHeight = 160, TileStep = 144;

        /// <summary>The shelf's top at night, the mockup's, 8 lower than by day so Continue's glass panel clears the label above it (docs/LunaP.md §221.4).</summary>
        public const double NightShelfTop = 424;

        /// <summary>The least space between Continue's glass panel, its ring included, and the shelf's label at night: the mockup's 8 units.</summary>
        public const double NightPanelGap = 8;

        /// <summary>The space between the shelf and the focused cover's name under it at night, 2 rather than 6, so the name at the largest text size still ends above the places.</summary>
        public const double NightTitleGap = 2;

        /// <summary>The space between the shelf's label and the highest the tallest cover reaches focused, grown with its ring - the mockup's 5 units (docs/LunaP.md §212.12).</summary>
        public const double ShelfLabelGap = 5;

        /// <summary>The places' top, a place's width and height and the gap between two on the big screen.</summary>
        public const double PlacesTop = 622, PlaceWidth = 150, PlaceHeight = 64, PlaceGap = 17;

        /// <summary>The desktop's Continue card height, a cover's cell, the step between cells, a place's height and the gutter.</summary>
        public const double DesktopContinueHeight = 180, DesktopTileWidth = 100, DesktopTileHeight = 133, DesktopTileStep = 116, DesktopPlaceHeight = 64, DesktopGutter = 24, DesktopGap = 16;

        private readonly Panel _root = new();
        private readonly HintBar _hintBar = new();
        private HubContinue? _continue;
        private IReadOnlyList<CoverItem> _shelf = Array.Empty<CoverItem>();
        private IReadOnlyList<HubPlace> _places = Array.Empty<HubPlace>();
        private HubRow _row = HubRow.Continue;
        private string? _tileKey, _placeKey;
        private int _firstTile;
        private double _unitsWide;
        private string _status = string.Empty, _title = "Home", _recentTitle = "Recent", _placesTitle = "Places";
        private bool _showsArt = true, _night;
        private string? _mark;
        private readonly LunaSky _sky = new() { Name = "HubSky" };

        /// <summary>An empty hub in the desktop form.</summary>
        public HubLayout()
        {
            Focusable = true;
            ClipToBounds = true;
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            Child = _root;
            Fill();
        }

        /// <summary>The form the hub is arranged in: Desktop, or BigScreen; the Card form is drawn as the big screen.</summary>
        public AdaptiveForm Form { get => GetValue(FormProperty); set => SetValue(FormProperty, value); }

        /// <summary>The pad whose printing the help bar draws.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        private bool Big => Form != AdaptiveForm.Desktop;

        /// <summary>The way back to what was left; null when nothing was, and the top shows the focused cover instead.</summary>
        public HubContinue? Continue { get => _continue; set { _continue = value; if (value is null && _row == HubRow.Continue) _row = _shelf.Count > 0 ? HubRow.Shelf : HubRow.Places; Fill(); } }

        /// <summary>The shelf's covers, in order; set with SetShelf.</summary>
        public IReadOnlyList<CoverItem> Shelf => _shelf;

        /// <summary>The places, in order; set with SetPlaces.</summary>
        public IReadOnlyList<HubPlace> Places => _places;

        /// <summary>One line at the top right, such as the controllers connected and the time.</summary>
        public string Status { get => _status; set { _status = value ?? string.Empty; Fill(); } }

        /// <summary>The desktop header's name, such as Home.</summary>
        public string Title { get => _title; set { _title = value ?? string.Empty; Fill(); } }

        /// <summary>The words above the shelf, drawn in upper case, such as Recent.</summary>
        public string RecentTitle { get => _recentTitle; set { _recentTitle = value ?? string.Empty; Fill(); } }

        /// <summary>The desktop's words above the places, drawn in upper case, such as Places.</summary>
        public string PlacesTitle { get => _placesTitle; set { _placesTitle = value ?? string.Empty; Fill(); } }

        /// <summary>Whether the focused cover's art is drawn behind the big screen's hub, dimmed and blurred; true by default.</summary>
        public bool ShowsArt { get => _showsArt; set { _showsArt = value; Fill(); } }

        /// <summary>Whether the big screen's hub stands on the night sky, with glass panels and a glow in the focus's hue; false by default (docs/LunaP.md §221.4).</summary>
        public bool Night { get => _night; set { _night = value; Fill(); } }

        /// <summary>A wordmark drawn with a small crescent at the top right at night, such as the application's name; null for none.</summary>
        public string? Mark { get => _mark; set { _mark = value; Fill(); } }

        /// <summary>The sky behind the big screen's hub at night.</summary>
        public LunaSky Sky => _sky;

        /// <summary>Gives the help bar's entries for what is focused, generated from what the host declares.</summary>
        public Func<HubFocus, IReadOnlyList<PageHint>>? HintsFor { get; set; }

        /// <summary>Gives a cover's picture when it is drawn, so a long shelf decodes only what shows; null uses the item's own.</summary>
        public Func<CoverItem, IImage?>? PictureOf { get; set; }

        /// <summary>Gives the art behind the hub for the focused cover when it is drawn; null uses the item's own.</summary>
        public Func<CoverItem, IImage?>? ArtOf { get; set; }

        /// <summary>Draws the hub again from what it holds, such as after a picture has been decoded.</summary>
        public void Refresh() => Fill();

        /// <summary>Raised when Continue is chosen with A, a click or Enter.</summary>
        public event Action? ContinueChosen;

        /// <summary>Raised when a cover is chosen with A, a double click or Enter, with its key.</summary>
        public event Action<string>? TileChosen;

        /// <summary>Raised when a place is chosen with A, a click or Enter, with its key.</summary>
        public event Action<string>? PlaceChosen;

        /// <summary>Raised when a pad button the hints give a key to is pressed, with that key.</summary>
        public event Action<string>? HintInvoked;

        /// <summary>Raised when the focus moves to another row, cover or place.</summary>
        public event Action<HubFocus>? FocusChanged;

        /// <summary>What is focused now.</summary>
        public HubFocus Focused => new(_row, _row switch { HubRow.Shelf => _tileKey, HubRow.Places => _placeKey, _ => null });

        /// <summary>The help bar's entries as drawn now.</summary>
        public IReadOnlyList<HintEntry> Hints => _hintBar.Entries ?? Array.Empty<HintEntry>();

        /// <summary>The covers drawn now, first to last: a shelf longer than the screen shows a window of whole covers around the focused one.</summary>
        public IReadOnlyList<string> ShelfShown => _shelf.Skip(_firstTile).Take(ShelfFit()).Select(t => t.Key).ToList();

        /// <summary>Gives the shelf its covers, keeping the focused one where it still is.</summary>
        /// <param name="covers">The covers in order; their keys must be distinct.</param>
        public void SetShelf(IEnumerable<CoverItem> covers)
        {
            _shelf = covers?.ToList() ?? throw new ArgumentNullException(nameof(covers));
            if (_shelf.Select(t => t.Key).Distinct().Count() != _shelf.Count) throw new ArgumentException("Two covers share a key.", nameof(covers));
            if (_tileKey is null || !_shelf.Any(t => t.Key == _tileKey)) { _tileKey = _shelf.FirstOrDefault()?.Key; _firstTile = 0; }
            if (_row == HubRow.Shelf && _shelf.Count == 0) _row = _continue is not null ? HubRow.Continue : HubRow.Places;
            Fill();
        }

        /// <summary>Gives the hub its places, keeping the focused one where it still is.</summary>
        /// <param name="places">The places in order; their keys must be distinct.</param>
        public void SetPlaces(IEnumerable<HubPlace> places)
        {
            _places = places?.ToList() ?? throw new ArgumentNullException(nameof(places));
            if (_places.Select(p => p.Key).Distinct().Count() != _places.Count) throw new ArgumentException("Two places share a key.", nameof(places));
            if (_placeKey is null || !_places.Any(p => p.Key == _placeKey)) _placeKey = _places.FirstOrDefault()?.Key;
            Fill();
        }

        /// <summary>Puts the focus where a hub starts: on Continue, or on the first cover when nothing was left, or on the first place.</summary>
        public void FocusStart()
        {
            _row = _continue is not null ? HubRow.Continue : _shelf.Count > 0 ? HubRow.Shelf : HubRow.Places;
            if (_row == HubRow.Shelf) { _tileKey = _shelf[0].Key; _firstTile = 0; }
            Refocused();
        }

        /// <summary>Puts the focus on a row, and on the cover or place given there.</summary>
        /// <param name="row">The row to put the focus on.</param>
        /// <param name="key">A cover's or place's key on that row, or null to keep the one focused last.</param>
        /// <returns>Whether the row has anything to focus.</returns>
        public bool FocusOn(HubRow row, string? key = null)
        {
            switch (row)
            {
                case HubRow.Continue when _continue is null: return false;
                case HubRow.Shelf when _shelf.Count == 0: return false;
                case HubRow.Places when _places.Count == 0: return false;
                case HubRow.Shelf when key is not null && _shelf.Any(t => t.Key == key): _tileKey = key; break;
                case HubRow.Places when key is not null && _places.Any(p => p.Key == key): _placeKey = key; break;
            }
            _row = row;
            Refocused();
            return true;
        }

        /// <summary>Moves the focus with the directional pad or the arrow keys: Up and Down between the rows, Left and Right along the shelf or the places.</summary>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        /// <returns>Whether the focus moved.</returns>
        public bool Move(int dx, int dy)
        {
            if (dy != 0)
            {
                List<HubRow> rows = Rows();
                int at = rows.IndexOf(_row) + dy;
                if (at < 0 || at >= rows.Count) return false;
                HubRow next = rows[at];
                // A row below or above takes the item nearest to the one left, as a grid's column does.
                if (next == HubRow.Places && _row == HubRow.Shelf) _placeKey = NearestPlace();
                if (next == HubRow.Shelf && _row == HubRow.Places) _tileKey = NearestTile();
                _row = next;
                Refocused();
                return true;
            }
            if (dx == 0) return false;
            switch (_row)
            {
                case HubRow.Shelf:
                    int t = IndexOf(_shelf.Select(s => s.Key), _tileKey) + dx;
                    if (t < 0 || t >= _shelf.Count) return false;
                    _tileKey = _shelf[t].Key;
                    break;
                case HubRow.Places:
                    int p = IndexOf(_places.Select(s => s.Key), _placeKey) + dx;
                    if (p < 0 || p >= _places.Count) return false;
                    _placeKey = _places[p].Key;
                    break;
                default:
                    return false;
            }
            Refocused();
            return true;
        }

        /// <summary>A pad button: A chooses what is focused, and a button the hints give a key raises HintInvoked.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the hub used it; one declined is the host's.</returns>
        public bool Press(PadGlyphButton button)
        {
            if (button == PadGlyphButton.South) return Accept();
            if (CurrentHints().FirstOrDefault(h => h.Button == button && h.Key is not null) is { } hint)
            {
                HintInvoked?.Invoke(hint.Key!);
                return true;
            }
            return false;
        }

        private bool Accept()
        {
            switch (_row)
            {
                case HubRow.Continue when _continue is not null: ContinueChosen?.Invoke(); return true;
                case HubRow.Shelf when _tileKey is not null: TileChosen?.Invoke(_tileKey); return true;
                case HubRow.Places when _placeKey is not null: PlaceChosen?.Invoke(_placeKey); return true;
                default: return false;
            }
        }

        private List<HubRow> Rows()
        {
            var rows = new List<HubRow>();
            if (_continue is not null) rows.Add(HubRow.Continue);
            if (_shelf.Count > 0) rows.Add(HubRow.Shelf);
            if (_places.Count > 0) rows.Add(HubRow.Places);
            return rows;
        }

        private static int IndexOf(IEnumerable<string> keys, string? key) => Math.Max(0, keys.ToList().IndexOf(key ?? string.Empty));

        // The place under the middle of the focused cover, and the cover over the middle of the focused place.
        private string? NearestPlace()
        {
            if (_places.Count == 0) return null;
            int shown = IndexOf(_shelf.Select(s => s.Key), _tileKey) - _firstTile;
            double middle = Big ? shown * TileStep + TileWidth / 2 : shown * DesktopTileStep + DesktopTileWidth / 2;
            double each = PlaceStep();
            int columns = Big ? _places.Count : Math.Min(4, _places.Count);
            int at = Math.Clamp((int)(middle / Math.Max(1, each)), 0, columns - 1);
            return _places[at].Key;
        }

        private string? NearestTile()
        {
            if (_shelf.Count == 0) return null;
            int place = IndexOf(_places.Select(p => p.Key), _placeKey);
            int columns = Big ? _places.Count : Math.Min(4, _places.Count);
            double middle = (place % Math.Max(1, columns)) * PlaceStep() + PlaceStep() / 2;
            int at = (int)(middle / (Big ? TileStep : DesktopTileStep));
            return _shelf[Math.Clamp(_firstTile + Math.Min(at, ShelfFit() - 1), 0, _shelf.Count - 1)].Key;
        }

        private double PlaceStep()
        {
            double room = Big ? Math.Max(0, _unitsWide - 2 * Side) : Math.Max(0, _unitsWide - 2 * DesktopGutter);
            int columns = Big ? Math.Max(1, _places.Count) : Math.Max(1, Math.Min(4, _places.Count));
            double gap = Big ? PlaceGap : DesktopGap;
            return (room + gap) / columns;
        }

        // Whole covers only: as many as fit the room between the safe edges.
        private int ShelfFit()
        {
            double room = Big ? _unitsWide - 2 * Side : _unitsWide - 2 * DesktopGutter;
            double step = Big ? TileStep : DesktopTileStep, cell = Big ? TileWidth : DesktopTileWidth;
            return Math.Max(1, (int)Math.Floor((room - cell) / step) + 1);
        }

        // The window of covers moves only when the focus leaves it, a whole cover at a time.
        private void KeepTileShown()
        {
            int at = IndexOf(_shelf.Select(s => s.Key), _tileKey), fit = ShelfFit();
            if (at < _firstTile) _firstTile = at;
            if (at >= _firstTile + fit) _firstTile = at - fit + 1;
            _firstTile = Math.Clamp(_firstTile, 0, Math.Max(0, _shelf.Count - fit));
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

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != FormProperty && change.Property != MenuPanel.ScaleProperty) return;
            if (Bounds.Width > 0) _unitsWide = Big ? Bounds.Width / Unit(MenuPanel.GetScale(this)) : Bounds.Width;
            Fill();
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            double units = Big ? e.NewSize.Width / Unit(MenuPanel.GetScale(this)) : e.NewSize.Width;
            if (Math.Abs(units - _unitsWide) < 0.5) return;
            _unitsWide = units;
            Fill();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            e.Handled = e.Key switch
            {
                Key.Left => Move(-1, 0), Key.Right => Move(1, 0), Key.Up => Move(0, -1), Key.Down => Move(0, 1), Key.Enter => Accept(),
                _ => false,
            };
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Pane, () => _title);

        private static double Unit(double u) => double.IsFinite(u) && u > 0 ? u : 1;

        private LunaTypeScale Type => LunaTokens.Type(Big ? LunaForm.BigScreen : LunaForm.Desktop);

        private void Fill()
        {
            if (_unitsWide <= 0) _unitsWide = Big ? 1280 : 1080;
            KeepTileShown();
            if (_hintBar.Parent is Decorator d) d.Child = null;
            else if (_hintBar.Parent is Panel p) p.Children.Remove(_hintBar);
            _root.Children.Clear();
            if (Big && _night)
            {
                _root.Children.Add(_sky);
                Glow();
            }
            _root.Children.Add(Big ? ArrangeBig() : ArrangeDesktop());
            RefreshHints();
        }

        private static TextBlock Words(string text, double size, string ink, FontFamily? face = null, string? name = null)
        {
            var t = new TextBlock { Text = text, FontFamily = face ?? LunaFonts.Body, FontSize = size, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
            if (name is not null) t.Name = name;
            t[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ink);
            return t;
        }

        // The focused cover's, or Continue's, picture behind everything: the item's art at 40 per cent, blurred 40, under a gradient from the scrim to the surface.
        private IImage? BackdropArt() =>
            !_showsArt ? null
            : _row == HubRow.Shelf && _shelf.FirstOrDefault(t => t.Key == _tileKey) is { } tile ? ArtFor(tile)
            : _continue is not null ? _continue.Picture
            : _shelf.FirstOrDefault() is { } first ? ArtFor(first) : null;

        private IImage? ArtFor(CoverItem item) => item.Art ?? ArtOf?.Invoke(item) ?? PictureFor(item);

        private IImage? PictureFor(CoverItem item) => item.Picture ?? PictureOf?.Invoke(item);

        private Control Backdrop()
        {
            var back = new Panel { Name = "HubBackdrop", IsHitTestVisible = false };
            back[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            if (BackdropArt() is { } art)
            {
                back.Children.Add(new Image { Name = "HubArt", Source = art, Stretch = Stretch.UniformToFill, Opacity = 0.4, Effect = new BlurEffect { Radius = 40 } });
                var fade = new Border();
                fade.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Colour("LunaScrimColor", Color.FromArgb(0x8C, 0, 0, 0)), 0), new GradientStop(Colour("LunaSurfaceColor", Color.FromRgb(0x1B, 0x1B, 0x1E)), 1) },
                };
                back.Children.Add(fade);
            }
            return back;
        }

        // The glow in the focused cover's hue under the shelf, else in Continue's at the upper left (docs/LunaP.md §221.4).
        private void Glow()
        {
            _sky.GlowRadii = new Size(720, 450);
            _sky.GlowStrength = 0.22;
            double? tileHue = _row == HubRow.Shelf ? _shelf.FirstOrDefault(t => t.Key == _tileKey)?.Hue : null;
            Point centre = tileHue is not null ? new Point(510.0 / 1280, 0.625) : new Point(240.0 / 1280, 0.375);
            if (_sky.GlowHue is null) _sky.PlaceGlow(centre);
            else _sky.GlowCentre = centre;
            _sky.GlowHue = tileHue ?? _continue?.Hue ?? _shelf.FirstOrDefault(t => t.Key == _tileKey)?.Hue ?? _shelf.FirstOrDefault()?.Hue;
        }

        private Color Colour(string key, Color fallback) => this.TryFindResource(key, ActualThemeVariant, out object? found) && found is Color c ? c : fallback;
    }
}
