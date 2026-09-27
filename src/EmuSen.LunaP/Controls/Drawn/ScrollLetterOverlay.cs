using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // The letters a fast-scrolling list is passing, over a shade of its whole box - see docs/LunaP.md §180.
    /// <summary>An overlay for a list scrolled fast: a shade over the whole box and, in its middle, the first letters of the entry passing or a star.</summary>
    public class ScrollLetterOverlay : Control
    {
        public static readonly StyledProperty<string?> LettersProperty = AvaloniaProperty.Register<ScrollLetterOverlay, string?>(nameof(Letters));
        public static readonly StyledProperty<bool> StarProperty = AvaloniaProperty.Register<ScrollLetterOverlay, bool>(nameof(Star));
        public static readonly StyledProperty<Color> ShadeProperty = AvaloniaProperty.Register<ScrollLetterOverlay, Color>(nameof(Shade), Color.FromArgb(0x60, 0, 0, 0));
        public static readonly StyledProperty<Color> ForegroundProperty = AvaloniaProperty.Register<ScrollLetterOverlay, Color>(nameof(Foreground), LunaPalette.Text.Color);
        public static readonly StyledProperty<double> LetterSizeProperty = AvaloniaProperty.Register<ScrollLetterOverlay, double>(nameof(LetterSize), 96);

        static ScrollLetterOverlay()
        {
            AffectsRender<ScrollLetterOverlay>(LettersProperty, StarProperty, ShadeProperty, ForegroundProperty, LetterSizeProperty);
            IsHitTestVisibleProperty.OverrideDefaultValue<ScrollLetterOverlay>(false);
        }

        /// <summary>The letters shown; null or empty with Star off draws nothing at all.</summary>
        public string? Letters { get => GetValue(LettersProperty); set => SetValue(LettersProperty, value); }

        /// <summary>Whether a star is shown in place of the letters, for an entry among favourites kept on top. False by default.</summary>
        public bool Star { get => GetValue(StarProperty); set => SetValue(StarProperty, value); }

        /// <summary>The colour laid over the whole box while something is shown. Black at three-eighths opacity by default.</summary>
        public Color Shade { get => GetValue(ShadeProperty); set => SetValue(ShadeProperty, value); }

        /// <summary>The colour of the letters and the star, the palette's text colour by default.</summary>
        public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        /// <summary>The letters' size in pixels, the star's height with it. 96 by default.</summary>
        public double LetterSize { get => GetValue(LetterSizeProperty); set => SetValue(LetterSizeProperty, value); }

        /// <summary>Whether the overlay draws anything with its present values.</summary>
        public bool Showing => Star || !string.IsNullOrEmpty(Letters);

        public override void Render(DrawingContext context)
        {
            if (!Showing || Bounds.Width <= 0 || Bounds.Height <= 0) return;
            var box = new Rect(Bounds.Size);
            context.DrawRectangle(new ImmutableSolidColorBrush(Shade), null, box);
            var brush = new ImmutableSolidColorBrush(Foreground);
            if (Star)
            {
                double side = LetterSize;
                context.DrawGeometry(brush, null, StarRating.StarGeometry(new Rect(box.Center.X - side / 2, box.Center.Y - side / 2, side, side)));
                return;
            }
            var text = new FormattedText(Letters!, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, LetterSize, brush);
            context.DrawText(text, new Point(box.Center.X - text.Width / 2, box.Center.Y - text.Height / 2));
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Text, () => Star ? "Favorites" : Letters ?? "");
    }
}
