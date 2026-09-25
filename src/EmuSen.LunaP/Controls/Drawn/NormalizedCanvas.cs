using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // Children placed by fractions of the canvas, an origin, a rotation and a depth - see docs/LunaP.md §98.1.
    /// <summary>A panel that places each child by a position, size and origin given as fractions of the panel, rotates it about a point of its own, and draws its children in order of depth.</summary>
    public class NormalizedCanvas : Panel
    {
        /// <summary>The point of the panel, as fractions of its width and height, where the child's origin is placed. (0, 0) by default.</summary>
        public static readonly AttachedProperty<Point> PositionProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, Point>("Position");

        /// <summary>The child's size as fractions of the panel. An axis of 0 takes the child's own desired size on that axis. (0, 0) by default.</summary>
        public static readonly AttachedProperty<Size> SizeProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, Size>("Size");

        /// <summary>The largest box the child may measure to, as fractions of the panel, used where Size is 0 on an axis. (0, 0), no limit, by default.</summary>
        public static readonly AttachedProperty<Size> MaxSizeProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, Size>("MaxSize");

        /// <summary>The point of the child, as fractions of its own size, that Position names. (0, 0), the top left, by default.</summary>
        public static readonly AttachedProperty<Point> OriginProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, Point>("Origin");

        /// <summary>A clockwise rotation in degrees. 0 by default.</summary>
        public static readonly AttachedProperty<double> RotationProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, double>("Rotation");

        /// <summary>The point of the child, as fractions of its own size, it rotates about. (0.5, 0.5), the centre, by default.</summary>
        public static readonly AttachedProperty<Point> RotationOriginProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, Point>("RotationOrigin", new Point(0.5, 0.5));

        /// <summary>The drawing order: lower depths are drawn first, and ties keep the order of Children. 0 by default.</summary>
        public static readonly AttachedProperty<double> DepthProperty =
            AvaloniaProperty.RegisterAttached<NormalizedCanvas, Control, double>("Depth");

        static NormalizedCanvas()
        {
            AffectsParentMeasure<NormalizedCanvas>(SizeProperty, MaxSizeProperty);
            AffectsParentArrange<NormalizedCanvas>(PositionProperty, OriginProperty, RotationProperty, RotationOriginProperty);
            DepthProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as NormalizedCanvas)?.Restack());
        }

        /// <summary>Reads a child's Position; see PositionProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static Point GetPosition(Control c) => c.GetValue(PositionProperty);
        /// <summary>Sets a child's Position; see PositionProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetPosition(Control c, Point value) => c.SetValue(PositionProperty, value);
        /// <summary>Reads a child's Size; see SizeProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static Size GetSize(Control c) => c.GetValue(SizeProperty);
        /// <summary>Sets a child's Size; see SizeProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetSize(Control c, Size value) => c.SetValue(SizeProperty, value);
        /// <summary>Reads a child's MaxSize; see MaxSizeProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static Size GetMaxSize(Control c) => c.GetValue(MaxSizeProperty);
        /// <summary>Sets a child's MaxSize; see MaxSizeProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetMaxSize(Control c, Size value) => c.SetValue(MaxSizeProperty, value);
        /// <summary>Reads a child's Origin; see OriginProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static Point GetOrigin(Control c) => c.GetValue(OriginProperty);
        /// <summary>Sets a child's Origin; see OriginProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetOrigin(Control c, Point value) => c.SetValue(OriginProperty, value);
        /// <summary>Reads a child's Rotation; see RotationProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static double GetRotation(Control c) => c.GetValue(RotationProperty);
        /// <summary>Sets a child's Rotation; see RotationProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetRotation(Control c, double value) => c.SetValue(RotationProperty, value);
        /// <summary>Reads a child's RotationOrigin; see RotationOriginProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static Point GetRotationOrigin(Control c) => c.GetValue(RotationOriginProperty);
        /// <summary>Sets a child's RotationOrigin; see RotationOriginProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetRotationOrigin(Control c, Point value) => c.SetValue(RotationOriginProperty, value);
        /// <summary>Reads a child's Depth; see DepthProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <returns>The child's value.</returns>
        public static double GetDepth(Control c) => c.GetValue(DepthProperty);
        /// <summary>Sets a child's Depth; see DepthProperty.</summary>
        /// <param name="c">A child of the canvas.</param>
        /// <param name="value">The new value.</param>
        public static void SetDepth(Control c, double value) => c.SetValue(DepthProperty, value);

        /// <summary>Where a child is laid out, in the panel's coordinates, before its rotation.</summary>
        /// <param name="panel">The panel's size.</param>
        /// <param name="position">The child's Position, as fractions of the panel.</param>
        /// <param name="origin">The child's Origin, as fractions of the child.</param>
        /// <param name="child">The child's size in pixels.</param>
        /// <returns>The child's rectangle.</returns>
        public static Rect Place(Size panel, Point position, Point origin, Size child) =>
            new(position.X * panel.Width - origin.X * child.Width, position.Y * panel.Height - origin.Y * child.Height, child.Width, child.Height);

        // The box a child is measured in: its fraction of the panel, else its maximum, else unbounded.
        private static Size Available(Size panel, Control child)
        {
            Size size = GetSize(child), max = GetMaxSize(child);
            double w = size.Width > 0 ? size.Width * panel.Width : max.Width > 0 ? max.Width * panel.Width : double.PositiveInfinity;
            double h = size.Height > 0 ? size.Height * panel.Height : max.Height > 0 ? max.Height * panel.Height : double.PositiveInfinity;
            return new Size(w, h);
        }

        // The size a child is given: its fraction on an axis that has one, else what it asked for.
        private static Size Given(Size panel, Control child)
        {
            Size size = GetSize(child);
            return new Size(size.Width > 0 ? size.Width * panel.Width : child.DesiredSize.Width,
                size.Height > 0 ? size.Height * panel.Height : child.DesiredSize.Height);
        }

        protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            base.ChildrenChanged(sender, e);
            Restack();
        }

        // Depth is a real number and Avalonia's ZIndex an integer, so each child's ZIndex is its rank by depth.
        private void Restack()
        {
            List<Control> ordered = Children.Select((c, i) => (c, i)).OrderBy(p => GetDepth(p.c)).ThenBy(p => p.i).Select(p => p.c).ToList();
            for (int rank = 0; rank < ordered.Count; rank++) ordered[rank].ZIndex = rank;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size panel = Finite(availableSize);
            foreach (Control child in Children) child.Measure(Available(panel, child));
            return panel;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (Control child in Children)
            {
                Size given = Given(finalSize, child);
                child.Arrange(Place(finalSize, GetPosition(child), GetOrigin(child), given));
                double angle = GetRotation(child);
                if (angle == 0)
                {
                    child.RenderTransform = null;
                    continue;
                }

                Point about = GetRotationOrigin(child);
                child.RenderTransformOrigin = new RelativePoint(about, RelativeUnit.Relative);
                child.RenderTransform = new RotateTransform(angle);
            }

            return finalSize;
        }

        // A canvas in an unbounded parent has no size to take fractions of, so it takes none.
        private static Size Finite(Size s) => new(double.IsInfinity(s.Width) ? 0 : s.Width, double.IsInfinity(s.Height) ? 0 : s.Height);

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Group);
    }
}
