using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>What a big-screen menu row offers beside its label: nothing, a way into another screen, a value stepped sideways, or a switch.</summary>
    public enum MenuRowKind
    {
        /// <summary>A row that does something when chosen; its value, if any, is shown plainly.</summary>
        Action,
        /// <summary>A row that opens another screen, marked with a chevron at its right.</summary>
        Submenu,
        /// <summary>A row whose value Left and Right step, drawn between two arrows.</summary>
        Option,
        /// <summary>A row that turns something on or off, drawn as a switch.</summary>
        Switch,
    }

    /// <summary>Where each part of a MenuRow is drawn, in the row's coordinates; an absent part is an empty rectangle.</summary>
    /// <param name="Bar">The highlight bar, the row's full width above its rule.</param>
    /// <param name="Label">The label's box.</param>
    /// <param name="Value">The value's box.</param>
    /// <param name="LeftArrow">An option row's left arrow.</param>
    /// <param name="RightArrow">An option row's right arrow.</param>
    /// <param name="Chevron">A submenu row's chevron.</param>
    /// <param name="Switch">A switch row's switch.</param>
    /// <param name="Rule">The thin rule along the row's bottom edge.</param>
    public readonly record struct MenuRowLayout(Rect Bar, Rect Label, Rect Value, Rect LeftArrow, Rect RightArrow, Rect Chevron, Rect Switch, Rect Rule);

    // One row of a big-screen menu, drawn: an upper-case label, a right-aligned value, arrows, a chevron or a switch, a rule, and a full-width bar when highlighted - see docs/LunaP.md §181.1.
    /// <summary>One row of a big-screen menu, drawn in a typeface from a file: label left, value right, an option's arrows, a submenu's chevron or a switch, a rule beneath and a full-width bar when highlighted.</summary>
    public class MenuRow : Control
    {
        public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MenuRow, string?>(nameof(Label));
        public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MenuRow, string?>(nameof(Value));
        public static readonly StyledProperty<MenuRowKind> KindProperty = AvaloniaProperty.Register<MenuRow, MenuRowKind>(nameof(Kind));
        public static readonly StyledProperty<bool> IsOnProperty = AvaloniaProperty.Register<MenuRow, bool>(nameof(IsOn));
        public static readonly StyledProperty<bool> IsHighlightedProperty = AvaloniaProperty.Register<MenuRow, bool>(nameof(IsHighlighted));
        public static readonly StyledProperty<double> ScaleProperty = MenuPanel.ScaleProperty.AddOwner<MenuRow>();
        public static readonly StyledProperty<string?> FontPathProperty = MenuPanel.FontPathProperty.AddOwner<MenuRow>();
        public static readonly StyledProperty<double> RowHeightProperty = AvaloniaProperty.Register<MenuRow, double>(nameof(RowHeight), 54);
        public static readonly StyledProperty<double> TextSizeProperty = AvaloniaProperty.Register<MenuRow, double>(nameof(TextSize), 36);
        public static readonly StyledProperty<Color> TextColorProperty = AvaloniaProperty.Register<MenuRow, Color>(nameof(TextColor), Color.FromRgb(0x9C, 0x9C, 0xA0));
        public static readonly StyledProperty<Color> HighlightTextColorProperty = AvaloniaProperty.Register<MenuRow, Color>(nameof(HighlightTextColor), Color.FromRgb(0xEE, 0xEE, 0xF0));
        public static readonly StyledProperty<Color> BarColorProperty = AvaloniaProperty.Register<MenuRow, Color>(nameof(BarColor), Color.FromRgb(0x05, 0x05, 0x07));
        public static readonly StyledProperty<Color> RuleColorProperty = AvaloniaProperty.Register<MenuRow, Color>(nameof(RuleColor), Color.FromRgb(0x33, 0x33, 0x37));
        public static readonly StyledProperty<Color> AccentColorProperty = AvaloniaProperty.Register<MenuRow, Color>(nameof(AccentColor), Color.FromRgb(0x4C, 0x9A, 0xE8));
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<MenuRow, LetterCase>(nameof(LetterCase), LetterCase.Upper);
        public static readonly StyledProperty<Color?> ValueColorProperty = AvaloniaProperty.Register<MenuRow, Color?>(nameof(ValueColor));
        public static readonly StyledProperty<LetterCase?> ValueLetterCaseProperty = AvaloniaProperty.Register<MenuRow, LetterCase?>(nameof(ValueLetterCase));

        // Width at the right a MenuFieldRow keeps for the control it hosts, so the label is cut short of it - see docs/LunaP.md §182.3.
        internal static readonly StyledProperty<double> TrailingWidthProperty = AvaloniaProperty.Register<MenuRow, double>("TrailingWidth");

        // A template's label from the control's own attached one, which wins over its content - see MenuRows.
        internal static readonly StyledProperty<string?> LabelOverrideProperty = AvaloniaProperty.Register<MenuRow, string?>("LabelOverride");

        static MenuRow()
        {
            AffectsMeasure<MenuRow>(ScaleProperty, RowHeightProperty);
            AffectsRender<MenuRow>(LabelProperty, ValueProperty, KindProperty, IsOnProperty, IsHighlightedProperty, FontPathProperty, TextSizeProperty, TextColorProperty,
                HighlightTextColorProperty, BarColorProperty, RuleColorProperty, AccentColorProperty, LetterCaseProperty, LabelOverrideProperty, ValueColorProperty,
                ValueLetterCaseProperty, TrailingWidthProperty);
        }

        /// <summary>The words at the row's left, cased by LetterCase.</summary>
        public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

        /// <summary>The value at the row's right: plain for an action or submenu, between arrows for an option. Null shows none.</summary>
        public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        /// <summary>What the row offers beside its label. Action by default.</summary>
        public MenuRowKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

        /// <summary>Whether a switch row's switch is on. False by default.</summary>
        public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

        /// <summary>Whether the row is the one chosen, drawn as a bar across its full width. False by default.</summary>
        public bool IsHighlighted { get => GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }

        /// <summary>The factor every design size is multiplied by, inherited from MenuPanel.Scale. 1 by default.</summary>
        public double Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

        /// <summary>The font file the row is drawn in, inherited from MenuPanel.FontPath; null for the application's default typeface.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The row's height in design pixels before Scale, its rule included. 54 by default.</summary>
        public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

        /// <summary>The label's em size in design pixels before Scale. 36 by default.</summary>
        public double TextSize { get => GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }

        /// <summary>The colour of the text and the drawn marks. #FF9C9CA0, a light grey, by default.</summary>
        public Color TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

        /// <summary>The colour of the text and the marks on the highlighted row. #FFEEEEF0, near white, by default.</summary>
        public Color HighlightTextColor { get => GetValue(HighlightTextColorProperty); set => SetValue(HighlightTextColorProperty, value); }

        /// <summary>The highlight bar's colour. #FF050507, near black, by default.</summary>
        public Color BarColor { get => GetValue(BarColorProperty); set => SetValue(BarColorProperty, value); }

        /// <summary>The rule's colour. #FF333337, a dark grey, by default.</summary>
        public Color RuleColor { get => GetValue(RuleColorProperty); set => SetValue(RuleColorProperty, value); }

        /// <summary>The fill of a switch that is on. #FF4C9AE8, a mid blue, by default.</summary>
        public Color AccentColor { get => GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

        /// <summary>The casing of the label and the value. Upper by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The value's colour on either state of the row, such as one that marks where the value came from; null draws it as the label is drawn. Null by default.</summary>
        public Color? ValueColor { get => GetValue(ValueColorProperty); set => SetValue(ValueColorProperty, value); }

        /// <summary>The casing of the value alone, such as None for a name that must read as written; null follows LetterCase. Null by default.</summary>
        public LetterCase? ValueLetterCase { get => GetValue(ValueLetterCaseProperty); set => SetValue(ValueLetterCaseProperty, value); }

        private double Unit => double.IsFinite(Scale) && Scale > 0 ? Scale : 1;

        private string Text => FontLayout.Cased(GetValue(LabelOverrideProperty) ?? Label ?? "", LetterCase);

        // One line: a value with line breaks is shown with spaces for them.
        private string ValueText => FontLayout.Cased((Value ?? "").ReplaceLineEndings(" "), ValueLetterCase ?? LetterCase);

        internal GlyphTypeface Typeface() => FontPath is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;

        protected override Size MeasureOverride(Size availableSize) => new(0, RowHeight * Unit);

        /// <summary>Where each part is drawn for a row of a given size.</summary>
        /// <param name="size">The row's size; its width decides where the right-hand parts go.</param>
        /// <returns>The boxes, an absent part's empty.</returns>
        public MenuRowLayout Layout(Size size)
        {
            double u = Unit, w = size.Width, h = size.Height;
            double rule = RuleThickness(u);
            double pad = 8 * u, gap = 8 * u, arrowGap = 11 * u;
            double mark = CapHeight(Typeface(), TextSize * u);
            double mid = (h - rule) / 2;
            double right = w - pad - Math.Max(0, GetValue(TrailingWidthProperty));
            if (GetValue(TrailingWidthProperty) > 0) right -= gap;
            Rect chevron = default, rightArrow = default, leftArrow = default, toggle = default, value = default;

            if (Kind == MenuRowKind.Submenu)
            {
                double cw = mark * 0.55;
                chevron = new Rect(right - cw, mid - mark / 2, cw, mark);
                right = chevron.Left - gap;
            }
            else if (Kind == MenuRowKind.Switch)
            {
                double sh = mark * 0.95, sw = sh * 2.1;
                toggle = new Rect(right - sw, mid - sh / 2, sw, sh);
                right = toggle.Left - gap;
            }
            else if (Kind == MenuRowKind.Option)
            {
                double ah = mark * 0.85, aw = ah * 0.6;
                rightArrow = new Rect(right - aw, mid - ah / 2, aw, ah);
                right = rightArrow.Left - arrowGap;
            }

            string valueText = ValueText;
            if (valueText.Length > 0 && Kind != MenuRowKind.Switch)
            {
                double vw = Math.Min(FontLayout.Measure(Typeface(), TextSize * u, valueText), Math.Max(0, (right - pad) / 2));
                value = new Rect(right - vw, 0, vw, h - rule);
                right = value.Left - (Kind == MenuRowKind.Option ? arrowGap : gap);
            }

            if (Kind == MenuRowKind.Option)
            {
                double ah = rightArrow.Height, aw = rightArrow.Width;
                leftArrow = new Rect(right - aw, mid - ah / 2, aw, ah);
                right = leftArrow.Left - gap;
            }

            var label = new Rect(pad, 0, Math.Max(0, right - pad), h - rule);
            return new MenuRowLayout(new Rect(0, 0, w, Math.Max(0, h - rule)), label, value, leftArrow, rightArrow, chevron, toggle, new Rect(0, h - rule, w, rule));
        }

        // A hairline that stays one device pixel or more at every scale.
        private static double RuleThickness(double u) => Math.Max(1, Math.Round(u));

        // The height of a capital, read from the typeface's H, so upper-case text centres on the row.
        internal static double CapHeight(GlyphTypeface typeface, double size)
        {
            if (typeface.CharacterToGlyphMap.TryGetGlyph('H', out ushort glyph) && typeface.TryGetGlyphMetrics(glyph, out GlyphMetrics metrics) && metrics.Height != 0)
                return Math.Abs(metrics.Height) * size / typeface.Metrics.DesignEmHeight;
            return size * 0.7;
        }

        public override void Render(DrawingContext context)
        {
            MenuRowLayout at = Layout(Bounds.Size);
            double u = Unit;
            if (IsHighlighted) context.FillRectangle(new ImmutableSolidColorBrush(BarColor), at.Bar);
            context.FillRectangle(new ImmutableSolidColorBrush(RuleColor), at.Rule);

            Color ink = IsHighlighted ? HighlightTextColor : TextColor;
            var brush = new ImmutableSolidColorBrush(ink);
            GlyphTypeface typeface = Typeface();
            double size = TextSize * u;
            double baseline = at.Bar.Height / 2 + CapHeight(typeface, size) / 2;
            DrawLine(context, typeface, size, Text, at.Label, baseline, TextAlignment.Left, brush);
            if (at.Value.Width > 0) DrawLine(context, typeface, size, ValueText, at.Value, baseline, TextAlignment.Right, ValueColor is { } own ? new ImmutableSolidColorBrush(own) : brush);

            var pen = new ImmutablePen(brush, Math.Max(1, 1.6 * u), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            if (at.Chevron.Width > 0) Chevron(context, pen, at.Chevron);
            if (at.LeftArrow.Width > 0) Arrow(context, pen, at.LeftArrow, left: true);
            if (at.RightArrow.Width > 0) Arrow(context, pen, at.RightArrow, left: false);
            if (at.Switch.Width > 0) Switch(context, ink, at.Switch, u);
        }

        // One line at a baseline, cut with an ellipsis to its box.
        internal static void DrawLine(DrawingContext context, GlyphTypeface typeface, double size, string text, Rect box, double baseline, TextAlignment alignment, IBrush brush)
        {
            if (text.Length == 0 || box.Width <= 0) return;
            FontLayout line = FontLayout.Create(typeface, size, text, 1, box.Width, double.PositiveInfinity, false, "…");
            double top = baseline - line.Ascent - (line.LineHeight - (line.Ascent + line.Descent)) / 2;
            line.Draw(context, brush, new Rect(box.X, top, box.Width, line.Height), alignment, 0);
        }

        private static void Chevron(DrawingContext context, IPen pen, Rect box)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(box.TopLeft, false);
                c.LineTo(new Point(box.Right, box.Center.Y));
                c.LineTo(box.BottomLeft);
                c.EndFigure(false);
            }
            context.DrawGeometry(null, pen, g);
        }

        // An outlined triangle pointing the way the value steps.
        private static void Arrow(DrawingContext context, IPen pen, Rect box, bool left)
        {
            var g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(left ? box.TopRight : box.TopLeft, false);
                c.LineTo(new Point(left ? box.Left : box.Right, box.Center.Y));
                c.LineTo(left ? box.BottomRight : box.BottomLeft);
                c.EndFigure(true);
            }
            context.DrawGeometry(null, pen, g);
        }

        // A rounded track with its knob at the left when off, and filled with the accent and the knob at the right when on.
        private void Switch(DrawingContext context, Color ink, Rect box, double u)
        {
            double r = box.Height / 2;
            var pen = new ImmutablePen(new ImmutableSolidColorBrush(ink), Math.Max(1, 1.4 * u));
            IBrush? fill = IsOn ? new ImmutableSolidColorBrush(AccentColor) : null;
            context.DrawRectangle(fill, pen, box.Deflate(pen.Thickness / 2), r, r);
            double knob = r * 0.62;
            var centre = new Point(IsOn ? box.Right - r : box.Left + r, box.Center.Y);
            context.DrawEllipse(new ImmutableSolidColorBrush(IsOn ? HighlightTextColor : ink), null, centre, knob, knob);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ListItem, () => Describe(GetValue(LabelOverrideProperty) ?? Label, Value, Kind, IsOn));

        /// <summary>What a screen reader hears for a row: its label, then its value or its switch's state.</summary>
        /// <param name="label">The row's label.</param>
        /// <param name="value">Its value, or null.</param>
        /// <param name="kind">What it offers.</param>
        /// <param name="isOn">A switch's state.</param>
        /// <returns>The label alone, or the label and its value or state, comma separated.</returns>
        public static string Describe(string? label, string? value, MenuRowKind kind, bool isOn) =>
            kind == MenuRowKind.Switch ? $"{label}, {(isOn ? "on" : "off")}"
            : value is { Length: > 0 } ? $"{label}, {value}"
            : label ?? "";
    }
}
