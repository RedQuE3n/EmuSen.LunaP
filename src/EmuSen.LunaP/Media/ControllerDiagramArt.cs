using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Media
{
    // A drawn piece of a controller that is not a button: the shell, a well, a printed mark - see docs/LunaP.md §198.
    internal sealed record ArtPart(Geometry Shape, Color? Fill, Color? Stroke = null, double StrokeWidth = 0);

    // Words printed on the shell, in design units.
    internal sealed record ArtText(string Text, Point Centre, double Size, Color Colour, double Angle = 0, bool Bold = true);

    // One button, direction or trigger of a drawing, in design units; where its label goes is worked out from the drawing (§198.2).
    internal sealed class RegionArt
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
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
        public string? Stick { get; init; }
        public bool IsTrigger { get; init; }
        public bool Behind { get; init; }
        public Rect Bounds => Shape.Bounds;

        // Set by DiagramArt.Route: the shown point nearest the box's middle, where the line leaves the region, where it leaves the drawing's box, and the band that is.
        public Point Centre { get; set; }
        public Point Anchor { get; set; }
        public Point Edge { get; set; }
        public DiagramSide Side { get; set; }
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
            art.Route();
            Built[layout] = art;
            return art;
        }

        // Whether a design point is on a region where it shows: inside its shape and clip, and not under the shell for one drawn behind it.
        public bool Hits(RegionArt art, Point p) =>
            art.Bounds.Contains(p) && art.Shape.FillContains(p) && (art.Clip is null || art.Clip.FillContains(p))
            && (!art.Behind || !Body.Any(b => b.Fill is not null && b.Shape.Bounds.Contains(p) && b.Shape.FillContains(p)));

        // --- Where each label goes: the way out of the drawing that crosses the least of it and no other button - see docs/LunaP.md §198.2 ---

        private const double Cell = 4;

        private void Route()
        {
            int nx = (int)Math.Ceiling(Design.Width / Cell), ny = (int)Math.Ceiling(Design.Height / Cell);
            var body = new bool[nx, ny];
            var owner = new int[nx, ny];
            List<ArtPart> solid = Body.Where(b => b.Fill is not null).ToList();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    var p = new Point((i + 0.5) * Cell, (j + 0.5) * Cell);
                    body[i, j] = solid.Any(b => b.Shape.Bounds.Contains(p) && b.Shape.FillContains(p));
                    owner[i, j] = -1;
                    for (int k = Regions.Count - 1; k >= 0 && owner[i, j] == -1; k--)
                        if (Hits(Regions[k], p)) owner[i, j] = k;
                    for (int g = 0; g < Sticks.Count && owner[i, j] == -1; g++)
                        if (Sticks[g].Gate is { } gate && gate.Bounds.Contains(p) && gate.FillContains(p)) owner[i, j] = -2 - g;
                }

            // Each region's cheapest way out through each side: the length crossed of the drawing, a little for the whole line and for leaning off an axis.
            var ways = new Dictionary<DiagramSide, (double Cost, Point Anchor, Point Edge)>[Regions.Count];
            for (int k = 0; k < Regions.Count; k++)
            {
                RegionArt region = Regions[k];
                region.Centre = Inside(region);
                var bySide = ways[k] = new Dictionary<DiagramSide, (double, Point, Point)>();
                for (int degrees = 0; degrees < 360; degrees += 3)
                {
                    double a = degrees * Math.PI / 180;
                    var way = new Vector(Math.Cos(a), Math.Sin(a));
                    Point anchor = region.Centre, p;
                    bool own = true, blocked = false;
                    double crossed = 0, t = 0;
                    DiagramSide side;
                    while (true)
                    {
                        t += 2;
                        p = region.Centre + way * t;
                        if (p.X < 0) { side = DiagramSide.Left; break; }
                        if (p.X > Design.Width) { side = DiagramSide.Right; break; }
                        if (p.Y < 0) { side = DiagramSide.Top; break; }
                        if (p.Y > Design.Height) { side = DiagramSide.Bottom; break; }
                        if (own && Hits(region, p)) { anchor = p; continue; }
                        own = false;
                        int i = Math.Min(nx - 1, (int)(p.X / Cell)), j = Math.Min(ny - 1, (int)(p.Y / Cell));
                        int o = owner[i, j];
                        if ((o <= -2 && Sticks[-2 - o].ClickRegion != region.Id) || (o >= 0 && o != k)) blocked = true;
                        if (body[i, j]) crossed += 2;
                    }
                    double cost = crossed + 0.05 * t + 60 * (1 - Math.Max(Math.Abs(way.X), Math.Abs(way.Y))) + (blocked ? 1e6 : 0);
                    var edge = new Point(Math.Clamp(p.X, 0, Design.Width), Math.Clamp(p.Y, 0, Design.Height));
                    if (!bySide.TryGetValue(side, out var had) || cost < had.Item1) bySide[side] = (cost, anchor, edge);
                }
            }

            // Sides filled in turn by the regions that would lose most by moving, so no row or column takes more labels than it has room for.
            var room = new Dictionary<DiagramSide, int>
            {
                [DiagramSide.Top] = RowRoom, [DiagramSide.Bottom] = RowRoom,
                [DiagramSide.Left] = ColumnRoom, [DiagramSide.Right] = ColumnRoom,
            };
            var left = Enumerable.Range(0, Regions.Count).ToList();
            while (left.Count > 0)
            {
                double Regret(int k)
                {
                    var costs = ways[k].Where(w => room[w.Key] > 0).Select(w => w.Value.Cost).OrderBy(c => c).ToList();
                    return costs.Count < 2 ? double.MaxValue : costs[1] - costs[0];
                }
                int next = left.OrderByDescending(Regret).First();
                var open = ways[next].Where(w => room[w.Key] > 0).ToList();
                var (side, (_, anchor, edge)) = open.Count > 0 ? open.MinBy(w => w.Value.Cost) : ways[next].MinBy(w => w.Value.Cost);
                (Regions[next].Side, Regions[next].Anchor, Regions[next].Edge) = (side, anchor, edge);
                room[side]--;
                left.Remove(next);
            }
        }

        // How many labels a row above or below, and a column either side, is given before the next best side is used.
        private const int RowRoom = 6, ColumnRoom = 8;

        // The shown point of a region nearest the middle of its box.
        private Point Inside(RegionArt art)
        {
            Rect b = art.Bounds;
            Point best = b.Center;
            double bestDistance = double.MaxValue;
            for (int i = 0; i <= 16; i++)
                for (int j = 0; j <= 16; j++)
                {
                    var p = new Point(b.X + b.Width * i / 16, b.Y + b.Height * j / 16);
                    if (!Hits(art, p)) continue;
                    double d = ((Vector)(p - b.Center)).Length;
                    if (d < bestDistance) { bestDistance = d; best = p; }
                }
            return best;
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

        internal static Geometry Rotated(Geometry g, double angle, double cx, double cy)
        {
            g.Transform = new RotateTransform(angle, cx, cy);
            return g;
        }

        // A small solid triangle pointing up, down, left or right, centred on a point.
        internal static Geometry Arrow(double cx, double cy, double size, int dx, int dy)
        {
            double h = size / 2;
            Point tip = new(cx + dx * h, cy + dy * h);
            Point a = new(cx - dx * h + dy * h, cy - dy * h - dx * h);
            Point b = new(cx - dx * h - dy * h, cy - dy * h + dx * h);
            return Path(FormattableString.Invariant($"M {tip.X},{tip.Y} L {a.X},{a.Y} L {b.X},{b.Y} Z"));
        }

        // The band of a ring between two angles (degrees clockwise from east, y down): a shoulder on a round end, or a stick's direction.
        internal static Geometry ArcBand(double cx, double cy, double inner, double outer, double from, double to)
        {
            Point P(double r, double deg) => new(cx + r * Math.Cos(deg * Math.PI / 180), cy + r * Math.Sin(deg * Math.PI / 180));
            Point a = P(outer, from), b = P(outer, to), c = P(inner, to), d = P(inner, from);
            int large = to - from > 180 ? 1 : 0;
            return Path(FormattableString.Invariant(
                $"M {a.X},{a.Y} A {outer},{outer} 0 {large} 1 {b.X},{b.Y} L {c.X},{c.Y} A {inner},{inner} 0 {large} 0 {d.X},{d.Y} Z"));
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

        // A plus-shaped direction pad of four arm regions (up, down, left, right), its hub, and an arrow on each arm.
        private static void DirectionPad(DiagramArt art, double cx, double cy, double arm, double reach, Color fill, Color arrow,
            (string Id, string Name)[] arms, double radius = 10, Color? stroke = null)
        {
            double w = arm / 2;
            Geometry cross = Union(Box(cx - reach, cy - w, 2 * reach, arm, radius), Box(cx - w, cy - reach, arm, 2 * reach, radius));
            art.Body.Add(new ArtPart(cross, fill, stroke, stroke is null ? 0 : 3));
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
                    Id = arms[i].Id, Name = arms[i].Name, Shape = Box(x0, y0, bw, bh), Clip = cross,
                    Mark = Arrow(cx + dx * mid, cy + dy * mid, arm * 0.36, dx, dy), MarkColour = arrow,
                });
            }
            art.Marks.Add(new ArtPart(Circle(cx, cy, arm * 0.2), Darken(fill, 0.25)));
        }

        private static readonly (string, string)[] Cross = { ("Up", "Up"), ("Down", "Down"), ("Left", "Left"), ("Right", "Right") };

        // A stick: its gate, and its four directions as bands of a ring around the gate, each with an arrow.
        private static void Stick(DiagramArt art, string id, double sx, double sy, double gate, double knob, double travel, bool octagonal,
            (string Id, string Name)[]? ways, Color gateFill, Color knobFill, Color edge, Color band, Color arrow, string? click = null)
        {
            art.Sticks.Add(new StickArt
            {
                Id = id, Centre = new Point(sx, sy), KnobRadius = knob, Travel = travel,
                Gate = octagonal ? Octagon(sx, sy, gate) : Circle(sx, sy, gate), GateFill = gateFill, KnobFill = knobFill, KnobStroke = edge, ClickRegion = click,
            });
            double inner = gate + 8, outer = gate + 28;
            (double From, double To)[] arcs = { (-132, -48), (48, 132), (138, 222), (-42, 42) };
            for (int i = 0; ways is not null && i < 4; i++)
            {
                double mid = (arcs[i].From + arcs[i].To) / 2 * Math.PI / 180;
                art.Regions.Add(new RegionArt
                {
                    Id = ways[i].Id, Name = ways[i].Name, Shape = ArcBand(sx, sy, inner, outer, arcs[i].From, arcs[i].To), Fill = band, Stick = id,
                    Mark = Arrow(sx + (inner + outer) / 2 * Math.Cos(mid), sy + (inner + outer) / 2 * Math.Sin(mid), 15,
                        Math.Sign(Math.Round(Math.Cos(mid), 3)), Math.Sign(Math.Round(Math.Sin(mid), 3))), MarkColour = arrow,
                });
            }
            if (click is not null) art.Regions.Add(new RegionArt { Id = click, Name = click, Shape = Circle(sx, sy, knob) });
        }

        private static void Round(DiagramArt art, string id, double x, double y, double r, string colour, string? caption = null, double size = 30, string captionColour = "#FFFFFF")
        {
            Color c = Hex(colour);
            art.Regions.Add(new RegionArt
            {
                Id = id, Name = id, Shape = Circle(x, y, r), Fill = c, Stroke = Darken(c, 0.35), StrokeWidth = 3,
                Caption = caption, CaptionSize = size, CaptionColour = Hex(captionColour),
            });
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
            art.Regions.Add(new RegionArt { Id = "L", Name = "L", Shape = ArcBand(235, 265, 205, 229, 198, 282), Fill = grey, Stroke = edge, StrokeWidth = 3 });
            art.Regions.Add(new RegionArt { Id = "R", Name = "R", Shape = ArcBand(765, 265, 205, 229, 258, 342), Fill = grey, Stroke = edge, StrokeWidth = 3 });

            Geometry body = Union(Circle(235, 265, 215), Circle(765, 265, 215), Path("M 235,52 Q 500,96 765,52 L 765,478 Q 500,446 235,478 Z"));
            art.Body.Add(new ArtPart(body, shell, edge, 5));
            art.Body.Add(new ArtPart(Circle(235, 265, 128), well));
            art.Body.Add(new ArtPart(Rotated(Box(652, 152, 226, 226, 70), 45, 765, 265), well));

            DirectionPad(art, 235, 265, 70, 106, dark, Hex("#5E5E69"), Cross);

            art.Body.Add(new ArtPart(Pill(447, 290, 86, 36, -35), well));
            art.Body.Add(new ArtPart(Pill(553, 290, 86, 36, -35), well));
            art.Regions.Add(new RegionArt { Id = "Select", Name = "Select", Shape = Pill(447, 290, 70, 22, -35), Fill = Hex("#4A4A54") });
            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Shape = Pill(553, 290, 70, 22, -35), Fill = Hex("#4A4A54") });
            art.Words.Add(new ArtText("SELECT", new Point(440, 345), 17, Hex("#5A5A66")));
            art.Words.Add(new ArtText("START", new Point(548, 345), 17, Hex("#5A5A66")));

            Round(art, "X", 765, 191, 38, "#3F5FD0", "X", 32);
            Round(art, "A", 839, 265, 38, "#D0413F", "A", 32);
            Round(art, "B", 765, 339, 38, "#E3B82E", "B", 32);
            Round(art, "Y", 691, 265, 38, "#3E9A58", "Y", 32);
            return art;
        }

        // --- Nintendo 64: a wide top on three grips, a cross on the left grip, a stick on the middle one, A, B and four C buttons on the right ---

        private static DiagramArt Nintendo64()
        {
            var art = new DiagramArt { Design = new Size(1000, 880) };
            Color shell = Hex("#9A9AA4"), edge = Hex("#45454E"), dark = Hex("#303038"), grey = Hex("#7A7A84"), well = Hex("#85858F");

            art.Body.Add(new ArtPart(Path("M 500,150 C 500,110 500,60 520,4"), null, edge, 9));
            art.Regions.Add(new RegionArt { Id = "L", Name = "L", Shape = Path("M 92,196 C 110,150 170,130 262,126 L 270,158 C 190,162 136,178 116,214 Z"), Fill = grey, Stroke = edge, StrokeWidth = 3 });
            art.Regions.Add(new RegionArt { Id = "R", Name = "R", Shape = Path("M 908,196 C 890,150 830,130 738,126 L 730,158 C 810,162 864,178 884,214 Z"), Fill = grey, Stroke = edge, StrokeWidth = 3 });

            Geometry body = Path(
                "M 120,200 C 150,160 250,150 500,150 C 750,150 850,160 880,200 " +
                "C 930,260 960,380 945,520 C 930,660 900,760 850,790 C 800,815 745,790 725,730 " +
                "C 705,660 690,600 650,575 C 630,563 615,575 610,615 C 600,710 590,820 545,850 " +
                "C 520,866 480,866 455,850 C 410,820 400,710 390,615 C 385,575 370,563 350,575 " +
                "C 310,600 295,660 275,730 C 255,790 200,815 150,790 C 100,760 70,660 55,520 " +
                "C 40,380 70,260 120,200 Z");
            art.Regions.Add(new RegionArt { Id = "Z", Name = "Z", Shape = Path("M 404,630 L 332,662 C 306,676 306,730 334,744 L 410,760 Z"), Behind = true, Fill = dark, Stroke = edge, StrokeWidth = 3, Caption = "Z", CaptionColour = Hex("#C8C8D0"), CaptionSize = 26 });
            art.Body.Add(new ArtPart(body, shell, edge, 5));
            art.Body.Add(new ArtPart(Circle(205, 275, 116), well));
            art.Body.Add(new ArtPart(Circle(800, 318, 132), well));

            DirectionPad(art, 205, 275, 62, 90, dark, Hex("#5A5A64"), Cross);

            Round(art, "Start", 500, 250, 30, "#C8363A");
            art.Words.Add(new ArtText("START", new Point(500, 300), 17, Hex("#3A3A44")));

            Stick(art, "Stick", 500, 470, 74, 32, 38, octagonal: true,
                new[] { ("StickUp", "Stick Up"), ("StickDown", "Stick Down"), ("StickLeft", "Stick Left"), ("StickRight", "Stick Right") },
                Hex("#4A4A53"), Hex("#A8A8B1"), edge, Hex("#6E6E78"), Hex("#C4C4CC"));

            Round(art, "B", 672, 336, 33, "#2E9A57", "B", 30);
            Round(art, "A", 742, 408, 39, "#3456C8", "A", 34);

            (string Id, string Name, double X, double Y, int Dx, int Dy)[] cs =
            {
                ("CUp", "C Up", 838, 244, 0, -1), ("CDown", "C Down", 838, 360, 0, 1),
                ("CLeft", "C Left", 780, 302, -1, 0), ("CRight", "C Right", 896, 302, 1, 0),
            };
            Color yellow = Hex("#E8C230");
            foreach ((string id, string name, double x, double y, int dx, int dy) in cs)
                art.Regions.Add(new RegionArt
                {
                    Id = id, Name = name, Shape = Circle(x, y, 26), Fill = yellow, Stroke = Darken(yellow, 0.4), StrokeWidth = 3,
                    Mark = Arrow(x, y, 22, dx, dy), MarkColour = Darken(yellow, 0.55),
                });
            return art;
        }

        // --- NES: a flat oblong with a dark face, a cross on the left, Select and Start on a striped panel, B and A in square wells ---

        private static DiagramArt Nes()
        {
            var art = new DiagramArt { Design = new Size(1000, 470) };
            Color shell = Hex("#C4C4CA"), edge = Hex("#55555F"), face = Hex("#232327"), panel = Hex("#A9A9B1"), red = Hex("#C8323A");

            art.Body.Add(new ArtPart(Path("M 160,82 C 160,50 150,30 130,6"), null, edge, 9));
            art.Body.Add(new ArtPart(Box(20, 80, 960, 360, 26), shell, edge, 5));
            art.Body.Add(new ArtPart(Box(52, 118, 896, 284, 14), face));
            art.Body.Add(new ArtPart(Box(96, 156, 212, 212, 18), Hex("#3B3B42")));
            DirectionPad(art, 202, 262, 64, 94, Hex("#18181B"), Hex("#6A6A74"), Cross, 8, Hex("#5A5A64"));

            art.Body.Add(new ArtPart(Box(366, 150, 268, 220, 12), panel));
            foreach (double y in new[] { 176, 206, 236 }) art.Body.Add(new ArtPart(Box(380, y, 240, 14, 4), Hex("#8B8B94")));
            art.Words.Add(new ArtText("SELECT", new Point(446, 280), 18, red));
            art.Words.Add(new ArtText("START", new Point(554, 280), 18, red));
            art.Body.Add(new ArtPart(Box(402, 300, 88, 44, 22), Hex("#6F6F78")));
            art.Body.Add(new ArtPart(Box(510, 300, 88, 44, 22), Hex("#6F6F78")));
            art.Regions.Add(new RegionArt { Id = "Select", Name = "Select", Shape = Box(410, 310, 72, 24, 12), Fill = Hex("#1E1E22") });
            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Shape = Box(518, 310, 72, 24, 12), Fill = Hex("#1E1E22") });

            art.Body.Add(new ArtPart(Box(674, 196, 124, 124, 14), panel));
            art.Body.Add(new ArtPart(Box(812, 196, 124, 124, 14), panel));
            Round(art, "B", 736, 258, 46, "#C8323A");
            Round(art, "A", 874, 258, 46, "#C8323A");
            art.Words.Add(new ArtText("B", new Point(736, 356), 30, red));
            art.Words.Add(new ArtText("A", new Point(874, 356), 30, red));
            return art;
        }

        // --- Game Boy: an upright handheld with one rounded corner, the screen in a bezel, a cross, two slanted buttons, Select, Start and a grille ---

        private static DiagramArt GameBoy()
        {
            var art = new DiagramArt { Design = new Size(640, 1000) };
            Color shell = Hex("#C9C6BE"), edge = Hex("#5A5850"), groove = Hex("#ADA9A0"), ink = Hex("#2F3A78");

            art.Body.Add(new ArtPart(Path("M 60,20 H 580 Q 610,20 610,50 V 820 Q 610,980 450,980 H 60 Q 30,980 30,950 V 50 Q 30,20 60,20 Z"), shell, edge, 5));
            art.Body.Add(new ArtPart(Box(58, 44, 524, 6, 3), groove));
            art.Body.Add(new ArtPart(Path("M 84,86 H 556 Q 576,86 576,106 V 420 Q 576,480 516,480 H 104 Q 84,480 84,460 V 106 Q 84,86 104,86 Z"), Hex("#5A5B6E")));
            art.Body.Add(new ArtPart(Box(170, 126, 310, 280, 6), Hex("#9DAE72")));
            art.Body.Add(new ArtPart(Circle(122, 236, 8), Hex("#D23A3A")));
            art.Words.Add(new ArtText("POWER", new Point(122, 264), 14, Hex("#C9C6BE")));

            DirectionPad(art, 170, 650, 60, 92, Hex("#2A2A30"), Hex("#5E5E68"), Cross, 8);

            art.Body.Add(new ArtPart(Pill(462, 640, 250, 118, -28), groove));
            Round(art, "B", 408, 670, 44, "#8E2A5C");
            Round(art, "A", 518, 612, 44, "#8E2A5C");
            art.Words.Add(new ArtText("B", new Point(404, 745), 26, ink, -28));
            art.Words.Add(new ArtText("A", new Point(514, 687), 26, ink, -28));

            art.Body.Add(new ArtPart(Pill(250, 838, 78, 30, -28), groove));
            art.Body.Add(new ArtPart(Pill(352, 838, 78, 30, -28), groove));
            art.Regions.Add(new RegionArt { Id = "Select", Name = "Select", Shape = Pill(250, 838, 62, 18, -28), Fill = Hex("#8A8C98") });
            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Shape = Pill(352, 838, 62, 18, -28), Fill = Hex("#8A8C98") });
            art.Words.Add(new ArtText("SELECT", new Point(256, 884), 15, ink, -28));
            art.Words.Add(new ArtText("START", new Point(358, 884), 15, ink, -28));

            for (int i = 0; i < 6; i++) art.Marks.Add(new ArtPart(Pill(478 + i * 22, 890 - i * 8, 86, 9, -62), Hex("#8F8B82")));
            return art;
        }

        // --- A modern pad: two grips, the left stick over the cross, four face buttons over the right stick, two bumpers and two triggers; its sticks show where they are rather than four directions each ---

        private static DiagramArt Gamepad()
        {
            var art = new DiagramArt { Design = new Size(1000, 720) };
            Color shell = Hex("#8D8F99"), edge = Hex("#3E4048"), dark = Hex("#2E2F35"), trigger = Hex("#6A6C76");

            art.Regions.Add(new RegionArt { Id = "L2", Name = "L2", Shape = Path("M 190,150 C 200,90 250,62 320,60 C 345,60 352,80 350,112 L 346,150 Z"), Fill = trigger, Stroke = edge, StrokeWidth = 3, IsTrigger = true });
            art.Regions.Add(new RegionArt { Id = "R2", Name = "R2", Shape = Path("M 810,150 C 800,90 750,62 680,60 C 655,60 648,80 650,112 L 654,150 Z"), Fill = trigger, Stroke = edge, StrokeWidth = 3, IsTrigger = true });
            art.Regions.Add(new RegionArt { Id = "L", Name = "L1", Shape = Path("M 150,196 C 175,150 240,134 300,134 L 336,138 C 348,140 350,160 336,166 C 280,170 215,180 170,212 Z"), Fill = Hex("#5E6069"), Stroke = edge, StrokeWidth = 3 });
            art.Regions.Add(new RegionArt { Id = "R", Name = "R1", Shape = Path("M 850,196 C 825,150 760,134 700,134 L 664,138 C 652,140 650,160 664,166 C 720,170 785,180 830,212 Z"), Fill = Hex("#5E6069"), Stroke = edge, StrokeWidth = 3 });

            art.Body.Add(new ArtPart(Path(
                "M 260,160 C 380,142 620,142 740,160 C 850,176 905,220 935,320 C 975,460 1000,590 950,650 " +
                "C 905,705 830,690 790,620 C 755,560 715,528 660,528 L 340,528 C 285,528 245,560 210,620 " +
                "C 170,690 95,705 50,650 C 0,590 25,460 65,320 C 95,220 150,176 260,160 Z"), shell, edge, 5));

            Stick(art, "LeftStick", 250, 320, 66, 42, 20, octagonal: false,
                null,
                dark, Hex("#4A4C55"), edge, Hex("#6E7079"), Hex("#C4C6CE"), "L3");
            DirectionPad(art, 375, 460, 46, 68, dark, Hex("#6A6C76"), Cross, 8);
            Stick(art, "RightStick", 625, 460, 66, 42, 20, octagonal: false,
                null,
                dark, Hex("#4A4C55"), edge, Hex("#6E7079"), Hex("#C4C6CE"), "R3");

            Round(art, "X", 750, 250, 32, "#2E2F35", "X", 28);
            Round(art, "A", 816, 316, 32, "#2E2F35", "A", 28);
            Round(art, "B", 750, 382, 32, "#2E2F35", "B", 28);
            Round(art, "Y", 684, 316, 32, "#2E2F35", "Y", 28);

            art.Regions.Add(new RegionArt { Id = "Select", Name = "Select", Shape = Pill(430, 300, 44, 26, 0), Fill = dark });
            art.Regions.Add(new RegionArt { Id = "Start", Name = "Start", Shape = Pill(570, 300, 44, 26, 0), Fill = dark });
            Round(art, "Guide", 500, 236, 30, "#3A3B42");
            return art;
        }
    }
}
