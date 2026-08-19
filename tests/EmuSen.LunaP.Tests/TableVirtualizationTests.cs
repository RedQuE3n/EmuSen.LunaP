using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // COLUMN VIRTUALIZATION - see docs/LunaP.md §72.
    //
    // THE SUBJECT HERE IS WHAT DOES NOT EXIST, which is a different kind of assertion from the rest
    // of the table's suite and needs a different kind of care. "Column 90 has no cell" passes just as
    // happily when the table is empty, when the row never realized, when the column index is wrong,
    // and when the feature works - so every guard below pairs the absence with a presence in the same
    // row: these columns are here AND those are not, counted from one grid.
    //
    // The other trap this file is built around: a table that scrolls nowhere virtualizes nothing.
    // Every fixture is deliberately wider than its viewport, and the ones that scroll assert the
    // range actually moved before asserting anything about what is in it.
    public class TableVirtualizationTests
    {
        private sealed class Row
        {
            public Row(string name) => Name = name;

            // Settable, because the editor guard opens a real editor on a real cell.
            public string Name { get; set; }
        }

        private static readonly Color Ink = Colors.Red;

        // Thirty columns of 120 in an 800-wide window: 3,600 wanted against roughly 776 of viewport,
        // so about six columns can be seen and twenty-four cannot. Wide enough that a range of nine
        // is unmistakably narrower than thirty, and small enough that a failure prints a readable
        // list of column indices.
        private static LunaTable<Row> Wide(bool virtualized, int columns = 30, string width = "120")
        {
            var table = new LunaTable<Row> { Key = r => r.Name, VirtualizeColumns = virtualized };

            for (int i = 0; i < columns; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>($"col{n}", r => $"{r.Name}-{n}")
                {
                    Width = width,
                    Commit = (r, text) => r.Name = text,
                });
            }

            table.Refresh(Enumerable.Range(0, 40).Select(i => new Row($"row{i:D2}")).ToArray());
            return table;
        }

        private static Grid RowGrid(LunaTable<Row> table) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => c.DataContext is Row)
                .GetVisualDescendants().OfType<Grid>().First();

        // The columns a row actually holds a cell for, ASKED THROUGH TryGetCell rather than read off
        // the internal marker. Two reasons, and the second is the better one: the marker is internal
        // and this project's tests see only what a consumer sees (§32), and TryGetCell's answer IS
        // the documented consequence of virtualizing a column - so a guard written in terms of it
        // fails if the feature works and the public answer does not follow.
        private static IReadOnlyList<int> Held(LunaTable<Row> table, int columns = 30)
        {
            var row = (Row)RowGrid(table).GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;

            var found = new List<int>();
            for (int i = 0; i < columns; i++)
            {
                if (table.TryGetCell(row, i, out _)) found.Add(i);
            }

            return found;
        }

        private static ScrollViewer Viewer(LunaTable<Row> table) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ScrollViewer>()
                .First(s => !s.GetVisualAncestors().OfType<ListBoxItem>().Any());

        private static void ScrollTo(LunaTable<Row> table, double x)
        {
            Viewer(table).Offset = new Vector(x, 0);
            Settle(table);
        }

        // The twice-deliberately loop moved into the harness at §79.6, because a consumer building a
        // control that fills during layout needs it and could not reach it here. UiTest.Settle
        // carries the argument now; this stays as the name the fixtures below already call.
        private static void Settle(LunaTable<Row> table) => UiTest.Settle(table);

        // A FACTORY AND NOT A TABLE, because a control built on the test thread and shown on the UI
        // thread throws on the first child added to it - "the calling thread cannot access this
        // object". Every fixture here is therefore built inside the run.
        private static Task Shown(Func<LunaTable<Row>> make, Action<LunaTable<Row>, Window> assert) =>
            UiTest.Run(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = 800, Height = 300, Content = table };
                window.Show();
                Settle(table);

                assert(table, window);
                window.Close();
            });

        // ---- what is built, and what is not ----

        // THE CONTROL CASE, and the file is worthless without it. A table that has not asked for this
        // builds all thirty columns however far off screen they are, which is what every version
        // before §72 did and what §26.13 promises a 0.8.0 table upgrading to 0.9.0.
        [Fact]
        public Task A_table_that_does_not_ask_builds_every_column() =>
            Shown(() => Wide(virtualized: false), (table, window) =>
            {
                Assert.Equal(Enumerable.Range(0, 30), Held(table));
            });

        // AND THE FEATURE. Both halves in one assertion: the columns at the left edge are here, the
        // ones far to the right are not, and the count is read from the same grid - so a row that
        // never realized fails this rather than passing it.
        [Fact]
        public Task A_virtualizing_table_builds_only_the_columns_it_can_show() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                IReadOnlyList<int> held = Held(table);

                Assert.True(held.Count is > 2 and < 12,
                    $"expected a handful of columns near the viewport; got {held.Count}: {string.Join(",", held)}");
                Assert.Contains(0, held);
                Assert.DoesNotContain(29, held);
            });

        // The range FOLLOWS the viewport, which is the half a static "build the first few" would also
        // pass. Asserted as a swap rather than as a membership: what was held at rest is gone, and
        // what was out of reach is here.
        [Fact]
        public Task Scrolling_sideways_builds_the_columns_that_arrive_and_drops_the_ones_that_leave() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                IReadOnlyList<int> before = Held(table);
                ScrollTo(table, 2400);
                IReadOnlyList<int> after = Held(table);

                // The fixture really moved, before anything is concluded from the fact that it did.
                Assert.True(Viewer(table).Offset.X > 2000,
                    $"the fixture did not scroll; offset is {Viewer(table).Offset.X}");

                Assert.Contains(20, after);
                Assert.DoesNotContain(20, before);
                Assert.DoesNotContain(0, after);
                Assert.Contains(0, before);
            });

        // Turning it off is not merely "stop leaving columns out" - the cells that were left out have
        // to come BACK, in rows that are already on screen. A version that only checked the flag on
        // the next rebuild would leave a scrolled table permanently missing its left-hand columns.
        [Fact]
        public Task Turning_it_off_puts_every_cell_back() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                ScrollTo(table, 2400);
                Assert.DoesNotContain(0, Held(table));

                table.VirtualizeColumns = false;
                Settle(table);

                Assert.Equal(Enumerable.Range(0, 30), Held(table));
            });

        // ---- the columns that are never left out ----

        // A frozen column is on screen at every offset - that is what frozen means - so leaving it
        // out could only ever be wrong. Paired with a column that IS dropped at the same offset, or
        // this passes on a table that virtualized nothing at all.
        [Fact]
        public Task A_frozen_column_is_built_however_far_the_table_is_scrolled() =>
            Shown(
                () =>
                {
                    LunaTable<Row> table = Wide(virtualized: true);
                    table.FrozenColumns = 2;
                    return table;
                },
                (t, window) =>
            {
                ScrollTo(t, 2400);
                IReadOnlyList<int> held = Held(t);

                Assert.Contains(0, held);
                Assert.Contains(1, held);
                Assert.DoesNotContain(5, held);
            });

        // AN AUTO COLUMN IS BUILT WHATEVER THE VIEWPORT SAYS, and the two guards below say why it has
        // to be. This one only says that it is.
        [Fact]
        public Task An_auto_column_is_built_however_far_the_table_is_scrolled() =>
            Shown(Mixed, (table, window) =>
            {
                ScrollTo(table, 2400);
                IReadOnlyList<int> held = Held(table);

                Assert.Contains(0, held);
                Assert.DoesNotContain(5, held);
            });

        // THE REASON THE CLAUSE ABOVE EXISTS, AND THE FIRST VERSION OF THIS GUARD COULD NOT FAIL.
        //
        // It scrolled past the Auto column and read its width back, which is the obvious test and
        // measures nothing: a shared size group does not shrink when its largest contributor is
        // removed from a grid that already exists. Measured on Avalonia 12.1.0 with the Auto clause
        // deleted - 158 pixels before the scroll, 158 after, in the header and in the rows, and 158
        // again after forcing a re-measure. The guard was green on a broken control.
        //
        // WHAT BREAKS IT IS THE NEXT REBUILD. Refresh builds new row grids, and the group is then
        // computed from members that no longer include a cell for that column: the same fixture went
        // to 24 - the bare heading - and STAYED at 24 after scrolling back, with its cells crushed
        // into it. So the refresh is the whole guard, and a table that is never refreshed while
        // scrolled cannot show the defect at all. §72.3.
        [Fact]
        public Task An_auto_column_survives_a_refresh_that_happens_while_it_is_scrolled_past() =>
            Shown(Mixed, (table, window) =>
            {
                double before = RowGrid(table).ColumnDefinitions[0].ActualWidth;
                Assert.True(before > 100,
                    $"the fixture's Auto column measured {before}, which is too narrow for a collapse to show.");

                ScrollTo(table, 2400);
                Assert.DoesNotContain(5, Held(table));

                // The tick a polling window would take here, and the reason the scroll alone is not
                // enough (§27.11 and §21 are the windows this matters to).
                table.Refresh(Enumerable.Range(0, 40).Select(i => new Row($"row{i:D2}")).ToArray());
                Settle(table);

                double after = RowGrid(table).ColumnDefinitions[0].ActualWidth;
                Assert.True(Math.Abs(before - after) < 2,
                    $"the Auto column was {before} wide and is {after} after a refresh while scrolled past it.");
            });

        // A STAR COLUMN IS THE SAME RULE AND A LOUDER FAILURE, and it needs no refresh to show it. A
        // row is measured against no width limit, so a star column there takes its content's size
        // rather than a share of the viewport - and loses it the instant the cells go. Measured with
        // this clause removed: 175 pixels at rest, 0 while scrolled past, and the table's extent
        // moving 3,679 to 3,504 - which slides every column to its right 175 pixels sideways while
        // the user is dragging the scrollbar. §72.3.
        [Fact]
        public Task A_star_column_does_not_collapse_when_the_table_is_scrolled_past_it() =>
            Shown(Starred, (table, window) =>
            {
                double before = RowGrid(table).ColumnDefinitions[5].ActualWidth;
                double extent = Viewer(table).Extent.Width;
                Assert.True(before > 100, $"the fixture's star column measured {before}, too narrow for a collapse to show.");

                ScrollTo(table, 2000);
                Assert.DoesNotContain(12, Held(table));

                Assert.Equal(before, RowGrid(table).ColumnDefinitions[5].ActualWidth, 1);
                Assert.Equal(extent, Viewer(table).Extent.Width, 1);
            });

        // Column 0 is Auto and holds a string long enough to be wider than its heading, so its width
        // is decided by the CELLS rather than by the header - which is the only arrangement in which
        // dropping cells could change it. A fixture whose Auto column is sized by its heading would
        // pass the guard above with the feature completely broken.
        private static LunaTable<Row> Mixed()
        {
            var table = new LunaTable<Row> { Key = r => r.Name, VirtualizeColumns = true };

            table.Column(new LunaColumn<Row>("c", r => $"{r.Name} a rather long value") { Width = "Auto" });

            for (int i = 1; i < 30; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>($"col{n}", r => $"{r.Name}-{n}") { Width = "120" });
            }

            table.Refresh(Enumerable.Range(0, 40).Select(i => new Row($"row{i:D2}")).ToArray());
            return table;
        }

        // The gutter is not a cell and carries no column marker (§58), so it is exactly the kind of
        // child a fill that removed things by Grid.Column would take out by accident - and a user
        // scrolled sideways would lose the one thing telling them which row they are reading.
        [Fact]
        public Task The_gutter_survives_a_sideways_scroll() =>
            Shown(
                () =>
                {
                    LunaTable<Row> table = Wide(virtualized: true);
                    table.RowHeader = (_, index) => $"{index}";
                    return table;
                },
                (t, window) =>
            {
                ScrollTo(t, 2400);

                Assert.DoesNotContain(0, Held(t));
                Assert.Contains(RowGrid(t).Children.OfType<Control>(), c => c.Classes.Contains("row-header"));
            });

        // ---- what still has to work ----

        // A COLUMN THAT WAS NEVER BUILT CAN STILL BE EDITED. Without ShowColumn, Edit finds no cell,
        // returns, and renames nothing - which is §64.4's defect arriving by a new road: an edit
        // refused because of where the viewport happens to be. §72.4.
        [Fact]
        public Task An_editor_opens_on_a_column_that_was_left_out() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                var row = (Row)RowGrid(table).GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;

                Assert.DoesNotContain(25, Held(table));

                table.Edit(row, 25);
                Assert.True(table.IsEditing, "Edit found no cell for a column the viewport had not reached.");
            });

        // AND THE EDITED COLUMN IS NOT TORN OUT FROM UNDER THE CARET. An editor is a child of the row
        // grid with no owner marker, so the fill leaves it alone - but its CELL has one, and removing
        // that while the editor sat in its place would leave a caret in a column with nothing behind
        // it, which is the shape of the defect §55.7 records.
        //
        // What keeps it is that Edit asked for the column on the way in and nothing takes that back;
        // there is no separate clause for the caret, because one was written and could not be made
        // to fail. §72.6.
        [Fact]
        public Task An_open_editor_survives_a_scroll_that_would_have_dropped_its_column() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                var row = (Row)RowGrid(table).GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;

                table.Edit(row, 2);
                Assert.True(table.IsEditing);

                ScrollTo(table, 2400);

                Assert.True(table.IsEditing, "the editor went away when its column scrolled off.");
                Assert.Contains(2, Held(table));
            });

        // WALKING RIGHT OFF THE EDGE OF THE RANGE. The arrow key selects the cell either way; what
        // virtualization can break is the BringIntoView after it, which needs a visual that does not
        // exist yet. Twelve presses is well past the six columns that started on screen.
        [Fact]
        public Task The_arrow_keys_walk_past_the_columns_that_were_built() =>
            Shown(
                () =>
                {
                    LunaTable<Row> table = Wide(virtualized: true);
                    table.SelectionUnit = LunaSelectionUnit.Cell;
                    return table;
                },
                (t, window) =>
            {
                var row = (Row)RowGrid(t).GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;
                t.SelectCell(row, 0);

                for (int i = 0; i < 12; i++)
                {
                    t.RaiseEvent(new KeyEventArgs
                    {
                        RoutedEvent = InputElement.KeyDownEvent,
                        Key = Key.Right,
                    });
                }

                Settle(t);

                Assert.Equal(12, t.SelectedCell!.Value.Column);
                Assert.True(t.TryGetCell(row, 12, out _), "the selected cell had no visual to scroll to.");
                Assert.True(Viewer(t).Offset.X > 0, "walking right past the viewport did not scroll the table.");
            });

        // THE ROW'S SPOKEN SENTENCE IS NOT VIRTUALIZED. Spoken builds it from the column specs rather
        // than from the cells that happen to exist, so a screen reader hears the whole row whatever
        // the viewport is showing - and if that ever changes, a reader would hear a row's contents
        // change as somebody else scrolled.
        [Fact]
        public Task A_row_is_still_spoken_in_full_when_most_of_it_is_not_built() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                ListBoxItem container = table.FindNamed<ListBox>("PART_Rows")
                    .GetVisualDescendants().OfType<ListBoxItem>().First(c => c.DataContext is Row);

                string? spoken = AutomationProperties.GetName(container);

                Assert.DoesNotContain(29, Held(table));
                Assert.Contains("col29:", spoken);
                Assert.Contains("col0:", spoken);
            });

        // A vertical rule is a child of the row grid that is not a cell, and it belongs to a column
        // just as much as the cell does. Left behind by a fill, a table scrolled far to the right
        // would accumulate one rule per column it had ever passed.
        [Fact]
        public Task A_columns_rule_leaves_with_its_cell() =>
            Shown(
                () =>
                {
                    LunaTable<Row> table = Wide(virtualized: true);
                    table.GridLines = LunaGridLines.Vertical;
                    return table;
                },
                (t, window) =>
            {
                ScrollTo(t, 2400);

                int rules = RowGrid(t).Children.OfType<Control>().Count(c => c.Classes.Contains("column-rule"));

                Assert.True(rules <= Held(t).Count,
                    $"{rules} rules against {Held(t).Count} cells - rules are being left behind.");
                Assert.True(rules > 0, "no rules at all, so this guard is not testing anything.");
            });

        // NOT GUARDED, AND SAID SO RATHER THAN LEFT LOOKING GUARDED. Row() consults the range as it
        // builds, so a row realized while scrolling comes up narrow instead of being built whole and
        // trimmed - which is most of the cost this feature exists to avoid, in a table scrolled down
        // through hundreds of rows.
        //
        // No assertion here holds it, and a test was written and deleted rather than kept: the fill
        // runs from LayoutUpdated, so by the time any layout pass has finished the row is narrow
        // EITHER WAY, and the guard passed with the line removed. What differs is work done, and
        // nothing this file can see counts it. §72.5 records it as a hazard, which is what an
        // untested claim is in this project.

        // NO GAP AT EITHER EDGE OF THE VIEWPORT, DERIVED FROM WHERE THE CELLS ACTUALLY ARE.
        //
        // Every other guard here reads the range in the same terms the range was computed in, so a
        // range that is consistently wrong reads as consistently right. This one takes the arranged
        // bounds of the cells that exist and asks whether they cover what the user is looking at,
        // which is the question the range is an answer to rather than a restatement of it.
        //
        // WHAT THIS DOES NOT PIN, said plainly. The range is measured from a ROW's definitions and
        // not from the header's, because the header is laid out at the viewport's width and a row at
        // its own - measured on Avalonia 12.1.0, one "*" among twenty-nine fixed columns resolved to
        // 0 in the header and 175 in the rows. Swapping the source is still green here and in every
        // other guard in the file: a mis-measured range comes out WIDER rather than displaced, and
        // the Auto and star columns are realized unconditionally anyway, so they plug the gaps it
        // would otherwise leave. It costs cells rather than correctness, and that is recorded as a
        // hazard rather than dressed up as a guard. §72.7.
        [Fact]
        public Task Every_column_the_user_can_see_has_a_cell() =>
            Shown(Starred, (table, window) =>
            {
                foreach (double offset in new double[] { 0, 700, 1500, 2300 })
                {
                    ScrollTo(table, offset);

                    Grid grid = RowGrid(table);
                    var row = (Row)grid.GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;

                    List<Rect> cells = Enumerable.Range(0, 30)
                        .Where(i => table.TryGetCell(row, i, out _))
                        .Select(i => { table.TryGetCell(row, i, out Control? c); return c!.Bounds; })
                        .Where(b => b.Width > 0)
                        .ToList();

                    Assert.True(cells.Count > 0, $"nothing was built at offset {offset}.");

                    double seen = Viewer(table).Offset.X;
                    double wide = Viewer(table).Viewport.Width;
                    double left = cells.Min(b => b.X);
                    double right = cells.Max(b => b.Right);

                    // CLAMPED TO THE ROW'S OWN WIDTH, which is not fussiness. At maximum scroll the
                    // viewport reaches 24 pixels past the last cell - the row padding the theme puts
                    // on both ends (§27.10) - so an unclamped expectation fails on a correct control
                    // by exactly that much, which is how this was found.
                    double wanted = Math.Min(seen + wide, grid.Bounds.Width);

                    Assert.True(left <= seen + 2,
                        $"at offset {seen} the leftmost built cell starts at {left}, leaving a gap on the left.");
                    Assert.True(right >= wanted - 2,
                        $"at offset {seen} the rightmost built cell ends at {right}, short of {wanted}.");
                }
            });

        // Absolute columns with a star every fifth, which is what makes the header's geometry and a
        // row's disagree - see the guard above. SIX OF THEM AND NOT ONE, because the error is per
        // star column and the range carries a column of slack either side: a single star is 54 pixels
        // adrift and a 120-wide slack column swallows it whole, so a one-star fixture reports a
        // working control whichever grid the range is measured from.
        private static LunaTable<Row> Starred()
        {
            var table = new LunaTable<Row> { Key = r => r.Name, VirtualizeColumns = true };

            for (int i = 0; i < 30; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>(
                    $"col{n}",
                    // The starred columns carry a longer value, because a star column in an
                    // overflowing row is as wide as its content - so a short one could not show a
                    // collapse and could not shift the columns after it either.
                    r => n % 5 == 0 ? $"{r.Name} a long starred value" : $"{r.Name}-{n}")
                { Width = n % 5 == 0 ? "*" : "120" });
            }

            table.Refresh(Enumerable.Range(0, 20).Select(i => new Row($"row{i:D2}")).ToArray());
            return table;
        }

        // ---- the two that cannot be inferred from properties ----

        // LAYOUT HAS TO SETTLE. The fill runs from LayoutUpdated and adds children, which invalidates
        // layout and brings it straight back - so the whole feature rests on it being a fixpoint. If
        // realizing a cell could change a column's width, it would not be, and the control would
        // spin for as long as it was on screen. This is what the Auto clause in Realized protects,
        // and nothing else in the file would notice it going.
        //
        // COUNTING LAYOUT PASSES DOES NOT ASK THIS, and the first version of this guard did exactly
        // that and could not fail: LayoutUpdated fires once per UpdateLayout whether or not anything
        // was dirty, so ten calls report ten passes on a table doing nothing at all. What settles or
        // does not is the CHILDREN, so the mutations are counted instead - and a fill that re-added
        // the same cells forever would show up here while a pass count stayed flat.
        //
        // Both halves, because "zero mutations" is also what a broken feature that never fills
        // reports: the scroll has to move some, and then the quiet has to be quiet.
        [Fact]
        public Task The_fill_settles_instead_of_rebuilding_the_row_forever() =>
            Shown(() => Wide(virtualized: true), (table, window) =>
            {
                int changes = 0;
                void Count(object? sender, NotifyCollectionChangedEventArgs e) => changes++;

                RowGrid(table).Children.CollectionChanged += Count;
                ScrollTo(table, 2400);
                RowGrid(table).Children.CollectionChanged -= Count;

                Assert.True(changes > 0, "the scroll changed no children, so the quiet below proves nothing.");

                changes = 0;
                RowGrid(table).Children.CollectionChanged += Count;

                for (int i = 0; i < 10; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    table.UpdateLayout();
                }

                RowGrid(table).Children.CollectionChanged -= Count;

                Assert.Equal(0, changes);
            });

        // AND A RENDER, because every property this file reads can be right while nothing is drawn.
        // A cell built into a grid is not a cell the user can see: it can be clipped by the frozen
        // band (§61), or inserted after a selection box and cover it, or arrive with no bounds and
        // stay that way. Only pixels say otherwise.
        //
        // The middle column of the visible three is drawn in a flat colour and counted where it
        // should be after a scroll of exactly two columns - so the assertion is about a column that
        // was NOT built when the window opened.
        [Fact]
        public Task A_column_that_scrolled_into_view_is_actually_drawn() =>
            Shown(Painted, (t, window) =>
            {
                Assert.DoesNotContain(20, Held(t));

                ScrollTo(t, 2400);
                Assert.Contains(20, Held(t));

                RenderedFrame frame = UiTest.Capture(window);
                int found = Count(frame, Ink);

                // Eight rows of a 120-wide, 16-tall cell is 15,360 pixels at most; anything in the
                // thousands is the column drawn rather than a stray edge.
                Assert.True(found > 2000,
                    $"column 20 scrolled into view and drew {found} pixels of its colour.");
            });

        private static LunaTable<Row> Painted()
        {
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                VirtualizeColumns = true,
                SelectionUnit = LunaSelectionUnit.Cell,
            };

            for (int i = 0; i < 30; i++)
            {
                int n = i;
                table.Column(new LunaColumn<Row>(
                    $"col{n}",
                    // A HEIGHT, because a Border with no child has no desired size, and thirty of
                    // them give a row of no height and a count of zero pixels of everything. Opaque
                    // in every column, so the draw-order guard has something that would hide an
                    // outline drawn underneath it.
                    _ => new Border { Background = new SolidColorBrush(n == 20 ? Ink : Colors.DarkSlateGray), Height = 16 },
                    r => $"{r.Name} {n}")
                {
                    Width = "120",
                });
            }

            table.Refresh(Enumerable.Range(0, 8).Select(i => new Row($"row{i:D2}")).ToArray());
            return table;
        }

        // A CELL THAT ARRIVES BY A SCROLL GOES UNDER THE BOX MARKING IT SELECTED, NOT OVER IT.
        //
        // A Panel draws its children in the order it holds them. Row() appends cells and then puts
        // the overlays in, so the order is right by construction - but the fill runs LATER than the
        // box does, because MarkCells puts a box on a selected cell of a realized row whether or not
        // that column has ever been built. Append there instead of inserting at the front and the
        // arriving cell covers the outline, and the user scrolls back to a cell that is selected and
        // does not look it.
        //
        // A RENDER, because this is a claim about draw order and nothing readable off a property
        // says it. The cell is painted opaque on purpose: a TextBlock would let the outline show
        // through whichever order they were in, which is a fixture that cannot tell the two apart.
        // The outline's own brush is read off the box rather than named here, so the count follows
        // the theme (§12.2) instead of restating it. §72.2.
        [Fact]
        public Task A_cell_arriving_by_a_scroll_is_drawn_under_the_box_marking_it_selected() =>
            Shown(Painted, (table, window) =>
            {
                var row = (Row)RowGrid(table).GetVisualAncestors().OfType<ListBoxItem>().First().DataContext!;
                table.SelectCell(row, 20);

                ScrollTo(table, 2400);
                Assert.True(table.TryGetCell(row, 20, out Control? cell), "column 20 never arrived.");

                Border box = RowGrid(table).Children.OfType<Border>()
                    .First(c => c.Classes.Contains("cell-selection"));
                var outline = ((ISolidColorBrush)box.BorderBrush!).Color;

                // The cell's own rectangle, grown by two so the one-pixel outline around its edge is
                // inside the area being counted.
                Control found20 = cell!;
                Point at = found20.TranslatePoint(new Point(0, 0), window)!.Value;
                var area = new Rect(at.X - 2, at.Y - 2, found20.Bounds.Width + 4, found20.Bounds.Height + 4);

                RenderedFrame frame = UiTest.Capture(window);
                int found = Count(frame, outline, area);

                Assert.True(found > 40,
                    $"the selected cell's outline drew {found} pixels; the cell is covering it.");
            });

        private static int Count(RenderedFrame frame, Color colour, Rect area)
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

        private static int Count(RenderedFrame frame, Color colour)
        {
            int found = 0;
            for (int i = 0; i < frame.Rgba.Length; i += 4)
            {
                if (frame.Rgba[i] == colour.R && frame.Rgba[i + 1] == colour.G && frame.Rgba[i + 2] == colour.B)
                {
                    found++;
                }
            }

            return found;
        }
    }
}
