using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // The big-screen menu: its rows, its panel, stock controls drawn as rows, the blurred backdrop and a chromeless sheet - see docs/LunaP.md §181.
    public class MenuTests
    {
        private static MenuRow Row(MenuRowKind kind, string? value = null, double width = 600) =>
            new() { Label = "Sort Games By", Value = value, Kind = kind, Width = width };

        [Fact]
        public Task An_option_row_puts_its_value_between_two_arrows_at_the_right_and_its_label_at_the_left() => UiTest.Run(() =>
        {
            MenuRow row = Row(MenuRowKind.Option, "Name, ascending");
            MenuRowLayout at = row.Layout(new Size(600, 54));
            Assert.Equal(new Rect(0, 0, 600, 53), at.Bar);
            Assert.Equal(new Rect(0, 53, 600, 1), at.Rule);
            Assert.True(at.LeftArrow.Right < at.Value.Left && at.Value.Right < at.RightArrow.Left, $"{at.LeftArrow} {at.Value} {at.RightArrow}");
            Assert.Equal(592, at.RightArrow.Right, 0.01);
            Assert.True(at.Label.Right < at.LeftArrow.Left);
            Assert.Equal(default, at.Chevron);
            Assert.Equal(default, at.Switch);
        });

        [Fact]
        public Task A_submenu_row_has_a_chevron_and_a_switch_row_a_switch_each_at_the_right_edge() => UiTest.Run(() =>
        {
            MenuRowLayout sub = Row(MenuRowKind.Submenu, "3 selected").Layout(new Size(600, 54));
            Assert.Equal(592, sub.Chevron.Right, 0.01);
            Assert.True(sub.Value.Right < sub.Chevron.Left);
            MenuRowLayout toggle = Row(MenuRowKind.Switch, "ignored").Layout(new Size(600, 54));
            Assert.Equal(592, toggle.Switch.Right, 0.01);
            Assert.Equal(default, toggle.Value);
        });

        [Fact]
        public Task A_row_is_its_design_height_times_the_inherited_scale() => UiTest.Run(() =>
        {
            var row = new MenuRow();
            var host = new StackPanel { Children = { row } };
            MenuPanel.SetScale(host, 1.5);
            ToolWindow window = Show(host, 400, 200);
            Assert.Equal(1.5, row.Scale);
            Assert.Equal(81, row.Bounds.Height, 0.01);
            window.Close();
        });

        [Fact]
        public Task A_highlighted_row_is_a_bar_of_its_colour_from_edge_to_edge() => UiTest.Run(() =>
        {
            MenuRow row = Row(MenuRowKind.Action, width: 300);
            row.IsHighlighted = true;
            row.BarColor = Colors.Red;
            ToolWindow window = Show(new StackPanel { Children = { row } }, 300, 60);
            RenderedFrame f = Frame(window);
            for (int x = 0; x < 300; x += 10) Assert.True(Near(At(f, x, 2), Colors.Red), $"x {x} is {At(f, x, 2)}");
            row.IsHighlighted = false;
            RenderedFrame plain = Frame(window);
            Assert.False(Near(At(plain, 150, 2), Colors.Red));
            window.Close();
        });

        [Fact]
        public Task A_button_drawn_as_a_row_keeps_its_content_its_click_and_takes_the_bar_with_the_focus() => UiTest.Run(() =>
        {
            int clicks = 0;
            var button = new Button { Content = "Edit This Game's Metadata" };
            button.Click += (_, _) => clicks++;
            MenuRows.Apply(button, MenuRowKind.Submenu, "value");
            var other = new Button { Content = "Other" };
            ToolWindow window = Show(new StackPanel { Children = { button, other } }, 400, 200);
            MenuRow row = button.GetVisualDescendants().OfType<MenuRow>().Single();
            Assert.Equal("Edit This Game's Metadata", button.Content);
            Assert.Equal("Edit This Game's Metadata", row.Label);
            Assert.Equal(MenuRowKind.Submenu, row.Kind);
            Assert.Equal("value", row.Value);
            Assert.False(row.IsHighlighted);
            button.Focus(NavigationMethod.Directional);
            Assert.True(row.IsHighlighted);
            other.Focus(NavigationMethod.Directional);
            Assert.False(row.IsHighlighted);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, clicks);
            MenuRows.SetValue(button, "changed");
            Assert.Equal("changed", row.Value);
            window.Close();
        });

        [Fact]
        public Task A_dropdown_drawn_as_a_row_shows_its_selection_between_arrows_and_still_steps_and_opens() => UiTest.Run(() =>
        {
            var choice = new Dropdown();
            choice.Fill(new[] { "A", "B", "C" }, "B");
            MenuRows.Apply(choice, "Jump To...");
            ToolWindow window = Show(new StackPanel { Children = { choice } }, 400, 200);
            MenuRow row = choice.GetVisualDescendants().OfType<MenuRow>().Single();
            Assert.Equal(MenuRowKind.Option, row.Kind);
            Assert.Equal("Jump To...", MenuRows.GetLabel(choice));
            Assert.Equal("B", row.Value);
            choice.SelectedIndex = 2;
            Assert.Equal("C", row.Value);
            choice.IsDropDownOpen = true;
            window.UpdateLayout();
            Assert.True(choice.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single().IsOpen);
            choice.IsDropDownOpen = false;
            window.Close();
        });

        [Fact]
        public Task A_list_drawn_as_rows_highlights_the_selection_and_gives_its_rows_back_when_the_look_is_removed() => UiTest.Run(() =>
        {
            var list = new ListBox { ItemsSource = new[] { "Resume", "Reset" } };
            list.ContainerPrepared += (_, e) => MenuRows.SetKind(e.Container, e.Index == 0 ? MenuRowKind.Action : MenuRowKind.Submenu);
            MenuRows.Apply(list);
            list.SelectedIndex = 1;
            ToolWindow window = Show(list, 400, 200);
            MenuRow[] rows = list.GetVisualDescendants().OfType<MenuRow>().ToArray();
            Assert.Equal(new[] { "Resume", "Reset" }, rows.Select(r => r.Label));
            Assert.Equal(new[] { false, true }, rows.Select(r => r.IsHighlighted));
            Assert.Equal(MenuRowKind.Submenu, rows[1].Kind);
            MenuRows.Remove(list);
            window.UpdateLayout();
            Assert.Empty(list.GetVisualDescendants().OfType<MenuRow>());
            window.Close();
        });

        [Fact]
        public Task The_panel_is_centred_as_wide_as_its_fraction_or_its_height_allows_and_shows_its_rows_whole() => UiTest.Run(() =>
        {
            var rows = new StackPanel();
            for (int i = 0; i < 20; i++) rows.Children.Add(new MenuRow { Label = $"Row {i}" });
            var panel = new MenuPanel { Title = "Main Menu", Footer = "Version", Child = new ScrollViewer { Content = rows }, Hints = new[] { new HintEntry("Select") { Button = PadGlyphButton.South } } };
            ToolWindow window = Show(panel, 1280, 800);
            Rect box = panel.PanelBounds;
            Assert.Equal(640, box.Center.X, 0.01);
            Assert.Equal(Math.Min(1280 * 0.66, 800 * 1.05), box.Width, 0.01);
            Rect child = new(panel.Child!.TranslatePoint(default, panel)!.Value, panel.Child.Bounds.Size);
            Assert.Equal(0, child.Height % 54, 0.01);
            Assert.True(child.Height >= 54 * 8, $"only {child.Height / 54} rows fit");
            Rect help = new(panel.HelpBar.TranslatePoint(default, panel)!.Value, panel.HelpBar.Bounds.Size);
            Assert.Equal(640, help.Center.X, 0.5);
            Assert.True(help.Top > box.Bottom, $"help {help} under panel {box}");
            Assert.True(help.Bottom <= 800);

            // A tall, narrow area caps the width by the height; a short list takes only its own height.
            window.Close();
            panel = new MenuPanel { Title = "Short", Child = new StackPanel { Children = { new MenuRow { Label = "One" } } }, MaxWidthToHeight = 0.5 };
            window = Show(panel, 1280, 800);
            Assert.Equal(400, panel.PanelBounds.Width, 0.01);
            Assert.Equal(100 + 54 + 20, panel.PanelBounds.Height, 0.01);
            window.Close();
        });

        [Fact]
        public Task The_backdrop_blurs_its_target_while_shown_and_leaves_an_effect_it_did_not_set() => UiTest.Run(() =>
        {
            var screen = new Border { Background = Brushes.SteelBlue };
            var backdrop = new BlurBackdrop { Target = screen, IsVisible = false };
            ToolWindow window = Show(new Grid { Children = { screen, backdrop } }, 200, 200);
            Assert.Null(screen.Effect);
            backdrop.IsVisible = true;
            Assert.True(backdrop.IsBlurring);
            Assert.Equal(16, Assert.IsType<BlurEffect>(screen.Effect).Radius);
            backdrop.IsVisible = false;
            Assert.Null(screen.Effect);
            var own = new DropShadowEffect();
            screen.Effect = own;
            backdrop.IsVisible = true;
            Assert.False(backdrop.IsBlurring);
            Assert.Same(own, screen.Effect);
            window.Close();
        });

        [Fact]
        public Task A_chromeless_sheet_fills_the_layer_with_its_content_alone_and_the_layer_draws_nothing_behind_it() => UiTest.Run(() =>
        {
            var layer = new SheetLayer { PresentsWindows = true };
            var host = new ToolWindow { Width = 800, Height = 600, Content = new Grid { Children = { new Border(), layer } } };
            host.Show();
            var content = new Border { Name = "Menu" };
            var menu = new ToolWindow { Title = "Hidden title", Width = 300, Content = content };
            SheetLayer.SetChromeless(menu, true);
            _ = SheetLayer.Show(menu, host);
            host.UpdateLayout();
            Assert.Equal(new Size(800, 600), content.Bounds.Size);
            Assert.DoesNotContain(layer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Hidden title");
            Assert.Null(layer.Background);

            var plain = new ToolWindow { Title = "Settings", Width = 300, Content = new Border() };
            _ = SheetLayer.Show(plain, host);
            host.UpdateLayout();
            Assert.NotNull(layer.Background);
            plain.Close();
            Assert.Null(layer.Background);
            menu.Close();
            host.Close();
        });
    }
}
