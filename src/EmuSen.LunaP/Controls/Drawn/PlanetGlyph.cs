using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Controls
{
    // A planet as the night screens draw one: a lit disc, a faint band and a glow in its hue - see docs/LunaP.md §221.3.
    /// <summary>A planet in a hue: a disc lit from its upper left, with a band and a glow around it drawn beyond its bounds.</summary>
    public sealed class PlanetGlyph : Control
    {
        private double _hue;
        private double _band, _bandOpacity, _glow, _glowOpacity;
        private IBrush? _body, _bandBrush, _glowBrush;

        /// <summary>A planet of hue 0 with no band and no glow.</summary>
        public PlanetGlyph() => IsHitTestVisible = false;

        /// <summary>The hue in degrees.</summary>
        public double Hue { get => _hue; set { _hue = value; Rebrush(); } }

        /// <summary>Gives the band and glow around the disc, in the control's own units.</summary>
        /// <param name="band">The band's width.</param>
        /// <param name="bandOpacity">The band's alpha.</param>
        /// <param name="glow">How far the glow reaches beyond the disc.</param>
        /// <param name="glowOpacity">The glow's alpha at the disc's edge.</param>
        public void Halo(double band, double bandOpacity, double glow, double glowOpacity)
        {
            (_band, _bandOpacity, _glow, _glowOpacity) = (Math.Max(0, band), bandOpacity, Math.Max(0, glow), glowOpacity);
            Rebrush();
        }

        /// <summary>The band's width and the glow's reach, as set.</summary>
        public (double Band, double Glow) HaloSize => (_band, _glow);

        private void Rebrush()
        {
            _body = new ImmutableRadialGradientBrush(
                [new ImmutableGradientStop(0, NightSky.Hsl(_hue, 0.85, 0.8)), new ImmutableGradientStop(0.45, NightSky.Hsl(_hue, 0.65, 0.52)), new ImmutableGradientStop(1, NightSky.Hsl(_hue, 0.6, 0.22))],
                center: new RelativePoint(0.34, 0.3, RelativeUnit.Relative), gradientOrigin: new RelativePoint(0.34, 0.3, RelativeUnit.Relative),
                radiusX: new RelativeScalar(0.962, RelativeUnit.Relative), radiusY: new RelativeScalar(0.962, RelativeUnit.Relative));
            _bandBrush = new ImmutableSolidColorBrush(NightSky.Hsl(_hue, 0.7, 0.55, _bandOpacity));
            _glowBrush = null;
            InvalidateVisual();
        }

        private IBrush GlowBrush(double r, double outer)
        {
            if (_glowBrush is ImmutableRadialGradientBrush b && b.GradientStops.Count == 3 && Math.Abs(b.GradientStops[1].Offset - r / outer) < 1e-6) return b;
            return _glowBrush = new ImmutableRadialGradientBrush(
                [new ImmutableGradientStop(0, NightSky.Hsl(_hue, 0.8, 0.6, _glowOpacity)), new ImmutableGradientStop(r / outer, NightSky.Hsl(_hue, 0.8, 0.6, _glowOpacity * 0.5)), new ImmutableGradientStop(1, NightSky.Hsl(_hue, 0.8, 0.6, 0))],
                radiusX: new RelativeScalar(0.5, RelativeUnit.Relative), radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            double d = Math.Min(Bounds.Width, Bounds.Height);
            if (d <= 0 || _body is null) return;
            double r = d / 2;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            if (_glow > 0 && _glowOpacity > 0)
            {
                double outer = r + _glow;
                context.FillRectangle(GlowBrush(r, outer), new Rect(c.X - outer, c.Y - outer, 2 * outer, 2 * outer));
            }
            if (_band > 0 && _bandOpacity > 0) context.DrawEllipse(_bandBrush, null, c, r + _band, r + _band);
            context.DrawEllipse(_body, null, c, r, r);
        }

        /// <summary>Rebuilds the glow's brush on the next draw, as after its opacity changes.</summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty) _glowBrush = null;
        }
    }
}
