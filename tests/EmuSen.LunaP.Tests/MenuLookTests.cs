using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // The big-screen menu's look given to stock controls, and a sheet framed as a menu - see docs/LunaP.md §196.
    public class MenuLookTests
    {
        private static readonly Color Input = Color.Parse("#0E0E10"), Bar = Color.Parse("#050507"), Light = Color.Parse("#EEEEF0");

        private static (StackPanel Root, Button Button, ListBox List, Border Surface) Stock(string name)
        {
            var button = new Button { Name = name + "Button", Content = "Apply" };
            var list = new ListBox { Name = name + "List", ItemsSource = new[] { "One", "Two", "Three" }, SelectedIndex = 1, Width = 300 };
            var surface = new Border { Height = 20, Width = 300 };
            surface[!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LunaInputSurface");
            var root = new StackPanel { Name = name, Spacing = 10, Width = 300, Children = { surface, list, button } };
            return (root, button, list, surface);
        }

        private static string Shown(Button button) =>
            string.Concat(button.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

        private static Rect In(Visual v, Visual root) => new(v.TranslatePoint(default, root)!.Value, v.Bounds.Size);

        [Fact]
        public Task The_look_paints_stock_controls_under_its_element_in_the_menu_colours_and_typeface_and_nothing_else() => UiTest.Run(() =>
        {
            var family = new FontFamily("Menu Test Family");
            (StackPanel look, Button lookButton, ListBox lookList, Border lookSurface) = Stock("Look");
            (StackPanel plain, Button plainButton, ListBox plainList, Border plainSurface) = Stock("Plain");
            var both = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Children = { look, plain } };
            both.Resources[MenuLook.FontFamilyKey] = family;
            MenuLook.SetIsOn(look, true);
            ToolWindow window = Show(both, 700, 400);
            RenderedFrame f = Frame(window);

            Assert.Contains(MenuLook.ClassName, look.Classes);
            Assert.True(MenuLook.Covers(lookButton));
            Assert.False(MenuLook.Covers(plainButton));
            Assert.Equal("APPLY", Shown(lookButton));
            Assert.Equal("Apply", Shown(plainButton));
            Assert.Same(family, lookList.GetVisualDescendants().OfType<TextBlock>().First().FontFamily);
            Assert.NotSame(family, plainList.GetVisualDescendants().OfType<TextBlock>().First().FontFamily);

            Rect surface = In(lookSurface, both);
            Assert.True(Near(At(f, surface.Center.X, surface.Center.Y ), Input), $"{At(f, surface.Center.X, surface.Center.Y)}");
            Rect other = In(plainSurface, both);
            Assert.False(Near(At(f, other.Center.X, other.Center.Y ), Input));

            // The chosen row is the menu's bar from one edge of the list to the other; the plain list's is not.
            Rect row = In((Control)lookList.ContainerFromIndex(1)!, both);
            for (double x = row.Left + 1; x < row.Right - 1; x += 20) Assert.True(Near(At(f, x, row.Top + 3), Bar), $"x {x}: {At(f, x, row.Top + 3)}");
            Rect plainRow = In((Control)plainList.ContainerFromIndex(1)!, both);
            Assert.False(Near(At(f, plainRow.Center.X, plainRow.Top + 3), Bar));

            MenuLook.SetIsOn(look, false);
            Dispatcher.UIThread.RunJobs();
            RenderedFrame back = Frame(window);
            Assert.DoesNotContain(MenuLook.ClassName, look.Classes);
            Assert.Equal("Apply", Shown(lookButton));
            Assert.False(Near(At(back, surface.Center.X, surface.Center.Y ), Input));
            window.Close();
        });

        [Fact]
        public Task A_focused_button_in_the_look_is_filled_near_black_as_a_row_s_bar_is() => UiTest.Run(() =>
        {
            (StackPanel look, Button button, _, _) = Stock("Look");
            look.Background = new SolidColorBrush(Color.Parse("#1B1B1E"));
            MenuLook.SetIsOn(look, true);
            ToolWindow window = Show(look, 400, 400);
            Rect at = In(button, window);
            RenderedFrame before = Frame(window);
            Assert.False(Near(At(before, at.Left + 4, at.Center.Y), Bar));
            button.Focus(NavigationMethod.Directional);
            RenderedFrame focused = Frame(window);
            Assert.True(Near(At(focused, at.Left + 4, at.Center.Y), Bar), $"{At(focused, at.Left + 4, at.Center.Y)}");
            Assert.True(button.Bounds.Height >= 44, $"{button.Bounds.Height}");
            window.Close();
        });

        [Fact]
        public Task WhenApplied_runs_once_where_the_look_is_on_and_never_where_it_is_not() => UiTest.Run(() =>
        {
            var inside = new Border();
            var outside = new Border();
            int ran = 0, strayed = 0;
            MenuLook.WhenApplied(inside, () => ran++);
            MenuLook.WhenApplied(outside, () => strayed++);
            var look = new Border { Child = inside };
            MenuLook.SetIsOn(look, true);
            var first = new StackPanel { Children = { look, outside } };
            ToolWindow window = Show(first, 200, 200);
            Assert.Equal(1, ran);
            Assert.Equal(0, strayed);
            window.Content = null;
            first.Children.Clear();
            window.Content = new StackPanel { Children = { look } };
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, ran);
            window.Close();
        });

        private static (ToolWindow Host, SheetLayer Layer) Host(double width = 1280, double height = 800, double scale = 1)
        {
            var layer = new SheetLayer { PresentsWindows = true, MenuLook = true };
            var host = new ToolWindow { Width = width, Height = height, Content = new Grid { Children = { new Border(), layer } } };
            MenuPanel.SetScale(host, scale);
            host.Show();
            return (host, layer);
        }

        [Fact]
        public Task A_layer_with_the_menu_look_frames_a_plain_sheet_as_a_menu_titled_by_its_window_with_its_help_bar() => UiTest.Run(() =>
        {
            (ToolWindow host, SheetLayer layer) = Host();
            IReadOnlyList<HintEntry> defaults = [new HintEntry("Select"), new HintEntry("Back")];
            layer.MenuHintsFor = _ => defaults;
            var content = new Border { Name = "Status", Height = 200 };
            var window = new ToolWindow { Title = "Scraping", Width = 720, Content = content };
            _ = SheetLayer.Show(window, host);
            host.UpdateLayout();

            MenuPanel panel = layer.GetVisualDescendants().OfType<MenuPanel>().Single();
            Assert.Equal("Scraping", panel.Title);
            Assert.Same(defaults, panel.Hints);
            Assert.True(MenuLook.Covers(content));
            Assert.True(layer.DrawsMenu(window));
            Assert.Null(layer.Background);
            Assert.Equal(Math.Min(1280 * 0.66, 800 * 1.05), panel.PanelBounds.Width, 0.5);
            Assert.Equal(640, panel.PanelBounds.Center.X, 0.5);
            Assert.DoesNotContain(layer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Scraping");

            // A window's own help and footer win over the layer's; a title that changes follows.
            IReadOnlyList<HintEntry> own = [new HintEntry("Rewind Here")];
            MenuLook.SetHints(window, own);
            MenuLook.SetFooter(window, "Attribution\nsecond line");
            window.Title = "Finished";
            Assert.Same(own, panel.Hints);
            Assert.Equal("Attribution second line", panel.Footer);
            Assert.Equal(3, panel.FooterMaxLines);
            Assert.Equal("Finished", panel.Title);
            window.Close();

            // A window the host keeps in the plain frame has it: a title line, the layer's fill, no menu.
            var plain = new ToolWindow { Title = "Controller Bindings", Width = 600, Content = new Border() };
            layer.MenuFrameFor = w => !ReferenceEquals(w, plain);
            _ = SheetLayer.Show(plain, host);
            host.UpdateLayout();
            Assert.Empty(layer.GetVisualDescendants().OfType<MenuPanel>());
            Assert.Contains(layer.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Controller Bindings");
            Assert.False(layer.DrawsMenu(plain));
            Assert.NotNull(layer.Background);
            plain.Close();

            // A window that needs more room than a menu takes its own share of the width.
            var wide = new ToolWindow { Title = "Cheat Database", Content = new Border { Height = 100 } };
            MenuLook.SetWidthFraction(wide, 0.8);
            _ = SheetLayer.Show(wide, host);
            host.UpdateLayout();
            Assert.Equal(1280 * 0.8, layer.GetVisualDescendants().OfType<MenuPanel>().Single().PanelBounds.Width, 0.5);
            wide.Close();
            host.Close();
        });

        [Fact]
        public Task A_framed_sheet_s_content_is_laid_out_at_the_menu_s_scale() => UiTest.Run(() =>
        {
            (ToolWindow host, SheetLayer layer) = Host(1920, 1200, 1.5);
            var content = new Border { Width = 300, Height = 100 };
            var window = new ToolWindow { Title = "Scaled", Content = content };
            _ = SheetLayer.Show(window, host);
            host.UpdateLayout();
            Rect shown = new(content.TranslatePoint(default, host)!.Value, content.TranslatePoint(new Point(300, 100), host)!.Value);
            Assert.Equal(450, shown.Width, 0.5);
            Assert.Equal(150, shown.Height, 0.5);
            window.Close();
            host.Close();
        });

        // Avalonia 12.1 shrank a list's effective viewport by the scale a second time for a clipping child of a scaled control, and rows it showed were never built - §196.4.
        [Fact]
        public Task A_list_at_the_foot_of_a_scaled_framed_sheet_builds_every_row_it_shows() => UiTest.Run(() =>
        {
            (ToolWindow host, SheetLayer layer) = Host(1920, 1200, 1.5);
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 3).Select(i => $"Row {i}").ToArray(), Height = 3 * 42 + 2 };
            var content = new DockPanel { LastChildFill = true, Children = { new Border { Height = 200 }, list } };
            DockPanel.SetDock(content.Children[0], Dock.Top);
            var window = new ToolWindow { Title = "Cheats", Content = content };
            _ = SheetLayer.Show(window, host);
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            host.UpdateLayout();
            Assert.Equal(3, list.GetRealizedContainers().Count());
            window.Close();
            host.Close();
        });
    }
}
