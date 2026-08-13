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
}
