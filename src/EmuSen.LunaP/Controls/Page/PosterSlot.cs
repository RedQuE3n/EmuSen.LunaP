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

        /// <summary>Whether the OnArt gradient, black from nothing at the top to 72 per cent at the foot, is drawn over the picture so words over it read. True by default.</summary>
        public bool Scrim { get => GetValue(ScrimProperty); set => SetValue(ScrimProperty, value); }

        /// <summary>Whether a clip is playing in place of the poster: false until a video surface exists, and always false under reduced motion.</summary>
        public bool PlaysClip => false;

        /// <summary>How long the poster shows before a clip would cross-fade in, the Poster motion token.</summary>
        public static TimeSpan PosterDelay => LunaMotion.Nominal(LunaMotionToken.Poster);

        // The OnArt scrim's foot, black at 72 per cent, where the palette gives none.
        private static readonly Color ScrimFoot = Color.FromArgb(0xB8, 0, 0, 0);

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
            Color foot = this.TryFindResource("LunaOnArtScrimColor", ActualThemeVariant, out object? f) && f is Color g ? g : ScrimFoot;
            var gradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(foot, 1) },
            };
            context.FillRectangle(gradient, box);
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image, () => "Poster");
    }
}
