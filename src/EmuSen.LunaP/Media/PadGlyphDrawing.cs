using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Media
{
    // The toolkit's own drawings of a gamepad's buttons, one set per family, in one colour and in a square box - see docs/LunaP.md §103.
    //
    // Every shape here is plain geometry or a letter from the application's default typeface: a ring, a disc, a
    // cross, a pill. None is traced from a vendor's artwork or from another frontend's icons, and none carries a
    // vendor's colours; a host that wants those supplies its own image files through HintEntry.IconPath, which
    // always wins. What tells the families apart is what a player reads off the pad: the letters and their
    // positions, the four shapes, the shoulder names, the two middle buttons' marks.
    //
    // The face buttons are named by position (South is the bottom one), as SDL names them, so a Nintendo pad's
    // South is its B and an Xbox pad's is its A; the letters below follow that.
    internal static class PadGlyphDrawing
    {
        internal static void Draw(DrawingContext dc, Rect box, PadFamily family, PadGlyphButton button, Color colour)
        {
            double s = Math.Min(box.Width, box.Height);
            if (s <= 0) return;
            var brush = new ImmutableSolidColorBrush(colour);
            var pen = new ImmutablePen(brush, Math.Max(1, s * 0.075), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var thin = new ImmutablePen(brush, Math.Max(1, s * 0.06), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            Point c = box.Center;

            switch (button)
            {
                case PadGlyphButton.South or PadGlyphButton.East or PadGlyphButton.West or PadGlyphButton.North:
                    Face(dc, c, s, family, button, brush, pen, thin);
                    break;
                case PadGlyphButton.DPad or PadGlyphButton.DPadUpDown or PadGlyphButton.DPadLeftRight:
                    DPad(dc, c, s, button, brush, thin);
                    break;
                case PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder or PadGlyphButton.LeftTrigger or PadGlyphButton.RightTrigger:
                    Shoulder(dc, c, s, family, button, brush, thin);
                    break;
                case PadGlyphButton.Start or PadGlyphButton.Select:
                    Middle(dc, c, s, family, button == PadGlyphButton.Start, brush, pen, thin);
                    break;
                default:
                    dc.DrawEllipse(null, pen, c, s * 0.42, s * 0.42);
                    dc.DrawEllipse(brush, null, c, s * 0.16, s * 0.16);
                    break;
            }
        }

        // The letter a family prints on a face button at a position; null for the families that print shapes or nothing.
        internal static string? FaceLetter(PadFamily family, PadGlyphButton button) => family switch
        {
            PadFamily.Xbox => button switch { PadGlyphButton.South => "A", PadGlyphButton.East => "B", PadGlyphButton.West => "X", _ => "Y" },
            PadFamily.Nintendo => button switch { PadGlyphButton.South => "B", PadGlyphButton.East => "A", PadGlyphButton.West => "Y", _ => "X" },
            _ => null,
        };

        // The words a family prints on its shoulders and triggers.
        internal static string ShoulderLabel(PadFamily family, PadGlyphButton button) => (family, button) switch
        {
            (PadFamily.Xbox, PadGlyphButton.LeftShoulder) => "LB",
            (PadFamily.Xbox, PadGlyphButton.RightShoulder) => "RB",
            (PadFamily.Xbox, PadGlyphButton.LeftTrigger) => "LT",
            (PadFamily.Xbox, PadGlyphButton.RightTrigger) => "RT",
            (PadFamily.Nintendo, PadGlyphButton.LeftShoulder) => "L",
            (PadFamily.Nintendo, PadGlyphButton.RightShoulder) => "R",
            (PadFamily.Nintendo, PadGlyphButton.LeftTrigger) => "ZL",
            (PadFamily.Nintendo, PadGlyphButton.RightTrigger) => "ZR",
            (_, PadGlyphButton.LeftShoulder) => "L1",
            (_, PadGlyphButton.RightShoulder) => "R1",
            (_, PadGlyphButton.LeftTrigger) => "L2",
            _ => "R2",
        };

        private static void Face(DrawingContext dc, Point c, double s, PadFamily family, PadGlyphButton button, IBrush brush, IPen pen, IPen thin)
        {
            switch (family)
            {
                // Four dots in a diamond, the one meant filled: a position, for a pad whose printing is not known.
                case PadFamily.Generic:
                    foreach (PadGlyphButton at in new[] { PadGlyphButton.North, PadGlyphButton.East, PadGlyphButton.South, PadGlyphButton.West })
                    {
                        Point p = c + Direction(at) * (s * 0.29);
                        dc.DrawEllipse(at == button ? brush : null, at == button ? null : thin, p, s * 0.14, s * 0.14);
                    }
                    break;

                case PadFamily.PlayStation:
                    dc.DrawEllipse(null, pen, c, s * 0.44, s * 0.44);
                    Shape(dc, c, s * 0.2, button, thin);
                    break;

                // A ring round the letter for one layout, a disc with the letter cut out of it for the other, so the two lettered families differ at a glance.
                case PadFamily.Nintendo:
                    Geometry disc = new EllipseGeometry(new Rect(c.X - s * 0.46, c.Y - s * 0.46, s * 0.92, s * 0.92));
                    dc.DrawGeometry(brush, null, new CombinedGeometry(GeometryCombineMode.Exclude, disc, Letter(FaceLetter(family, button)!, c, s * 0.58)));
                    break;

                default:
                    dc.DrawEllipse(null, pen, c, s * 0.44, s * 0.44);
                    dc.DrawGeometry(brush, null, Letter(FaceLetter(family, button)!, c, s * 0.56));
                    break;
            }
        }

        private static Vector Direction(PadGlyphButton at) => at switch
        {
            PadGlyphButton.North => new Vector(0, -1),
            PadGlyphButton.South => new Vector(0, 1),
            PadGlyphButton.West => new Vector(-1, 0),
            _ => new Vector(1, 0),
        };

        // The four shapes printed on one family's face buttons, by position: a cross at the bottom, a circle to the right, a square to the left, a triangle at the top.
        private static void Shape(DrawingContext dc, Point c, double r, PadGlyphButton button, IPen pen)
        {
            switch (button)
            {
                case PadGlyphButton.South:
                    dc.DrawLine(pen, c + new Vector(-r, -r), c + new Vector(r, r));
                    dc.DrawLine(pen, c + new Vector(-r, r), c + new Vector(r, -r));
                    break;
                case PadGlyphButton.East:
                    dc.DrawEllipse(null, pen, c, r, r);
                    break;
                case PadGlyphButton.West:
                    dc.DrawRectangle(null, pen, new Rect(c.X - r * 0.9, c.Y - r * 0.9, r * 1.8, r * 1.8));
                    break;
                default:
                    var triangle = new StreamGeometry();
                    using (StreamGeometryContext g = triangle.Open())
                    {
                        g.BeginFigure(c + new Vector(0, -r * 1.05), true);
                        g.LineTo(c + new Vector(r * 1.0, r * 0.7));
                        g.LineTo(c + new Vector(-r * 1.0, r * 0.7));
                        g.EndFigure(true);
                    }
                    dc.DrawGeometry(null, pen, triangle);
                    break;
            }
        }

        // A plus with its arms outlined, the arms the hint moves along filled.
        private static void DPad(DrawingContext dc, Point c, double s, PadGlyphButton button, IBrush brush, IPen pen)
        {
            double arm = s * 0.28, reach = s * 0.46;
            var plus = new StreamGeometry();
            using (StreamGeometryContext g = plus.Open())
            {
                double a = arm / 2;
                g.BeginFigure(new Point(c.X - a, c.Y - reach), true);
                foreach ((double x, double y) in new[] { (a, -reach), (a, -a), (reach, -a), (reach, a), (a, a), (a, reach), (-a, reach), (-a, a), (-reach, a), (-reach, -a), (-a, -a) })
                    g.LineTo(new Point(c.X + x, c.Y + y));
                g.EndFigure(true);
            }
            dc.DrawGeometry(null, pen, plus);

            double inset = s * 0.07, half = arm / 2 - inset;
            bool vertical = button is PadGlyphButton.DPad or PadGlyphButton.DPadUpDown, horizontal = button is PadGlyphButton.DPad or PadGlyphButton.DPadLeftRight;
            if (vertical)
            {
                dc.DrawRectangle(brush, null, new Rect(c.X - half, c.Y - reach + inset, half * 2, reach - arm / 2 - inset));
                dc.DrawRectangle(brush, null, new Rect(c.X - half, c.Y + arm / 2, half * 2, reach - arm / 2 - inset));
            }
            if (horizontal)
            {
                dc.DrawRectangle(brush, null, new Rect(c.X - reach + inset, c.Y - half, reach - arm / 2 - inset, half * 2));
                dc.DrawRectangle(brush, null, new Rect(c.X + arm / 2, c.Y - half, reach - arm / 2 - inset, half * 2));
            }
        }

        // A shoulder is a bar rounded at the top; a trigger is taller and rounded at the bottom; each carries its family's name for it.
        private static void Shoulder(DrawingContext dc, Point c, double s, PadFamily family, PadGlyphButton button, IBrush brush, IPen pen)
        {
            bool trigger = button is PadGlyphButton.LeftTrigger or PadGlyphButton.RightTrigger;
            double w = s * 0.94, h = trigger ? s * 0.8 : s * 0.56, r = s * 0.2;
            var body = new Rect(c.X - w / 2, c.Y - h / 2, w, h);
            var outline = new StreamGeometry();
            using (StreamGeometryContext g = outline.Open())
            {
                double top = trigger ? s * 0.05 : r, bottom = trigger ? r : s * 0.05;
                g.BeginFigure(new Point(body.Left + top, body.Top), true);
                g.LineTo(new Point(body.Right - top, body.Top));
                g.ArcTo(new Point(body.Right, body.Top + top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                g.LineTo(new Point(body.Right, body.Bottom - bottom));
                g.ArcTo(new Point(body.Right - bottom, body.Bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                g.LineTo(new Point(body.Left + bottom, body.Bottom));
                g.ArcTo(new Point(body.Left, body.Bottom - bottom), new Size(bottom, bottom), 0, false, SweepDirection.Clockwise);
                g.LineTo(new Point(body.Left, body.Top + top));
                g.ArcTo(new Point(body.Left + top, body.Top), new Size(top, top), 0, false, SweepDirection.Clockwise);
                g.EndFigure(true);
            }
            dc.DrawGeometry(null, pen, outline);

            // With no printing known, the half on the button's own side is filled: which shoulder, not what it is called.
            if (family == PadFamily.Generic)
            {
                bool left = button is PadGlyphButton.LeftShoulder or PadGlyphButton.LeftTrigger;
                double inset = s * 0.12;
                dc.DrawRectangle(brush, null, new Rect(left ? body.Left + inset : c.X, body.Top + inset, w / 2 - inset, h - 2 * inset), s * 0.06, s * 0.06);
                return;
            }

            string label = ShoulderLabel(family, button);
            dc.DrawGeometry(brush, null, Letter(label, c, Math.Min(s * 0.44, w * 0.7 / Math.Max(1, label.Length * 0.62))));
        }

        // The two middle buttons: a ring for the families that print a mark in a circle, a pill for the others, each with its own mark.
        private static void Middle(DrawingContext dc, Point c, double s, PadFamily family, bool start, IBrush brush, IPen pen, IPen thin)
        {
            double r = s * 0.44;
            switch (family)
            {
                case PadFamily.Xbox:
                    dc.DrawEllipse(null, pen, c, r, r);
                    if (start)
                    {
                        for (int i = -1; i <= 1; i++) dc.DrawLine(thin, c + new Vector(-s * 0.18, i * s * 0.13), c + new Vector(s * 0.18, i * s * 0.13));
                    }
                    else
                    {
                        dc.DrawRectangle(null, thin, new Rect(c.X - s * 0.2, c.Y - s * 0.16, s * 0.24, s * 0.2));
                        dc.DrawRectangle(null, thin, new Rect(c.X - s * 0.04, c.Y - s * 0.04, s * 0.24, s * 0.2));
                    }
                    break;

                case PadFamily.Nintendo:
                    dc.DrawEllipse(null, pen, c, r, r);
                    dc.DrawLine(thin, c + new Vector(-s * 0.2, 0), c + new Vector(s * 0.2, 0));
                    if (start) dc.DrawLine(thin, c + new Vector(0, -s * 0.2), c + new Vector(0, s * 0.2));
                    break;

                default:
                    var pill = new Rect(c.X - s * 0.46, c.Y - s * 0.26, s * 0.92, s * 0.52);
                    dc.DrawRectangle(null, pen, pill, s * 0.26, s * 0.26);
                    if (family == PadFamily.PlayStation)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            if (start) dc.DrawLine(thin, c + new Vector(-s * 0.16, i * s * 0.09), c + new Vector(s * 0.16, i * s * 0.09));
                            else dc.DrawLine(thin, c + new Vector(i * s * 0.12, -s * 0.1), c + new Vector(i * s * 0.12, s * 0.1));
                        }
                    }
                    else if (start)
                    {
                        var play = new StreamGeometry();
                        using (StreamGeometryContext g = play.Open())
                        {
                            g.BeginFigure(c + new Vector(-s * 0.1, -s * 0.13), true);
                            g.LineTo(c + new Vector(s * 0.14, 0));
                            g.LineTo(c + new Vector(-s * 0.1, s * 0.13));
                            g.EndFigure(true);
                        }
                        dc.DrawGeometry(brush, null, play);
                    }
                    else dc.DrawRectangle(brush, null, new Rect(c.X - s * 0.1, c.Y - s * 0.1, s * 0.2, s * 0.2));
                    break;
            }
        }

        // A word in the default typeface as geometry, its ink box centred on a point, so it can be filled or cut out of a disc.
        private static Geometry Letter(string text, Point centre, double size)
        {
            GlyphTypeface typeface = FontFiles.Default;
            ShapedBuffer shaped = TextShaper.Current.ShapeText(text.AsMemory(), new TextShaperOptions(typeface, size, 0, CultureInfo.InvariantCulture, 0, 0, null));
            // Built at the origin and moved by a transform: a first version gave the run its origin instead and drew no letter at all (§103.1).
            Geometry? letter = new GlyphRun(typeface, size, text.AsMemory(), shaped.ToArray(), default, 0).BuildGeometry();
            if (letter is null) return new StreamGeometry();
            Rect ink = letter.Bounds;
            letter.Transform = new TranslateTransform(centre.X - ink.Center.X, centre.Y - ink.Center.Y);
            return letter;
        }
    }
}
