using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // Where each part of the page goes in each form, and how the content is drawn - see docs/LunaP.md §209.2.
    public partial class PageLayout
    {
        private readonly Panel _root = new();
        private readonly List<Control> _rowControls = new();
        private readonly List<(string Key, Control Ring, Control? Buttons, TextBlock Label)> _tileControls = new();
        private bool _filling;

        private LunaTypeScale Type => LunaTokens.Type(Big ? LunaForm.BigScreen : LunaForm.Desktop);

        // The help band as the frame's: the bar at 24 in Barlow, the glyphs strong, on its own band with a rule above, inset to the safe area.
        private static void StyleBand(Border band, HintBar bar, double side)
        {
            StyleBar(bar);
            band[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            band.BorderThickness = new Thickness(0, 1, 0, 0);
            band.Padding = new Thickness(side, 10, side, 6);
            band.Child = bar;
        }

        internal static void StyleBar(HintBar bar)
        {
            bar.FontPath = LunaFonts.BodyFile;
            bar.FontSize = 24;
            bar.Bind(HintBar.IconColorProperty, bar.GetResourceObservable("LunaTextStrongColor", c => c is Color colour ? colour : bar.IconColor));
            bar.Bind(HintBar.TextColorProperty, bar.GetResourceObservable("LunaTextColor", c => c is Color colour ? colour : bar.TextColor));
        }

        private void Arrange()
        {
            foreach (Control c in new Control[] { _poster, _cover, _titleFit, _factsFit, _actionRow, _strip, _tabs, _contentHost, _hintBand, _back, _viewer }) Detach(c);
            _root.Children.Clear();
            _titleFit.Child = _title;
            _factsFit.Child = _facts;
            Control arranged = Big ? ArrangeBig() : ArrangeDesktop();
            _root.Children.Add(arranged);
            Child = _root;
            FillHero();
            FillActions();
            FillStrip();
            FillContent();
            RefreshHints();
        }

        private Control ArrangeBig()
        {
            var surface = new Panel();
            surface[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            _poster.Height = HeroHeight;
            _poster.VerticalAlignment = VerticalAlignment.Top;
            _cover.Margin = new Thickness(Side, 180, 0, 0);
            _cover.HorizontalAlignment = HorizontalAlignment.Left;
            _cover.VerticalAlignment = VerticalAlignment.Top;
            _cover.CornerRadius = new CornerRadius(8);
            _cover.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 8, Blur = 24, Color = Color.FromArgb(0x80, 0, 0, 0) });
            Place(_titleFit, TitleTop, 60);
            Place(_factsFit, FactsTop, 32);
            Place(_actionRow, ActionsTop, 44);
            Place(_strip, StripTop, 44);
            _actionRow.Spacing = 12;
            _contentHost.Margin = new Thickness(Side, ContentTop, Side, DesignHeight - ContentTop - ContentHeight);
            _contentHost.VerticalAlignment = VerticalAlignment.Stretch;
            StyleBand(_hintBand, _hintBar, Side);
            _hintBand.Height = HelpBarHeight;
            _hintBand.VerticalAlignment = VerticalAlignment.Top;
            _hintBand.Margin = new Thickness(0, HelpBarTop, 0, 0);
            _hintBand[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            _title.FontFamily = LunaFonts.Condensed;
            _title.FontWeight = FontWeight.Medium;
            _title.FontSize = Type.Display;
            _facts.FontFamily = LunaFonts.Body;
            _facts.FontSize = Type.Body;
            _facts.Opacity = 0.7;
            BuildViewer();
            foreach (Control c in new Control[] { _poster, _cover, _titleFit, _factsFit, _actionRow, _strip, _contentHost, _hintBand, _viewer }) surface.Children.Add(c);
            var scaled = new LayoutTransformControl { Child = surface };
            scaled.Bind(LayoutTransformControl.LayoutTransformProperty, this.GetObservable(MenuPanel.ScaleProperty, u => (ITransform)new ScaleTransform(Unit(u), Unit(u))));
            return scaled;
        }

        // The content's room on the big screen, from its top to the help bar.
        private const double ContentHeight = HelpBarTop - ContentTop;

        private static double Unit(double u) => double.IsFinite(u) && u > 0 ? u : 1;

        private static void Place(Control c, double top, double height)
        {
            c.Margin = new Thickness(TitleLeft, top, Side, 0);
            c.Height = height;
            c.VerticalAlignment = VerticalAlignment.Top;
            c.HorizontalAlignment = c is Viewbox ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        }

        private Control ArrangeDesktop()
        {
            _poster.Height = DesktopHeroHeight;
            _poster.VerticalAlignment = VerticalAlignment.Top;
            _back.Margin = new Thickness(24, 16, 0, 0);
            _back.Width = 80;
            _back.Height = 28;
            _back.HorizontalAlignment = HorizontalAlignment.Left;
            _back.VerticalAlignment = VerticalAlignment.Top;
            _cover.Margin = new Thickness(24, 93, 0, 0);
            _cover.HorizontalAlignment = HorizontalAlignment.Left;
            _cover.VerticalAlignment = VerticalAlignment.Top;
            _cover.CornerRadius = new CornerRadius(6);
            _cover.BoxShadow = default;
            foreach ((Control c, double y, double height) in new (Control, double, double)[] { (_titleFit, 104, 34), (_factsFit, 144, 20), (_actionRow, 184, 28) })
            {
                c.Margin = new Thickness(208, y, 24, 0);
                c.Height = height;
                c.VerticalAlignment = VerticalAlignment.Top;
                c.HorizontalAlignment = c is Viewbox ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            }
            _actionRow.Spacing = 16;
            _title.FontFamily = LunaFonts.Condensed;
            _title.FontWeight = FontWeight.Medium;
            _title.FontSize = Type.Display;
            _facts.FontFamily = LunaFonts.Body;
            _facts.FontSize = Type.Body;
            _facts.Opacity = 1;
            var top = new Panel { Height = 300 };
            foreach (Control c in new Control[] { _poster, _back, _cover, _titleFit, _factsFit, _actionRow }) top.Children.Add(c);
            var tabs = new Border { Child = _tabs, Margin = new Thickness(24, 0, 24, 0), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(12, 0) };
            tabs[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaRule");
            AdaptiveLayout.StyleStrip(_tabs);
            var scroll = new ScrollViewer { Content = _contentHost, Margin = new Thickness(24, 18, 24, 12), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            _contentHost.Margin = default;
            MenuLook.SetFadesEdges(scroll, true);
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
            grid[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            Grid.SetRow(top, 0);
            Grid.SetRow(tabs, 1);
            Grid.SetRow(scroll, 2);
            grid.Children.Add(top);
            grid.Children.Add(tabs);
            grid.Children.Add(scroll);
            BuildViewer();
            return new Panel { Children = { grid, _viewer } };
        }

        private void FillHero()
        {
            PageHero? h = _hero;
            _poster.Poster = h?.Art;
            _title.Text = h?.Title ?? string.Empty;
            _title.Bind(TextBlock.ForegroundProperty, _title.GetResourceObservable("LunaOnArt"));
            _facts.Text = h?.Facts ?? string.Empty;
            _facts.IsVisible = h?.Facts is { Length: > 0 };
            _facts.Bind(TextBlock.ForegroundProperty, _facts.GetResourceObservable("LunaOnArt"));
            if (h is null) { _cover.Child = null; _cover.IsVisible = false; return; }
            _cover.IsVisible = true;
            double aspect = double.IsFinite(h.CoverAspect) && h.CoverAspect > 0 ? h.CoverAspect : 0.75;
            (double w, double ht) = Big
                ? (Math.Min(CoverWidth, CoverMaxHeight * aspect), Math.Min(CoverMaxHeight, CoverWidth / aspect))
                : (Math.Min(DesktopCoverWidth, DesktopCoverHeight * aspect), Math.Min(DesktopCoverHeight, DesktopCoverWidth / aspect));
            _cover.Width = w;
            _cover.Height = ht;
            _cover.Child = Card(h.Cover, h.Title, h.Mark, Big ? 28 : 14);
            _cover[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            AutomationProperties.SetName(_cover, $"{h.Title}, cover");
        }

        // A picture filling its card, or a generated card: the mark at the top left and the title in the middle, in Barlow Condensed.
        private Control Card(IImage? picture, string title, string? mark, double titleSize)
        {
            var panel = new Panel();
            if (picture is not null)
            {
                panel.Children.Add(new Image { Source = picture, Stretch = Stretch.UniformToFill, IsHitTestVisible = false });
                return panel;
            }
            if (mark is { Length: > 0 })
            {
                var m = new TextBlock { Text = mark, FontFamily = LunaFonts.Body, FontSize = Big ? 17 : 11, Margin = Big ? new Thickness(8, 6) : new Thickness(6, 4), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                m.Bind(TextBlock.ForegroundProperty, m.GetResourceObservable("LunaMuted"));
                panel.Children.Add(m);
            }
            var t = new TextBlock { Text = title, FontFamily = LunaFonts.Condensed, FontSize = titleSize, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
            t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable("LunaTextStrong"));
            panel.Children.Add(new Viewbox { Child = t, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(4, Big ? 24 : 16, 4, 4) });
            return panel;
        }

        private void FillActions()
        {
            _actionRow.Children.Clear();
            for (int i = 0; i < _actions.Count; i++) _actionRow.Children.Add(Big ? BigAction(_actions[i], i) : DesktopAction(_actions[i]));
            FillActionsFocus();
        }

        // A button 44 tall: its pad glyph and words, a stepped value between arrows, filled with the accent for the main action; the focus is a ring 3 units wide drawn 4 outside.
        private Control BigAction(PageAction a, int index)
        {
            var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            string ink = a.IsMain ? "LunaOnAccentColor" : "LunaTextColor";
            if (a.PadButton is { } pad)
            {
                var glyph = new PadGlyph { Button = pad, GlyphSize = 30, VerticalAlignment = VerticalAlignment.Center };
                glyph[!PadGlyph.FamilyProperty] = this[!PadFamilyProperty];
                glyph.Bind(PadGlyph.ColorProperty, glyph.GetResourceObservable(ink, c => c is Color colour ? colour : Colors.White));
                words.Children.Add(glyph);
            }
            if (Mark(a.Mark, ink, 22) is { } mark) words.Children.Add(mark);
            words.Children.Add(Word(a.Text, ink));
            if (a.Value is { } value)
            {
                words.Children.Add(Arrow(ink, left: true));
                words.Children.Add(Word(value, ink));
                words.Children.Add(Arrow(ink, left: false));
            }
            var button = new Border { Height = 44, CornerRadius = new CornerRadius(4), Padding = new Thickness(a.PadButton is null ? 20 : 8, 0, 20, 0), Child = words, Name = "PageAction." + a.Key, Opacity = a.IsEnabled ? 1 : 0.5 };
            if (a.IsMain) button[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaAccent");
            else
            {
                button.BorderThickness = new Thickness(1);
                button[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            }
            var ring = new Border { Margin = new Thickness(-7), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(11), IsVisible = false, IsHitTestVisible = false };
            ring[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaAccent");
            var cell = new Panel { Children = { button, ring } };
            AutomationProperties.SetName(cell, a.Value is null ? a.Text : $"{a.Text}, {a.Value}");
            cell.PointerPressed += (_, _) => { _actionAt = index; _inActions = true; Refocused(); if (a.IsEnabled) ActionInvoked?.Invoke(a.Key); };
            return cell;
        }

        // The value's arrows as filled triangles, as MenuRow draws its own, so no typeface is asked for them.
        private static Control Arrow(string ink, bool left)
        {
            var path = new Path { Data = Geometry.Parse(left ? "M 7,0 L 0,6 L 7,12 Z" : "M 0,0 L 7,6 L 0,12 Z"), Width = 7, Height = 12, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            path.Bind(Shape.FillProperty, path.GetResourceObservable(ink, c => (IBrush?)(c is Color colour ? new SolidColorBrush(colour) : Brushes.White)));
            return path;
        }

        // A heart on a 24-unit grid, filled or outlined, scaled to the words' size.
        internal static Control? Mark(PageMark mark, string ink, double size)
        {
            if (mark == PageMark.None) return null;
            var path = new Path
            {
                Data = Geometry.Parse("M 12,21 C 12,21 2,14.5 2,8.5 C 2,5.4 4.4,3 7.3,3 C 9.3,3 11,4.2 12,6 C 13,4.2 14.7,3 16.7,3 C 19.6,3 22,5.4 22,8.5 C 22,14.5 12,21 12,21 Z"),
                Width = size, Height = size, Stretch = Stretch.Uniform, StrokeThickness = mark == PageMark.Heart ? 1.6 : 0, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
            };
            path.Bind(mark == PageMark.Heart ? Shape.StrokeProperty : Shape.FillProperty, path.GetResourceObservable(ink, c => (IBrush?)(c is Color colour ? new SolidColorBrush(colour) : Brushes.White)));
            return path;
        }

        private TextBlock Word(string text, string ink, double? size = null)
        {
            var t = new TextBlock { Text = text, FontFamily = LunaFonts.Body, FontSize = size ?? Type.Row, VerticalAlignment = VerticalAlignment.Center };
            t.Bind(TextBlock.ForegroundProperty, t.GetResourceObservable(ink, c => c is Color colour ? new SolidColorBrush(colour) : Brushes.White));
            return t;
        }

        private Control DesktopAction(PageAction a)
        {
            if (a.Choices is { Count: > 0 } choices && a.Value is not null)
            {
                var menu = new MenuFlyout();
                for (int i = 0; i < choices.Count; i++)
                {
                    int to = i;
                    var item = new MenuItem { Header = choices[i] };
                    item.Click += (_, _) => ActionStepped?.Invoke(a.Key, to - choices.ToList().IndexOf(a.Value));
                    menu.Items.Add(item);
                }
                var split = new SplitButton { Content = $"{a.Text}: {a.Value}", Flyout = menu, MinHeight = 28, Padding = new Thickness(12, 2), IsEnabled = a.IsEnabled, Name = "PageAction." + a.Key, FontFamily = LunaFonts.Body, FontSize = Type.Body };
                split.Click += (_, _) => ActionInvoked?.Invoke(a.Key);
                return split;
            }
            object content = a.Mark == PageMark.None ? a.Text : new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { Mark(a.Mark, "LunaTextColor", 12)!, new TextBlock { Text = a.Text, VerticalAlignment = VerticalAlignment.Center } } };
            var button = new Button { Content = content, MinHeight = 28, MinWidth = 96, Padding = new Thickness(12, 2), IsEnabled = a.IsEnabled, Name = "PageAction." + a.Key, HorizontalContentAlignment = HorizontalAlignment.Center, FontFamily = LunaFonts.Body, FontSize = Type.Body };
            if (a.IsMain)
            {
                button[!Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty] = new DynamicResourceExtension("LunaAccent");
                button[!Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty] = new DynamicResourceExtension("LunaOnAccent");
            }
            button.Click += (_, _) => ActionInvoked?.Invoke(a.Key);
            return button;
        }

        private void FillActionsFocus()
        {
            for (int i = 0; i < _actionRow.Children.Count; i++)
                if (_actionRow.Children[i] is Panel { Children: [_, Border ring] }) ring.IsVisible = Big && _inActions && i == _actionAt;
        }

        private void FillStrip()
        {
            Panel row = Big ? _strip : _tabs;
            (Big ? _tabs : (Panel)_strip).Children.Clear();
            row.Children.Clear();
            IEnumerable<(string Key, string Label, bool Chosen, string? Status)> shown = _pageTitle is { } page
                ? new[] { ("trail", _trail!, false, (string?)null), ("page", page, true, null) }
                : _sections.Select(s => (s.Key, s.Label, s.Key == _selected, s.Status));
            int chosen = 0, n = 0;
            foreach ((string key, string label, bool isChosen, string? status) in shown)
            {
                var button = new Avalonia.Controls.Primitives.ToggleButton { Content = label.ToUpperInvariant(), IsChecked = isChosen, Focusable = !Big, IsTabStop = !Big };
                if (!Big)
                {
                    button.FontSize = Type.Strong;
                    button.MinHeight = 30;
                    button.Padding = new Thickness(0, 4);
                }
                AutomationProperties.SetName(button, status is { Length: > 0 } ? $"{label}, {status}" : label);
                if (_pageTitle is null)
                {
                    string k = key;
                    button.Click += (_, _) => { if (k != _selected) Choose(k); else button.IsChecked = true; };
                }
                else button.Click += (_, _) => button.IsChecked = isChosen;
                if (isChosen) chosen = n;
                n++;
                row.Children.Add(button);
            }
            _strip.Chosen = chosen;
        }

        private void FillContent()
        {
            _rowControls.Clear();
            _tileControls.Clear();
            IReadOnlyList<string> keys = Keys();
            if (_focusKey is not null && !keys.Contains(_focusKey)) _focusKey = keys.FirstOrDefault();
            if (!_inActions && keys.Count == 0) _inActions = true;
            _contentHost.Child = _content switch
            {
                PageContent.Rows r => Big ? BigRows(r) : DesktopRows(r),
                PageContent.Tiles t => Big ? BigTiles(t) : DesktopTiles(t),
                PageContent.Text x => TextContent(x),
                _ => null,
            };
            FillActionsFocus();
            RefreshHints();
        }

        private void FillContentFocus()
        {
            if (_content is PageContent.Rows r && Big) { _contentHost.Child = BigRows(r); return; }
            if (_content is PageContent.Tiles t && Big) { _contentHost.Child = BigTiles(t); return; }
            foreach ((string key, Control ring, Control? buttons, TextBlock label) in _tileControls)
            {
                ring.IsVisible = !_inActions && key == _focusKey;
                label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable(ring.IsVisible ? "LunaTextStrong" : "LunaText"));
                if (buttons is not null) buttons.IsVisible = key == _focusKey;
            }
        }

        // The rows that fit, whole, the focused one kept in view; the rest scroll by whole rows.
        private Control BigRows(PageContent.Rows r)
        {
            var stack = new StackPanel { Name = "PageRows" };
            double room = ContentHeight;
            if (r.Sentence is { Length: > 0 } sentence)
            {
                var s = new TextBlock { Text = sentence, FontFamily = LunaFonts.Body, FontSize = Type.Body, TextWrapping = TextWrapping.Wrap, MaxLines = 2, Margin = new Thickness(0, 0, 0, 12) };
                s.Bind(TextBlock.ForegroundProperty, s.GetResourceObservable("LunaText"));
                stack.Children.Add(s);
                room -= 2 * Type.Body * 1.25 + 12;
            }
            int fit = Math.Max(1, (int)Math.Floor(room / RowHeight));
            int at = _focusKey is null ? 0 : Math.Max(0, r.Items.ToList().FindIndex(i => i.Key == _focusKey));
            if (_inActions && _focusKey is null) at = 0;
            if (at < _firstShown) _firstShown = at;
            if (at >= _firstShown + fit) _firstShown = at - fit + 1;
            _firstShown = Math.Clamp(_firstShown, 0, Math.Max(0, r.Items.Count - fit));
            foreach (PageRow row in r.Items.Skip(_firstShown).Take(fit)) stack.Children.Add(BigRow(row, !_inActions && row.Key == _focusKey));
            return stack;
        }

        private MenuRow BigRow(PageRow row, bool focused)
        {
            var m = new MenuRow
            {
                Name = "PageRow." + row.Key,
                Label = row.Label,
                Value = row.Status ?? (row.Kind == PageRowKind.Asks ? "…" : null),
                Kind = row.Kind switch { PageRowKind.Opens or PageRowKind.Text => MenuRowKind.Submenu, PageRowKind.Steps => MenuRowKind.Option, _ => MenuRowKind.Action },
                IsHighlighted = focused,
                LetterCase = LetterCase.None,
                FontPath = LunaFonts.BodyFile,
                TextSize = Type.Row,
                RowHeight = RowHeight,
                Inset = 24,
                Scale = 1,
            };
            m.Bind(MenuRow.TextColorProperty, m.GetResourceObservable(row.IsEnabled ? "LunaTextColor" : "LunaMutedColor", c => c is Color colour ? colour : m.TextColor));
            m.Bind(MenuRow.HighlightTextColorProperty, m.GetResourceObservable("LunaTextStrongColor", c => c is Color colour ? colour : m.HighlightTextColor));
            m.Bind(MenuRow.BarColorProperty, m.GetResourceObservable("LunaSurfaceRaisedColor", c => c is Color colour ? colour : m.BarColor));
            m.Bind(MenuRow.RuleColorProperty, m.GetResourceObservable("LunaRuleColor", c => c is Color colour ? colour : m.RuleColor));
            m.Bind(MenuRow.AccentColorProperty, m.GetResourceObservable("LunaAccentColor", c => c is Color colour ? colour : m.AccentColor));
            if (!focused) m.Bind(MenuRow.ValueColorProperty, m.GetResourceObservable("LunaMutedColor", c => c is Color colour ? colour : (Color?)null));
            m.PointerPressed += (_, _) => { _inActions = false; _focusKey = row.Key; Refocused(); if (row.IsEnabled) RowChosen?.Invoke(row.Key); };
            return m;
        }

        private Control DesktopRows(PageContent.Rows r)
        {
            var stack = new StackPanel { Name = "PageRows", Spacing = 4 };
            if (r.Sentence is { Length: > 0 } sentence) stack.Children.Add(new TextBlock { Text = sentence, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
            _filling = true;
            foreach (PageRow row in r.Items) stack.Children.Add(new FieldRow { Name = "PageRow." + row.Key, Label = row.Label, Hint = row.Sentence ?? string.Empty, Content = DesktopRowControl(row), MinHeight = 48 });
            _filling = false;
            if (r.Buttons is { Count: > 0 } buttons)
            {
                var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
                foreach ((string key, string text) in buttons)
                {
                    var b = new Button { Content = text, Name = "PageButton." + key, Height = 28 };
                    b.Click += (_, _) => ContentButton?.Invoke(key);
                    bar.Children.Add(b);
                }
                stack.Children.Add(bar);
            }
            return stack;
        }

        private Control DesktopRowControl(PageRow row)
        {
            switch (row.Kind)
            {
                case PageRowKind.Steps when row.Choices is { Count: > 0 } choices:
                    var box = new ComboBox { ItemsSource = choices, SelectedItem = row.Status, MinWidth = 240, IsEnabled = row.IsEnabled };
                    box.SelectionChanged += (_, _) =>
                    {
                        if (_filling || box.SelectedItem is not string picked) return;
                        int by = choices.ToList().IndexOf(picked) - choices.ToList().IndexOf(row.Status ?? "");
                        if (by != 0) RowStepped?.Invoke(row.Key, by);
                    };
                    return box;
                case PageRowKind.Text:
                    var text = new TextBox { Text = row.Status ?? string.Empty, MinWidth = 240, IsEnabled = row.IsEnabled };
                    void Commit() { if ((text.Text ?? "") != (row.Status ?? "")) RowEdited?.Invoke(row.Key, text.Text ?? ""); }
                    text.LostFocus += (_, _) => Commit();
                    text.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
                    return text;
                case PageRowKind.Action when !row.IsEnabled && row.Status is not null:
                    return new TextBlock { Text = row.Status, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                default:
                    string words = row.Kind switch { PageRowKind.Opens => (row.Status ?? "Open") + " ›", PageRowKind.Asks => row.Status ?? "Ask…", _ => row.Status ?? row.Label };
                    var b = new Button { Content = words, IsEnabled = row.IsEnabled, HorizontalAlignment = HorizontalAlignment.Left };
                    b.Click += (_, _) => { _inActions = false; _focusKey = row.Key; RowChosen?.Invoke(row.Key); };
                    return b;
            }
        }

        // The tiles that fit across, whole, the focused one kept in view.
        private Control BigTiles(PageContent.Tiles t)
        {
            var strip = new StackPanel { Name = "PageTiles", Orientation = Orientation.Horizontal, Spacing = 24 };
            double across = Bounds.Width > 0 ? Bounds.Width / Unit(MenuPanel.GetScale(this)) - 2 * Side : 1280 - 2 * Side;
            int fit = Math.Max(1, (int)Math.Floor((across + 24) / (t.Width + 24)));
            int at = _focusKey is null ? 0 : Math.Max(0, t.Items.ToList().FindIndex(i => i.Key == _focusKey));
            if (at < _firstShown) _firstShown = at;
            if (at >= _firstShown + fit) _firstShown = at - fit + 1;
            _firstShown = Math.Clamp(_firstShown, 0, Math.Max(0, t.Items.Count - fit));
            foreach (PageTile tile in t.Items.Skip(_firstShown).Take(fit)) strip.Children.Add(Tile(tile, t.Width, t.Height, !_inActions && tile.Key == _focusKey));
            return strip;
        }

        private Control Tile(PageTile tile, double width, double height, bool focused)
        {
            double radius = Big ? 8 : 6;
            var card = new Border { Width = width, Height = height, CornerRadius = new CornerRadius(radius), ClipToBounds = true };
            if (tile.IsEmpty)
            {
                var dashes = new Rectangle { StrokeThickness = Big ? 2 : 1, StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }, RadiusX = radius, RadiusY = radius, IsHitTestVisible = false };
                dashes[!Shape.StrokeProperty] = new DynamicResourceExtension("LunaBorder");
                var empty = new TextBlock { Text = tile.Caption ?? "Empty", FontFamily = LunaFonts.Body, FontSize = Big ? Type.Body : Type.Body, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                empty.Bind(TextBlock.ForegroundProperty, empty.GetResourceObservable("LunaMuted"));
                card.Child = new Panel { Children = { dashes, empty } };
            }
            else
            {
                card[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
                card.Child = tile.Picture is null && tile.Caption is null ? null : Card(tile.Picture, tile.Caption ?? string.Empty, tile.Mark, Big ? 22 : 13);
            }
            var face = new Panel { Width = width, Height = height, Name = "PageTile." + tile.Key };
            face.Children.Add(card);
            if (tile.IsMarked)
            {
                var dot = new Ellipse { Width = 20, Height = 20, Margin = new Thickness(0, 8, 8, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
                dot[!Shape.FillProperty] = new DynamicResourceExtension("LunaAccent");
                face.Children.Add(dot);
            }
            double thick = Big ? 3 : 2, offset = Big ? 4 : 1;
            var ring = new Border { Margin = new Thickness(-(thick + offset)), BorderThickness = new Thickness(thick), CornerRadius = new CornerRadius(radius + thick + offset), IsHitTestVisible = false, IsVisible = focused };
            ring[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaAccent");
            face.Children.Add(ring);
            Control? buttons = null;
            if (!Big && tile.Buttons is { Count: > 0 } list)
            {
                var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8), IsVisible = tile.Key == _focusKey };
                foreach ((string key, string text) in list)
                {
                    var b = new Button { Content = text, Name = $"PageTile.{tile.Key}.{key}", Height = 24, FontSize = Type.Caption, Padding = new Thickness(12, 0) };
                    b.Click += (_, _) => TileButton?.Invoke(tile.Key, key);
                    bar.Children.Add(b);
                }
                face.Children.Add(bar);
                buttons = bar;
                face.PointerEntered += (_, _) => bar.IsVisible = true;
                face.PointerExited += (_, _) => bar.IsVisible = tile.Key == _focusKey;
            }
            face.PointerPressed += (_, e) =>
            {
                _inActions = false;
                _focusKey = tile.Key;
                Refocused();
                if (e.ClickCount == 2 || Big) TileChosen?.Invoke(tile.Key);
            };
            AutomationProperties.SetName(face, tile.Status is { Length: > 0 } s ? $"{tile.Label}, {s}" : tile.Label);
            var cell = new StackPanel { Spacing = Big ? 4 : 2, Width = width };
            cell.Children.Add(face);
            var label = new TextBlock { Text = tile.Label, FontFamily = LunaFonts.Body, FontSize = Type.Caption, TextWrapping = TextWrapping.Wrap, MaxLines = tile.Status is { Length: > 0 } ? 1 : 2, Margin = new Thickness(0, Big ? 8 : 6, 0, 0) };
            label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable(focused ? "LunaTextStrong" : "LunaText"));
            _tileControls.Add((tile.Key, ring, buttons, label));
            cell.Children.Add(label);
            if (tile.Status is { Length: > 0 } status)
            {
                var line = new TextBlock { Text = status, FontFamily = LunaFonts.Body, FontSize = Type.Caption, TextWrapping = TextWrapping.Wrap, MaxLines = 2 };
                line.Bind(TextBlock.ForegroundProperty, line.GetResourceObservable("LunaMuted"));
                cell.Children.Add(line);
            }
            return cell;
        }

        private Control DesktopTiles(PageContent.Tiles t)
        {
            var wrap = new WrapPanel { Name = "PageTiles", Orientation = Orientation.Horizontal, ItemSpacing = 16, LineSpacing = 16, Margin = new Thickness(4) };
            foreach (PageTile tile in t.Items) wrap.Children.Add(Tile(tile, 160, 120, !_inActions && tile.Key == _focusKey));
            return wrap;
        }

        private Control TextContent(PageContent.Text x)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Big ? "*,24,388" : "*,24,320") };
            int lines = (int)Math.Floor(ContentHeight / (Type.Body * 1.2));
            var words = new TextBlock { Name = "PageWords", Text = x.Words, FontFamily = LunaFonts.Body, FontSize = Type.Body, TextWrapping = TextWrapping.Wrap, MaxLines = Big ? lines : 0, VerticalAlignment = VerticalAlignment.Top };
            words.Bind(TextBlock.ForegroundProperty, words.GetResourceObservable("LunaText"));
            grid.Children.Add(words);
            if (x.Picture is not null || x.Caption is not null)
            {
                var picture = new Border { CornerRadius = new CornerRadius(Big ? 8 : 6), ClipToBounds = true, Height = Big ? 222 : 180, VerticalAlignment = VerticalAlignment.Top, Child = Card(x.Picture, x.Caption ?? string.Empty, null, Big ? 26 : 14), Name = "PagePicture" };
                picture[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
                Grid.SetColumn(picture, 2);
                grid.Children.Add(picture);
            }
            return grid;
        }

        // The viewer over the whole page: the picture on the void, and a caption bar 80 tall at the foot with its words and the help bar.
        private void BuildViewer()
        {
            if (_viewer.Children.Count > 0) return;
            _viewer[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaVoid");
            _viewerPicture.Margin = new Thickness(Side, 40, Side, 100);
            var bar = new Grid { Height = 80, VerticalAlignment = VerticalAlignment.Bottom, ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            bar[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaCaptionSurface");
            _viewerCaption.FontFamily = LunaFonts.Body;
            _viewerCaption.FontSize = LunaTokens.BigScreenType.Body;
            _viewerCaption.Margin = new Thickness(Side, 0, 24, 0);
            _viewerCaption.Bind(TextBlock.ForegroundProperty, _viewerCaption.GetResourceObservable("LunaOnArt"));
            _viewerHints.Margin = new Thickness(0, 0, Side, 0);
            Grid.SetColumn(_viewerHints, 1);
            bar.Children.Add(new Viewbox { Child = _viewerCaption, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
            bar.Children.Add(_viewerHints);
            _viewer.Children.Add(_viewerPicture);
            _viewer.Children.Add(bar);
        }

        private static Control Detach(Control control)
        {
            switch (control.Parent)
            {
                case Panel panel: panel.Children.Remove(control); break;
                case Decorator decorator: decorator.Child = null; break;
                case ContentControl content: content.Content = null; break;
            }
            return control;
        }
    }
}
