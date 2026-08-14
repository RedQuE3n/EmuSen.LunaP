using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // PER-COLUMN ALIGNMENT AND A SORT WITHOUT A CLICK - see docs/LunaP.md §70.
    //
    // The half of §54.3's per-column row that never landed. Two things worth knowing:
    //
    //   - ALIGNMENT IS SPELLED DIFFERENTLY PER CELL KIND, on purpose. A text cell takes
    //     TextAlignment and keeps its trimming; the other two take layout alignment. Asserting the
    //     property alone would pass for a rule applied to the wrong one, so the cell tests measure
    //     the property AND the trimming that the alternative would have cost.
    //   - A SORT THAT IS NEVER WRITTEN DOWN. §70.4's defect lived behind fixtures that all called
    //     SaveNow by hand; the guards for it are in TableLayoutTests, where the persistence lives.
    public class TableColumnTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableColumnTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, int size)
            {
                Name = name;
                Size = size;
            }

            public string Name { get; }
            public int Size { get; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("gamma", 30), new Row("alpha", 10), new Row("beta", 20),
        };

        private static Task Realised(Func<LunaTable<Row>> make, Action<LunaTable<Row>> assert) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = 500, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static LunaTable<Row> Aligned(HorizontalAlignment? across, VerticalAlignment? down = null)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };

            table.Column(new LunaColumn<Row>("name", r => r.Name) { Width = "200" })
                 .Column(new LunaColumn<Row>("size", r => r.Size.ToString())
                  {
                      Width = "120",
                      Alignment = across,
                      VerticalAlignment = down,
                      Sort = (a, b) => a.Size.CompareTo(b.Size),
                  })
                 .Column(new LunaColumn<Row>("ok", r => r.Size > 15)
                  {
                      Width = "80",
                      Alignment = across,
                  })
                 .Column(new LunaColumn<Row>(
                     "dot",
                     _ => new Ellipse { Width = 8, Height = 8, Fill = Brushes.Red },
                     _ => "a dot")
                  {
                      Width = "80",
                      Alignment = across,
                  });

            table.Refresh(Rows());
            return table;
        }

        private static Control Cell(LunaTable<Row> table, int column)
        {
            Assert.True(table.TryGetCell(table.Models[0], column, out Control? cell));
            return cell!;
        }

        // ---- nothing changes for a column that says nothing ----

        // §26.13. A column with no Alignment leaves every kind doing what it already did: a text
        // cell reading from the left, a checkbox pinned left so the rest of the cell still selects
        // the row (§57), a template cell under §69.2's rule.
        [Fact]
        public Task A_column_that_names_no_alignment_changes_nothing() =>
            Realised(() => Aligned(null), table =>
            {
                Assert.Equal(TextAlignment.Start, ((TextBlock)Cell(table, 1)).TextAlignment);
                Assert.Equal(HorizontalAlignment.Left, Cell(table, 2).HorizontalAlignment);
                Assert.Equal(HorizontalAlignment.Left, Cell(table, 3).HorizontalAlignment);
            });

        // ---- the alignment, per kind ----

        // A TEXT CELL TAKES TEXT ALIGNMENT AND KEEPS ITS TRIMMING, which is the whole reason this is
        // not one rule for all three kinds. The alternative - shrinking the TextBlock to its content
        // and pushing it right - produces the same picture for a short value and silently stops
        // trimming a long one, so the second assertion is the one that distinguishes them.
        [Fact]
        public Task A_right_aligned_text_cell_moves_its_text_and_still_trims() =>
            Realised(() => Aligned(HorizontalAlignment.Right), table =>
            {
                var cell = (TextBlock)Cell(table, 1);

                Assert.Equal(TextAlignment.Right, cell.TextAlignment);
                Assert.Equal(TextTrimming.CharacterEllipsis, cell.TextTrimming);
                Assert.True(cell.Bounds.Width > 100,
                    $"the cell shrank to {cell.Bounds.Width:F1}px, so it is no longer filling its "
                    + "column and its ellipsis has nothing to trim against.");
            });

        [Fact]
        public Task A_right_aligned_check_cell_moves_the_box() =>
            Realised(() => Aligned(HorizontalAlignment.Right), table =>
                Assert.Equal(HorizontalAlignment.Right, Cell(table, 2).HorizontalAlignment));

        // A TEMPLATE CELL TOO, and this is the one §69.2's default would otherwise have pinned left
        // for ever: the column's instruction has to outrank "do not centre something the caller
        // sized", or a caller could never right-align their own control.
        [Fact]
        public Task A_right_aligned_template_cell_moves_the_callers_control() =>
            Realised(() => Aligned(HorizontalAlignment.Right), table =>
            {
                Control dot = Cell(table, 3);

                Assert.Equal(HorizontalAlignment.Right, dot.HorizontalAlignment);
                Assert.True(dot.Bounds.X > 400,
                    $"the dot is at {dot.Bounds.X:F1}, which is not the right-hand end of its column.");
            });

        [Fact]
        public Task Centre_is_a_third_answer_and_not_a_synonym_for_either_end() =>
            Realised(() => Aligned(HorizontalAlignment.Center), table =>
            {
                Assert.Equal(TextAlignment.Center, ((TextBlock)Cell(table, 1)).TextAlignment);
                Assert.Equal(HorizontalAlignment.Center, Cell(table, 2).HorizontalAlignment);
            });

        // Stretch is the one value with nothing to say to a text cell - filling the column is what a
        // text cell already does, and there is no text alignment that expresses it. It must leave the
        // cell alone rather than picking an end.
        [Fact]
        public Task Stretch_leaves_a_text_cell_exactly_as_it_was() =>
            Realised(() => Aligned(HorizontalAlignment.Stretch), table =>
                Assert.Equal(TextAlignment.Start, ((TextBlock)Cell(table, 1)).TextAlignment));

        [Fact]
        public Task A_column_can_align_down_the_cell_as_well_as_across() =>
            Realised(() => Aligned(null, VerticalAlignment.Top), table =>
                Assert.Equal(VerticalAlignment.Top, Cell(table, 1).VerticalAlignment));

        // ---- the heading follows ----

        // A right-aligned column of sizes under a left-aligned word reads as two different columns.
        // Measured as the LABEL's alignment rather than the button's, because a sortable heading is a
        // Button stretched across the column and moving it would take its hover fill off the column.
        [Fact]
        public Task A_heading_lines_up_with_the_column_it_names() =>
            Realised(() => Aligned(HorizontalAlignment.Right), table =>
            {
                TextBlock label = table.FindNamed<Grid>("PART_Header")
                    .GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Text == "size");

                Assert.Equal(TextAlignment.Right, label.TextAlignment);
            });

        // And an unsortable column's heading is a bare TextBlock rather than a Button, so it takes
        // the same instruction by a different route - a case the sortable one cannot cover.
        [Fact]
        public Task An_unsortable_heading_lines_up_too() => Realised(
            () =>
            {
                var table = new LunaTable<Row> { Key = r => r.Name };
                table.Column(new LunaColumn<Row>("size", r => r.Size.ToString())
                {
                    Width = "120",
                    Alignment = HorizontalAlignment.Right,
                });
                table.Refresh(Rows());
                return table;
            },
            table =>
            {
                TextBlock label = table.FindNamed<Grid>("PART_Header")
                    .GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Text == "size");

                Assert.Empty(table.FindNamed<Grid>("PART_Header").GetVisualDescendants().OfType<Button>());
                Assert.Equal(TextAlignment.Right, label.TextAlignment);
            });

        // ---- sorting without a click ----

        [Fact]
        public Task A_table_starts_unsorted_and_says_so() => Realised(() => Aligned(null), table =>
        {
            Assert.Equal(-1, table.SortedColumn);
            Assert.False(table.SortedDescending);
            Assert.Equal(new[] { "gamma", "alpha", "beta" }, table.Models.Select(r => r.Name));
        });

        [Fact]
        public Task SortBy_orders_the_rows_and_reports_where_it_is() =>
            Realised(() => Aligned(null), table =>
            {
                table.SortBy(1);

                Assert.Equal(1, table.SortedColumn);
                Assert.False(table.SortedDescending);
                Assert.Equal(new[] { "alpha", "beta", "gamma" }, table.Models.Select(r => r.Name));

                table.SortBy(1, descending: true);

                Assert.True(table.SortedDescending);
                Assert.Equal(new[] { "gamma", "beta", "alpha" }, table.Models.Select(r => r.Name));
            });

        [Fact]
        public Task ClearSort_puts_the_rows_back_in_the_order_they_were_given() =>
            Realised(() => Aligned(null), table =>
            {
                table.SortBy(1);
                table.ClearSort();

                Assert.Equal(-1, table.SortedColumn);
                Assert.Equal(new[] { "gamma", "alpha", "beta" }, table.Models.Select(r => r.Name));
            });

        // REFUSED RATHER THAN FALLING BACK TO THE TEXT, which is the assertion that matters. A column
        // with no comparison is one the caller declared unsortable, and sorting it by the projected
        // string would put "10" before "9" - the exact bug §27 made Sort take a comparison to avoid,
        // arriving through a door with no heading to click.
        [Fact]
        public Task SortBy_refuses_a_column_that_declared_no_comparison() =>
            Realised(() => Aligned(null), table =>
            {
                table.SortBy(0);

                Assert.Equal(-1, table.SortedColumn);
                Assert.Equal(new[] { "gamma", "alpha", "beta" }, table.Models.Select(r => r.Name));
            });

        [Fact]
        public Task SortBy_outside_the_columns_does_nothing() => Realised(() => Aligned(null), table =>
        {
            table.SortBy(9);
            table.SortBy(-1);

            Assert.Equal(-1, table.SortedColumn);
        });

        // The glyph is what a user reads to know which way the sort went, and a programmatic sort
        // that ordered the rows without moving it would leave the heading contradicting the table.
        [Fact]
        public Task A_sort_set_in_code_shows_the_same_glyph_a_click_would() =>
            Realised(() => Aligned(null), table =>
            {
                table.SortBy(1, descending: true);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                TextBlock glyph = table.FindNamed<Grid>("PART_Header")
                    .GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Classes.Contains("sort") && t.IsVisible);

                Assert.False(string.IsNullOrWhiteSpace(glyph.Text));
            });
    }
}
