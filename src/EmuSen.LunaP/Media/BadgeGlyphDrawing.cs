using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Media
{
    // The toolkit's own badge and controller drawings, plain geometry in one colour in a square box - see docs/LunaP.md §180.
    internal static class BadgeGlyphDrawing
    {
        private static (double S, Point C, IBrush Brush, IPen Pen, IPen Thin) Tools(Rect box, Color colour)
        {
            double s = Math.Min(box.Width, box.Height);
            var brush = new ImmutableSolidColorBrush(colour);
            return (s, box.Center, brush, new ImmutablePen(brush, Math.Max(1, s * 0.075), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round),
                new ImmutablePen(brush, Math.Max(1, s * 0.055), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
        }

        internal static void Draw(DrawingContext dc, Rect box, BadgeKind kind, Color colour)
        {
            (double s, Point c, IBrush brush, IPen pen, IPen thin) = Tools(box, colour);
            if (s <= 0) return;
            if (kind != BadgeKind.FolderLink) dc.DrawRectangle(null, pen, Square(c, s * 0.86), s * 0.16, s * 0.16);
            switch (kind)
            {
                case BadgeKind.Favorite:
                    dc.DrawGeometry(brush, null, StarRating.StarGeometry(Square(c, s * 0.56)));
                    break;
                case BadgeKind.Completed:
                    Polyline(dc, pen, c, s, (-0.2, 0.0), (-0.05, 0.16), (0.22, -0.16));
                    break;
                case BadgeKind.KidGame:
                    dc.DrawEllipse(null, thin, c, s * 0.25, s * 0.25);
                    dc.DrawEllipse(brush, null, c + new Vector(-s * 0.09, -s * 0.06), s * 0.035, s * 0.035);
                    dc.DrawEllipse(brush, null, c + new Vector(s * 0.09, -s * 0.06), s * 0.035, s * 0.035);
                    dc.DrawGeometry(null, thin, Arc(c + new Vector(0, s * 0.02), s * 0.13, 20, 160));
                    break;
                case BadgeKind.Broken:
                    Polyline(dc, pen, c, s, (0.04, -0.3), (-0.08, -0.08), (0.08, 0.02), (-0.05, 0.3));
                    break;
                case BadgeKind.Controller:
                    break;
                case BadgeKind.AltEmulator:
                    Polyline(dc, thin, c, s, (-0.22, -0.09), (0.2, -0.09));
                    Polyline(dc, thin, c, s, (0.1, -0.18), (0.22, -0.09), (0.1, 0.0));
                    Polyline(dc, thin, c, s, (0.22, 0.11), (-0.2, 0.11));
                    Polyline(dc, thin, c, s, (-0.1, 0.02), (-0.22, 0.11), (-0.1, 0.2));
                    break;
                case BadgeKind.Collection:
                    dc.DrawRectangle(null, thin, new Rect(c.X - s * 0.15, c.Y - s * 0.24, s * 0.36, s * 0.3), s * 0.04, s * 0.04);
                    dc.DrawRectangle(brush, null, new Rect(c.X - s * 0.22, c.Y - s * 0.08, s * 0.36, s * 0.3), s * 0.04, s * 0.04);
                    break;
                case BadgeKind.Folder:
                    Polyline(dc, thin, c, s, (-0.26, 0.2), (-0.26, -0.2), (-0.08, -0.2), (-0.02, -0.12), (0.26, -0.12), (0.26, 0.2), (-0.26, 0.2));
                    break;
                case BadgeKind.Manual:
                    Polyline(dc, thin, c, s, (0, -0.14), (-0.1, -0.2), (-0.26, -0.2), (-0.26, 0.18), (-0.1, 0.18), (0, 0.24), (0.1, 0.18), (0.26, 0.18), (0.26, -0.2), (0.1, -0.2), (0, -0.14), (0, 0.24));
                    break;
                case BadgeKind.FolderLink:
                    dc.DrawGeometry(null, pen, Link(box));
                    break;
            }
        }

        internal static void Draw(DrawingContext dc, Rect box, ControllerShape shape, Color colour)
        {
            (double s, Point c, IBrush brush, IPen pen, IPen thin) = Tools(box, colour);
            if (s <= 0) return;
            switch (shape)
            {
                case ControllerShape.Nes:
                    dc.DrawRectangle(null, pen, new Rect(c.X - s * 0.45, c.Y - s * 0.19, s * 0.9, s * 0.38), s * 0.03, s * 0.03);
                    Cross(dc, brush, c + new Vector(-s * 0.26, 0), s * 0.2);
                    dc.DrawRectangle(brush, null, new Rect(c.X - s * 0.09, c.Y + s * 0.04, s * 0.07, s * 0.035));
                    dc.DrawRectangle(brush, null, new Rect(c.X + s * 0.02, c.Y + s * 0.04, s * 0.07, s * 0.035));
                    dc.DrawEllipse(brush, null, c + new Vector(s * 0.2, s * 0.04), s * 0.05, s * 0.05);
                    dc.DrawEllipse(brush, null, c + new Vector(s * 0.33, s * 0.04), s * 0.05, s * 0.05);
                    break;
                case ControllerShape.Snes:
                    dc.DrawRectangle(null, pen, new Rect(c.X - s * 0.46, c.Y - s * 0.2, s * 0.92, s * 0.4), s * 0.2, s * 0.2);
                    Cross(dc, brush, c + new Vector(-s * 0.26, 0), s * 0.19);
                    foreach ((double x, double y) in new[] { (0.26, -0.09), (0.26, 0.09), (0.17, 0.0), (0.35, 0.0) })
                        dc.DrawEllipse(brush, null, c + new Vector(s * x, s * y), s * 0.04, s * 0.04);
                    dc.DrawLine(thin, c + new Vector(-s * 0.07, s * 0.05), c + new Vector(-s * 0.02, s * 0.0));
                    dc.DrawLine(thin, c + new Vector(s * 0.02, s * 0.05), c + new Vector(s * 0.07, s * 0.0));
                    break;
                case ControllerShape.Nintendo64:
                    var body = new StreamGeometry();
                    using (StreamGeometryContext g = body.Open())
                    {
                        g.BeginFigure(P(c, s, -0.44, -0.18), true);
                        foreach ((double x, double y) in new[] { (0.44, -0.18), (0.44, 0.02), (0.3, 0.38), (0.2, 0.38), (0.12, 0.08), (0.08, 0.08), (0.06, 0.4), (-0.06, 0.4), (-0.08, 0.08), (-0.12, 0.08), (-0.2, 0.38), (-0.3, 0.38), (-0.44, 0.02) })
                            g.LineTo(P(c, s, x, y));
                        g.EndFigure(true);
                    }
                    dc.DrawGeometry(null, thin, body);
                    Cross(dc, brush, c + new Vector(-s * 0.27, -s * 0.06), s * 0.15);
                    dc.DrawEllipse(brush, null, c + new Vector(0, s * 0.02), s * 0.055, s * 0.055);
                    dc.DrawEllipse(brush, null, c + new Vector(s * 0.24, -s * 0.03), s * 0.045, s * 0.045);
                    dc.DrawEllipse(brush, null, c + new Vector(s * 0.33, -s * 0.1), s * 0.045, s * 0.045);
                    break;
                default:
                    var pad = new StreamGeometry();
                    using (StreamGeometryContext g = pad.Open())
                    {
                        g.BeginFigure(P(c, s, -0.3, -0.2), true);
                        g.LineTo(P(c, s, 0.3, -0.2));
                        g.ArcTo(P(c, s, 0.34, 0.3), new Size(s * 0.18, s * 0.26), 0, false, SweepDirection.Clockwise);
                        g.LineTo(P(c, s, 0.12, 0.12));
                        g.LineTo(P(c, s, -0.12, 0.12));
                        g.LineTo(P(c, s, -0.34, 0.3));
                        g.ArcTo(P(c, s, -0.3, -0.2), new Size(s * 0.18, s * 0.26), 0, false, SweepDirection.Clockwise);
                        g.EndFigure(true);
                    }
                    dc.DrawGeometry(null, thin, pad);
                    if (shape == ControllerShape.Gamepad)
                    {
                        dc.DrawEllipse(brush, null, c + new Vector(-s * 0.2, -s * 0.03), s * 0.07, s * 0.07);
                        dc.DrawEllipse(brush, null, c + new Vector(s * 0.2, -s * 0.03), s * 0.07, s * 0.07);
                    }
                    else dc.DrawGeometry(brush, null, PadGlyphDrawing.Letter("?", c + new Vector(0, -s * 0.03), s * 0.32));
                    break;
            }
        }

        private static Rect Square(Point c, double side) => new(c.X - side / 2, c.Y - side / 2, side, side);

        private static Point P(Point c, double s, double x, double y) => new(c.X + x * s, c.Y + y * s);

        private static void Polyline(DrawingContext dc, IPen pen, Point c, double s, params (double X, double Y)[] points)
        {
            var line = new StreamGeometry();
            using (StreamGeometryContext g = line.Open())
            {
                g.BeginFigure(P(c, s, points[0].X, points[0].Y), false);
                for (int i = 1; i < points.Length; i++) g.LineTo(P(c, s, points[i].X, points[i].Y));
                g.EndFigure(false);
            }
            dc.DrawGeometry(null, pen, line);
        }

        // A plus of two filled bars, a pad's directional cross.
        private static void Cross(DrawingContext dc, IBrush brush, Point c, double size)
        {
            double arm = size * 0.34;
            dc.DrawRectangle(brush, null, new Rect(c.X - size / 2, c.Y - arm / 2, size, arm));
            dc.DrawRectangle(brush, null, new Rect(c.X - arm / 2, c.Y - size / 2, arm, size));
        }

        // An arc of a circle between two angles in degrees, clockwise from the right.
        private static Geometry Arc(Point c, double r, double from, double to)
        {
            var arc = new StreamGeometry();
            using (StreamGeometryContext g = arc.Open())
            {
                double a = from * Math.PI / 180, b = to * Math.PI / 180;
                g.BeginFigure(new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)), false);
                g.ArcTo(new Point(c.X + r * Math.Cos(b), c.Y + r * Math.Sin(b)), new Size(r, r), 0, to - from > 180, SweepDirection.Clockwise);
                g.EndFigure(false);
            }
            return arc;
        }

        // Two rounded links overlapping on the rising diagonal, a chain's two ends, as the outline a pen strokes.
        internal static Geometry Link(Rect box)
        {
            double s = Math.Min(box.Width, box.Height);
            Point c = box.Center;
            var link = new GeometryGroup
            {
                Children =
                {
                    new RectangleGeometry(new Rect(c.X - s * 0.4, c.Y - s * 0.14, s * 0.48, s * 0.28), s * 0.14, s * 0.14),
                    new RectangleGeometry(new Rect(c.X - s * 0.08, c.Y - s * 0.14, s * 0.48, s * 0.28), s * 0.14, s * 0.14),
                },
                Transform = new MatrixTransform(Matrix.CreateTranslation(-c.X, -c.Y) * Matrix.CreateRotation(-Math.PI / 4) * Matrix.CreateTranslation(c.X, c.Y)),
            };
            return link;
        }

        // The ground a drawn link needs cleared beneath it: its outline widened to three times its pen, so a badge under it is cut where the link crosses.
        internal static Geometry LinkGround(Rect box)
        {
            double s = Math.Min(box.Width, box.Height);
            return Link(box).GetWidenedGeometry(new Pen(Brushes.White, Math.Max(3, s * 0.075 * 3)));
        }
    }
}
