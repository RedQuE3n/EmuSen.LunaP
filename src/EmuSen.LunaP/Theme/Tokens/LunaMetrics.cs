using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Theme
{
    /// <summary>Which rendering of the interface a set of metrics is for.</summary>
    public enum LunaForm
    {
        /// <summary>A desktop window, in device pixels at 1×.</summary>
        Desktop,
        /// <summary>The big screen and the handheld, in design units: a pixel at 1280×800, scaled with the screen's height.</summary>
        BigScreen,
    }

    /// <summary>The interface's type scale for one form: the size of each role, in that form's units.</summary>
    /// <param name="Display">The largest words: a game's name on Home or its page.</param>
    /// <param name="Title">A screen's or a card's title.</param>
    /// <param name="Strong">A label that must stand out from body text.</param>
    /// <param name="Row">A menu row's label and value.</param>
    /// <param name="Body">Running text and a setting's sentence.</param>
    /// <param name="Caption">Status lines, facts and the help bar's words.</param>
    /// <param name="Glyph">A lettered pad glyph.</param>
    public sealed record LunaTypeScale(double Display, double Title, double Strong, double Row, double Body, double Caption, double Glyph);

    /// <summary>The interface's spacing for one form, in that form's units.</summary>
    /// <param name="Base">The grid every other value is a multiple of.</param>
    /// <param name="RowHeight">A row of a list or a menu.</param>
    /// <param name="ControlHeight">The least height of anything pressed or touched.</param>
    /// <param name="PaneGutter">The margin inside a pane.</param>
    /// <param name="PaneGap">The gap between two panes.</param>
    /// <param name="SafeArea">What is kept clear at the screen's edges.</param>
    /// <param name="CardWidth">A card's panel.</param>
    /// <param name="TitleRow">A title row's height.</param>
    /// <param name="HelpBar">The help bar's height.</param>
    /// <param name="TileWidth">A cover tile's width.</param>
    /// <param name="TileHeight">A cover tile's height.</param>
    /// <param name="TileGap">The gap between tiles.</param>
    /// <param name="RowRadius">The rounding of rows and buttons.</param>
    /// <param name="TileRadius">The rounding of tiles and cards.</param>
    public sealed record LunaSpacing(double Base, double RowHeight, double ControlHeight, double PaneGutter, double PaneGap, Thickness SafeArea,
        double CardWidth, double TitleRow, double HelpBar, double TileWidth, double TileHeight, double TileGap, double RowRadius, double TileRadius);

    /// <summary>How the focus is marked, in the form's units: the bar on a focused row and the ring around a focused tile.</summary>
    /// <param name="RingThickness">The ring's stroke.</param>
    /// <param name="RingOffset">How far outside the item the ring is drawn.</param>
    /// <param name="BarWidth">The accent bar at a focused row's left edge.</param>
    /// <param name="TileScale">How much a focused tile grows.</param>
    /// <param name="HoverBorder">The border a pointer's hover draws instead of the ring.</param>
    public sealed record LunaFocus(double RingThickness, double RingOffset, double BarWidth, double TileScale, double HoverBorder);

    // The interface's non-colour tokens as resources for one form; values are LunaTokens' - see docs/LunaP.md §206.
    /// <summary>A resource dictionary of the interface's type scale, typefaces, spacing and focus metrics for one form.</summary>
    public sealed class LunaMetrics : ResourceDictionary
    {
        private LunaForm _form;

        /// <summary>A dictionary of the desktop's metrics, filled again when the text scale changes.</summary>
        public LunaMetrics()
        {
            Fill();
            var self = new WeakReference<LunaMetrics>(this);
            void Refill()
            {
                if (self.TryGetTarget(out LunaMetrics? metrics)) metrics.Fill();
                else LunaTokens.Changed -= Refill;
            }
            LunaTokens.Changed += Refill;
        }

        /// <summary>The form whose metrics the dictionary holds. Desktop by default.</summary>
        public LunaForm Form
        {
            get => _form;
            set { _form = value; Fill(); }
        }

        private void Fill()
        {
            LunaTypeScale type = LunaTokens.Type(_form);
            LunaSpacing space = LunaTokens.Spacing(_form);
            LunaFocus focus = LunaTokens.Focus(_form);
            this["LunaDisplayFontSize"] = type.Display;
            this["LunaTitleFontSize"] = type.Title;
            this["LunaStrongFontSize"] = type.Strong;
            this["LunaRowFontSize"] = type.Row;
            this["LunaBodyFontSize"] = type.Body;
            this["LunaCaptionFontSize"] = type.Caption;
            this["LunaGlyphFontSize"] = type.Glyph;
            this["LunaHintFontSize"] = type.Caption;
            this["ControlContentThemeFontSize"] = type.Body;
            this["ContentControlThemeFontFamily"] = LunaFonts.Body;
            this["LunaHeaderFontSize"] = LunaTokens.HeaderSize(_form);
            this["LunaDisplayFontFamily"] = LunaFonts.Condensed;
            this["LunaTitleFontFamily"] = LunaFonts.Condensed;
            this["LunaBodyFontFamily"] = LunaFonts.Body;
            this["LunaDisplayFontWeight"] = FontWeight.Medium;
            this["LunaRowHeight"] = space.RowHeight;
            this["LunaControlHeight"] = space.ControlHeight;
            this["LunaPaneGutter"] = space.PaneGutter;
            this["LunaPaneGap"] = space.PaneGap;
            this["LunaSafeArea"] = space.SafeArea;
            this["LunaCardWidth"] = space.CardWidth;
            this["LunaTitleRowHeight"] = space.TitleRow;
            this["LunaHelpBarHeight"] = space.HelpBar;
            this["LunaTileWidth"] = space.TileWidth;
            this["LunaTileHeight"] = space.TileHeight;
            this["LunaTileGap"] = space.TileGap;
            this["LunaRowRadius"] = new CornerRadius(space.RowRadius);
            this["LunaTileRadius"] = new CornerRadius(space.TileRadius);
            this["LunaFocusRingThickness"] = new Thickness(focus.RingThickness);
            this["LunaFocusRingOffset"] = focus.RingOffset;
            this["LunaFocusBarWidth"] = focus.BarWidth;
            this["LunaFocusTileScale"] = focus.TileScale;
            this["LunaFocusRingRadius"] = new CornerRadius(space.TileRadius + focus.RingOffset);
        }
    }

    // The values of the interface's type scale, spacing and focus, written once - see docs/LunaP.md §206.2 to §206.4.
    /// <summary>The interface's type scale, spacing and focus metrics for each form, the one place their values are written.</summary>
    public static class LunaTokens
    {
        private static readonly LunaTypeScale BigScreenBase = new(Display: 44, Title: 32, Strong: 26, Row: 26, Body: 22, Caption: 20, Glyph: 22);
        private static readonly LunaTypeScale DesktopBase = new(Display: 24, Title: 16, Strong: 14, Row: 13, Body: 13, Caption: 11, Glyph: 13);
        private static double _textScale = 1;
        private static LunaTypeScale _bigScreen = BigScreenBase, _desktop = DesktopBase;

        /// <summary>The largest text scale.</summary>
        public const double MaxTextScale = 1.3;

        /// <summary>How much every size of the type scales is multiplied by, 1 to 1.3; 1 by default. Setting it raises Changed - see docs/LunaP.md §214.</summary>
        public static double TextScale
        {
            get => _textScale;
            set
            {
                double scale = double.IsFinite(value) ? Math.Clamp(value, 1, MaxTextScale) : 1;
                if (scale == _textScale) return;
                _textScale = scale;
                _bigScreen = Scaled(BigScreenBase, scale);
                _desktop = Scaled(DesktopBase, scale);
                Changed?.Invoke();
            }
        }

        /// <summary>Raised when the text scale changes, after the type scales have.</summary>
        public static event Action? Changed;

        // Each size times the scale, to the nearest half unit.
        private static LunaTypeScale Scaled(LunaTypeScale t, double s)
        {
            static double R(double v) => Math.Round(v * 2, MidpointRounding.AwayFromZero) / 2;
            return new(R(t.Display * s), R(t.Title * s), R(t.Strong * s), R(t.Row * s), R(t.Body * s), R(t.Caption * s), R(t.Glyph * s));
        }

        /// <summary>The big screen's type scale, in design units, at the text scale.</summary>
        public static LunaTypeScale BigScreenType => _bigScreen;

        /// <summary>The desktop's type scale, in pixels, at the text scale.</summary>
        public static LunaTypeScale DesktopType => _desktop;

        /// <summary>The big screen's spacing, in design units.</summary>
        public static readonly LunaSpacing BigScreenSpacing = new(Base: 4, RowHeight: 54, ControlHeight: 44, PaneGutter: 24, PaneGap: 32,
            SafeArea: new Thickness(64, 40), CardWidth: 512, TitleRow: 100, HelpBar: 58, TileWidth: 180, TileHeight: 240, TileGap: 24, RowRadius: 4, TileRadius: 8);

        /// <summary>The desktop's spacing, in pixels.</summary>
        public static readonly LunaSpacing DesktopSpacing = new(Base: 4, RowHeight: 24, ControlHeight: 28, PaneGutter: 24, PaneGap: 16,
            SafeArea: new Thickness(0), CardWidth: 512, TitleRow: 28, HelpBar: 24, TileWidth: 100, TileHeight: 133, TileGap: 16, RowRadius: 4, TileRadius: 6);

        /// <summary>The focus marks, the same in both forms' units.</summary>
        public static readonly LunaFocus FocusMarks = new(RingThickness: 3, RingOffset: 4, BarWidth: 4, TileScale: 1.06, HoverBorder: 1);

        /// <summary>The share of the screen's width and height kept clear at its edges on the big screen.</summary>
        public const double SafeAreaFraction = 0.05;

        /// <summary>The type scale of a form.</summary>
        /// <param name="form">The form asked about.</param>
        /// <returns>The form's type scale.</returns>
        public static LunaTypeScale Type(LunaForm form) => form == LunaForm.BigScreen ? BigScreenType : DesktopType;

        /// <summary>The spacing of a form.</summary>
        /// <param name="form">The form asked about.</param>
        /// <returns>The form's spacing.</returns>
        public static LunaSpacing Spacing(LunaForm form) => form == LunaForm.BigScreen ? BigScreenSpacing : DesktopSpacing;

        /// <summary>The focus marks of a form.</summary>
        /// <param name="form">The form asked about.</param>
        /// <returns>The form's focus marks.</returns>
        public static LunaFocus Focus(LunaForm form) => FocusMarks;

        // A section heading's size before the header role replaces it: Strong on the desktop, the menus' 28 on the big screen (§206.2).
        /// <summary>The size LunaHeaderFontSize keeps in a form: the desktop's Strong, and on the big screen the menus' section heading.</summary>
        /// <param name="form">The form asked about.</param>
        /// <returns>The size in the form's units.</returns>
        public static double HeaderSize(LunaForm form) => form == LunaForm.BigScreen ? 28 : DesktopType.Strong;

        /// <summary>The safe area of a screen of a given size on the big screen: 5% of each dimension.</summary>
        /// <param name="size">The screen's size.</param>
        /// <returns>The margins to keep clear.</returns>
        public static Thickness SafeAreaOf(Size size) =>
            new(Math.Round(size.Width * SafeAreaFraction), Math.Round(size.Height * SafeAreaFraction));
    }
}
