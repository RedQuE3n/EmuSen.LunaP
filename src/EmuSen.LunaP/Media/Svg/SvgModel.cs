using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Media
{
    // What a parsed SVG draws: groups and shapes with their transforms, opacities, clips and paints - see docs/LunaP.md §99.1.
    internal abstract class SvgNode
    {
        internal Matrix Transform = Matrix.Identity;
        internal double Opacity = 1;
        internal Geometry? Clip;

        internal void Draw(DrawingContext dc)
        {
            using DrawingContext.PushedState t = dc.PushTransform(Transform);
            using DrawingContext.PushedState? o = Opacity < 1 ? dc.PushOpacity(Opacity) : null;
            using DrawingContext.PushedState? c = Clip is null ? null : dc.PushGeometryClip(Clip);
            DrawContent(dc);
        }

        protected abstract void DrawContent(DrawingContext dc);
    }

    internal sealed class SvgGroup : SvgNode
    {
        internal readonly List<SvgNode> Children = new();

        protected override void DrawContent(DrawingContext dc)
        {
            foreach (SvgNode child in Children) child.Draw(dc);
        }
    }

    internal sealed class SvgShape : SvgNode
    {
        internal SvgShape(Geometry geometry) => Geometry = geometry;

        internal Geometry Geometry { get; }
        internal SvgPaint? Fill;
        internal SvgPaint? Stroke;
        internal double StrokeWidth = 1;
        internal PenLineJoin Join = PenLineJoin.Miter;
        internal PenLineCap Cap = PenLineCap.Flat;
        internal double MiterLimit = 4;
        internal double[]? Dashes;
        internal double DashOffset;

        protected override void DrawContent(DrawingContext dc)
        {
            Rect bounds = Geometry.Bounds;
            IBrush? fill = Fill?.Brush(bounds);
            IPen? pen = null;
            if (Stroke is not null && StrokeWidth > 0 && Stroke.Brush(bounds) is { } strokeBrush)
            {
                IDashStyle? dash = Dashes is { Length: > 0 } d && d.Any(v => v > 0) ? new DashStyle(d.Select(v => v / StrokeWidth), DashOffset / StrokeWidth) : null;
                pen = new Pen(strokeBrush, StrokeWidth, dash, Cap, Join, MiterLimit);
            }

            if (fill is not null || pen is not null) dc.DrawGeometry(fill, pen, Geometry);
        }
    }

    // A fill or a stroke: a flat colour, or a linear gradient laid out against the shape it paints.
    internal abstract class SvgPaint
    {
        internal abstract IBrush? Brush(Rect bounds);
    }

    internal sealed class SvgSolid : SvgPaint
    {
        private readonly IBrush _brush;

        internal SvgSolid(Color colour, double opacity) =>
            _brush = new ImmutableSolidColorBrush(Color.FromArgb((byte)Math.Round(colour.A * Math.Clamp(opacity, 0, 1)), colour.R, colour.G, colour.B));

        internal override IBrush? Brush(Rect bounds) => _brush;
    }

    internal sealed record SvgStop(double Offset, Color Colour);

    internal sealed class SvgLinear : SvgPaint
    {
        internal required Point Start;
        internal required Point End;
        internal required bool BoundingBoxUnits;
        internal required Matrix GradientTransform;
        internal required GradientSpreadMethod Spread;
        internal required IReadOnlyList<SvgStop> Stops;
        internal double Opacity = 1;

        internal override IBrush? Brush(Rect bounds)
        {
            if (Stops.Count == 0) return null;
            if (Stops.Count == 1) return new SvgSolid(Stops[0].Colour, Opacity).Brush(bounds);
            Matrix placement = GradientTransform;
            if (BoundingBoxUnits)
            {
                if (bounds.Width <= 0 || bounds.Height <= 0) return null;
                placement = placement * Matrix.CreateScale(bounds.Width, bounds.Height) * Matrix.CreateTranslation(bounds.X, bounds.Y);
            }

            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(Start, RelativeUnit.Absolute),
                EndPoint = new RelativePoint(End, RelativeUnit.Absolute),
                SpreadMethod = Spread,
                Opacity = Math.Clamp(Opacity, 0, 1),
                Transform = new MatrixTransform(placement),
                TransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
            };
            foreach (SvgStop stop in Stops) brush.GradientStops.Add(new GradientStop(stop.Colour, stop.Offset));
            return brush;
        }
    }
}
