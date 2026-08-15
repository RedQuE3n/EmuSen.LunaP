using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // FINDING A ROW, A CELL, OR THE GRID ONE SITS IN - see docs/LunaP.md §54.3.
    //
    // Five lookups, and they are together because they share one rule: NONE OF THEM REALISES
    // ANYTHING. Each answers about the tree as it currently stands and says so honestly when the
    // answer is nothing, rather than forcing a container or a cell into existence to have something
    // to return. Every caller elsewhere in this control is written against that, which is why
    // BringRowIntoView and ShowColumn exist as separate acts a caller performs first.
    //
    // The two static helpers are here rather than beside their callers because they have four
    // between them, in four different files, and a second copy of "which Grid is the row grid" is
    // exactly the kind of thing that gets one of them wrong.
    public partial class LunaTable<T> where T : class
    {
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
        /// <param name="cell">The cell, or null when the row is not on screen, the column is hidden, or
        /// <see cref="VirtualizeColumns"/> has left that column out.</param>
        /// <returns>True when a realised cell was found.</returns>
        public bool TryGetCell(T item, int column, out Control? cell)
        {
            cell = Cell(item, column);
            return cell is not null;
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

        private static Grid? RowGridIn(Control container) =>
            container.GetVisualDescendants().OfType<Grid>().FirstOrDefault();

        // The row grid a cell belongs to. Its immediate parent for an ordinary column, an ancestor
        // once an expander panel sits between them.
        private static Grid? RowGridOf(Control cell) =>
            cell.GetVisualAncestors().OfType<Grid>().FirstOrDefault();
    }
}
