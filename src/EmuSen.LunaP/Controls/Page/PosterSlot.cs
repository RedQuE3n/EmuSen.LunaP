using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // A picture's place shaped for the clip that will replace it, under the OnArt scrim - see docs/LunaP.md §209.3.
    /// <summary>A hero picture filling its box under the OnArt gradient, shaped for a clip to cross-fade into after the Poster delay once a video surface plays it.</summary>
    public class PosterSlot : Control
    {
        /// <summary>Defines the Poster property.</summary>
        public static readonly StyledProperty<IImage?> PosterProperty = AvaloniaProperty.Register<PosterSlot, IImage?>(nameof(Poster));

        /// <summary>Defines the Video property.</summary>
        public static readonly StyledProperty<object?> VideoProperty = AvaloniaProperty.Register<PosterSlot, object?>(nameof(Video));

        /// <summary>Defines the Scrim property.</summary>
        public static readonly StyledProperty<bool> ScrimProperty = AvaloniaProperty.Register<PosterSlot, bool>(nameof(Scrim), true);

        static PosterSlot() => AffectsRender<PosterSlot>(PosterProperty, ScrimProperty);

        /// <summary>The picture drawn, filling the slot and cut at its edges, never stretched; null draws the surface alone.</summary>
        public IImage? Poster { get => GetValue(PosterProperty); set => SetValue(PosterProperty, value); }

        /// <summary>The clip the poster turns into, kept for the video surface that will play it; nothing plays it yet, so the poster stays.</summary>
        public object? Video { get => GetValue(VideoProperty); set => SetValue(VideoProperty, value); }

        /// <summary>Whether the hero's scrim, clear at the top, the OnArt scrim by a third of the height and near black at the foot, is drawn over the picture so words low in it read. True by default.</summary>
        public bool Scrim { get => GetValue(ScrimProperty); set => SetValue(ScrimProperty, value); }

        /// <summary>Whether a clip is playing in place of the poster: false until a video surface exists, and always false under reduced motion.</summary>
        public bool PlaysClip => false;

        /// <summary>How long the poster shows before a clip would cross-fade in, the Poster motion token.</summary>
        public static TimeSpan PosterDelay => LunaMotion.Nominal(LunaMotionToken.Poster);

        /// <summary>Where down the hero the scrim reaches the OnArt scrim's darkness, as a share of its height; below it the scrim deepens to the hero's foot.</summary>
        public const double ScrimFull = 0.35;

        /// <summary>The palette's colour keys the scrim is drawn from: the OnArt scrim and the hero's foot.</summary>
        public const string ScrimColourKey = "LunaOnArtScrimColor", FootColourKey = "LunaHeroFootColor";

        /// <summary>The brush drawn over the picture: clear at the top, the OnArt scrim at ScrimFull, the hero's foot at the bottom, in the palette's colours (black at 72 and 94 per cent where none is in reach).</summary>
        public IBrush ScrimBrush => new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Colour(ScrimColourKey, Color.FromArgb(0xB8, 0, 0, 0)), ScrimFull),
                new GradientStop(Colour(FootColourKey, Color.FromArgb(0xF0, 0, 0, 0)), 1),
            },
        };

        private Color Colour(string key, Color fallback) => this.TryFindResource(key, ActualThemeVariant, out object? found) && found is Color c ? c : fallback;

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            var box = new Rect(Bounds.Size);
            if (box.Width <= 0 || box.Height <= 0) return;
            Color surface = this.TryFindResource("LunaSurfaceRaisedColor", ActualThemeVariant, out object? s) && s is Color c ? c : Color.FromRgb(0x26, 0x26, 0x2B);
            context.FillRectangle(new ImmutableSolidColorBrush(surface), box);
            if (Poster is { } picture && picture.Size.Width > 0 && picture.Size.Height > 0)
            {
                double scale = Math.Max(box.Width / picture.Size.Width, box.Height / picture.Size.Height);
                Size drawn = picture.Size * scale;
                var source = new Rect((drawn.Width - box.Width) / 2 / scale, (drawn.Height - box.Height) / 2 / scale, box.Width / scale, box.Height / scale);
                using (context.PushClip(box)) context.DrawImage(picture, source, box);
            }
            if (!Scrim) return;
            context.FillRectangle(ScrimBrush, box);
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image, () => "Poster");
    }
}
