using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // Rows shown whole or not at all: the scroll snapped to a row's top where it can be, and any row the viewport still cuts left undrawn - see docs/LunaP.md §211.5.
    public partial class LunaTable<T> where T : class
    {
        private bool _wholeRows;
        private bool _snapping;
        private double _snappedTo = double.NaN;

        /// <summary>Whether only whole rows are drawn: a scroll that stops inside a row moves on to the next row's top, with room made under the last row where the rows end, and a row the viewport still cuts at the foot is left undrawn. False by default.</summary>
        public bool WholeRows
        {
            get => _wholeRows;
            set
            {
                _wholeRows = value;
                if (!value && RowsPresenter(OwnViewer()) is { } presenter) presenter.Clip = null;
                InvalidateArrange();
            }
        }

        /// <summary>The part of the rows' viewport drawn, in the viewport's coordinates; all of it unless WholeRows leaves a cut row undrawn.</summary>
        public Rect DrawnRows => RowsPresenter(OwnViewer()) is { } presenter ? presenter.Clip?.Bounds ?? new Rect(presenter.Bounds.Size) : default;

        // The viewer's presenter, what the rows are drawn in.
        private static ScrollContentPresenter? RowsPresenter(ScrollViewer? viewer) => viewer?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();

        // Each realised row's top and bottom in the viewport, in order.
        private (double Top, double Bottom)[] RowSpans(ScrollViewer viewer, Visual presenter) =>
            Rows!.GetRealizedContainers().OfType<Control>().Where(c => c.IsVisible && c.Bounds.Height > 0)
                .Select(c => c.TranslatePoint(default, presenter) is { } at ? (at.Y, at.Y + c.Bounds.Height) : (double.NaN, double.NaN))
                .Where(s => !double.IsNaN(s.Item1)).OrderBy(s => s.Item1).ToArray();

        private void KeepRowsWhole()
        {
            if (!_wholeRows || _snapping || Rows is null || OwnViewer() is not { } viewer || RowsPresenter(viewer) is not { } presenter) return;
            (double Top, double Bottom)[] spans = RowSpans(viewer, presenter);
            double height = presenter.Bounds.Height, slack = 0.5;
            if (spans.Length == 0 || height <= 0) return;
            // A scroll that stopped inside the first row goes on to the next row's top; a row taller than the viewport can never be whole and is left as it is.
            if (spans.FirstOrDefault(s => s.Top < -slack && s.Bottom > slack) is { Bottom: > 0 } cut && cut.Bottom - cut.Top <= height + slack)
            {
                double target = viewer.Offset.Y + cut.Bottom, end = viewer.Extent.Height - viewer.Viewport.Height;
                // At the end of the rows room is made under the last row, never more than a viewport of it, so the rows stay flush with the top.
                double room = Math.Ceiling(target - end);
                bool roomy = room <= slack || Rows.Padding.Bottom + room <= height;
                // A snap that left the rows cut where it put them is not tried again there, so layout never chases it.
                if (roomy && !(Math.Abs(viewer.Offset.Y - _snappedTo) <= slack))
                {
                    _snappedTo = target;
                    _snapping = true;
                    try
                    {
                        if (room > slack) Rows.Padding = new Thickness(Rows.Padding.Left, Rows.Padding.Top, Rows.Padding.Right, Rows.Padding.Bottom + room);
                        viewer.Offset = new Vector(viewer.Offset.X, target);
                    }
                    finally { _snapping = false; }
                    return;
                }
            }
            double top = spans.Where(s => s.Top >= -slack).Select(s => s.Top).DefaultIfEmpty(0).Min();
            double bottom = spans.Where(s => s.Top >= -slack && s.Bottom <= height + slack).Select(s => s.Bottom).DefaultIfEmpty(height).Max();
            bool whole = top <= slack && spans.All(s => s.Bottom <= height + slack || s.Top >= height - slack);
            Geometry? clip = whole ? null : new RectangleGeometry(new Rect(0, Math.Max(0, top), presenter.Bounds.Width, Math.Max(0, bottom - Math.Max(0, top))));
            // The clip is set only when it changes, so drawing it asks for no layout of its own.
            if (clip?.Bounds != presenter.Clip?.Bounds) presenter.Clip = clip;
        }
    }
}
