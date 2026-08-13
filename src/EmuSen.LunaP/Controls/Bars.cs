using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A right-aligned run of buttons. Buttons go in as ordinary children - see docs/LunaP.md §5.5.
    public class ButtonBar : ItemsControl
    {
        // ToolBar, not List, and the correction is worth the line. ItemsControl's stock peer reports
        // AutomationControlType.List, so a row of OK/Cancel buttons announced as a list of two
        // items - inviting a reader to navigate it as data rather than to press one of them.
        // ToolBar is UIA's name for exactly this: a run of commands. See docs/LunaP.md §24.2.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ToolBar);
    }

    // The bottom strip: a status message on the left, a ButtonBar's worth of actions on the right.
    public class StatusBar : ContentControl
    {
        public static readonly StyledProperty<string> StatusProperty =
            AvaloniaProperty.Register<StatusBar, string>(nameof(Status), string.Empty);

        public string Status
        {
            get => GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        // The status line is the one place in the toolkit where text arrives to be READ RATHER THAN
        // FOUND: "Applied 12 cheats", "Save State failed". A sighted user gets that from the corner
        // of their eye without going to look for it, and the equivalent is a live region - which is
        // set on this control in Theme/Controls/Bars.axaml rather than here, because it is a default a
        // caller may want to turn off (a status line updating twice a second is a live region that
        // never shuts up) and Avalonia's attached property is the way to say so.
        //
        // Status is the NAME rather than the item status, because for this control the message is
        // what the thing is. There is nothing else it could be called.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.StatusBar, name: () => Status);
    }
}
