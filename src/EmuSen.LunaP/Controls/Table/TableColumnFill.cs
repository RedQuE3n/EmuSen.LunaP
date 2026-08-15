using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // COLUMNS THE TABLE DOES NOT BUILD - see docs/LunaP.md §72.
    //
    // OFF BY DEFAULT, like every other item in §54's arc, and here the default is not merely
    // conservative - it is the correct answer for almost every table this toolkit draws. Six
    // columns cost nothing to draw whole, and the machinery here would spend a range computation
    // per layout to leave every one of them exactly where it was.
    //
    // WHAT IT IS WORTH, MEASURED. A table of 120 columns of 120 pixels in an 800-wide viewport
    // shows 6.7 of them, and built 2,144 visuals against a six-column table's 181. Twenty
    // refreshes of 200 rows took 732ms at 120 columns and 60ms at six - so a polling window
    // redrawing a wide table spends 36ms a tick, and about 95% of it on cells nobody can see.
    //
    // WHAT IT COSTS, ALSO MEASURED AND NOT HIDDEN. A column left out has no cell, so TryGetCell
    // answers false for it and a screen reader walking cells does not reach it - exactly what row
    // virtualization has always done to a row scrolled away (§54.3), now true sideways as well. The
    // row's spoken sentence is NOT affected, because Spoken builds it from the column specs rather
    // than from the cells that happen to exist.
    //
    // FillColumns runs from LayoutUpdated alongside Pin, and the ordering between the three passes
    // registered there is a fact about the wiring rather than about this feature - so it is argued
    // in OnPartsAttached and not here. §74.3.
    public partial class LunaTable<T> where T : class
    {
        // The flag itself, and the whole of what a table pays for this feature existing when it is
        // off: Realized answers true unconditionally, FillColumns returns on its first line, and
        // Row() builds every column exactly as it did before §72.
        private bool _virtualizeColumns;

        /// <summary>Builds cells only for the columns near the viewport. Off by default.</summary>
        /// <remarks>
        /// Worth turning on for a table wide enough to scroll sideways past many columns, and worth
        /// leaving off otherwise. Only columns of a fixed width are ever left out: an Auto or star column
        /// takes its width from its content, so dropping its cells would change how wide it is, and
        /// frozen columns are always on screen by definition. A column that is not built has no cell, so
        /// <see cref="TryGetCell"/> answers false for it.
        /// </remarks>
        public bool VirtualizeColumns
        {
            get => _virtualizeColumns;
            set
            {
                if (_virtualizeColumns == value) return;

                _virtualizeColumns = value;

                // The range is dropped rather than kept, so turning this on starts from "everything
                // is realized" and the first fill narrows it - and turning it off puts every cell
                // back through the same fill, which is what _filled below is for.
                _wanted = null;
                FillColumns();
            }
        }

        // WHICH COLUMNS A ROW CURRENTLY HOLDS, or null for "all of them" - see docs/LunaP.md §72.
        //
        // Null is the resting state and the honest one: before the first layout there is no viewport
        // to measure against, and a range guessed from nothing would leave a table blank until
        // something moved. Row() reads it too, so a row realized while scrolling vertically is built
        // narrow straight away rather than built whole and trimmed on the next pass.
        private (int First, int Last)? _wanted;

        // Whether anything is currently left out, so turning the feature off puts every cell back and
        // a table that never asked for it does no work at all. The same shape as _pinned, and for the
        // same reason: a feature that is off has to be able to clear what it set once, and then stop.
        private bool _filled;

        // A column something is about to look for the cell of, whatever the viewport says. Edit and
        // the arrow keys both find a cell by walking the row, and a virtualized column has no cell
        // to find - so they would quietly do nothing, which is §64.4's defect arriving by a new road.
        //
        // HELD RATHER THAN CLEARED AFTER THE FILL, and that is what keeps an open editor's cell
        // underneath it. Edit asks for its column on the way in; nothing takes it back, so the cell
        // is still there however far the table is scrolled afterwards.
        //
        // ONE FIELD IS ENOUGH, WHICH WAS CHECKED RATHER THAN ASSUMED. It looks like it needs a second
        // clause for the edited column - a caret sitting over a cell that has been removed is exactly
        // the kind of defect this control has had before (§55.7) - and that clause was written, and
        // could not be made to fail. Nothing can overwrite this while an edit is open: OnKeyDown
        // returns on `IsEditing` before it reaches the arrows, Edit ends one editor before opening
        // the next, and no other path asks. So the clause went, on the same grounds as the duplicate
        // IsEditable test in OnKeyDown. §72.6.
        private int _needed = -1;

        // Whether column n has to exist in every realized row.
        //
        // ONLY A COLUMN OF A FIXED WIDTH MAY BE LEFT OUT, and that is the clause with everything
        // behind it - see §72.3, which has the measurements and the two guards that could not fail
        // before them. The rule is not a caution: a column whose width comes from its CONTENT stops
        // having a width when its content stops being built, and both of the other two kinds do.
        //
        // A STAR COLUMN COLLAPSES IMMEDIATELY, and this is the worse of the two. A row is measured
        // against no width limit, so a star column there takes its content's size rather than a share
        // of anything. Measured on Avalonia 12.1.0, one "*" among twenty-nine fixed columns: 175
        // pixels at rest, 0 the moment its cells were dropped, and 175 again on scrolling back - with
        // the extent moving 3,679 to 3,504 and every column to its right sliding 175 pixels sideways
        // under the user's hand while they scrolled.
        //
        // AN AUTO COLUMN COLLAPSES ONE REFRESH LATER, which is why it was nearly missed. Its width is
        // shared between the header and every row by a size group (§27.10), and a shared size group
        // does NOT shrink when its largest contributor is removed from a grid that already exists: a
        // 158-pixel column stayed 158 with its cell gone from every row, in the header and the rows,
        // and through a forced re-measure. What breaks it is the next rebuild, because Refresh makes
        // new grids and the group is recomputed without a cell - 158 to 24, the bare heading, and
        // STILL 24 after scrolling back. For the polling windows this toolkit exists for (§21) that
        // is about a second after the scroll.
        //
        // The practical scope this leaves is the honest one, and it is the case that needed it: a
        // wide table of fixed columns - a disassembly, a memory map, a register dump - is exactly
        // what §72 was measured on.
        //
        // A FROZEN COLUMN IS ALWAYS BUILT for the plainer reason that it is on screen at every
        // offset - that is what frozen means - so leaving it out could only ever be wrong.
        private bool Realized(int column)
        {
            if (!_virtualizeColumns || _wanted is not { } range) return true;
            if (column < _frozenColumns) return true;
            if (!_columns[column].Width.IsAbsolute) return true;
            if (column == _needed) return true;

            return column >= range.First && column <= range.Last;
        }

        // The columns whose own width overlaps the viewport, widened by one on each side.
        //
        // MEASURED FROM A ROW AND NOT FROM THE HEADER, which looks like the obvious source and is
        // not the right one. The header grid is laid out at the VIEWPORT's width - 776 against a
        // row's 2,400 - so the two disagree about any column that is not a fixed width: measured on
        // Avalonia 12.1.0, one "*" among twenty-nine 120s resolved to 0 in the header and 175 in the
        // rows. The row grid's own definitions are the geometry of the cells being placed, which is
        // what the question is actually about.
        //
        // NO GUARD FAILS IF THIS IS SWAPPED, and that is worth knowing rather than discovering. A
        // range measured in the header's compressed space comes out WIDER than it needs to be rather
        // than displaced, and Auto and star columns are built unconditionally anyway, so they fill
        // the gaps it would leave. What it costs is cells built for nothing. §72.7.
        //
        // ONE COLUMN OF SLACK EACH SIDE rather than a half-viewport of overscan. The whole point is
        // to build less, so the margin is the smallest one that still has a cell ready before its
        // leading edge is reached.
        //
        // A JUDGEMENT AND NOT A MEASUREMENT, which is worth knowing before somebody tunes it: no
        // guard fails when the slack is removed altogether, because every assertion in
        // TableVirtualizationTests is made after layout has settled and the range is correct at rest
        // with or without it. What it buys is the frame DURING a drag, which nothing here captures.
        //
        // Null when there is nothing to measure against - no viewer yet, a zero viewport, or a set
        // of columns none of which overlaps it - and null means "build everything". That is the safe
        // direction: a table that shows every cell is slow, and a table that shows none is broken.
        private (int First, int Last)? WantedRange(Grid grid)
        {
            double viewport = _viewer?.Viewport.Width ?? 0;
            if (viewport <= 0) return null;

            double from = _scrolledTo;
            double to = _scrolledTo + viewport;

            int first = -1;
            int last = -1;
            double x = _rowHeader is null || grid.ColumnDefinitions.Count == 0
                ? 0
                : grid.ColumnDefinitions[0].ActualWidth;

            for (int i = 0; i < _columns.Count; i++)
            {
                int at = GridColumn(i);
                if (at >= grid.ColumnDefinitions.Count) break;

                double width = grid.ColumnDefinitions[at].ActualWidth;

                if (x < to && x + width > from)
                {
                    if (first < 0) first = i;
                    last = i;
                }

                x += width;
            }

            if (first < 0) return null;

            return (Math.Max(0, first - 1), Math.Min(_columns.Count - 1, last + 1));
        }

        // Brings every realized row into line with the range, and recomputes the range first.
        //
        // RUN FROM LayoutUpdated, ALONGSIDE Pin, and unlike Pin this one adds and removes children -
        // which invalidates layout and brings us straight back here. It converges because it is a
        // fixpoint: a second run with the same range finds every wanted column already held and
        // every unwanted one already gone, changes nothing, and so invalidates nothing.
        //
        // TableVirtualizationTests holds it to that by counting CHILD MUTATIONS rather than layout
        // passes - LayoutUpdated fires once per pass whether or not anything was dirty, so a pass
        // count is a number this control cannot influence and the first version of that guard could
        // not fail. §72.5.
        private void FillColumns()
        {
            if (!_virtualizeColumns && !_filled) return;
            if (Rows is null) return;

            IReadOnlyList<Control> containers = Rows.GetRealizedContainers().ToList();

            // The range comes from the first row that has a grid, and every row after it is filled
            // from that one answer. They all carry the same definitions, so measuring each would be
            // the same sum computed fourteen times.
            _wanted = null;
            if (_virtualizeColumns)
            {
                foreach (Control container in containers)
                {
                    if (RowGridIn(container) is not { } probe) continue;

                    _wanted = WantedRange(probe);
                    break;
                }
            }

            foreach (Control container in containers)
            {
                if (container.DataContext is not T model) continue;
                if (RowGridIn(container) is not { } grid) continue;

                FillRow(grid, model);
            }

            _filled = _virtualizeColumns && _wanted is not null;
        }

        // One row's worth: take out what is no longer wanted, then put in what is missing. Both
        // halves are no-ops when nothing changed, which is what makes the whole thing a fixpoint.
        private void FillRow(Grid grid, T item)
        {
            for (int n = grid.Children.Count - 1; n >= 0; n--)
            {
                if (grid.Children[n] is not Control child) continue;

                // Owner is -1 for the gutter, the frozen edge, a cell-selection box and an open
                // editor, so none of the four is ever taken out by a scroll. §72.2.
                int owner = TableCells.GetOwner(child);
                if (owner < 0 || Realized(owner)) continue;

                grid.Children.RemoveAt(n);
            }

            var held = new HashSet<int>();
            foreach (Control child in grid.Children.OfType<Control>())
            {
                int owner = TableCells.GetOwner(child);
                if (owner >= 0) held.Add(owner);
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                if (!_columns[i].IsVisible || !Realized(i) || held.Contains(i)) continue;

                AddCell(grid, item, i, atFront: true);
            }
        }

        // Puts one column back before something goes looking for its cell, and is the reason Edit
        // and the arrow keys still work sideways past the viewport.
        private void ShowColumn(int column)
        {
            if (!_virtualizeColumns || column < 0 || column >= _columns.Count) return;

            _needed = column;
            FillColumns();
        }
    }
}
