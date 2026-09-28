using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // A scrolling area's edges in the look: faded where more lies beyond, and a control brought into view kept clear of the fade - see docs/LunaP.md §196.10.
    public static partial class MenuLook
    {
        /// <summary>The height of a scrolling area's faded edge, in the area's own units (design pixels in a framed sheet).</summary>
        public const double EdgeFade = 36;

        /// <summary>Whether a scrolling area in the look fades its top edge, as it does while content lies above it.</summary>
        public static readonly AttachedProperty<bool> FadesTopProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesTop", typeof(MenuLook));

        /// <summary>Whether a scrolling area in the look fades its bottom edge, as it does while content lies below it.</summary>
        public static readonly AttachedProperty<bool> FadesBottomProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("FadesBottom", typeof(MenuLook));

        /// <summary>Reads whether a scrolling area fades its top edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while content lies above it in the look.</returns>
        public static bool GetFadesTop(ScrollViewer viewer) => viewer.GetValue(FadesTopProperty);

        /// <summary>Reads whether a scrolling area fades its bottom edge.</summary>
        /// <param name="viewer">The scrolling area.</param>
        /// <returns>True while content lies below it in the look.</returns>
        public static bool GetFadesBottom(ScrollViewer viewer) => viewer.GetValue(FadesBottomProperty);

        private static void SetUpEdges()
        {
            ScrollViewer.OffsetProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => Fade(viewer));
            ScrollViewer.ExtentProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => Fade(viewer));
            ScrollViewer.ViewportProperty.Changed.AddClassHandler<ScrollViewer>((viewer, _) => Fade(viewer));
            // A control brought into view is brought clear of the faded edges, so the one with the focus is never under a fade.
            Control.RequestBringIntoViewEvent.AddClassHandler<Control>((control, e) =>
            {
                if (!ReferenceEquals(e.Source, control) || !Covers(control) || control.FindAncestorOfType<ScrollViewer>() is not { } viewer || !Fades(viewer)) return;
                e.TargetRect = e.TargetRect.Inflate(new Thickness(0, EdgeFade));
            });
        }

        // A scrolling area that moves up and down, other than a text box's own.
        private static bool Fades(ScrollViewer viewer) =>
            viewer.TemplatedParent is not TextBox && viewer.VerticalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled && Covers(viewer);

        private static void Fade(ScrollViewer viewer)
        {
            if (viewer.Presenter is not ScrollContentPresenter presenter) return;
            bool on = Fades(viewer) && viewer.Extent.Height > viewer.Viewport.Height + 0.5 && viewer.Viewport.Height > 2 * EdgeFade;
            bool top = on && viewer.Offset.Y > 0.5;
            bool bottom = on && viewer.Offset.Y < viewer.Extent.Height - viewer.Viewport.Height - 0.5;
            viewer.SetValue(FadesTopProperty, top);
            viewer.SetValue(FadesBottomProperty, bottom);
            if (!top && !bottom)
            {
                if (presenter.OpacityMask is LinearGradientBrush { } ours && ReferenceEquals(ours, presenter.GetValue(FadeMaskProperty))) presenter.OpacityMask = null;
                return;
            }
            double f = Math.Clamp(EdgeFade / viewer.Viewport.Height, 0, 0.5);
            var mask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(top ? Colors.Transparent : Colors.Black, 0),
                    new GradientStop(Colors.Black, f),
                    new GradientStop(Colors.Black, 1 - f),
                    new GradientStop(bottom ? Colors.Transparent : Colors.Black, 1),
                },
            };
            presenter.SetValue(FadeMaskProperty, mask);
            presenter.OpacityMask = mask;
        }

        // The mask the look put on a presenter, so it only ever takes away its own.
        private static readonly AttachedProperty<IBrush?> FadeMaskProperty =
            AvaloniaProperty.RegisterAttached<ScrollContentPresenter, IBrush?>("FadeMask", typeof(MenuLook));
    }
}
