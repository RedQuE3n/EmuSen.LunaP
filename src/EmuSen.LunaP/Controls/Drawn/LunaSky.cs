using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Motion;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // The night gradient, Moonrise's stars held still and one glow easing to the focus's hue, drawn in pixels - see docs/LunaP.md §221.2.
    /// <summary>A night sky to stand a screen on: the gradient, a seeded star field and one soft glow whose hue follows the focus.</summary>
    public sealed class LunaSky : Control
    {
        /// <summary>The design height the glow's radii and the stars' size are measured in.</summary>
        public const double DesignHeight = 800;

        /// <summary>A star's size against the splash's.</summary>
        public const double StarSize = 0.7;

        private static readonly Stopwatch Running = Stopwatch.StartNew();
        private static readonly Easing Out = new CubicEaseOut();

        private int _starCount = SplashTimeline.Moonrise.StarCount;
        private uint _seed = SplashTimeline.Moonrise.Seed;
        private NightSky.Star[] _stars = [];
        private double? _hue;
        private Glide _shownHue, _strength = Glide.At(0), _x = Glide.At(0.5), _y = Glide.At(0.5);
        private Point _centre = new(0.5, 0.5);
        private Size _radii = new(756, 450);
        private double _glowStrength = 0.2;
        private IBrush? _glowBrush;
        private (double Hue, double Strength) _glowKey = (double.NaN, double.NaN);
        private bool _frameAsked;

        /// <summary>A sky with Moonrise's stars and no glow.</summary>
        public LunaSky()
        {
            IsHitTestVisible = false;
            Place();
        }

        /// <summary>The time the glow's easing reads; a host or test may give its own clock.</summary>
        public Func<TimeSpan> Clock { get; set; } = () => Running.Elapsed;

        /// <summary>How many stars; Moonrise's by default.</summary>
        public int StarCount { get => _starCount; set { _starCount = Math.Max(0, value); Place(); } }

        /// <summary>The seed that places the stars; Moonrise's by default.</summary>
        public uint Seed { get => _seed; set { _seed = value; Place(); } }

        /// <summary>The glow's hue in degrees, or null for no glow; a change eases along the shorter way round over the Pan token.</summary>
        public double? GlowHue
        {
            get => _hue;
            set
            {
                if (value == _hue) return;
                TimeSpan now = Clock();
                TimeSpan span = LunaMotion.Nominal(LunaMotionToken.Pan);
                if (value is double to)
                {
                    if (_hue is null && _strength.ValueAt(now) <= 0) _shownHue = Glide.At(to);
                    else
                    {
                        double from = _shownHue.ValueAt(now), target = from + Wrap(to - from);
                        _shownHue = _shownHue.Toward(target, now, span, Out);
                    }
                    _strength = _strength.Toward(1, now, span, Out);
                }
                else _strength = _strength.Toward(0, now, span, Out);
                _hue = value;
                InvalidateVisual();
            }
        }

        /// <summary>The glow's centre as shares of the width and height; a change eases with the hue.</summary>
        public Point GlowCentre
        {
            get => _centre;
            set
            {
                if (value == _centre) return;
                TimeSpan now = Clock(), span = LunaMotion.Nominal(LunaMotionToken.Pan);
                _x = _x.Toward(value.X, now, span, Out);
                _y = _y.Toward(value.Y, now, span, Out);
                _centre = value;
                InvalidateVisual();
            }
        }

        /// <summary>Puts the glow's centre at once, with no easing, as a screen first shown does.</summary>
        /// <param name="centre">The centre as shares of the width and height.</param>
        public void PlaceGlow(Point centre)
        {
            _centre = centre;
            _x = Glide.At(centre.X);
            _y = Glide.At(centre.Y);
            InvalidateVisual();
        }

        /// <summary>The glow's radii in units of the height over 800.</summary>
        public Size GlowRadii { get => _radii; set { _radii = value; InvalidateVisual(); } }

        /// <summary>The glow's alpha at its centre.</summary>
        public double GlowStrength { get => _glowStrength; set { _glowStrength = Math.Clamp(value, 0, 1); InvalidateVisual(); } }

        /// <summary>The hue drawn now, in [0, 360), or null while no glow shows.</summary>
        public double? ShownHue => _strength.ValueAt(Clock()) > 0 || _hue is not null ? Normal(_shownHue.ValueAt(Clock())) : null;

        /// <summary>Whether the glow is at rest.</summary>
        public bool Settled
        {
            get
            {
                TimeSpan now = Clock();
                return _shownHue.IsSettledAt(now) && _strength.IsSettledAt(now) && _x.IsSettledAt(now) && _y.IsSettledAt(now);
            }
        }

        /// <summary>The glow's bounds in the control at the current time, for an audit; empty while none shows.</summary>
        /// <returns>The ellipse's bounding box in the control's pixels, or an empty rectangle.</returns>
        public Rect GlowBounds()
        {
            TimeSpan now = Clock();
            double w = Bounds.Width, h = Bounds.Height, u = h / DesignHeight;
            if (_strength.ValueAt(now) <= 0 || w <= 0 || h <= 0) return default;
            double cx = _x.ValueAt(now) * w, cy = _y.ValueAt(now) * h, rx = _radii.Width * u, ry = _radii.Height * u;
            return new Rect(cx - rx, cy - ry, 2 * rx, 2 * ry);
        }

        private static double Wrap(double d) => ((d % 360) + 540) % 360 - 180;

        private static double Normal(double h) => ((h % 360) + 360) % 360;

        private void Place()
        {
            _stars = NightSky.Place(new SplashRandom(_seed), _starCount);
            InvalidateVisual();
        }

        private IBrush GlowBrush(double hue, double strength)
        {
            (double, double) key = (Math.Round(hue * 4) / 4, Math.Round(strength * 1000) / 1000);
            if (_glowBrush is not null && key == _glowKey) return _glowBrush;
            _glowKey = key;
            double h = key.Item1, a = key.Item2;
            return _glowBrush = new ImmutableRadialGradientBrush(
                [new ImmutableGradientStop(0, NightSky.Hsl(h, 0.7, 0.5, a)), new ImmutableGradientStop(0.45, NightSky.Hsl(h, 0.7, 0.4, a * 0.35)), new ImmutableGradientStop(0.75, NightSky.Hsl(h, 0.7, 0.4, 0)), new ImmutableGradientStop(1, NightSky.Hsl(h, 0.7, 0.4, 0))],
                radiusX: new RelativeScalar(0.5, RelativeUnit.Relative), radiusY: new RelativeScalar(0.5, RelativeUnit.Relative));
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;
            double u = h / DesignHeight;
            context.FillRectangle(NightSky.Screens, new Rect(0, 0, w, h));
            NightSky.DrawStars(context, _stars, w, h, u, StarSize, 0.85, 0);
            TimeSpan now = Clock();
            double strength = _strength.ValueAt(now) * _glowStrength;
            if (strength > 0)
            {
                Rect glow = GlowBounds();
                context.FillRectangle(GlowBrush(Normal(_shownHue.ValueAt(now)), strength), glow);
            }
            if (!Settled) AskFrame();
        }

        private void AskFrame()
        {
            if (_frameAsked || TopLevel.GetTopLevel(this) is not { } top) return;
            _frameAsked = true;
            top.RequestAnimationFrame(_ =>
            {
                _frameAsked = false;
                InvalidateVisual();
            });
        }
    }
}
