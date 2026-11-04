using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // The gap between columns - see docs/LunaP.md §216. Two Auto columns side by side are as wide as their widest values,
    // so without a gap the first's widest value ends where the second's begins and the two read as one word.
    public class TableColumnGapTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableColumnGapTests).GetTypeInfo().Assembly);

        private sealed record Row(string Console, string Format, string Size);

        private static readonly Row[] Rows = [new("PlayStation 2", "CHD", "4.3 GB"), new("SNES", "SFC", "2.0 MB")];

        // The left and right edges of a text's control in the table, the widest of that text's cells.
        private static (double Left, double Right) Edges(LunaTable<Row> table, string text)
        {
            Control cell = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
            Point at = cell.TranslatePoint(default, table)!.Value;
            return (at.X, at.X + cell.Bounds.Width);
        }

        private static Task Shown(double gap, Action<LunaTable<Row>> assert) => Session.Dispatch(() =>
        {
            var table = new LunaTable<Row> { ColumnGap = gap };
            table.Column(new LunaColumn<Row>("Console", r => r.Console) { Width = "Auto" })
                .Column(new LunaColumn<Row>("Format", r => r.Format) { Width = "Auto" })
                .Column(new LunaColumn<Row>("Size", r => r.Size) { Width = "Auto" });
            table.Refresh(Rows);
            var window = new ToolWindow { Width = 600, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            assert(table);
            window.Close();
        }, default);

        [Fact]
        public Task Without_a_gap_auto_columns_touch() => Shown(0, table =>
            Assert.Equal(Edges(table, "PlayStation 2").Right, Edges(table, "CHD").Left, 1));

        [Fact]
        public Task A_gap_separates_every_column_but_the_last_heading_and_cells_alike() => Shown(16, table =>
        {
            Assert.True(Edges(table, "CHD").Left - Edges(table, "PlayStation 2").Right >= 16 - 0.5, "a cell's gap");
            Assert.True(Edges(table, "Format").Left - Edges(table, "Console").Right >= 16 - 0.5, "a heading's gap");
            Assert.True(Edges(table, "4.3 GB").Left - Edges(table, "CHD").Right >= 16 - 0.5, "the gap before the last column");
            Assert.Equal(Edges(table, "4.3 GB").Left, Edges(table, "Size").Left, 1);
        });

        [Fact]
        public void A_negative_gap_is_refused() => Assert.Throws<ArgumentOutOfRangeException>(() => new LunaTable<Row> { ColumnGap = -1 });
    }
}
