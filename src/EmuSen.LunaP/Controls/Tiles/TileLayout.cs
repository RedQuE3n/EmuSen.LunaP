using System;
using Avalonia;

namespace EmuSen.LunaP.Controls
{
    // Where tile N goes, for a width, a tile size and a spacing - see docs/LunaP.md §88.2.
    //
    // The arithmetic is IKImageBrowserView's, which is what OpenEmu's cover grid is: as many columns
    // as fit with at least Spacing between them and at both edges, then the space left over shared
    // out evenly among every gap, so a window a little too wide for another column spreads the
    // columns it has rather than leaving a ragged strip down the right. Rows keep exactly Spacing
    // between them, because vertical leftover is what a scroll bar is for.
    //
    // Kept apart from the panel because it is the one piece with no Avalonia state in it at all, and
    // the one a keyboard handler needs too: PageDown asks how many rows a viewport holds, and asking
    // the panel would make navigation depend on something having been laid out.
    internal readonly record struct TileLayout(int Columns, double TileWidth, double TileHeight, double Spacing, double Gap)
    {
        // Columns is never below one, even for a width narrower than a tile: a grid that answered
        // zero would divide by it in every keyboard move, and one clipped column is still usable.
        internal static TileLayout For(double width, double tileWidth, double tileHeight, double spacing)
        {
            double tw = Math.Max(1, tileWidth);
            double s = Math.Max(0, spacing);

            int columns = double.IsFinite(width) && width > 0
                ? Math.Max(1, (int)Math.Floor((width - s) / (tw + s)))
                : 1;

            // Clamped at zero so a single column wider than the window sits at the left edge rather
            // than at a negative offset the scroll viewer cannot reach.
            double gap = double.IsFinite(width) && width > 0
                ? Math.Max(0, (width - columns * tw) / (columns + 1))
                : s;

            return new TileLayout(columns, tw, Math.Max(1, tileHeight), s, gap);
        }

        internal double RowPitch => TileHeight + Spacing;

        internal int Rows(int count) => count <= 0 ? 0 : (count + Columns - 1) / Columns;

        internal double Extent(int count) => count <= 0 ? 0 : Spacing + Rows(count) * RowPitch;

        internal double RowTop(int row) => Spacing + row * RowPitch;

        internal Rect Slot(int index)
        {
            int row = index / Columns;
            int column = index % Columns;
            return new Rect(Gap + column * (TileWidth + Gap), RowTop(row), TileWidth, TileHeight);
        }

        // How many whole rows a viewport of this height shows, never below one, so PageDown always
        // moves somewhere.
        internal int RowsIn(double viewport) =>
            double.IsFinite(viewport) && viewport > 0 ? Math.Max(1, (int)Math.Floor(viewport / RowPitch)) : 1;
    }
}
