using Avalonia.Automation;
using Avalonia.Controls;

namespace EmuSen.LunaP.Fluent
{
    // The accessibility half of the fluent surface - see docs/LunaP.md §24.3.
    //
    // §9's rule is that every name here is the XAML attribute it sets, so that building a window in
    // code and building one in XAML stay one vocabulary. These follow it, with one forced exception
    // noted below: the toolkit has always shipped a `.Name(...)` that sets `Control.Name`, and
    // `AutomationProperties.Name` cannot also be `.Name`.
    //
    // Why this exists at all, when a caller could write `AutomationProperties.SetName(box, "...")`:
    // because they demonstrably do not. Across three repositories and four applications the
    // attached form was used ZERO times (§21.4), and a fluent chain that breaks to call a static
    // setter on the line between `.Width(200)` and `.Margin(8)` is a chain a person stops writing.
    // Naming a control has to cost one call in the middle of a builder or it does not get done.
    public static class AccessibilityExtensions
    {
        // AutomationProperties.Name: what a screen reader calls this control.
        //
        // NOT `.Name(...)`, which is taken and sets the x:Name used for template and namescope
        // lookup - two entirely different things that would be one word apart. `Accessible` is the
        // prefix rather than `Automation` because the point of the property is a person using the
        // control, not the test framework that shares the API.
        //
        // Should be what the control's visible label says, where it has one. A name that does not
        // contain the visible text breaks voice control: somebody saying "click save" needs "Save"
        // to be in the name of the button that says Save.
        public static T AccessibleName<T>(this T control, string name) where T : Control
        {
            AutomationProperties.SetName(control, name);
            return control;
        }

        // AutomationProperties.HelpText: the sentence after the name. For what a control does when
        // the name says only what it is - "Browse..." and "chooses where save states are written".
        public static T HelpText<T>(this T control, string help) where T : Control
        {
            AutomationProperties.SetHelpText(control, help);
            return control;
        }

        // AutomationProperties.LabeledBy: borrow the name from a label that is already on screen.
        //
        // Better than AccessibleName wherever a visible label exists, because there is then one
        // string rather than two, and the two cannot drift apart when somebody edits the visible
        // one. FieldRow does this internally for its own content.
        public static T LabeledBy<T>(this T control, Control label) where T : Control
        {
            AutomationProperties.SetLabeledBy(control, label);
            return control;
        }

        // AutomationProperties.LiveSetting: announce changes to this control without the user
        // having to go and look.
        //
        // Polite waits for a pause; Assertive interrupts. Reach for Polite - Assertive on anything
        // that updates more than rarely makes an application unusable with a screen reader on,
        // which is a failure mode worse than the silence it was meant to fix. StatusBar sets Polite
        // for itself.
        public static T LiveRegion<T>(this T control, AutomationLiveSetting setting = AutomationLiveSetting.Polite)
            where T : Control
        {
            AutomationProperties.SetLiveSetting(control, setting);
            return control;
        }

        // AutomationProperties.AccessibilityView = Raw: hide this control from the control view.
        //
        // For decoration that would otherwise be announced - a separator, a spacer, an icon beside
        // a label that already says the same word. It hides the control and not its children, so it
        // is not a way to hide a subtree.
        public static T Decorative<T>(this T control) where T : Control
        {
            AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
            return control;
        }
    }
}
