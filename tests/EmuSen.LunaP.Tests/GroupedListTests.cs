using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // GroupedList - see docs/LunaP.md §94.1. Headings on the first row of a group, never rows of their own; a virtualised list; the accent on the row and not the heading.
    public class GroupedListTests
    {
        private sealed record Shader(string Category, string Name, string? Folder = null);

        private static Shader[] Shaders() => new[]
        {
            new Shader("Built-in", "None"),
            new Shader("Built-in", "CRT (Lottes)"),
            new Shader("CRT", "crt-royale", "crt"),
            new Shader("CRT", "crt-royale-kurozumi", "crt"),
            new Shader("Handheld", "lcd-grid-v2", "handheld"),
        };

        private static GroupedList<Shader> List() => new()
        {
            Group = s => s.Category,
            Label = s => s.Name,
            Detail = s => s.Folder,
            Key = s => s.Name,
        };

        private static ToolWindow Show(Control content, double width = 320, double height = 480)
        {
            var window = new ToolWindow { Width = width, Height = height, Content = content };
            window.Show();
            Settle(window);
            return window;
        }

        private static void Settle(Window window)
        {
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
        }

        private static List<ListBoxItem> Items(Control list) => list.GetVisualDescendants().OfType<ListBoxItem>().ToList();

        private static string[] Texts(Control root, string style) =>
            root.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains(style)).Select(t => t.Text!).ToArray();

        private static Border Row(ListBoxItem item) => item.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("grouped-row"));

        [Fact]
        public Task A_heading_rides_on_the_first_row_of_each_group_with_its_count() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            list.Refresh(Shaders());
            ToolWindow window = Show(list);

            Assert.Equal(5, Items(list).Count);
            Assert.Equal(new[] { "BUILT-IN", "CRT", "HANDHELD" }, Texts(list, "grouped-heading"));
            Assert.Equal(new[] { "2", "2", "1" }, Texts(list, "grouped-count"));
            Assert.Single(Texts(Items(list)[2], "grouped-heading"));
            Assert.Empty(Texts(Items(list)[3], "grouped-heading"));

            // A detail line only where one was given.
            Assert.Equal(new[] { "crt", "crt", "handheld" }, Texts(list, "grouped-detail"));

            list.ShowGroupCounts = false;
            list.Refresh(Shaders());
            Settle(window);
            Assert.Empty(Texts(list, "grouped-count"));
            window.Close();
        });

        [Fact]
        public Task Refresh_keeps_the_selection_by_key_and_neither_it_nor_Select_raises_Chose() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            var chose = new List<Shader?>();
            list.Chose += s => chose.Add(s);
            list.Refresh(Shaders());
            list.Select(new Shader("CRT", "crt-royale-kurozumi"));
            ToolWindow window = Show(list);

            Assert.Equal("crt-royale-kurozumi", list.Selected?.Name);
            list.Refresh(Shaders().Where(s => s.Category != "Built-in"));
            Assert.Equal(1, list.SelectedIndex);
            list.Refresh(Shaders().Where(s => s.Category == "Handheld"));
            Assert.Null(list.Selected);
            Assert.Empty(chose);

            list.Refresh(Shaders());
            list.SelectedIndex = 4;
            Assert.Equal(new[] { "lcd-grid-v2" }, chose.Select(s => s!.Name));
            window.Close();
        });

        [Fact]
        public Task The_arrow_keys_move_the_selection_and_a_person_s_move_raises_Chose() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            var chose = new List<string>();
            list.Chose += s => chose.Add(s!.Name);
            list.Refresh(Shaders());
            list.Select(Shaders()[0]);
            ToolWindow window = Show(list);

            ((ListBoxItem)list.ContainerFromIndex(0)!).Focus(NavigationMethod.Directional);
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, string.Empty);
            window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, string.Empty);
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, string.Empty);
            window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, string.Empty);
            Settle(window);

            Assert.Equal("crt-royale", list.Selected?.Name);
            Assert.Equal(new[] { "CRT (Lottes)", "crt-royale" }, chose);
            window.Close();
        });

        // The container paints nothing, so the heading on a selected row keeps the surface; the row takes the accent.
        [Fact]
        public Task The_selected_row_is_painted_in_the_accent_and_its_heading_is_not() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            list.Refresh(Shaders());
            list.Select(Shaders()[2]);
            ToolWindow window = Show(list);

            Application.Current!.TryGetResource("LunaAccent", ThemeVariant.Dark, out object? accent);
            Application.Current!.TryGetResource("LunaOnAccent", ThemeVariant.Dark, out object? onAccent);
            Application.Current!.TryGetResource("LunaMuted", ThemeVariant.Dark, out object? muted);
            var item = (ListBoxItem)list.ContainerFromIndex(2)!;

            Assert.Equal(((ISolidColorBrush)accent!).Color, ((ISolidColorBrush)Row(item).Background!).Color);
            Assert.Equal(((ISolidColorBrush)onAccent!).Color, ((ISolidColorBrush)item.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("grouped-text")).Foreground!).Color);
            Assert.Equal(((ISolidColorBrush)muted!).Color, ((ISolidColorBrush)item.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("grouped-heading")).Foreground!).Color);
            ContentPresenter presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)presenter.Background!).Color);

            var other = (ListBoxItem)list.ContainerFromIndex(3)!;
            Assert.Equal(Colors.Transparent, ((ISolidColorBrush)Row(other).Background!).Color);
            window.Close();
        });

        [Fact]
        public Task A_badge_is_drawn_only_where_given_and_a_long_name_wraps_rather_than_being_cut() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            list.Badge = s => s.Name == "crt-royale" ? "In use" : null;
            list.Refresh(Shaders().Append(new Shader("Bezel", "MBZ 0 SMOOTH-ADV GLASS GDV-MINI-NTSC with a name longer than the window is wide")));
            ToolWindow window = Show(list, width: 240);

            Assert.Equal(new[] { "In use" }, Texts(list, "grouped-badge-text"));
            Assert.Single(list.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("grouped-badge")));

            TextBlock longName = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text!.StartsWith("MBZ", System.StringComparison.Ordinal));
            TextBlock shortName = list.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "None");
            Assert.Equal(TextWrapping.Wrap, longName.TextWrapping);
            Assert.True(longName.Bounds.Height > 2 * shortName.Bounds.Height, $"{longName.Bounds.Height} against {shortName.Bounds.Height}");
            Assert.True(longName.Bounds.Width <= 240);
            window.Close();
        });

        [Fact]
        public Task A_row_is_named_for_a_reader_by_its_heading_and_its_label() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            list.Refresh(Shaders());
            ToolWindow window = Show(list);

            Assert.Equal("CRT: crt-royale", AutomationProperties.GetName(list.ContainerFromIndex(2)!));
            Assert.Equal("crt-royale-kurozumi", AutomationProperties.GetName(list.ContainerFromIndex(3)!));
            window.Close();
        });

        [Fact]
        public Task Five_thousand_rows_realise_only_what_is_in_view() => UiTest.Run(() =>
        {
            GroupedList<Shader> list = List();
            list.Refresh(Enumerable.Range(0, 5000).Select(i => new Shader($"Group {i / 100}", $"Shader {i}")));
            ToolWindow window = Show(list, height: 600);

            int realised = Items(list).Count;
            Assert.InRange(realised, 1, 60);
            list.Select(new Shader("Group 49", "Shader 4990"));
            Settle(window);
            Assert.Contains(Items(list), i => i.IsSelected && ReferenceEquals(i, list.ContainerFromIndex(4990)));
            window.Close();
        });
    }
}
