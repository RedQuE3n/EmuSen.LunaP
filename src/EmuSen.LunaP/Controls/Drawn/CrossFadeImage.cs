using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace EmuSen.LunaP.Controls
{
    // Two FittedImages, the new picture drawn over the old at Progress, the clock left to the consumer - see docs/LunaP.md §184.2.
    /// <summary>A picture that changes by fading the new one in over the old one, or over nothing, as Progress runs from 0 to 1.</summary>
    public class CrossFadeImage : Panel
    {
        public static readonly StyledProperty<double> ProgressProperty = AvaloniaProperty.Register<CrossFadeImage, double>(nameof(Progress), 1);
        public static readonly StyledProperty<ImageFit> FitProperty = AvaloniaProperty.Register<CrossFadeImage, ImageFit>(nameof(Fit), ImageFit.Contain);
        public static readonly StyledProperty<BitmapInterpolationMode> InterpolationProperty =
            AvaloniaProperty.Register<CrossFadeImage, BitmapInterpolationMode>(nameof(Interpolation), BitmapInterpolationMode.HighQuality);

        private readonly FittedImage _from = new() { IsVisible = false };
        private readonly FittedImage _to = new();

        public CrossFadeImage()
        {
            IsHitTestVisible = false;
            Children.Add(_from);
            Children.Add(_to);
            Apply();
        }

        /// <summary>How far the change has gone: 0 shows only the old picture, 1 only the new. 1 by default; clamped to [0, 1].</summary>
        public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

        /// <summary>How both pictures fill the box. Contain by default.</summary>
        public ImageFit Fit { get => GetValue(FitProperty); set => SetValue(FitProperty, value); }

        /// <summary>How both pictures are sampled when scaled. HighQuality by default.</summary>
        public BitmapInterpolationMode Interpolation { get => GetValue(InterpolationProperty); set => SetValue(InterpolationProperty, value); }

        /// <summary>The picture fading in, or shown whole once Progress is 1. Null shows nothing.</summary>
        public string? Source => _to.Source;

        /// <summary>The picture fading out beneath, while Progress is below 1; null when the change fades in over nothing.</summary>
        public string? Previous => _from.IsVisible ? _from.Source : null;

        /// <summary>The opacity the new picture is drawn at now.</summary>
        public double SourceOpacity => _to.Opacity;

        /// <summary>Starts a change to a new picture at Progress 0: over the current one when overPrevious is true, else over nothing.</summary>
        /// <param name="source">The file to show; null shows nothing once the change is done.</param>
        /// <param name="overPrevious">Whether the current picture stays beneath until the change is done.</param>
        public void Show(string? source, bool overPrevious = true)
        {
            _from.Source = overPrevious ? _to.Source : null;
            _to.Source = source;
            SetCurrentValue(ProgressProperty, 0d);
            Apply();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ProgressProperty || change.Property == FitProperty || change.Property == InterpolationProperty) Apply();
        }

        private void Apply()
        {
            double p = Math.Clamp(Progress, 0, 1);
            _to.Opacity = p;
            _from.IsVisible = p < 1 && _from.Source is { Length: > 0 };
            foreach (FittedImage image in new[] { _from, _to })
            {
                image.Fit = Fit;
                image.Interpolation = Interpolation;
            }
        }
    }
}
