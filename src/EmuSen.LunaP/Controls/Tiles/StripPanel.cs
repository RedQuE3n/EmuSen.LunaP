using Avalonia;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // The surface inside a TileStrip's scroll viewer: the whole row's width reported, children for the tiles in view only - see docs/LunaP.md §95.1.
    internal sealed class StripPanel : Panel
    {
        private readonly TileStrip _owner;

        internal StripPanel(TileStrip owner) => _owner = owner;

        protected override Size MeasureOverride(Size availableSize)
        {
            _owner.Realise();

            Rect slot = _owner.Slot(0);
            foreach (Control child in Children)
            {
                if (child.IsVisible) child.Measure(slot.Size);
            }

            return new Size(_owner.Extent(_owner.Count), slot.Height + 2 * slot.Top);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (Control child in Children)
            {
                if (child is TileGridItem { IsVisible: true, Index: >= 0 } item) item.Arrange(_owner.Slot(item.Index));
            }

            return finalSize;
        }
    }
}
