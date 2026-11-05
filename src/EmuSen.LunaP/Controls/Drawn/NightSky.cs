using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Controls
{
    // The night gradient and the star field, painted for LunaSplash and LunaSky alike - see docs/LunaP.md §221.1.
    internal static class NightSky
    {
        /// <summary>A star: its place as shares of the width and height, its radius in units, and its twinkle's phase and rate.</summary>
        internal readonly record struct Star(double X, double Y, double R, double Phase, double Rate);

        /// <summary>The share of the height the stars are spread over.</summary>
        public const double HeightShare = 0.92;

        public static readonly IBrush Moonrise = Gradient((0, "#05060f"), (0.7, "#0b1029"), (1, "#141b45"));

        public static readonly IBrush Screens = Gradient((0, "#05060f"), (0.55, "#0a0d22"), (1, "#141a3c"));

        private static readonly IBrush StarBrush = new ImmutableSolidColorBrush(Color.Parse("#dfe6ff"));

        public static IBrush Gradient(params (double Offset, string Colour)[] stops)
        {
            var list = new ImmutableGradientStop[stops.Length];
            for (int i = 0; i < stops.Length; i++) list[i] = new ImmutableGradientStop(stops[i].Offset, Color.Parse(stops[i].Colour));
            return new ImmutableLinearGradientBrush(list, startPoint: new RelativePoint(0, 0, RelativeUnit.Relative), endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
        }

        // Five draws a star, in Moonrise's order: x, y, radius, phase, twinkle rate.
        public static Star[] Place(SplashRandom random, int count)
        {
            var stars = new Star[Math.Max(0, count)];
            for (int i = 0; i < stars.Length; i++)
                stars[i] = new Star(random.Next(), random.Next() * HeightShare, random.Next() * 1.2 + 0.2, random.Next() * 6.28, 0.6 + random.Next() * 1.6);
            return stars;
        }

        public static void DrawStars(DrawingContext context, Star[] stars, double w, double h, double unit, double size, double opacity, double time)
        {
            if (opacity <= 0) return;
            foreach (Star s in stars)
            {
                double twinkle = 0.55 + 0.45 * Math.Sin(s.Phase + time * s.Rate), r = s.R * unit * size;
                using (context.PushOpacity(opacity * twinkle)) context.DrawEllipse(StarBrush, null, new Point(s.X * w, s.Y * h), r, r);
            }
        }

        public static Color Hsl(double hue, double s, double l, double a = 1) => HslColor.ToRgb(((hue % 360) + 360) % 360, s, l, a);
    }
}
