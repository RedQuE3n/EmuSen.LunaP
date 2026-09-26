using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>One hint of a HintBar: an icon file, or a short glyph drawn in a ring when there is none, and its label.</summary>
    /// <param name="Label">The words beside the icon.</param>
    /// <param name="IconPath">An image file for the icon; null draws the glyph in a ring.</param>
    /// <param name="Glyph">A letter or two drawn in the ring when there is no icon file.</param>
    public sealed record HintEntry(string Label, string? IconPath = null, string? Glyph = null)
    {
        /// <summary>A gamepad button to draw, in the bar's PadFamily, when there is no icon file; null draws the glyph in a ring.</summary>
        public PadGlyphButton? Button { get; init; }
    }

    // A row of button hints, icon then label, on an optional rounded background, sized from its font - see docs/LunaP.md §101.3.
    /// <summary>A row of button hints, each an icon and a label, in a typeface from a file, on an optional rounded background.</summary>
    public class HintBar : Control
    {
        public static readonly StyledProperty<IReadOnlyList<HintEntry>?> EntriesProperty = AvaloniaProperty.Register<HintBar, IReadOnlyList<HintEntry>?>(nameof(Entries));
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<HintBar, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<HintBar, double>(nameof(FontSize), 16);
        public static readonly StyledProperty<double> EntryScaleProperty = AvaloniaProperty.Register<HintBar, double>(nameof(EntryScale), 1);
        public static readonly StyledProperty<Color> IconColorProperty = AvaloniaProperty.Register<HintBar, Color>(nameof(IconColor), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<HintBar, Color>(nameof(TextColor), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<HintBar, Color>(nameof(BackgroundColor), Colors.Transparent);
        public static readonly StyledProperty<double> BackgroundCornerRadiusProperty = AvaloniaProperty.Register<HintBar, double>(nameof(BackgroundCornerRadius));
        public static readonly StyledProperty<Thickness> PaddingProperty = AvaloniaProperty.Register<HintBar, Thickness>(nameof(Padding));
        public static readonly StyledProperty<double> EntrySpacingProperty = AvaloniaProperty.Register<HintBar, double>(nameof(EntrySpacing), 16);
        public static readonly StyledProperty<double> IconTextSpacingProperty = AvaloniaProperty.Register<HintBar, double>(nameof(IconTextSpacing), 6);
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<HintBar, LetterCase>(nameof(LetterCase));
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<HintBar, PadFamily>(nameof(PadFamily));

        static HintBar()
        {
            AffectsMeasure<HintBar>(EntriesProperty, FontPathProperty, FontSizeProperty, EntryScaleProperty, PaddingProperty, EntrySpacingProperty, IconTextSpacingProperty, LetterCaseProperty);
            AffectsRender<HintBar>(IconColorProperty, TextColorProperty, BackgroundColorProperty, BackgroundCornerRadiusProperty, PadFamilyProperty);
        }

        /// <summary>The hints, left to right.</summary>
        public IReadOnlyList<HintEntry>? Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

        /// <summary>The font file the labels are drawn in; null for the application's default typeface.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size in pixels before EntryScale, 16 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>A scale applied to the labels and icons together, 1 by default.</summary>
        public double EntryScale { get => GetValue(EntryScaleProperty); set => SetValue(EntryScaleProperty, value); }

        /// <summary>The icons' colour, the palette's text colour by default.</summary>
        public Color IconColor { get => GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }

        /// <summary>The labels' colour, the palette's text colour by default.</summary>
        public Color TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

        /// <summary>A fill behind the row and its padding. Transparent by default.</summary>
        public Color BackgroundColor { get => GetValue(BackgroundColorProperty); set => SetValue(BackgroundColorProperty, value); }

        /// <summary>The background's corner radius in pixels. 0 by default.</summary>
        public double BackgroundCornerRadius { get => GetValue(BackgroundCornerRadiusProperty); set => SetValue(BackgroundCornerRadiusProperty, value); }

        /// <summary>Space between the background's edge and the hints.</summary>
        public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

        /// <summary>The gap between one hint's label and the next hint's icon, in pixels. 16 by default.</summary>
        public double EntrySpacing { get => GetValue(EntrySpacingProperty); set => SetValue(EntrySpacingProperty, value); }

        /// <summary>The gap between an icon and its label, in pixels. 6 by default.</summary>
        public double IconTextSpacing { get => GetValue(IconTextSpacingProperty); set => SetValue(IconTextSpacingProperty, value); }

        /// <summary>The casing of the labels. None by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The set an entry's Button is drawn from. Generic by default. A change redraws the icons and moves nothing: every glyph fills the same square.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        private double Size => FontSize * EntryScale;

        private GlyphTypeface Typeface() => FontPath is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;

        /// <summary>Each hint's icon box and label box, in the control's coordinates.</summary>
        /// <param name="bounds">The control's size; the hints run from the left padding whatever it is.</param>
        /// <returns>One pair of rectangles per entry, in order.</returns>
        public IReadOnlyList<(Rect Icon, Rect Label)> Layout(Size bounds)
        {
            var boxes = new List<(Rect, Rect)>();
            GlyphTypeface typeface = Typeface();
            double size = Size, icon = size, x = Padding.Left, y = Padding.Top;
            double rowHeight = Math.Max(icon, size * 1.2);
            foreach (HintEntry e in Entries ?? Array.Empty<HintEntry>())
            {
                double iconW = e.IconPath is { Length: > 0 } && PictureFiles.Intrinsic(e.IconPath) is { Width: > 0, Height: > 0 } own ? icon * own.Width / own.Height : icon;
                var iconBox = new Rect(x, y + (rowHeight - icon) / 2, iconW, icon);
                x += iconW + IconTextSpacing;
                double w = FontLayout.Measure(typeface, size, FontLayout.Cased(e.Label, LetterCase));
                var label = new Rect(x, y, w, rowHeight);
                boxes.Add((iconBox, label));
                x += w + EntrySpacing;
            }

            return boxes;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            IReadOnlyList<(Rect Icon, Rect Label)> boxes = Layout(default);
            if (boxes.Count == 0) return default;
            double right = boxes[^1].Label.Right + Padding.Right;
            double bottom = boxes.Max(b => Math.Max(b.Icon.Bottom, b.Label.Bottom)) + Padding.Bottom;
            return new Size(right, bottom);
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            if (BackgroundColor.A > 0) context.DrawRectangle(new ImmutableSolidColorBrush(BackgroundColor), null, bounds, BackgroundCornerRadius, BackgroundCornerRadius);
            IReadOnlyList<HintEntry> entries = Entries ?? Array.Empty<HintEntry>();
            IReadOnlyList<(Rect Icon, Rect Label)> boxes = Layout(Bounds.Size);
            GlyphTypeface typeface = Typeface();
            var text = new ImmutableSolidColorBrush(TextColor);
            for (int i = 0; i < boxes.Count; i++)
            {
                HintEntry e = entries[i];
                // An image file wins, then a named button in the bar's family, then the glyph in a ring (§103).
                if (e.IconPath is { Length: > 0 }) PictureFiles.Draw(context, this, e.IconPath, boxes[i].Icon, IconColor);
                else if (e.Button is { } button) PadGlyphDrawing.Draw(context, boxes[i].Icon, PadFamily, button, IconColor);
                else Ring(context, typeface, e.Glyph ?? "", boxes[i].Icon, IconColor);
                FontLayout.Create(typeface, Size, FontLayout.Cased(e.Label, LetterCase), 1.2, double.PositiveInfinity, double.PositiveInfinity, false, null)
                    .Draw(context, text, boxes[i].Label, TextAlignment.Left, 0.5);
            }
        }

        // The built-in icon: the glyph centred in a ring of the icon colour.
        private static void Ring(DrawingContext context, GlyphTypeface typeface, string glyph, Rect box, Color colour)
        {
            var brush = new ImmutableSolidColorBrush(colour);
            double r = Math.Min(box.Width, box.Height) / 2;
            context.DrawEllipse(null, new ImmutablePen(brush, Math.Max(1, r / 7)), box.Center, r * 0.9, r * 0.9);
            if (glyph.Length == 0) return;
            FontLayout.Create(typeface, r * 1.1, glyph, 1, double.PositiveInfinity, double.PositiveInfinity, false, null).Draw(context, brush, box, TextAlignment.Center, 0.5);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group, () => string.Join(", ", (Entries ?? Array.Empty<HintEntry>()).Select(e => $"{(e.Button is { } b && e.IconPath is not { Length: > 0 } ? PadGlyph.Describe(PadFamily, b) : e.Glyph ?? "")} {e.Label}".Trim())));
    }
}
