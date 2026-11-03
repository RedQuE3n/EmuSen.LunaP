using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // Group headings that ride on a group's first row and are never rows themselves - see docs/LunaP.md §211.1.
    public class TableGroupTests
    {
        public sealed record Core(string Name, string? Maker);

        private static readonly Core[] Cores =
        [
            new("MoonRT", "Nintendo"), new("VenusRT", "Nintendo"), new("Nephrite", "Sega"), new("Diamond", "Sony"), new("Sapphire", "Sony"), new("Own", null),
        ];

        private static (Window Window, LunaTable<Core> Table) Host(IEnumerable<Core> rows)
        {
            var table = new LunaTable<Core> { GroupOf = c => c.Maker, Key = c => c.Name };
            table.Column(new LunaColumn<Core>("Core", c => c.Name));
            table.Refresh(rows);
            var window = new Window { Width = 600, Height = 600, Content = table };
            window.Show();
            UiTest.Settle(table);
            return (window, table);
        }

        private static List<string?> Bands(LunaTable<Core> table) => table.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains(LunaTable<Core>.GroupHeadingClass) && t.IsVisible).Select(t => t.Text).ToList();

        [Fact]
        public Task A_heading_rides_on_the_first_row_of_each_group_only() => UiTest.Run(() =>
        {
            var (window, table) = Host(Cores);
            Assert.Equal(new string?[] { "NINTENDO", null, "SEGA", "SONY", null, null }, Enumerable.Range(0, Cores.Length).Select(table.HeadingAt));
            Assert.Equal(new[] { "NINTENDO", "SEGA", "SONY" }, Bands(table));
            Assert.Equal(Cores.Length, table.Models.Count);
            window.Close();
        });

        [Fact]
        public Task One_group_draws_no_heading() => UiTest.Run(() =>
        {
            var (window, table) = Host(Cores.Where(c => c.Maker == "Sony"));
            Assert.Empty(Bands(table));
            Assert.Null(table.HeadingAt(0));
            window.Close();
        });

        [Fact]
        public Task The_keyboard_steps_from_row_to_row_past_every_heading() => UiTest.Run(() =>
        {
            var (window, table) = Host(Cores);
            table.Select(Cores[1]);
            UiTest.Settle(table);
            ListBoxItem item = table.GetVisualDescendants().OfType<ListBoxItem>().First(i => ReferenceEquals(i.DataContext, Cores[1]));
            item.Focus();
            window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, string.Empty);
            UiTest.Settle(table);
            Assert.Same(Cores[2], table.Selected);
            window.Close();
        });

        [Fact]
        public Task A_refreshed_table_moves_its_headings_and_names_the_row_with_its_heading() => UiTest.Run(() =>
        {
            var (window, table) = Host(Cores);
            table.Refresh(Cores.Skip(2));
            UiTest.Settle(table);
            Assert.Equal(new[] { "SEGA", "SONY" }, Bands(table));
            ListBoxItem nephrite = table.GetVisualDescendants().OfType<ListBoxItem>().First(i => ReferenceEquals(i.DataContext, Cores[2]));
            Assert.StartsWith("SEGA: ", AutomationProperties.GetName(nephrite));
            window.Close();
        });

        // No row is drawn cut: a table scrolled to a row far down shows only whole rows, headings and all (docs/LunaP.md §211.5).
        [Fact]
        public Task A_table_of_whole_rows_never_draws_a_row_cut_by_its_edge() => UiTest.Run(() =>
        {
            Core[] many = Enumerable.Range(0, 40).Select(i => new Core($"Core {i}", $"Maker {i / 3}")).ToArray();
            var table = new LunaTable<Core> { GroupOf = c => c.Maker, Key = c => c.Name, WholeRows = true, ShowsHeader = false };
            table.Column(new LunaColumn<Core>("Core", c => c.Name));
            table.Refresh(many);
            var window = new Window { Width = 400, Height = 307, Content = table };
            window.Show();
            UiTest.Settle(table);
            foreach (int at in new[] { 7, 22, 39, 13, 0 })
            {
                table.Select(many[at]);
                table.BringRowIntoView(many[at]);
                UiTest.Settle(table);
                Rect drawn = table.DrawnRows;
                Assert.True(drawn.Top <= 0.5, $"the rows start flush with the top after choosing {many[at].Name}, not at {drawn.Top}");
                Visual presenter = table.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
                foreach (ListBoxItem item in table.GetVisualDescendants().OfType<ListBoxItem>().Where(i => i.IsVisible))
                {
                    Rect box = new Rect(item.TranslatePoint(default, presenter)!.Value, item.Bounds.Size);
                    if (box.Intersects(drawn) && box.Height > 0) Assert.True(drawn.Contains(box.Deflate(0.5)), $"{item.DataContext} at {box} is cut by the drawn rows {drawn} after choosing {many[at].Name}");
                }
                ListBoxItem chosen = table.GetVisualDescendants().OfType<ListBoxItem>().First(i => ReferenceEquals(i.DataContext, many[at]));
                Assert.True(drawn.Contains(new Rect(chosen.TranslatePoint(default, presenter)!.Value, chosen.Bounds.Size).Deflate(0.5)), $"the chosen {many[at].Name} is drawn whole");
            }
            window.Close();
        });

        [Fact]
        public Task A_table_that_does_not_group_builds_no_band() => UiTest.Run(() =>
        {
            var table = new LunaTable<Core>();
            table.Column(new LunaColumn<Core>("Core", c => c.Name));
            table.Refresh(Cores);
            var window = new Window { Width = 600, Height = 600, Content = table };
            window.Show();
            UiTest.Settle(table);
            Assert.DoesNotContain(table.GetVisualDescendants().OfType<TextBlock>(), t => t.Classes.Contains(LunaTable<Core>.GroupHeadingClass));
            window.Close();
        });
    }
}
