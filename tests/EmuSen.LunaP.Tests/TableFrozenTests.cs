using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
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

            // Settable, because the click guard opens a real editor on a real cell.
            public string Name { get; set; }
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

        // Text columns rather than coloured borders, because the click test needs a cell an editor
        // can open on and the render tests need a cell that paints a flat colour. Two shapes, one
        // question each.
        private static LunaTable<Row> Editable(int frozen)
        {
            var table = new LunaTable<Row> { Key = r => r.Name, FrozenColumns = frozen };

            for (int i = 0; i < 6; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>($"col{n}", r => $"{r.Name}-{n}")
                {
                    Width = "200",
                    Commit = (r, text) => r.Name = text,
                });
            }

            table.Refresh(new[] { new Row("row00"), new Row("row01") });
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
            Scrolled(frozen: 1, offset: 300, (table, _) =>
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
        // "all of them" rather than an exception at layout time. What that then means is worth
        // stating, because the two bounds meet here: freezing EVERY column makes the band the whole
        // width of the table, so it can only leave room when the table already fits - and a table
        // that fits does not scroll. Every other case is refused by §64.1, and what the user gets is
        // an ordinary scrolling table rather than one with columns it cannot reach.
        [Fact]
        public Task Freezing_more_columns_than_exist_is_harmless() =>
            // Scrolled to the end - a ScrollViewer clamps to its maximum - because the property being
            // asserted is that the LAST column can be reached, and at a middle offset it simply has
            // not been reached yet, which is ordinary scrolling rather than a defect.
            Scrolled(frozen: 99, offset: 5000, (table, window) =>
            {
                Assert.All(RowGrid(table).Children.OfType<Control>(), child =>
                {
                    Assert.Null(child.RenderTransform);
                    Assert.Null(child.Clip);
                });

                // And every column is still reachable, which is the property that matters.
                Assert.True(table.TryGetCell(table.Models[0], 5, out Control? last));
                double x = last!.TranslatePoint(new Point(0, 0), window)!.Value.X;
                Assert.True(x is > -1 and < 400, $"the last column is at x={x:F0}.");
            });

        // ---- pass 2: input, focus and what a reader is told ----

        // A CLIP REMOVES A CELL FROM HIT-TESTING, MEASURED RATHER THAN HOPED. This is not obvious and
        // the control depends on it: a scrolling cell whose right-hand part lies under the frozen
        // band is still LAID OUT there, and grid children later in the collection sit above earlier
        // ones - so column 1 covers the frozen column 0 at every point of the band. If the clip did
        // not take it out of hit-testing, every click on a frozen cell would land on an invisible
        // neighbour, and a double-click would open an editor on a cell nobody can see.
        //
        // Driven with real pointer input rather than a hit-test API, because the API probe that went
        // looking first returned no cell at ANY point, including a fully visible one - silence that
        // means nothing (§48.2). What a user does is click, so the test clicks.
        [Fact]
        public Task A_click_in_the_frozen_band_reaches_the_frozen_cell() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Editable(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 300);

            // x=60 is inside the band, and also inside column 1's laid-out rectangle - which spans
            // -88..112 at this offset and is therefore the cell that would be hit if clipping did
            // not count.
            var inBand = new Point(60, 40);
            for (int i = 0; i < 2; i++)
            {
                window.MouseDown(inBand, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(inBand, MouseButton.Left, RawInputModifiers.None);
            }

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.IsEditing, "a double-click inside the frozen band opened nothing at all.");
            Assert.Equal(
                "row00-0",
                table.GetVisualDescendants().OfType<TextBox>().Single().Text);

            window.Close();
        });

        // THE DEFECT PASS 2 EXISTS FOR. A ScrollViewer brings a focused control into view by putting
        // it inside the VIEWPORT, whose left edge is zero - it knows nothing about a band of frozen
        // columns sitting over the first two hundred pixels. Measured before the fix: tabbing to a
        // button in column 1 of a table scrolled to 824 left it focused at x=0 with a clip of zero
        // width, holding the keyboard focus and drawing nothing at all.
        //
        // Sabotaged by removing the GotFocus handler, which puts it straight back under the band.
        [Fact]
        public Task Focus_never_lands_under_the_frozen_band() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 824);

            // Column 1 is the first column that is not frozen, so it is the one BringIntoView parks
            // exactly under the band.
            Assert.True(table.TryGetCell(table.Models[0], 1, out Control? found));
            Control cell = found!;
            Control target = cell.GetVisualDescendants().OfType<Border>().FirstOrDefault() ?? cell;
            target.Focusable = true;

            target.Focus();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            double x = cell.TranslatePoint(new Point(0, 0), window)!.Value.X;
            double bandRight = BandOf(table, window).Right;

            Assert.True(x >= bandRight - 1,
                $"the focused cell sits at x={x:F0}, under a frozen band that runs to {bandRight:F0}.");
            Assert.Null(cell.Clip);

            window.Close();
        });

        // AND FOCUSING A FROZEN CELL MOVES NOTHING. A frozen child sits at a small Bounds.X - column
        // zero is at zero - so the overlap arithmetic that clears a scrolling cell reads, for a
        // frozen one, as "the whole scroll offset plus the band", and the table jumps back to the
        // start the moment a pinned cell takes focus. It is already visible; there is nothing to
        // clear.
        //
        // Sabotaged by dropping the frozen test from the focus walk, which turned NOTHING red until
        // this existed - the other two focus guards only ever focus a scrolling cell.
        [Fact]
        public Task Focusing_a_frozen_cell_does_not_scroll_the_table() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 300);

            ScrollViewer viewer = table.FindNamed<ListBox>("PART_Rows")
                .GetVisualDescendants().OfType<ScrollViewer>().First();
            double before = viewer.Offset.X;

            Assert.True(table.TryGetCell(table.Models[0], 0, out Control? found));
            Control frozen = found!;
            frozen.Focusable = true;
            frozen.Focus();

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(before, viewer.Offset.X, 1);

            window.Close();
        });

        // ---- pass 3: the seam, the gutter and hierarchy ----

        [Fact]
        public Task No_frozen_columns_means_no_seam() =>
            Scrolled(frozen: 0, offset: 0, (table, _) =>
                Assert.DoesNotContain(
                    table.GetVisualDescendants().OfType<Border>(),
                    b => b.Classes.Contains("frozen-edge")));

        // DRAWN BEFORE ANYTHING IS SCROLLED, which is the point of it. A layout that behaves
        // differently on the left should say so before the user discovers it, not after.
        [Fact]
        public Task The_seam_is_drawn_at_rest() =>
            Scrolled(frozen: 1, offset: 0, (table, _) =>
            {
                Assert.Contains(
                    table.FindNamed<Grid>("PART_Header").Children.OfType<Border>(),
                    b => b.Classes.Contains("frozen-edge"));

                Assert.Contains(
                    RowGrid(table).Children.OfType<Border>(),
                    b => b.Classes.Contains("frozen-edge"));
            });

        // AND IT SITS EXACTLY WHERE THE PINNING STOPS. The seam has no positioning code - it is a
        // sibling in the last frozen column, aligned right, so Pin moves it with everything else in
        // there. This asserts the outcome rather than that arrangement: wherever the frozen band
        // ends, that is where the line is, at every offset.
        //
        // TWO COLUMNS AND NOT ONE, which is the difference between a guard and a coincidence. With a
        // single frozen column the last frozen index IS zero, so placing the seam in "column 0"
        // rather than "the last frozen column" is the same instruction and the sabotage that
        // substitutes one for the other turns nothing red. It does at two.
        [Fact]
        public Task The_seam_marks_the_edge_of_the_band_at_every_offset() => UiTest.Run(() =>
        {
            // 700 WIDE, NOT 400, and that is not cosmetic. Two frozen columns of 200 make a band of
            // 400, and a band is refused when it does not leave room (§64.1) - so at 400 wide
            // nothing would be frozen and this would be measuring an ordinary scrolling table, where
            // the seam sits at the same place for a completely different reason.
            LunaTable<Row> table = Striped(frozen: 2);
            var window = new ToolWindow { Width = 700, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            foreach (double offset in new[] { 0.0, 137.0, 300.0, 824.0 })
            {
                ScrollTo(table, offset);

                Border seam = RowGrid(table).Children.OfType<Border>()
                    .First(b => b.Classes.Contains("frozen-edge"));

                // The LAST frozen column, which is what the band ends at.
                Assert.True(table.TryGetCell(table.Models[0], 1, out Control? found));
                Control frozen = found!;

                double seamX = seam.TranslatePoint(new Point(0, 0), window)!.Value.X;
                double bandRight = frozen.TranslatePoint(new Point(0, 0), window)!.Value.X + frozen.Bounds.Width;

                Assert.True(Math.Abs(seamX - (bandRight - seam.Bounds.Width)) < 1.5,
                    $"at offset {offset} the seam is at {seamX:F0} and the band ends at {bandRight:F0}.");
            }

            window.Close();
        });

        // A GUTTER IS FROZEN ON ITS OWN ACCOUNT, with no column frozen beside it - §63. The seam
        // therefore appears for a table that never set FrozenColumns at all, which is the visible
        // half of that decision.
        [Fact]
        public Task A_gutter_alone_is_enough_to_freeze_and_to_draw_a_seam() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 0);
            table.RowHeader = (_, i) => (i + 1).ToString();

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, table.FrozenColumns);
            Assert.Contains(
                RowGrid(table).Children.OfType<Border>(),
                b => b.Classes.Contains("frozen-edge"));

            window.Close();
        });

        // A TREE'S TOGGLE AND INDENT SURVIVE THE SCROLL when the expander column is frozen, which is
        // the case §55's ExpanderColumn exists for meeting §61's pin. Nothing special was written for
        // it: the expander panel is a grid child like any other, so it is pinned by its column.
        [Fact]
        public Task A_frozen_expander_column_keeps_its_toggle_on_screen() => UiTest.Run(() =>
        {
            var kids = new[] { new Row("kid") };
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                FrozenColumns = 1,
                Children = r => r.Name == "row00" ? kids : Array.Empty<Row>(),
                ExpanderColumn = 0,
            };

            for (int i = 0; i < 6; i++)
            {
                int n = i;
                table.Column($"col{n}", r => $"{r.Name}-{n}", "200");
            }

            table.Refresh(new[] { new Row("row00"), new Row("row01") });

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Button toggle = table.GetVisualDescendants().OfType<Button>()
                .First(b => b.Classes.Contains("expander") && b.IsVisible);

            double before = toggle.TranslatePoint(new Point(0, 0), window)!.Value.X;
            ScrollTo(table, 300);
            double after = toggle.TranslatePoint(new Point(0, 0), window)!.Value.X;

            Assert.Equal(before, after, 1);
            Assert.True(after > 0, $"the toggle scrolled off to x={after:F0}.");

            window.Close();
        });

        // FROZEN AFTER THE FACT, which is how an application with a "Freeze first column" menu item
        // would use this - and the case every other test here misses, because they all set
        // FrozenColumns in an object initializer BEFORE the columns are added, and adding a column
        // rebuilds anyway. Sabotaging the rebuild out of the setter turned nothing red until this
        // existed.
        [Fact]
        public Task Freezing_a_column_after_the_table_is_on_screen_works() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 0);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 300);
            Assert.DoesNotContain(
                RowGrid(table).Children.OfType<Border>(),
                b => b.Classes.Contains("frozen-edge"));

            table.FrozenColumns = 1;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.Contains(
                RowGrid(table).Children.OfType<Border>(),
                b => b.Classes.Contains("frozen-edge"));

            Rect band = BandOf(table, window);
            RenderedFrame frame = UiTest.Capture(window);

            Assert.True(Count(frame, band, Frozen) > 1000,
                $"after freezing, the band holds {Count(frame, band, Frozen)} pixels of the frozen column.");
            Assert.Equal(0, Count(frame, band, Scrolling));

            window.Close();
        });

        // ---- pass 4: the interactions and the bounds ----

        // EDITING A COLUMN THAT IS NOT ON SCREEN. Edit is public, F2 goes through it, and a "Rename"
        // menu item is the obvious caller - so the cell being edited need not be anywhere near the
        // viewport. Measured before the fix: Edit(item, 4) on a 400-wide table left a focused editor
        // at x=812 and moved the scroll not at all.
        //
        // The editor's own Focus() cannot do this, which is why BeginEdit scrolls the CELL: a
        // ScrollViewer brings a focused control into view from its arranged bounds, and the editor is
        // created, inserted and focused inside one call, so it has never been laid out and has no
        // bounds to bring anywhere.
        [Fact]
        public Task Editing_an_offscreen_column_brings_it_into_view() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Editable(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 4);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.IsEditing);
            TextBox editor = table.GetVisualDescendants().OfType<TextBox>().Single();

            double x = editor.TranslatePoint(new Point(0, 0), window)!.Value.X;
            double bandRight = BandOf(table, window).Right;

            Assert.True(x >= bandRight - 1 && x < window.Width,
                $"the editor opened at x={x:F0}, outside a viewport of {window.Width:F0} "
                + $"or under a band ending at {bandRight:F0}.");
            Assert.Null(editor.Clip);

            window.Close();
        });

        // AND THE HEADER FOLLOWS A SCROLL IT DID NOT CAUSE. This is §64.2's correction to §59.2 and
        // §64.3's to §62: the offset used to be cached from the ScrollChanged event, which reports a
        // stale number for a BringIntoView, and the viewer used to be taken from that event's Source -
        // which is the editor's OWN inner ScrollViewer the moment a cell is edited, whose offset is
        // always zero.
        //
        // Sabotaged either way: the headings end up hundreds of pixels from their own cells.
        [Fact]
        public Task The_header_follows_a_scroll_caused_by_editing() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Editable(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.Edit(table.Models[0], 4);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Grid header = table.FindNamed<Grid>("PART_Header");
            Control heading = header.Children.OfType<Control>().First(c => c is TextBlock && Grid.GetColumn(c) == 4);

            Assert.True(table.TryGetCell(table.Models[0], 4, out Control? found));
            double dx = Math.Abs(
                heading.TranslatePoint(new Point(0, 0), window)!.Value.X
                - found!.TranslatePoint(new Point(0, 0), window)!.Value.X);

            Assert.True(dx < 1.0, $"after editing scrolled the table, heading 4 is {dx:F1}px from its own cells.");

            window.Close();
        });

        // DRAGGING A FROZEN COLUMN'S WIDTH MOVES THE BAND WITH IT. Nothing was written for this - Pin
        // recomputes the band from the live column widths on every layout - but "it happens to work"
        // and "it is guaranteed to work" are different claims.
        [Fact]
        public Task Resizing_a_frozen_column_moves_the_band_and_the_seam() => UiTest.Run(() =>
        {
            LunaTable<Row> table = Striped(frozen: 1);
            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 300);

            Grid header = table.FindNamed<Grid>("PART_Header");
            header.ColumnDefinitions[0].Width = new GridLength(120);
            table.GetVisualDescendants().OfType<GridSplitter>().First()
                .RaiseEvent(new Avalonia.Input.VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent });
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(table.TryGetCell(table.Models[0], 0, out Control? found));
            Control frozen = found!;

            Assert.Equal(120, frozen.Bounds.Width, 0);

            // Still pinned, at its original x, and the seam still marks its right-hand edge.
            double x = frozen.TranslatePoint(new Point(0, 0), window)!.Value.X;
            Border seam = RowGrid(table).Children.OfType<Border>()
                .First(b => b.Classes.Contains("frozen-edge"));
            double seamX = seam.TranslatePoint(new Point(0, 0), window)!.Value.X;

            Assert.Equal(12, x, 0);
            Assert.True(Math.Abs(seamX - (x + 120 - seam.Bounds.Width)) < 1.5,
                $"the band is {120:F0} wide from x={x:F0} and the seam is at {seamX:F0}.");

            window.Close();
        });

        // ---- bounds ----

        // A BAND AS WIDE AS THE VIEWPORT LEAVES THE OTHER COLUMNS NOWHERE TO BE, and the table is
        // back to §59's defect: columns that exist and cannot be reached by scrollbar, wheel or key.
        // Measured at two frozen columns of 300 in a 400-wide table - every later column clipped to
        // nothing at maximum scroll. Freezing is a refinement of scrolling and does not get to
        // remove it, so a band with no room freezes nothing.
        [Fact]
        public Task A_band_too_wide_for_the_viewport_freezes_nothing() => UiTest.Run(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name, FrozenColumns = 2 };
            for (int i = 0; i < 6; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>(
                    $"col{n}",
                    _ => new Border { Background = new SolidColorBrush(n == 0 ? Frozen : Scrolling), Height = 16 },
                    r => $"{r.Name} {n}") { Width = "300" });
            }

            table.Refresh(new[] { new Row("row00"), new Row("row01") });

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 1424);

            // Nothing pinned, nothing clipped, and the far columns are reachable again.
            Assert.All(RowGrid(table).Children.OfType<Control>(), child =>
            {
                Assert.Null(child.RenderTransform);
                Assert.Null(child.Clip);
            });

            Assert.True(table.TryGetCell(table.Models[0], 5, out Control? last));
            double x = last!.TranslatePoint(new Point(0, 0), window)!.Value.X;
            Assert.True(x is > -1 and < 400, $"the last column is at x={x:F0} and cannot be reached.");

            // And the seam does not claim a boundary that is not being kept.
            Assert.DoesNotContain(
                RowGrid(table).Children.OfType<Border>().Where(b => b.Classes.Contains("frozen-edge")),
                b => b.IsVisible);

            window.Close();
        });

        [Fact]
        public Task A_negative_count_freezes_nothing() =>
            Scrolled(frozen: -3, offset: 300, (table, _) =>
                Assert.All(RowGrid(table).Children.OfType<Control>(), child =>
                {
                    Assert.Null(child.RenderTransform);
                    Assert.Null(child.Clip);
                }));

        // A HIDDEN COLUMN STILL TAKES ONE, because FrozenColumns is counted in columns as they were
        // added - the same rule as every other index this control takes (§27.11, §58.2). It
        // contributes nothing to the band, being pinned to zero width, so freezing two columns of
        // which the first is hidden pins a band exactly one column wide.
        [Fact]
        public Task A_hidden_column_takes_one_of_the_frozen_places() => UiTest.Run(() =>
        {
            var table = new LunaTable<Row> { Key = r => r.Name, FrozenColumns = 2 };
            table.Column(new LunaColumn<Row>(
                "hidden",
                _ => new Border { Background = new SolidColorBrush(Scrolling), Height = 16 },
                _ => "hidden") { Width = "200", IsVisible = false });

            for (int i = 1; i < 6; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>(
                    $"col{n}",
                    _ => new Border { Background = new SolidColorBrush(n == 1 ? Frozen : Scrolling), Height = 16 },
                    r => $"{r.Name} {n}") { Width = "200" });
            }

            table.Refresh(new[] { new Row("row00"), new Row("row01") });

            var window = new ToolWindow { Width = 400, Height = 300, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            ScrollTo(table, 300);

            Rect band = BandOf(table, window);
            RenderedFrame frame = UiTest.Capture(window);

            Assert.True(Count(frame, band, Frozen) > 1000,
                $"the band holds {Count(frame, band, Frozen)} pixels of column 1.");
            Assert.Equal(0, Count(frame, band, Scrolling));

            window.Close();
        });

        private static Grid RowGrid(LunaTable<Row> table) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                .First().GetVisualDescendants().OfType<Grid>().First();
    }
}
