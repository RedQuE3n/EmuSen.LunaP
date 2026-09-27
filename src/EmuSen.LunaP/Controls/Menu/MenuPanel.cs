using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    // A big-screen menu's panel: wide, centred, rounded, a large upper-case title, the rows, a footer line, and the help bar at the foot of the screen - see docs/LunaP.md §181.2.
    /// <summary>A big-screen menu: a wide rounded panel centred in its area, with a large title, the rows as its child, a footer line, and a help bar at the bottom of the area.</summary>
    public class MenuPanel : Decorator
    {
        /// <summary>The factor every design size of a menu is multiplied by, inherited, so a host sets it once for everything under it.</summary>
        public static readonly AttachedProperty<double> ScaleProperty = AvaloniaProperty.RegisterAttached<MenuPanel, Control, double>("Scale", 1, inherits: true);

        /// <summary>The font file a menu is drawn in, inherited, so a host sets it once for everything under it.</summary>
        public static readonly AttachedProperty<string?> FontPathProperty = AvaloniaProperty.RegisterAttached<MenuPanel, Control, string?>("FontPath", inherits: true);

        public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<MenuPanel, string?>(nameof(Title));
        public static readonly StyledProperty<string?> FooterProperty = AvaloniaProperty.Register<MenuPanel, string?>(nameof(Footer));
        public static readonly StyledProperty<double> TitleSizeProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(TitleSize), 68);
        public static readonly StyledProperty<double> FooterSizeProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(FooterSize), 26);
        public static readonly StyledProperty<double> WidthFractionProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(WidthFraction), 0.66);
        public static readonly StyledProperty<double> MaxWidthToHeightProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(MaxWidthToHeight), 1.05);
        public static readonly StyledProperty<double> PanelCornerRadiusProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(PanelCornerRadius), 16);
        public static readonly StyledProperty<Color> PanelColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(PanelColor), Color.FromRgb(0x1B, 0x1B, 0x1E));
        public static readonly StyledProperty<Color> TitleColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(TitleColor), Color.FromRgb(0xB4, 0xB4, 0xB8));
        public static readonly StyledProperty<Color> FooterColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(FooterColor), Color.FromRgb(0x86, 0x86, 0x8A));
        public static readonly StyledProperty<Color> RuleColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(RuleColor), Color.FromRgb(0x33, 0x33, 0x37));
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<MenuPanel, LetterCase>(nameof(LetterCase), LetterCase.Upper);
        public static readonly StyledProperty<IReadOnlyList<HintEntry>?> HintsProperty = AvaloniaProperty.Register<MenuPanel, IReadOnlyList<HintEntry>?>(nameof(Hints));
        public static readonly StyledProperty<PadFamily> HintFamilyProperty = AvaloniaProperty.Register<MenuPanel, PadFamily>(nameof(HintFamily));
        public static readonly StyledProperty<Color> HintColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(HintColor), Color.FromRgb(0xD2, 0xD2, 0xD6));
        public static readonly StyledProperty<double> RowPitchProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(RowPitch), 54);
        public static readonly StyledProperty<Color> HintBackgroundProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(HintBackground), Color.FromArgb(0xF0, 0x16, 0x16, 0x18));

        // Design sizes before Scale: the title's band, the footer's band (or the padding under the rows without one), the margins, and the help bar's text.
        private const double TitleBand = 100, FooterBand = 78, BareFooter = 20, Edge = 24, HintText = 26, HintGap = 10;

        private readonly HintBar _hints = new() { LetterCase = LetterCase.Upper };

        static MenuPanel()
        {
            AffectsMeasure<MenuPanel>(ScaleProperty, TitleProperty, FooterProperty, WidthFractionProperty, MaxWidthToHeightProperty, HintsProperty, RowPitchProperty);
            AffectsRender<MenuPanel>(FontPathProperty, TitleSizeProperty, FooterSizeProperty, PanelCornerRadiusProperty, PanelColorProperty, TitleColorProperty,
                FooterColorProperty, RuleColorProperty, LetterCaseProperty);
        }

        /// <summary>An empty panel, its help bar hidden until Hints are given.</summary>
        public MenuPanel()
        {
            _hints.IsVisible = false;
            VisualChildren.Add(_hints);
            LogicalChildren.Add(_hints);
        }

        /// <summary>Reads the inherited scale a host set on an element.</summary>
        /// <param name="element">The element.</param>
        /// <returns>Its scale, 1 unless set on it or an ancestor.</returns>
        public static double GetScale(Control element) => element.GetValue(ScaleProperty);

        /// <summary>Sets the scale for an element and everything under it.</summary>
        /// <param name="element">The element, typically a window.</param>
        /// <param name="value">The factor, such as the height over 800.</param>
        public static void SetScale(Control element, double value) => element.SetValue(ScaleProperty, value);

        /// <summary>Reads the inherited font file a host set on an element.</summary>
        /// <param name="element">The element.</param>
        /// <returns>The path, or null for the default typeface.</returns>
        public static string? GetFontPath(Control element) => element.GetValue(FontPathProperty);

        /// <summary>Sets the font file for an element and everything under it.</summary>
        /// <param name="element">The element, typically a window.</param>
        /// <param name="value">A font file, or null for the default typeface.</param>
        public static void SetFontPath(Control element, string? value) => element.SetValue(FontPathProperty, value);

        /// <summary>The large title above the rows, cased by LetterCase. Null shows none, and its band stays.</summary>
        public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

        /// <summary>A short line under the rows, such as a version. Null shows none and shortens the panel.</summary>
        public string? Footer { get => GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

        /// <summary>The title's em size in design pixels before Scale. 68 by default.</summary>
        public double TitleSize { get => GetValue(TitleSizeProperty); set => SetValue(TitleSizeProperty, value); }

        /// <summary>The footer's em size in design pixels before Scale. 26 by default.</summary>
        public double FooterSize { get => GetValue(FooterSizeProperty); set => SetValue(FooterSizeProperty, value); }

        /// <summary>The panel's width as a fraction of the area's. 0.66 by default.</summary>
        public double WidthFraction { get => GetValue(WidthFractionProperty); set => SetValue(WidthFractionProperty, value); }

        /// <summary>The most the panel's width may be, as a multiple of the area's height, so a wide screen does not stretch it. 1.05 by default.</summary>
        public double MaxWidthToHeight { get => GetValue(MaxWidthToHeightProperty); set => SetValue(MaxWidthToHeightProperty, value); }

        /// <summary>The panel's corner radius in design pixels before Scale. 16 by default.</summary>
        public double PanelCornerRadius { get => GetValue(PanelCornerRadiusProperty); set => SetValue(PanelCornerRadiusProperty, value); }

        /// <summary>The panel's fill. #FF1B1B1E, a near black grey, by default.</summary>
        public Color PanelColor { get => GetValue(PanelColorProperty); set => SetValue(PanelColorProperty, value); }

        /// <summary>The title's colour. #FFB4B4B8, a light grey, by default.</summary>
        public Color TitleColor { get => GetValue(TitleColorProperty); set => SetValue(TitleColorProperty, value); }

        /// <summary>The footer's colour. #FF86868A, a mid grey, by default.</summary>
        public Color FooterColor { get => GetValue(FooterColorProperty); set => SetValue(FooterColorProperty, value); }

        /// <summary>The colour of the rule under the title. #FF333337, the rows' dark grey, by default.</summary>
        public Color RuleColor { get => GetValue(RuleColorProperty); set => SetValue(RuleColorProperty, value); }

        /// <summary>The casing of the title and the footer. Upper by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The help bar's entries, drawn upper case at the bottom centre of the area. Null or empty hides it.</summary>
        public IReadOnlyList<HintEntry>? Hints { get => GetValue(HintsProperty); set => SetValue(HintsProperty, value); }

        /// <summary>The pad family the help bar draws its buttons in. Generic by default.</summary>
        public PadFamily HintFamily { get => GetValue(HintFamilyProperty); set => SetValue(HintFamilyProperty, value); }

        /// <summary>The help bar's text and icon colour. #FFD2D2D6, a light grey, by default.</summary>
        public Color HintColor { get => GetValue(HintColorProperty); set => SetValue(HintColorProperty, value); }

        /// <summary>The help bar's fill. #F0161618, a near black grey almost opaque, by default.</summary>
        public Color HintBackground { get => GetValue(HintBackgroundProperty); set => SetValue(HintBackgroundProperty, value); }

        /// <summary>The height of one row in design pixels before Scale; rows that do not all fit are shown in whole rows of it. 54 by default, MenuRow's own.</summary>
        public double RowPitch { get => GetValue(RowPitchProperty); set => SetValue(RowPitchProperty, value); }

        /// <summary>The panel's rectangle in this control's coordinates, as last arranged.</summary>
        public Rect PanelBounds { get; private set; }

        /// <summary>The title band's rectangle in this control's coordinates, as last arranged; the rule is its bottom edge.</summary>
        public Rect TitleBounds { get; private set; }

        /// <summary>The help bar, which a host may read but should configure through Hints and the colours.</summary>
        public HintBar HelpBar => _hints;

        private double Unit => double.IsFinite(GetValue(ScaleProperty)) && GetValue(ScaleProperty) > 0 ? GetValue(ScaleProperty) : 1;

        private double BottomBand => (Footer is { Length: > 0 } ? FooterBand : BareFooter) * Unit;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HintsProperty || change.Property == HintFamilyProperty || change.Property == HintColorProperty || change.Property == HintBackgroundProperty
                || change.Property == ScaleProperty || change.Property == FontPathProperty)
                ConfigureHints();
        }

        private void ConfigureHints()
        {
            double u = Unit;
            _hints.Entries = Hints;
            _hints.IsVisible = Hints is { Count: > 0 };
            _hints.PadFamily = HintFamily;
            _hints.FontPath = GetValue(FontPathProperty);
            _hints.FontSize = HintText * u;
            _hints.IconColor = HintColor;
            _hints.TextColor = HintColor;
            _hints.BackgroundColor = HintBackground;
            _hints.BackgroundCornerRadius = 8 * u;
            _hints.Padding = new Thickness(14 * u, 8 * u);
            _hints.EntrySpacing = 18 * u;
            _hints.IconTextSpacing = 6 * u;
        }

        // The panel's width and the most its rows may take, for an area of this size.
        private (double Width, double RowsMax) Frame(Size area, double hintHeight)
        {
            double u = Unit;
            double width = Math.Max(0, Math.Min(Math.Min(area.Width * WidthFraction, area.Height * MaxWidthToHeight), area.Width - 2 * Edge * u));
            double rowsMax = Math.Max(0, area.Height - hintHeight - 2 * Edge * u - TitleBand * u - BottomBand);
            // Rows that do not all fit scroll, and the last one shown is never cut through.
            double pitch = RowPitch * u;
            if (pitch > 0 && rowsMax >= pitch) rowsMax = Math.Floor((rowsMax + 0.01) / pitch) * pitch;
            return (width, rowsMax);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            ConfigureHints();
            _hints.Measure(Size.Infinity);
            double hintHeight = _hints.IsVisible ? _hints.DesiredSize.Height + HintGap * Unit : 0;
            Size area = new(double.IsFinite(availableSize.Width) ? availableSize.Width : 1280 * Unit, double.IsFinite(availableSize.Height) ? availableSize.Height : 800 * Unit);
            (double width, double rowsMax) = Frame(area, hintHeight);
            Child?.Measure(new Size(width, rowsMax));
            return area;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double u = Unit;
            double hintHeight = _hints.IsVisible ? _hints.DesiredSize.Height + HintGap * u : 0;
            (double width, double rowsMax) = Frame(finalSize, hintHeight);
            double rows = Math.Min(Child?.DesiredSize.Height ?? 0, rowsMax);
            double height = TitleBand * u + rows + BottomBand;
            double top = Math.Clamp((finalSize.Height - height) / 2, Edge * u, Math.Max(Edge * u, finalSize.Height - hintHeight - Edge * u - height));
            double left = (finalSize.Width - width) / 2;
            PanelBounds = new Rect(left, top, width, height);
            TitleBounds = new Rect(left, top, width, TitleBand * u);
            Child?.Arrange(new Rect(left, top + TitleBand * u, width, rows));
            if (_hints.IsVisible)
            {
                Size hs = _hints.DesiredSize;
                _hints.Arrange(new Rect((finalSize.Width - hs.Width) / 2, finalSize.Height - hs.Height - HintGap * u, hs.Width, hs.Height));
            }
            return finalSize;
        }

        public override void Render(DrawingContext context)
        {
            double u = Unit;
            Rect panel = PanelBounds;
            if (panel.Width <= 0) return;
            double radius = PanelCornerRadius * u;
            context.DrawRectangle(new ImmutableSolidColorBrush(PanelColor), null, panel, radius, radius);
            double rule = Math.Max(1, Math.Round(u));
            context.FillRectangle(new ImmutableSolidColorBrush(RuleColor), new Rect(panel.X, TitleBounds.Bottom - rule, panel.Width, rule));

            GlyphTypeface typeface = GetValue(FontPathProperty) is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;
            if (Title is { Length: > 0 } title)
            {
                double size = TitleSize * u;
                Rect band = TitleBounds.Deflate(new Thickness(24 * u, 0));
                MenuRow.DrawLine(context, typeface, size, FontLayout.Cased(title, LetterCase), band, band.Center.Y + MenuRow.CapHeight(typeface, size) / 2, TextAlignment.Center, new ImmutableSolidColorBrush(TitleColor));
            }
            if (Footer is { Length: > 0 } footer)
            {
                double size = FooterSize * u;
                var band = new Rect(panel.X + 24 * u, panel.Bottom - FooterBand * u, panel.Width - 48 * u, FooterBand * u);
                MenuRow.DrawLine(context, typeface, size, FontLayout.Cased(footer, LetterCase), band, band.Center.Y + MenuRow.CapHeight(typeface, size) / 2, TextAlignment.Center, new ImmutableSolidColorBrush(FooterColor));
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Menu, () => Title ?? "");
    }
}
