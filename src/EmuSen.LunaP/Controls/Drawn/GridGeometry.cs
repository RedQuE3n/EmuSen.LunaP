using System;
using Avalonia;

namespace EmuSen.LunaP.Controls
{
    // An ImageGrid's arithmetic, apart from any control so a host can ask it before layout - see docs/LunaP.md §104.1.
    /// <summary>The layout of an ImageGrid in a box: columns, rows, the unscaled item and spacing, the margins a scaled item needs, where the rows are cut, and where they scroll to.</summary>
    /// <param name="Box">The grid's own size.</param>
    /// <param name="Item">An unselected item's size.</param>
    /// <param name="Spacing">The space between neighbouring unscaled items.</param>
    /// <param name="Scale">The selected item's scale.</param>
    /// <param name="ScaleInwards">Whether items at the edges scale towards the middle rather than about their centres.</param>
    /// <param name="FractionalRows">Whether a row cut by the box's bottom is drawn in part.</param>
    /// <param name="Count">How many items there are.</param>
    public readonly record struct GridGeometry(Size Box, Size Item, Size Spacing, double Scale, bool ScaleInwards, bool FractionalRows, int Count)
    {
        /// <summary>The layout for a box; a null spacing is the automatic one, half an item's growth on each axis.</summary>
        /// <param name="box">The grid's size.</param>
        /// <param name="item">An unselected item's size.</param>
        /// <param name="spacing">The space between items, or null for the automatic spacing.</param>
        /// <param name="scale">The selected item's scale.</param>
        /// <param name="scaleInwards">Whether edge items scale towards the middle.</param>
        /// <param name="fractionalRows">Whether a cut row is drawn in part.</param>
        /// <param name="count">How many items there are.</param>
        /// <returns>The layout of that box.</returns>
        public static GridGeometry Of(Size box, Size item, Size? spacing, double scale, bool scaleInwards, bool fractionalRows, int count) =>
            new(box, item, spacing ?? new Size(item.Width * (scale - 1) / 2, item.Height * (scale - 1) / 2), Math.Max(0.01, scale), scaleInwards, fractionalRows, Math.Max(0, count));

        /// <summary>The room kept at the left and top so a scaled item in the first column or row stays inside the box; none with ScaleInwards.</summary>
        public Size Margin => ScaleInwards ? default : new Size(Math.Max(0, Item.Width * (Scale - 1) / 2), Math.Max(0, Item.Height * (Scale - 1) / 2));

        /// <summary>The distance from one column, or one row, to the next.</summary>
        public Size Pitch => new(Item.Width + Spacing.Width, Item.Height + Spacing.Height);

        /// <summary>How many columns fit with room for a scaled item: at least one.</summary>
        public int Columns => Pitch.Width <= 0 ? 1 : Math.Max(1, (int)Math.Floor((Box.Width + Spacing.Width - 2 * Margin.Width) / Pitch.Width + 1e-9));

        /// <summary>How many rows the items fill.</summary>
        public int Rows => Count == 0 ? 0 : (Count + Columns - 1) / Columns;

        /// <summary>The rows that fit, not rounded down.</summary>
        public double VisibleRows => Pitch.Height <= 0 ? 1 : Math.Max(0, (Box.Height + Spacing.Height - 2 * Margin.Height) / Pitch.Height);

        /// <summary>The whole rows that fit: at least one.</summary>
        public int WholeRows => Math.Max(1, (int)Math.Floor(VisibleRows + 1e-9));

        /// <summary>The rows a selection sits within from the top before the grid scrolls: VisibleRows with FractionalRows, else WholeRows.</summary>
        public double ShownRows => FractionalRows ? Math.Max(1, VisibleRows) : WholeRows;

        /// <summary>The height the rows are drawn in: the box with FractionalRows, else the whole rows and their scaling room.</summary>
        public double ClipHeight => FractionalRows ? Box.Height : Math.Min(Box.Height, WholeRows * Pitch.Height - Spacing.Height + 2 * Margin.Height);

        /// <summary>The scroll, in rows, that a selection settles at: none until its row passes the last one shown, then its row kept on the bottom.</summary>
        /// <param name="index">The selected item.</param>
        /// <returns>Rows scrolled off the top.</returns>
        public double ScrollFor(int index) => Math.Max(0, RowOf(index) - (ShownRows - 1));

        /// <summary>An item's row.</summary>
        /// <param name="index">The item's index in the list.</param>
        /// <returns>Its row, from 0.</returns>
        public int RowOf(int index) => Math.Max(0, index) / Columns;

        /// <summary>An item's column.</summary>
        /// <param name="index">The item's index in the list.</param>
        /// <returns>Its column, from 0.</returns>
        public int ColumnOf(int index) => Math.Max(0, index) % Columns;

        /// <summary>An item's unscaled box at a scroll, in the grid's coordinates; columns run from the left and leave what is over on the right.</summary>
        /// <param name="index">The item's index in the list.</param>
        /// <param name="scroll">Rows scrolled off the top.</param>
        /// <returns>The item's box before its scale.</returns>
        public Rect CellRect(int index, double scroll) =>
            new(Margin.Width + ColumnOf(index) * Pitch.Width, Margin.Height + (RowOf(index) - scroll) * Pitch.Height, Item.Width, Item.Height);

        /// <summary>The point of an item's box that stays put while it scales: its centre, or with ScaleInwards an outer edge in the first and last columns, the first row, and the bottom row of a scrolled grid.</summary>
        /// <param name="index">The item's index in the list.</param>
        /// <param name="scroll">Rows scrolled off the top.</param>
        /// <returns>The point, as fractions of the box.</returns>
        public RelativePoint Anchor(int index, double scroll)
        {
            if (!ScaleInwards) return RelativePoint.Center;
            int column = ColumnOf(index), row = RowOf(index);
            double x = Columns > 1 && column == 0 ? 0 : Columns > 1 && column == Columns - 1 ? 1 : 0.5;
            double y = row == 0 && scroll < 1e-6 ? 0 : scroll > 1e-6 && row - scroll >= ShownRows - 1 - 1e-3 ? 1 : 0.5;
            return new RelativePoint(x, y, RelativeUnit.Relative);
        }
    }
}
