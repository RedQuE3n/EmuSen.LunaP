using System;
using Avalonia;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // The big screen's section row: only whole children, as many as fit around the chosen one, the rest stepped to by the shoulders - see docs/LunaP.md §202.6.
    /// <summary>A row that draws only whole children, the widest run that fits and holds the chosen one; the others are laid out at no size and take no focus.</summary>
    internal sealed class WholeRow : Panel
    {
        private int _chosen;

        /// <summary>The gap between two children.</summary>
        public double Spacing { get; init; } = 8;

        /// <summary>The child that is always drawn.</summary>
        public int Chosen
        {
            get => _chosen;
            set
            {
                _chosen = value;
                InvalidateArrange();
            }
        }

        /// <summary>The first and last child drawn at the width last arranged.</summary>
        public (int First, int Last) Drawn { get; private set; } = (0, -1);

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = 0, height = 0;
            for (int i = 0; i < Children.Count; i++)
            {
                Control child = Children[i];
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                width += child.DesiredSize.Width + (i > 0 ? Spacing : 0);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(Math.Min(width, availableSize.Width), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Drawn = Window(finalSize.Width);
            double x = 0;
            for (int i = 0; i < Children.Count; i++)
            {
                Control child = Children[i];
                bool shown = i >= Drawn.First && i <= Drawn.Last;
                child.Focusable = shown;
                child.IsHitTestVisible = shown;
                if (!shown)
                {
                    child.Arrange(default);
                    continue;
                }
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width + Spacing;
            }
            return finalSize;
        }

        // Grown from the chosen child one step right then one step left while the next still fits whole.
        private (int First, int Last) Window(double width)
        {
            int count = Children.Count;
            if (count == 0) return (0, -1);
            int first = Math.Clamp(_chosen, 0, count - 1), last = first;
            double used = Children[first].DesiredSize.Width;
            for (bool grew = true; grew;)
            {
                grew = false;
                if (last + 1 < count && used + Spacing + Children[last + 1].DesiredSize.Width <= width + 0.5)
                {
                    used += Spacing + Children[++last].DesiredSize.Width;
                    grew = true;
                }
                if (first > 0 && used + Spacing + Children[first - 1].DesiredSize.Width <= width + 0.5)
                {
                    used += Spacing + Children[--first].DesiredSize.Width;
                    grew = true;
                }
            }
            return (first, last);
        }
    }
}
