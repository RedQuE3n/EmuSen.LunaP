using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Media
{
    // A drawn piece of a controller that is not a button: the shell, a well, a printed mark - see docs/LunaP.md §198.
    internal sealed record ArtPart(Geometry Shape, Color? Fill, Color? Stroke = null, double StrokeWidth = 0);

    // Words printed on the shell, in design units.
    internal sealed record ArtText(string Text, Point Centre, double Size, Color Colour, double Angle = 0, bool Bold = true);

    // One button, direction or trigger of a drawing, in design units.
    internal sealed class RegionArt
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required DiagramSide Side { get; init; }
        public required Geometry Shape { get; init; }
        public Geometry? Clip { get; init; }
        public Color? Fill { get; init; }
        public Color? Stroke { get; init; }
        public double StrokeWidth { get; init; }
        public string? Caption { get; init; }
        public Color CaptionColour { get; init; } = Colors.White;
        public double CaptionSize { get; init; } = 30;
        public double CaptionAngle { get; init; }
        public Geometry? Mark { get; init; }
        public Color MarkColour { get; init; } = Colors.White;
        public Point? AnchorAt { get; init; }
        public string? Stick { get; init; }
        public bool IsTrigger { get; init; }
        public bool Behind { get; init; }
        public Point Anchor => AnchorAt ?? Toward(Shape.Bounds, Side);

        // A point just inside the edge that faces the label, so the leader does not cross the button's own caption.
        private static Point Toward(Rect b, DiagramSide side) => side switch
        {
            DiagramSide.Left => new Point(b.Left + b.Width * 0.12, b.Center.Y),
            DiagramSide.Right => new Point(b.Right - b.Width * 0.12, b.Center.Y),
            DiagramSide.Top => new Point(b.Center.X, b.Top + b.Height * 0.12),
            _ => new Point(b.Center.X, b.Bottom - b.Height * 0.12),
        };
        public Rect Bounds => Shape.Bounds;
    }

    // A stick: its gate, its knob and how far the knob travels, in design units.
    internal sealed class StickArt
    {
        public required string Id { get; init; }
        public required Point Centre { get; init; }
        public required double KnobRadius { get; init; }
        public required double Travel { get; init; }
        public Geometry? Gate { get; init; }
        public Color GateFill { get; init; }
        public Color KnobFill { get; init; }
        public Color KnobStroke { get; init; }
        public string? ClickRegion { get; init; }
    }

    // One controller's drawing: shell parts under the regions, marks and words over them - see docs/LunaP.md §198.
    internal sealed class DiagramArt
    {
        public required Size Design { get; init; }
        public List<ArtPart> Body { get; } = new();
        public List<RegionArt> Regions { get; } = new();
        public List<ArtPart> Marks { get; } = new();
        public List<ArtText> Words { get; } = new();
        public List<StickArt> Sticks { get; } = new();

        private static readonly Dictionary<ControllerLayout, DiagramArt> Built = new();

        public static DiagramArt For(ControllerLayout layout)
        {
            if (Built.TryGetValue(layout, out DiagramArt? art)) return art;
            art = layout switch
            {
                ControllerLayout.Snes => Snes(),
                ControllerLayout.Nintendo64 => Nintendo64(),
                ControllerLayout.Nes => Nes(),
                ControllerLayout.GameBoy => GameBoy(),
                _ => Gamepad(),
            };
            Built[layout] = art;
            return art;
        }

        // --- Geometry helpers, all in design units ---

        internal static Geometry Circle(double cx, double cy, double r) => new EllipseGeometry(new Rect(cx - r, cy - r, 2 * r, 2 * r));

        internal static Geometry Box(double x, double y, double w, double h, double r = 0) => new RectangleGeometry(new Rect(x, y, w, h), r, r);

        internal static Geometry Pill(double cx, double cy, double length, double thickness, double angle)
        {
            Geometry g = Box(cx - length / 2, cy - thickness / 2, length, thickness, thickness / 2);
            if (angle != 0) g.Transform = new RotateTransform(angle, cx, cy);
            return g;
        }

        internal static Geometry Path(string data) => StreamGeometry.Parse(data);

        internal static Geometry Union(params Geometry[] parts)
        {
            Geometry g = parts[0];
            for (int i = 1; i < parts.Length; i++) g = new CombinedGeometry(GeometryCombineMode.Union, g, parts[i]);
            return g;
        }

        internal static Geometry Minus(Geometry a, Geometry b) => new CombinedGeometry(GeometryCombineMode.Exclude, a, b);

        // A small solid triangle pointing up, down, left or right, centred on a point.
        internal static Geometry Arrow(double cx, double cy, double size, int dx, int dy)
        {
            double h = size / 2;
            Point tip = new(cx + dx * h, cy + dy * h);
            Point a = new(cx - dx * h + dy * h, cy - dy * h - dx * h);
            Point b = new(cx - dx * h - dy * h, cy - dy * h + dx * h);
            return Path(FormattableString.Invariant($"M {tip.X},{tip.Y} L {a.X},{a.Y} L {b.X},{b.Y} Z"));
        }

        // The band just outside a circle between two angles (degrees clockwise from east, y down), for a shoulder button on a round end.
        internal static Geometry ArcBand(double cx, double cy, double inner, double outer, double from, double to)
        {
            Point P(double r, double deg) => new(cx + r * Math.Cos(deg * Math.PI / 180), cy + r * Math.Sin(deg * Math.PI / 180));
            Point a = P(outer, from), b = P(outer, to), c = P(inner, to), d = P(inner, from);
            int large = to - from > 180 ? 1 : 0;
            return Path(FormattableString.Invariant(
                $"M {a.X},{a.Y} A {outer},{outer} 0 {large} 1 {b.X},{b.Y} L {c.X},{c.Y} A {inner},{inner} 0 {large} 0 {d.X},{d.Y} Z"));
        }

        // A plus-shaped direction pad of four arm regions, its hub, and an arrow on each arm; ids are the host's.
        private static void DirectionPad(DiagramArt art, double cx, double cy, double arm, double reach, Color fill, Color arrow,
            (string Id, string Name, DiagramSide Side)[] arms, double radius = 10)
        {
            double w = arm / 2;
            Geometry cross = Union(Box(cx - reach, cy - w, 2 * reach, arm, radius), Box(cx - w, cy - reach, arm, 2 * reach, radius));
            art.Body.Add(new ArtPart(cross, fill));
            (int Dx, int Dy)[] ways = { (0, -1), (0, 1), (-1, 0), (1, 0) };
            for (int i = 0; i < 4; i++)
            {
                (int dx, int dy) = ways[i];
                double x0 = dx == 0 ? cx - w : dx < 0 ? cx - reach : cx + w;
                double y0 = dy == 0 ? cy - w : dy < 0 ? cy - reach : cy + w;
                double bw = dx == 0 ? arm : reach - w, bh = dy == 0 ? arm : reach - w;
                double mid = (reach + w) / 2;
                art.Regions.Add(new RegionArt
                {
                    Id = arms[i].Id, Name = arms[i].Name, Side = arms[i].Side,
                    Shape = Box(x0, y0, bw, bh), Clip = cross,
                    Mark = Arrow(cx + dx * mid, cy + dy * mid, arm * 0.36, dx, dy), MarkColour = arrow,
                    AnchorAt = new Point(cx + dx * (reach - arm * 0.3), cy + dy * (reach - arm * 0.3)),
                });
            }
            art.Marks.Add(new ArtPart(Circle(cx, cy, arm * 0.2), Darken(fill, 0.25)));
        }

        internal static Color Darken(Color c, double by) =>
            Color.FromArgb(c.A, (byte)(c.R * (1 - by)), (byte)(c.G * (1 - by)), (byte)(c.B * (1 - by)));

        internal static Color Hex(string hex) => Color.Parse(hex);

        // --- Super NES: two round grips joined by a bar, a cross on the left and four coloured buttons on the right ---

        private static DiagramArt Snes()
        {
            var art = new DiagramArt { Design = new Size(1000, 500) };
            Color shell = Hex("#C9C9D1"), edge = Hex("#55555F"), well = Hex("#B4B4BE"), dark = Hex("#2C2C33"), grey = Hex("#8E8E99");

            art.Body.Add(new ArtPart(Path("M 488,58 C 488,30 500,18 512,4"), null, edge, 9));
            art.Regions.Add(new RegionArt { Id = "L", Name = "L", Side = DiagramSide.Left, Shape = ArcBand(235, 265, 205, 229, 198, 282), Fill = grey, Stroke = edge, StrokeWidth = 3, AnchorAt = new Point(120, 88) });
            art.Regions.Add(new RegionArt { Id = "R", Name = "R", Side = DiagramSide.Right, Shape = ArcBand(765, 265, 205, 229, 258, 342), Fill = grey, Stroke = edge, StrokeWidth = 3, AnchorAt = new Point(880, 88) });

            Geometry body = Union(Circle(235, 265, 215), Circle(765, 265, 215), Path("M 235,52 Q 500,96 765,52 L 765,478 Q 500,446 235,478 Z"));
            art.Body.Add(new ArtPart(body, shell, edge, 5));
            art.Body.Add(new ArtPart(Circle(235, 265, 128), well));
            art.Body.Add(new ArtPart(Rotated(Box(652, 152, 226, 226, 70), 45, 765, 265), well));

            DirectionPad(art, 235, 265, 70, 106, dark, Hex("#5E5E69"), new[]
            {
                ("Up", "Up", DiagramSide.Left), ("Down", "Down", DiagramSide.Left),
                ("Left", "Left", DiagramSide.Left), ("Right", "Right", DiagramSide.Bottom),
            });

            art.Body.Add(new ArtPart(Pill(447, 290, 86, 36, -35), well));
            art.Body.Add(new ArtPart(Pill(553, 290, 86, 36, -35), well));
            art.Regions.Add(new RegionArt { Id = "Select", Name = "Select", Side = DiagramSide.Bottom, Shape = Pill(447, 290, 70, 22, -35), Fill = Hex("#4A4A54") });
            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Side = DiagramSide.Bottom, Shape = Pill(553, 290, 70, 22, -35), Fill = Hex("#4A4A54") });
            art.Words.Add(new ArtText("SELECT", new Point(440, 345), 17, Hex("#5A5A66")));
            art.Words.Add(new ArtText("START", new Point(548, 345), 17, Hex("#5A5A66")));

            (string Id, double X, double Y, string Colour, DiagramSide Side)[] face =
            {
                ("X", 765, 191, "#3F5FD0", DiagramSide.Right), ("A", 839, 265, "#D0413F", DiagramSide.Right),
                ("B", 765, 339, "#E3B82E", DiagramSide.Right), ("Y", 691, 265, "#3E9A58", DiagramSide.Top),
            };
            foreach ((string id, double x, double y, string colour, DiagramSide side) in face)
            {
                art.Regions.Add(new RegionArt
                {
                    Id = id, Name = id, Side = side, Shape = Circle(x, y, 38), Fill = Hex(colour), Stroke = Darken(Hex(colour), 0.35), StrokeWidth = 3,
                    Caption = id, CaptionColour = Colors.White, CaptionSize = 32,
                });
            }
            return art;
        }

        internal static Geometry Rotated(Geometry g, double angle, double cx, double cy)
        {
            g.Transform = new RotateTransform(angle, cx, cy);
            return g;
        }

        // --- Nintendo 64: a wide top on three grips, a cross on the left grip, a stick on the middle one, A, B and four C buttons on the right ---

        private static DiagramArt Nintendo64()
        {
            var art = new DiagramArt { Design = new Size(1000, 880) };
            Color shell = Hex("#9A9AA4"), edge = Hex("#45454E"), dark = Hex("#303038"), grey = Hex("#7A7A84"), well = Hex("#85858F");

            art.Body.Add(new ArtPart(Path("M 500,150 C 500,110 500,60 520,4"), null, edge, 9));
            art.Regions.Add(new RegionArt { Id = "L", Name = "L", Side = DiagramSide.Left, Shape = Path("M 92,196 C 110,150 170,130 262,126 L 270,158 C 190,162 136,178 116,214 Z"), Fill = grey, Stroke = edge, StrokeWidth = 3, AnchorAt = new Point(160, 150) });
            art.Regions.Add(new RegionArt { Id = "R", Name = "R", Side = DiagramSide.Right, Shape = Path("M 908,196 C 890,150 830,130 738,126 L 730,158 C 810,162 864,178 884,214 Z"), Fill = grey, Stroke = edge, StrokeWidth = 3, AnchorAt = new Point(860, 156) });

            Geometry body = Path(
                "M 120,200 C 150,160 250,150 500,150 C 750,150 850,160 880,200 " +
                "C 930,260 960,380 945,520 C 930,660 900,760 850,790 C 800,815 745,790 725,730 " +
                "C 705,660 690,600 650,575 C 630,563 615,575 610,615 C 600,710 590,820 545,850 " +
                "C 520,866 480,866 455,850 C 410,820 400,710 390,615 C 385,575 370,563 350,575 " +
                "C 310,600 295,660 275,730 C 255,790 200,815 150,790 C 100,760 70,660 55,520 " +
                "C 40,380 70,260 120,200 Z");
            art.Regions.Add(new RegionArt { Id = "Z", Name = "Z", Side = DiagramSide.Bottom, Shape = Path("M 404,630 L 332,662 C 306,676 306,730 334,744 L 410,760 Z"), Behind = true, Fill = dark, Stroke = edge, StrokeWidth = 3, Caption = "Z", CaptionColour = Hex("#C8C8D0"), CaptionSize = 26, AnchorAt = new Point(338, 726) });
            art.Body.Add(new ArtPart(body, shell, edge, 5));
            art.Body.Add(new ArtPart(Circle(205, 275, 116), well));
            art.Body.Add(new ArtPart(Circle(800, 318, 132), well));

            DirectionPad(art, 205, 275, 62, 90, dark, Hex("#5A5A64"), new[]
            {
                ("Up", "Up", DiagramSide.Left), ("Down", "Down", DiagramSide.Left),
                ("Left", "Left", DiagramSide.Left), ("Right", "Right", DiagramSide.Bottom),
            });

            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Side = DiagramSide.Top, Shape = Circle(500, 250, 30), Fill = Hex("#C8363A"), Stroke = Darken(Hex("#C8363A"), 0.35), StrokeWidth = 3 });
            art.Words.Add(new ArtText("START", new Point(500, 300), 17, Hex("#3A3A44")));

            const double sx = 500, sy = 470;
            art.Sticks.Add(new StickArt
            {
                Id = "Stick", Centre = new Point(sx, sy), KnobRadius = 32, Travel = 38,
                Gate = Octagon(sx, sy, 74), GateFill = Hex("#4A4A53"), KnobFill = Hex("#A8A8B1"), KnobStroke = edge,
            });
            (string Id, string Name, double From, double To, double Anchor, DiagramSide Side)[] ways =
            {
                ("StickUp", "Stick Up", -132, -48, -122, DiagramSide.Left), ("StickRight", "Stick Right", -42, 42, 0, DiagramSide.Right),
                ("StickDown", "Stick Down", 48, 132, 90, DiagramSide.Bottom), ("StickLeft", "Stick Left", 138, 222, 180, DiagramSide.Left),
            };
            foreach ((string id, string name, double from, double to, double anchor, DiagramSide side) in ways)
            {
                double a = anchor * Math.PI / 180, mid = (from + to) / 2 * Math.PI / 180;
                art.Regions.Add(new RegionArt
                {
                    Id = id, Name = name, Side = side, Shape = ArcBand(sx, sy, 84, 104, from, to), Fill = Hex("#6E6E78"), Stick = "Stick",
                    Mark = Arrow(sx + 94 * Math.Cos(mid), sy + 94 * Math.Sin(mid), 15, Math.Sign(Math.Round(Math.Cos(mid), 3)), Math.Sign(Math.Round(Math.Sin(mid), 3))), MarkColour = Hex("#C4C4CC"),
                    AnchorAt = new Point(sx + 96 * Math.Cos(a), sy + 96 * Math.Sin(a)),
                });
            }

            art.Regions.Add(new RegionArt { Id = "B", Name = "B", Side = DiagramSide.Top, Shape = Circle(672, 336, 33), Fill = Hex("#2E9A57"), Stroke = Darken(Hex("#2E9A57"), 0.35), StrokeWidth = 3, Caption = "B", CaptionSize = 30 });
            art.Regions.Add(new RegionArt { Id = "A", Name = "A", Side = DiagramSide.Right, Shape = Circle(742, 408, 39), Fill = Hex("#3456C8"), Stroke = Darken(Hex("#3456C8"), 0.35), StrokeWidth = 3, Caption = "A", CaptionSize = 34 });

            (string Id, string Name, double X, double Y, int Dx, int Dy, DiagramSide Side)[] cs =
            {
                ("CUp", "C Up", 838, 244, 0, -1, DiagramSide.Right), ("CDown", "C Down", 838, 360, 0, 1, DiagramSide.Right),
                ("CLeft", "C Left", 780, 302, -1, 0, DiagramSide.Top), ("CRight", "C Right", 896, 302, 1, 0, DiagramSide.Right),
            };
            Color yellow = Hex("#E8C230");
            foreach ((string id, string name, double x, double y, int dx, int dy, DiagramSide side) in cs)
                art.Regions.Add(new RegionArt
                {
                    Id = id, Name = name, Side = side, Shape = Circle(x, y, 26), Fill = yellow, Stroke = Darken(yellow, 0.4), StrokeWidth = 3,
                    Mark = Arrow(x, y, 22, dx, dy), MarkColour = Darken(yellow, 0.55),
                    AnchorAt = id == "CLeft" ? new Point(x - 6, y - 20) : null,
                });
            return art;
        }

        internal static Geometry Octagon(double cx, double cy, double r)
        {
            var points = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                double a = (22.5 + 45 * i) * Math.PI / 180;
                points.Add(FormattableString.Invariant($"{cx + r * Math.Cos(a)},{cy + r * Math.Sin(a)}"));
            }
            return Path("M " + string.Join(" L ", points) + " Z");
        }

        // --- Stand-ins until their drawings are done ---

        private static DiagramArt Nes() => Snes();

        private static DiagramArt GameBoy() => Snes();

        private static DiagramArt Gamepad() => Snes();
    }
}
