using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
