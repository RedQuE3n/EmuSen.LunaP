using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The order a FrameSequenceImage plays its frames in.</summary>
    public enum FrameDirection
    {
        /// <summary>First to last, then from the first again.</summary>
        Normal,
        /// <summary>Last to first, then from the last again.</summary>
        Reverse,
        /// <summary>First to last and back, each end once.</summary>
        Alternate,
        /// <summary>Last to first and back, each end once.</summary>
        AlternateReverse,
    }

    // An animated GIF played from the host's clock, by the timing rules the consumer measured from its reference - see docs/LunaP.md §194.
    /// <summary>An animated GIF drawn frame by frame: the frame shown is a function of Time, Speed, Direction and IterationCount, the control keeping no clock of its own; fitted, tinted, desaturated and rounded as FittedImage is.</summary>
    public class FrameSequenceImage : Control
    {
        public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<FrameSequenceImage, string?>(nameof(Source));
        public static readonly StyledProperty<TimeSpan> TimeProperty = AvaloniaProperty.Register<FrameSequenceImage, TimeSpan>(nameof(Time));
        public static readonly StyledProperty<double> SpeedProperty = AvaloniaProperty.Register<FrameSequenceImage, double>(nameof(Speed), 1);
        public static readonly StyledProperty<FrameDirection> DirectionProperty = AvaloniaProperty.Register<FrameSequenceImage, FrameDirection>(nameof(Direction));
        public static readonly StyledProperty<int> IterationCountProperty = AvaloniaProperty.Register<FrameSequenceImage, int>(nameof(IterationCount));
        public static readonly StyledProperty<ImageFit> FitProperty = AvaloniaProperty.Register<FrameSequenceImage, ImageFit>(nameof(Fit), ImageFit.Contain);
        public static readonly StyledProperty<Color> TintProperty = AvaloniaProperty.Register<FrameSequenceImage, Color>(nameof(Tint), Colors.White);
        public static readonly StyledProperty<Color?> TintEndProperty = AvaloniaProperty.Register<FrameSequenceImage, Color?>(nameof(TintEnd));
        public static readonly StyledProperty<Orientation> TintDirectionProperty = AvaloniaProperty.Register<FrameSequenceImage, Orientation>(nameof(TintDirection));
        public static readonly StyledProperty<double> SaturationProperty = AvaloniaProperty.Register<FrameSequenceImage, double>(nameof(Saturation), 1);
        public static readonly StyledProperty<double> CornerRadiusProperty = AvaloniaProperty.Register<FrameSequenceImage, double>(nameof(CornerRadius));
        public static readonly StyledProperty<BitmapInterpolationMode> InterpolationProperty = AvaloniaProperty.Register<FrameSequenceImage, BitmapInterpolationMode>(nameof(Interpolation), BitmapInterpolationMode.None);

        private const int KeptFrames = 32;

        private readonly Dictionary<(int, ImageEffects), Bitmap> _frames = new();

        static FrameSequenceImage()
        {
            AffectsMeasure<FrameSequenceImage>(SourceProperty, FitProperty);
            AffectsRender<FrameSequenceImage>(TimeProperty, SpeedProperty, DirectionProperty, IterationCountProperty, TintProperty, TintEndProperty, TintDirectionProperty, SaturationProperty,
                CornerRadiusProperty, InterpolationProperty);
        }

        /// <summary>The .gif file to play; a missing or unreadable one shows nothing.</summary>
        public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

        /// <summary>How long the animation has played, on the host's clock; the frame shown is a function of it.</summary>
        public TimeSpan Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

        /// <summary>A multiplier on the rate frames advance at. 1 by default.</summary>
        public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

        /// <summary>The order frames are played in. Normal by default.</summary>
        public FrameDirection Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

        /// <summary>How many times the animation plays before it holds its last frame; 0, the default, plays forever.</summary>
        public int IterationCount { get => GetValue(IterationCountProperty); set => SetValue(IterationCountProperty, value); }

        /// <summary>How each frame fills the box: Fill stretches, anything else contains. Contain by default.</summary>
        public ImageFit Fit { get => GetValue(FitProperty); set => SetValue(FitProperty, value); }

        /// <summary>A colour every pixel is multiplied by. White by default.</summary>
        public Color Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }

        /// <summary>When set, the tint runs from Tint to this colour across the frame. Null by default.</summary>
        public Color? TintEnd { get => GetValue(TintEndProperty); set => SetValue(TintEndProperty, value); }

        /// <summary>The direction a two-colour tint runs in. Horizontal by default.</summary>
        public Orientation TintDirection { get => GetValue(TintDirectionProperty); set => SetValue(TintDirectionProperty, value); }

        /// <summary>1 keeps the colours, 0 is greyscale. 1 by default.</summary>
        public double Saturation { get => GetValue(SaturationProperty); set => SetValue(SaturationProperty, value); }

        /// <summary>The corner radius, in pixels, the frame is clipped to. 0 by default.</summary>
        public double CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

        /// <summary>How frames are sampled when scaled: None, nearest, by default.</summary>
        public BitmapInterpolationMode Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }

        private GifFile? File() => Source is { Length: > 0 } p ? GifFile.Open(p) : null;

        /// <summary>The number of frames the source holds, 0 when there is none.</summary>
        public int FrameCount => File()?.Count ?? 0;

        /// <summary>The logical screen's size in pixels, 0 by 0 when there is no source.</summary>
        public Size IntrinsicSize => File() is { } f ? new Size(f.Width, f.Height) : default;

        /// <summary>How long each frame is shown at Speed 1: the first frame's delay, which the reference applies to every frame, or 100 ms when it is 0.</summary>
        public TimeSpan FrameDuration => File() is { } f && f.Delays[0] > TimeSpan.Zero ? f.Delays[0] : TimeSpan.FromMilliseconds(100);

        /// <summary>The frame shown at a time: the first frame for one frame's duration, then the direction's sequence, holding at the end of the last iteration.</summary>
        /// <param name="time">How long the animation has played.</param>
        /// <returns>A frame index, 0 when there are no frames.</returns>
        public int FrameAt(TimeSpan time)
        {
            int n = FrameCount;
            if (n <= 1) return 0;
            double d = FrameDuration.TotalMilliseconds / Math.Max(0.01, Speed);
            long step = (long)Math.Floor(Math.Max(0, time.TotalMilliseconds) / d);
            if (step == 0) return 0;
            long p = step - 1;
            bool bounce = Direction is FrameDirection.Alternate or FrameDirection.AlternateReverse;
            long period = bounce ? 2L * n - 2 : n;
            if (IterationCount > 0) p = Math.Min(p, IterationCount * period - (bounce ? 0 : 1));
            long q = p % period;
            int forward = (int)(bounce ? (q < n ? q : period - q) : q);
            return Direction is FrameDirection.Reverse or FrameDirection.AlternateReverse ? n - 1 - forward : forward;
        }

        /// <summary>The earliest time from a time on at which the frame shown changes; null when it never will.</summary>
        /// <param name="from">A time on the same clock as Time.</param>
        /// <returns>The time of the next frame, or null once the last iteration has ended or there is one frame.</returns>
        public TimeSpan? NextFrameChange(TimeSpan from)
        {
            int n = FrameCount;
            if (n <= 1) return null;
            double d = FrameDuration.TotalMilliseconds / Math.Max(0.01, Speed);
            long step = (long)Math.Floor(Math.Max(0, from.TotalMilliseconds) / d);
            if (IterationCount > 0)
            {
                bool bounce = Direction is FrameDirection.Alternate or FrameDirection.AlternateReverse;
                long last = 1 + IterationCount * (bounce ? 2L * n - 2 : n) - (bounce ? 0 : 1);
                if (step >= last) return null;
            }

            return TimeSpan.FromMilliseconds((step + 1) * d);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceProperty) _frames.Clear();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Size own = IntrinsicSize;
            if (own.Width <= 0 || own.Height <= 0) return default;
            bool wInf = double.IsInfinity(availableSize.Width), hInf = double.IsInfinity(availableSize.Height);
            if (wInf && hInf) return own;
            if (wInf) return new Size(availableSize.Height * own.Width / own.Height, availableSize.Height);
            if (hInf) return new Size(availableSize.Width, availableSize.Width * own.Height / own.Width);
            if (Fit != ImageFit.Contain) return availableSize;
            double s = Math.Min(availableSize.Width / own.Width, availableSize.Height / own.Height);
            return new Size(own.Width * s, own.Height * s);
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            GifFile? file = File();
            if (file is null || bounds.Width <= 0 || bounds.Height <= 0) return;
            var effects = new ImageEffects(Tint, TintEnd ?? Tint, TintDirection == Orientation.Vertical, Saturation);
            int index = FrameAt(Time);
            if (!_frames.TryGetValue((index, effects), out Bitmap? frame))
            {
                if (_frames.Count >= KeptFrames) _frames.Clear(); // a long GIF is decoded as it plays rather than held whole (§194)
                _frames[(index, effects)] = frame = file.Frame(index, effects);
            }
            Rect target = Fit == ImageFit.Contain ? FittedImage.Contain(bounds, new Size(file.Width, file.Height)) : bounds;
            using DrawingContext.PushedState options = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = Interpolation });
            using DrawingContext.PushedState? clip = CornerRadius > 0 ? context.PushClip(new RoundedRect(bounds, CornerRadius)) : null;
            context.DrawImage(frame, new Rect(frame.Size), target);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image);
    }
}
