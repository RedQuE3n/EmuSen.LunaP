using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // Every frame drawn from vectors for a time the host sets; the draft's positions in units of the height over 720 - see docs/LunaP.md §219.
    /// <summary>A start-up splash drawn live: a star field, a moon that draws itself, a row of planets, a sweep and a title, at the time the host sets.</summary>
    public sealed class LunaSplash : Control
    {
        private const double Unit = 720, MoonY = 0.36, MoonRadius = 92, RowY = 0.66, RowShare = 0.78, RowMost = 980, TitleY = 0.85, TitleSize = 78, CaptionY = 0.91, CaptionSize = 15;

        private static readonly IBrush HaloBrush = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.FromArgb(71, 201, 214, 255)), new ImmutableGradientStop(0.6 / 3.2, Color.FromArgb(71, 201, 214, 255)), new ImmutableGradientStop(1, Color.FromArgb(0, 201, 214, 255))],
            radiusX: new RelativeScalar(0.5, RelativeUnit.Relative), radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        private static readonly IImmutableBrush Outline = new ImmutableSolidColorBrush(Color.FromArgb(242, 243, 239, 226));
        private static readonly IBrush Lit = new ImmutableRadialGradientBrush(
            [new ImmutableGradientStop(0, Color.Parse("#faf7ec")), new ImmutableGradientStop(0.1 / 1.1, Color.Parse("#faf7ec")), new ImmutableGradientStop(1, Color.Parse("#d6d2c4"))],
            center: new RelativePoint(0.5, 0.5, RelativeUnit.Relative), gradientOrigin: new RelativePoint(0.5 - 0.175, 0.5 - 0.15, RelativeUnit.Relative),
            radiusX: new RelativeScalar(0.55, RelativeUnit.Relative), radiusY: new RelativeScalar(0.55, RelativeUnit.Relative));
        private static readonly IBrush Shadow = new ImmutableSolidColorBrush(Color.Parse("#070a1c"));
        private static readonly IBrush SweepBrush = new ImmutableLinearGradientBrush(
            [new ImmutableGradientStop(0, Color.FromArgb(0, 255, 255, 255)), new ImmutableGradientStop(0.5, Color.FromArgb(140, 214, 226, 255)), new ImmutableGradientStop(1, Color.FromArgb(0, 255, 255, 255))],
            startPoint: new RelativePoint(0, 0.5, RelativeUnit.Relative), endPoint: new RelativePoint(1, 0.5, RelativeUnit.Relative));
        private static readonly IImmutableBrush LineBrush = new ImmutableSolidColorBrush(Color.FromArgb(89, 159, 180, 255));
        private static readonly IBrush TitleBrush = new ImmutableSolidColorBrush(Color.Parse("#e7e9f4"));
        private static readonly IBrush CaptionBrush = new ImmutableSolidColorBrush(Color.Parse("#8b90ad"));

        private SplashTimeline _timeline = SplashTimeline.Moonrise;
        private IReadOnlyList<SplashBody> _bodies = [];
        private string _title = "", _caption = "";
        private double _time;
        private NightSky.Star[] _stars = [];
        private (double A0, double Rx, double Ry, IBrush Body, IBrush Glow, IBrush Flare)[] _planets = [];
        private TextLayout? _titleLayout, _captionLayout;
        private (double Size, double Spacing) _titleKey, _captionKey;

        /// <summary>A splash with the Moonrise timeline, no planets and no words, at its start.</summary>
        public LunaSplash()
        {
            IsHitTestVisible = true;
            Place();
        }

        /// <summary>The splash's timing; setting it places the stars and orbits again from its seed.</summary>
        public SplashTimeline Timeline
        {
            get => _timeline;
            set
            {
                _timeline = value ?? throw new ArgumentNullException(nameof(value));
                Place();
                Time = _time;
            }
        }

        /// <summary>The planets of the row, in order from the left.</summary>
        public IReadOnlyList<SplashBody> Bodies
        {
            get => _bodies;
            set
            {
                _bodies = value ?? throw new ArgumentNullException(nameof(value));
                Place();
            }
        }

        /// <summary>The title beneath the row.</summary>
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; _titleLayout = null; InvalidateVisual(); }
        }

        /// <summary>The caption beneath the title.</summary>
        public string Caption
        {
            get => _caption;
            set { _caption = value ?? ""; _captionLayout = null; InvalidateVisual(); }
        }

        /// <summary>Seconds since the splash began, clamped to [0, Timeline.Length]; the frame drawn is the one for this time.</summary>
        public double Time
        {
            get => _time;
            set
            {
                double t = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, _timeline.Length);
                if (t == _time) return;
                _time = t;
                InvalidateVisual();
            }
        }

        /// <summary>Whether the finished frame is drawn.</summary>
        public bool AtEnd => _time >= _timeline.Length;

        /// <summary>Jumps to the finished frame, as a skip or reduced motion shows it.</summary>
        public void Finish() => Time = _timeline.Length;

        private void Place()
        {
            var random = new SplashRandom(_timeline.Seed);
            _stars = NightSky.Place(random, _timeline.StarCount);
            _planets = new (double, double, double, IBrush, IBrush, IBrush)[_bodies.Count];
            for (int i = 0; i < _planets.Length; i++)
            {
                double hue = _bodies[i].Hue;
                IBrush body = new ImmutableRadialGradientBrush(
                    [new ImmutableGradientStop(0, Hsl(hue, 0.7, 0.78)), new ImmutableGradientStop(0.1, Hsl(hue, 0.7, 0.78)), new ImmutableGradientStop(1, Hsl(hue, 0.55, 0.38))],
                    gradientOrigin: new RelativePoint(0.3, 0.3, RelativeUnit.Relative), radiusX: new RelativeScalar(0.5, RelativeUnit.Relative), radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
                IBrush glow = new ImmutableSolidColorBrush(Hsl(hue, 0.8, 0.7), 0.18);
                IBrush flare = new ImmutableSolidColorBrush(Hsl(hue, 0.9, 0.86));
                _planets[i] = (random.Next() * 6.28, 0.32 + random.Next() * 0.16, 0.12 + random.Next() * 0.1, body, glow, flare);
            }
            InvalidateVisual();
        }

        private static Color Hsl(double hue, double s, double l) => NightSky.Hsl(hue, s, l);

        private static double Ease(double v) => 1 - Math.Pow(1 - Math.Clamp(v, 0, 1), 3);

        private static double Spring(double v)
        {
            v = Math.Clamp(v, 0, 1);
            return 1 - Math.Cos(v * Math.PI * 2.4) * Math.Exp(-5 * v);
        }

        private double PlanetRadius(int i, double u) => (5 + (i % 4) * 1.6) * u * 1.25;

        // A planet's centre at a draft time, from its orbit into its place in the row.
        private Point PlanetAt(int i, double t, double w, double h, double rowW)
        {
            double u = h / Unit, mx = w / 2, my = h * MoonY;
            double local = Math.Clamp(_timeline.Settle.At(t) * _timeline.SettleRate - i * _timeline.SettleStagger, 0, 1);
            var p = _planets[i];
            double ang = p.A0 + (1 - Ease(local)) * _timeline.OrbitSwing;
            double ox = mx + Math.Cos(ang) * w * p.Rx, oy = my + Math.Sin(ang) * h * p.Ry;
            int n = _planets.Length;
            double tx = n == 1 ? w / 2 : w / 2 - rowW / 2 + rowW * ((double)i / (n - 1)), ty = h * RowY;
            double k = Spring(local);
            return new Point(ox + (tx - ox) * k, oy + (ty - oy) * k);
        }

        // The sweep's centre at a draft time: from 60 units left of the row to 60 right of it, at a steady speed.
        private static double SweepX(double sweep, double w, double rowW, double u) => w / 2 - rowW / 2 - 60 * u + (rowW + 120 * u) * sweep;

        // Where along the sweep a planet's place in the row is, 0 to 1.
        private static double SweepShare(int i, int n, double rowW, double u) => ((n == 1 ? rowW / 2 : rowW * i / (n - 1)) + 60 * u) / (rowW + 120 * u);

        /// <summary>When the sweep reaches each planet's place in the row at the control's present size, in seconds since the splash began: the times its bells ring.</summary>
        /// <returns>One time per body, left to right.</returns>
        public IReadOnlyList<double> StrikeTimes()
        {
            double w = Bounds.Width, h = Bounds.Height, u = h / Unit, rowW = Math.Min(w * RowShare, RowMost * u);
            var times = new double[_bodies.Count];
            for (int i = 0; i < times.Length; i++)
                times[i] = h > 0 ? (_timeline.Sweep.From + SweepShare(i, times.Length, rowW, u) * (_timeline.Sweep.To - _timeline.Sweep.From)) * _timeline.Pace : 0;
            return times;
        }

        private static TextLayout Words(string text, FontFamily family, FontWeight weight, double size, double spacing, IBrush brush) =>
            new(text, new Typeface(family, FontStyle.Normal, weight), size, brush, TextAlignment.Left, TextWrapping.NoWrap, letterSpacing: spacing);

        private TextLayout TitleLayout(double size, double spacing)
        {
            if (_titleLayout is null || _titleKey != (size, spacing))
            {
                _titleLayout?.Dispose();
                _titleLayout = Words(_title, LunaFonts.Condensed, FontWeight.Light, size, spacing, TitleBrush);
                _titleKey = (size, spacing);
            }
            return _titleLayout;
        }

        private TextLayout CaptionLayout(double size, double spacing)
        {
            if (_captionLayout is null || _captionKey != (size, spacing))
            {
                _captionLayout?.Dispose();
                _captionLayout = Words(_caption, LunaFonts.Body, FontWeight.Medium, size, spacing, CaptionBrush);
                _captionKey = (size, spacing);
            }
            return _captionLayout;
        }

        private static double RoundSpacing(double v) => Math.Round(v * 4) / 4;

        /// <summary>The bounds of the moon, its halo, every planet, the title and the caption in the frame at the current time, for a fit audit.</summary>
        /// <returns>Each part drawn at the current time, by name.</returns>
        public IReadOnlyList<SplashPart> Parts()
        {
            var parts = new List<SplashPart>();
            double w = Bounds.Width, h = Bounds.Height, t = _timeline.DraftTime(_time), u = h / Unit;
            if (w <= 0 || h <= 0) return parts;
            double mx = w / 2, my = h * MoonY, mr = MoonRadius * u;
            if (_timeline.Stroke.At(t) > 0) parts.Add(new SplashPart("moon", new Rect(mx - mr, my - mr, 2 * mr, 2 * mr).Inflate(1.1 * u)));
            if (_timeline.Halo.At(t) > 0) parts.Add(new SplashPart("halo", new Rect(mx - mr * 3.2, my - mr * 3.2, mr * 6.4, mr * 6.4)));
            double rowW = Math.Min(w * RowShare, RowMost * u);
            for (int i = 0; i < _planets.Length; i++)
            {
                if (Ease(_timeline.Appear.At(t - i * _timeline.AppearStagger)) <= 0) continue;
                Point c = PlanetAt(i, t, w, h, rowW);
                double r = PlanetRadius(i, u);
                parts.Add(new SplashPart(_bodies[i].Name, new Rect(c.X - r, c.Y - r, 2 * r, 2 * r)));
            }
            double word = Ease(_timeline.Words.At(t));
            if (word > 0)
            {
                TextLayout title = TitleLayout(TitleSize * u, RoundSpacing((18 - 8 * word) * u));
                double tx = w / 2 - title.WidthIncludingTrailingWhitespace / 2, ty = h * TitleY + (1 - word) * 10 * u - title.Baseline;
                parts.Add(new SplashPart("title", new Rect(tx, ty, title.WidthIncludingTrailingWhitespace, title.Height)));
                TextLayout caption = CaptionLayout(CaptionSize * u, RoundSpacing(6 * u));
                parts.Add(new SplashPart("caption", new Rect(w / 2 - caption.WidthIncludingTrailingWhitespace / 2, h * CaptionY - caption.Baseline, caption.WidthIncludingTrailingWhitespace, caption.Height)));
            }
            return parts;
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;
            double t = _timeline.DraftTime(_time), u = h / Unit;
            context.FillRectangle(NightSky.Moonrise, new Rect(0, 0, w, h));
            NightSky.DrawStars(context, _stars, w, h, u, 1.4, Ease(_timeline.Stars.At(t)) * 0.9, t);

            double mx = w / 2, my = h * MoonY, mr = MoonRadius * u;
            double glow = Ease(_timeline.Halo.At(t));
            if (glow > 0)
                using (context.PushOpacity(glow)) context.DrawEllipse(HaloBrush, null, new Point(mx, my), mr * 3.2, mr * 3.2);

            double drawn = Ease(_timeline.Stroke.At(t));
            if (drawn > 0) context.DrawGeometry(null, new ImmutablePen(Outline, 2.2 * u, lineCap: PenLineCap.Round), Arc(mx, my, mr, drawn));

            double fill = Ease(_timeline.Fill.At(t));
            if (fill > 0)
                using (context.PushGeometryClip(new EllipseGeometry(new Rect(mx - mr, my - mr, 2 * mr, 2 * mr))))
                {
                    using (context.PushOpacity(fill)) context.FillRectangle(Lit, new Rect(mx - mr, my - mr, 2 * mr, 2 * mr));
                    context.DrawEllipse(Shadow, null, new Point(mx + mr * (0.42 + (1 - fill) * 0.7), my - mr * 0.12), mr * 0.96, mr * 0.96);
                }

            double rowY = h * RowY, rowW = Math.Min(w * RowShare, RowMost * u);
            double sweep = _timeline.Sweep.At(t), sweepX = SweepX(sweep, w, rowW, u);
            bool sweeping = sweep > 0 && sweep < 1;
            for (int i = 0; i < _planets.Length; i++)
            {
                double appear = Ease(_timeline.Appear.At(t - i * _timeline.AppearStagger));
                if (appear <= 0) continue;
                Point c = PlanetAt(i, t, w, h, rowW);
                double r = PlanetRadius(i, u);
                using (context.PushOpacity(appear))
                {
                    context.DrawEllipse(_planets[i].Glow, null, c, r * 2.6, r * 2.6);
                    context.DrawEllipse(_planets[i].Body, null, c, r, r);
                }
                double hit = sweeping ? Math.Exp(-Math.Pow((c.X - sweepX) / (_timeline.FlareWidth * u), 2)) : 0;
                if (hit > 0.01)
                    using (context.PushOpacity(0.55 * hit)) context.DrawEllipse(_planets[i].Flare, null, c, r * (2 + 2.2 * hit), r * (2 + 2.2 * hit));
            }

            if (sweeping) context.FillRectangle(SweepBrush, new Rect(sweepX - 90 * u, rowY - 26 * u, 180 * u, 52 * u));

            double line = Ease(_timeline.Line.At(t));
            if (line > 0)
                using (context.PushOpacity(line))
                    context.DrawLine(new ImmutablePen(LineBrush, u), new Point(w / 2 - rowW / 2 * line, rowY), new Point(w / 2 + rowW / 2 * line, rowY));

            double word = Ease(_timeline.Words.At(t));
            if (word <= 0) return;
            using (context.PushOpacity(word))
            {
                TextLayout title = TitleLayout(TitleSize * u, RoundSpacing((18 - 8 * word) * u));
                title.Draw(context, new Point(w / 2 - title.WidthIncludingTrailingWhitespace / 2, h * TitleY + (1 - word) * 10 * u - title.Baseline));
                TextLayout caption = CaptionLayout(CaptionSize * u, RoundSpacing(6 * u));
                caption.Draw(context, new Point(w / 2 - caption.WidthIncludingTrailingWhitespace / 2, h * CaptionY - caption.Baseline));
            }
        }

        // The outline from the top, clockwise, as far round as drawn; two halves once it passes half, since one arc cannot close a circle.
        private static StreamGeometry Arc(double cx, double cy, double r, double drawn)
        {
            var geometry = new StreamGeometry();
            using StreamGeometryContext g = geometry.Open();
            double start = -Math.PI / 2, end = start + Math.PI * 2 * Math.Min(drawn, 0.9999);
            Point At(double a) => new(cx + Math.Cos(a) * r, cy + Math.Sin(a) * r);
            g.BeginFigure(At(start), false);
            if (end - start > Math.PI)
            {
                g.ArcTo(At(start + Math.PI), new Size(r, r), 0, false, SweepDirection.Clockwise);
                g.ArcTo(At(end), new Size(r, r), 0, false, SweepDirection.Clockwise);
            }
            else g.ArcTo(At(end), new Size(r, r), 0, false, SweepDirection.Clockwise);
            g.EndFigure(drawn >= 1);
            return geometry;
        }
    }
}
