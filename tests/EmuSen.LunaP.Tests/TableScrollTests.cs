using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // SCROLLING SIDEWAYS - see docs/LunaP.md §59.
    //
    // The defect this closes was measured before it was fixed: six columns of 200 in a 400-wide table
    // resolved all six definitions to 200 and clipped the grid at 376, with the ScrollViewer
    // reporting extent equal to viewport. Columns past the right edge were unreachable by scrollbar,
    // wheel or keyboard - not merely awkward to reach, but absent from every way a user has of
    // getting at them.
    //
    // The thing that can go wrong now is ALIGNMENT. The rows scroll inside the ListBox's own
    // ScrollViewer and the header is the one part of the control outside it, so the two are only in
    // step because LunaTable moves the header by hand. Most of this file measures a heading against
    // its own cells rather than measuring the scroll.
    public class TableScrollTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableScrollTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name) => Name = name;
            public string Name { get; }
        }

        private static Row[] Rows() => new[] { new Row("alpha"), new Row("bravo") };

        // Six columns of 200 in a 400-wide window: 1200 wanted, 376 available.
        private static LunaTable<Row> Wide(Row[] rows)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            for (int i = 0; i < 6; i++)
            {
                int n = i;
                table.Column($"col{n}", r => $"{r.Name}-{n}", "200");
            }

            table.Refresh(rows);
            return table;
        }

        private static LunaTable<Row> Fitting(Row[] rows)
        {
            var table = new LunaTable<Row> { Key = r => r.Name };
            table.Column("a", r => r.Name, "2*").Column("b", _ => "x", "*").Column("c", _ => "y", "Auto");
            table.Refresh(rows);
            return table;
        }

        private static Task Realised(Func<LunaTable<Row>> make, double width, Action<LunaTable<Row>> assert) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = width, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static ScrollViewer Viewer(LunaTable<Row> table) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ScrollViewer>().First();

        private static void ScrollTo(LunaTable<Row> table, double x)
        {
            Viewer(table).Offset = new Vector(x, 0);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // Where something is, in the table's own coordinates - which is what "lined up" means to
        // somebody looking at it, and is the only frame in which the header and a row are comparable
        // at all now that one of them lives inside a ScrollViewer.
        private static double X(LunaTable<Row> table, Visual v) =>
            v.TranslatePoint(new Point(0, 0), table)?.X ?? double.NaN;

        // ---- the defect ----

        [Fact]
        public Task Columns_past_the_edge_can_be_scrolled_to() => Realised(() => Wide(Rows()), 400, table =>
        {
            ScrollViewer viewer = Viewer(table);

            Assert.True(viewer.Extent.Width > viewer.Viewport.Width,
                $"extent {viewer.Extent.Width} is not wider than viewport {viewer.Viewport.Width} - "
                + "the columns are being squeezed rather than scrolled.");

            // The row grid lays out at its natural width rather than being clipped to the viewport,
            // which is the half of the defect that made the far columns unreachable.
            Grid row = table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                .First().GetVisualDescendants().OfType<Grid>().First();

            Assert.Equal(1200, row.Bounds.Width, 0);
        });

        [Fact]
        public Task A_horizontal_scrollbar_appears_only_when_it_is_needed()
        {
            return Session.Dispatch(() =>
            {
                Assert.True(Bar(Wide(Rows()), 400), "a table wider than its viewport has no horizontal scrollbar.");
                Assert.False(Bar(Fitting(Rows()), 600), "a table that fits grew a horizontal scrollbar anyway.");
            }, default);

            static bool Bar(LunaTable<Row> table, double width)
            {
                var window = new ToolWindow { Width = width, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                bool visible = table.GetVisualDescendants().OfType<ScrollBar>()
                    .Any(b => b.Orientation == Orientation.Horizontal && b.IsVisible);

                window.Close();
                return visible;
            }
        }

        // ---- the alignment, which is what the fix can break ----

        // THE GUARD THAT MATTERS. A heading has to sit over its own cells, and once the rows scroll
        // inside a ScrollViewer and the header does not, that is only true because LunaTable moves
        // the header itself. Sabotaged by removing the ScrollChanged handler: the headings stay put
        // while the cells slide out from under them, and every column here goes 300 pixels out.
        [Fact]
        public Task Every_heading_stays_over_its_own_cells_while_scrolling() =>
            Realised(() => Wide(Rows()), 400, table =>
            {
                foreach (double offset in new[] { 0.0, 137.0, 300.0, 824.0 })
                {
                    ScrollTo(table, offset);

                    Grid header = table.FindNamed<Grid>("PART_Header");
                    for (int i = 0; i < 6; i++)
                    {
                        Control heading = header.Children.OfType<Control>()
                            .First(c => c is TextBlock && Grid.GetColumn(c) == i);

                        Assert.True(table.TryGetCell(table.Models[0], i, out Control? cell));

                        double dx = Math.Abs(X(table, heading) - X(table, cell!));
                        Assert.True(dx < 1.0,
                            $"at offset {offset}, heading {i} is {dx:F1}px from its own cells.");
                    }
                }
            });

        [Fact]
        public Task The_header_moves_by_exactly_what_the_rows_moved() =>
            Realised(() => Wide(Rows()), 400, table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Assert.Null(header.RenderTransform);

                ScrollTo(table, 300);
                Assert.Equal(-300, ((TranslateTransform)header.RenderTransform!).X, 1);

                ScrollTo(table, 0);
                Assert.Equal(0, ((TranslateTransform)header.RenderTransform!).X, 1);
            });

        // ---- what did not change ----

        // §27.10's failure mode, re-measured under scrolling. A shared size group makes a STAR column
        // behave as Auto, and an unconstrained measure could do the same thing by a different route -
        // so this asserts the outcome that section cares about: the table fills its own width, and
        // the header and rows resolve every column identically.
        [Fact]
        public Task A_table_that_fits_still_fills_its_width_and_stays_aligned() =>
            Realised(() => Fitting(Rows()), 600, table =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Grid row = table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                    .First().GetVisualDescendants().OfType<Grid>().First();

                double total = header.ColumnDefinitions.Sum(d => d.ActualWidth);
                Assert.Equal(header.Bounds.Width, total, 0);
                Assert.True(total > 500, $"three star columns resolved to {total:F1} in a 600-wide window.");

                for (int i = 0; i < header.ColumnDefinitions.Count; i++)
                {
                    Assert.Equal(
                        header.ColumnDefinitions[i].ActualWidth,
                        row.ColumnDefinitions[i].ActualWidth,
                        1);
                }
            });

        [Fact]
        public Task A_table_that_fits_never_scrolls_sideways() =>
            Realised(() => Fitting(Rows()), 600, table =>
            {
                ScrollViewer viewer = Viewer(table);
                Assert.Equal(viewer.Viewport.Width, viewer.Extent.Width, 0);
            });

        // A KNOWN CONSEQUENCE, PINNED SO IT IS A DECISION RATHER THAN A SURPRISE. The gutter lives in
        // the row grid, so it scrolls away with everything else - it is the natural first FROZEN
        // column, and §59.3 records why freezing anything is a pass of its own rather than a line
        // here. If this ever starts failing it is because that work landed, and this test should be
        // rewritten rather than deleted.
        [Fact]
        public Task The_gutter_scrolls_away_with_the_rest_because_nothing_is_frozen_yet() => Realised(
            () =>
            {
                LunaTable<Row> table = Wide(Rows());
                table.RowHeader = (_, i) => (i + 1).ToString();
                return table;
            },
            400,
            table =>
            {
                TextBlock gutter = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>().First()
                    .GetVisualDescendants().OfType<TextBlock>()
                    .First(t => t.Classes.Contains("row-header"));

                double before = X(table, gutter);
                ScrollTo(table, 300);

                Assert.Equal(before - 300, X(table, gutter), 1);
            });
    }
}
