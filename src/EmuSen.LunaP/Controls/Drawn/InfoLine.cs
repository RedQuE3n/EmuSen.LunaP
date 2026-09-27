using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A small picture drawn in an InfoLine beside its text, in the line's colour.</summary>
    public enum InfoIcon
    {
        /// <summary>No picture: the item is its text alone.</summary>
        None,
        /// <summary>A gamepad: a count of games.</summary>
        Gamepad,
        /// <summary>A five-pointed star: a count of favourites.</summary>
        Star,
        /// <summary>A funnel: a filtered count.</summary>
        Filter,
        /// <summary>An open folder: a folder has been entered.</summary>
        Folder,
    }

    /// <summary>One item of an InfoLine: a picture, then its text; either may be missing.</summary>
    /// <param name="Icon">The picture drawn before the text.</param>
    /// <param name="Text">The words after the picture.</param>
    public sealed record InfoItem(InfoIcon Icon, string? Text = null);

    // One line of pictures and numbers, laid out in the proportions the consumer measured from its reference - see docs/LunaP.md §193.
    /// <summary>A single line of items, each a small drawn picture followed by its text, in one typeface, size and colour, aligned in its box on an optional background.</summary>
    public class InfoLine : Control
    {
        public static readonly StyledProperty<IReadOnlyList<InfoItem>?> ItemsProperty = AvaloniaProperty.Register<InfoLine, IReadOnlyList<InfoItem>?>(nameof(Items));
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<InfoLine, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<InfoLine, double>(nameof(FontSize), 16);
        public static readonly StyledProperty<Color> ForegroundProperty = AvaloniaProperty.Register<InfoLine, Color>(nameof(Foreground), Colors.White);
        public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<InfoLine, Color>(nameof(BackgroundColor), Colors.Transparent);
        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty = AvaloniaProperty.Register<InfoLine, TextAlignment>(nameof(TextAlignment));
        public static readonly StyledProperty<VerticalAlignment> LineVerticalAlignmentProperty = AvaloniaProperty.Register<InfoLine, VerticalAlignment>(nameof(LineVerticalAlignment), VerticalAlignment.Center);
        public static readonly StyledProperty<double> LineSpacingProperty = AvaloniaProperty.Register<InfoLine, double>(nameof(LineSpacing), 1.5);

        static InfoLine()
        {
            AffectsMeasure<InfoLine>(ItemsProperty, FontPathProperty, FontSizeProperty, LineSpacingProperty);
            AffectsRender<InfoLine>(ForegroundProperty, BackgroundColorProperty, TextAlignmentProperty, LineVerticalAlignmentProperty);
        }

        /// <summary>The items, in reading order; with TextAlignment Right a folder item is drawn first, at the left.</summary>
        public IReadOnlyList<InfoItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

        /// <summary>The font file for the text; null or a missing file uses the toolkit's default typeface.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The em size in pixels, which the pictures are drawn in proportion to. 16 by default.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>The colour of the text and the pictures. White by default.</summary>
        public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        /// <summary>A fill behind the whole box. Transparent by default.</summary>
        public Color BackgroundColor { get => GetValue(BackgroundColorProperty); set => SetValue(BackgroundColorProperty, value); }

        /// <summary>Where the line sits across the box. Left by default.</summary>
        public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

        /// <summary>Where the line sits down the box. Center by default.</summary>
        public VerticalAlignment LineVerticalAlignment { get => GetValue(LineVerticalAlignmentProperty); set => SetValue(LineVerticalAlignmentProperty, value); }

        /// <summary>The line's height as a multiple of FontSize, which the control measures to when its box gives no height. 1.5 by default.</summary>
        public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }

        private GlyphTypeface Typeface() => FontPath is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;

        // Each picture's width and its gaps, in ems: after a picture before its text, and before a picture that follows text.
        private static double IconWidth(InfoIcon icon) => icon switch { InfoIcon.Gamepad => 1.06, InfoIcon.Star => 0.92, InfoIcon.Filter => 0.78, InfoIcon.Folder => 1.06, _ => 0 };

        private const double AfterIcon = 0.3, BetweenItems = 0.47;

        // The items in drawing order, each with its text's width; a right-aligned line puts the folder first.
        private IReadOnlyList<(InfoItem Item, double TextWidth)> Laid()
        {
            GlyphTypeface face = Typeface();
            IEnumerable<InfoItem> items = Items ?? [];
            if (TextAlignment is TextAlignment.Right or TextAlignment.End)
                items = items.Where(i => i.Icon == InfoIcon.Folder).Concat(items.Where(i => i.Icon != InfoIcon.Folder));
            return items.Select(i => (i, string.IsNullOrEmpty(i.Text) ? 0 : FontLayout.Measure(face, FontSize, i.Text!))).ToList();
        }

        /// <summary>The width the items take in pixels at the current font and size.</summary>
        public double LineWidth
        {
            get
            {
                IReadOnlyList<(InfoItem Item, double TextWidth)> laid = Laid();
                double w = 0;
                for (int i = 0; i < laid.Count; i++)
                {
                    (InfoItem item, double text) = laid[i];
                    if (i > 0) w += BetweenItems * FontSize;
                    w += IconWidth(item.Icon) * FontSize + (item.Icon != InfoIcon.None && text > 0 ? AfterIcon * FontSize : 0) + text;
                }

                return w;
            }
        }

        protected override Size MeasureOverride(Size availableSize) => new(LineWidth, LineSpacing * FontSize);

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            if (BackgroundColor.A > 0) context.FillRectangle(new ImmutableSolidColorBrush(BackgroundColor), bounds);
            IReadOnlyList<(InfoItem Item, double TextWidth)> laid = Laid();
            if (laid.Count == 0) return;
            GlyphTypeface face = Typeface();
            double em = FontSize, width = LineWidth, line = LineSpacing * em;
            double x = TextAlignment switch
            {
                TextAlignment.Center => (bounds.Width - width) / 2,
                TextAlignment.Right or TextAlignment.End => bounds.Width - width,
                _ => 0,
            };
            double top = LineVerticalAlignment switch { VerticalAlignment.Top => 0, VerticalAlignment.Bottom => bounds.Height - line, _ => (bounds.Height - line) / 2 };
            FontLayout probe = FontLayout.Create(face, em, "0", LineSpacing, double.PositiveInfinity, double.PositiveInfinity, false, null);
            double baseline = top + (line - (probe.Ascent + probe.Descent)) / 2 + probe.Ascent;
            var brush = new ImmutableSolidColorBrush(Foreground);
            for (int i = 0; i < laid.Count; i++)
            {
                (InfoItem item, double text) = laid[i];
                if (i > 0) x += BetweenItems * em;
                if (item.Icon != InfoIcon.None)
                {
                    DrawIcon(context, item.Icon, new Point(x, baseline), em, brush);
                    x += IconWidth(item.Icon) * em + (text > 0 ? AfterIcon * em : 0);
                }

                if (text > 0)
                {
                    FontLayout.Create(face, em, item.Text!, LineSpacing, double.PositiveInfinity, double.PositiveInfinity, false, null)
                        .Draw(context, brush, new Rect(x, top, text + 1, line), TextAlignment.Left, 0);
                    x += text;
                }
            }
        }

        // Each picture from its left edge on the text's baseline, in ems.
        private static void DrawIcon(DrawingContext dc, InfoIcon icon, Point at, double em, IBrush brush)
        {
            Point P(double fx, double fy) => new(at.X + fx * em, at.Y + fy * em);
            switch (icon)
            {
                case InfoIcon.Gamepad:
                {
                    // A rounded body 1.06 by 0.58 ems standing on the baseline, a cross cut out on the left and two buttons on the right.
                    Geometry body = new RectangleGeometry(new Rect(P(0, -0.58), P(1.06, 0)), 0.29 * em, 0.29 * em);
                    var holes = new GeometryGroup { FillRule = FillRule.NonZero };
                    holes.Children.Add(new RectangleGeometry(new Rect(P(0.2, -0.33), P(0.42, -0.25))));
                    holes.Children.Add(new RectangleGeometry(new Rect(P(0.27, -0.4), P(0.35, -0.18))));
                    holes.Children.Add(new EllipseGeometry(new Rect(P(0.66, -0.4), P(0.76, -0.3))));
                    holes.Children.Add(new EllipseGeometry(new Rect(P(0.76, -0.28), P(0.86, -0.18))));
                    dc.DrawGeometry(brush, null, new CombinedGeometry(GeometryCombineMode.Exclude, body, holes));
                    break;
                }
                case InfoIcon.Star:
                {
                    // Five points 0.48 ems from a centre 0.35 ems above the baseline, the inner corners at half that, as the reference's star measured.
                    var star = new StreamGeometry();
                    using (StreamGeometryContext g = star.Open())
                    {
                        Point c = P(0.46, -0.35);
                        for (int k = 0; k < 10; k++)
                        {
                            double r = (k % 2 == 0 ? 0.48 : 0.24) * em, a = -Math.PI / 2 + k * Math.PI / 5;
                            var p = new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
                            if (k == 0) g.BeginFigure(p, true);
                            else g.LineTo(p);
                        }

                        g.EndFigure(true);
                    }

                    dc.DrawGeometry(brush, null, star);
                    break;
                }
                case InfoIcon.Filter:
                {
                    // A funnel 0.78 ems wide: a wide top narrowing to a stem that ends just below the baseline.
                    var funnel = new StreamGeometry();
                    using (StreamGeometryContext g = funnel.Open())
                    {
                        g.BeginFigure(P(0, -0.72), true);
                        foreach (Point p in new[] { P(0.78, -0.72), P(0.47, -0.36), P(0.47, 0.06), P(0.31, -0.04), P(0.31, -0.36) }) g.LineTo(p);
                        g.EndFigure(true);
                    }

                    dc.DrawGeometry(brush, null, funnel);
                    break;
                }
                case InfoIcon.Folder:
                {
                    // An open folder 1.06 ems wide and 0.78 tall: the back with its tab, and the front leaning forward, a gap between.
                    var back = new StreamGeometry();
                    using (StreamGeometryContext g = back.Open())
                    {
                        g.BeginFigure(P(0, -0.78), true);
                        foreach (Point p in new[] { P(0.36, -0.78), P(0.44, -0.68), P(0.86, -0.68), P(0.86, -0.46), P(0.26, -0.46), P(0.09, -0.08), P(0, -0.08) }) g.LineTo(p);
                        g.EndFigure(true);
                    }

                    var front = new StreamGeometry();
                    using (StreamGeometryContext g = front.Open())
                    {
                        g.BeginFigure(P(0.3, -0.39), true);
                        foreach (Point p in new[] { P(1.06, -0.39), P(0.82, 0), P(0.05, 0) }) g.LineTo(p);
                        g.EndFigure(true);
                    }

                    dc.DrawGeometry(brush, null, back);
                    dc.DrawGeometry(brush, null, front);
                    break;
                }
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Text, () => string.Join(", ", (Items ?? []).Select(i => i.Text).Where(t => !string.IsNullOrEmpty(t))));
    }
}
