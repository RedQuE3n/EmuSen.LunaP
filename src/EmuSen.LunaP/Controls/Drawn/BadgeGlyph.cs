using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A mark an entry of a list can carry, which BadgeGlyph draws when the host has no image for it.</summary>
    public enum BadgeKind
    {
        /// <summary>The entry is a favourite: a star on a plate.</summary>
        Favorite,
        /// <summary>The game was finished: a check mark on a plate.</summary>
        Completed,
        /// <summary>The game suits children: a smiling face on a plate.</summary>
        KidGame,
        /// <summary>The game does not work: a crack down a plate.</summary>
        Broken,
        /// <summary>A controller is named for the game: an empty plate, for a ControllerGlyph to sit on.</summary>
        Controller,
        /// <summary>Another program or engine runs the game: two opposed arrows on a plate.</summary>
        AltEmulator,
        /// <summary>The entry belongs to the set being edited: two stacked cards on a plate.</summary>
        Collection,
        /// <summary>The entry is a folder: a folder's outline on a plate.</summary>
        Folder,
        /// <summary>A manual exists for the game: an open book on a plate.</summary>
        Manual,
        /// <summary>A folder opens one of its entries directly: a chain link with no plate, drawn over a Folder badge.</summary>
        FolderLink,
    }

    /// <summary>The kind of controller a game is played with, which ControllerGlyph draws.</summary>
    public enum ControllerShape
    {
        /// <summary>Not known: a pad's outline with a question mark.</summary>
        Unknown,
        /// <summary>Any modern pad: a body with two grips and two sticks.</summary>
        Gamepad,
        /// <summary>A flat oblong pad with a cross and two round buttons.</summary>
        Nes,
        /// <summary>An oblong pad with round ends, a cross and four buttons in a diamond.</summary>
        Snes,
        /// <summary>A pad with three prongs and a stick on the middle one.</summary>
        Nintendo64,
    }

    // One badge drawn as the toolkit's own geometry in one colour - see docs/LunaP.md §180.
    /// <summary>One list badge, such as a favourite or a folder mark, drawn as the toolkit's own geometry in one colour.</summary>
    public class BadgeGlyph : Control
    {
        public static readonly StyledProperty<BadgeKind> KindProperty = AvaloniaProperty.Register<BadgeGlyph, BadgeKind>(nameof(Kind));
        public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<BadgeGlyph, Color>(nameof(Color), LunaPalette.Text.Color);
        public static readonly StyledProperty<double> GlyphSizeProperty = AvaloniaProperty.Register<BadgeGlyph, double>(nameof(GlyphSize), 24);

        static BadgeGlyph()
        {
            AffectsMeasure<BadgeGlyph>(GlyphSizeProperty);
            AffectsRender<BadgeGlyph>(KindProperty, ColorProperty);
        }

        /// <summary>Which badge is drawn. Favorite by default.</summary>
        public BadgeKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

        /// <summary>The one colour the drawing uses, the palette's text colour by default.</summary>
        public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        /// <summary>The side of the square the glyph measures to, in pixels. 24 by default.</summary>
        public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

        protected override Size MeasureOverride(Size availableSize) => new(GlyphSize, GlyphSize);

        public override void Render(DrawingContext context) => BadgeGlyphDrawing.Draw(context, new Rect(Bounds.Size), Kind, Color);

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image, () => Describe(Kind));

        /// <summary>What a screen reader hears for a badge.</summary>
        /// <param name="kind">The badge to name.</param>
        /// <returns>A short name, such as "Favorite" or "Folder link".</returns>
        public static string Describe(BadgeKind kind) => kind switch
        {
            BadgeKind.KidGame => "Kids' game",
            BadgeKind.AltEmulator => "Alternative emulator",
            BadgeKind.FolderLink => "Folder link",
            _ => kind.ToString(),
        };
    }

    // One controller drawn as the toolkit's own geometry in one colour, to stand on a Controller badge - see docs/LunaP.md §180.
    /// <summary>One controller, such as a flat oblong pad or a three-pronged one, drawn as the toolkit's own geometry in one colour.</summary>
    public class ControllerGlyph : Control
    {
        public static readonly StyledProperty<ControllerShape> ShapeProperty = AvaloniaProperty.Register<ControllerGlyph, ControllerShape>(nameof(Shape));
        public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<ControllerGlyph, Color>(nameof(Color), LunaPalette.Text.Color);
        public static readonly StyledProperty<double> GlyphSizeProperty = AvaloniaProperty.Register<ControllerGlyph, double>(nameof(GlyphSize), 24);

        static ControllerGlyph()
        {
            AffectsMeasure<ControllerGlyph>(GlyphSizeProperty);
            AffectsRender<ControllerGlyph>(ShapeProperty, ColorProperty);
        }

        /// <summary>Which controller is drawn. Unknown by default.</summary>
        public ControllerShape Shape { get => GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

        /// <summary>The one colour the drawing uses, the palette's text colour by default.</summary>
        public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        /// <summary>The side of the square the glyph measures to, in pixels. 24 by default.</summary>
        public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

        protected override Size MeasureOverride(Size availableSize) => new(GlyphSize, GlyphSize);

        public override void Render(DrawingContext context) => BadgeGlyphDrawing.Draw(context, new Rect(Bounds.Size), Shape, Color);

        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Image, () => Describe(Shape));

        /// <summary>What a screen reader hears for a controller.</summary>
        /// <param name="shape">The controller.</param>
        /// <returns>A short name, such as "NES controller" or "Unknown controller".</returns>
        public static string Describe(ControllerShape shape) => shape switch
        {
            ControllerShape.Gamepad => "Gamepad",
            ControllerShape.Nes => "NES controller",
            ControllerShape.Snes => "SNES controller",
            ControllerShape.Nintendo64 => "Nintendo 64 controller",
            _ => "Unknown controller",
        };
    }
}
