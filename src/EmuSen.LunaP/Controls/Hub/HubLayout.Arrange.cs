using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // Where each part of the hub goes in each form - see docs/LunaP.md §212.3 and §212.4.
    public partial class HubLayout
    {
        /// <summary>The name of a place's live line, which the design cuts with an ellipsis where it is long; a fit audit allows it.</summary>
        public const string StatusName = "HubPlaceStatus";

        /// <summary>The name of the desktop's lines under a cover, cut with an ellipsis at the cover's width as a desktop grid's are; a fit audit allows it.</summary>
        public const string CoverLineName = "HubCoverLine";

        /// <summary>The title's size where its two lines hold it, and the floor it shrinks to before it is cut.</summary>
        public const double TitleFloor = 32;

        private Control ArrangeBig()
        {
            var surface = new Panel { Width = _unitsWide, Height = DesignHeight, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            if (!_night) surface.Children.Add(Backdrop());
            TextBlock status = Words(_status, Type.Caption, "LunaMuted", name: "HubStatus");
            status.HorizontalAlignment = _night ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            status.VerticalAlignment = VerticalAlignment.Top;
            status.Margin = _night ? new Thickness(Side, Top, 0, 0) : new Thickness(0, Top, Side, 0);
            surface.Children.Add(status);
            if (_night && _mark is { Length: > 0 } mark) surface.Children.Add(Wordmark(mark));
            AddTop(surface);
            if (_shelf.Count > 0) AddShelf(surface);
            AddPlaces(surface);
            surface.Children.Add(HelpBand.Make(_hintBar, "HubHelpBar", _night));
            var scaled = new LayoutTransformControl { Child = surface, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            scaled.Bind(LayoutTransformControl.LayoutTransformProperty, this.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(Unit(u), Unit(u))));
            return scaled;
        }

        // Continue's picture and words, or with nothing left the focused cover's, which nothing focuses.
        private void AddTop(Panel surface)
        {
            CoverItem? preview = _continue is null ? _shelf.FirstOrDefault(t => t.Key == _tileKey) ?? _shelf.FirstOrDefault() : null;
            if (_continue is null && preview is null) return;
            var frame = new Panel { Name = "HubContinue", Width = ContinueWidth, Height = ContinueHeight, Margin = new Thickness(Side, ContinueTop, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var slot = new PosterSlot { Name = "HubPoster", Scrim = false, Poster = _continue?.Picture ?? (preview is null ? null : ArtFor(preview)), Video = _continue?.Video, IsHitTestVisible = false };
            var box = new Border { Child = slot, CornerRadius = new CornerRadius(8), ClipToBounds = true };
            bool focused = _row == HubRow.Continue && _continue is not null;
            if (_night && _continue is not null) surface.Children.Add(ContinuePanel(focused));
            if (focused) box.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 24, Color = Color.FromArgb(0x80, 0, 0, 0) });
            frame.Children.Add(box);
            frame.Children.Add(Ring(focused && !_night, 8));
            if (_continue is not null)
            {
                AutomationProperties.SetName(frame, _continue.Title);
                frame.PointerPressed += (_, e) => { FocusOn(HubRow.Continue); if (e.ClickCount == 2) ContinueChosen?.Invoke(); };
            }
            surface.Children.Add(frame);

            double left = Side + ContinueWidth + 24, width = Math.Max(0, _unitsWide - Side - left);
            if (_continue is not null)
            {
                TextBlock caption = Words(_continue.Caption.ToUpperInvariant(), Type.Caption, "LunaMuted", name: "HubCaption");
                At(caption, left, 112, width);
                surface.Children.Add(caption);
            }
            string title = _continue?.Title ?? preview!.Title;
            TextBlock name = Words(title, TitleSize(title, width), "LunaTextStrong", LunaFonts.Condensed, "HubTitle");
            name.FontWeight = FontWeight.Medium;
            name.TextWrapping = TextWrapping.Wrap;
            name.MaxLines = 2;
            // The lines under the caption stand apart by the text scale, as their sizes do (docs/LunaP.md §214).
            double scale = LunaTokens.TextScale, nameTop = 112 + 24 * scale;
            At(name, left, nameTop, width);
            surface.Children.Add(name);
            if ((_continue?.Facts ?? preview?.Facts) is { Length: > 0 } facts)
            {
                TextBlock line = Words(facts, Type.Body, "LunaMuted", name: "HubFacts");
                line.TextWrapping = TextWrapping.Wrap;
                line.MaxLines = 2;
                double indent = _night && _continue?.Hue is double hue ? FactsPlanet(surface, hue, left, nameTop + 116 * scale) : 0;
                At(line, left + indent, nameTop + 116 * scale, width - indent);
                surface.Children.Add(line);
            }
        }

        // Display on two lines where they hold the name, else shrunk a unit at a time to the floor, then cut.
        private double TitleSize(string title, double width)
        {
            for (double size = Type.Display; size > TitleFloor; size -= 2)
            {
                using var layout = new TextLayout(title, new Typeface(LunaFonts.Condensed, FontStyle.Normal, FontWeight.Medium), size, null, TextAlignment.Left, TextWrapping.Wrap, maxWidth: Math.Max(1, width));
                if (layout.TextLines.Count <= 2) return size;
            }
            return TitleFloor;
        }

        private double ShelfY => _night ? NightShelfTop : ShelfTop;

        private TextBlock RecentLabel() => Words(_recentTitle.ToUpperInvariant(), Type.Caption, "LunaMuted", name: "HubRecentTitle");

        // The label's top: its foot over the tallest cover focused, grown about its centre with the ring around it (docs/LunaP.md §212.12).
        private double LabelTop(TextBlock label)
        {
            LunaFocus focus = LunaTokens.FocusMarks;
            double rise = (TileHeight / 2 + focus.RingThickness + focus.RingOffset) * focus.TileScale - TileHeight / 2 + (_night ? NightLift : 0);
            label.Measure(Size.Infinity);
            return Math.Floor(ShelfY - rise - ShelfLabelGap - label.DesiredSize.Height);
        }

        private void AddShelf(Panel surface)
        {
            TextBlock label = RecentLabel();
            At(label, Side, LabelTop(label), 400);
            surface.Children.Add(label);
            var shelf = new Panel { Name = "HubShelf", Height = TileHeight, Margin = new Thickness(Side, ShelfY, Side, 0), VerticalAlignment = VerticalAlignment.Top };
            AutomationProperties.SetName(shelf, _recentTitle);
            int fit = ShelfFit(), i = 0;
            foreach (CoverItem item in _shelf.Skip(_firstTile).Take(fit))
            {
                var card = Card(item, TileWidth, TileHeight, true, _row == HubRow.Shelf && item.Key == _tileKey);
                card.Margin = new Thickness(i++ * TileStep, 0, 0, 0);
                shelf.Children.Add(card);
            }
            surface.Children.Add(shelf);
            if (_row == HubRow.Shelf && _shelf.FirstOrDefault(t => t.Key == _tileKey) is { } focused)
            {
                TextBlock under = Words(focused.Title, Type.Body, "LunaText", name: "HubShelfTitle");
                At(under, Side, ShelfY + TileHeight + (_night ? NightTitleGap : 6), _unitsWide - 2 * Side);
                surface.Children.Add(under);
            }
        }

        private CoverCard Card(CoverItem item, double width, double height, bool big, bool focused)
        {
            var card = new CoverCard { Big = big, Lift = big && _night ? NightLift : 0, CellWidth = width, CellHeight = height, Item = item.Picture is null && PictureOf?.Invoke(item) is { } picture ? item with { Picture = picture } : item, ShowsFocus = focused, Name = "HubTile." + item.Key, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            string key = item.Key;
            card.PointerPressed += (_, e) =>
            {
                FocusOn(HubRow.Shelf, key);
                if (e.ClickCount == 2 || big) TileChosen?.Invoke(key);
            };
            return card;
        }

        private void AddPlaces(Panel surface)
        {
            if (_places.Count == 0) return;
            double room = Math.Max(0, _unitsWide - 2 * Side), width = (room - (_places.Count - 1) * PlaceGap) / _places.Count;
            var row = new Panel { Name = "HubPlaces", Height = PlaceHeight, Margin = new Thickness(Side, PlacesTop, Side, 0), VerticalAlignment = VerticalAlignment.Top };
            for (int i = 0; i < _places.Count; i++)
            {
                Control place = Place(_places[i], width, PlaceHeight, true, _row == HubRow.Places && _places[i].Key == _placeKey);
                place.Margin = new Thickness(i * (width + PlaceGap), 0, 0, 0);
                row.Children.Add(place);
            }
            surface.Children.Add(row);
        }

        // A place: its name, and its live line under it cut with an ellipsis, after a warning sign where it needs the person, in the attention colour on the big screen only (docs/LunaP.md §215.4).
        private Control Place(HubPlace place, double width, double height, bool big, bool focused)
        {
            double radius = big ? 8 : 6;
            var words = new StackPanel { Spacing = big ? 0 : 2, VerticalAlignment = VerticalAlignment.Center };
            TextBlock label = Words(place.Label, big ? Type.Caption : Type.Strong, "LunaTextStrong");
            if (!big) label.FontWeight = FontWeight.Medium;
            words.Children.Add(label);
            if (place.Status is { Length: > 0 } status)
                words.Children.Add(Words(place.NeedsAttention ? "⚠ " + status : status, Type.Caption, big && place.NeedsAttention ? "LunaAttention" : "LunaMuted", name: StatusName));
            var card = new Border { Child = words, CornerRadius = new CornerRadius(radius), Padding = big ? new Thickness(12, 0) : new Thickness(16, 0) };
            var cell = new Panel { Name = "HubPlace." + place.Key, Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Cursor = new Cursor(StandardCursorType.Hand) };
            if (big && _night)
            {
                cell.Children.Add(Glass(focused ? 0.92 : 0.5, radius));
                if (!focused)
                {
                    var edge = new Border { CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Opacity = 0.35, IsHitTestVisible = false };
                    edge[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
                    cell.Children.Add(edge);
                }
            }
            else card[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            cell.Children.Add(card);
            cell.Children.Add(Ring(focused, radius, big));
            AutomationProperties.SetName(cell, place.Status is { Length: > 0 } s ? $"{place.Label}, {s}" : place.Label);
            string key = place.Key;
            cell.PointerPressed += (_, _) => { FocusOn(HubRow.Places, key); PlaceChosen?.Invoke(key); };
            return cell;
        }

        /// <summary>How far a focused cover rises at night, besides growing.</summary>
        public const double NightLift = 6;

        // The raised surface at an opacity, as glass over the sky.
        private static Border Glass(double opacity, double radius)
        {
            var glass = new Border { CornerRadius = new CornerRadius(radius), Opacity = opacity, IsHitTestVisible = false, Name = "HubGlass" };
            glass[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            return glass;
        }

        // Continue's glass panel, 12 units round its picture and words, with the ring inside its edge when focused.
        private Control ContinuePanel(bool focused)
        {
            var panel = new Panel { Name = "HubContinuePanel", IsHitTestVisible = false };
            panel.Children.Add(Glass(focused ? 0.55 : 0.3, 12));
            if (focused)
            {
                var ring = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(LunaTokens.FocusMarks.RingThickness), Name = "HubRing" };
                ring[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaAccent");
                panel.Children.Add(ring);
            }
            At(panel, Side - 12, ContinueTop - 12, _unitsWide - 2 * (Side - 12));
            // 12 round the picture, the foot shortened where the shelf's label would come within the mockup's gap of it.
            double foot = _shelf.Count > 0 ? LabelTop(RecentLabel()) - NightPanelGap : double.PositiveInfinity;
            panel.Height = Math.Min(ContinueHeight + 24, foot - (ContinueTop - 12));
            return panel;
        }

        // A small planet in Continue's hue before the facts; returns how far the facts move right.
        private double FactsPlanet(Panel surface, double hue, double left, double top)
        {
            var planet = new PlanetGlyph { Name = "HubContinuePlanet", Hue = hue, Width = 14, Height = 14 };
            planet.Halo(0, 0, 10, 0.6);
            At(planet, left, top + (Type.Body * 1.3 - 14) / 2, 14);
            surface.Children.Add(planet);
            return 24;
        }

        // A small crescent and the wordmark at the top right, as the night hub's mockup draws them.
        private Control Wordmark(string mark)
        {
            var lit = new EllipseGeometry(new Rect(0, 0, 26, 26));
            var moon = new Avalonia.Controls.Shapes.Path { Width = 26, Height = 26, Data = new CombinedGeometry(GeometryCombineMode.Exclude, lit, new EllipseGeometry(new Rect(7, -2, 26, 26))), Fill = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.Parse("#e9e4d4")) };
            var glow = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), BoxShadow = new BoxShadows(new BoxShadow { Blur = 18, Color = Color.FromArgb(89, 233, 228, 212) }), VerticalAlignment = VerticalAlignment.Center, Child = moon };
            TextBlock word = Words(mark, 24 * LunaTokens.TextScale, "LunaTextStrong", LunaFonts.Condensed, "HubMark");
            word.FontWeight = FontWeight.Light;
            word.LetterSpacing = 6;
            word.VerticalAlignment = VerticalAlignment.Center;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 36, Side, 0), Name = "HubWordmark" };
            row.Children.Add(glow);
            row.Children.Add(word);
            return row;
        }

        internal static Border Ring(bool shown, double radius, bool big = true)
        {
            LunaFocus focus = LunaTokens.FocusMarks;
            double thick = big ? focus.RingThickness : 2, offset = big ? focus.RingOffset : 1;
            var ring = new Border { IsHitTestVisible = false, IsVisible = shown, Margin = new Thickness(-(thick + offset)), BorderThickness = new Thickness(thick), CornerRadius = new CornerRadius(radius + thick + offset), Name = "HubRing" };
            ring[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaAccent");
            return ring;
        }

        private static void At(Control c, double x, double y, double width)
        {
            c.Margin = new Thickness(x, y, 0, 0);
            c.Width = Math.Max(0, width);
            c.HorizontalAlignment = HorizontalAlignment.Left;
            c.VerticalAlignment = VerticalAlignment.Top;
        }

        // The desktop's hub in the content area: the header, the Continue card with its button, the shelf with each cover's name and line, and the places four across.
        private Control ArrangeDesktop()
        {
            var page = new StackPanel { Margin = new Thickness(DesktopGutter, 0, DesktopGutter, DesktopGutter) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Height = 48 };
            TextBlock title = Words(_title.ToUpperInvariant(), 20, "LunaTextStrong", LunaFonts.Condensed, "HubTitle");
            title.VerticalAlignment = VerticalAlignment.Center;
            TextBlock status = Words(_status, Type.Caption, "LunaMuted", name: "HubStatus");
            status.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(title);
            header.Children.Add(status);
            page.Children.Add(header);
            if (_continue is { } c) page.Children.Add(DesktopContinue(c));
            if (_shelf.Count > 0)
            {
                page.Children.Add(Heading(_recentTitle, "HubRecentTitle"));
                var shelf = new StackPanel { Name = "HubShelf", Orientation = Orientation.Horizontal, Spacing = DesktopTileStep - DesktopTileWidth };
                AutomationProperties.SetName(shelf, _recentTitle);
                foreach (CoverItem item in _shelf.Skip(_firstTile).Take(ShelfFit()))
                {
                    var cell = new StackPanel { Width = DesktopTileWidth, Spacing = 2 };
                    cell.Children.Add(Card(item, DesktopTileWidth, DesktopTileHeight, false, _row == HubRow.Shelf && item.Key == _tileKey));
                    TextBlock name = Words(item.Title, Type.Body, "LunaText", name: CoverLineName);
                    name.TextAlignment = TextAlignment.Center;
                    name.Margin = new Thickness(0, 6, 0, 0);
                    cell.Children.Add(name);
                    if (item.Status is { Length: > 0 } line)
                    {
                        TextBlock under = Words(line, Type.Caption, "LunaMuted", name: CoverLineName);
                        under.TextAlignment = TextAlignment.Center;
                        cell.Children.Add(under);
                    }
                    shelf.Children.Add(cell);
                }
                page.Children.Add(shelf);
            }
            if (_places.Count > 0)
            {
                page.Children.Add(Heading(_placesTitle, "HubPlacesTitle"));
                int columns = Math.Min(4, _places.Count);
                var grid = new Grid { Name = "HubPlaces" };
                for (int col = 0; col < columns; col++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                for (int i = 0; i < _places.Count; i++)
                {
                    if (i % columns == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Control place = Place(_places[i], double.NaN, DesktopPlaceHeight, false, _row == HubRow.Places && _places[i].Key == _placeKey);
                    place.HorizontalAlignment = HorizontalAlignment.Stretch;
                    place.Margin = new Thickness(i % columns == 0 ? 0 : DesktopGap / 2, 0, i % columns == columns - 1 ? 0 : DesktopGap / 2, DesktopGap);
                    Grid.SetColumn(place, i % columns);
                    Grid.SetRow(place, i / columns);
                    grid.Children.Add(place);
                }
                page.Children.Add(grid);
            }
            var scroll = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            MenuLook.SetFadesEdges(scroll, true);
            var back = new Panel { Children = { scroll } };
            back[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            return back;
        }

        private TextBlock Heading(string text, string name)
        {
            TextBlock heading = Words(text.ToUpperInvariant(), Type.Caption, "LunaMuted", name: name);
            heading.Margin = new Thickness(0, 10, 0, 6);
            return heading;
        }

        private Control DesktopContinue(HubContinue c)
        {
            var slot = new PosterSlot { Name = "HubPoster", Scrim = false, Poster = c.Picture, Video = c.Video, Width = 240, IsHitTestVisible = false };
            var words = new StackPanel { Spacing = 6, Margin = new Thickness(24, 24, 24, 0) };
            words.Children.Add(Words(c.Caption.ToUpperInvariant(), Type.Caption, "LunaMuted", name: "HubCaption"));
            TextBlock name = Words(c.Title, Type.Display, "LunaTextStrong", LunaFonts.Condensed, "HubContinueTitle");
            name.FontWeight = FontWeight.Medium;
            words.Children.Add(name);
            if (c.Facts is { Length: > 0 } facts) words.Children.Add(Words(facts, Type.Body, "LunaMuted", name: "HubFacts"));
            var resume = new Button
            {
                Name = "HubResume", Content = c.Button, MinHeight = 28, MinWidth = 100, Padding = new Thickness(16, 2), Margin = new Thickness(0, 10, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center, FontFamily = LunaFonts.Body, FontSize = Type.Body,
            };
            resume[!Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty] = new DynamicResourceExtension("LunaAccent");
            resume[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("LunaOnAccent");
            resume.Click += (_, _) => ContinueChosen?.Invoke();
            var button = new Panel { HorizontalAlignment = HorizontalAlignment.Left, Children = { resume, Ring(_row == HubRow.Continue, 4, false) } };
            words.Children.Add(button);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*"), Height = DesktopContinueHeight };
            Grid.SetColumn(words, 1);
            grid.Children.Add(slot);
            grid.Children.Add(words);
            var card = new Border { Name = "HubContinue", Child = grid, CornerRadius = new CornerRadius(6), ClipToBounds = true };
            card[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            AutomationProperties.SetName(card, c.Title);
            return card;
        }
    }
}
