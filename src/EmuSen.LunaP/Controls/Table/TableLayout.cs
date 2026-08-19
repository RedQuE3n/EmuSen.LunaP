using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // WHERE THE USER LEFT IT - see docs/LunaP.md §27.11, and §70.4 for the half that was missing.
    //
    // Four methods and three fields, and the reason they are a file rather than an appendix to
    // TableColumns.cs is that they answer a different question. That file decides what a column IS;
    // this one decides what SURVIVES A RESTART, which is a smaller set on purpose - the widths the
    // user dragged and the sort they left it in, and nothing the caller declared.
    //
    // EVERY REASON TO WRITE GOES THROUGH ONE DEBOUNCE, and every reason to read goes through one
    // Restore that is idempotent and refuses a layout it does not recognise. Both are single
    // funnels by design: §70.4 is what happened when a second way to save did not exist and a sort
    // therefore never reached the file at all.
    //
    // The file format and the disk are TableLayoutStore's, which is a non-generic type that knows
    // about JSON and nothing about tables (§74.4).
    public partial class LunaTable<T> where T : class
    {
        // Saving is debounced for the reason SplitPane debounces its divider (§26.6): a drag
        // produces a property change per frame, and writing tables.json sixty times a second would
        // be a full read-modify-write of every table's layout per frame.
        private Debounce? _save;

        private string? _tableKey;

        // Whether the widths currently on the columns came off a saved layout, so Revert knows
        // whether there is anything to undo. Replaces the _restored latch, which could not express
        // "applied, and no longer applicable" - the state §79.2 lived in.
        private bool _applied;

        // The layout for _loadedKey, read once rather than once per Column() call (§79.5). Null is a
        // real answer - no layout saved under that key - so the key is tracked separately rather than
        // using null to mean "not looked yet".
        private TableLayout? _loaded;
        private string? _loadedKey;

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

            // The cached copy Restore reads is now the stale one. Dropped rather than updated, so
            // there is one path to a layout and it is the file (§79.5).
            _loadedKey = null;
        }

        // MATCHED BY HEADER AND BY COUNT, and both halves matter. A saved layout describes the
        // columns that existed when it was written; a caller who has since added, removed or
        // renamed one is describing a different table, and applying half a layout to it would move
        // widths onto the wrong columns and point the sort arrow confidently at the wrong heading.
        //
        // The safe answer to a layout that does not match is to ignore it. A user loses the column
        // widths they dragged once, after the application changed its own table - which is a good
        // deal better than a table that comes back scrambled and cannot be explained.
        // RE-ENTRANT, AND IT HAS TO BE - see docs/LunaP.md §79.2.
        //
        // This runs after every Column() call, so it sees the table at every intermediate column
        // count on the way up. The count check below is what refuses a layout describing a different
        // table - and a five-column table IS a two-column table for one moment while it is being
        // built, so a stale two-column layout matched, latched, and left widths of [500, 500, 100,
        // 100, 100] on a table the caller declared entirely at 100. That is precisely the outcome the
        // check exists to prevent, arriving through the call site rather than through the comparison.
        //
        // So the decision is re-made from Declared every time rather than applied once and latched:
        // a match at two columns is undone by the call at three. _restored is gone; what replaces it
        // is _applied, which records whether the widths currently on the columns came from a layout
        // and therefore whether there is anything to take back off.
        //
        // AND THE FILE IS READ ONCE PER KEY, not once per call. The comment at the Column() call site
        // used to say this cost "two comparisons"; until the latch was set it cost a read and a full
        // JSON parse of tables.json - the file every table in the application shares - measured at
        // thirty reads for a thirty-column table (§79.5). One table instance wants one answer from
        // disk; a second table writing the same key mid-construction is not a case this control has.
        private void Restore()
        {
            if (TableKey is not { } key || _columns.Count == 0) return;

            if (!string.Equals(_loadedKey, key, StringComparison.Ordinal))
            {
                _loaded = TableLayoutStore.Load(key);
                _loadedKey = key;
            }

            if (_loaded is not { } layout || layout.Widths.Count != _columns.Count)
            {
                Revert();
                return;
            }

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
                    Revert();
                    return;
                }
            }

            _applied = true;

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

        // TAKES A RESTORE BACK OFF, and does nothing at all if there was never one to take.
        //
        // The guard matters as much as the reversion: a table with no saved layout must not have its
        // widths written at every Column() call, because a resize drag writes Width too and this
        // would undo it. _applied is only true between a layout being applied and it ceasing to
        // match, which is a window that closes during construction and never reopens.
        private void Revert()
        {
            if (!_applied) return;

            _applied = false;
            for (int i = 0; i < _columns.Count; i++)
            {
                _columns[i] = _columns[i] with { Width = _columns[i].Declared };
            }

            // The sort came off the same layout, so it goes back with the widths. A SortBy the caller
            // made itself is not at risk: Restore only runs while the table is being built, and §27.6
            // already has a remembered layout outranking a sort set in code.
            _sortColumn = -1;
            _sortDescending = false;

            Rebuild();
            Show();
        }

        // THE DEBOUNCED WRITE, AND EVERY REASON TO WRITE GOES THROUGH IT - see docs/LunaP.md §70.4.
        //
        // Until §70 only a column RESIZE poked this, so a sort was never written down unless the
        // application called SaveNow itself. The promise on the tin is "columns sort, resize and
        // remember where you left them", and half of it did not happen.
        //
        // 400ms for SplitPane's reason (§22.3): a drag raises a property change per frame and the
        // file is a full read-modify-write. A sort click is one act and would not need debouncing on
        // its own, but sharing the path is worth more than the 400ms - two ways to save are two
        // things that can disagree about what gets written.
        private void Remember()
        {
            _save ??= new Debounce(TimeSpan.FromMilliseconds(400), SaveNow);
            _save.Poke();
        }

        // AND FLUSHED ON THE WAY OUT, which is the other half and the half that made the defect
        // invisible. A debounce that has not fired when the window closes has written nothing, so
        // without this a sort or a drag in the last 400ms of a window's life was simply lost -
        // and every test in TableLayoutTests called SaveNow by hand, so no fixture ever ran the
        // path a user actually takes. SplitPane has done this since §22; this control had not.
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            SaveNow();
            base.OnDetachedFromVisualTree(e);
        }
    }
}
