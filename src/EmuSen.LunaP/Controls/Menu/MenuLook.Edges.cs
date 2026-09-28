using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // A scrolling area's edges in the look: faded where more lies beyond, the fade ending where the focused row begins - see docs/LunaP.md §196.10.
    public static partial class MenuLook
    {
        /// <summary>The height of a scrolling area's faded edge, in the area's own units (design pixels in a framed sheet).</summary>
        public const double EdgeFade = 36;

        /// <summary>Whether a scrolling area in the look fades its top edge, as it does while content lies above it and the focused row leaves room for a fade.</summary>
        public static readonly AttachedProperty<bool> FadesTopProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesTop", typeof(MenuLook));

        /// <summary>Whether a scrolling area in the look fades its bottom edge, as it does while content lies below it and the focused row leaves room for a fade.</summary>
        public static readonly AttachedProperty<bool> FadesBottomProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesBottom", typeof(MenuLook));

        /// <summary>Whether a scrolling area in the look that scrolls only sideways fades its left edge, as it does while content lies left of it.</summary>
        public static readonly AttachedProperty<bool> FadesLeftProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesLeft", typeof(MenuLook));

        /// <summary>Whether a scrolling area in the look that scrolls only sideways fades its right edge, as it does while content lies right of it.</summary>
        public static readonly AttachedProperty<bool> FadesRightProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesRight", typeof(MenuLook));

        /// <summary>Reads whether a scrolling area fades its left edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while its left edge fades in the look.</returns>
        public static bool GetFadesLeft(ScrollViewer viewer) => viewer.GetValue(FadesLeftProperty);

        /// <summary>Reads whether a scrolling area fades its right edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while its right edge fades in the look.</returns>
        public static bool GetFadesRight(ScrollViewer viewer) => viewer.GetValue(FadesRightProperty);

        /// <summary>Reads whether a scrolling area fades its top edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while its top edge fades in the look.</returns>
        public static bool GetFadesTop(ScrollViewer viewer) => viewer.GetValue(FadesTopProperty);

        /// <summary>Reads whether a scrolling area fades its bottom edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while its bottom edge fades in the look.</returns>
        public static bool GetFadesBottom(ScrollViewer viewer) => viewer.GetValue(FadesBottomProperty);

        private static void SetUpEdges()
        {
            ScrollViewer.OffsetProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => FadeSoon(viewer));
            ScrollViewer.ExtentProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => FadeSoon(viewer));
            ScrollViewer.ViewportProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => FadeSoon(viewer));
            InputElement.GotFocusEvent.AddClassHandler<ScrollViewer>((viewer, _) => FadeSoon(viewer));
            InputElement.LostFocusEvent.AddClassHandler<ScrollViewer>((viewer, _) => FadeSoon(viewer));
        }

        // A scrolling area that moves up and down, other than a text box's own.
        private static bool Fades(ScrollViewer viewer) =>
            viewer.TemplatedParent is not TextBox && viewer.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled && Covers(viewer);

        // Once layout has placed the rows, so the focused row's place is where it is drawn.
        private static void FadeSoon(ScrollViewer viewer)
        {
            if (viewer.GetValue(FadePendingProperty) || !Covers(viewer)) return;
            viewer.SetValue(FadePendingProperty, true);
            Dispatcher.UIThread.Post(() =>
            {
                viewer.SetValue(FadePendingProperty, false);
                Fade(viewer);
            }, DispatcherPriority.Loaded);
        }

        private static void Fade(ScrollViewer viewer)
        {
            if (viewer.Presenter is not ScrollContentPresenter presenter) return;
            // An area that scrolls up and down fades its top and foot; one that scrolls only sideways, such as a strip of tiles, its two sides.
            bool down = Fades(viewer) && viewer.Extent.Height > viewer.Viewport.Height + 0.5;
            bool across = !down && FadesAcross(viewer) && viewer.Extent.Width > viewer.Viewport.Width + 0.5;
            double length = across ? viewer.Viewport.Width : viewer.Viewport.Height;
            double offset = across ? viewer.Offset.X : viewer.Offset.Y, extent = across ? viewer.Extent.Width : viewer.Extent.Height;
            bool on = (down || across) && length > 2 * EdgeFade;
            double start = on && offset > 0.5 ? EdgeFade : 0;
            double end = on && offset < extent - length - 0.5 ? EdgeFade : 0;
            // The fade stops at the focused row, so the row with the focus is never dimmed and only what lies past it fades (§196.10).
            if (FocusedRow(presenter) is { } row && (across ? row.Right > 0 && row.Left < length : row.Bottom > 0 && row.Top < length))
            {
                double near = across ? row.Left : row.Top, far = across ? row.Right : row.Bottom;
                if (near < start) start = Math.Max(0, near);
                if (far > length - end) end = Math.Max(0, length - far);
            }
            bool fadesStart = start > 0.5, fadesEnd = end > 0.5;
            viewer.SetValue(FadesTopProperty, !across && fadesStart);
            viewer.SetValue(FadesBottomProperty, !across && fadesEnd);
            viewer.SetValue(FadesLeftProperty, across && fadesStart);
            viewer.SetValue(FadesRightProperty, across && fadesEnd);
            if (!fadesStart && !fadesEnd)
            {
                if (presenter.OpacityMask is { } ours && ReferenceEquals(ours, presenter.GetValue(FadeMaskProperty))) presenter.OpacityMask = null;
                return;
            }
            var mask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(across ? 1 : 0, across ? 0 : 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(fadesStart ? Colors.Transparent : Colors.Black, 0),
                    new GradientStop(Colors.Black, Math.Clamp(start / length, 0, 0.5)),
                    new GradientStop(Colors.Black, 1 - Math.Clamp(end / length, 0, 0.5)),
                    new GradientStop(fadesEnd ? Colors.Transparent : Colors.Black, 1),
                },
            };
            presenter.SetValue(FadeMaskProperty, mask);
            presenter.OpacityMask = mask;
        }

        // A scrolling area that moves sideways, other than a text box's own.
        private static bool FadesAcross(ScrollViewer viewer) =>
            viewer.TemplatedParent is not TextBox && viewer.HorizontalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled && Covers(viewer);

        // Where the focused control's row lies in the area's viewport: its item's container when it is in a list, else itself; null when the focus is elsewhere.
        private static Rect? FocusedRow(ScrollContentPresenter presenter)
        {
            if (TopLevel.GetTopLevel(presenter)?.FocusManager?.GetFocusedElement() is not Visual focused || !presenter.IsVisualAncestorOf(focused)) return null;
            Visual row = focused;
            for (Visual? at = focused; at is not null && !ReferenceEquals(at, presenter); at = at.GetVisualParent())
                if (at is Control c && ItemsControl.ItemsControlFromItemContainer(c) is not null)
                {
                    row = c;
                    break;
                }
            return row.TransformToVisual(presenter) is { } m ? new Rect(row.Bounds.Size).TransformToAABB(m) : null;
        }

        // The mask the look put on a presenter, so it only ever takes away its own.
        private static readonly AttachedProperty<IBrush?> FadeMaskProperty =
            AvaloniaProperty.RegisterAttached<ScrollContentPresenter, IBrush?>("FadeMask", typeof(MenuLook));

        private static readonly AttachedProperty<bool> FadePendingProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadePending", typeof(MenuLook));
    }
}
