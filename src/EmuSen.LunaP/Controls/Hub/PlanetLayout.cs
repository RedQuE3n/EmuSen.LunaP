using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A world on a PlanetLayout: a wide tile along the top, or a planet in a row.</summary>
    /// <param name="Key">What the host switches on, never shown.</param>
    /// <param name="Name">The name on the tile or under the planet.</param>
    public sealed record PlanetItem(string Key, string Name)
    {
        /// <summary>The planet's hue in degrees; null for a wide tile.</summary>
        public double? Hue { get; init; }

        /// <summary>The line under the name, such as how many games.</summary>
        public string? Status { get; init; }

        /// <summary>The words under the rows while it is focused; null uses the name.</summary>
        public string? Title { get; init; }

        /// <summary>The line under the title while it is focused.</summary>
        public string? Facts { get; init; }

        /// <summary>The covers fanned at the right while it is focused, the first at the back; up to four are drawn.</summary>
        public IReadOnlyList<CoverItem> Covers { get; init; } = Array.Empty<CoverItem>();

        /// <summary>A wide tile's icon as SVG path data in a 24-unit box, stroked; null for none.</summary>
        public string? Icon { get; init; }
    }

    // Wide tiles over rows of planets on orbit lines, the focused world's words and covers beneath, on the night sky - see docs/LunaP.md §221.3.
    /// <summary>A big screen's chooser of worlds declared as data: wide tiles along the top, planets in rows that fit on faint orbit lines, and the focused one's title, facts and a fan of its covers beneath.</summary>
    public class PlanetLayout : Border
    {
        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<PlanetLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>The design height; the layout is drawn in units of it and scaled by MenuPanel.Scale.</summary>
        public const double DesignHeight = 800;

        /// <summary>The safe area's side, the header's top and height.</summary>
        public const double Side = 64, HeaderTop = 40, HeaderHeight = 44;

        /// <summary>The wide tiles' top, height and the gap between two.</summary>
        public const double TilesTop = 100, TileHeight = 72, TileGap = 18;

        /// <summary>The first planet row's top, the step to the next, a cell's side, the gap between cells and the planet's area at a cell's top.</summary>
        public const double RowTop = 196, RowStep = 192, Cell = 176, CellGap = 18, PlanetArea = 120;

        /// <summary>A planet's diameter at rest and focused, and how far a focused cell rises.</summary>
        public const double Planet = 58, FocusedPlanet = 84, Lift = 6;

        /// <summary>The focused world's words and fan: their top and height, and the fan's width and a cover's height.</summary>
        public const double DetailTop = 586, DetailHeight = 104, FanWidth = 420, FanCover = 100, FanStep = 74;

        /// <summary>How many planet rows show at once.</summary>
        public const int RowsShown = 2;

        private static readonly IBrush OrbitInk = new ImmutableSolidColorBrush(Color.FromArgb(71, 140, 150, 220));

        private readonly Panel _root = new();
        private readonly HintBar _hintBar = new();
        private readonly LunaSky _sky = new() { Name = "PlanetSky" };
        private IReadOnlyList<PlanetItem> _tiles = Array.Empty<PlanetItem>(), _planets = Array.Empty<PlanetItem>();
        private string? _focus;
        private int _firstRow;
        private double _unitsWide = 1280;
        private double? _lastHue;
        private bool _night = true;
        private string _title = string.Empty, _subtitle = string.Empty, _count = string.Empty;

        /// <summary>An empty layout at night.</summary>
        public PlanetLayout()
        {
            Focusable = true;
            ClipToBounds = true;
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            _sky.GlowRadii = new Size(756, 450);
            _sky.GlowStrength = 0.2;
            _sky.PlaceGlow(new Point(0.5, 615.0 / 800));
            Child = _root;
            Fill();
        }

        /// <summary>The pad whose printing the help bar draws.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        /// <summary>Whether the layout stands on the night sky; false draws the theme's surface, as the light variant does.</summary>
        public bool Night { get => _night; set { _night = value; Fill(); } }

        /// <summary>The sky behind the layout while Night is on.</summary>
        public LunaSky Sky => _sky;

        /// <summary>The header's first word, drawn in upper case, such as Games.</summary>
        public string Title { get => _title; set { _title = value ?? string.Empty; Fill(); } }

        /// <summary>The words after the title, such as Choose a console.</summary>
        public string Subtitle { get => _subtitle; set { _subtitle = value ?? string.Empty; Fill(); } }

        /// <summary>The line at the header's right.</summary>
        public string Count { get => _count; set { _count = value ?? string.Empty; Fill(); } }

        /// <summary>The wide tiles, in order.</summary>
        public IReadOnlyList<PlanetItem> Tiles => _tiles;

        /// <summary>The planets, in order.</summary>
        public IReadOnlyList<PlanetItem> Planets => _planets;

        /// <summary>The focused item's key, or null when there is none.</summary>
        public string? Focused => _focus;

        /// <summary>Gives the help bar's entries for the focused key, generated from what the host declares.</summary>
        public Func<string?, IReadOnlyList<PageHint>>? HintsFor { get; set; }

        /// <summary>Gives a cover's picture when it is drawn; null uses the item's own.</summary>
        public Func<CoverItem, IImage?>? PictureOf { get; set; }

        /// <summary>The help bar's entries as drawn now.</summary>
        public IReadOnlyList<HintEntry> Hints => _hintBar.Entries ?? Array.Empty<HintEntry>();

        /// <summary>Raised when an item is chosen with A, a click or Enter, with its key.</summary>
        public event Action<string>? ItemChosen;

        /// <summary>Raised when a pad button the hints give a key to is pressed, with that key.</summary>
        public event Action<string>? HintInvoked;

        /// <summary>Raised when the focus moves, with the new key.</summary>
        public event Action<string>? FocusChanged;

        /// <summary>Draws the layout again from what it holds.</summary>
        public void Refresh() => Fill();

        /// <summary>The number of cells across at the present width.</summary>
        public int Columns => Math.Max(1, (int)Math.Floor((_unitsWide - 2 * Side + CellGap) / (Cell + CellGap)));

        /// <summary>The planets of each row, top to bottom: shared evenly among the rows the columns need.</summary>
        public IReadOnlyList<IReadOnlyList<PlanetItem>> Rows
        {
            get
            {
                int n = _planets.Count;
                if (n == 0) return Array.Empty<IReadOnlyList<PlanetItem>>();
                int rows = (n + Columns - 1) / Columns, per = (n + rows - 1) / rows;
                return Enumerable.Range(0, rows).Select(r => (IReadOnlyList<PlanetItem>)_planets.Skip(r * per).Take(per).ToList()).Where(r => r.Count > 0).ToList();
            }
        }

        /// <summary>Gives the layout its wide tiles, keeping the focus where it still is.</summary>
        /// <param name="tiles">The tiles in order.</param>
        public void SetTiles(IEnumerable<PlanetItem> tiles)
        {
            _tiles = tiles?.ToList() ?? throw new ArgumentNullException(nameof(tiles));
            Distinct();
            Fill();
        }

        /// <summary>Gives the layout its planets, keeping the focus where it still is.</summary>
        /// <param name="planets">The planets in order.</param>
        public void SetPlanets(IEnumerable<PlanetItem> planets)
        {
            _planets = planets?.ToList() ?? throw new ArgumentNullException(nameof(planets));
            Distinct();
            Fill();
        }

        private void Distinct()
        {
            List<string> keys = _tiles.Concat(_planets).Select(i => i.Key).ToList();
            if (keys.Distinct().Count() != keys.Count) throw new ArgumentException("Two items share a key.");
            if (_focus is null || !keys.Contains(_focus)) _focus = keys.FirstOrDefault();
        }

        /// <summary>Puts the focus on an item.</summary>
        /// <param name="key">The item's key.</param>
        /// <returns>Whether there is such an item.</returns>
        public bool FocusOn(string key)
        {
            if (!_tiles.Concat(_planets).Any(i => i.Key == key)) return false;
            _focus = key;
            Refocused();
            return true;
        }

        // The focused item's row (-1 for the tiles) and place in it.
        private (int Row, int At) Where()
        {
            int t = _tiles.ToList().FindIndex(i => i.Key == _focus);
            if (t >= 0) return (-1, t);
            IReadOnlyList<IReadOnlyList<PlanetItem>> rows = Rows;
            for (int r = 0; r < rows.Count; r++)
            {
                int at = rows[r].ToList().FindIndex(i => i.Key == _focus);
                if (at >= 0) return (r, at);
            }
            return (_tiles.Count > 0 ? -1 : 0, 0);
        }

        private IReadOnlyList<PlanetItem> Row(int row) => row < 0 ? _tiles : Rows is var rows && row < rows.Count ? rows[row] : Array.Empty<PlanetItem>();

        // An item's centre across, in units.
        private double CentreOf(int row, int at)
        {
            if (row < 0)
            {
                double w = TileWidth();
                return Side + at * (w + TileGap) + w / 2;
            }
            int k = Row(row).Count;
            return RowLeft(k) + at * (Cell + CellGap) + Cell / 2;
        }

        private double TileWidth() => _tiles.Count == 0 ? 0 : (_unitsWide - 2 * Side - (_tiles.Count - 1) * TileGap) / _tiles.Count;

        private double RowLeft(int count) => (_unitsWide - (count * Cell + Math.Max(0, count - 1) * CellGap)) / 2;

        /// <summary>Moves the focus: Left and Right along a row, Up and Down to the nearest item of the next row.</summary>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        /// <returns>Whether the focus moved.</returns>
        public bool Move(int dx, int dy)
        {
            if (_focus is null) return false;
            (int row, int at) = Where();
            if (dx != 0)
            {
                IReadOnlyList<PlanetItem> items = Row(row);
                int to = at + dx;
                if (to < 0 || to >= items.Count) return false;
                _focus = items[to].Key;
            }
            else if (dy != 0)
            {
                int next = row + dy, top = _tiles.Count > 0 ? -1 : 0;
                if (next < top || next >= Rows.Count || Row(next).Count == 0) return false;
                double x = CentreOf(row, at);
                IReadOnlyList<PlanetItem> items = Row(next);
                int best = Enumerable.Range(0, items.Count).OrderBy(i => Math.Abs(CentreOf(next, i) - x)).First();
                _focus = items[best].Key;
            }
            else return false;
            Refocused();
            return true;
        }

        /// <summary>A pad button: A chooses the focused item, and a button the hints give a key raises HintInvoked.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the layout used it.</returns>
        public bool Press(PadGlyphButton button)
        {
            if (button == PadGlyphButton.South && _focus is not null)
            {
                ItemChosen?.Invoke(_focus);
                return true;
            }
            if (CurrentHints().FirstOrDefault(h => h.Button == button && h.Key is not null) is { } hint)
            {
                HintInvoked?.Invoke(hint.Key!);
                return true;
            }
            return false;
        }

        private IReadOnlyList<PageHint> CurrentHints() => HintsFor?.Invoke(_focus) ?? Array.Empty<PageHint>();

        private void Refocused()
        {
            Fill();
            if (_focus is not null) FocusChanged?.Invoke(_focus);
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
            if (change.Property != MenuPanel.ScaleProperty) return;
            if (Bounds.Width > 0) _unitsWide = Bounds.Width / Unit(MenuPanel.GetScale(this));
            Fill();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            e.Handled = e.Key switch
            {
                Key.Left => Move(-1, 0), Key.Right => Move(1, 0), Key.Up => Move(0, -1), Key.Down => Move(0, 1), Key.Enter => Press(PadGlyphButton.South),
                _ => false,
            };
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Pane, () => _title);

        private static double Unit(double u) => double.IsFinite(u) && u > 0 ? u : 1;

        private static double Scaled(double size) => size * LunaTokens.TextScale;

        // The window of rows moves only when the focus leaves it, a whole row at a time.
        private void KeepRowShown()
        {
            int rows = Rows.Count, row = Where().Row;
            if (row >= 0 && row < _firstRow) _firstRow = row;
            if (row >= _firstRow + RowsShown) _firstRow = row - RowsShown + 1;
            _firstRow = Math.Clamp(_firstRow, 0, Math.Max(0, rows - RowsShown));
        }

        private void Fill()
        {
            KeepRowShown();
            if (_hintBar.Parent is Decorator d) d.Child = null;
            else if (_hintBar.Parent is Panel p) p.Children.Remove(_hintBar);
            var surface = new Panel { Width = _unitsWide, Height = DesignHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            AddHeader(surface);
            AddTiles(surface);
            AddPlanets(surface);
            AddDetail(surface);
            surface.Children.Add(HelpBand.Make(_hintBar, "PlanetHelpBar", _night));
            var scaled = new LayoutTransformControl { Child = surface, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            scaled.Bind(LayoutTransformControl.LayoutTransformProperty, this.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(Unit(u), Unit(u))));
            _root.Children.Clear();
            if (_night)
            {
                _root.Background = null;
                _root.Children.Add(_sky);
                PlanetItem? focused = _planets.FirstOrDefault(i => i.Key == _focus);
                _lastHue = focused?.Hue ?? _lastHue ?? _planets.FirstOrDefault(i => i.Hue is not null)?.Hue;
                _sky.GlowHue = _lastHue;
            }
            else _root[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            _root.Children.Add(scaled);
            RefreshHints();
        }

        private void RefreshHints()
        {
            var entries = CurrentHints().Select(h => new HintEntry(h.Text) { Button = h.Button }).ToList();
            _hintBar.Entries = entries;
            AutomationProperties.SetName(_hintBar, string.Join(", ", entries.Select(e => e.Label)));
        }

        private static TextBlock Words(string text, double size, string ink, FontFamily? face = null, string? name = null, FontWeight weight = FontWeight.Normal, double spacing = 0)
        {
            var t = new TextBlock { Text = text, FontFamily = face ?? LunaFonts.Body, FontSize = size, FontWeight = weight, LetterSpacing = spacing, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
            if (name is not null) t.Name = name;
            t[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ink);
            return t;
        }

        private static void At(Control c, double x, double y, double width = double.NaN)
        {
            c.Margin = new Thickness(x, y, 0, 0);
            if (!double.IsNaN(width)) c.Width = Math.Max(0, width);
            c.HorizontalAlignment = HorizontalAlignment.Left;
            c.VerticalAlignment = VerticalAlignment.Top;
        }

        private void AddHeader(Panel surface)
        {
            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Name = "PlanetHeader" };
            TextBlock title = Words(_title.ToUpperInvariant(), Scaled(32), "LunaTextStrong", LunaFonts.Condensed, "PlanetTitle", FontWeight.Medium, 3);
            TextBlock sub = Words(_subtitle, Scaled(20), "LunaMuted", name: "PlanetSubtitle");
            title.VerticalAlignment = sub.VerticalAlignment = VerticalAlignment.Bottom;
            sub.Margin = new Thickness(0, 0, 0, 5);
            left.Children.Add(title);
            left.Children.Add(sub);
            TextBlock count = Words(_count, Scaled(20), "LunaMuted", name: "PlanetCount");
            count.VerticalAlignment = VerticalAlignment.Bottom;
            count.Margin = new Thickness(24, 0, 0, 5);
            var row = new DockPanel { Height = HeaderHeight, LastChildFill = true };
            DockPanel.SetDock(count, Dock.Right);
            row.Children.Add(count);
            row.Children.Add(left);
            At(row, Side, HeaderTop, _unitsWide - 2 * Side);
            surface.Children.Add(row);
        }

        // A cell's glass: the raised surface at an opacity, beneath the cell's words.
        private Border Glass(double opacity, double radius)
        {
            var glass = new Border { CornerRadius = new CornerRadius(radius), Opacity = _night ? opacity : opacity > 0 ? 1 : 0, IsHitTestVisible = false, Name = "PlanetGlass" };
            glass[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            return glass;
        }

        private void AddTiles(Panel surface)
        {
            if (_tiles.Count == 0) return;
            double w = TileWidth();
            for (int i = 0; i < _tiles.Count; i++)
            {
                PlanetItem tile = _tiles[i];
                bool on = tile.Key == _focus;
                var cell = new Panel { Name = "PlanetTile." + tile.Key, Width = w, Height = TileHeight, Cursor = new Cursor(StandardCursorType.Hand) };
                cell.Children.Add(Glass(on ? 0.92 : 0.55, 8));
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Margin = new Thickness(20, 0), VerticalAlignment = VerticalAlignment.Center };
                var shade = new Border { CornerRadius = new CornerRadius(20), Opacity = 0.06, IsHitTestVisible = false };
                shade[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaTextStrong");
                var disc = new Panel { Width = 40, Height = 40, Children = { shade } };
                if (tile.Icon is { Length: > 0 } icon)
                {
                    var path = new Path { Data = Geometry.Parse(icon), StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Stretch = Stretch.None, Width = 24, Height = 24 };
                    path[!Shape.StrokeProperty] = new DynamicResourceExtension(on ? "LunaTextStrong" : "LunaMuted");
                    disc.Children.Add(new Viewbox { Width = 22, Height = 22, Child = path });
                }
                row.Children.Add(disc);
                var words = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Max(0, w - 96) };
                words.Children.Add(Words(tile.Name.ToUpperInvariant(), Scaled(24), "LunaTextStrong", LunaFonts.Condensed, null, FontWeight.Medium, 1.5));
                if (tile.Status is { Length: > 0 } status) words.Children.Add(Words(status, Scaled(18), "LunaMuted", name: StatusName));
                row.Children.Add(words);
                cell.Children.Add(row);
                cell.Children.Add(HubLayout.Ring(on, 8));
                AutomationProperties.SetName(cell, tile.Status is { Length: > 0 } s ? $"{tile.Name}, {s}" : tile.Name);
                string key = tile.Key;
                cell.PointerPressed += (_, _) => { if (key == _focus) ItemChosen?.Invoke(key); else FocusOn(key); };
                At(cell, Side + i * (w + TileGap), TilesTop);
                surface.Children.Add(cell);
            }
        }

        /// <summary>The name of a tile's or planet's words, cut with an ellipsis at the cell's width; a fit audit allows it.</summary>
        public const string StatusName = "PlanetStatus";

        private void AddPlanets(Panel surface)
        {
            IReadOnlyList<IReadOnlyList<PlanetItem>> rows = Rows;
            for (int shown = 0; shown < RowsShown && _firstRow + shown < rows.Count; shown++)
            {
                IReadOnlyList<PlanetItem> row = rows[_firstRow + shown];
                double top = RowTop + shown * RowStep, line = top + PlanetArea / 2;
                surface.Children.Add(Orbit(line, shown));
                double left = RowLeft(row.Count);
                for (int i = 0; i < row.Count; i++)
                {
                    Control cell = PlanetCell(row[i]);
                    At(cell, left + i * (Cell + CellGap), top);
                    surface.Children.Add(cell);
                }
            }
        }

        // A faint line through a row's planets, fading out at the screen's edges.
        private Control Orbit(double y, int shown)
        {
            double fade = shown == 0 ? 0.15 : 0.2;
            var ink = new ImmutableLinearGradientBrush(
                [new ImmutableGradientStop(0, Color.FromArgb(0, 140, 150, 220)), new ImmutableGradientStop(fade, Color.FromArgb(71, 140, 150, 220)), new ImmutableGradientStop(1 - fade, Color.FromArgb(71, 140, 150, 220)), new ImmutableGradientStop(1, Color.FromArgb(0, 140, 150, 220))],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0, RelativeUnit.Relative));
            var orbit = new Border { Name = "PlanetOrbit", Height = 1, Background = _night ? ink : null, IsHitTestVisible = false };
            if (!_night) orbit[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaBorder");
            At(orbit, 0, y, _unitsWide);
            return orbit;
        }

        private Control PlanetCell(PlanetItem item)
        {
            bool on = item.Key == _focus;
            var cell = new Panel { Name = "PlanetCell." + item.Key, Width = Cell, Height = Cell, Cursor = new Cursor(StandardCursorType.Hand) };
            cell.Children.Add(Glass(on ? 0.88 : 0, 8));
            var stack = new StackPanel { Spacing = 2, Margin = new Thickness(8, 0, 8, 14) };
            double d = on ? FocusedPlanet : Planet;
            var planet = new PlanetGlyph { Name = "Planet." + item.Key, Hue = item.Hue ?? 0, Width = d, Height = d, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            planet.Halo(on ? 14 : 9, on ? 0.22 : 0.12, on ? 60 : 24, on ? 0.55 : 0.25);
            stack.Children.Add(new Panel { Height = PlanetArea, Children = { planet } });
            TextBlock name = Words(item.Name.ToUpperInvariant(), Scaled(22), "LunaTextStrong", LunaFonts.Condensed, StatusName, FontWeight.Medium, 1.2);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            stack.Children.Add(name);
            if (item.Status is { Length: > 0 } status)
            {
                TextBlock line = Words(status, Scaled(17), "LunaMuted", name: StatusName);
                line.HorizontalAlignment = HorizontalAlignment.Center;
                stack.Children.Add(line);
            }
            cell.Children.Add(stack);
            cell.Children.Add(HubLayout.Ring(on, 8));
            if (on)
            {
                cell.RenderTransform = new TranslateTransform(0, -Lift);
                ((Border)cell.Children[0]).BoxShadow = _night ? new BoxShadows(new BoxShadow { OffsetY = 14, Blur = 40, Color = Color.FromArgb(115, 0, 0, 0) }) : default;
            }
            AutomationProperties.SetName(cell, item.Status is { Length: > 0 } s ? $"{item.Name}, {s}" : item.Name);
            string key = item.Key;
            cell.PointerPressed += (_, _) => { if (key == _focus) ItemChosen?.Invoke(key); else FocusOn(key); };
            return cell;
        }

        private void AddDetail(Panel surface)
        {
            if (_tiles.Concat(_planets).FirstOrDefault(i => i.Key == _focus) is not { } item) return;
            var words = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) };
            TextBlock title = Words((item.Title ?? item.Name).ToUpperInvariant(), Scaled(44), "LunaTextStrong", LunaFonts.Condensed, "PlanetFocusTitle", FontWeight.Medium, 2);
            title.LineHeight = Scaled(44);
            words.Children.Add(title);
            if (item.Facts is { Length: > 0 } facts) words.Children.Add(Words(facts, Scaled(22), "LunaMuted", name: "PlanetFocusFacts"));
            double fanLeft = _unitsWide - Side - FanWidth;
            words.MaxWidth = Math.Max(0, fanLeft - Side - 16);
            var box = new Panel { Height = DetailHeight };
            box.Children.Add(words);
            At(box, Side, DetailTop, _unitsWide - 2 * Side);
            surface.Children.Add(box);
            var fan = new Panel { Name = "PlanetFan", Width = FanWidth, Height = DetailHeight, ClipToBounds = false };
            List<CoverItem> covers = item.Covers.Take(4).ToList();
            for (int k = 0; k < covers.Count; k++)
            {
                int i = 4 - covers.Count + k;
                CoverItem cover = covers[k].Picture is null && PictureOf?.Invoke(covers[k]) is { } picture ? covers[k] with { Picture = picture } : covers[k];
                double aspect = CoverCard.ShapeOf(cover.Picture, cover.Aspect), w = Math.Min(150, FanCover * aspect), h = w / aspect;
                var card = new CoverCard { Big = true, CellWidth = w, CellHeight = h, Item = cover, Name = "PlanetCover." + cover.Key, ZIndex = i + 1, IsHitTestVisible = false };
                card.RenderTransformOrigin = RelativePoint.Center;
                card.RenderTransform = new RotateTransform((i - 1.5) * 4);
                At(card, FanWidth - (3 - i) * FanStep - w, DetailHeight - h);
                fan.Children.Add(card);
            }
            At(fan, fanLeft, DetailTop);
            surface.Children.Add(fan);
        }
    }
}
