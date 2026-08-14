using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // CELL SELECTION - see docs/LunaP.md §67.
    //
    // The last of §54.3's big rows. Two things are worth knowing before reading the rest, because
    // both are ways this can look finished and be wrong:
    //
    //   - A SELECTION NOTHING PAINTS. The set of selected cells is held by key, so every property
    //     on the control can answer correctly while the user sees nothing at all - §5.5's shape,
    //     which this codebase has been caught by often enough to name. Most of these tests read the
    //     BOX out of the row's visual tree rather than asking the table what it thinks.
    //   - A GUARD THAT ONLY WORKS WITH SEVERAL ROWS. ContainerPrepared hands over a container that
    //     is not yet in GetRealizedContainers - measured, listed=False for every row - so a sweep of
    //     the realised ones marks each row when the NEXT one is prepared, and the last row never.
    //     With three rows a test asking about row 1 passes either way; only a one-row table tells
    //     the two implementations apart. §67.5.
    public class TableCellSelectionTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TableCellSelectionTests).GetTypeInfo().Assembly);

        private sealed class Row
        {
            public Row(string name, int size)
            {
                Name = name;
                Size = size;
            }

            public string Name { get; set; }
            public int Size { get; }
        }

        private static Row[] Rows() => new[]
        {
            new Row("alpha", 1), new Row("bravo", 2), new Row("charlie", 3),
        };

        private static LunaTable<Row> Table(
            LunaSelectionMode mode = LunaSelectionMode.Single,
            LunaSelectionUnit unit = LunaSelectionUnit.Cell,
            bool hideMiddle = false,
            Row[]? rows = null)
        {
            var table = new LunaTable<Row>
            {
                Key = r => r.Name,
                SelectionMode = mode,
                SelectionUnit = unit,
            };

            table.Column(new LunaColumn<Row>("name", r => r.Name)
                  {
                      Width = "120",
                      Commit = (r, text) => r.Name = text,
                  })
                 .Column(new LunaColumn<Row>("size", r => r.Size.ToString())
                  {
                      Width = "80",
                      IsVisible = !hideMiddle,
                  })
                 .Column("tag", _ => "x", "60");

            table.Refresh(rows ?? Rows());
            return table;
        }

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

        // WHERE THE BOXES ACTUALLY ARE, read out of the rows rather than off the control. Returns
        // "row:gridColumn" pairs so an assertion reads as the picture somebody would describe.
        private static string[] Boxes(LunaTable<Row> table)
        {
            var found = new List<string>();

            foreach (ListBoxItem container in table.FindNamed<ListBox>("PART_Rows")
                         .GetVisualDescendants().OfType<ListBoxItem>())
            {
                if (container.DataContext is not Row row) continue;

                foreach (Control box in container.GetVisualDescendants().OfType<Control>()
                             .Where(c => c.Classes.Contains("cell-selection")))
                {
                    found.Add($"{row.Name}:{Grid.GetColumn(box)}");
                }
            }

            found.Sort(StringComparer.Ordinal);
            return found.ToArray();
        }

        private static void Key(LunaTable<Row> table, Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            table.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                KeyModifiers = modifiers,
            });

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        private static string Coordinate(LunaTable<Row> table) =>
            table.SelectedCell is { } at ? $"{at.Row.Name}:{at.Column}" : "none";

        // ---- nothing changes for a table that does not ask ----

        // §26.13, which every item in this arc is held to. A table that never names SelectionUnit
        // selects rows, draws no boxes, and does not answer the cell questions.
        [Fact]
        public Task A_table_selects_rows_until_it_is_told_otherwise() =>
            Realised(() => Table(unit: LunaSelectionUnit.Row), table =>
            {
                Assert.Equal(LunaSelectionUnit.Row, table.SelectionUnit);

                table.SelectCell(table.Models[0], 1);

                Assert.Null(table.SelectedCell);
                Assert.Empty(table.SelectedCells);
                Assert.Empty(Boxes(table));
            });

        // WHETHER THIS CONTROL TOOK THE KEY, ASKED WITH NOTHING ELSE IN THE ROOM. A table inside a
        // window cannot answer it: Avalonia's own directional focus navigation handles an arrow key
        // on the way up, so e.Handled comes back True whatever LunaTable did - measured, and it made
        // the first draft of the edge test below pass without any code behind it. Raising the event
        // on a parentless table leaves exactly one handler in the chain, which is the one under test.
        //
        // No template is needed for either question. Both are decided in OnKeyDown from the column
        // list and the current cell, and both of those exist before there is a control tree (§27.6).
        private static bool TookTheKey(LunaSelectionUnit unit, Action<LunaTable<Row>> setUp, Key key)
        {
            var table = new LunaTable<Row> { Key = r => r.Name, SelectionUnit = unit };
            table.Column("name", r => r.Name, "120")
                 .Column("size", r => r.Size.ToString(), "80")
                 .Column("tag", _ => "x", "60");
            table.Refresh(Rows());
            setUp(table);

            var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
            table.RaiseEvent(e);
            return e.Handled;
        }

        // The arrows stay the ListBox's in a row unit, or a table that had never heard of cell
        // selection would start eating keys its user expects to move the row.
        [Fact]
        public Task Arrow_keys_are_not_taken_over_in_a_row_unit() => Session.Dispatch(() =>
        {
            Assert.False(
                TookTheKey(LunaSelectionUnit.Row, table => table.Select(table.Models[1]), Avalonia.Input.Key.Right),
                "a row-unit table took an arrow key, which is the ListBox's to move the row with.");
        }, default);

        // ---- the selection, seen ----

        [Fact]
        public Task Selecting_a_cell_draws_a_box_on_it() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 2);

            Assert.Equal("bravo:2", Coordinate(table));
            Assert.Equal(new[] { "bravo:2" }, Boxes(table));
            Assert.True(table.IsCellSelected(table.Models[1], 2));
            Assert.False(table.IsCellSelected(table.Models[1], 1));
        });

        // THE ONE-ROW TABLE, and it is not a degenerate case being thorough - it is the only fixture
        // that can fail when the marking sweeps the realised containers instead of taking the one
        // ContainerPrepared just handed over. With three rows every row but the last is marked when
        // its successor arrives, so the bug hides behind the fixture. §67.5.
        [Fact]
        public Task The_only_row_in_a_table_still_gets_its_box() =>
            Realised(() => Table(rows: new[] { new Row("solo", 9) }), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Assert.Equal(new[] { "solo:0" }, Boxes(table));
            });

        [Fact]
        public Task Selecting_another_cell_moves_the_box() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 0);
            table.SelectCell(table.Models[2], 2);

            Assert.Equal(new[] { "charlie:2" }, Boxes(table));
        });

        [Fact]
        public Task Clearing_takes_the_box_off() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 0);
            table.ClearCellSelection();

            Assert.Null(table.SelectedCell);
            Assert.Empty(Boxes(table));
        });

        // The row under the current cell follows it, which is what keeps Selected, Chose and the
        // vertical scroll working in this unit without a second implementation of any of them.
        [Fact]
        public Task The_row_under_the_current_cell_is_the_selected_row() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[2], 1);

            Assert.Same(table.Models[2], table.Selected);
        });

        // ---- the keyboard ----

        [Fact]
        public Task Right_and_left_walk_the_columns() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 0);

            Key(table, Avalonia.Input.Key.Right);
            Assert.Equal("alpha:1", Coordinate(table));

            Key(table, Avalonia.Input.Key.Right);
            Assert.Equal("alpha:2", Coordinate(table));

            Key(table, Avalonia.Input.Key.Left);
            Assert.Equal("alpha:1", Coordinate(table));

            Assert.Equal(new[] { "alpha:1" }, Boxes(table));
        });

        // AT THE EDGE THE KEY IS STILL EATEN, and that is the half worth a test of its own. Letting
        // Right through on the last column hands it to the framework's directional focus navigation,
        // which moves focus out of the table - so walking one column too far would leave the control.
        //
        // Asked away from a window, for the reason TookTheKey carries: inside one, Avalonia handles
        // the arrow itself and this assertion is true no matter what LunaTable does.
        [Fact]
        public Task At_the_last_column_right_does_nothing_and_keeps_the_key() => Session.Dispatch(() =>
        {
            Assert.True(
                TookTheKey(LunaSelectionUnit.Cell, table => table.SelectCell(table.Models[0], 2), Avalonia.Input.Key.Right),
                "Right on the last column was passed on, and the focus will leave the table with it.");
        }, default);

        // And the cell does not move, which is the other half of "nothing happens".
        [Fact]
        public Task At_the_last_column_right_leaves_the_cell_where_it_is() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 2);

            Key(table, Avalonia.Input.Key.Right);

            Assert.Equal("alpha:2", Coordinate(table));
        });

        [Fact]
        public Task Home_and_End_go_to_the_ends_of_the_row() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 1);

            Key(table, Avalonia.Input.Key.End);
            Assert.Equal("bravo:2", Coordinate(table));

            Key(table, Avalonia.Input.Key.Home);
            Assert.Equal("bravo:0", Coordinate(table));
        });

        // A hidden column keeps its INDEX (§54.3) but must not be landed on: a selection nobody can
        // see is one the user cannot act on, and the arrow key would look like it had done nothing.
        [Fact]
        public Task A_hidden_column_is_stepped_over_rather_than_landed_on() =>
            Realised(() => Table(hideMiddle: true), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Key(table, Avalonia.Input.Key.Right);

                Assert.Equal("alpha:2", Coordinate(table));
            });

        // ---- ranges, which need the mode as well as the unit ----

        [Fact]
        public Task Shift_right_extends_a_range_across_the_row() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Key(table, Avalonia.Input.Key.Right, KeyModifiers.Shift);

                Assert.Equal(new[] { "alpha:0", "alpha:1" }, Boxes(table));
                Assert.Equal("alpha:1", Coordinate(table));
            });

        // A RECTANGLE AND NOT A READING ORDER. Shift from (row 0, column 0) to (row 1, column 1) is
        // four cells, not three - a spreadsheet's Shift has never meant "everything from here to
        // there along the rows", and a caller acting on the selection would get a cell the user
        // never saw highlighted.
        [Fact]
        public Task Shift_down_extends_a_rectangle_and_not_a_run() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Key(table, Avalonia.Input.Key.Right, KeyModifiers.Shift);
                Key(table, Avalonia.Input.Key.Down, KeyModifiers.Shift);

                Assert.Equal(
                    new[] { "alpha:0", "alpha:1", "bravo:0", "bravo:1" },
                    Boxes(table));
            });

        // The range shrinks again on the way back, which is what the anchor is for: a Shift that
        // only ever grew would make one overshoot unrecoverable without starting again.
        [Fact]
        public Task A_range_shrinks_when_the_far_end_comes_back() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Key(table, Avalonia.Input.Key.Right, KeyModifiers.Shift);
                Key(table, Avalonia.Input.Key.Right, KeyModifiers.Shift);
                Assert.Equal(3, table.SelectedCells.Count);

                Key(table, Avalonia.Input.Key.Left, KeyModifiers.Shift);
                Assert.Equal(new[] { "alpha:0", "alpha:1" }, Boxes(table));
            });

        // Shift in a SINGLE-selection table moves rather than extends. The mode is what says how
        // many, and a unit cannot overrule it - otherwise "Single" would mean single until somebody
        // held a key down.
        [Fact]
        public Task Shift_without_the_multiple_mode_just_moves() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[0], 0);

            Key(table, Avalonia.Input.Key.Right, KeyModifiers.Shift);

            Assert.Equal(new[] { "alpha:1" }, Boxes(table));
        });

        // ---- the pointer ----

        [Fact]
        public Task Clicking_a_cell_selects_it() => Realised(() => Table(), table =>
        {
            Assert.True(table.TryGetCell(table.Models[1], 2, out Control? cell));

            Click(table, cell!);

            Assert.Equal("bravo:2", Coordinate(table));
        });

        [Fact]
        public Task Ctrl_clicking_adds_a_cell_and_clicking_it_again_takes_it_back() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                Assert.True(table.TryGetCell(table.Models[0], 0, out Control? first));
                Assert.True(table.TryGetCell(table.Models[2], 2, out Control? second));

                Click(table, first!);
                Click(table, second!, KeyModifiers.Control);

                Assert.Equal(new[] { "alpha:0", "charlie:2" }, Boxes(table));

                Click(table, second!, KeyModifiers.Control);

                Assert.Equal(new[] { "alpha:0" }, Boxes(table));
            });

        private static void Click(LunaTable<Row> table, Control cell, KeyModifiers modifiers = KeyModifiers.None)
        {
            cell.RaiseEvent(new PointerPressedEventArgs(
                cell,
                new Avalonia.Input.Pointer(0, PointerType.Mouse, true),
                cell,
                default,
                0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                modifiers));

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            table.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        // ---- the modes, and switching between them ----

        [Fact]
        public Task None_selects_no_cell_either() =>
            Realised(() => Table(mode: LunaSelectionMode.None), table =>
            {
                table.SelectCell(table.Models[0], 0);

                Assert.Null(table.SelectedCell);
                Assert.Empty(Boxes(table));
            });

        // Changing the unit CLEARS, and that is the decision rather than an oversight: a row has no
        // column to become, and turning a selected cell into its whole row selects more than the
        // user asked for. Nothing selected is the one state both units agree on.
        //
        // SelectedCells IS THE ASSERTION THAT BITES. SelectedCell, the boxes and the row all go when
        // the current cell is forgotten, so a version that dropped the cursor and kept the set passed
        // the first draft of this test unchanged - and would have handed a caller a list of cells
        // after the user had switched the table to selecting rows.
        [Fact]
        public Task Changing_the_unit_clears_what_was_selected() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 1);

            table.SelectionUnit = LunaSelectionUnit.Row;

            Assert.Null(table.SelectedCell);
            Assert.Empty(table.SelectedCells);
            Assert.False(table.IsCellSelected(table.Models[1], 1));
            Assert.Empty(Boxes(table));
            Assert.Null(table.Selected);
        });

        // ---- what a caller reads ----

        // In a cell unit a row is selected when any of its cells is, so a caller asking which models
        // are involved gets the same kind of answer in both units rather than "one", which is what
        // reading the ListBox would say for a selection spanning three rows.
        [Fact]
        public Task SelectedItems_reports_every_row_a_selected_cell_is_in() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                table.SelectCell(table.Models[0], 0);
                Key(table, Avalonia.Input.Key.Down, KeyModifiers.Shift);
                Key(table, Avalonia.Input.Key.Down, KeyModifiers.Shift);

                Assert.Equal(
                    new[] { "alpha", "bravo", "charlie" },
                    table.SelectedItems.Select(r => r.Name).ToArray());
            });

        // In DISPLAY order and not click order, for SelectedItems' reason (§54): a caller acting on
        // a multi-selection wants it in the order the user is looking at.
        [Fact]
        public Task SelectedCells_come_back_in_display_order() =>
            Realised(() => Table(mode: LunaSelectionMode.Multiple), table =>
            {
                Assert.True(table.TryGetCell(table.Models[2], 2, out Control? last));
                Assert.True(table.TryGetCell(table.Models[0], 1, out Control? first));

                Click(table, last!);
                Click(table, first!, KeyModifiers.Control);

                Assert.Equal(
                    new[] { "alpha:1", "charlie:2" },
                    table.SelectedCells.Select(c => $"{c.Row.Name}:{c.Column}").ToArray());
            });

        [Fact]
        public Task CellChosen_reports_the_cell_once() => Realised(() => Table(), table =>
        {
            var heard = new List<string>();
            table.CellChosen += cell => heard.Add(cell is { } at ? $"{at.Row.Name}:{at.Column}" : "none");

            table.SelectCell(table.Models[1], 2);
            table.ClearCellSelection();

            Assert.Equal(new[] { "bravo:2", "none" }, heard);
        });

        // ---- the rest of the control ----

        // A cell selection is held by KEY, so a Refresh that rebuilds every model keeps it - the same
        // rule the row selection has followed since §27.6 and expansion since §55.4.
        [Fact]
        public Task A_cell_selection_survives_a_refresh_that_rebuilds_the_models() =>
            Realised(() => Table(), table =>
            {
                table.SelectCell(table.Models[1], 2);

                table.Refresh(new[] { new Row("alpha", 1), new Row("bravo", 7), new Row("charlie", 3) });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Equal("bravo:2", Coordinate(table));
                Assert.Equal(new[] { "bravo:2" }, Boxes(table));
            });

        // And a row that has actually gone takes its cell with it.
        //
        // THE ROW COMES BACK, and that is the whole test rather than decoration. Every reader of the
        // selection resolves a key through the current view, so a departed row already answers
        // "nothing selected" whether or not anything was pruned - the first draft of this asserted
        // exactly that and could not fail. The difference only becomes visible when a row with the
        // same key returns: unpruned, its old cell lights up again, having been selected by nobody.
        [Fact]
        public Task A_cell_whose_row_left_the_view_does_not_come_back_with_it() =>
            Realised(() => Table(), table =>
            {
                table.SelectCell(table.Models[1], 2);

                table.Refresh(new[] { new Row("alpha", 1), new Row("charlie", 3) });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Null(table.SelectedCell);
                Assert.Empty(table.SelectedCells);

                table.Refresh(new[] { new Row("alpha", 1), new Row("bravo", 2), new Row("charlie", 3) });
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                table.UpdateLayout();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.Empty(table.SelectedCells);
                Assert.Empty(Boxes(table));
            });

        // F2 OPENS THE SELECTED CELL, which is what having a cell cursor buys the keyboard. In a row
        // unit it still opens the first editable column, because there is nothing better to guess -
        // this asserts the difference rather than the mechanism, since the mechanism is one branch.
        [Fact]
        public Task F2_edits_the_cell_the_user_is_on() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 0);

            Key(table, Avalonia.Input.Key.F2);

            Assert.True(table.IsEditing);
        });

        // The other half, and the one that would pass by accident if F2 still took the first editable
        // column: column 2 is not editable, so an F2 there must open nothing at all.
        [Fact]
        public Task F2_on_a_cell_that_cannot_be_edited_opens_nothing() => Realised(() => Table(), table =>
        {
            table.SelectCell(table.Models[1], 2);

            Key(table, Avalonia.Input.Key.F2);

            Assert.False(table.IsEditing);
        });
    }
}
