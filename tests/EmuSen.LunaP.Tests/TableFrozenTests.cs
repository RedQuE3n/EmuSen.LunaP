using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // FROZEN COLUMNS - see docs/LunaP.md §61, and §60 for the correction that made them possible
    // after §59.3 had concluded they were not.
    //
    // THE GUARD HERE IS A RENDER AND NOT A PROPERTY READ, and that is the whole point of the file.
    // Asserting that a RenderTransform was set and a Clip was assigned is the §5.5 shape - an
    // assertion about wiring that passes while the effect is absent - and this feature is exactly the
    // kind where wiring and effect can part company: a clip with the wrong sign, a transform applied
    // to the wrong parent, or a band measured in the wrong coordinate space all leave every property
    // set and the frozen column somewhere else.
    //
    // So the columns are drawn in flat colours through the public Template kind (§57) and the pixels
    // inside the band are counted. What must be true is not "a clip exists" but "no pixel of the
    // scrolled column is inside the frozen band", and only a render can say that.
    public class TableFrozenTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableFrozenTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name) => Name = name;
            public string Name { get; }
        }

        private static readonly Color Frozen = Colors.Red;
        private static readonly Color Scrolling = Colors.Blue;

        // Six columns of 200 in a 400-wide table: 1200 wanted, and the first one is the one pinned.
        // The cells are Template columns filled with a flat colour, which is a shape a caller could
        // write - nothing here reaches into the control to paint anything.
        private static LunaTable<Row> Striped(int frozen, int rows = 2)
        {
            var table = new LunaTable<Row> { Key = r => r.Name, FrozenColumns = frozen };

            for (int i = 0; i < 6; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>(
                    $"col{n}",
                    // A HEIGHT, because a Border with no child has no desired size and six of them
                    // give a row of no height at all - which is how the first version of this file
                    // counted zero pixels of every colour and looked like a broken feature.
                    _ => new Border
                    {
                        Background = new SolidColorBrush(n == 0 ? Frozen : Scrolling),
                        Height = 16,
                    },
                    r => $"{r.Name} {n}")
                {
                    Width = "200",
                });
            }

            table.Refresh(Enumerable.Range(0, rows).Select(i => new Row($"row{i:D2}")).ToArray());
            return table;
        }

        // THE STRIP THE FROZEN COLUMN OCCUPIES, DERIVED AND NOT WRITTEN DOWN. The first version of
        // this hard-coded a rectangle and was simply wrong about where the rows start, which counted
        // zero of both colours and would have reported a working feature as broken.
        //
        // Measured from the control instead: the rows viewport says where the band begins, and any
        // realised cell says where a row sits vertically and how tall it is. Inset a little on every
        // side, so the assertion is about the middle of the band and not about its edges, where
        // antialiasing and a one-pixel rule are somebody else's argument.
        private static Rect BandOf(LunaTable<Row> table, Window window)
        {
            ListBox rows = table.FindNamed<ListBox>("PART_Rows");
            double left = rows.TranslatePoint(new Point(0, 0), window)!.Value.X;

            // A row that is FULLY INSIDE the viewport, which is not the same as a realised one: a
            // list scrolled to an arbitrary offset realises a row that is half above the top edge,
            // and a band measured on that row is mostly outside the rows area and counts the window
            // behind it. That cost one confusing red - 196 pixels where thousands were expected -
            // and the answer is to measure a whole row rather than to lower the threshold.
            double top = rows.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            double bottom = top + rows.Bounds.Height;

            var model = (Row)rows.GetVisualDescendants().OfType<ListBoxItem>()
                .Where(c => c.DataContext is Row)
                .First(c =>
                {
                    double y = c.TranslatePoint(new Point(0, 0), window)!.Value.Y;
                    return y >= top && y + c.Bounds.Height <= bottom;
                })
                .DataContext!;

            // The last column, because it is realised whatever the horizontal offset is.
            Assert.True(table.TryGetCell(model, 5, out Control? found));
            Control any = found!;
            Point at = any.TranslatePoint(new Point(0, 0), window)!.Value;

            // 12 is the row padding the theme gives a ListBoxItem, so the cells begin there and the
            // 200-wide frozen column runs to 212.
            return new Rect(left + 14, at.Y + 3, 196, any.Bounds.Height - 6);
        }

        private static int Count(RenderedFrame frame, Rect area, Color colour)
        {
            int found = 0;
            int x0 = Math.Max(0, (int)area.X), x1 = Math.Min(frame.Width, (int)area.Right);
            int y0 = Math.Max(0, (int)area.Y), y1 = Math.Min(frame.Height, (int)area.Bottom);

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    int i = ((y * frame.Width) + x) * 4;
                    if (frame.Rgba[i] == colour.R && frame.Rgba[i + 1] == colour.G && frame.Rgba[i + 2] == colour.B)
                    {
                        found++;
                    }
                }
            }

            return found;
        }

        private static void ScrollTo(LunaTable<Row> table, double x)
        {
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ScrollViewer>()
                .First().Offset = new Vector(x, 0);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        private static Task Scrolled(int frozen, double offset, Action<LunaTable<Row>, Window> assert) =>
            UiTest.Run(() =>
            {
                LunaTable<Row> table = Striped(frozen);
                var window = new ToolWindow { Width = 400, Height = 300, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                ScrollTo(table, offset);
                assert(table, window);
                window.Close();
            });

        // ---- the render, which is the whole guard ----

        // THE CONTROL CASE, and it is here so the one below cannot pass by accident. With nothing
        // frozen, a table scrolled to 300 shows the SCROLLING colour in that strip - which is both
        // what §59 delivered and the thing frozen columns has to change. If this ever goes red the
        // measurement itself has moved and the test below means nothing.
        [Fact]
        public Task Scrolled_with_nothing_frozen_the_band_holds_the_scrolling_column() =>
            Scrolled(frozen: 0, offset: 300, (table, window) =>
            {
                Rect band = BandOf(table, window);
                RenderedFrame frame = UiTest.Capture(window);

                Assert.True(Count(frame, band, Scrolling) > 1000,
                    $"expected the scrolled column to fill the band; found {Count(frame, band, Scrolling)} pixels of it.");
                Assert.Equal(0, Count(frame, band, Frozen));
            });

        // AND THE FEATURE. Zero pixels of the scrolling colour is the assertion that matters: the
        // neighbour is REMOVED from the band rather than covered by the frozen cell, which is what
        // makes a backdrop unnecessary and leaves Fluent's row fill showing through untouched (§60.1).
        [Fact]
        public Task Scrolled_with_one_column_frozen_the_band_holds_only_that_column() =>
            Scrolled(frozen: 1, offset: 300, (table, window) =>
            {
                Rect band = BandOf(table, window);
                RenderedFrame frame = UiTest.Capture(window);

                Assert.True(Count(frame, band, Frozen) > 1000,
                    $"expected the frozen column to fill the band; found {Count(frame, band, Frozen)} pixels of it.");
                Assert.Equal(0, Count(frame, band, Scrolling));
            });

        // The scrolling region is still scrolling. A "frozen" table that simply stopped moving would
        // pass the test above and be useless, so this pins that the far columns still arrive.
        [Fact]
        public Task Freezing_a_column_does_not_stop_the_others_scrolling() =>
            Scrolled(frozen: 1, offset: 300, (table, _) =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 2, out Control? third));

                double x = third!.TranslatePoint(new Point(0, 0), table)?.X ?? double.NaN;
                Assert.True(x is > 100 and < 300,
                    $"column 2 should have scrolled into view beside the frozen column; it is at x={x:F0}.");
            });

        // A ROW REALISED WHILE ALREADY SCROLLED SIDEWAYS. This is the case the LayoutUpdated hook
        // exists for and nothing else covers: the rows this pins were built by the virtualising panel
        // long after the horizontal scroll happened, so the scroll handler had already run and will
        // not run again. Without the hook they arrive unpinned - a frozen column that is frozen only
        // for the rows that happened to be on screen when you scrolled.
        //
        // Sabotaged by removing the hook, which turned NOTHING red until this existed.
        [Fact]
        public Task Rows_realised_after_the_scroll_are_pinned_too() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 1, rows: 60);
            table.Height = 90;

            var window = new ToolWindow { Width = 400, Height = 140, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollViewer viewer = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ScrollViewer>().First();

            // Sideways first, so every row on screen is pinned by the scroll handler...
            viewer.Offset = new Vector(300, 0);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // ...then downwards, which throws those rows away and builds new ones.
            viewer.Offset = new Vector(300, 400);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Rect band = BandOf(table, window);
            RenderedFrame frame = UiTest.Capture(window);

            Assert.True(Count(frame, band, Frozen) > 200,
                $"a row realised after the scroll shows {Count(frame, band, Frozen)} pixels of the frozen column.");
            Assert.Equal(0, Count(frame, band, Scrolling));

            window.Close();
        });

        // ---- the geometry, as a second opinion on the render ----

        [Fact]
        public Task A_frozen_column_ends_up_where_it_started() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.TryGetCell(table.Models[0], 0, out Control? found));
            Control first = found!;
            double before = first.TranslatePoint(new Point(0, 0), table)!.Value.X;

            foreach (double offset in new[] { 137.0, 300.0, 824.0 })
            {
                ScrollTo(table, offset);
                double now = first.TranslatePoint(new Point(0, 0), table)!.Value.X;

                Assert.True(Math.Abs(now - before) < 1.0,
                    $"at offset {offset} the frozen column sits at x={now:F0}, having started at {before:F0}.");
            }

            window.Close();
        });

        // The heading has to be pinned with its cells, or a frozen column ends up under somebody
        // else's name - which is the same alignment argument §59.2 makes for the header as a whole,
        // one level down.
        [Fact]
        public Task A_frozen_heading_stays_over_its_own_cells() =>
            Scrolled(frozen: 1, offset: 300, (table, _) =>
            {
                Grid header = table.FindNamed<Grid>("PART_Header");
                Control heading = header.Children.OfType<Control>().First(c => Grid.GetColumn(c) == 0);

                Assert.True(table.TryGetCell(table.Models[0], 0, out Control? cell));

                double dx = Math.Abs(
                    (heading.TranslatePoint(new Point(0, 0), table)?.X ?? 0)
                    - (cell!.TranslatePoint(new Point(0, 0), table)?.X ?? 0));

                Assert.True(dx < 1.0, $"the frozen heading is {dx:F1}px from its own cells.");
            });

        // ---- off by default, and reversible ----

        [Fact]
        public void A_table_freezes_nothing_unless_it_is_asked_to() =>
            Assert.Equal(0, new LunaTable<Row>().FrozenColumns);

        [Fact]
        public Task Nothing_is_transformed_or_clipped_when_no_column_is_frozen() =>
            Scrolled(frozen: 0, offset: 300, (table, _) =>
            {
                Grid row = RowGrid(table);

                Assert.All(row.Children.OfType<Control>(), child =>
                {
                    Assert.Null(child.RenderTransform);
                    Assert.Null(child.Clip);
                });
            });

        // TURNING IT OFF HAS TO CLEAR WHAT IT SET, which is the reason Pin keeps a flag rather than
        // returning on a zero count. A table that dropped back to zero and kept its transforms would
        // have one column pinned to nothing.
        [Fact]
        public Task Unfreezing_puts_everything_back() =>
            Scrolled(frozen: 2, offset: 300, (table, _) =>
            {
                Assert.Contains(RowGrid(table).Children.OfType<Control>(), c => c.RenderTransform is not null);

                table.FrozenColumns = 0;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.All(RowGrid(table).Children.OfType<Control>(), child =>
                {
                    Assert.Null(child.RenderTransform);
                    Assert.Null(child.Clip);
                });
            });

        // Freezing more columns than there are is a harmless thing to say, and the honest reading is
        // "all of them" - which leaves nothing to scroll rather than throwing at layout time.
        [Fact]
        public Task Freezing_more_columns_than_exist_freezes_all_of_them() =>
            Scrolled(frozen: 99, offset: 300, (table, window) =>
            {
                Rect band = BandOf(table, window);
                RenderedFrame frame = UiTest.Capture(window);

                Assert.True(Count(frame, band, Frozen) > 1000);
                Assert.Equal(0, Count(frame, band, Scrolling));
            });

        private static Grid RowGrid(LunaTable<Row> table) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                .First().GetVisualDescendants().OfType<Grid>().First();
    }
}
