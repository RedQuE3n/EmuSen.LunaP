using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>Which ways a menu's rows run past its panel, as its scroll indicator shows.</summary>
    public enum MenuScrollIndicator
    {
        /// <summary>Every row shows, or there is no indicator.</summary>
        None,
        /// <summary>More rows below: the lower chevrons alone.</summary>
        Down,
        /// <summary>More rows above: the upper chevrons alone.</summary>
        Up,
        /// <summary>More rows above and below: both pairs.</summary>
        Both,
    }

    /// <summary>Where a menu's panel stands in its area.</summary>
    public enum MenuAnchor
    {
        /// <summary>Centred, as ES-DE's menus are.</summary>
        Centre,
        /// <summary>At the area's left edge, leaving the room on its right to the pane, as a card over a paused game.</summary>
        Left,
        /// <summary>The full height of the area at its left edge, the design's card: the title row, rows and pane inside the safe area and the help bar a band across the foot - see docs/LunaP.md §210.</summary>
        Edge,
    }

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
        public static readonly StyledProperty<Color> TitleColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(TitleColor), Color.FromRgb(0xEE, 0xEE, 0xF0));
        public static readonly StyledProperty<Color> FooterColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(FooterColor), Color.FromRgb(0x8D, 0x8D, 0x91));
        public static readonly StyledProperty<Color> RuleColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(RuleColor), Color.FromRgb(0x33, 0x33, 0x37));
        public static readonly StyledProperty<LetterCase> LetterCaseProperty = AvaloniaProperty.Register<MenuPanel, LetterCase>(nameof(LetterCase), LetterCase.Upper);
        public static readonly StyledProperty<IReadOnlyList<HintEntry>?> HintsProperty = AvaloniaProperty.Register<MenuPanel, IReadOnlyList<HintEntry>?>(nameof(Hints));
        public static readonly StyledProperty<PadFamily> HintFamilyProperty = AvaloniaProperty.Register<MenuPanel, PadFamily>(nameof(HintFamily));
        public static readonly StyledProperty<Color> HintColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(HintColor), Color.FromRgb(0xD4, 0xD4, 0xD4));
        public static readonly StyledProperty<double> RowPitchProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(RowPitch), 54);
        public static readonly StyledProperty<double> OpeningScaleProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(OpeningScale), 1.0);
        public static readonly StyledProperty<LetterCase?> FooterLetterCaseProperty = AvaloniaProperty.Register<MenuPanel, LetterCase?>(nameof(FooterLetterCase));
        public static readonly StyledProperty<double> TitleMinScaleProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(TitleMinScale), 1.0);
        public static readonly StyledProperty<int> TitleMaxLinesProperty = AvaloniaProperty.Register<MenuPanel, int>(nameof(TitleMaxLines), 1);
        public static readonly StyledProperty<string?> SubtitleProperty = AvaloniaProperty.Register<MenuPanel, string?>(nameof(Subtitle));
        public static readonly StyledProperty<double> SubtitleSizeProperty = AvaloniaProperty.Register<MenuPanel, double>(nameof(SubtitleSize), 26);
        public static readonly StyledProperty<LetterCase> SubtitleLetterCaseProperty = AvaloniaProperty.Register<MenuPanel, LetterCase>(nameof(SubtitleLetterCase), LetterCase.Upper);
        public static readonly StyledProperty<bool> ShowsTitleBandProperty = AvaloniaProperty.Register<MenuPanel, bool>(nameof(ShowsTitleBand), true);
        public static readonly StyledProperty<int> FooterMaxLinesProperty = AvaloniaProperty.Register<MenuPanel, int>(nameof(FooterMaxLines), 1);
        public static readonly StyledProperty<Control?> ButtonsProperty = AvaloniaProperty.Register<MenuPanel, Control?>(nameof(Buttons));
        public static readonly StyledProperty<Color> HintBackgroundProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(HintBackground), Color.FromArgb(0xF0, 0x16, 0x16, 0x18));
        public static readonly StyledProperty<PadGlyphStyle> HintGlyphStyleProperty = AvaloniaProperty.Register<MenuPanel, PadGlyphStyle>(nameof(HintGlyphStyle), PadGlyphStyle.Filled);
        public static readonly StyledProperty<bool> ShowsScrollIndicatorProperty = AvaloniaProperty.Register<MenuPanel, bool>(nameof(ShowsScrollIndicator), true);
        public static readonly StyledProperty<Color> ScrollIndicatorColorProperty = AvaloniaProperty.Register<MenuPanel, Color>(nameof(ScrollIndicatorColor), Color.FromRgb(0x74, 0x74, 0x78));
        public static readonly StyledProperty<MenuAnchor> AnchorProperty = AvaloniaProperty.Register<MenuPanel, MenuAnchor>(nameof(Anchor));
        public static readonly StyledProperty<Control?> PaneProperty = AvaloniaProperty.Register<MenuPanel, Control?>(nameof(Pane));
        public static readonly StyledProperty<IImage?> TitleIconProperty = AvaloniaProperty.Register<MenuPanel, IImage?>(nameof(TitleIcon));

        // Design sizes before Scale: the title's band, the footer's band (or the padding under the rows without one), the margins, and the help bar's text.
        private const double TitleBand = 100, FooterBand = 78, BareFooter = 20, Edge = 24, HintText = 26, HintGap = 10;

        // With a subtitle: the title's centre, the first subtitle line's centre, the lines' pitch, and the space under the last; the buttons' band's padding.
        private const double SubtitledTitleCentre = 49, FirstSubtitleCentre = 107, SubtitlePitch = 32, UnderSubtitle = 32, ButtonsPad = 12, FooterPad = 14;

        // The title icon's height and its gap to the title, and the narrowest and widest it is drawn as a multiple of its height (§205.3).
        private const double IconHeight = 64, IconGap = 16, IconNarrowest = 0.5, IconWidest = 1.5;

        // The scroll indicator's design sizes: each chevron pair's square, the gap between the two pairs, and the inset from the panel's right edge (§195.2).
        private const double IndicatorSide = 25, IndicatorGap = 7, IndicatorInset = 11;

        // The edge card's design sizes (§210): the safe area at the sides, top and foot, the help bar's band, the gap above the rows and the pane, the room right of the rows, the cover's height and the help bar's text.
        private const double SafeSide = 64, SafeTop = 40, SafeFoot = 40, HelpBand = 58, RowsGap = 16, RowsRight = 24, EdgeIconHeight = 44, EdgeHintText = 20;

        // With a breadcrumb over an edge card's title: the breadcrumb's centre, the title's centre below it, the breadcrumb's size and its lines' pitch.
        private const double CrumbCentre = 31, CrumbedTitleCentre = 65, CrumbSize = 20, CrumbPitch = 26;

        private readonly HintBar _hints = new() { LetterCase = LetterCase.Upper };
        private ScrollViewer? _watched;

        static MenuPanel()
        {
            AffectsMeasure<MenuPanel>(ScaleProperty, TitleProperty, FooterProperty, WidthFractionProperty, MaxWidthToHeightProperty, HintsProperty, RowPitchProperty,
                SubtitleProperty, ButtonsProperty, FooterMaxLinesProperty, FooterSizeProperty, ShowsTitleBandProperty, TitleMaxLinesProperty, TitleMinScaleProperty, TitleSizeProperty, FontPathProperty, LetterCaseProperty,
                AnchorProperty, PaneProperty, TitleIconProperty);
            AffectsRender<MenuPanel>(TitleIconProperty, FontPathProperty, TitleSizeProperty, TitleMinScaleProperty, FooterSizeProperty, PanelCornerRadiusProperty, PanelColorProperty, TitleColorProperty,
                FooterColorProperty, RuleColorProperty, LetterCaseProperty, FooterLetterCaseProperty, SubtitleSizeProperty, SubtitleLetterCaseProperty, ShowsScrollIndicatorProperty, ScrollIndicatorColorProperty);
        }

        private ResourceDictionary _palette;

        // The palette in the column MenuLook.IsLight asks for, replacing the one the panel holds (§206.1).
        private void SwapPalette(bool light)
        {
            ResourceDictionary next = MenuLook.CreatePalette(light);
            int at = Resources.MergedDictionaries.IndexOf(_palette);
            if (at >= 0) Resources.MergedDictionaries[at] = next;
            else Resources.MergedDictionaries.Add(next);
            _palette = next;
        }

        /// <summary>An empty panel, its help bar hidden until Hints are given.</summary>
        public MenuPanel()
        {
            // The look's palette, so what the menu holds is painted from tokens a theme can reach - see docs/LunaP.md §200.3.
            _palette = MenuLook.CreatePalette();
            Resources.MergedDictionaries.Add(_palette);
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

        /// <summary>How far a title too wide for its band shrinks before it is cut with an ellipsis, as a fraction of TitleSize. 1 by default, which never shrinks it - see docs/LunaP.md §196.9.</summary>
        public double TitleMinScale { get => GetValue(TitleMinScaleProperty); set => SetValue(TitleMinScaleProperty, value); }

        /// <summary>The footer's casing alone, such as None for words that hold a path; null follows LetterCase. Null by default.</summary>
        public LetterCase? FooterLetterCase { get => GetValue(FooterLetterCaseProperty); set => SetValue(FooterLetterCaseProperty, value); }

        private LetterCase FooterCase => FooterLetterCase ?? LetterCase;

        /// <summary>Whether a wrapped footer (FooterMaxLines above one) needs more lines than it is given and ends in an ellipsis.</summary>
        public bool IsFooterCut
        {
            get
            {
                if (Footer is not { Length: > 0 } words || FooterMaxLines <= 1 || PanelBounds.Width <= 0) return false;
                double u = Unit, size = FooterSize * u, lineHeight = size * 1.25;
                GlyphTypeface typeface = GetValue(FontPathProperty) is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;
                return FontLayout.Create(typeface, size, FontLayout.Cased(words, FooterCase), 1, PanelBounds.Width - 48 * u, FooterMaxLines * lineHeight + 0.5, true, "…").Truncated;
            }
        }

        /// <summary>Whether the title, at the size it is drawn, is still cut with an ellipsis because even its smallest size does not fit the band.</summary>
        public bool IsTitleCut => Title is { Length: > 0 } t && TitleLayout(t, TitleBounds.Width).Cut;

        /// <summary>How many lines the title is drawn on: more than one only where TitleMaxLines lets a title too wide for one line at TitleMinScale wrap.</summary>
        public int TitleLines => Title is { Length: > 0 } t && TitleLayout(t, TitleBounds.Width).Lines is { } l ? l.Lines.Count : Title is { Length: > 0 } ? 1 : 0;

        /// <summary>The size the title is drawn at, in this control's pixels, as last arranged; 0 with no title.</summary>
        public double TitleDrawnSize => Title is { Length: > 0 } t ? TitleLayout(t, TitleBounds.Width).Size : 0;

        // A wrapped title may shrink below TitleMinScale by this much more, and no further, before it is cut.
        private const double WrappedTitleLeast = 0.75;

        // The title at a panel's width: shrunk toward TitleMinScale on one line; then, where TitleMaxLines allows and there is no subtitle, wrapped and shrunk toward WrappedTitleLeast of that; and whether it is still cut (§196.11).
        private (double Size, FontLayout? Lines, bool Cut) TitleLayout(string title, double panelWidth)
        {
            double u = Unit, full = TitleSize * u;
            double room = panelWidth - 48 * u - IconSpace();
            if (room <= 0) return (full, null, false);
            GlyphTypeface typeface = GetValue(FontPathProperty) is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;
            string cased = FontLayout.Cased(title, LetterCase);
            double width = FontLayout.Measure(typeface, full, cased);
            if (width <= room) return (full, null, false);
            double least = full * Math.Clamp(TitleMinScale, 0.1, 1);
            double size = Math.Max(least, full * room / width);
            bool cut = FontLayout.Measure(typeface, size, cased) > room + 0.5;
            if (!cut || TitleMaxLines <= 1 || SubtitleLines.Length > 0) return (size, null, cut);
            for (double s = least; ; s = Math.Max(least * WrappedTitleLeast, s * 0.96))
            {
                double line = FontLayout.Create(typeface, s, "H", 1, double.PositiveInfinity, double.PositiveInfinity, false, null).LineHeight;
                FontLayout lines = FontLayout.Create(typeface, s, cased, 1, room, TitleMaxLines * line + 0.5, true, "…");
                if (!lines.Truncated || s <= least * WrappedTitleLeast + 1e-9) return (s, lines, lines.Truncated);
            }
        }

        /// <summary>The most lines a title too wide for its band at TitleMinScale may wrap onto, the band growing to hold them, shrinking a little further if it must, before it is cut. 1 by default, which never wraps - see docs/LunaP.md §196.11.</summary>
        public int TitleMaxLines { get => GetValue(TitleMaxLinesProperty); set => SetValue(TitleMaxLinesProperty, value); }

        /// <summary>The footer's em size in design pixels before Scale. 26 by default.</summary>
        public double FooterSize { get => GetValue(FooterSizeProperty); set => SetValue(FooterSizeProperty, value); }

        /// <summary>The panel's width as a fraction of the area's. 0.66 by default.</summary>
        public double WidthFraction { get => GetValue(WidthFractionProperty); set => SetValue(WidthFractionProperty, value); }

        /// <summary>The most the panel's width may be, as a multiple of the area's height, so a wide screen does not stretch it. 1.05 by default.</summary>
        public double MaxWidthToHeight { get => GetValue(MaxWidthToHeightProperty); set => SetValue(MaxWidthToHeightProperty, value); }

        /// <summary>The panel's corner radius in design pixels before Scale. 16 by default.</summary>
        public double PanelCornerRadius { get => GetValue(PanelCornerRadiusProperty); set => SetValue(PanelCornerRadiusProperty, value); }

        /// <summary>The panel's fill. #FF1B1B1E, the dark surface, by default; the theme sets LunaSurfaceColor.</summary>
        public Color PanelColor { get => GetValue(PanelColorProperty); set => SetValue(PanelColorProperty, value); }

        /// <summary>The title's colour. #FFEEEEF0, the strong text, by default; the theme sets LunaTextStrongColor.</summary>
        public Color TitleColor { get => GetValue(TitleColorProperty); set => SetValue(TitleColorProperty, value); }

        /// <summary>The footer's colour. #FF8D8D91, the muted grey, by default; the theme sets LunaMutedColor.</summary>
        public Color FooterColor { get => GetValue(FooterColorProperty); set => SetValue(FooterColorProperty, value); }

        /// <summary>The colour of the rule under the title. #FF333337, the rows' dark grey, by default; the theme sets LunaRuleColor.</summary>
        public Color RuleColor { get => GetValue(RuleColorProperty); set => SetValue(RuleColorProperty, value); }

        /// <summary>The casing of the title and the footer. Upper by default.</summary>
        public LetterCase LetterCase { get => GetValue(LetterCaseProperty); set => SetValue(LetterCaseProperty, value); }

        /// <summary>The help bar's entries, drawn upper case at the bottom centre of the area. Null or empty hides it.</summary>
        public IReadOnlyList<HintEntry>? Hints { get => GetValue(HintsProperty); set => SetValue(HintsProperty, value); }

        /// <summary>The pad family the help bar draws its buttons in. Generic by default.</summary>
        public PadFamily HintFamily { get => GetValue(HintFamilyProperty); set => SetValue(HintFamilyProperty, value); }

        /// <summary>The help bar's text and icon colour. #FFD4D4D4, the text, by default; the theme sets LunaTextColor.</summary>
        public Color HintColor { get => GetValue(HintColorProperty); set => SetValue(HintColorProperty, value); }

        /// <summary>The help bar's fill. #F0161618, a near black grey almost opaque, by default.</summary>
        public Color HintBackground { get => GetValue(HintBackgroundProperty); set => SetValue(HintBackgroundProperty, value); }

        /// <summary>How the help bar draws its buttons. Filled, the reference's lettered discs, by default.</summary>
        public PadGlyphStyle HintGlyphStyle { get => GetValue(HintGlyphStyleProperty); set => SetValue(HintGlyphStyleProperty, value); }

        /// <summary>Whether a pair of chevrons at the title's right shows that the rows run past the panel, above, below or both, while a ScrollViewer child has more rows than it shows. True by default.</summary>
        public bool ShowsScrollIndicator { get => GetValue(ShowsScrollIndicatorProperty); set => SetValue(ShowsScrollIndicatorProperty, value); }

        /// <summary>The scroll indicator's colour. #FF747478, a mid grey, by default.</summary>
        public Color ScrollIndicatorColor { get => GetValue(ScrollIndicatorColorProperty); set => SetValue(ScrollIndicatorColorProperty, value); }

        /// <summary>Which ways the rows run past the panel now, as the indicator draws it; None when they all show, when the child is not a ScrollViewer, or when the indicator is off.</summary>
        public MenuScrollIndicator ScrollIndicator
        {
            get
            {
                if (!ShowsScrollIndicator || _watched is not { } sv) return MenuScrollIndicator.None;
                double extent = sv.Extent.Height, viewport = sv.Viewport.Height, offset = sv.Offset.Y;
                if (extent <= viewport + 0.5) return MenuScrollIndicator.None;
                bool up = offset > 0.5, down = offset + viewport < extent - 0.5;
                return up && down ? MenuScrollIndicator.Both : up ? MenuScrollIndicator.Up : down ? MenuScrollIndicator.Down : MenuScrollIndicator.None;
            }
        }

        /// <summary>The squares the indicator's upper and lower chevron pairs are drawn in, in this control's coordinates, from the last arrange; each is there whether or not it is drawn now.</summary>
        public (Rect Up, Rect Down) ScrollIndicatorBounds
        {
            get
            {
                if (TitleBounds.Height <= 0) return default;
                double u = Unit, side = IndicatorSide * u, gap = IndicatorGap * u / 2, right = PanelBounds.Right - IndicatorInset * u;
                double centre = TitleCentre();
                return (new Rect(right - side, centre - gap - side, side, side), new Rect(right - side, centre + gap, side, side));
            }
        }

        /// <summary>The height of one row in design pixels before Scale; rows that do not all fit are shown in whole rows of it. 54 by default, MenuRow's own.</summary>
        public double RowPitch { get => GetValue(RowPitchProperty); set => SetValue(RowPitchProperty, value); }

        /// <summary>The panel's scale about its own centre, for an opening that grows it into place: the panel, its title, rows, buttons and footer are drawn at it, the help bar is not, and nothing is laid out again. 1 by default.</summary>
        public double OpeningScale { get => GetValue(OpeningScaleProperty); set => SetValue(OpeningScaleProperty, value); }

        /// <summary>Lines under the title, smaller, such as what the menu is about; separated by line breaks. Null shows none and keeps the title's band its own height.</summary>
        public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

        /// <summary>The subtitle's em size in design pixels before Scale. 26 by default.</summary>
        public double SubtitleSize { get => GetValue(SubtitleSizeProperty); set => SetValue(SubtitleSizeProperty, value); }

        /// <summary>The casing of the subtitle. Upper by default.</summary>
        public LetterCase SubtitleLetterCase { get => GetValue(SubtitleLetterCaseProperty); set => SetValue(SubtitleLetterCaseProperty, value); }

        /// <summary>Whether the panel keeps its title's band and the rule under it; false, with no title, starts the rows at the panel's top, as a message box does. True by default.</summary>
        public bool ShowsTitleBand { get => GetValue(ShowsTitleBandProperty); set => SetValue(ShowsTitleBandProperty, value); }

        /// <summary>How many lines the footer may wrap to; its band is kept that tall whatever the footer holds, so the panel does not change height as it changes. 1 by default.</summary>
        public int FooterMaxLines { get => GetValue(FooterMaxLinesProperty); set => SetValue(FooterMaxLinesProperty, value); }

        /// <summary>A control, typically a row of push buttons, shown in a band of its own under the rows and always in view, however the rows scroll. Null shows none.</summary>
        public Control? Buttons { get => GetValue(ButtonsProperty); set => SetValue(ButtonsProperty, value); }

        /// <summary>Where the panel stands: centred (the default) or at the area's left edge - see docs/LunaP.md §205.2.</summary>
        public MenuAnchor Anchor { get => GetValue(AnchorProperty); set => SetValue(AnchorProperty, value); }

        /// <summary>A control shown in the room to the right of the panel, above the help bar, such as what the chosen row is about; it is arranged in PaneBounds by its own alignment. Null shows none.</summary>
        public Control? Pane { get => GetValue(PaneProperty); set => SetValue(PaneProperty, value); }

        /// <summary>The room to the right of the panel, between the margins and above the help bar, in this control's coordinates, as last arranged; empty where there is none.</summary>
        public Rect PaneBounds { get; private set; }

        /// <summary>A small picture at the left of the title's band, such as a game's cover, with the title left-aligned after it on one line. Null shows none and centres the title.</summary>
        public IImage? TitleIcon { get => GetValue(TitleIconProperty); set => SetValue(TitleIconProperty, value); }

        /// <summary>The rectangle the title icon is drawn in, in this control's coordinates, as last arranged; empty without one.</summary>
        public Rect TitleIconBounds
        {
            get
            {
                if (IconSpace() is not > 0 || TitleBounds.Height <= 0) return default;
                double u = Unit, h = IconHigh * u;
                return new Rect(TitleBounds.X + (IsEdge ? 0 : 24 * u), TitleCentre() - h / 2, IconSpace() - IconGap * u, h);
            }
        }

        // The width the icon and its gap take from the title's band, 0 without one.
        private double IconSpace()
        {
            if (TitleIcon is not { } icon || icon.Size.Height <= 0) return 0;
            double u = Unit, h = IconHigh * u;
            return h * Math.Clamp(icon.Size.Width / icon.Size.Height, IconNarrowest, IconWidest) + IconGap * u;
        }

        /// <summary>The buttons' band's rectangle in this control's coordinates, as last arranged; empty without Buttons.</summary>
        public Rect ButtonsBounds { get; private set; }

        /// <summary>The panel's rectangle in this control's coordinates, as last arranged.</summary>
        public Rect PanelBounds { get; private set; }

        /// <summary>The title band's rectangle in this control's coordinates, as last arranged; the rule is its bottom edge.</summary>
        public Rect TitleBounds { get; private set; }

        /// <summary>The help bar, which a host may read but should configure through Hints and the colours.</summary>
        public HintBar HelpBar => _hints;

        private bool IsEdge => Anchor == MenuAnchor.Edge;

        private double IconHigh => IsEdge ? EdgeIconHeight : IconHeight;

        // The edge card's help bar band, from the foot's safe area up, in this control's coordinates.
        private Rect HelpBandBounds(Size area) => new(0, area.Height - (SafeFoot + HelpBand) * Unit, area.Width, HelpBand * Unit);

        private double Unit => double.IsFinite(GetValue(ScaleProperty)) && GetValue(ScaleProperty) > 0 ? GetValue(ScaleProperty) : 1;

        private double BottomBand => FooterMaxLines > 1 ? (FooterMaxLines * FooterSize * 1.25 + 2 * FooterPad) * Unit : (Footer is { Length: > 0 } ? FooterBand : BareFooter) * Unit;

        private string[] SubtitleLines => Subtitle is { Length: > 0 } sub ? sub.Split('\n') : [];

        private double TopBand => TopBandAt(PanelBounds.Width);

        // The title's band at a panel's width, taller by a line for each line a wrapped title adds.
        private double TopBandAt(double panelWidth)
        {
            if (!ShowsTitleBand && Title is not { Length: > 0 }) return 0;
            if (IsEdge) return (TitleBand + CrumbPitch * Math.Max(0, SubtitleLines.Length - 1)) * Unit;
            if (SubtitleLines.Length is var n && n > 0) return (FirstSubtitleCentre + SubtitlePitch * (n - 1) + UnderSubtitle) * Unit;
            double extra = Title is { Length: > 0 } t && TitleLayout(t, panelWidth).Lines is { Lines.Count: > 1 } l ? (l.Lines.Count - 1) * l.LineHeight : 0;
            return TitleBand * Unit + extra;
        }

        private double ButtonsBand => Buttons is { IsVisible: true } b ? b.DesiredSize.Height + 2 * ButtonsPad * Unit : 0;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MenuLook.IsLightProperty) SwapPalette(change.GetNewValue<bool>());
            if (change.Property == ChildProperty) Watch(Scroller(change.NewValue as Control));
            if (change.Property == ButtonsProperty || change.Property == PaneProperty)
            {
                if (change.OldValue is Control old)
                {
                    VisualChildren.Remove(old);
                    LogicalChildren.Remove(old);
                }
                if (change.NewValue is Control added)
                {
                    VisualChildren.Add(added);
                    LogicalChildren.Add(added);
                }
            }
            if (change.Property == OpeningScaleProperty)
            {
                ScaleChildren();
                InvalidateVisual();
            }
            if (change.Property == HintsProperty || change.Property == HintFamilyProperty || change.Property == HintGlyphStyleProperty || change.Property == HintColorProperty || change.Property == HintBackgroundProperty
                || change.Property == ScaleProperty || change.Property == FontPathProperty || change.Property == Windowing.SheetLayer.IsCoveredProperty)
                ConfigureHints();
        }

        private void ConfigureHints()
        {
            double u = Unit;
            _hints.Entries = Hints;
            _hints.IsVisible = Hints is { Count: > 0 };
            // Under a message box the box's help bar is the one shown; this one keeps its place so the panel does not move.
            _hints.Opacity = GetValue(Windowing.SheetLayer.IsCoveredProperty) ? 0 : 1;
            _hints.PadFamily = HintFamily;
            _hints.GlyphStyle = HintGlyphStyle;
            _hints.FontPath = GetValue(FontPathProperty);
            _hints.FontSize = HintText * u;
            _hints.IconColor = HintColor;
            _hints.TextColor = HintColor;
            _hints.BackgroundColor = HintBackground;
            _hints.BackgroundCornerRadius = 8 * u;
            _hints.Padding = new Thickness(14 * u, 8 * u);
            _hints.EntrySpacing = 18 * u;
            _hints.IconTextSpacing = 6 * u;
            _hints.IconScale = 1;
            _hints.LetterCase = LetterCase.Upper;
            if (IsEdge)
            {
                // On the edge card the band is the bar's background, and its words are Barlow in their own case (§210).
                _hints.FontPath = LunaFonts.BodyFile;
                _hints.FontSize = EdgeHintText * u;
                _hints.BackgroundColor = Colors.Transparent;
                _hints.BackgroundCornerRadius = 0;
                _hints.Padding = default;
                _hints.EntrySpacing = 40 * u;
                _hints.IconTextSpacing = 10 * u;
                _hints.IconScale = 1.5;
                _hints.LetterCase = LetterCase.None;
            }
        }

        // The rows' scroller, redrawn as it scrolls or its rows change, so the indicator follows it.
        private void Watch(ScrollViewer? scroller)
        {
            if (_watched is not null) _watched.PropertyChanged -= OnScrollerChanged;
            _watched = scroller;
            if (scroller is not null) scroller.PropertyChanged += OnScrollerChanged;
            InvalidateVisual();
        }

        private void OnScrollerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ExtentProperty || e.Property == ScrollViewer.ViewportProperty) InvalidateVisual();
        }

        // The child itself when it scrolls, else the first scroller its template holds, as a list's does.
        private static ScrollViewer? Scroller(Control? child) =>
            child as ScrollViewer ?? child?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

        // The title's capitals' centre: the band's middle, or with a subtitle the title's own line.
        private double TitleCentre() => IsEdge ? (SubtitleLines.Length > 0 ? TitleBounds.Top + (CrumbedTitleCentre + CrumbPitch * (SubtitleLines.Length - 1)) * Unit : TitleBounds.Center.Y)
            : SubtitleLines.Length > 0 ? TitleBounds.Top + SubtitledTitleCentre * Unit : TitleBounds.Center.Y;

        // Two chevrons one above the other in a square, pointing up or down, as the reference's indicator is drawn (§195.2).
        private static void DrawChevrons(DrawingContext context, Rect box, bool down, IPen pen)
        {
            double w = box.Width;
            for (int i = 0; i < 2; i++)
            {
                double arms = (down ? 0.072 : 0.928) + (down ? 1 : -1) * i * 0.396, apex = arms + (down ? 1 : -1) * 0.38;
                var chevron = new StreamGeometry();
                using (StreamGeometryContext g = chevron.Open())
                {
                    g.BeginFigure(new Point(box.X + 0.072 * w, box.Y + arms * w), false);
                    g.LineTo(new Point(box.X + 0.5 * w, box.Y + apex * w));
                    g.LineTo(new Point(box.X + 0.928 * w, box.Y + arms * w));
                    g.EndFigure(false);
                }
                context.DrawGeometry(null, pen, chevron);
            }
        }

        // The panel's width and the most its rows may take, for an area of this size.
        private (double Width, double RowsMax) Frame(Size area, double hintHeight)
        {
            double u = Unit;
            double width = Math.Max(0, Math.Min(Math.Min(area.Width * WidthFraction, area.Height * MaxWidthToHeight), area.Width - 2 * Edge * u));
            double rowsMax = IsEdge ? Math.Max(0, area.Height - (SafeTop + RowsGap + RowsGap + HelpBand + SafeFoot) * u - TopBandAt(width) - ButtonsBand - (Footer is { Length: > 0 } ? BottomBand : 0))
                : Math.Max(0, area.Height - hintHeight - 2 * Edge * u - TopBandAt(width) - ButtonsBand - BottomBand);
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
            Buttons?.Measure(new Size(double.IsFinite(area.Width) ? area.Width : double.PositiveInfinity, double.PositiveInfinity));
            (double width, double rowsMax) = Frame(area, hintHeight);
            Child?.Measure(new Size(IsEdge ? RowsWidth(width) : width, rowsMax));
            Pane?.Measure(PaneRoom(area, width, hintHeight).Size);
            return area;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double u = Unit;
            double hintHeight = _hints.IsVisible ? _hints.DesiredSize.Height + HintGap * u : 0;
            (double width, double rowsMax) = Frame(finalSize, hintHeight);
            double rows = Math.Min(Child?.DesiredSize.Height ?? 0, rowsMax);
            if (IsEdge) return ArrangeEdge(finalSize, width, rows);
            double buttons = ButtonsBand, band = TopBandAt(width);
            double height = band + rows + buttons + BottomBand;
            double top = Math.Clamp((finalSize.Height - height) / 2, Edge * u, Math.Max(Edge * u, finalSize.Height - hintHeight - Edge * u - height));
            double left = PanelLeft(finalSize.Width, width);
            PanelBounds = new Rect(left, top, width, height);
            PaneBounds = PaneRoom(finalSize, width, hintHeight);
            Pane?.Arrange(PaneBounds);
            TitleBounds = new Rect(left, top, width, band);
            Child?.Arrange(new Rect(left, top + band, width, rows));
            if (Scroller(Child) is var scroller && !ReferenceEquals(scroller, _watched)) Watch(scroller);
            ButtonsBounds = Buttons is { IsVisible: true } ? new Rect(left, top + band + rows, width, buttons) : default;
            if (Buttons is { } b)
            {
                Size bs = b.DesiredSize;
                b.Arrange(new Rect(left + Math.Max(0, (width - bs.Width) / 2), top + band + rows + ButtonsPad * u, Math.Min(width, bs.Width), bs.Height));
            }
            ScaleChildren();
            if (_hints.IsVisible)
            {
                Size hs = _hints.DesiredSize;
                _hints.Arrange(new Rect((finalSize.Width - hs.Width) / 2, finalSize.Height - hs.Height - HintGap * u, hs.Width, hs.Height));
            }
            return finalSize;
        }

        // The edge card: the panel the area's full height at its left, the title row and rows inside the safe area, the pane beside them, the help bar in its band (§210).
        private Size ArrangeEdge(Size area, double width, double rows)
        {
            double u = Unit, band = TopBandAt(width), x = SafeSide * u, top = SafeTop * u;
            PanelBounds = new Rect(0, 0, width, area.Height);
            TitleBounds = new Rect(x, top, RowsWidth(width), band);
            double rowsTop = top + band + RowsGap * u;
            Child?.Arrange(new Rect(x, rowsTop, RowsWidth(width), rows));
            if (Scroller(Child) is var scroller && !ReferenceEquals(scroller, _watched)) Watch(scroller);
            ButtonsBounds = Buttons is { IsVisible: true } ? new Rect(x, rowsTop + rows, RowsWidth(width), ButtonsBand) : default;
            if (Buttons is { } b) b.Arrange(new Rect(x, rowsTop + rows + ButtonsPad * u, Math.Min(RowsWidth(width), b.DesiredSize.Width), b.DesiredSize.Height));
            PaneBounds = PaneRoom(area, width, 0);
            Pane?.Arrange(PaneBounds);
            ScaleChildren();
            if (_hints.IsVisible)
            {
                Rect help = HelpBandBounds(area);
                Size hs = _hints.DesiredSize;
                _hints.Arrange(new Rect(x, help.Y + (help.Height - hs.Height) / 2, Math.Min(hs.Width, Math.Max(0, area.Width - 2 * x)), hs.Height));
            }
            return area;
        }

        // The edge card's footer: under the rows and the buttons, as wide as the rows.
        private Rect EdgeFooter(double height) => new(TitleBounds.X, TitleBounds.Bottom + RowsGap * Unit + (Child?.Bounds.Height ?? 0) + ButtonsBand, TitleBounds.Width, height);

        // The rows' width on the edge card: the panel less the safe area at its left and the gap at its right.
        private double RowsWidth(double width) => Math.Max(0, width - (SafeSide + RowsRight) * Unit);

        // The panel's left edge: the margin when anchored left, else centred.
        private double PanelLeft(double areaWidth, double width) => Anchor == MenuAnchor.Edge ? 0 : Anchor == MenuAnchor.Left ? Math.Min(Edge * Unit, Math.Max(0, areaWidth - width)) : (areaWidth - width) / 2;

        // The room right of the panel for the pane: inside the margins, above the help bar.
        private Rect PaneRoom(Size area, double width, double hintHeight)
        {
            double u = Unit, x = PanelLeft(area.Width, width) + width + Edge * u;
            if (IsEdge)
            {
                x = width + SafeSide * u;
                double top = (SafeTop + RowsGap) * u + TopBandAt(width), bottom = HelpBandBounds(area).Y - RowsGap * u;
                return new Rect(x, top, Math.Max(0, area.Width - SafeSide * u - x), Math.Max(0, bottom - top));
            }
            return new Rect(x, Edge * u, Math.Max(0, area.Width - Edge * u - x), Math.Max(0, area.Height - hintHeight - 2 * Edge * u));
        }

        // The rows and the buttons scaled about the panel's centre, as the panel is drawn; a scale of 1 takes the transform off again.
        private void ScaleChildren()
        {
            double k = OpeningScale;
            foreach (Control? c in new[] { Child, Buttons })
            {
                if (c is null) continue;
                bool ours = c.RenderTransform is ScaleTransform t && _openingTransforms.Contains(t);
                if (k == 1)
                {
                    if (ours) c.RenderTransform = null;
                    continue;
                }
                if (c.RenderTransform is not null && !ours) continue;
                c.RenderTransformOrigin = new RelativePoint(PanelBounds.Center - c.Bounds.TopLeft, RelativeUnit.Absolute);
                if (ours) ((ScaleTransform)c.RenderTransform!).ScaleX = ((ScaleTransform)c.RenderTransform!).ScaleY = k;
                else
                {
                    var scale = new ScaleTransform(k, k);
                    _openingTransforms.Add(scale);
                    c.RenderTransform = scale;
                }
            }
            if (k == 1) _openingTransforms.Clear();
        }

        private readonly HashSet<ScaleTransform> _openingTransforms = new();

        public override void Render(DrawingContext context)
        {
            double u = Unit;
            Rect panel = PanelBounds;
            if (panel.Width <= 0) return;
            double k = OpeningScale;
            using DrawingContext.PushedState opening = context.PushTransform(Matrix.CreateTranslation(-panel.Center.X, -panel.Center.Y) * Matrix.CreateScale(k, k) * Matrix.CreateTranslation(panel.Center.X, panel.Center.Y));
            double radius = IsEdge ? 0 : PanelCornerRadius * u;
            double rule = Math.Max(1, Math.Round(u));
            if (IsEdge && _hints.IsVisible)
            {
                // The help bar's band across the foot, on the panel's surface with the Border token's line along its top (§210).
                Rect help = HelpBandBounds(Bounds.Size);
                context.FillRectangle(new ImmutableSolidColorBrush(PanelColor), help);
                Color line = this.TryFindResource("LunaBorderColor", ActualThemeVariant, out object? found) && found is Color c ? c : EmuSen.LunaP.Theme.LunaPalette.Border.Color;
                context.FillRectangle(new ImmutableSolidColorBrush(line), new Rect(help.X, help.Y, help.Width, rule));
            }
            if (IsEdge)
            {
                // The one shadow, on the card's open edge: the Void token at half, 8 out and 24 blurred (§210.2).
                Color voidColour = this.TryFindResource("LunaVoidColor", ActualThemeVariant, out object? v) && v is Color vc ? vc : Colors.Black;
                var shadow = new BoxShadow { OffsetX = 8 * u, Blur = 24 * u, Color = Color.FromArgb(0x80, voidColour.R, voidColour.G, voidColour.B) };
                context.DrawRectangle(new ImmutableSolidColorBrush(PanelColor), null, new RoundedRect(panel), new BoxShadows(shadow));
            }
            else context.DrawRectangle(new ImmutableSolidColorBrush(PanelColor), null, panel, radius, radius);
            if (TitleBounds.Height > 0 && !IsEdge) context.FillRectangle(new ImmutableSolidColorBrush(RuleColor), new Rect(panel.X, TitleBounds.Bottom - rule, panel.Width, rule));

            GlyphTypeface typeface = GetValue(FontPathProperty) is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;
            // On the edge card the title alone is in the panel's face; the breadcrumb and the footer are Barlow (§210).
            GlyphTypeface small = IsEdge && FontFiles.Load(LunaFonts.BodyFile) is { } body ? body : typeface;
            string[] lines = SubtitleLines;
            if (ScrollIndicator is var scroll and not MenuScrollIndicator.None)
            {
                var pen = new ImmutablePen(new ImmutableSolidColorBrush(ScrollIndicatorColor), IndicatorSide * u * 0.136, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                (Rect upBox, Rect downBox) = ScrollIndicatorBounds;
                if (scroll is MenuScrollIndicator.Up or MenuScrollIndicator.Both) DrawChevrons(context, upBox, down: false, pen);
                if (scroll is MenuScrollIndicator.Down or MenuScrollIndicator.Both) DrawChevrons(context, downBox, down: true, pen);
            }
            if (TitleIcon is { } icon && TitleIconBounds is { Width: > 0 } iconBox)
            {
                double fit = Math.Min(iconBox.Width / icon.Size.Width, iconBox.Height / icon.Size.Height);
                context.DrawImage(icon, new Rect(icon.Size), iconBox.CenterRect(new Rect(0, 0, icon.Size.Width * fit, icon.Size.Height * fit)));
            }
            if (Title is { Length: > 0 } title)
            {
                (double size, FontLayout? wrappedTitle, _) = TitleLayout(title, TitleBounds.Width);
                Rect band = IsEdge ? TitleBounds : TitleBounds.Deflate(new Thickness(24 * u, 0));
                TextAlignment align = IsEdge ? TextAlignment.Left : TextAlignment.Center;
                if (IconSpace() is > 0 and var iconSpace)
                {
                    band = new Rect(band.X + iconSpace, band.Y, Math.Max(0, band.Width - iconSpace), band.Height);
                    align = TextAlignment.Left;
                }
                if (wrappedTitle is { Lines.Count: > 1 }) wrappedTitle.Draw(context, new ImmutableSolidColorBrush(TitleColor), band, align, 0.5);
                else MenuRow.DrawLine(context, typeface, size, FontLayout.Cased(title, LetterCase), band, TitleCentre() + MenuRow.CapHeight(typeface, size) / 2, align, new ImmutableSolidColorBrush(TitleColor));
            }
            for (int i = 0; i < lines.Length && IsEdge; i++)
            {
                // On the edge card the subtitle is the way back, a breadcrumb over the title.
                double size = CrumbSize * u;
                double centre = TitleBounds.Top + (CrumbCentre + CrumbPitch * i) * u;
                MenuRow.DrawLine(context, small, size, FontLayout.Cased(lines[i], SubtitleLetterCase), TitleBounds, centre + MenuRow.CapHeight(small, size) / 2, TextAlignment.Left, new ImmutableSolidColorBrush(FooterColor));
            }
            for (int i = 0; i < lines.Length && !IsEdge; i++)
            {
                double size = SubtitleSize * u;
                Rect band = TitleBounds.Deflate(new Thickness(24 * u, 0));
                double centre = TitleBounds.Top + (FirstSubtitleCentre + SubtitlePitch * i) * u;
                MenuRow.DrawLine(context, typeface, size, FontLayout.Cased(lines[i], SubtitleLetterCase), band, centre + MenuRow.CapHeight(typeface, size) / 2, TextAlignment.Center, new ImmutableSolidColorBrush(FooterColor));
            }
            if (Footer is { Length: > 0 } wrapped && FooterMaxLines > 1)
            {
                double size = FooterSize * u;
                var band = IsEdge ? EdgeFooter(BottomBand) : new Rect(panel.X + 24 * u, panel.Bottom - BottomBand, panel.Width - 48 * u, BottomBand);
                double lineHeight = FontLayout.Create(small, size, "H", 1, double.PositiveInfinity, double.PositiveInfinity, false, null).LineHeight;
                FontLayout wrappedLines = FontLayout.Create(small, size, FontLayout.Cased(wrapped, FooterCase), 1, band.Width, FooterMaxLines * lineHeight + 0.5, true, "…");
                wrappedLines.Draw(context, new ImmutableSolidColorBrush(FooterColor), band, IsEdge ? TextAlignment.Left : TextAlignment.Center, 0.5);
            }
            else if (Footer is { Length: > 0 } footer)
            {
                double size = FooterSize * u;
                var band = IsEdge ? EdgeFooter(FooterBand * u) : new Rect(panel.X + 24 * u, panel.Bottom - FooterBand * u, panel.Width - 48 * u, FooterBand * u);
                MenuRow.DrawLine(context, small, size, FontLayout.Cased(footer, FooterCase), band, band.Center.Y + MenuRow.CapHeight(small, size) / 2, IsEdge ? TextAlignment.Left : TextAlignment.Center, new ImmutableSolidColorBrush(FooterColor));
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Menu, () => Title ?? "");
    }
}
