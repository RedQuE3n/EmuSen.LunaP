using System;
using System.Collections.Generic;
using System.Linq;

namespace EmuSen.LunaP.Controls
{
    // SELECTING A ROW - see docs/LunaP.md §27.6, and §54 for the multi-selection.
    //
    // NO INDEX ARITHMETIC ANYWHERE IN THIS FILE, and that is the difference from LunaList<T> worth
    // knowing if the two are ever merged. LunaList puts STRINGS in its ListBox and has to map an
    // index back to a model; this puts the models in directly, so Selected is a cast and nothing
    // more. LunaList's string projection is the older design, and this is what it looks like
    // without it.
    //
    // SELECTION IS MATCHED BY Key ACROSS A REFRESH, which is the trap this control shares with
    // LunaList and the reason Key exists at all. The default is the item itself - reference identity
    // for a class - which is right for a cached model handed back unchanged and wrong for rows
    // rebuilt on every poll, where every item is a new object, nothing matches, and the selection is
    // lost each refresh.
    //
    // Select HOLDS A SELECTION MADE BEFORE THERE IS A TEMPLATE, which is not a nicety: a window in
    // this toolkit is built in its constructor, so the table is filled and a row is selected long
    // before anything is shown. An early Select that returned quietly would leave the caller looking
    // at a table with nothing highlighted and no error to explain it. It was found by looking at a
    // render rather than by a test, which is why TemplateOrderTests exists (§28.2).
    public partial class LunaTable<T> where T : class
    {
        // HELD HERE RATHER THAN READ OFF THE ListBox, because a caller can set this before there is
        // a template - the same reason Select queues (§27.6) - and a property that silently did
        // nothing when set in a constructor would be the §5.5 symptom again.
        private LunaSelectionMode _selectionMode = LunaSelectionMode.Single;

        // Single by default, which is what the table has always done, so no existing table changes
        // (§26.13). None is a real mode rather than an omission: a table used purely as a readout -
        // a register dump, a log - has nothing useful to select, and a row that highlights under the
        // pointer suggests an action that does not exist.
        /// <summary>How many rows may be selected at once. Single by default.</summary>
        public LunaSelectionMode SelectionMode
        {
            get => _selectionMode;
            set
            {
                _selectionMode = value;
                ApplySelectionMode();
            }
        }

        private void ApplySelectionMode()
        {
            if (Rows is null) return;

            // A CELL UNIT PUTS THE ListBox IN SINGLE, whatever the mode says, and the multi-cell
            // selection is kept beside it rather than in it. Avalonia's ListBox selects rows; asking
            // it for Multiple here would highlight whole rows behind a selection that is a handful of
            // cells, which says something untrue about what the next command will act on.
            Rows.SelectionMode = (_selectionUnit, _selectionMode) switch
            {
                (LunaSelectionUnit.Cell, _) => Avalonia.Controls.SelectionMode.Single,
                (_, LunaSelectionMode.Multiple) => Avalonia.Controls.SelectionMode.Multiple,
                _ => Avalonia.Controls.SelectionMode.Single,
            };

            // None is spelled as "single, and nothing can be hit", because Avalonia's ListBox has no
            // mode that refuses selection outright. Clearing what is there matters as much as
            // refusing the next one - switching to None with a row already selected has to leave the
            // table with nothing selected, or the mode reads as "no NEW selections".
            Rows.IsHitTestVisible = _selectionMode != LunaSelectionMode.None;
            if (_selectionMode == LunaSelectionMode.None)
            {
                using (_filling.Suppress()) Rows.SelectedItem = null;
            }
        }

        // THE MULTI-SELECTION, AS MODELS AND IN DISPLAY ORDER. Empty rather than null when nothing
        // is selected, because a caller writing `foreach (T row in table.SelectedItems)` should not
        // have to ask first - and because "nothing is selected" and "selection is unavailable" are
        // the same answer to this question.
        //
        // Ordered by the VIEW rather than by the order rows were clicked. A caller acting on a
        // multi-selection - delete these, export these - almost always wants them in the order the
        // user is looking at, and click order is not recoverable from the ListBox anyway.
        /// <summary>Every selected model, in display order. Empty when nothing is selected.</summary>
        public IReadOnlyList<T> SelectedItems
        {
            get
            {
                // IN A CELL UNIT, A ROW IS SELECTED WHEN ANY OF ITS CELLS IS. The ListBox is holding
                // one row (the current cell's) whatever the mode, so reading it here would answer
                // "one" for a selection spanning four rows. A caller asking which models are involved
                // in what the user has picked gets the same answer in both units.
                if (_selectionUnit == LunaSelectionUnit.Cell)
                {
                    if (_cells.Count == 0) return Array.Empty<T>();

                    var rows = new List<T>();
                    foreach (T candidate in _view)
                    {
                        object key = KeyOf(candidate);
                        for (int column = 0; column < _columns.Count; column++)
                        {
                            if (!_cells.Contains((key, column))) continue;

                            rows.Add(candidate);
                            break;
                        }
                    }

                    return rows;
                }

                if (Rows?.SelectedItems is not { Count: > 0 } selected) return Array.Empty<T>();

                var picked = new List<T>(selected.Count);
                foreach (T candidate in _view)
                {
                    if (selected.Contains(candidate)) picked.Add(candidate);
                }

                return picked;
            }
        }

        // The selected model. Unlike LunaList<T>, which puts STRINGS in its ListBox and has to map
        // an index back, this one puts the models in directly - so there is no index arithmetic
        // here at all. That difference is worth knowing if the two are ever merged: LunaList's
        // string projection is the older design, and this is what it would look like without it.
        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected => Rows?.SelectedItem as T;

        // Raised only for a real user choice, never for the selection restored during a refresh.
        //
        // A SELECTION CHANGE, NOT AN ACTIVATION - the same correction LunaList's Chose carries, and
        // for the same reason: the old summary said "when the user picks a row", which a consumer
        // read as double-click and wired an irreversible action to. §78. A table row's activation
        // gesture is DoubleTapped on the table, as it is for a list.
        /// <summary>Raised when the selection changes to a different row, with the model rather than the row. This is a selection, NOT an activation - for double-click or Enter, handle DoubleTapped or KeyDown. Not raised by Refresh or Select.</summary>
        public event Action<T?>? Chose;

        // Selects by model. Does not raise Chose - a caller setting the selection knows what it set.
        //
        // HELD UNTIL THE TEMPLATE EXISTS, which is not a nicety. A window in this toolkit is built
        // in its constructor: the table is filled and a row is selected long before anything is
        // shown, and an early Select that returned quietly would leave the caller looking at a
        // table with nothing highlighted and no error to explain it. FilterBar.SetFacets holds
        // pending facets for exactly the same reason (§14.2).
        //
        // Found by looking at a render rather than by a test: the row simply was not highlighted.
        /// <summary>Selects a model without raising Chose.</summary>
        /// <param name="item">The model to select, matched by Key. Null clears the selection.</param>
        public void Select(T? item)
        {
            if (Rows is null)
            {
                _pending = item;
                _hasPending = true;
                return;
            }

            using (_filling.Suppress())
            {
                Rows.SelectedItem = item is null
                    ? null
                    : _view.FirstOrDefault(candidate => Equals(Key(candidate), Key(item)));
            }
        }

        // A selection asked for before there was anywhere to put it. Null is a real value here -
        // "select nothing" - so the flag rather than the field says whether one is waiting.
        private T? _pending;

        private bool _hasPending;
    }
}
