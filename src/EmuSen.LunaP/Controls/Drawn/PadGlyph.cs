using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The printing a gamepad carries on its buttons, which decides how a PadGlyph draws them.</summary>
    public enum PadFamily
    {
        /// <summary>A pad whose printing is not known: buttons are drawn by position.</summary>
        Generic,
        /// <summary>A, B, X and Y from the bottom clockwise, LB and RB, LT and RT.</summary>
        Xbox,
        /// <summary>A cross, a circle, a square and a triangle; L1 and R1, L2 and R2.</summary>
        PlayStation,
        /// <summary>B, A, Y and X from the bottom clockwise; L and R, ZL and ZR; plus and minus.</summary>
        Nintendo,
    }

    /// <summary>A gamepad button or group of buttons a hint can name, the face buttons by position.</summary>
    public enum PadGlyphButton
    {
        /// <summary>The bottom face button.</summary>
        South,
        /// <summary>The right face button.</summary>
        East,
        /// <summary>The left face button.</summary>
        West,
        /// <summary>The top face button.</summary>
        North,
        /// <summary>The whole directional pad.</summary>
        DPad,
        /// <summary>The directional pad's up and down.</summary>
        DPadUpDown,
        /// <summary>The directional pad's left and right.</summary>
        DPadLeftRight,
        /// <summary>The left shoulder button.</summary>
        LeftShoulder,
        /// <summary>The right shoulder button.</summary>
        RightShoulder,
        /// <summary>The left trigger.</summary>
        LeftTrigger,
        /// <summary>The right trigger.</summary>
        RightTrigger,
        /// <summary>The right-hand middle button (Start, Menu, Options, plus).</summary>
        Start,
        /// <summary>The left-hand middle button (Select, View, Create, minus).</summary>
        Select,
        /// <summary>The button in the middle of the pad that belongs to the system.</summary>
        Guide,
    }

    // One gamepad button drawn in one colour as the toolkit's own geometry, in the set for a family of pads - see docs/LunaP.md §103.
    //
    // The same drawings a HintBar uses for an entry that names a button and gives no image file. It measures a square
    // GlyphSize on a side, so it can stand beside text of any size.
    /// <summary>One gamepad button drawn as the toolkit's own geometry in one colour, in the set for a family of pads.</summary>
    public class PadGlyph : Control
    {
        public static readonly StyledProperty<PadFamily> FamilyProperty = AvaloniaProperty.Register<PadGlyph, PadFamily>(nameof(Family));
        public static readonly StyledProperty<PadGlyphButton> ButtonProperty = AvaloniaProperty.Register<PadGlyph, PadGlyphButton>(nameof(Button));
        public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<PadGlyph, Color>(nameof(Color), LunaPalette.Text.Color);
        public static readonly StyledProperty<double> GlyphSizeProperty = AvaloniaProperty.Register<PadGlyph, double>(nameof(GlyphSize), 24);

        static PadGlyph()
        {
            AffectsMeasure<PadGlyph>(GlyphSizeProperty);
            AffectsRender<PadGlyph>(FamilyProperty, ButtonProperty, ColorProperty);
        }

        /// <summary>Which set the button is drawn from. Generic by default.</summary>
        public PadFamily Family { get => GetValue(FamilyProperty); set => SetValue(FamilyProperty, value); }

        /// <summary>Which button is drawn. South by default.</summary>
        public PadGlyphButton Button { get => GetValue(ButtonProperty); set => SetValue(ButtonProperty, value); }

        /// <summary>The one colour the drawing uses, the palette's text colour by default.</summary>
        public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        /// <summary>The side of the square the glyph measures to, in pixels. 24 by default.</summary>
        public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

        protected override Size MeasureOverride(Size availableSize) => new(GlyphSize, GlyphSize);

        public override void Render(DrawingContext context) => PadGlyphDrawing.Draw(context, new Rect(Bounds.Size), Family, Button, Color);

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Image, () => PadGlyph.Describe(Family, Button));

        /// <summary>What a screen reader hears for a button of a family: its printed name where the family prints one, else its position.</summary>
        /// <param name="family">The pad's family.</param>
        /// <param name="button">The button, face buttons by position.</param>
        /// <returns>A short name, such as "A", "Cross", "LB" or "South button".</returns>
        public static string Describe(PadFamily family, PadGlyphButton button) => button switch
        {
            PadGlyphButton.South or PadGlyphButton.East or PadGlyphButton.West or PadGlyphButton.North => family switch
            {
                PadFamily.PlayStation => button switch { PadGlyphButton.South => "Cross", PadGlyphButton.East => "Circle", PadGlyphButton.West => "Square", _ => "Triangle" },
                PadFamily.Generic => $"{button} button",
                _ => PadGlyphDrawing.FaceLetter(family, button)!,
            },
            PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder or PadGlyphButton.LeftTrigger or PadGlyphButton.RightTrigger => PadGlyphDrawing.ShoulderLabel(family, button),
            PadGlyphButton.DPad => "D-pad",
            PadGlyphButton.DPadUpDown => "D-pad up and down",
            PadGlyphButton.DPadLeftRight => "D-pad left and right",
            PadGlyphButton.Start => family switch { PadFamily.Xbox => "Menu", PadFamily.PlayStation => "Options", PadFamily.Nintendo => "Plus", _ => "Start" },
            PadGlyphButton.Select => family switch { PadFamily.Xbox => "View", PadFamily.PlayStation => "Create", PadFamily.Nintendo => "Minus", _ => "Select" },
            _ => "Guide",
        };
    }
}
