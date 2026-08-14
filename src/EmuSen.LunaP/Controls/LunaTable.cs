using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // The non-generic half, which exists so the theme can name it - see docs/LunaP.md §27.
    //
    // A generic control cannot carry a XAML style selector: `luna|LunaTable` has no way to say
    // which T, and `LunaList<T>` sidesteps the whole question by borrowing ListBox's theme and
    // having no template of its own. A table cannot do that, because the one thing it adds to a
    // list is a HEADER ROW, and that has to come from somewhere.
    //
    // So the parts and the look live on this class, which XAML can select, and the models live on
    // the generic one below. An Avalonia style selector matches subclasses, so a style written for
    // `luna|LunaTable` reaches every `LunaTable<T>` there will ever be.
    /// <summary>The non-generic base of LunaTable&lt;T&gt;, which exists so that a theme can name the type.</summary>
    public abstract class LunaTable : TemplatedControl
    {
        // The scrollbar is the reason this is worth a note. Fluent's ScrollViewer overlays its
        // scrollbar rather than taking layout space from the content, so the rows keep the full
        // width when one appears and the header above them stays lined up. If that ever changes,
        // the symptom is a header that drifts right of its cells by about seventeen pixels the
        // moment a table gets long enough to scroll.
        /// <summary>The header row from the template, or null before the template has been applied.</summary>
        protected Grid? HeaderGrid;
        /// <summary>The row list from the template, or null before the template has been applied.</summary>
        protected ListBox? Rows;

        /// <summary>Where a rejected edit says what is wrong, or null before the template has been applied.</summary>
        protected ErrorText? Message;

        // A Table, not a DataGrid, and the distinction is a promise rather than a label: UIA's
        // DataGrid and Table types come with IGridProvider and ITableProvider, which let a reader
        // ask for "row 4, column 2" and navigate a grid as a grid. This control implements neither
        // - it is a list of rows that happen to be laid out in columns - so claiming the type
        // would advertise navigation that is not there. What it does instead is give every ROW a
        // name built from its own cells, "name: Site, type: text, pg: 1", which is the useful
        // half of a table for a reader and is honestly deliverable. §27.3.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            HeaderGrid = e.NameScope.Find<Grid>("PART_Header");
            Rows = e.NameScope.Find<ListBox>("PART_Rows");
            Message = e.NameScope.Find<ErrorText>("PART_Error");
            OnPartsAttached();
        }

        /// <summary>Called once the template has been applied and HeaderGrid and Rows are available, which is where a subclass replays anything configured before that.</summary>
        protected abstract void OnPartsAttached();
    }

    // A list with columns - see docs/LunaP.md §27.
    //
    // The evidence for this is one site and it is worth stating exactly, because §21's rule is
    // that one hand-roll is a hazard note rather than a roadmap entry. The site is not in a
    // consumer of this toolkit yet: it is `bima/viewer.py:460`, in the Python application BIMA-C
    // is porting, and it reads
    //
    //     self.tree.setHeaderLabels(["name", "type", "pg"])
    //     ...
    //     QTreeWidgetItem(self.tree, [f.name, f.type, str(f.page + 1)])
    //
    // Every row added at the top level: a three-column list in a tree widget, with the tree part
    // unused. That shape is the whole specification, and it is why this control is FLAT and has no
    // expander. A hierarchical view is a different control and §27.4 says so rather than leaving
    // the absence to be discovered.
    //
    // TAKES PROJECTIONS, NOT AN INTERFACE, exactly as LunaList<T> does and for the same §1 reason:
    // a column is a header and a Func<T, string>, so a caller's own model needs no attribute, no
    // base class and no knowledge that LunaP exists.
    /// <summary>A flat list with columns, where each column is a header and a projection from the model.</summary>
    public class LunaTable<T> : LunaTable where T : class
    {
        private readonly List<ColumnSpec> _columns = new();
        private readonly List<Head> _heads = new();
        private readonly Suppressor _filling = new();

        // Its own suppressor rather than _filling's, because the two guard different things: _filling
        // says "this selection is not the user's" and gates Chose, and this says "this tick is the
        // table putting the box back" and gates the caller's Toggle. Sharing one would mean a refresh
        // arriving mid-toggle swallowed the other's event.
        private readonly Suppressor _toggling = new();

        // _items is the order the caller gave to Refresh and never changes under a sort. _view is
        // what is on screen. They are the same list until a header is clicked, and keeping them
        // apart is what makes the third click - back to arrival order - possible at all.
        private IReadOnlyList<T> _items = Array.Empty<T>();
        private IReadOnlyList<T> _view = Array.Empty<T>();

        // Which column is sorted, and which way. -1 is the third state and the initial one.
        private int _sortColumn = -1;
        private bool _sortDescending;

        // HIERARCHY, AND THE THREE THINGS IT NEEDS - see docs/LunaP.md §55.
        //
        // Keyed by Key(item) rather than by the model, so that expansion survives a Refresh that
        // hands back new objects for the same rows. That is the same reason selection is keyed that
        // way (§27.6), and it matters more here: a polling window refreshes every second, and a tree
        // that collapsed itself on every poll would be unusable rather than merely annoying.
        //
        // _expanded is the USER'S state and outlives any particular set of models. _depth and
        // _expandable are derived, rebuilt on every flatten, and exist only so that Row() can draw an
        // indent and a toggle without walking the tree again per row.
        private readonly HashSet<object> _expanded = new();
        private readonly Dictionary<object, int> _depth = new();
        private readonly HashSet<object> _expandable = new();
        private Func<T, IEnumerable<T>>? _children;

        // Where each row sits in the view, so the gutter can be told without a scan. Empty and
        // untouched when there is no gutter to feed. §58.
        private readonly Dictionary<object, int> _position = new();

        // How far the rows have been scrolled sideways, so the header can be moved to match and so
        // an event that reports the same offset twice costs nothing. §59.
        private double _scrolledTo;

        // Saving is debounced for the reason SplitPane debounces its divider (§26.6): a drag
        // produces a property change per frame, and writing tables.json sixty times a second would
        // be a full read-modify-write of every table's layout per frame.
        private Debounce? _save;
        private string? _tableKey;
        private bool _restored;

        // A selection asked for before there was anywhere to put it. Null is a real value here -
        // "select nothing" - so the flag rather than the field says whether one is waiting.
        private T? _pending;
        private bool _hasPending;

        // What makes two items "the same item" across a refresh, so the selection survives one.
        // Defaults to reference identity, which is right for a cached model and wrong for rows
        // rebuilt from disk on every poll - the same default, and the same trap, as LunaList<T>.
        /// <summary>How a row is matched to a model across a Refresh, so a selection survives a rebuild.</summary>
        /// <remarks>
        /// Defaults to the item itself, which means REFERENCE IDENTITY for a class. Right for a cached
        /// model handed back unchanged; wrong for rows rebuilt on every poll, where every item is a new
        /// object, nothing matches, and the selection is lost each refresh. Give a stable key (an id, a
        /// path) when the models are rebuilt rather than reused.
        /// </remarks>
        public Func<T, object?> Key { get; set; } = item => item;

        // Opt-in, exactly as ToolWindow.WindowKey and SplitPane.PaneKey are: no key, no file. A
        // toolkit that started remembering every table in an application because the application
        // upgraded is a toolkit writing files nobody asked for.
        //
        // What is remembered is what the USER did - the widths they dragged and the sort they left
        // it in - and not what the caller declared. A column the caller widened in code between two
        // releases should take effect; a column the user widened should survive it. That is why
        // Widths saves Avalonia's own notation rather than resolved pixels: an untouched star column
        // comes back as "2*" and re-resolves against whatever window it now finds itself in.
        /// <summary>An opt-in key under which this table's column widths and sort are remembered. Null, the default, means nothing is written down.</summary>
        public string? TableKey
        {
            get => _tableKey;
            set
            {
                _tableKey = value;
                Restore();
            }
        }

        // Raised only for a real user choice, never for the selection restored during a refresh.
        /// <summary>Raised when the user picks a row, with the model rather than the row. Not raised for a selection restored by Refresh.</summary>
        public event Action<T?>? Chose;

        // The DISPLAYED order, which is the arrival order until a header is clicked and the sorted
        // order afterwards. A caller reading this back to write a report gets what the user is
        // looking at rather than what was handed in, which is the only reading of "currently shown"
        // that stays true once the table can sort.
        /// <summary>The models currently shown, in the order they are displayed in.</summary>
        public IReadOnlyList<T> Models => _view;

        // The selected model. Unlike LunaList<T>, which puts STRINGS in its ListBox and has to map
        // an index back, this one puts the models in directly - so there is no index arithmetic
        // here at all. That difference is worth knowing if the two are ever merged: LunaList's
        // string projection is the older design, and this is what it would look like without it.
        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected => Rows?.SelectedItem as T;

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

        // THE ONE PROPERTY THAT TURNS A LIST INTO A TREE, and null - the default - means it is not
        // one. A table that never sets this runs the same code path it did in 0.7.0: Flatten returns
        // Ordered(_items) unchanged, no depth is recorded, no expander is built, and nothing about a
        // flat table pays for hierarchy existing (§26.13).
        //
        // A PROJECTION, NOT AN INTERFACE, for the §1 reason every other seam here is one: a caller's
        // model needs no base class, no ITreeNode, and no knowledge that LunaP exists. `r => r.Kids`
        // is the whole of it, and a model that stores children in a dictionary elsewhere writes
        // `r => index[r.Id]` instead - which an interface on the model could not express at all.
        /// <summary>How to find a row's children, or null - the default - for a flat table.</summary>
        /// <remarks>
        /// Called during every rebuild, for every visible row and for every row that has to be tested for
        /// children, so it should be cheap. Return an empty sequence for a leaf. Rows are expanded through
        /// <see cref="Expand"/> and start collapsed.
        /// </remarks>
        public Func<T, IEnumerable<T>>? Children
        {
            get => _children;
            set
            {
                _children = value;
                Show();
            }
        }

        // How far one level is indented from the one above it. A property rather than a constant
        // because a table of file paths and a table of two-deep config groups want different
        // amounts, and neither is wrong.
        /// <summary>How many pixels each level of hierarchy is indented. 16 by default.</summary>
        public double IndentSize { get; set; } = 16;

        // WHICH COLUMN CARRIES THE EXPANDER, and it is a choice rather than always the first because
        // the first column is not always the name. A table whose leading column is a checkbox or an
        // icon wants the toggle beside the label instead, and TreeDataGrid makes the same choice by
        // having the caller declare which column is the hierarchical one.
        /// <summary>Which column shows the expander and the indent. The first column by default.</summary>
        public int ExpanderColumn { get; set; }

        // WHAT OPENS AN EDITOR, and it is a set rather than a mode because the two gestures are
        // independent - a table can want F2 without double-click, or neither. §56.
        //
        // Both by default, which is what §50 hardcoded, so no existing table changes. None is a real
        // value and not an omission: a table whose columns have a Commit but whose editing is driven
        // entirely by an application's own "Rename" menu item wants LunaTable.Edit and no gesture at
        // all, and turning the column read-only to get that would lose the validation with it.
        /// <summary>Which gestures open a cell editor. Double-click and F2 by default.</summary>
        public LunaEditGestures EditGestures { get; set; } = LunaEditGestures.Default;

        // THE GUTTER DOWN THE LEFT - see docs/LunaP.md §58.
        //
        // Null, the default, means there is no gutter and a table's grids have exactly the columns
        // they always had. Nothing about a table without one pays for this existing (§26.13).
        //
        // TAKES THE DISPLAY INDEX AS WELL AS THE MODEL, and that second argument is the whole reason
        // this is a delegate rather than a bool. TreeDataGrid's row header is a row NUMBER and
        // nothing else - its cell is a string with no projection behind it - which serves a list of
        // records and serves this toolkit's actual subject badly. A memory viewer wants addresses
        // down the left and a disassembly wants them too, and both are on the model.
        //
        // So both are expressible: `(_, i) => (i + 1).ToString()` numbers the rows, and
        // `(row, _) => row.Address.ToString("X4")` labels them. The index is the DISPLAYED one,
        // counted down the view after sorting and flattening, which is the only number that matches
        // what the user is looking at and is not otherwise reachable from a caller's projection.
        /// <summary>What to show in the gutter down the left, given a row and its displayed position. Null - the default - means no gutter.</summary>
        /// <remarks>
        /// The index is the row's position in what is currently DISPLAYED, so it counts down the screen
        /// under a sort rather than following the order given to Refresh. Number the rows with
        /// <c>(_, i) =&gt; (i + 1).ToString()</c>, or label them from the model and ignore it.
        /// </remarks>
        public Func<T, int, string>? RowHeader
        {
            get => _rowHeader;
            set
            {
                _rowHeader = value;
                Rebuild();
                Show();
                Pin();
            }
        }

        private Func<T, int, string>? _rowHeader;

        /// <summary>The gutter's width, in Avalonia's own notation. "Auto" by default, which fits the widest label.</summary>
        public string RowHeaderWidth { get; set; } = "Auto";

        // COLUMNS THAT DO NOT SCROLL AWAY - see docs/LunaP.md §61, and §60 for the correction that
        // made this possible after §59.3 concluded it was not.
        //
        // Zero, the default, is a table that behaves exactly as it did: nothing is transformed,
        // nothing is clipped, and Pin returns before touching a single child (§26.13).
        //
        // Counted in COLUMNS AND NOT IN GRID COLUMNS, like every other number a caller gives this
        // control: FrozenColumns = 2 freezes the first two columns the caller declared, whether or
        // not there is a gutter in front of them and whether or not one of them is hidden. §58.2 is
        // the section about keeping those two indices apart, and this is another place they meet.
        private int _frozenColumns;

        /// <summary>How many leading columns stay put when the table is scrolled sideways. Zero by default.</summary>
        /// <remarks>
        /// Counted in columns as they were added, so a hidden column still takes one. A gutter, when there
        /// is one, is frozen along with them as soon as this is greater than zero. Freezing more columns
        /// than the table has freezes all of them, which simply leaves nothing to scroll.
        /// </remarks>
        public int FrozenColumns
        {
            get => _frozenColumns;
            set
            {
                if (_frozenColumns == value) return;

                _frozenColumns = value;

                // Rebuilt rather than only re-pinned, because the seam is a child of the header and
                // of every row - so turning frozen columns on or off changes what those grids
                // CONTAIN and not merely where their contents sit.
                Rebuild();
                Show();
                Pin();
            }
        }

        // The frozen band expressed in GRID columns, which is what Pin walks. Clamped rather than
        // trusted: a caller who freezes five columns of a three-column table has said something
        // harmless, and the honest reading is "all of them" rather than an exception at layout time.
        //
        // A GUTTER IS ALWAYS FROZEN, EVEN AT FrozenColumns = 0 - see docs/LunaP.md §63. §59.4 pinned
        // the opposite as a decision on the record, because nothing could be frozen then; this is
        // that decision being taken rather than reversed by accident. A row header is how the user
        // refers to a row, and one that scrolls away leaves them reading a line of values with
        // nothing to say which row it is - which is the whole of what a gutter is for.
        private int FrozenGridColumns
        {
            get
            {
                int gutter = _rowHeader is null ? 0 : 1;
                return _frozenColumns <= 0
                    ? gutter
                    : gutter + Math.Min(_frozenColumns, _columns.Count);
            }
        }

        /// <summary>What sits above the gutter, in the header row. Empty by default, which is the spreadsheet's empty corner.</summary>
        public string RowHeaderCaption { get; set; } = string.Empty;

        // ONE PLACE THAT KNOWS THE GUTTER SHIFTS EVERY COLUMN RIGHT BY ONE. Every Grid.SetColumn and
        // every ColumnDefinition lookup in this control goes through this rather than using a column
        // index directly, because the two indices are genuinely different things: a COLUMN index is
        // what a caller wrote and what a remembered layout, a sort and Edit(item, 2) are written in,
        // and a GRID index is where that column sits once a gutter may be in front of it. Conflating
        // them is how a gutter would silently move a saved layout onto the wrong columns.
        private int GridColumn(int column) => _rowHeader is null ? column : column + 1;

        // NONE BY DEFAULT, which is what every table drew before §56 - and is also the better
        // default for the instrument panels this toolkit was built for, where a meter list wants to
        // read as a block rather than as a spreadsheet. A table of many narrow columns wants them;
        // a table of three does not.
        //
        // Drawn in LunaBorder, the token that already means "where one surface stops and the next
        // begins" (§26.9), rather than a colour of its own. A rule between cells is exactly that,
        // and it is already held to 3:1 against both surfaces.
        private LunaGridLines _gridLines;

        // THE LIFECYCLE, AND WHY IT IS TWO EVENTS AND NOT FIVE. TreeDataGrid raises CellPrepared,
        // CellClearing, RowPrepared, RowClearing and CellValueChanged. The cell pair is not
        // reproducible here and saying so is better than approximating it: this control builds its
        // cells inside the row template rather than realising them independently, so there is no
        // moment at which a cell is prepared that is not simply "its row was prepared". A CellPrepared
        // that fired once per cell during RowPrepared would carry no information the row event does
        // not, while implying a virtualization boundary that is not there. §56.
        //
        // RowPrepared and RowClearing ARE real and are exactly what recycling makes worth having: a
        // caller attaching per-row state - a tooltip, a context menu, a colour from a live source -
        // needs to know when a container starts standing for a different model, and the container
        // is reused, so "when it was created" is the wrong hook and there is no other.
        /// <summary>Raised when a row's container is about to stand for a model, including when a recycled container is reused.</summary>
        public event Action<T, Control>? RowPrepared;

        /// <summary>Raised when a row's container stops standing for its model, before it is reused or dropped.</summary>
        public event Action<T, Control>? RowClearing;

        // Raised after a value has been written and the row renamed, so a handler reading the model
        // sees the committed value rather than the one being replaced. Fires for an edit made by a
        // person and for one made through the automation provider, because both go through the same
        // gate (§50.6) and a caller watching for changes wants both.
        /// <summary>Raised after a cell edit has been committed, with the model and the column index.</summary>
        public event Action<T, int>? CellValueChanged;

        /// <summary>Which rules to draw between cells. None by default.</summary>
        public LunaGridLines GridLines
        {
            get => _gridLines;
            set
            {
                if (_gridLines == value) return;

                _gridLines = value;
                Show();
            }
        }

        /// <summary>Whether a row is currently expanded. Always false when the table is flat.</summary>
        /// <param name="item">The row's model.</param>
        /// <returns>True when the row's children are shown.</returns>
        public bool IsExpanded(T item) => item is not null && _expanded.Contains(KeyOf(item));

        /// <summary>Shows a row's children. Does nothing for a leaf or a flat table.</summary>
        /// <param name="item">The row's model.</param>
        public void Expand(T item)
        {
            if (item is null || _children is null) return;
            if (_expanded.Add(KeyOf(item))) Show();
        }

        /// <summary>Hides a row's children.</summary>
        /// <param name="item">The row's model.</param>
        public void Collapse(T item)
        {
            if (item is null) return;
            if (_expanded.Remove(KeyOf(item))) Show();
        }

        // EXPANDS WHAT IS REACHABLE, NOT WHAT IS EXPANDED. Walking only the currently-visible rows
        // would expand one level per call, which reads as a broken ExpandAll rather than a lazy one -
        // so this walks the whole tree through Children, whatever is open at the time.
        //
        // The cycle guard in Walk protects this too: without it, ExpandAll on a model whose Children
        // eventually returns an ancestor is an immediate stack overflow rather than a slow table.
        /// <summary>Expands every row that has children, at every level.</summary>
        public void ExpandAll()
        {
            if (_children is null) return;

            var seen = new HashSet<object>();
            CollectKeys(_items, seen, new HashSet<object>());
            if (seen.Count == 0) return;

            foreach (object key in seen) _expanded.Add(key);
            Show();
        }

        /// <summary>Collapses every row.</summary>
        public void CollapseAll()
        {
            if (_expanded.Count == 0) return;

            _expanded.Clear();
            Show();
        }

        private void CollectKeys(IEnumerable<T> level, HashSet<object> into, HashSet<object> path)
        {
            foreach (T item in level)
            {
                object key = KeyOf(item);
                if (!path.Add(key)) continue;

                IReadOnlyList<T> kids = ChildrenOf(item);
                if (kids.Count > 0)
                {
                    into.Add(key);
                    CollectKeys(kids, into, path);
                }

                path.Remove(key);
            }
        }

        // Key can return null - a caller's projection is allowed to - and null cannot go in a
        // dictionary, so the model itself is the fallback. That degrades to reference identity for
        // exactly the rows whose key is missing rather than for the whole table.
        private object KeyOf(T item) => Key(item) ?? item;

        private IReadOnlyList<T> ChildrenOf(T item)
        {
            if (_children is null) return Array.Empty<T>();

            IEnumerable<T>? kids = _children(item);
            return kids as IReadOnlyList<T> ?? kids?.ToList() ?? (IReadOnlyList<T>)Array.Empty<T>();
        }

        // A column, in the order it will appear. `width` is a GridLength as XAML spells one -
        // "*", "2*", "120" - and defaults to an equal share.
        //
        // AUTO IS ACCEPTED AND MADE TO WORK, which takes a little machinery: an Auto column in the
        // header and an Auto column in each row size themselves independently, so left alone they
        // would all be different widths and nothing would line up. Every column is therefore put
        // in a shared size group and the root is a shared size scope, which is Avalonia's own
        // mechanism for exactly this - and see Define below, because half of that mechanism does
        // not work the way it reads and this control shipped for a while with it silently off.
        /// <summary>Adds a column. Call once per column, before or after the template is applied.</summary>
        /// <param name="header">The column heading.</param>
        /// <param name="text">Turns a model into this cell text. Called for every row on every Refresh, so it should be cheap and free of side effects.</param>
        /// <param name="width">An Avalonia column width - "*", "Auto", or a number of pixels. Headers and cells share a size group, so they stay aligned.</param>
                /// <returns>The same table, so columns can be chained.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="header"/> or <paramref name="text"/> is null.</exception>
        public LunaTable<T> Column(string header, Func<T, string> text, string width = "*") =>
            Column(new LunaColumn<T>(header, text) { Width = width });

        // The form that carries behaviour, and the LAST Column overload that will ever be added -
        // anything a column grows from here is an init-only property on LunaColumn<T>, which is
        // additive by construction.
        //
        // THE TERSE OVERLOAD ABOVE DELEGATES HERE ON PURPOSE. Two ways to declare a column is a
        // deliberate convenience (§27), but two ways to BUILD one would be a defect waiting to
        // happen: the day a column gains a fifth property, the form somebody forgot to update
        // silently produces a different column. There is one path to a ColumnSpec, and a test
        // asserts the two forms are indistinguishable.
        /// <summary>Adds a column described by a LunaColumn&lt;T&gt;, which is how a column carries a sort.</summary>
        /// <param name="column">The column. Its Header and Text are required; Width and Sort have defaults.</param>
        /// <returns>The same table, so columns can be chained.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="column"/> is null.</exception>
        public LunaTable<T> Column(LunaColumn<T> column)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));

            _columns.Add(new ColumnSpec(
                column.Header, column.Text, GridLength.Parse(column.Width), column.Sort,
                column.Commit, column.Validate,
                column.MinWidth, column.MaxWidth, column.IsVisible,
                column.Kind, column.Checked, column.Toggle, column.Build));
            Rebuild();

            // A saved layout can only be matched once the columns it describes exist, and there is
            // no ordering rule that puts TableKey after them - `new LunaTable<T> { TableKey = "x" }`
            // followed by three Column calls is the shape an object initializer invites. Restore is
            // idempotent and refuses a layout whose column count does not match, so calling it after
            // every column costs two comparisons and removes the ordering trap entirely.
            Restore();
            return this;
        }

        // Replaces the contents and puts the selection back, the same operation LunaList.Refresh
        // performs and for the same reason: "rebuild the list" and "keep the selection" are one
        // thing that only looks like two.
        /// <summary>Replaces every row, keeping the selection if Key still matches something.</summary>
        /// <param name="items">The new models, in display order. Safe to call before the control has a template.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="items"/> is null.</exception>
        public void Refresh(IEnumerable<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            _items = items.ToList();
            Show();
        }

        // Puts _view in the ListBox and the selection back on top of it. Refresh and a header click
        // are the same operation from here down: both replace what is displayed and both have to
        // leave the selection where the user put it.
        //
        // THE SORT IS RE-APPLIED, WHICH IS THE POINT. New rows arriving under an active sort land in
        // sorted order - a table that quietly reverted to arrival order on the next poll would be a
        // table whose sort lasted until the data moved, which for a polling window is about a second.
        private void Show()
        {
            object? wasSelected = Selected is { } previous ? Key(previous) : null;

            _view = Flatten();

            // WHERE EACH ROW SITS ON SCREEN, BUILT ONCE PER REBUILD rather than searched per row.
            // RowHeader is handed the displayed index, and a virtualising list asks for that index
            // once per realised container - so the obvious IndexOf(item) is an O(n) scan run per
            // visible row, which is fine at three rows and is a scan of ten thousand Key() calls per
            // screenful at ten thousand. First entry wins, for the same reason the selection lookup
            // takes the first match: a caller whose Key is not unique has one bug, not two.
            _position.Clear();
            if (_rowHeader is not null)
            {
                for (int i = 0; i < _view.Count; i++) _position.TryAdd(KeyOf(_view[i]), i);
            }

            if (Rows is null) return;

            using (_filling.Suppress())
            {
                Rows.ItemsSource = _view;

                // Null when the previously selected row is gone, which is a real answer rather
                // than a failure to restore: selecting its neighbour would be a guess.
                Rows.SelectedItem = wasSelected is null
                    ? null
                    : _view.FirstOrDefault(item => Equals(Key(item), wasSelected));
            }

            DropCellsThatLeft();
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

        // ORDERBY AND NOT List<T>.Sort, and the difference is visible rather than academic.
        // List<T>.Sort is an unstable introsort: rows that compare equal come out in an arbitrary
        // order that changes between runs, so a table sorted by a column with ties reshuffles its
        // equal rows every time it is refreshed. LINQ's OrderBy is documented stable, so ties keep
        // the order the caller gave to Refresh.
        //
        // It also projects rather than reorders: _items stays in arrival order, so the third click
        // on a header has somewhere to return to. Sorting in place would make the unsorted state
        // unreachable, which is most of why §27 chose a three-state cycle at all.
        private IReadOnlyList<T> Ordered(IReadOnlyList<T> level)
        {
            if (_sortColumn < 0 || _sortColumn >= _columns.Count) return level;
            if (_columns[_sortColumn].Sort is not { } comparison) return level;

            var comparer = Comparer<T>.Create(comparison);
            return _sortDescending
                ? level.OrderByDescending(item => item, comparer).ToList()
                : level.OrderBy(item => item, comparer).ToList();
        }

        // THE TREE, FLATTENED INTO THE LIST THE ListBox ACTUALLY SHOWS - see docs/LunaP.md §55.
        //
        // A ListBox displays a sequence, so hierarchy has to become one: parents followed by their
        // visible children, each row remembering how deep it is. Everything else about the control -
        // selection by model, editing, the row's spoken name, virtualization - keeps working on a
        // flat sequence of T and never learns that a tree exists.
        //
        // THE FLAT CASE IS THE OLD CASE, EXACTLY. No Children means this returns Ordered(_items) and
        // returns it before touching any of the three dictionaries, so a table that is not a tree
        // does no extra work and allocates nothing new (§26.13).
        private IReadOnlyList<T> Flatten()
        {
            if (_children is null) return Ordered(_items);

            _depth.Clear();
            _expandable.Clear();

            var flat = new List<T>(_items.Count);
            Walk(_items, 0, flat, new HashSet<object>());
            return flat;
        }

        // SORTED AT EVERY LEVEL, WHICH IS THE ONLY READING THAT MAKES SENSE. Sorting the flattened
        // list would interleave children with strangers' parents and destroy the tree; sorting only
        // the roots would leave every child list in arrival order under a header the user just
        // clicked. Each level is ordered among its own siblings.
        //
        // `path` IS A CYCLE GUARD AND NOT AN OPTIMISATION. Children is a caller's delegate and
        // nothing stops it returning an ancestor - a parent index built from a bad file, a symlink
        // loop in a directory walk. Without this, the first such model is a StackOverflowException,
        // which cannot be caught and takes the application with it. With it, the repeat is dropped
        // and the rest of the table still draws.
        private void Walk(IReadOnlyList<T> level, int depth, List<T> into, HashSet<object> path)
        {
            foreach (T item in Ordered(level))
            {
                object key = KeyOf(item);
                if (!path.Add(key)) continue;

                _depth[key] = depth;
                into.Add(item);

                IReadOnlyList<T> kids = ChildrenOf(item);
                if (kids.Count > 0)
                {
                    _expandable.Add(key);
                    if (_expanded.Contains(key)) Walk(kids, depth + 1, into, path);
                }

                path.Remove(key);
            }
        }

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

        // Writes the layout now rather than waiting for the drag to settle. Exists for the same
        // reason SplitPane.SaveNow does: a window closing does not wait for a debounce.
        /// <summary>Writes the column widths and sort immediately, rather than waiting for a drag to settle. Does nothing without a TableKey.</summary>
        public void SaveNow()
        {
            _save?.Cancel();

            if (TableKey is not { } key || _columns.Count == 0) return;

            TableLayoutStore.Update(key, layout =>
            {
                layout.Widths = _columns.Select(c => c.Width.ToString()).ToList();
                layout.SortedBy = _sortColumn >= 0 && _sortColumn < _columns.Count ? _columns[_sortColumn].Header : null;
                layout.Descending = _sortDescending;
            });
        }

        // MATCHED BY HEADER AND BY COUNT, and both halves matter. A saved layout describes the
        // columns that existed when it was written; a caller who has since added, removed or
        // renamed one is describing a different table, and applying half a layout to it would move
        // widths onto the wrong columns and point the sort arrow confidently at the wrong heading.
        //
        // The safe answer to a layout that does not match is to ignore it. A user loses the column
        // widths they dragged once, after the application changed its own table - which is a good
        // deal better than a table that comes back scrambled and cannot be explained.
        private void Restore()
        {
            if (_restored || TableKey is not { } key || _columns.Count == 0) return;
            if (TableLayoutStore.Load(key) is not { } layout) return;
            if (layout.Widths.Count != _columns.Count) return;

            // PARSED IN FULL BEFORE ANY OF IT IS APPLIED, and there is no GridLength.TryParse to
            // lean on - Parse throws. A hand-edited or truncated tables.json that is good for two
            // columns and garbage for the third would otherwise leave the table half restored, which
            // is the state this method exists to avoid. All or nothing, and nothing is a table that
            // looks the way the caller declared it.
            var widths = new GridLength[_columns.Count];
            for (int i = 0; i < _columns.Count; i++)
            {
                try
                {
                    widths[i] = GridLength.Parse(layout.Widths[i]);
                }
                catch (FormatException)
                {
                    return;
                }
            }

            _restored = true;

            for (int i = 0; i < _columns.Count; i++)
            {
                _columns[i] = _columns[i] with { Width = widths[i] };
            }

            // Only a column that still has a comparison can be the sorted one. A caller who made a
            // column unsortable between releases gets an unsorted table rather than a sort arrow on
            // a heading that cannot be clicked.
            int sorted = layout.SortedBy is null
                ? -1
                : _columns.FindIndex(c => c.Header == layout.SortedBy && c.Sort is not null);

            _sortColumn = sorted;
            _sortDescending = sorted >= 0 && layout.Descending;

            Rebuild();
            Show();
        }

        protected override void OnPartsAttached()
        {
            if (Rows is null) return;

            Rows.SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(Selected);
            };

            // A recycled container keeps the ColumnDefinitions it was built with, so a row scrolling
            // back into view after a resize would come back at the old widths. This is the hook that
            // catches it - and it fires for every container, so a row realized for the first time
            // after a drag is covered by the same line.
            // CLICKING A CELL SELECTS THAT CELL - see docs/LunaP.md §67.
            //
            // On the table and bubbling, rather than a handler per cell. A cell can be a TableCell, a
            // CheckBox or any control a caller returned from Build (§57), so there is no one type to
            // attach to - and a pointer that goes down on something INSIDE a template cell has to
            // count as a click on the cell, which walking up from the source is exactly how to decide.
            //
            // Never handled. A check cell's box still toggles, a template cell's own button still
            // fires, and a double-click still opens an editor: selecting is something that happens as
            // well as the click, not instead of it.
            AddHandler(
                Avalonia.Input.InputElement.PointerPressedEvent,
                (_, e) => ClickedCell(e),
                Avalonia.Interactivity.RoutingStrategies.Bubble);

            Rows.ContainerPrepared += (_, e) =>
            {
                Widen(e.Container);


                // AND THE ROW'S NAME, which has to happen here rather than in Row(): the grid is
                // built before anything parents it, so at that moment there is no container to put
                // the name on. This fires for every container including recycled ones, so a row
                // scrolled back into view is renamed for whatever model it now holds. §50.5.
                if (e.Container.DataContext is T model)
                {
                    AutomationProperties.SetName(e.Container, Spoken(model));
                    RowPrepared?.Invoke(model, e.Container);
                }
            };

            Rows.ContainerClearing += (_, e) =>
            {
                if (e.Container.DataContext is T model) RowClearing?.Invoke(model, e.Container);
            };

            // THE HEADER FOLLOWS THE ROWS SIDEWAYS - see docs/LunaP.md §59.
            //
            // The rows scroll inside the ListBox's own ScrollViewer; the header is the one part of
            // this control outside it, so it is the one part that has to be moved by hand. A render
            // transform rather than a margin or a second ScrollViewer: it is the cheapest thing that
            // moves a laid-out subtree, it costs no measure pass, and it cannot disturb the shared
            // size groups the header and rows use to line up at all (§27.10).
            //
            // ScrollChanged is a ROUTED event, so this catches the viewer inside the ListBox's
            // template without having to find it - which cannot be done here anyway, because the
            // ListBox has not applied its own template at this point.
            Rows.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) =>
            {
                // The event is a nudge and nothing more - what scrolled and by how much is read in
                // Pin, from the one viewer this control owns. Taking either from the event is what
                // §64.3 records going wrong.
                Pin();
            });

            // AND AGAIN AFTER EVERY LAYOUT, which is not belt-and-braces. A clip is computed from a
            // child's arranged Bounds, and the two moments that produce new children with no bounds
            // yet are exactly the two this control does not otherwise hear about: a row realised by
            // the virtualising panel while scrolling vertically, and a column resized under the
            // header. LayoutUpdated is the signal that everything has bounds again.
            //
            // Cheap when it does not apply: Pin returns on its first line for a table with no frozen
            // columns, which is every table that has not asked for them.
            //
            // AND THE CELL BOXES, for exactly the same reason and it took the same wrong turn first.
            // A row realised while scrolling, and a row rebuilt by Refresh, both arrive through
            // ContainerPrepared - where there is no row grid to put a box in yet. This is the hook
            // where everything exists. MarkCells early-outs just as sharply. §67.5.
            LayoutUpdated += (_, _) =>
            {
                Pin();
                MarkCells();
            };

            // Bubbling, so it catches a heading, a cell editor and a caller's own control inside a
            // template column without any of them being registered. Only a flag is set: the
            // correction needs the ScrollViewer to have finished its own BringIntoView, and that has
            // not happened yet when this fires.
            AddHandler(
                GotFocusEvent,
                (_, _) => _focusMoved = true,
                Avalonia.Interactivity.RoutingStrategies.Bubble);

            ApplySelectionMode();
            Rebuild();
            Restore();

            // Items set before the template existed - a caller filling the table from its
            // constructor, which is the normal way a window is built in this toolkit.
            if (_items.Count > 0) Show();

            // A cell selected before there was a template. The cells themselves were kept all along -
            // they are held by key rather than by visual - but the row under the current one was
            // never put on the ListBox, because there was no ListBox. Without this, a table
            // configured from a constructor comes up with a boxed cell and Selected reading null.
            Sync();

            if (!_hasPending) return;

            _hasPending = false;
            Select(_pending);
            _pending = null;
        }

        private void Rebuild()
        {
            if (HeaderGrid is null || Rows is null || _columns.Count == 0) return;

            // One group name per column, unique to this table, so two tables on one page do not
            // silently size each other's columns.
            string scope = "LunaTable" + GetHashCode().ToString("X");

            Define(HeaderGrid, scope);
            HeaderGrid.Children.Clear();
            _heads.Clear();

            // THE CORNER, and it is a plain label rather than a heading Button even when every other
            // column is sortable. There is nothing to sort by: the gutter's contents are positions or
            // addresses, and "sort by row number" is either the identity or a lie. A button that
            // takes focus and does nothing is worse for a keyboard user than not being a stop at all,
            // which is the same argument Heading makes for an unsortable column (§27.3).
            if (_rowHeader is not null)
            {
                var corner = new TextBlock
                {
                    Text = RowHeaderCaption,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                corner.Classes.Add("row-header");
                Grid.SetColumn(corner, 0);
                HeaderGrid.Children.Add(corner);
            }

            // The last column with anything on its right. A grip after it would have nothing to
            // give the space to, and a hidden column is not something on its right - so with the
            // final two columns hidden, the grip belongs after the last one you can see.
            int lastVisible = LastVisibleColumn;

            for (int i = 0; i < _columns.Count; i++)
            {
                // A hidden column contributes no heading and no grip. Its ColumnDefinition is still
                // there, pinned to zero, so every index after it is unmoved.
                if (!_columns[i].IsVisible) continue;

                Control cell = Heading(i);
                Grid.SetColumn(cell, GridColumn(i));
                HeaderGrid.Children.Add(cell);

                if (i < lastVisible) HeaderGrid.Children.Add(Grip(i));
            }

            AddFrozenEdge(HeaderGrid);

            Rows.ItemTemplate = new FuncDataTemplate<T>((item, _) => Row(item, scope), supportsRecycling: true);
            ShowSortState();
        }

        // A GRIDSPLITTER AND NOT A THUMB, for the same reason a heading is a Button: a column width
        // a mouse can change and a keyboard cannot is a feature half the users of this toolkit do
        // not have. GridSplitter handles arrow keys, takes focus and carries an accessible name;
        // Thumb is the lighter primitive and gives a drag and nothing else.
        //
        // It sits in the column it resizes, aligned right, four pixels wide, so it straddles the
        // boundary with its neighbour. §26.6 made the same choice for SplitPane's divider and §26.11
        // records what happens when one loses its name.
        private Control Grip(int index)
        {
            var grip = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                Background = Avalonia.Media.Brushes.Transparent,
            };

            grip.Classes.Add("grip");
            AutomationProperties.SetName(grip, $"Resize {_columns[index].Header}");

            // DragDelta rather than DragCompleted, so the rows follow the pointer instead of
            // snapping when it is released. The save is debounced underneath, so a drag is one
            // write and not one per frame.
            grip.DragDelta += (_, _) => Resized();
            grip.DragCompleted += (_, _) => Resized();

            Grid.SetColumn(grip, GridColumn(index));
            return grip;
        }

        // The splitter edits the HEADER's definitions, and this is what makes that reach the rows.
        //
        // MEASURED, NOT ASSUMED: a width set on the header alone does not propagate. Only Auto
        // columns share a size group (§27.10), so a star or absolute column's header definition and
        // its row definitions are unrelated objects - setting the header's column 0 to 150 left
        // every row at 404 and put the cells 253 pixels right of their headings. So the header is
        // read back into the column specs, which are the one source of truth, and every realized row
        // is brought into line from there.
        private void Resized()
        {
            if (HeaderGrid is null) return;

            // Reads DEFINITIONS BY GRID INDEX and writes SPECS BY COLUMN INDEX, which is the whole
            // reason GridColumn exists: with a gutter in front, definition 0 is the gutter and
            // column 0 is definition 1. Reading them off by one puts every dragged width onto its
            // left-hand neighbour and saves that to disk.
            for (int i = 0; i < _columns.Count; i++)
            {
                int at = GridColumn(i);
                if (at >= HeaderGrid.ColumnDefinitions.Count) break;

                GridLength width = HeaderGrid.ColumnDefinitions[at].Width;
                if (width != _columns[i].Width) _columns[i] = _columns[i] with { Width = width };
            }

            if (Rows is not null)
            {
                foreach (Control container in Rows.GetRealizedContainers()) Widen(container);
            }

            _save ??= new Debounce(TimeSpan.FromMilliseconds(400), SaveNow);
            _save.Poke();
        }

        // FROZEN COLUMNS, AND THE TWO RULES THEY ARE - see docs/LunaP.md §61.
        //
        // §59.3 said this needed a different control and §60 records why that was wrong. The whole
        // mechanism is two lines of geometry applied to the row grid's DIRECT CHILDREN:
        //
        //   - a child in a frozen column is translated by +scrollX, which cancels the scroll the
        //     ScrollContentPresenter is applying to everything and leaves it where it started;
        //   - a child in a scrolling column is CLIPPED so that whatever would fall inside the frozen
        //     band is not drawn at all.
        //
        // CLIPPED AND NOT COVERED, which is the part worth understanding. A frozen cell painted over
        // its neighbours would need an opaque backdrop, and the row's backdrop is Fluent's selected
        // and pointer-over fill, which is not reachable without reaching into a template §48 refuses
        // to touch. Removing the neighbour instead means the thing behind both of them - that same
        // Fluent fill - carries on showing through, and nothing has to be matched at all. Measured at
        // 6,399 red pixels and ZERO blue inside the band (§60.1).
        //
        // THE DIRECT CHILDREN ARE THE RIGHT GRANULARITY because every one of them already carries a
        // Grid.Column, so a bare cell, a cell inside an expander panel (§55), a vertical rule (§56.2),
        // a resize grip and an open editor are all handled without one of them being a special case.
        //
        // BOUNDS RATHER THAN COLUMN OFFSETS. The child's own Bounds.X is where it actually sits in
        // the grid, which already accounts for its alignment, its margin and any column span - a
        // GridSplitter is aligned right inside its column and would be placed wrongly by arithmetic
        // over column starts.
        //
        // Render-level, both of them: Clip and RenderTransform affect drawing and not layout, so this
        // costs no measure and no arrange, and the shared size groups that line the header up with
        // the rows (§27.10) never learn it happened.
        private void Pin()
        {
            // THE OFFSET IS READ HERE AND NOWHERE ELSE - see docs/LunaP.md §64.2, which is a
            // correction to §59.2.
            //
            // §59 cached the offset from the ScrollChanged event's own viewer, and that number is
            // not always the one the viewer has settled on. Measured: a scroll caused by
            // BringIntoView - which is what Tab, F2 and Edit all provoke - raises ScrollChanged
            // reporting 0 while the viewer is already at 612, and no later event corrects it. The
            // header stayed put and every heading sat 612 pixels from its own cells, silently,
            // whenever a scroll was caused by anything other than the user dragging the bar.
            //
            // Reading the live offset at layout time cannot be stale, because layout is what happens
            // after the offset settles. The event is kept for promptness and for catching the viewer
            // out of the ListBox's template, and its Offset is now ignored.
            // NOT ANY ScrollViewer UNDER THE ROWS, AND NOT THE EVENT'S EITHER - see §64.3. A TextBox
            // has a ScrollViewer inside its own template, so the moment a cell editor is opened its
            // inner viewer raises ScrollChanged, that event bubbles to PART_Rows, and a handler that
            // trusted e.Source pointed this control at the editor's scroller from then on. Its offset
            // is always zero, so the header stopped following the rows and every pin went stale -
            // measured as a table sitting at offset 600 with _scrolledTo reading 0.
            //
            // The one this control owns is the one that is not inside a row, which is what the filter
            // says. Resolved once and kept, because it comes from the ListBox's template and cannot
            // be found before that template is applied.
            _viewer ??= Rows?.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault(scroller => !scroller.GetVisualAncestors().OfType<ListBoxItem>().Any());

            double offset = _viewer?.Offset.X ?? 0;
            if (Math.Abs(_scrolledTo - offset) > 0.01)
            {
                _scrolledTo = offset;

                // Null at rest, so an unscrolled table has the tree it always had.
                if (HeaderGrid is not null)
                {
                    HeaderGrid.RenderTransform = offset == 0 ? null : new TranslateTransform(-offset, 0);
                }
            }

            // Nothing asked for and nothing ever set: the common table does not pay for this feature
            // existing, and this is the line that makes that true. It tests what the caller ASKED
            // for rather than what was granted, because a refused band still has a seam in every row
            // to hide - and the flag beside it, because turning frozen columns off has to clear what
            // was set once, and then stop.
            int asked = FrozenGridColumns;
            if (asked <= 0 && !_pinned) return;

            int frozen = asked;

            // FREEZING MUST NEVER TAKE SCROLLING AWAY - see docs/LunaP.md §64.
            //
            // A frozen band as wide as the viewport leaves the scrolling columns nowhere to be: they
            // are clipped to nothing at every offset, and the table is back to §59's defect, where
            // columns exist and no scrollbar, wheel or key can reach them. Measured at two frozen
            // columns of 300 in a 400-wide table - band 600, viewport 400, and columns 2 upwards
            // fully clipped at maximum scroll.
            //
            // So a band that does not leave room freezes NOTHING. Freezing is a refinement of
            // scrolling and does not get to remove it; a caller who freezes too much, or a user who
            // drags the window narrow, gets an ordinary scrolling table rather than an unusable one.
            // It comes back by itself when there is room again, because this is recomputed per pass
            // rather than latched.
            if (frozen > 0 && !LeavesRoom(frozen)) frozen = 0;

            _pinned = frozen > 0;

            if (HeaderGrid is not null) Pin(HeaderGrid, frozen);
            if (Rows is null) return;

            foreach (Control container in Rows.GetRealizedContainers())
            {
                if (RowGridIn(container) is { } grid) Pin(grid, frozen);
            }

            if (!_focusMoved) return;

            _focusMoved = false;
            ClearFocusFromBand(frozen);
        }

        // NOTHING MAY TAKE FOCUS WHERE NOBODY CAN SEE IT - see docs/LunaP.md §62.
        //
        // A ScrollViewer brings a newly focused control into view by scrolling the least it can to
        // put that control inside the VIEWPORT, and the viewport's left edge is zero. It knows
        // nothing about a band of frozen columns sitting over the first two hundred pixels of it, so
        // "just visible at the left" means "exactly underneath them". Measured: tabbing to a button
        // in column 1 of a table scrolled to 824 left it focused, at x=0, with a clip of zero width -
        // a control holding the keyboard focus and drawing nothing at all. That is §24's failure in a
        // new place, and it is the reason this pass exists rather than a refinement of pass 1.
        //
        // THE CLIP CANNOT SUPPLY THE CORRECTION, which is worth knowing before someone simplifies
        // this. Pin clamps the hidden amount to the child's own width, because a clip rectangle
        // cannot be wider than what it clips; the amount needed to clear the band is the UNCLAMPED
        // overlap, and for a fully hidden control those two are different numbers. Reading it back
        // off Clip.Bounds.X scrolls a narrow control by its own width and leaves it under the band.
        //
        // Scrolling by exactly the overlap lands the control on the band's edge and no further: it
        // is the smallest move that makes it visible, which is what BringIntoView was trying to do.
        private void ClearFocusFromBand(int frozen)
        {
            if (frozen <= 0 || _viewer is null) return;
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused) return;

            foreach (Visual step in focused.GetSelfAndVisualAncestors())
            {
                if (ReferenceEquals(step, this)) return;

                // The grid child is where a pin lives, so it is the thing whose overlap counts - the
                // focused control itself may be nested inside a template cell.
                if (step is not Control child || child.Parent is not Grid grid) continue;
                if (Grid.GetColumn(child) < frozen) return;

                double overlap = _scrolledTo + BandOf(grid, frozen) - child.Bounds.X;
                if (overlap > 0.5)
                {
                    _viewer.Offset = new Vector(Math.Max(0, _scrolledTo - overlap), _viewer.Offset.Y);
                }

                return;
            }
        }

        // Raised for anything inside the table, including a heading, a cell editor and a caller's own
        // control in a template column. Handled on the next layout rather than here, because the
        // ScrollViewer's own BringIntoView has not run yet at this point and correcting a position it
        // is about to change would be corrected right back.
        private bool _focusMoved;

        // The viewer the rows scroll in, kept from the scroll event because it lives inside the
        // ListBox's template and cannot be found before that template is applied.
        private ScrollViewer? _viewer;

        // Whether the frozen band leaves anything for the rest of the table to be seen in. Measured
        // against the viewport the rows actually scroll in, and falling back to the control's own
        // width before that viewer has been found - which is the state during the first layout, when
        // nothing has scrolled and the answer does not matter yet.
        //
        // A zero-width viewport is "not laid out", not "no room": answering false there would unfreeze
        // every table for one pass on the way up.
        private bool LeavesRoom(int frozen)
        {
            if (HeaderGrid is null) return true;

            double viewport = _viewer?.Viewport.Width ?? Bounds.Width;
            if (viewport <= 0) return true;

            return BandOf(HeaderGrid, frozen) < viewport;
        }

        // The band, in a grid's own coordinates. The frozen columns start at zero by definition, so
        // their total width is also where the band ends on screen.
        private static double BandOf(Grid grid, int frozen)
        {
            double band = 0;
            for (int i = 0; i < frozen && i < grid.ColumnDefinitions.Count; i++)
            {
                band += grid.ColumnDefinitions[i].ActualWidth;
            }

            return band;
        }

        private void Pin(Grid grid, int frozen)
        {
            double band = BandOf(grid, frozen);

            foreach (Control child in grid.Children.OfType<Control>())
            {
                // THE SEAM ONLY EXISTS WHILE THE BOUNDARY DOES. It is built into the header and every
                // row from FrozenColumns (§63.1), which is what the caller asked for - but the band
                // can be refused for want of room, and a line drawn where nothing is pinned is a
                // statement about the layout that is not true.
                if (child.Classes.Contains("frozen-edge")) child.IsVisible = frozen > 0;

                if (frozen <= 0)
                {
                    child.RenderTransform = null;
                    child.Clip = null;
                    continue;
                }

                if (Grid.GetColumn(child) < frozen)
                {
                    child.Clip = null;

                    // Null rather than a zero translation, so an unscrolled table has exactly the
                    // visual tree it had before frozen columns existed. It matters more than tidiness
                    // now that a gutter is frozen by default (§63): every table with a row header
                    // would otherwise carry a transform per cell for a scroll that never happened.
                    child.RenderTransform = _scrolledTo == 0
                        ? null
                        : new TranslateTransform(_scrolledTo, 0);
                    continue;
                }

                child.RenderTransform = null;

                Rect bounds = child.Bounds;
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    // Not arranged yet, so there is no honest rectangle to write. The next layout
                    // pass calls this again with real bounds; guessing one now would clip a cell to
                    // nothing and leave it that way until something else moved.
                    child.Clip = null;
                    continue;
                }

                // How much of this child is inside the band. Degenerates correctly: unscrolled,
                // _scrolledTo is zero and a scrolling column starts at or after the band, so this is
                // zero or negative and no clip is set at all.
                double hidden = Math.Clamp(_scrolledTo + band - bounds.X, 0, bounds.Width);

                child.Clip = hidden <= 0
                    ? null
                    : new RectangleGeometry(new Rect(hidden, 0, bounds.Width - hidden, bounds.Height));
            }
        }

        // Whether anything is currently pinned, so that turning frozen columns off clears what was
        // set and a table that never had any does no work at all.
        private bool _pinned;

        private static Grid? RowGridIn(Control container) =>
            container.GetVisualDescendants().OfType<Grid>().FirstOrDefault();

        // Brings one row container's columns into line with the specs. Cheap enough to call per
        // container per drag frame - a virtualized list realizes tens of rows, not thousands (§27.7).
        private void Widen(Control? container)
        {
            if (container is null || RowGridIn(container) is not { } row) return;

            for (int i = 0; i < _columns.Count; i++)
            {
                int at = GridColumn(i);
                if (at >= row.ColumnDefinitions.Count) break;

                if (row.ColumnDefinitions[at].Width != _columns[i].Width)
                {
                    row.ColumnDefinitions[at].Width = _columns[i].Width;
                }
            }
        }

        // A SORTABLE HEADING IS A BUTTON; AN UNSORTABLE ONE STAYS A TEXTBLOCK.
        //
        // The button is not for the look - it is styled flat, and the theme spends more lines taking
        // Fluent's chrome off it than putting anything on. It is there because a heading that only
        // responds to a click is a sort a keyboard user does not have, and §24 is the section about
        // exactly this class of miss. A Button brings focus, Tab, Space and Enter, an invoke peer and
        // a focus adorner, all of which would otherwise be hand-built on a TextBlock and half of
        // which would be forgotten.
        //
        // The converse matters as much: a column with no comparison is left a plain TextBlock rather
        // than made into a button that does nothing. An inert tab stop costs a keyboard user a press
        // and tells them nothing, which is worse than not being a stop at all.
        private Control Heading(int index)
        {
            ColumnSpec column = _columns[index];

            var label = new TextBlock
            {
                Text = column.Header,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (column.Sort is null)
            {
                _heads.Add(new Head(label, null));
                return label;
            }

            // Hidden rather than blank in the unsorted state, and never a neutral "sortable" mark.
            // Three states with a glyph in all three reads as three sorts; two glyphs and nothing
            // reads as what it is - two sorted states and off.
            //
            // Raw in the automation tree because a screen reader announcing "black up-pointing
            // triangle" after the column name is noise. The state is carried on the button's own
            // name instead, where a reader will actually meet it.
            var glyph = new TextBlock
            {
                FontWeight = Avalonia.Media.FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsVisible = false,
            };

            glyph.Classes.Add("sort");
            AutomationProperties.SetAccessibilityView(glyph, AccessibilityView.Raw);

            var button = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { label, glyph },
                },
            };

            button.Classes.Add("heading");

            int clicked = index;
            button.Click += (_, _) => Cycle(clicked);

            _heads.Add(new Head(button, glyph));
            return button;
        }

        // ASCENDING, DESCENDING, THEN BACK TO THE ORDER REFRESH WAS GIVEN.
        //
        // Two states is the commoner convention and this departs from it knowingly. The order a
        // caller hands to Refresh carries meaning in this toolkit far more often than in a database
        // front end - log order, file order, the order a scan found things in - and a two-state
        // cycle makes that order unreachable the moment somebody clicks a header. The cost is a
        // third click that will surprise somebody; the alternative is a table that can lose
        // information the caller deliberately put in it. docs/LunaP.md §27.
        private void Cycle(int index)
        {
            if (_sortColumn != index)
            {
                _sortColumn = index;
                _sortDescending = false;
            }
            else if (!_sortDescending)
            {
                _sortDescending = true;
            }
            else
            {
                _sortColumn = -1;
                _sortDescending = false;
            }

            Show();
            ShowSortState();
        }

        // UPDATES THE HEADINGS IN PLACE AND NEVER REBUILDS THEM, which is a keyboard requirement
        // rather than a performance one. A user who reached a heading with Tab and pressed Space is
        // focused on that button; replacing it with a new one drops focus to the top of the window
        // and leaves them nowhere, having just used the control exactly as intended.
        private void ShowSortState()
        {
            for (int i = 0; i < _heads.Count && i < _columns.Count; i++)
            {
                if (_heads[i].Glyph is not { } glyph) continue;

                bool sorted = i == _sortColumn;
                glyph.IsVisible = sorted;
                glyph.Text = sorted ? (_sortDescending ? "▼" : "▲") : string.Empty;

                AutomationProperties.SetName(
                    _heads[i].Cell,
                    sorted
                        ? $"{_columns[i].Header}, sorted {(_sortDescending ? "descending" : "ascending")}"
                        : $"{_columns[i].Header}, not sorted");
            }
        }

        private Control Row(T? item, string scope)
        {
            var grid = new Grid();
            Define(grid, scope);

            if (item is null) return Ruled(grid);

            // THE GUTTER, AND IT IS NOT A CELL. No column marker goes on it, so it never answers to
            // Cell(item, n), never takes an editor and is not one of the things TryGetCell finds -
            // which is what keeps a caller's column indices meaning what they meant before there was
            // a gutter. Its width and its caption are the table's, not a column's, and it has no
            // resize grip because §27.11's remembered layout is a list of COLUMN widths. §58.
            if (_rowHeader is not null)
            {
                var gutter = new TextBlock
                {
                    Text = _rowHeader(item, PositionOf(item)),
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                gutter.Classes.Add("row-header");

                // Raw, because the row's own sentence already begins with this label and a reader
                // that met both would hear the address twice before reaching the first column. The
                // sort glyph is hidden for the same reason (§27.3), and MeterRow's inner bar for a
                // closely related one (§24.2).
                AutomationProperties.SetAccessibilityView(gutter, AccessibilityView.Raw);

                Grid.SetColumn(gutter, 0);
                grid.Children.Add(gutter);
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                // Nothing is built for a hidden column - not a zero-width control, nothing. A cell
                // that exists and cannot be seen still costs a measure pass per row per frame.
                if (!_columns[i].IsVisible) continue;

                // ONE PLACE THAT DECIDES WHAT A CELL IS, and everything after it is the same for
                // all three kinds: the index marker, the column, the expander, the rule. A kind that
                // needed a second branch further down would be a kind that had leaked. §57.
                int index = i;
                Control cell = _columns[i].Kind switch
                {
                    LunaCellKind.Check => CheckCell(item, index),
                    LunaCellKind.Template => TemplateCell(item, index),
                    _ => TextCell(item, index),
                };

                // The expander column gets the indent and the toggle in front of its text; every
                // other column, and every column of a flat table, gets the bare cell exactly as
                // before.
                Control placed = _children is not null && i == ExpanderColumn
                    ? Expander(item, cell, index)
                    : cell;

                // THE MARKER GOES ON THE CELL AND THE COLUMN GOES ON WHAT THE GRID HOLDS, and those
                // are two different controls whenever the expander wraps one. Grid reads its attached
                // properties off its own direct children, so a Grid.SetColumn left on the inner cell
                // is a number nothing ever reads - the wrapper takes the default of 0 and the row
                // draws that cell on top of column 0's. §66 has the measurement; it was invisible for
                // as long as it was, because every test set ExpanderColumn to 0 and 0 is the default.
                //
                // The marker stays on the cell on purpose: Cell() walks descendants to find it, so it
                // is reachable through the wrapper, and putting it on the wrapper instead would make
                // a template cell's own children answer for the column.
                TableCells.SetColumn(cell, i);
                Grid.SetColumn(placed, GridColumn(i));
                grid.Children.Add(placed);

                if (_gridLines.HasFlag(LunaGridLines.Vertical) && i < LastVisibleColumn)
                {
                    Control rule = ColumnRule();
                    Grid.SetColumn(rule, GridColumn(i));
                    grid.Children.Add(rule);
                }
            }

            AddFrozenEdge(grid);
            NameRow(grid, item);
            return Ruled(grid);
        }

        // Puts the seam in the last frozen column of a grid, or leaves the grid alone when nothing is
        // frozen. Both the header and every row go through here, so the two cannot disagree about
        // where the boundary is.
        private void AddFrozenEdge(Grid grid)
        {
            int frozen = FrozenGridColumns;
            if (frozen <= 0) return;

            Control edge = FrozenEdge();
            Grid.SetColumn(edge, frozen - 1);
            grid.Children.Add(edge);
        }

        // The text cell, which is what every cell was before §57 - unchanged but for being one arm
        // of a switch rather than the whole of the loop.
        private TableCell TextCell(T item, int index)
        {
            var cell = new TableCell
            {
                Text = _columns[index].Text(item) ?? string.Empty,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,

                // Read through the projection so a reader always gets the model's current value,
                // and Write only where the column can be committed to - which is what makes
                // IValueProvider.IsReadOnly answer honestly per column. §50.6.
                Read = () => _columns[index].Text(item) ?? string.Empty,
                Write = _columns[index].IsEditable
                    ? text => SetFromAutomation(item, index, text)
                    : null,
            };

            // DOUBLE-CLICK OPENS THE EDITOR, and the handler goes on the CELL rather than on the
            // row because the cell is the only thing that knows which column was hit. Doing it
            // on the row would mean hit-testing the pointer's x against the column boundaries -
            // arithmetic that has to be kept in step with the Grid, to answer a question the
            // Grid has already answered by delivering the event here.
            //
            // Captured rather than looked up: `cell` and `item` in this closure are the live
            // visual and its model, so a recycled row's handler refers to that row's own cell.
            if (_columns[index].IsEditable)
            {
                cell.DoubleTapped += (_, e) =>
                {
                    if (!EditGestures.HasFlag(LunaEditGestures.DoubleTap)) return;

                    BeginEdit(item, index, cell);
                    e.Handled = true;
                };
            }

            return cell;
        }

        // A STOCK CheckBox AND NOT A CELL TYPE OF THIS TOOLKIT'S OWN, which is the same argument that
        // made a sortable heading a Button (§27.3): Avalonia's CheckBox already brings focus, Space,
        // a focus adorner and an IToggleProvider peer, and a hand-rolled tick would have to reproduce
        // all four and would forget two. The only things added here are the name and the gate.
        //
        // IsEnabled IS THE READ-ONLY MECHANISM, AND IT IS THE ONLY ONE THAT WORKS. Measured on
        // Avalonia 12.1.0: IToggleProvider.Toggle() throws ElementNotEnabledException on a disabled
        // control, and does NOT on one that is merely IsHitTestVisible=false - so the version that
        // kept full contrast by refusing the pointer would have left a read-only cell a screen reader
        // could still flip. That is the §50.6 defect exactly, and it was one design decision away.
        // The contrast cost is paid in FluentBridge.axaml instead. §57.3.
        private Control CheckCell(T item, int index)
        {
            ColumnSpec column = _columns[index];

            var box = new CheckBox
            {
                IsChecked = column.Checked?.Invoke(item) == true,
                IsEnabled = column.Toggle is not null,
                VerticalAlignment = VerticalAlignment.Center,

                // LEFT, NOT STRETCHED, and that is a behaviour rather than a look. A CheckBox
                // defaults to filling its slot, so in a column two hundred pixels wide the whole cell
                // becomes the toggle - and a user aiming at the row to select it ticks a box instead.
                // Left-aligned, the box is the target and the rest of the cell still selects the row.
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0),
            };

            box.Classes.Add("cell-check");

            // The header, because a checkbox with no content has nothing else to say what it is - a
            // reader landing on it would otherwise hear "checkbox, checked" with no column name. The
            // row's own sentence carries the whole row; this is what the cell says on its own.
            AutomationProperties.SetName(box, column.Header);

            box.IsCheckedChanged += (_, _) => Toggled(item, index, box);
            return box;
        }

        // THE MODEL IS THE TRUTH AND THE BOX IS A VIEW OF IT, which is why this writes and then reads
        // back rather than trusting what the user just clicked. Two things fall out of that and both
        // are wanted: a Toggle that normalises - one that turns three flags on together - shows what
        // it actually did, and a Toggle that REFUSES leaves the model alone and the tick returns to
        // where it was, with no separate veto mechanism to build. It is the same rule as Close
        // re-reading a committed cell through the projection instead of keeping the typed text.
        //
        // The suppressor is not optional: putting the box back raises IsCheckedChanged again, and
        // without it a refused toggle calls the caller's delegate forever.
        private void Toggled(T item, int index, CheckBox box)
        {
            if (_toggling.IsSuppressing) return;

            ColumnSpec column = _columns[index];
            if (column.Checked is not { } read) return;

            bool before = read(item);
            column.Toggle?.Invoke(item, box.IsChecked == true);
            bool after = read(item);

            using (_toggling.Suppress()) box.IsChecked = after;

            if (RowGridOf(box) is { } grid) NameRow(grid, item);

            // ONLY WHEN THE MODEL ACTUALLY MOVED. CellValueChanged says a value was committed, and a
            // refused toggle committed nothing - raising it anyway would make "changed" mean "was
            // clicked", which is a different event and one nobody asked for.
            if (before != after) CellValueChanged?.Invoke(item, index);
        }

        // WHATEVER THE CALLER BUILT, UNWRAPPED. No ContentControl around it and no Border: the
        // control the caller returned is the control in the row, so its own margins, alignment and
        // automation are what they look like at the call site rather than being negotiated with a
        // host this toolkit put in the way.
        //
        // A null return is an EMPTY cell rather than a throw, which is the same tolerance Text gets
        // two lines up (`?? string.Empty`). A build that has nothing to show for one row - no icon
        // for an unknown kind - should not have to invent a blank control.
        private Control TemplateCell(T item, int index) =>
            _columns[index].Build?.Invoke(item) ?? new Border();

        // THE HORIZONTAL RULE IS ONE BORDER AROUND THE ROW, not a line per cell, because a rule
        // under a row is a property of the row - drawing it per cell would break wherever a column
        // is hidden and leave the gap unruled. The vertical rules ARE per cell, because that is
        // what a column boundary is.
        //
        // Returns the grid untouched when there are no lines, so a table that draws none has no
        // extra Border in its tree at all rather than a transparent one per row.
        // The last column with anything to its right, which is what decides where a vertical rule
        // and a resize grip stop. A hidden column is not something on the right.
        private int LastVisibleColumn
        {
            get
            {
                int last = -1;
                for (int i = 0; i < _columns.Count; i++) if (_columns[i].IsVisible) last = i;
                return last;
            }
        }

        private Control Ruled(Grid grid)
        {
            if (!_gridLines.HasFlag(LunaGridLines.Horizontal)) return grid;

            var ruled = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = grid,
            };

            // Styled by class in LunaTable.axaml rather than given a brush here, so a host theme can
            // restyle a rule the same way it restyles anything else (§12.2).
            ruled.Classes.Add("row-rule");
            return ruled;
        }

        // WHERE THE PINNED COLUMNS STOP, DRAWN SO SOMEBODY CAN SEE IT - see docs/LunaP.md §63.
        //
        // Without this, frozen columns are invisible until the table is scrolled: the user is given
        // a layout that behaves differently on the left and is told nothing about it until they
        // discover it. So the edge is drawn whether or not anything has been scrolled yet.
        //
        // A SIBLING IN THE LAST FROZEN COLUMN, WHICH IS THE WHOLE IMPLEMENTATION. It is the same
        // shape as a vertical grid rule (§56.2) - a Border in the column, aligned right - and because
        // it sits in a frozen column, Pin translates it with everything else in there. There is no
        // positioning code for the seam at all, and no way for it to drift from the boundary it
        // marks: it IS the right-hand edge of that column.
        //
        // LunaBorder, the same token the grid rules take, rather than a colour of its own. §56.2's
        // argument applies unchanged - it is where one surface stops and the next begins, and it is
        // already held to 3:1 against both. When vertical rules are also on, the two coincide exactly
        // rather than doubling up, because both are one pixel aligned to the same edge.
        private static Control FrozenEdge()
        {
            var edge = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            edge.Classes.Add("frozen-edge");
            return edge;
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

        // A SIBLING IN THE COLUMN, NOT A WRAPPER AROUND THE CELL, and that is load-bearing rather
        // than stylistic. Wrapping would make a cell's parent a Border, and Border is a Decorator
        // rather than a Panel - which is exactly what BeginEdit needs the parent to be in order to
        // put the editor in the cell's place (§55.7). A GridSplitter already sits in its column the
        // same way (§27.11), so this is the shape the control was already using.
        private static Control ColumnRule() 
        {
            var rule = new Border
            {
                Width = 1,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            rule.Classes.Add("column-rule");
            return rule;
        }

        // EDITING, AND THE THREE WAYS IT ENDS - see docs/LunaP.md §50.
        //
        // The editor is put INTO the row's existing grid, over the cell it replaces, rather than the
        // row being rebuilt with an editor in it. That is not an optimisation, it is what keeps
        // trap 2 in PLAN-table.md §6 closed: rebuilding the view means calling Show(), Show()
        // re-applies the sort, and a row whose sorted column has just been edited would leap to a
        // different position with the caret still in it. Nothing here touches _items, _view or the
        // sort, so a committed edit changes a value and moves nothing.
        //
        // The state is (item, column, cell) and not a row index, because an index is only true until
        // the list is refreshed under it.
        private T? _editItem;
        private int _editColumn = -1;
        private TextBox? _editor;
        private TableCell? _editCell;

        // EndEdit can be re-entered - committing moves focus, which raises LostFocus, which commits -
        // and this is what makes the second call a no-op instead of a second commit.
        private bool _ending;

        // Which column a commit just wrote, held only across Close so that CellValueChanged is
        // raised AFTER the cell has been re-read and the row renamed - a handler that looks at the
        // table should see the finished state rather than the middle of the update.
        private int _committed = -1;

        /// <summary>Whether a cell is currently being edited.</summary>
        public bool IsEditing => _editor is not null;

        // Opens an editor on a cell. Public because F2 is not the only way an application might want
        // to start one - a "Rename" menu item is the obvious other - and because the alternative is a
        // consumer synthesising a double-click.
        /// <summary>Opens an editor on one cell of one row, if that column has a Commit.</summary>
        /// <param name="item">The row's model.</param>
        /// <param name="column">The column index, in the order the columns were added.</param>
        /// <remarks>
        /// Does nothing, rather than throwing, when the column is read-only, the index is out of range,
        /// or the row is not currently realised - a row scrolled out of view has no cell to put a caret
        /// in. Nothing is queued for later either; see docs/LunaP.md §50.3.
        /// </remarks>
        public void Edit(T item, int column)
        {
            if (item is null || column < 0 || column >= _columns.Count) return;
            if (TextCellOf(item, column) is not { } cell) return;

            BeginEdit(item, column, cell);
        }

        // F2 EDITS THE SELECTED ROW, because double-click is a mouse gesture and a table whose cells
        // can only be opened with a pointer is a table half this toolkit's users cannot edit - the
        // same argument that made a sortable heading a Button and a column grip a GridSplitter
        // rather than a Thumb (§24).
        //
        // WHICH COLUMN DEPENDS ON THE UNIT, AND UNTIL §67 THERE WAS ONLY ONE ANSWER. This comment
        // used to read "the FIRST editable column, and not the selected one, because this control has
        // no concept of a focused cell" - which was true, and is the thing cell selection changed. In
        // a Cell unit there is a focused cell and F2 opens it; in a Row unit there is still no such
        // thing and the first editable column is still the only honest guess.
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled || IsEditing) return;

            if (_selectionUnit == LunaSelectionUnit.Cell && MoveCell(e)) return;

            if (e.Key != Avalonia.Input.Key.F2) return;
            if (!EditGestures.HasFlag(LunaEditGestures.F2)) return;

            if (_selectionUnit == LunaSelectionUnit.Cell)
            {
                // No IsEditable test here on purpose: Edit already refuses a column with no Commit,
                // and one rule with two owners is one rule that will disagree with itself. A
                // duplicate test here also cannot be sabotaged into failing, which is how it was
                // found - the check was removed and every guard stayed green.
                if (SelectedCell is not { } at) return;

                Edit(at.Row, at.Column);
                e.Handled = IsEditing;
                return;
            }

            if (Selected is not { } item) return;

            int column = _columns.FindIndex(c => c.IsEditable);
            if (column < 0) return;

            Edit(item, column);
            e.Handled = IsEditing;
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
            BringRowIntoView(target);
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

        // NAVIGATION, AND ALL THREE ANSWER "NOT REALISED" HONESTLY. A virtualising list has no visual
        // for a row that is scrolled away, so these return false rather than forcing one into
        // existence - a caller who needs the row on screen calls BringRowIntoView first, which is
        // exactly why that one exists. §54.3.
        /// <summary>Scrolls a row into view.</summary>
        /// <param name="item">The model whose row to show. Ignored when it is not in the current view.</param>
        public void BringRowIntoView(T item)
        {
            if (Rows is null || item is null) return;

            int index = IndexOf(item);
            if (index >= 0) Rows.ScrollIntoView(index);
        }

        /// <summary>Finds the visual for a row, when that row is currently realised.</summary>
        /// <param name="item">The model whose row to find.</param>
        /// <param name="row">The row's container, or null when the row is not on screen.</param>
        /// <returns>True when a realised row was found.</returns>
        public bool TryGetRow(T item, out Control? row)
        {
            row = null;
            if (Rows is null || item is null) return false;

            foreach (Control container in Rows.GetRealizedContainers())
            {
                if (!Equals(container.DataContext, item)) continue;

                row = container;
                return true;
            }

            return false;
        }

        /// <summary>Finds the visual for one cell of one row, when that row is currently realised.</summary>
        /// <param name="item">The model whose row to look in.</param>
        /// <param name="column">The column index, in the order the columns were added.</param>
        /// <param name="cell">The cell, or null when the row is not on screen or the column is hidden.</param>
        /// <returns>True when a realised cell was found.</returns>
        public bool TryGetCell(T item, int column, out Control? cell)
        {
            cell = Cell(item, column);
            return cell is not null;
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

        // The position of a model in the DISPLAYED order, which is what a scroll wants - _items is
        // arrival order and would scroll to the wrong row under a sort.
        private int IndexOf(T item)
        {
            for (int i = 0; i < _view.Count; i++)
            {
                if (Equals(Key(_view[i]), Key(item))) return i;
            }

            return -1;
        }

        private void BeginEdit(T item, int column, TableCell cell)
        {
            if (!_columns[column].IsEditable) return;

            // THE CELL COMES INTO VIEW BEFORE THE CARET GOES INTO IT - see docs/LunaP.md §64.
            //
            // Edit is public and F2 goes through it, so the cell being edited need not be anywhere
            // near the screen: measured on a 400-wide table scrolled to the left, Edit(item, 4) put
            // a focused editor at x=812 and moved the scroll not at all. A rename that happens where
            // nobody can see it is worse than one that is refused.
            //
            // The editor's own Focus() does NOT do this, which is the part worth knowing. A
            // ScrollViewer brings a focused control into view from its arranged bounds, and the
            // editor is created, inserted and focused inside one call - it has never been laid out
            // at that point and has no bounds to bring anywhere. The CELL has been arranged for as
            // long as its row has, so it is the thing that can be scrolled to.
            //
            // Clearing the frozen band afterwards is §62.2's business and needs nothing here: this
            // scroll lands the cell at the viewport edge, the editor's Focus raises GotFocus, and the
            // next layout moves it clear of the band.
            cell.BringIntoView();

            // An edit already open somewhere else is committed first, which is the same rule as
            // clicking away from it. Beginning a second editor while the first is still on screen
            // would leave two carets and one _editor field pointing at one of them.
            if (IsEditing) EndEdit(commit: true);
            if (IsEditing) return; // the previous edit refused to close, so it keeps the caret

            // WHATEVER PANEL HOLDS THE CELL, which is the row grid for an ordinary column and an
            // indent panel for the one carrying an expander (§55). The editor takes the cell's own
            // place in that panel rather than being appended to it, so an edited tree cell opens
            // where the text was instead of to the right of the expander.
            if (cell.Parent is not Panel host) return;

            var editor = new TextBox
            {
                Text = _columns[column].Text(item) ?? string.Empty,
                VerticalAlignment = VerticalAlignment.Center,
                MinHeight = 0,
                Padding = new Thickness(2, 0),
            };

            editor.KeyDown += EditorKey;
            editor.LostFocus += (_, _) => EndEdit(commit: true);

            // A ROW SCROLLED OUT OF VIEW TAKES ITS EDITOR WITH IT, and this is where that is
            // noticed. The list recycles containers, so a row that leaves the viewport has its
            // content rebuilt for a different model - the editor is simply gone from the tree, and
            // an _editor field still pointing at it would leave the table believing it is editing.
            // Cancelling rather than committing: the value was never confirmed, and writing one
            // because the user scrolled would be a change they did not ask for. Trap 1.
            // false, because the tree is ALREADY being torn down when this fires and taking the
            // editor out of its panel here mutates a children collection Avalonia is walking by
            // index - which throws ArgumentOutOfRange out of OnDetachedFromVisualTreeCore. The
            // editor is going away with the row regardless; only the state needs clearing. §55.
            editor.DetachedFromVisualTree += (_, _) => EndEdit(commit: false, detaching: true);

            if (host is Grid) Grid.SetColumn(editor, GridColumn(column));
            host.Children.Insert(host.Children.IndexOf(cell), editor);

            cell.IsVisible = false;

            _editItem = item;
            _editColumn = column;
            _editor = editor;
            _editCell = cell;

            editor.Focus();
            editor.SelectAll();
        }

        // A READER SETTING A CELL GOES THROUGH THE SAME GATE A TYPIST DOES, which is the point of
        // routing it here rather than letting the peer call Commit directly. Validate runs, a
        // refusal is refused, and the row's spoken name is rebuilt afterwards - so an assistive
        // technology cannot write a value that a person typing the same characters would have been
        // stopped from writing. §50.6.
        //
        // The message is shown for the same reason it is shown to a typist: a refusal with no
        // sentence is a cell that will not take a value and will not say why.
        private void SetFromAutomation(T item, int column, string text)
        {
            if (!_columns[column].IsEditable) return;

            if (_columns[column].Validate?.Invoke(item, text) is { } problem)
            {
                ShowMessage(problem);
                return;
            }

            _columns[column].Commit?.Invoke(item, text);
            ShowMessage(null);

            if (TextCellOf(item, column) is { } cell)
            {
                cell.Text = _columns[column].Text(item) ?? string.Empty;
                if (RowGridOf(cell) is { } grid) NameRow(grid, item);
            }

            CellValueChanged?.Invoke(item, column);
        }

        private void EditorKey(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Avalonia.Input.Key.Enter:
                    EndEdit(commit: true);
                    e.Handled = true;
                    break;

                // Escape restores the prior value by simply not writing one: the model was never
                // touched, so there is nothing to roll back.
                case Avalonia.Input.Key.Escape:
                    EndEdit(commit: false);
                    e.Handled = true;
                    break;
            }
        }

        // Returns having either closed the editor or left it open because the value was refused.
        // A rejected commit KEEPS THE CARET, which is the only humane answer - closing the editor
        // would throw away what was typed and show a message about a value no longer on screen.
        private void EndEdit(bool commit, bool detaching = false)
        {
            if (_ending || _editor is null || _editItem is null || _editCell is null) return;

            _ending = true;
            try
            {
                T item = _editItem;
                int column = _editColumn;
                string text = _editor.Text ?? string.Empty;

                if (commit)
                {
                    // Validate can veto. It runs before Commit and never after, so Commit may assume
                    // the text is good - which is what lets a caller put int.TryParse in Validate and
                    // a plain assignment in Commit.
                    if (_columns[column].Validate?.Invoke(item, text) is { } problem)
                    {
                        ShowMessage(problem);
                        _ending = false;
                        return;
                    }

                    _columns[column].Commit?.Invoke(item, text);
                    _committed = column;
                }

                Close(item, detaching);
                if (_committed >= 0)
                {
                    int changed = _committed;
                    _committed = -1;
                    CellValueChanged?.Invoke(item, changed);
                }
            }
            finally
            {
                _ending = false;
            }
        }

        private void Close(T item, bool detaching = false)
        {
            if (!detaching && _editor is { } editor)
            {
                if (editor.Parent is Panel host) host.Children.Remove(editor);
            }

            if (!detaching && _editCell is { } cell)
            {
                // Re-read through the projection rather than assigning the typed text: a Commit that
                // normalised the value - trimmed it, title-cased it, rounded it - would otherwise
                // leave the cell showing what was typed while the model holds something else.
                cell.Text = _columns[_editColumn].Text(item) ?? string.Empty;
                cell.IsVisible = true;

                if (RowGridOf(cell) is { } grid) NameRow(grid, item);
            }

            _editItem = null;
            _editColumn = -1;
            _editor = null;
            _editCell = null;
            ShowMessage(null);
        }

        // The row grid a cell belongs to. Its immediate parent for an ordinary column, an ancestor
        // once an expander panel sits between them.
        private static Grid? RowGridOf(Control cell) =>
            cell.GetVisualAncestors().OfType<Grid>().FirstOrDefault();

        private void ShowMessage(string? problem)
        {
            if (Message is null) return;

            Message.Text = problem ?? string.Empty;
            Message.IsVisible = !string.IsNullOrEmpty(problem);
        }

        // Finds a realised cell for a model. Null when the row is scrolled out of view, which is a
        // real answer rather than a failure: there is nothing on screen to put an editor into.
        //
        // SEARCHES BY THE MARKER AND NOT BY TYPE, since §57 - a cell can be a TableCell, a CheckBox
        // or anything a caller returned from Build, and the one thing all three have is the attached
        // column index. Whatever a template put inside its cell is walked past, because the marker is
        // only ever set on the cell itself.
        private Control? Cell(T item, int column)
        {
            if (Rows is null) return null;

            foreach (Control container in Rows.GetRealizedContainers())
            {
                if (!Equals(container.DataContext, item)) continue;

                foreach (Control candidate in container.GetVisualDescendants().OfType<Control>())
                {
                    if (TableCells.GetColumn(candidate) == column) return candidate;
                }
            }

            return null;
        }

        // The same lookup narrowed to a cell an editor can go into. A Check or Template cell answers
        // null here and that is what makes Edit refuse them without a kind test of its own.
        private TableCell? TextCellOf(T item, int column) => Cell(item, column) as TableCell;

        // THE INDENT AND THE TOGGLE, in front of the cell that carries the row's name - §55.
        //
        // A LEAF STILL GETS THE SPACE THE TOGGLE WOULD HAVE TAKEN, which is why the button is made
        // invisible rather than left out. Omitting it would shift a leaf's text left of its
        // siblings' by the width of a glyph, so a list of files under a folder would not line up
        // with each other - the one thing an indent exists to do.
        //
        // The whole thing is a DockPanel and not a Grid: two fixed-width things on the left and one
        // elastic thing filling the rest is exactly what docking is, and a Grid here would need
        // three ColumnDefinitions per row per level for the same picture.
        private Control Expander(T item, Control cell, int column)
        {
            object key = KeyOf(item);
            int depth = _depth.TryGetValue(key, out int found) ? found : 0;
            bool expandable = _expandable.Contains(key);
            bool expanded = _expanded.Contains(key);

            var toggle = new Button
            {
                Content = expanded ? "\u25BE" : "\u25B8",
                IsVisible = expandable,
                Width = 16,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            toggle.Classes.Add("expander");

            // A BUTTON, so it is focusable, invokable and reachable by keyboard for the same reason
            // a sortable heading is one (§27.3) - a tree a mouse can open and a keyboard cannot is a
            // tree half this toolkit's users cannot read. The name says what pressing it does rather
            // than what it is, because "expander" tells a reader nothing about which row.
            //
            // NAMED FROM THE PROJECTION AND NOT OFF THE CELL, since §57. Reading cell.Text was fine
            // while every cell was a TextBlock; an ExpanderColumn that is a checkbox or a template
            // has no text to read, and the projection answers for all three kinds.
            AutomationProperties.SetName(
                toggle,
                $"{(expanded ? "Collapse" : "Expand")} {_columns[column].Text(item) ?? string.Empty}");

            toggle.Click += (_, e) =>
            {
                if (_expanded.Contains(key)) Collapse(item); else Expand(item);
                e.Handled = true;
            };

            var row = new DockPanel();

            row.Children.Add(new Border
            {
                Width = depth * IndentSize,
                [DockPanel.DockProperty] = Dock.Left,
            });

            DockPanel.SetDock(toggle, Dock.Left);
            row.Children.Add(toggle);
            row.Children.Add(cell);

            return row;
        }

        // WHAT A READER HEARS, and the reason it is built here rather than left to Avalonia. A row
        // of bare TextBlocks in a Grid announces as its concatenated text at best - "Site text 1" -
        // which is three values with nothing to say which column each came from. Pairing every value
        // with its header turns that into "name: Site, type: text, pg: 1", which is the information
        // a column layout is carrying visually. §27.3.
        //
        // ITS OWN METHOD SINCE §50, because a committed edit changes a value this sentence contains.
        // Built once at row construction, a reader would announce the old value for as long as the
        // row stayed realised - which is the whole of trap 3 in PLAN-table.md §6.
        // SET ON THE CONTAINER AND NOT ONLY ON THE GRID, and that correction is §50.5.
        //
        // Until §50 this name went onto the row Grid alone. The Grid's peer is a NoneAutomationPeer
        // with IsControlElement = false - it is not in the view a screen reader navigates - so the
        // sentence was never reachable. What a reader actually got was the CONTAINER's name, and a
        // ListBoxItem with no name of its own falls back to its DataContext's ToString(): a reader
        // on the gallery's table heard "EmuSen.LunaP.Gallery.GalleryWindow+Field" three times.
        //
        // The old guard could not have caught it, because it read the attached property straight
        // back off the Grid rather than asking the peer - the §5.5 shape, an assertion about wiring
        // that passes while the effect is absent. It now asks the peer.
        //
        // The Grid keeps its name too. It costs nothing, it is what the existing test reads, and if
        // Avalonia ever puts row content into the control view the sentence is already there.
        private void NameRow(Grid grid, T item)
        {
            string sentence = Spoken(item);
            AutomationProperties.SetName(grid, sentence);

            // After a commit the grid IS parented, so the container can be renamed from here. On the
            // first build it is not, and ContainerPrepared does it instead.
            if (grid.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() is { } container)
            {
                AutomationProperties.SetName(container, sentence);
            }
        }

        // Where a row sits in the view. Zero when there is no gutter, because nothing asks then, and
        // zero for a row the map has not seen - which is a row not currently displayed, and there is
        // no honest position to give it.
        private int PositionOf(T item) => _position.TryGetValue(KeyOf(item), out int at) ? at : 0;

        // The sentence itself, so ContainerPrepared can build one without a grid in hand.
        private string Spoken(T item)
        {
            string cells = string.Join(", ", _columns
                .Where(c => c.IsVisible)
                .Select(c => $"{c.Header}: {c.Text(item) ?? string.Empty}"));

            // THE GUTTER GOES IN FRONT, because that is what a gutter is FOR: it is how the user
            // refers to the row - "line 12", "address 8040" - and a reader that heard it last, after
            // every cell, would have to hold the whole sentence to find out which row it was about.
            // Prefixed with the caption when there is one, so "addr 8040: op: LDA" says what the
            // number is; bare when there is not, because "1: name: alpha" is already unambiguous.
            if (_rowHeader is not null)
            {
                string label = _rowHeader(item, PositionOf(item)) ?? string.Empty;
                string prefix = string.IsNullOrEmpty(RowHeaderCaption)
                    ? label
                    : $"{RowHeaderCaption} {label}";

                cells = string.IsNullOrEmpty(cells) ? prefix : $"{prefix}: {cells}";
            }

            // A ROW THAT CAN BE OPENED SAYS SO, because in a tree "does this have more under it"
            // is part of what the row IS, and a reader that only hears the cells cannot tell a leaf
            // from a folder nobody has opened. Only for rows that actually have children - saying
            // "collapsed" about a leaf would be worse than saying nothing. §55.
            if (_children is null) return cells;

            object key = KeyOf(item);
            if (!_expandable.Contains(key)) return cells;

            return cells + (_expanded.Contains(key) ? ", expanded" : ", collapsed");
        }

        // POPULATES THE GRID'S OWN COLLECTION, AND NEVER ASSIGNS A NEW ONE. Not a style preference:
        // swapping this back to `grid.ColumnDefinitions = new ColumnDefinitions { ... }` turns the
        // shared sizing off again, silently, with no error and no visible change to the code's
        // intent.
        //
        // Avalonia 12.1.0 registers a definition with its shared size scope when it is ADDED to the
        // collection a Grid already owns, and does not when a ready-made collection is ASSIGNED to
        // the Grid. An assigned definition keeps a SharedSizeGroup that reads back correctly and
        // shares nothing - so every column sizes alone while looking, from the outside and from any
        // test that compares group names, exactly like a column that is sharing.
        //
        // The symptom is small, which is why it shipped. Star and absolute columns resolve to the
        // same number in both grids without needing to share, so they line up anyway; only an Auto
        // column exposes it, drifting by the difference between the widest heading and the widest
        // cell. A bold "type" heading six pixels wider than "text" put every cell in that column six
        // pixels right of its own heading.
        //
        // Fixed upstream by AvaloniaUI/Avalonia#21848, "register assigned definition collections
        // with their shared size group", merged 2026-07-26 - after 12.1.0 was released on
        // 2026-07-09. Populating works on 12.1.0 as it stands, so this costs no version bump, and it
        // stays correct whenever the upstream fix does arrive. docs/LunaP.md §27.7 carries the
        // measurement, the reduction to two plain grids, and why the guard that watched this could
        // not have caught it.
        //
        // AND ONLY AUTO COLUMNS JOIN THE GROUP, WHICH IS THE OTHER HALF AND WAS LEARNED THE HARD
        // WAY. A shared size group makes a STAR column behave as Auto - measured at 360.0 outside a
        // scope against 36.0 inside one, on two otherwise identical grids - so grouping every column
        // fixed the alignment and stopped the table filling its own width. That is Avalonia #19114,
        // open, and #6455 before it.
        //
        // Sharing only the Auto columns is not a workaround for that; it is what was needed all
        // along. Absolute columns are identical in both grids by definition, and a star column
        // resolves from whatever the other columns leave over - so once the Auto columns agree, the
        // remainder agrees, and star lines up without being told to. §27.7's own measurement said as
        // much before the cause was known: pre-fix, the star and absolute columns were already at
        // delta 0.0 and only the Auto column was out. §27.10.
        private void Define(Grid grid, string scope)
        {
            grid.ColumnDefinitions.Clear();

            // THE GUTTER GOES FIRST AND SHARES A SIZE GROUP LIKE ANY OTHER AUTO COLUMN, which is
            // what keeps the caption over its own labels. Auto is the default width because a gutter
            // exists to be exactly as wide as its widest label and no wider - and Auto is precisely
            // the case §27.10 records as needing the shared group, so leaving it out would put the
            // caption a few pixels off the numbers under it. §58.
            if (_rowHeader is not null)
            {
                GridLength gutter = GridLength.Parse(RowHeaderWidth);
                grid.ColumnDefinitions.Add(new ColumnDefinition(gutter)
                {
                    SharedSizeGroup = gutter.IsAuto ? scope + "_gutter" : null,
                });
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                ColumnSpec column = _columns[i];

                // A HIDDEN COLUMN IS A ZERO-WIDTH ONE THAT SHARES NOTHING, rather than a definition
                // left out. Leaving it out would shift every index after it, and the index is what
                // a remembered layout, a sort and Edit(item, column) are all written in terms of.
                // Pinned at zero on all three of width, min and max, because an Auto or star column
                // with a MinWidth would still claim space with nothing in it.
                if (!column.IsVisible)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0))
                    {
                        MinWidth = 0,
                        MaxWidth = 0,
                    });
                    continue;
                }

                GridLength width = column.Width;

                var definition = new ColumnDefinition(width)
                {
                    SharedSizeGroup = width.IsAuto ? scope + "_" + i : null,
                };

                // Left null, these stay the Grid's own 0 and infinity, which is what every column
                // did before §54 - so a column that names neither is untouched.
                if (column.MinWidth is { } min) definition.MinWidth = min;
                if (column.MaxWidth is { } max) definition.MaxWidth = max;

                grid.ColumnDefinitions.Add(definition);
            }
        }

        // Width is resolved here rather than kept as the caller's string, because a GridLength is
        // what the Grid wants and parsing it once at declaration is what makes a bad width fail at
        // the call site instead of at layout. Commit and Validate ride along unresolved - they are
        // the caller's own delegates and there is nothing to resolve.
        private readonly record struct ColumnSpec(
            string Header,
            Func<T, string> Text,
            GridLength Width,
            Comparison<T>? Sort,
            Action<T, string>? Commit,
            Func<T, string, string?>? Validate,
            double? MinWidth,
            double? MaxWidth,
            bool IsVisible,
            LunaCellKind Kind,
            Func<T, bool>? Checked,
            Action<T, bool>? Toggle,
            Func<T, Control>? Build)
        {
            // Matches LunaColumn<T>.IsEditable rather than restating it loosely: a Check column is
            // changed by being ticked and must not answer to the text editor, or F2 stops at it.
            public bool IsEditable => Kind == LunaCellKind.Text && Commit is not null;
        }

        // The heading control for a column, and its glyph when it has one. Held so that a sort can
        // update what is already on screen rather than building it again - see ShowSortState.
        private readonly record struct Head(Control Cell, TextBlock? Glyph);
    }
}
