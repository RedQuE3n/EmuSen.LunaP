using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // REORDERING ROWS BY DRAGGING THEM - see docs/LunaP.md §71.
    //
    // The last behavioural row of §54.3. Two things worth knowing before reading the rest:
    //
    //   - THE TABLE DOES NOT REORDER ANYTHING. It reports where the drop landed and the caller
    //     applies it, because this control holds a copy of the caller's list and moving rows in the
    //     copy would be undone by the next Refresh. So most of these tests assert about the DROP that
    //     was reported, and the ones that reorder do it in the handler, the way a consumer would.
    //   - THE GESTURE IS DRIVEN, NOT SIMULATED. Press, move, release, with real positions in the
    //     table's own coordinates. A test that called an internal method would pass for a control
    //     whose handlers were never registered, which is §5.5's shape.
    public class TableDragTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableDragTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, params Row[] kids)
            {
                Name = name;
                Kids = kids;
            }

            public string Name { get; }
            public Row[] Kids { get; }
        }

        private static List<Row> Rows() => new()
        {
            new Row("alpha"), new Row("bravo"), new Row("charlie"), new Row("delta"),
        };

        private static LunaTable<Row> Table(
            IEnumerable<Row>? rows = null,
            bool reorder = true,
            bool tree = false)
        {
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                CanReorderRows = reorder,
            };

            if (tree) table.Children = r => r.Kids;
            table.Column("name", r => r.Name, "200");
            table.Refresh(rows ?? Rows());
            return table;
        }

        private static Task Realised(Func<LunaTable<Row>> make, Action<LunaTable<Row>> assert) =>
            Session.Dispatch(() =>
            {
                LunaTable<Row> table = make();
                var window = new ToolWindow { Width = 400, Height = 400, Content = table };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                assert(table);
                window.Close();
            }, default);

        private static ListBoxItem Container(LunaTable<Row> table, string name) =>
            table.FindNamed<ListBox>("PART_Rows").GetVisualDescendants().OfType<ListBoxItem>()
                .First(c => (c.DataContext as Row)?.Name == name);

        // A point inside one row, `down` of the way through its height - so 0.25 is the top quarter
        // and 0.85 the bottom, which is what decides Before, After and Inside.
        private static Point In(LunaTable<Row> table, string name, double down)
        {
            ListBoxItem container = Container(table, name);
            Point corner = container.TranslatePoint(new Point(0, 0), table)!.Value;
            return new Point(corner.X + 4, corner.Y + (container.Bounds.Height * down));
        }

        // Press, move, release - the whole gesture, in the table's coordinates. The move happens
        // twice because the first one only crosses the drag threshold.
        private static void Drag(LunaTable<Row> table, string from, Point to, double grab = 0.5)
        {
            ListBoxItem source = Container(table, from);
            var pointer = new Avalonia.Input.Pointer(0, PointerType.Mouse, true);

            source.RaiseEvent(new PointerPressedEventArgs(
                source, pointer, table, In(table, from, grab), 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));

            table.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent, table, pointer, table, to, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                KeyModifiers.None));

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            table.RaiseEvent(new PointerReleasedEventArgs(
                table, pointer, table, to, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None, MouseButton.Left));

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        private static string Report(LunaRowDrop<Row> drop) =>
            $"{string.Join("+", drop.Rows.Select(r => r.Name))} {drop.Position} {drop.Target?.Name ?? "end"}";

        // ---- off by default ----

        [Fact]
        public Task A_table_does_not_reorder_until_it_is_told_to() =>
            Realised(() => Table(reorder: false), table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                Drag(table, "alpha", In(table, "charlie", 0.8));

                Assert.Empty(heard);
            });

        // ---- the drop, reported ----

        [Fact]
        public Task Dropping_below_a_row_reports_after_that_row() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "alpha", In(table, "charlie", 0.8));

            Assert.Equal(new[] { "alpha After charlie" }, heard);
        });

        [Fact]
        public Task Dropping_above_a_row_reports_before_that_row() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "delta", In(table, "bravo", 0.2));

            Assert.Equal(new[] { "delta Before bravo" }, heard);
        });

        // PAST THE LAST ROW IS A REAL DROP, and it is reported with no target rather than by
        // inventing the last row as one - "put it at the end" is what the user did.
        [Fact]
        public Task Dropping_past_the_last_row_reports_the_end() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "alpha", new Point(20, 380));

            Assert.Equal(new[] { "alpha After end" }, heard);
        });

        // A CLICK IS NOT A DRAG, and the fixture has to cross a row boundary to prove it.
        //
        // The first version released on the same row it pressed, which reports nothing whether or not
        // there is a threshold - a row cannot be dropped on itself, so the self-drop rule was doing
        // all the work and sabotaging the threshold turned nothing red. A press near the bottom of
        // one row that wobbles two pixels into the next is the case the threshold exists for: a user
        // selecting a row with a slightly unsteady hand, who must not reorder the table by doing it.
        [Fact]
        public Task A_click_that_wobbles_into_the_next_row_reorders_nothing() =>
            Realised(() => Table(), table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                Point edge = In(table, "alpha", 0.95);

                Assert.True(
                    Container(table, "bravo").TranslatePoint(new Point(0, 0), table)!.Value.Y < edge.Y + 2,
                    "the fixture does not cross into the next row, so it cannot test the threshold.");

                Drag(table, "alpha", new Point(edge.X, edge.Y + 2), grab: 0.95);

                Assert.Empty(heard);
            });

        // And four pixels the other way is a drag, or the threshold would just be a way of refusing
        // short drags.
        [Fact]
        public Task A_press_that_moves_far_enough_is_a_drag() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "alpha", In(table, "bravo", 0.8));

            Assert.Equal(new[] { "alpha After bravo" }, heard);
        });

        // Dropping a row onto itself would report a move to where it already is, and a caller acting
        // on it would remove and reinsert a row for nothing.
        [Fact]
        public Task A_row_cannot_be_dropped_on_itself() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "bravo", In(table, "bravo", 0.9));

            Assert.Empty(heard);
        });

        // ---- what the caller does with it ----

        // THE TABLE REORDERS NOTHING BY ITSELF, which is the design rather than an omission: it holds
        // a copy of the caller's list, so moving rows here would be undone by the next Refresh.
        [Fact]
        public Task The_table_does_not_move_the_rows_itself() => Realised(() => Table(), table =>
        {
            Drag(table, "alpha", In(table, "charlie", 0.8));

            Assert.Equal(
                new[] { "alpha", "bravo", "charlie", "delta" },
                table.Models.Select(r => r.Name));
        });

        // And the handler a consumer actually writes: reorder your own list, call Refresh.
        [Fact]
        public Task A_caller_that_applies_the_drop_gets_the_new_order() => Session.Dispatch(() =>
        {
            List<Row> rows = Rows();
            LunaTable<Row> table = Table(rows);

            table.RowDropped += drop =>
            {
                foreach (Row moved in drop.Rows) rows.Remove(moved);

                int at = drop.Target is null ? rows.Count : rows.FindIndex(r => r == drop.Target);
                if (drop.Position == LunaDropPosition.After) at++;

                rows.InsertRange(Math.Clamp(at, 0, rows.Count), drop.Rows);
                table.Refresh(rows);
            };

            var window = new ToolWindow { Width = 400, Height = 400, Content = table };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Drag(table, "alpha", In(table, "charlie", 0.8));

            Assert.Equal(
                new[] { "bravo", "charlie", "alpha", "delta" },
                table.Models.Select(r => r.Name));

            window.Close();
        }, default);

        // ---- the veto ----

        [Fact]
        public Task A_refused_drop_is_never_reported() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                table.CanDrop = drop => drop.Target?.Name != "charlie";
                return table;
            },
            table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                Drag(table, "alpha", In(table, "charlie", 0.8));
                Assert.Empty(heard);

                Drag(table, "alpha", In(table, "delta", 0.8));
                Assert.Equal(new[] { "alpha After delta" }, heard);
            });

        // THE INDICATOR AND THE DROP READ THE SAME ANSWER, so a refused drop cannot draw a line
        // promising something the release will decline.
        [Fact]
        public Task A_refused_drop_draws_no_line() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                table.CanDrop = _ => false;
                return table;
            },
            table =>
            {
                Hold(table, "alpha", In(table, "charlie", 0.8));

                Assert.Empty(Lines(table));
            });

        // ---- what the user sees mid-drag ----

        [Fact]
        public Task A_drag_in_progress_draws_a_line_where_the_row_would_land() =>
            Realised(() => Table(), table =>
            {
                Hold(table, "alpha", In(table, "charlie", 0.8));

                Control line = Assert.Single(Lines(table));

                Assert.Equal(VerticalAlignment.Bottom, line.VerticalAlignment);
                Assert.Same(Container(table, "charlie"), line.GetVisualAncestors().OfType<ListBoxItem>().First());
            });

        [Fact]
        public Task The_line_goes_when_the_drag_ends() => Realised(() => Table(), table =>
        {
            Drag(table, "alpha", In(table, "charlie", 0.8));

            Assert.Empty(Lines(table));
        });

        // ---- a tree ----

        // A FLAT TABLE HAS NO "INSIDE", so the row splits in half and every position is a reorder.
        // Offering Inside where nothing can act on it would be an indicator promising a reparent.
        [Fact]
        public Task A_flat_table_never_reports_a_drop_inside_a_row() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            Drag(table, "alpha", In(table, "charlie", 0.5));

            Assert.Equal(new[] { "alpha After charlie" }, heard);
        });

        // And a tree does, because there it means something a caller can perform: reparent.
        [Fact]
        public Task A_tree_reports_a_drop_into_the_middle_of_a_row_as_a_reparent() => Realised(
            () => Table(new[] { new Row("roms", new Row("smw")), new Row("saves") }, tree: true),
            table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                Drag(table, "saves", In(table, "roms", 0.5));

                Assert.Equal(new[] { "saves Inside roms" }, heard);
            });

        // ---- the keyboard, which §24 makes a requirement and not a nicety ----

        [Fact]
        public Task Alt_down_moves_the_selected_row_without_a_pointer() =>
            Realised(() => Table(), table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                table.Select(table.Models[0]);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Key(table, Avalonia.Input.Key.Down, KeyModifiers.Alt);

                Assert.Equal(new[] { "alpha After bravo" }, heard);
            });

        [Fact]
        public Task Alt_up_moves_the_selected_row_the_other_way() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            table.Select(table.Models[2]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Key(table, Avalonia.Input.Key.Up, KeyModifiers.Alt);

            Assert.Equal(new[] { "charlie Before bravo" }, heard);
        });

        // At the ends there is nowhere to go, and the key must report nothing rather than a move to
        // where the row already is.
        [Fact]
        public Task Alt_up_on_the_first_row_reports_nothing() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            table.Select(table.Models[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Key(table, Avalonia.Input.Key.Up, KeyModifiers.Alt);

            Assert.Empty(heard);
        });

        // PLAIN Up STILL MOVES THE SELECTION, or turning reordering on would take the arrow keys away
        // from every user who was using them to walk the list.
        [Fact]
        public Task Alt_is_required_and_a_bare_arrow_is_left_alone() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.RowDropped += drop => heard.Add(Report(drop));

            table.Select(table.Models[1]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Key(table, Avalonia.Input.Key.Down, KeyModifiers.None);

            Assert.Empty(heard);
        });

        [Fact]
        public Task The_keyboard_move_obeys_the_same_veto() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                table.CanDrop = _ => false;
                return table;
            },
            table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                table.Select(table.Models[0]);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Key(table, Avalonia.Input.Key.Down, KeyModifiers.Alt);

                Assert.Empty(heard);
            });

        // ---- a multi-selection travels together ----

        [Fact]
        public Task Dragging_a_row_inside_a_selection_takes_the_whole_selection() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                table.SelectionMode = LunaSelectionMode.Multiple;
                return table;
            },
            table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                ListBox rows = table.FindNamed<ListBox>("PART_Rows");
                rows.SelectedItems!.Clear();
                rows.SelectedItems.Add(table.Models[0]);
                rows.SelectedItems.Add(table.Models[1]);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Drag(table, "alpha", In(table, "delta", 0.8));

                Assert.Equal(new[] { "alpha+bravo After delta" }, heard);
            });

        // And a row OUTSIDE the selection travels alone, or a user dragging one row would move three
        // they had stopped pointing at.
        [Fact]
        public Task Dragging_a_row_outside_the_selection_takes_only_that_row() => Realised(
            () =>
            {
                LunaTable<Row> table = Table();
                table.SelectionMode = LunaSelectionMode.Multiple;
                return table;
            },
            table =>
            {
                var heard = new List<string>();
                table.RowDropped += drop => heard.Add(Report(drop));

                ListBox rows = table.FindNamed<ListBox>("PART_Rows");
                rows.SelectedItems!.Clear();
                rows.SelectedItems.Add(table.Models[0]);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Drag(table, "charlie", In(table, "delta", 0.8));

                Assert.Equal(new[] { "charlie After delta" }, heard);
            });

        // ---- helpers ----

        // The gesture up to the release, so a test can look at what is drawn mid-drag.
        private static void Hold(LunaTable<Row> table, string from, Point to)
        {
            ListBoxItem source = Container(table, from);
            var pointer = new Avalonia.Input.Pointer(0, PointerType.Mouse, true);

            source.RaiseEvent(new PointerPressedEventArgs(
                source, pointer, table, In(table, from, 0.5), 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));

            table.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent, table, pointer, table, to, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
                KeyModifiers.None));

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        private static Control[] Lines(LunaTable<Row> table) =>
            table.GetVisualDescendants().OfType<Control>()
                .Where(c => c.Classes.Contains("drop-line") || c.Classes.Contains("drop-into"))
                .ToArray();

        private static void Key(LunaTable<Row> table, Avalonia.Input.Key key, KeyModifiers modifiers)
        {
            table.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                KeyModifiers = modifiers,
            });

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
    }
}
