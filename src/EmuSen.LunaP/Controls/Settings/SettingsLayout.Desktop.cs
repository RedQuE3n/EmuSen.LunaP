using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // The desktop form of the same description: a source list of the sections, the header with Back and search, rows of 48 with their controls, and the pane where the row has a picture - see docs/LunaP.md §213.4.
    public partial class SettingsLayout
    {
        /// <summary>The desktop's sections list, the pane's width, a row's height and the search field's width.</summary>
        public const double DesktopSections = 200, DesktopPaneWidth = 276, DesktopRowHeight = 48, DesktopSearchWidth = 220, DesktopGutter = 24;

        private readonly SourceList _sourceList = new() { Name = "SettingsSourceList" };
        private readonly TextBox _searchBox = new() { Name = "SettingsSearch", PlaceholderText = "Search settings", Width = DesktopSearchWidth, Height = 28 };
        private bool _filling;

        /// <summary>Raised when the desktop's Back is pressed, for the host to close the page shown.</summary>
        public event Action? BackRequested;

        /// <summary>Raised as the desktop's search field changes, for the host to filter rows across every section.</summary>
        public event Action<string>? SearchChanged;

        private static LunaTypeScale DeskType => LunaTokens.DesktopType;

        private void WireDesktop()
        {
            _sourceList.Chose += key => { if (_filling || key == _selected) return; _selected = key; Fill(); SectionChosen?.Invoke(key); };
            _searchBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !_filling) SearchChanged?.Invoke(_searchBox.Text ?? string.Empty); };
            AutomationProperties.SetName(_searchBox, "Search settings");
        }

        private void FillDesktop()
        {
            _filling = true;
            foreach (Control c in new Control[] { _sourceList, _searchBox })
                switch (c.Parent)
                {
                    case Panel p: p.Children.Remove(c); break;
                    case Decorator d: d.Child = null; break;
                }
            _sourceList.Fill([new SourceListGroup("", _sections.Select(s => new SourceListItem(s.Key, s.Label)).ToList())], _selected);
            _sourceList.Width = DesktopSections;
            var sections = new Border { Child = _sourceList, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 12, 0, 0) };
            sections[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaRule");

            var header = new DockPanel { Name = "SettingsHeader", Height = 48, LastChildFill = true };
            _searchBox.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(_searchBox, Dock.Right);
            header.Children.Add(_searchBox);
            if (_trail.Count > 0)
            {
                var back = new Button { Name = "SettingsBack", Content = "‹ Back", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
                back.Click += (_, _) => BackRequested?.Invoke();
                DockPanel.SetDock(back, Dock.Left);
                header.Children.Add(back);
            }
            string title = _trail.Count > 0 ? string.Join("  ›  ", _trail) : _sections.FirstOrDefault(s => s.Key == _selected)?.Label ?? "";
            var words = new TextBlock { Name = "SettingsTitle", Text = title.ToUpperInvariant(), FontFamily = LunaFonts.Condensed, FontSize = DeskType.Title, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            words[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaTextStrong");
            header.Children.Add(words);

            var rows = new StackPanel { Name = "SettingsRows" };
            AutomationProperties.SetName(rows, _trail.LastOrDefault() ?? _sections.FirstOrDefault(s => s.Key == _selected)?.Label ?? "Settings");
            foreach (PageRow row in _rows) rows.Children.Add(DeskRow(row));
            Control scroller;
            if (_content is { } content) scroller = DeskContent(content);
            else
            {
                var scrolled = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                MenuLook.SetFadesEdges(scrolled, true);
                scroller = scrolled;
            }

            var body = new DockPanel { LastChildFill = true, Margin = new Thickness(DesktopGutter, 8, DesktopGutter, DesktopGutter) };
            DockPanel.SetDock(header, Dock.Top);
            body.Children.Add(header);
            SettingsPane? said = PaneKey is not { } paneKey ? null : PaneFor?.Invoke(paneKey);
            if (said is not null && (said.Preview is not null || _content is not null))
            {
                var pane = new Border { Name = "SettingsPane", Width = DesktopPaneWidth, CornerRadius = new CornerRadius(6), Padding = new Thickness(16), Margin = new Thickness(16, 8, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                pane[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
                var stack = new StackPanel { Spacing = 8 };
                if (said.Preview is { } preview) stack.Children.Add(Preview(preview, DesktopPaneWidth - 32, (DesktopPaneWidth - 32) * 0.66, DeskType));
                foreach (SettingsLine line in said.Lines) stack.Children.Add(Line(line, DeskType.Body));
                pane.Child = stack;
                DockPanel.SetDock(pane, Dock.Right);
                body.Children.Add(pane);
            }
            body.Children.Add(scroller);

            var whole = new DockPanel { LastChildFill = true };
            whole[!Panel.BackgroundProperty] = new DynamicResourceExtension("LunaSurface");
            DockPanel.SetDock(sections, Dock.Left);
            whole.Children.Add(sections);
            whole.Children.Add(body);
            _root.Children.Clear();
            _root.Children.Add(whole);
            _filling = false;
        }

        // A page of content on the desktop: the card in the rows' column, its tabs over the body and the caption under it (§213.7).
        private Control DeskContent(SettingsContent content)
        {
            var dock = new DockPanel { LastChildFill = true };
            if (content.Tabs.Count > 0)
            {
                WholeRow tabs = ContentTabs(content, 32);
                DockPanel.SetDock(tabs, Dock.Top);
                dock.Children.Add(tabs);
            }
            if (content.Caption is { Length: > 0 } caption)
            {
                var under = new TextBlock { Name = "SettingsContentCaption", Text = caption, FontSize = DeskType.Caption, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
                under[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
                DockPanel.SetDock(under, Dock.Bottom);
                dock.Children.Add(under);
            }
            Detach(content.Body);
            dock.Children.Add(new Border { Name = "SettingsContentBody", CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = content.Body, Margin = new Thickness(0, 8, 0, 0) });
            var card = new Border { Name = "SettingsContent", CornerRadius = new CornerRadius(6), Padding = new Thickness(16), Margin = new Thickness(0, 8, 0, 0), Child = dock };
            card[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            AutomationProperties.SetName(card, _trail.LastOrDefault() ?? "Settings");
            return card;
        }

        // One row: the label over its sentence, the control at the right; a click focuses it and a double click or Enter chooses it.
        private Control DeskRow(PageRow row)
        {
            var label = new TextBlock { Text = row.Label, FontSize = DeskType.Body, TextTrimming = TextTrimming.CharacterEllipsis };
            label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(row.IsEnabled ? "LunaText" : "LunaMuted");
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { label } };
            if (row.Sentence is { Length: > 0 } sentence)
            {
                var under = new TextBlock { Text = sentence, FontSize = DeskType.Caption, TextWrapping = TextWrapping.Wrap };
                under[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
                text.Children.Add(under);
            }
            Control control = DeskControl(row);
            control.VerticalAlignment = VerticalAlignment.Center;
            control.Margin = new Thickness(16, 0, 8, 0);
            DockPanel.SetDock(control, Dock.Right);
            var line = new DockPanel { LastChildFill = true, Children = { control, text } };
            var box = new Border { Name = "SettingsRow." + row.Key, MinHeight = DesktopRowHeight, Padding = new Thickness(8, 4), BorderThickness = new Thickness(0, 0, 0, 1), Child = line, Focusable = true };
            box[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaRule");
            if (row.Key == _focusKey) box[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            AutomationProperties.SetName(box, row.Status is { Length: > 0 } s ? $"{row.Label}, {s}" : row.Label);
            string key = row.Key;
            box.PointerPressed += (_, e) =>
            {
                if (e.ClickCount >= 2 && row.IsEnabled) RowChosen?.Invoke(key);
                else if (key != _focusKey) FocusOn(key);
            };
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter && row.IsEnabled) { RowChosen?.Invoke(key); e.Handled = true; } };
            return box;
        }

        private Control DeskControl(PageRow row)
        {
            string key = row.Key;
            switch (row.Kind)
            {
                case PageRowKind.Steps when row.Choices is { Count: > 0 } choices && choices.Count == 2 && choices.Contains("On") && choices.Contains("Off"):
                    var toggle = new LunaSwitch { IsChecked = row.Status == "On", IsEnabled = row.IsEnabled, OnContent = null, OffContent = null };
                    AutomationProperties.SetName(toggle, row.Label);
                    toggle.IsCheckedChanged += (_, _) => { if (!_filling && (toggle.IsChecked == true) != (row.Status == "On")) RowStepped?.Invoke(key, 1); };
                    return toggle;
                case PageRowKind.Steps when row.Choices is { Count: > 0 } choices:
                    var box = new ComboBox { ItemsSource = choices, SelectedItem = row.Status, MinWidth = 120, MaxWidth = 260, IsEnabled = row.IsEnabled };
                    AutomationProperties.SetName(box, row.Label);
                    box.SelectionChanged += (_, _) =>
                    {
                        if (_filling || box.SelectedItem is not string picked) return;
                        int by = choices.ToList().IndexOf(picked) - choices.ToList().IndexOf(row.Status ?? "");
                        if (by != 0) RowStepped?.Invoke(key, by);
                    };
                    return box;
                case PageRowKind.Steps:
                    var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                    var less = new Button { Content = "−", IsEnabled = row.IsEnabled };
                    var more = new Button { Content = "+", IsEnabled = row.IsEnabled };
                    AutomationProperties.SetName(less, $"{row.Label} down");
                    AutomationProperties.SetName(more, $"{row.Label} up");
                    less.Click += (_, _) => RowStepped?.Invoke(key, -1);
                    more.Click += (_, _) => RowStepped?.Invoke(key, 1);
                    steps.Children.Add(less);
                    steps.Children.Add(new TextBlock { Text = row.Status ?? "", MinWidth = 56, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                    steps.Children.Add(more);
                    return steps;
                case PageRowKind.Opens or PageRowKind.Asks or PageRowKind.Text:
                    string words = row.Kind == PageRowKind.Asks ? $"{row.Label}…" : row.Status is { Length: > 0 } status ? $"{status} ›" : "Open…";
                    var open = new Button { Content = words, IsEnabled = row.IsEnabled, MaxWidth = 300 };
                    AutomationProperties.SetName(open, row.Label);
                    open.Click += (_, _) => { _focusKey = key; RowChosen?.Invoke(key); };
                    return open;
                default:
                    var said = new TextBlock { Text = row.Status ?? "", VerticalAlignment = VerticalAlignment.Center };
                    said[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
                    return said;
            }
        }
    }
}
