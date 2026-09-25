using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // Text in a typeface read from a file, wrapped, aligned, cased and cut with an ellipsis, on an optional rounded background - see docs/LunaP.md §98.3.
    /// <summary>Text drawn in a typeface read from a font file, with alignment, line spacing, letter case, an ellipsis where it does not fit, and an optional rounded background.</summary>
    public class FontText : Control
    {
        public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<FontText, string?>(nameof(Text));
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<FontText, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<FontText, double>(nameof(FontSize), 16);
        public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<FontText, IBrush?>(nameof(Foreground), LunaPalette.Text);
        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty = AvaloniaProperty.Register<FontText, TextAlignment>(nameof(TextAlignment));
        public static readonly StyledProperty<VerticalAlignment> TextVerticalAlignmentProperty = AvaloniaProperty.Register<FontText, VerticalAlignment>(nameof(TextVerticalAlignment), VerticalAlignment.Top);
        public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<FontText, double>(nameof(LineSpacing), 1.2);
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<FontText, LetterCase>(nameof(LetterCase));
        public static readonly StyledProperty<bool> WrapProperty = AvaloniaProperty.Register<FontText, bool>(nameof(Wrap), true);
        public static readonly StyledProperty<string?> EllipsisProperty = AvaloniaProperty.Register<FontText, string?>(nameof(Ellipsis), "…");
        public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<FontText, IBrush?>(nameof(Background));
        public static readonly StyledProperty<double> BackgroundCornerRadiusProperty = AvaloniaProperty.Register<FontText, double>(nameof(BackgroundCornerRadius));
        public static readonly StyledProperty<Thickness> PaddingProperty = AvaloniaProperty.Register<FontText, Thickness>(nameof(Padding));

        private FontLayout? _layout;

        static FontText()
        {
            AffectsMeasure<FontText>(TextProperty, FontPathProperty, FontSizeProperty, LineSpacingProperty, LetterCaseProperty, WrapProperty, EllipsisProperty, PaddingProperty);
            AffectsRender<FontText>(ForegroundProperty, TextAlignmentProperty, TextVerticalAlignmentProperty, BackgroundProperty, BackgroundCornerRadiusProperty);
        }

        /// <summary>The text shown. Line breaks start new lines.</summary>
        public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

        /// <summary>The font file to draw with; when null or unreadable the application's default typeface is used. Each file is read once, and never added to the font manager.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size in pixels, 16 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>The text's colour, the palette's text colour by default.</summary>
        public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        /// <summary>Where each line sits across the width. Left by default.</summary>
        public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

        /// <summary>Where the block of lines sits in a box taller than it. Top by default.</summary>
        public VerticalAlignment TextVerticalAlignment { get => GetValue(TextVerticalAlignmentProperty); set => SetValue(TextVerticalAlignmentProperty, value); }

        /// <summary>The distance between baselines as a multiple of FontSize, 1.2 by default.</summary>
        public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

        /// <summary>The casing applied before layout. None by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>Whether lines wrap at the width. True by default; false keeps each paragraph on one line.</summary>
        public bool Wrap { get => GetValue(WrapProperty); set => SetValue(WrapProperty, value); }

        /// <summary>What ends a line that was cut short; a horizontal ellipsis by default, null to cut without a mark.</summary>
        public string? Ellipsis { get => GetValue(EllipsisProperty); set => SetValue(EllipsisProperty, value); }

        /// <summary>A fill behind the text and its padding. None by default.</summary>
        public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        /// <summary>The background's corner radius in pixels. 0 by default.</summary>
        public double BackgroundCornerRadius { get => GetValue(BackgroundCornerRadiusProperty); set => SetValue(BackgroundCornerRadiusProperty, value); }

        /// <summary>Space between the background's edge and the text.</summary>
        public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

        /// <summary>Whether the last layout cut the text short.</summary>
        public bool IsTruncated => _layout?.Truncated ?? false;

        /// <summary>How many lines the last layout produced.</summary>
        public int LineCount => _layout?.Lines.Count ?? 0;

        /// <summary>The height of one line in pixels, FontSize times LineSpacing to a hundredth.</summary>
        public double LineHeight => Math.Round(FontSize * LineSpacing * 100) / 100;

        /// <summary>The lines as laid out, after casing, wrapping and truncation.</summary>
        /// <returns>Each line's text, top to bottom; empty before the first layout.</returns>
        public string[] LaidOutLines() => _layout is null ? Array.Empty<string>() : Array.ConvertAll(System.Linq.Enumerable.ToArray(_layout.Lines), l => l.Text);

        private GlyphTypeface Typeface() => FontPath is { Length: > 0 } path && FontFiles.Load(path) is { } loaded ? loaded : FontFiles.Default;

        protected override Size MeasureOverride(Size availableSize)
        {
            Thickness p = Padding;
            double w = availableSize.Width - p.Left - p.Right, h = availableSize.Height - p.Top - p.Bottom;
            _layout = FontLayout.Create(Typeface(), FontSize, FontLayout.Cased(Text ?? "", LetterCase), LineSpacing, Math.Max(0, w), Math.Max(0, h), Wrap, Ellipsis);
            return new Size(_layout.Width + p.Left + p.Right, _layout.Height + p.Top + p.Bottom);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Thickness p = Padding;
            double w = finalSize.Width - p.Left - p.Right, h = finalSize.Height - p.Top - p.Bottom;
            _layout = FontLayout.Create(Typeface(), FontSize, FontLayout.Cased(Text ?? "", LetterCase), LineSpacing, Math.Max(0, w), Math.Max(0, h), Wrap, Ellipsis);
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            if (Background is { } background) context.DrawRectangle(background, null, bounds, BackgroundCornerRadius, BackgroundCornerRadius);
            if (_layout is null || Foreground is not { } brush) return;
            Rect box = bounds.Deflate(Padding);
            using DrawingContext.PushedState clip = context.PushClip(bounds);
            double fraction = TextVerticalAlignment switch { VerticalAlignment.Center => 0.5, VerticalAlignment.Bottom => 1, _ => 0 };
            _layout.Draw(context, brush, box, TextAlignment, fraction);
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Text, () => Text);
    }
}
