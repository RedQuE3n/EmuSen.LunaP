using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // The three text idioms the frontends retyped per window; all styling lives in Theme/Controls/TextControls.axaml - see docs/LunaP.md §5.1.
    /// <summary>A bold coloured heading introducing a group of controls.</summary>
    public class SectionHeader : TextBlock
    {
    }

    // Grey 11pt wrapping explanatory text, under a label or a checkbox.
    /// <summary>Small muted wrapping text, for an explanation under a label or a checkbox.</summary>
    public class HintText : TextBlock
    {
    }

    // Body text in the monospace stack, for register dumps and runtime figures.
    /// <summary>Body text in the monospace stack, for register dumps and runtime figures.</summary>
    public class MonoText : TextBlock
    {
    }

    // What is wrong with the field above it - see docs/LunaP.md §49.
    //
    // A FOURTH IDIOM RATHER THAN A HintText IN LunaError, and the distinction is the whole reason
    // this type exists. A hint is advice and is there whether or not anything is wrong; an error is
    // a consequence and appears because something is. They wrap the same way and sit in the same
    // place, so the only thing telling a reader which one they are looking at is the colour - which
    // means a host restyling "the small text under a field" through CSS would silently restyle the
    // error message too if they shared a type (§12.2).
    //
    // It is NOT muted. Every other small line in this kit is grey because it is secondary; this one
    // is the reason the field is refusing to be saved, and the one line on the page a reader must
    // not skim past.
    /// <summary>Small wrapping text in the error colour, saying what is wrong with the field above it.</summary>
    public class ErrorText : TextBlock
    {
    }
}
