using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The kind of body a world is drawn as.</summary>
    public enum WorldShape
    {
        /// <summary>A lit disc with optional craters, bands, a polar cap, a ring and a moonlet.</summary>
        Planet,

        /// <summary>A cluster of three tall shards.</summary>
        Shards,

        /// <summary>A cut gem: a crown and a pavilion of facets.</summary>
        Gem,
    }

    // A world's body as plain data: its kind, a planet's three colours and its features as opacities - see docs/LunaP.md §222.1.
    /// <summary>What a world is drawn as: a shape, a planet's palette (null derives it from the hue) and five features, each an opacity from 0 (absent) to 1.</summary>
    /// <param name="Shape">The kind of body.</param>
    public sealed record WorldBody(WorldShape Shape)
    {
        /// <summary>A planet with no palette and no feature, drawn in its hue.</summary>
        public static WorldBody Plain { get; } = new(WorldShape.Planet);

        /// <summary>A planet's lit disc: its light, middle and rim colours; null derives the three from the hue.</summary>
        public IReadOnlyList<Color>? Palette { get; init; }

        /// <summary>Three craters in the rim colour, at this opacity.</summary>
        public double Craters { get; init; }

        /// <summary>Two bands across the disc in the light colour, at this opacity.</summary>
        public double Bands { get; init; }

        /// <summary>A pale polar cap, at this opacity.</summary>
        public double Cap { get; init; }

        /// <summary>A tilted ring round the disc: none, a subtle one or a big one.</summary>
        public WorldRing Ring { get; init; }

        /// <summary>How many moonlets, 0 to 2: the first at the upper right, the second, smaller, at the lower left.</summary>
        public int Moons { get; init; }
    }

    /// <summary>The size of a planet's ring.</summary>
    public enum WorldRing
    {
        /// <summary>No ring at all.</summary>
        None,

        /// <summary>A thin, faint ring close to the disc.</summary>
        Subtle,

        /// <summary>The mockup's ring: wide, bold and bright.</summary>
        Big,
    }

    // One world's body as a control, sized by its shape and focus, with a glow of its shape - see docs/LunaP.md §222.2.
    /// <summary>Draws a WorldBody in a hue, at rest or focused, at the size its shape and focus give; drawing reaches past its bounds and is not clipped.</summary>
    public sealed class WorldGlyph : Control
    {
        private WorldBody _body = WorldBody.Plain;
        private double _hue;
        private bool _focused;
        private WorldPaint _paint = new(WorldBody.Plain, 0, false);

        /// <summary>A plain planet of hue 0 at rest.</summary>
        public WorldGlyph()
        {
            IsHitTestVisible = false;
            ClipToBounds = false;
            Restyle();
        }

        /// <summary>The body drawn; null draws WorldBody.Plain.</summary>
        public WorldBody Body { get => _body; set { _body = value ?? WorldBody.Plain; Restyle(); } }

        /// <summary>The hue in degrees, for a plain planet's colours, the facets and the glow.</summary>
        public double Hue { get => _hue; set { _hue = value; Restyle(); } }

        /// <summary>Whether the world is focused: larger, brighter and with a wider glow.</summary>
        public bool Focused { get => _focused; set { _focused = value; Restyle(); } }

        /// <summary>The size a shape is drawn at, at rest or focused, in units.</summary>
        /// <param name="shape">The kind of body.</param>
        /// <param name="focused">Whether it is focused.</param>
        /// <returns>The width and height.</returns>
        public static Size SizeOf(WorldShape shape, bool focused) => shape == WorldShape.Planet ? (focused ? new Size(84, 84) : new Size(58, 58)) : focused ? new Size(60, 96) : new Size(42, 68);

        /// <summary>A planet's three colours, the palette's or, without one, its hue's.</summary>
        /// <param name="body">The body whose planet is drawn.</param>
        /// <param name="hue">The hue in degrees.</param>
        /// <returns>The light, middle and rim colours.</returns>
        public static (Color Light, Color Middle, Color Rim) Colours(WorldBody body, double hue) =>
            body.Palette is { Count: >= 3 } p ? (p[0], p[1], p[2]) : (NightSky.Hsl(hue, 0.85, 0.8), NightSky.Hsl(hue, 0.65, 0.52), NightSky.Hsl(hue, 0.6, 0.22));

        /// <summary>The four facet inks of shards and a gem, at rest or focused.</summary>
        /// <param name="hue">The hue in degrees.</param>
        /// <param name="focused">Whether the world is focused.</param>
        /// <returns>The inks, the first to the fourth.</returns>
        public static Color[] FacetInks(double hue, bool focused) => focused
            ? [NightSky.Hsl(hue, 0.8, 0.84), NightSky.Hsl(hue, 0.7, 0.64), NightSky.Hsl(hue, 0.95, 0.9), NightSky.Hsl(hue, 0.6, 0.46)]
            : [NightSky.Hsl(hue, 0.5, 0.74, 0.62), NightSky.Hsl(hue, 0.45, 0.58, 0.55), NightSky.Hsl(hue, 0.6, 0.82, 0.68), NightSky.Hsl(hue, 0.38, 0.42, 0.55)];

        private void Restyle()
        {
            Size size = SizeOf(_body.Shape, _focused);
            (Width, Height) = (size.Width, size.Height);
            Effect = new ImmutableDropShadowEffect(0, 0, _focused ? 24 : 9, NightSky.Hsl(_hue, 0.9, 0.7), _focused ? 0.75 : 0.3);
            _paint = new WorldPaint(_body, _hue, _focused);
            InvalidateVisual();
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            if (Bounds.Width > 0 && Bounds.Height > 0) _paint.Draw(context, new Rect(Bounds.Size));
        }
    }

    // The one drawing of a body, shared by WorldGlyph and LunaSplash: its brushes made once, drawn into a box of the shape's proportions - see docs/LunaP.md §222.2.
    internal sealed class WorldPaint
    {
        private static readonly IBrush CapInk = new ImmutableSolidColorBrush(Color.Parse("#FFF4F6"));
        private static readonly IBrush Highlight = new ImmutableSolidColorBrush(Color.FromArgb(128, 255, 255, 255));

        private static readonly (string Points, int Ink)[] ShardFacets =
        [
            ("14,74 26,62 30,156 20,156", 1), ("26,62 37,82 38,156 30,156", 2), ("64,84 76,52 74,156 63,156", 1),
            ("76,52 88,70 82,156 74,156", 4), ("50,4 37,44 40,156 50,156", 3), ("50,4 50,156 60,156 63,40", 4),
        ];

        private static readonly (string Points, int Ink)[] GemFacets =
        [
            ("6,72 30,48 40,72", 1), ("30,48 70,48 60,72 40,72", 3), ("70,48 94,72 60,72", 2),
            ("6,72 40,72 50,156", 2), ("40,72 60,72 50,156", 3), ("60,72 94,72 50,156", 4),
        ];

        // Parsed on first use, so reading a type's surface needs no renderer.
        private static class Shapes
        {
            public static readonly Geometry BigRing = Geometry.Parse("M-14,52 A64,13 0 0 0 114,52"), SubtleRing = Geometry.Parse("M-4,52 A54,10 0 0 0 104,52");
            public static readonly Geometry BandHigh = Geometry.Parse("M9,44 Q50,33 91,44"), BandLow = Geometry.Parse("M12,62 Q50,53 88,62");
            public static readonly Geometry Cap = Geometry.Parse("M33,13 Q50,4 67,13 Q50,20 33,13Z");
            public static readonly Geometry[] Shards = Polygons(ShardFacets), Gems = Polygons(GemFacets);
        }

        private static readonly ((Point From, Point To, double Width) A, (Point From, Point To, double Width) B) ShardSparks = ((new(44, 34), new(43, 104), 3), (new(22, 84), new(23, 108), 2));
        private static readonly ((Point From, Point To, double Width) A, (Point From, Point To, double Width) B) GemSparks = ((new(36, 55), new(54, 55), 3), (new(34, 82), new(42, 108), 2));

        private static Matrix About(double degrees, double x, double y) =>
            Matrix.CreateTranslation(-x, -y) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(x, y);

        private static readonly Matrix RingTurn = About(-16, 50, 52), HighlightTurn = About(-28, 35, 29);

        private static Geometry[] Polygons((string Points, int Ink)[] facets) =>
            Array.ConvertAll(facets, f => Geometry.Parse("M" + f.Points.Replace(" ", " L") + "Z"));

        private readonly WorldBody _body;
        private readonly IBrush _disc, _craterInk, _moonFill;
        private readonly IPen _ringPen, _subtlePen, _bandHigh, _bandLow, _moonEdge, _edge, _sparkA, _sparkB;
        private readonly IBrush[] _facets;

        /// <summary>Every brush and pen the body needs, made once.</summary>
        public WorldPaint(WorldBody body, double hue, bool focused)
        {
            _body = body;
            (Color light, Color middle, Color rim) = WorldGlyph.Colours(body, hue);
            _disc = new ImmutableRadialGradientBrush([new ImmutableGradientStop(0, light), new ImmutableGradientStop(0.48, middle), new ImmutableGradientStop(1, rim)],
                center: new RelativePoint(0.36, 0.32, RelativeUnit.Relative), gradientOrigin: new RelativePoint(0.36, 0.32, RelativeUnit.Relative),
                radiusX: new RelativeScalar(0.74, RelativeUnit.Relative), radiusY: new RelativeScalar(0.74, RelativeUnit.Relative));
            var ringInk = new ImmutableSolidColorBrush(focused ? Color.FromArgb(242, 255, 255, 255) : Color.FromArgb(179, 255, 245, 252));
            (_ringPen, _subtlePen) = (new ImmutablePen(ringInk, 2.4), new ImmutablePen(ringInk, 1.4));
            _craterInk = new ImmutableSolidColorBrush(rim);
            var lightInk = new ImmutableSolidColorBrush(light);
            (_bandHigh, _bandLow) = (new ImmutablePen(lightInk, 4, lineCap: PenLineCap.Round), new ImmutablePen(lightInk, 3, lineCap: PenLineCap.Round));
            (_moonFill, _moonEdge) = (new ImmutableSolidColorBrush(middle), new ImmutablePen(lightInk, 1));
            _facets = Array.ConvertAll(WorldGlyph.FacetInks(hue, focused), c => (IBrush)new ImmutableSolidColorBrush(c));
            _edge = new ImmutablePen(new ImmutableSolidColorBrush(focused ? Color.FromArgb(242, 255, 255, 255) : Color.FromArgb(140, 255, 245, 252)), 1.2, lineJoin: PenLineJoin.Round);
            var spark = new ImmutableSolidColorBrush(focused ? Color.FromArgb(242, 255, 255, 255) : Color.FromArgb(115, 255, 255, 255));
            (_sparkA, _sparkB) = (new ImmutablePen(spark, 3, lineCap: PenLineCap.Round), new ImmutablePen(spark, 2, lineCap: PenLineCap.Round));
        }

        /// <summary>The box's height over its width: 1 for a planet, 1.6 for shards and a gem.</summary>
        public static double Tallness(WorldShape shape) => shape == WorldShape.Planet ? 1 : 1.6;

        /// <summary>Draws the body into a box of its shape's proportions.</summary>
        public void Draw(DrawingContext context, Rect box)
        {
            double tall = 100 * Tallness(_body.Shape);
            using (context.PushTransform(Matrix.CreateScale(box.Width / 100, box.Height / tall) * Matrix.CreateTranslation(box.X, box.Y)))
            {
                if (_body.Shape == WorldShape.Planet) DrawPlanet(context);
                else if (_body.Shape == WorldShape.Gem) DrawFacets(context, Shapes.Gems, GemFacets, GemSparks);
                else DrawFacets(context, Shapes.Shards, ShardFacets, ShardSparks);
            }
        }

        private void DrawPlanet(DrawingContext context)
        {
            (double ringX, double ringY, double ringOpacity, Geometry? front) = _body.Ring switch
            {
                WorldRing.Big => (64.0, 13.0, 0.75, Shapes.BigRing),
                WorldRing.Subtle => (54.0, 10.0, 0.45, Shapes.SubtleRing),
                _ => (0.0, 0.0, 0.0, null),
            };
            IPen ringPen = _body.Ring == WorldRing.Subtle ? _subtlePen : _ringPen;
            if (front is not null)
                using (context.PushOpacity(ringOpacity))
                using (context.PushTransform(RingTurn))
                    context.DrawEllipse(null, ringPen, new Point(50, 52), ringX, ringY);
            context.DrawEllipse(_disc, null, new Point(50, 50), 44, 44);
            if (_body.Craters > 0)
                using (context.PushOpacity(_body.Craters))
                {
                    context.DrawEllipse(_craterInk, null, new Point(63, 40), 7, 7);
                    context.DrawEllipse(_craterInk, null, new Point(40, 64), 5, 5);
                    context.DrawEllipse(_craterInk, null, new Point(67, 66), 3.5, 3.5);
                }
            if (_body.Bands > 0)
                using (context.PushOpacity(_body.Bands))
                {
                    context.DrawGeometry(null, _bandHigh, Shapes.BandHigh);
                    context.DrawGeometry(null, _bandLow, Shapes.BandLow);
                }
            if (_body.Cap > 0)
                using (context.PushOpacity(_body.Cap))
                    context.DrawGeometry(CapInk, null, Shapes.Cap);
            if (front is not null)
                using (context.PushOpacity(ringOpacity))
                using (context.PushTransform(RingTurn))
                    context.DrawGeometry(null, ringPen, front);
            using (context.PushTransform(HighlightTurn))
                context.DrawEllipse(Highlight, null, new Point(35, 29), 10, 6);
            if (_body.Moons >= 1) context.DrawEllipse(_moonFill, _moonEdge, new Point(93, 14), 5.5, 5.5);
            if (_body.Moons >= 2) context.DrawEllipse(_moonFill, _moonEdge, new Point(5, 84), 3.8, 3.8);
        }

        private void DrawFacets(DrawingContext context, Geometry[] shapes, (string Points, int Ink)[] facets, ((Point From, Point To, double Width) A, (Point From, Point To, double Width) B) sparks)
        {
            for (int i = 0; i < shapes.Length; i++) context.DrawGeometry(_facets[facets[i].Ink - 1], _edge, shapes[i]);
            context.DrawLine(_sparkA, sparks.A.From, sparks.A.To);
            context.DrawLine(_sparkB, sparks.B.From, sparks.B.To);
        }
    }
}
