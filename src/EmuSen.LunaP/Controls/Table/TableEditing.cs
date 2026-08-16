using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace EmuSen.LunaP.Controls
{
    // EDITING A CELL - see docs/LunaP.md §50, and §56 for which gestures open one.
    //
    // ONE EDITOR AT A TIME, AND THE WHOLE FILE IS ITS LIFETIME. Four fields hold the open edit, and
    // the reason they are fields rather than a parameter passed around is that the edit outlives
    // every call here: it is begun by one event, ended by a different one, and can be interrupted by
    // a third that belongs to neither.
    //
    // THE EDITOR IS PUT INTO THE ROW'S EXISTING GRID rather than the row being rebuilt with an
    // editor in it, and nothing in this file touches _items, _view or the sort. That is what keeps a
    // committed edit from moving the row it was typed into - trap 2 in PLAN-table.md §6 - and it is
    // the constraint to check against before adding anything here.
    //
    // A READER GOES THROUGH THE SAME GATE A TYPIST DOES, which is why SetFromAutomation is in this
    // file and not in TableSpeech.cs: it is a write, it runs Validate, and a refusal refuses. §50.6.
    //
    // F2 arrives through OnKeyDown, which stays with the other framework overrides because it
    // dispatches to three features rather than to this one (§74.3).
    public partial class LunaTable<T> where T : class
    {
        // WHAT OPENS AN EDITOR, and it is a set rather than a mode because the two gestures are
        // independent - a table can want F2 without double-click, or neither. §56.
        //
        // Both by default, which is what §50 hardcoded, so no existing table changes. None is a real
        // value and not an omission: a table whose columns have a Commit but whose editing is driven
        // entirely by an application's own "Rename" menu item wants LunaTable.Edit and no gesture at
        // all, and turning the column read-only to get that would lose the validation with it.
        /// <summary>Which gestures open a cell editor. Double-click and F2 by default.</summary>
        public LunaEditGestures EditGestures { get; set; } = LunaEditGestures.Default;

        // Raised after a value has been written and the row renamed, so a handler reading the model
        // sees the committed value rather than the one being replaced. Fires for an edit made by a
        // person and for one made through the automation provider, because both go through the same
        // gate (§50.6) and a caller watching for changes wants both.
        /// <summary>Raised after a cell edit has been committed, with the model and the column index.</summary>
        public event Action<T, int>? CellValueChanged;

        // The open edit, if there is one. Four fields rather than a small struct because EndEdit
        // clears them one by one in a finally and a null _editor is the whole test for "not editing".
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

            // A COLUMN VIRTUALIZED AWAY HAS NO CELL AT ALL, so this puts it back before the lookup
            // below goes looking for it. Without it, Edit(item, 90) on a wide table would return on
            // the next line and rename nothing - the §64.4 defect exactly, where an edit that is
            // refused because of where the viewport happens to be is worse than one that scrolls.
            // A table not virtualizing columns never enters it. §72.4.
            ShowColumn(column);

            if (TextCellOf(item, column) is not { } cell) return;

            BeginEdit(item, column, cell);
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

                // The row's sentence and every cell's status, because a commit changes a value that
                // more than one of them can be reading. §50 fixed the row's; §68.4 is the same defect
                // one level down.
                if (RowGridOf(cell) is { } grid)
                {
                    NameRow(grid, item);
                    NameCells(grid, item);
                }
            }

            _editItem = null;
            _editColumn = -1;
            _editor = null;
            _editCell = null;
            ShowMessage(null);
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

                // THE WHOLE ROW, and this was the third path and the one that mattered most. §68.4
                // found a cell going stale because a different cell changed, and fixed the two paths
                // its sabotages happened to cross - a pointer toggle and a typed commit. This is the
                // same write arriving from a screen READER, so the defect was live on the automation
                // path in the pass whose subject was automation: a reader setting a name left the
                // template cell beside it describing the value before the one it had just written.
                if (RowGridOf(cell) is { } grid)
                {
                    NameRow(grid, item);
                    NameCells(grid, item);
                }
            }

            CellValueChanged?.Invoke(item, column);
        }

        // The same lookup narrowed to a cell an editor can go into. A Check or Template cell answers
        // null here and that is what makes Edit refuse them without a kind test of its own.
        private TableCell? TextCellOf(T item, int column) => Cell(item, column) as TableCell;

        private void ShowMessage(string? problem)
        {
            if (Message is null) return;

            Message.Text = problem ?? string.Empty;
            Message.IsVisible = !string.IsNullOrEmpty(problem);
        }
    }
}
