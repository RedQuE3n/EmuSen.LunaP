using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // SELECTING A CELL RATHER THAN A ROW - see docs/LunaP.md §67.
    //
    // The largest of the fourteen, and it is one feature rather than three: the SET of selected
    // cells, the BOXES that draw them, and the KEYS AND CLICKS that move them. Splitting those would
    // put the anchor in one file and the Shift range that measures from it in another.
    //
    // WHAT SPANS ALL THREE is that the ListBox underneath is doing something different from what the
    // user sees. It holds one row - the current cell's - whatever the mode, so every question about
    // "what is selected" has to be answered from `_cells` here rather than read off the control.
    // SelectedItems in TableSelection.cs is the one that had to learn this from outside, and its
    // comment records why.
    //
    // Row selection is TableSelection.cs. The two are deliberately not merged: a row selection and a
    // cell selection are not translations of each other, which is the argument on SelectionUnit
    // below for why changing the unit clears what was selected.
    public partial class LunaTable<T> where T : class
    {
        private LunaSelectionUnit _selectionUnit = LunaSelectionUnit.Row;

        // Row by default, so a table that never names this behaves exactly as it did (§26.13).
        //
        // Changing it CLEARS what was selected, and that is the honest answer rather than a
        // convenience: a row selection and a cell selection are not translations of each other. A row
        // has no column to become, and turning a selected cell into its whole row would select more
        // than the user asked for. Nothing selected is the one state both units agree on.
        /// <summary>Whether selection is a row or a single cell. Row by default.</summary>
        public LunaSelectionUnit SelectionUnit
        {
            get => _selectionUnit;
            set
            {
                if (_selectionUnit == value) return;

                _selectionUnit = value;
                _cells.Clear();
                _anchor = null;
                _current = null;

                using (_filling.Suppress())
                {
                    if (Rows is not null) Rows.SelectedItem = null;
                }

                ApplySelectionMode();
                MarkCells();
            }
        }

        // THE SELECTION ITSELF, AS KEYS AND COLUMN INDICES rather than as models. The key is what
        // survives a Refresh that rebuilds the models (§27.6, §55.4) - holding the T here would keep
        // a replaced object alive and compare it by reference against its own successor.
        private readonly HashSet<(object Key, int Column)> _cells = new();

        // Where a Shift range measures from, and where the arrows move. Two fields and not one: the
        // anchor stays put while Shift+arrow walks the current cell away from it, which is what makes
        // an extended range shrink again when the user comes back.
        private (object Key, int Column)? _anchor;

        private (object Key, int Column)? _current;

        /// <summary>Raised when the selected cell changes. Null when the selection was cleared.</summary>
        public event Action<LunaCell<T>?>? CellChosen;

        // THE CURRENT CELL, which is the one the arrows move and the one F2 opens. Under a Shift
        // range it is the far end rather than the anchor, because that is the end the user is moving.
        /// <summary>The current cell, or null when no cell is selected or the unit is Row.</summary>
        public LunaCell<T>? SelectedCell
        {
            get
            {
                if (_current is not { } at) return null;

                return ModelFor(at.Key) is { } model ? new LunaCell<T>(model, at.Column) : null;
            }
        }

        // IN DISPLAY ORDER, ROW BY ROW AND THEN COLUMN BY COLUMN, for SelectedItems' reason (§54):
        // a caller acting on a multi-selection wants it in the order the user is looking at, and the
        // order cells were clicked is not recoverable and not what anybody means by "these cells".
        //
        // Walked rather than stored, so a sort, an expand or a hidden column reorders this for free
        // and there is no second copy of the selection to keep in step.
        /// <summary>Every selected cell, in display order. Empty when nothing is selected.</summary>
        public IReadOnlyList<LunaCell<T>> SelectedCells
        {
            get
            {
                if (_cells.Count == 0) return Array.Empty<LunaCell<T>>();

                var picked = new List<LunaCell<T>>(_cells.Count);
                foreach (T row in _view)
                {
                    object key = KeyOf(row);
                    for (int column = 0; column < _columns.Count; column++)
                    {
                        if (_cells.Contains((key, column))) picked.Add(new LunaCell<T>(row, column));
                    }
                }

                return picked;
            }
        }

        /// <summary>Whether one cell is selected.</summary>
        /// <param name="item">The row's model.</param>
        /// <param name="column">The column index, in the order the columns were added.</param>
        /// <returns>True when that cell is part of the current selection.</returns>
        public bool IsCellSelected(T item, int column) =>
            item is not null && _cells.Contains((KeyOf(item), column));

        // Public for Edit's reason (§50.3): a menu item, a search result or a caller restoring its
        // own state needs to put the selection somewhere without synthesising a click.
        /// <summary>Selects one cell, replacing whatever was selected.</summary>
        /// <param name="item">The row's model.</param>
        /// <param name="column">The column index, in the order the columns were added.</param>
        /// <remarks>
        /// Does nothing when the unit is Row, the mode is None, the column is hidden or out of range,
        /// or the model is not in the current view. Unlike Edit it does not need the row to be
        /// realised, because a selection is a fact about the model rather than about a visual.
        /// </remarks>
        public void SelectCell(T item, int column)
        {
            if (!CanSelectCell(item, column)) return;

            _cells.Clear();
            Add(item, column);
            _anchor = _current;
            Announce();
        }

        /// <summary>Clears the cell selection.</summary>
        public void ClearCellSelection()
        {
            if (_cells.Count == 0 && _current is null) return;

            _cells.Clear();
            _anchor = null;
            _current = null;
            Announce();
        }

        private bool CanSelectCell(T? item, int column)
        {
            if (_selectionUnit != LunaSelectionUnit.Cell) return false;
            if (_selectionMode == LunaSelectionMode.None) return false;
            if (item is null || column < 0 || column >= _columns.Count) return false;

            return _columns[column].IsVisible && IndexOf(item) >= 0;
        }

        private void Add(T item, int column)
        {
            object key = KeyOf(item);
            _cells.Add((key, column));
            _current = (key, column);
        }

        // The rectangle between the anchor and one corner, which is what Shift means everywhere a
        // grid has ever been selected: rows between those two rows, columns between those two
        // columns, and every cell in the block. Not the reading order a text selection would give -
        // a spreadsheet's Shift+click has never meant "everything from here to there along the rows".
        //
        // Hidden columns are skipped rather than included-and-not-drawn, so a range that spans one
        // does not quietly select a cell the user cannot see and a later command cannot show them.
        private void SelectRangeTo(T item, int column)
        {
            if (_anchor is not { } anchor || _selectionMode != LunaSelectionMode.Multiple)
            {
                SelectCell(item, column);
                return;
            }

            if (ModelFor(anchor.Key) is not { } from || !CanSelectCell(item, column)) return;

            int firstRow = Math.Min(IndexOf(from), IndexOf(item));
            int lastRow = Math.Max(IndexOf(from), IndexOf(item));
            int firstColumn = Math.Min(anchor.Column, column);
            int lastColumn = Math.Max(anchor.Column, column);

            _cells.Clear();
            for (int row = firstRow; row <= lastRow; row++)
            {
                object key = KeyOf(_view[row]);
                for (int at = firstColumn; at <= lastColumn; at++)
                {
                    if (_columns[at].IsVisible) _cells.Add((key, at));
                }
            }

            _current = (KeyOf(item), column);
            Announce();
        }

        // Ctrl, which is the other half of what a multi-selection means: add this one, or take it
        // back off if it was already there. The anchor moves to it either way, so a Shift that
        // follows measures from where the user last pointed rather than from wherever they began.
        private void ToggleCell(T item, int column)
        {
            if (_selectionMode != LunaSelectionMode.Multiple)
            {
                SelectCell(item, column);
                return;
            }

            if (!CanSelectCell(item, column)) return;

            object key = KeyOf(item);
            if (!_cells.Remove((key, column))) _cells.Add((key, column));

            _anchor = (key, column);
            _current = _cells.Contains((key, column)) ? (key, column) : null;
            Announce();
        }

        // Both halves of "something changed": what the user sees, and what the caller hears. Every
        // path that touches the selection ends here so neither can be forgotten.
        private void Announce()
        {
            Sync();
            CellChosen?.Invoke(SelectedCell);
        }

        // The visible half on its own, because OnPartsAttached needs it and must NOT raise the event.
        // A caller who selected a cell from its constructor - before this control had a template -
        // has already been told about that selection; telling them again when the template arrives
        // would make CellChosen fire twice for one thing the user did, and the second one carries no
        // new information. §27.6 is the same trap for row selection, one layer down.
        private void Sync()
        {
            MarkCells();

            // The row under the current cell follows it, which is what keeps Selected, Chose and the
            // vertical scroll working in this unit without a second implementation of any of them.
            // Suppressed so the ListBox's own event does not read as a row the user chose.
            if (Rows is null || _selectionUnit != LunaSelectionUnit.Cell) return;

            using (_filling.Suppress())
            {
                Rows.SelectedItem = _current is { } at ? ModelFor(at.Key) : null;
            }
        }

        private T? ModelFor(object key)
        {
            foreach (T candidate in _view)
            {
                if (Equals(KeyOf(candidate), key)) return candidate;
            }

            return null;
        }

        // THE CELL SELECTION SURVIVES A REBUILD THE SAME WAY THE ROW ONE DOES, and for the same
        // reason: it is held by key (§67), so a sort or an expand leaves it entirely alone and only a
        // model actually leaving the view can take a cell with it.
        //
        // WHY THIS IS NOT REDUNDANT, WHICH IS NOT OBVIOUS AND COST A HOLLOW TEST. Every reader of the
        // selection resolves a key through the current view, so a departed row already answers
        // "not selected" without anything being pruned - the entry just sits there. The difference
        // appears when a row with the SAME KEY comes back, which is the normal life of a polling
        // window: unpruned, its old cell lights up again, selected by nobody. §67.5.
        //
        // No column test here. Columns are only ever appended and IsVisible is init-only, so an entry
        // cannot outlive its column - a clause for either would be unreachable code with the
        // authority of a decision.
        private void DropCellsThatLeft()
        {
            if (_cells.Count == 0) return;

            var alive = new HashSet<object>();
            foreach (T item in _view) alive.Add(KeyOf(item));

            _cells.RemoveWhere(at => !alive.Contains(at.Key));

            // The anchor and the current cell go when their cell does, or a Shift range would measure
            // from a row that is no longer there.
            if (_anchor is { } anchor && !_cells.Contains(anchor)) _anchor = null;
            if (_current is { } current && !_cells.Contains(current)) _current = null;

            MarkCells();
        }

        // Puts a box on every selected cell of every REALISED row, and takes the stale ones off.
        // Called from everywhere the selection or the rows can change, and from every layout - which
        // is the hook that actually catches a recycled row, for §61.4's reason one feature over.
        //
        // Whether any box might be on screen, so a table that is not doing cell selection - which is
        // every table that has not asked for one - pays a comparison per layout and nothing else. A
        // flag rather than a test of _cells, because switching the unit back to Row clears the cells
        // first and still needs one more sweep to take the boxes off.
        private bool _boxed;

        private void MarkCells()
        {
            if (Rows is null) return;
            if (_selectionUnit != LunaSelectionUnit.Cell && !_boxed) return;

            foreach (Control container in Rows.GetRealizedContainers()) MarkCells(container);

            _boxed = _selectionUnit == LunaSelectionUnit.Cell && _cells.Count > 0;
        }

        // One container's worth, and the sweep above is the only caller it has ever had. CONTAINER-
        // PREPARED LOOKS LIKE THE RIGHT HOOK AND IS NOT: at that moment the container has no row grid
        // inside it at all - measured, `grid=False` on every prepare, including recycled ones - so
        // marking there is a call that quietly does nothing. §67.5.
        private void MarkCells(Control container)
        {
            if (RowGridIn(container) is not { } grid) return;

            var wanted = new HashSet<int>();
            if (container.DataContext is T model && _selectionUnit == LunaSelectionUnit.Cell)
            {
                object key = KeyOf(model);
                for (int column = 0; column < _columns.Count; column++)
                {
                    if (_cells.Contains((key, column))) wanted.Add(GridColumn(column));
                }
            }

            // Removed by what is NOT wanted, and `wanted` is emptied of what already exists as it
            // goes - so a box that is still correct is left exactly where it is rather than being
            // torn down and rebuilt on every keystroke.
            foreach (Control stale in grid.Children.OfType<Control>()
                         .Where(c => c.Classes.Contains("cell-selection"))
                         .Where(c => !wanted.Remove(Grid.GetColumn(c)))
                         .ToList())
            {
                grid.Children.Remove(stale);
            }

            foreach (int column in wanted)
            {
                Control box = CellBox();
                Grid.SetColumn(box, column);
                grid.Children.Add(box);
            }
        }

        // WHAT A SELECTED CELL LOOKS LIKE - see docs/LunaP.md §67.
        //
        // A SIBLING BORDER IN THE CELL'S COLUMN, for FrozenEdge's reason and one of its own. The
        // reason it shares: a sibling costs no wrapper, so nothing about the row's tree changes and
        // BeginEdit still finds a Panel where it needs one. The reason of its own is the harder one -
        // a selected cell is drawn as an OUTLINE and never as a fill, because §57 lets a cell be any
        // control a caller returned. A fill would paint over somebody's coloured dot, their progress
        // bar, their sparkline; an outline is drawable around all three and hides none of them.
        //
        // Not hit-testable, or it would eat the click that selected it and the double-click that
        // opens an editor.
        //
        // BUILT ON DEMAND RATHER THAN PER CELL AND HIDDEN. Row() already refuses to build anything at
        // all for a hidden column, on the grounds that a control which cannot be seen still costs a
        // measure pass per row per frame; a permanent invisible box in every cell of every row would
        // be that cost, multiplied by the column count, for a feature most tables never turn on.
        private static Control CellBox()
        {
            var box = new Border
            {
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            box.Classes.Add("cell-selection");
            return box;
        }

        // Which cell a pointer went down on, if any. Walks up from whatever was actually hit until it
        // finds a control carrying the column marker - the same marker Cell() looks for, so the two
        // agree by construction about what counts as a cell.
        private void ClickedCell(Avalonia.Input.PointerPressedEventArgs e)
        {
            if (_selectionUnit != LunaSelectionUnit.Cell) return;
            if (e.Source is not Visual source) return;

            foreach (Visual step in source.GetSelfAndVisualAncestors())
            {
                if (step is not Control control) continue;

                int column = TableCells.GetColumn(control);
                if (column < 0) continue;

                if (control.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()
                        ?.DataContext is not T model)
                {
                    return;
                }

                Avalonia.Input.KeyModifiers keys = e.KeyModifiers;
                if (keys.HasFlag(Avalonia.Input.KeyModifiers.Control)) ToggleCell(model, column);
                else if (keys.HasFlag(Avalonia.Input.KeyModifiers.Shift)) SelectRangeTo(model, column);
                else SelectCell(model, column);

                return;
            }
        }

        // THE ARROWS, AND ONLY THE ONES THIS CONTROL HAS TO OWN - see docs/LunaP.md §67.
        //
        // Left and Right move the current cell across the row; Home and End take it to the ends. Up
        // and Down are deliberately NOT handled here: the ListBox already moves its row selection
        // with them, it scrolls and virtualises while doing so, and Announce keeps the current cell's
        // column while its row follows along. Re-implementing them would be a second, worse copy of
        // all three behaviours.
        //
        // Shift is the exception, and it is why this returns a bool rather than being a void the
        // caller ignores. Shift+Up in a single-selection ListBox does nothing at all, so a cell range
        // that grows upwards has to be built here - and once Shift+Up is ours, Shift+Down has to be
        // too or the two keys would behave differently.
        private bool MoveCell(Avalonia.Input.KeyEventArgs e)
        {
            if (_selectionMode == LunaSelectionMode.None) return false;
            if (SelectedCell is not { } at) return false;

            bool extend = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift);
            int row = IndexOf(at.Row);
            int column = at.Column;

            switch (e.Key)
            {
                case Avalonia.Input.Key.Left:
                    column = NextVisible(column, -1);
                    break;
                case Avalonia.Input.Key.Right:
                    column = NextVisible(column, 1);
                    break;
                case Avalonia.Input.Key.Home:
                    column = NextVisible(-1, 1);
                    break;
                case Avalonia.Input.Key.End:
                    column = NextVisible(_columns.Count, -1);
                    break;
                case Avalonia.Input.Key.Up when extend:
                    row--;
                    break;
                case Avalonia.Input.Key.Down when extend:
                    row++;
                    break;
                default:
                    return false;
            }

            // AT THE EDGE, NOTHING MOVES AND THE KEY IS STILL EATEN. Letting it through would send
            // Right on the last column to the ListBox, which would move focus out of the table
            // entirely - so the user would find that walking one column too far left the control.
            if (column < 0 || row < 0 || row >= _view.Count)
            {
                e.Handled = true;
                return true;
            }

            T target = _view[row];
            if (extend) SelectRangeTo(target, column); else SelectCell(target, column);

            // The cell has to be somewhere the user can see, and the two directions need two
            // mechanisms: a row scrolled away is the ListBox's business, and a column outside the
            // viewport is this control's (§64.4 measured the same problem for an editor).
            //
            // And since §72 there is a third thing that can be wrong with it: the column may not have
            // been built. Walking Right off the edge of the range would select a cell, find no visual
            // to scroll to, and leave the user pressing a key that appears to do nothing - so the
            // column is realized first and the existing BringIntoView then works unchanged. §72.4.
            BringRowIntoView(target);
            ShowColumn(column);
            if (Cell(target, column) is { } visual) visual.BringIntoView();

            e.Handled = true;
            return true;
        }

        // The next column that is actually on screen, walking in one direction from where we are.
        // A hidden column is stepped over rather than landed on, because a selection the user cannot
        // see is one they cannot act on - and it would look like the arrow key had done nothing.
        private int NextVisible(int from, int step)
        {
            for (int i = from + step; i >= 0 && i < _columns.Count; i += step)
            {
                if (_columns[i].IsVisible) return i;
            }

            return -1;
        }
    }
}
