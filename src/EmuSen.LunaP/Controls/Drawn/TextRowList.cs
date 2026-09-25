using System;
using System.Collections.Generic;
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
    /// <summary>One row of a TextRowList: its text, and whether it is drawn in the secondary colour.</summary>
    /// <param name="Text">The row's words.</param>
    /// <param name="Secondary">Whether the row takes the secondary colours, as a folder might.</param>
    public sealed record TextRow(string Text, bool Secondary = false);

    // Rows of one-line text at a fixed pitch, one selected with a selector bar and a rounded background, the window kept about it - see docs/LunaP.md §100.1.
    /// <summary>A list of one-line rows in a typeface from a file, drawn at a fixed pitch, with one row selected behind a selector bar and a rounded background, scrolled to keep the selection in view.</summary>
    public class TextRowList : Control
    {
        public static readonly StyledProperty<IReadOnlyList<TextRow>?> ItemsProperty = AvaloniaProperty.Register<TextRowList, IReadOnlyList<TextRow>?>(nameof(Items));
        public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<TextRowList, int>(nameof(SelectedIndex));
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<TextRowList, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(FontSize), 16);
        public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(LineSpacing), 1.5);
        public static readonly StyledProperty<Color> PrimaryColorProperty = AvaloniaProperty.Register<TextRowList, Color>(nameof(PrimaryColor), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color> SecondaryColorProperty = AvaloniaProperty.Register<TextRowList, Color>(nameof(SecondaryColor), LunaPalette.Muted.Color);
        public static readonly StyledProperty<Color> SelectedColorProperty = AvaloniaProperty.Register<TextRowList, Color>(nameof(SelectedColor), LunaPalette.OnAccent.Color);
        public static readonly StyledProperty<Color?> SelectedSecondaryColorProperty = AvaloniaProperty.Register<TextRowList, Color?>(nameof(SelectedSecondaryColor));
        public static readonly StyledProperty<Color> SelectorColorProperty = AvaloniaProperty.Register<TextRowList, Color>(nameof(SelectorColor), LunaPalette.Accent.Color);
        public static readonly StyledProperty<double> SelectorHeightProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(SelectorHeight), double.NaN);
        public static readonly StyledProperty<double> SelectorOffsetYProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(SelectorOffsetY));
        public static readonly StyledProperty<Color> SelectedBackgroundColorProperty = AvaloniaProperty.Register<TextRowList, Color>(nameof(SelectedBackgroundColor), Colors.Transparent);
        public static readonly StyledProperty<Thickness> SelectedBackgroundMarginsProperty = AvaloniaProperty.Register<TextRowList, Thickness>(nameof(SelectedBackgroundMargins));
        public static readonly StyledProperty<double> SelectedBackgroundCornerRadiusProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(SelectedBackgroundCornerRadius));
        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty = AvaloniaProperty.Register<TextRowList, TextAlignment>(nameof(TextAlignment));
        public static readonly StyledProperty<double> HorizontalMarginProperty = AvaloniaProperty.Register<TextRowList, double>(nameof(HorizontalMargin));
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<TextRowList, LetterCase>(nameof(LetterCase));

        static TextRowList()
        {
            AffectsRender<TextRowList>(ItemsProperty, SelectedIndexProperty, FontPathProperty, FontSizeProperty, LineSpacingProperty, PrimaryColorProperty, SecondaryColorProperty,
                SelectedColorProperty, SelectedSecondaryColorProperty, SelectorColorProperty, SelectorHeightProperty, SelectorOffsetYProperty, SelectedBackgroundColorProperty,
                SelectedBackgroundMarginsProperty, SelectedBackgroundCornerRadiusProperty, TextAlignmentProperty, HorizontalMarginProperty, LetterCaseProperty);
        }

        /// <summary>The rows, top to bottom.</summary>
        public IReadOnlyList<TextRow>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        /// <summary>The selected row; out of range selects nothing.</summary>
        public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

        /// <summary>The font file the rows are drawn in; null for the application's default typeface.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size in pixels, 16 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>The row pitch as a multiple of FontSize, 1.5 by default.</summary>
        public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

        /// <summary>The colour of an ordinary row, the palette's text colour by default.</summary>
        public Color PrimaryColor { get => GetValue(PrimaryColorProperty); set => SetValue(PrimaryColorProperty, value); }

        /// <summary>The colour of a row marked Secondary, the palette's muted colour by default.</summary>
        public Color SecondaryColor { get => GetValue(SecondaryColorProperty); set => SetValue(SecondaryColorProperty, value); }

        /// <summary>The colour of the selected row's text, the palette's on-accent colour by default.</summary>
        public Color SelectedColor { get => GetValue(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }

        /// <summary>The selected row's colour when it is Secondary; null uses SelectedColor.</summary>
        public Color? SelectedSecondaryColor { get => GetValue(SelectedSecondaryColorProperty); set => SetValue(SelectedSecondaryColorProperty, value); }

        /// <summary>The bar across the selected row, the palette's accent by default.</summary>
        public Color SelectorColor { get => GetValue(SelectorColorProperty); set => SetValue(SelectorColorProperty, value); }

        /// <summary>The bar's height in pixels; NaN, the default, is one row pitch.</summary>
        public double SelectorHeight { get => GetValue(SelectorHeightProperty); set => SetValue(SelectorHeightProperty, value); }

        /// <summary>How far the bar sits below the row's top, in pixels. 0 by default.</summary>
        public double SelectorOffsetY { get => GetValue(SelectorOffsetYProperty); set => SetValue(SelectorOffsetYProperty, value); }

        /// <summary>A rounded fill behind the selected row, drawn over the bar. Transparent by default.</summary>
        public Color SelectedBackgroundColor { get => GetValue(SelectedBackgroundColorProperty); set => SetValue(SelectedBackgroundColorProperty, value); }

        /// <summary>How far the selected background reaches beyond the row on the left and right, in pixels; top and bottom are ignored.</summary>
        public Thickness SelectedBackgroundMargins { get => GetValue(SelectedBackgroundMarginsProperty); set => SetValue(SelectedBackgroundMarginsProperty, value); }

        /// <summary>The selected background's corner radius in pixels. 0 by default.</summary>
        public double SelectedBackgroundCornerRadius { get => GetValue(SelectedBackgroundCornerRadiusProperty); set => SetValue(SelectedBackgroundCornerRadiusProperty, value); }

        /// <summary>Where each row's text sits across the width. Left by default.</summary>
        public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

        /// <summary>Space kept clear at the left and right of every row's text, in pixels. 0 by default.</summary>
        public double HorizontalMargin { get => GetValue(HorizontalMarginProperty); set => SetValue(HorizontalMarginProperty, value); }

        /// <summary>The casing applied to every row. None by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The distance from one row's top to the next, FontSize times LineSpacing to a hundredth.</summary>
        public double RowPitch => Math.Round(FontSize * LineSpacing * 100) / 100;

        /// <summary>How many whole rows fit in the height.</summary>
        public int VisibleRows => RowPitch <= 0 ? 0 : Math.Max(1, (int)Math.Floor((Bounds.Height + 0.01) / RowPitch));

        /// <summary>The first row drawn: the selection kept at the middle row once the list is longer than its window, clamped at both ends.</summary>
        public int FirstVisible
        {
            get
            {
                int count = Items?.Count ?? 0, visible = VisibleRows;
                if (count <= visible) return 0;
                return Math.Clamp(SelectedIndex - (visible - 1) / 2, 0, count - visible);
            }
        }

        /// <summary>A row's box in the control's coordinates, whether or not it is drawn.</summary>
        /// <param name="index">The row's index in Items.</param>
        /// <returns>The row's rectangle at the current scroll; rows above the window have negative tops.</returns>
        public Rect RowRect(int index) => new(0, (index - FirstVisible) * RowPitch, Bounds.Width, RowPitch);

        public override void Render(DrawingContext context)
        {
            IReadOnlyList<TextRow> items = Items ?? Array.Empty<TextRow>();
            if (items.Count == 0 || RowPitch <= 0) return;
            var bounds = new Rect(Bounds.Size);
            Thickness reach = SelectedBackgroundMargins;
            using DrawingContext.PushedState clip = context.PushClip(new Rect(-reach.Left, 0, bounds.Width + reach.Left + reach.Right, bounds.Height));
            GlyphTypeface typeface = FontPath is { Length: > 0 } p && FontFiles.Load(p) is { } loaded ? loaded : FontFiles.Default;
            int first = FirstVisible, last = Math.Min(items.Count, first + VisibleRows);

            if (SelectedIndex >= first && SelectedIndex < last)
            {
                Rect row = RowRect(SelectedIndex);
                double h = double.IsNaN(SelectorHeight) ? RowPitch : SelectorHeight;
                if (SelectorColor.A > 0) context.FillRectangle(new ImmutableSolidColorBrush(SelectorColor), new Rect(0, row.Y + SelectorOffsetY, bounds.Width, h));
                if (SelectedBackgroundColor.A > 0)
                {
                    Thickness m = SelectedBackgroundMargins;
                    var back = new Rect(row.X - m.Left, row.Y, row.Width + m.Left + m.Right, row.Height);
                    context.DrawRectangle(new ImmutableSolidColorBrush(SelectedBackgroundColor), null, back, SelectedBackgroundCornerRadius, SelectedBackgroundCornerRadius);
                }
            }

            for (int i = first; i < last; i++)
            {
                TextRow item = items[i];
                bool selected = i == SelectedIndex;
                Color colour = selected ? (item.Secondary ? SelectedSecondaryColor ?? SelectedColor : SelectedColor) : item.Secondary ? SecondaryColor : PrimaryColor;
                Rect row = RowRect(i);
                Rect box = new(row.X + HorizontalMargin, row.Y, Math.Max(0, row.Width - 2 * HorizontalMargin), row.Height);
                FontLayout layout = FontLayout.Create(typeface, FontSize, FontLayout.Cased(item.Text, LetterCase), LineSpacing, box.Width, double.PositiveInfinity, false, "…");
                layout.Draw(context, new ImmutableSolidColorBrush(colour), box, TextAlignment, 0.5);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.List, status: () => Items is { } items && SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex].Text : null);
    }
}
