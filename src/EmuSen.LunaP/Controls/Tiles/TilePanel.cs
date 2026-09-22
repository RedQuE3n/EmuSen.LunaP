using System;
using Avalonia;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // The surface inside a TileGrid's scroll viewer - see docs/LunaP.md §88.2.
    //
    // It reports the height of EVERY row, so the scroll bar is the right size for five thousand
    // items, and holds children for only the rows in view. The owner decides which rows those are
    // and fills the containers; this class is only the two layout passes, so the realisation logic
    // can live beside the items and the selection it has to agree with.
    internal sealed class TilePanel : Panel
    {
        private readonly TileGrid _owner;

        internal TilePanel(TileGrid owner) => _owner = owner;

        // Children are realised here, during measure, because this is the one moment the width and
        // the scroll position are both known. Adding a container invalidates this panel's measure
        // again; the second pass finds every row already realised and adds nothing, so the layout
        // settles in two passes rather than looping.
        protected override Size MeasureOverride(Size availableSize)
        {
            TileLayout layout = _owner.LayoutFor(availableSize.Width);
            _owner.RealiseFor(layout);

            var tile = new Size(layout.TileWidth, layout.TileHeight);
            foreach (Control child in Children)
            {
                if (child.IsVisible) child.Measure(tile);
            }

            double width = double.IsFinite(availableSize.Width) ? availableSize.Width : layout.Columns * (layout.TileWidth + layout.Spacing) + layout.Spacing;
            return new Size(width, layout.Extent(_owner.Count));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            TileLayout layout = _owner.LayoutFor(finalSize.Width);

            foreach (Control child in Children)
            {
                if (child is TileGridItem { IsVisible: true, Index: >= 0 } item) item.Arrange(layout.Slot(item.Index));
            }

            return finalSize;
        }
    }
}
