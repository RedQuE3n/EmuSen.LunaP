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
    /// <summary>Fluent setters for the properties that make a control reachable by a screen reader.</summary>
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
        /// <summary>Gives the control the name a screen reader announces, for when its visible content is a glyph or nothing at all.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to name.</param>
        /// <param name="name">What a reader should say. A word, not a sentence - help text is where a sentence goes.</param>
        /// <returns>The same control, so calls can be chained.</returns>
        public static T AccessibleName<T>(this T control, string name) where T : Control
        {
            AutomationProperties.SetName(control, name);
            return control;
        }

        // AutomationProperties.HelpText: the sentence after the name. For what a control does when
        // the name says only what it is - "Browse..." and "chooses where save states are written".
        /// <summary>Gives the control the sentence a reader offers after the name.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control to describe.</param>
        /// <param name="help">The explanation. This is what tells several identically named buttons apart.</param>
        /// <returns>The same control, so calls can be chained.</returns>
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
        /// <summary>Points the control at the control that labels it, so a reader announces the label when focus lands.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control being labelled, usually an input.</param>
        /// <param name="label">The control holding the label text.</param>
        /// <returns>The same control, so calls can be chained.</returns>
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
        /// <summary>Marks the control as one whose changes should be announced without focus moving to it.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The control that changes on its own - a status line, say.</param>
        /// <param name="setting">How insistently to announce. Polite waits for a pause; Assertive interrupts, and re-reads the whole region rather than what changed.</param>
        /// <returns>The same control, so calls can be chained.</returns>
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
        /// <summary>Hides the control from the accessibility tree, for something that carries no information a reader needs.</summary>
        /// <typeparam name="T">The control type, preserved so chaining keeps the concrete type.</typeparam>
        /// <param name="control">The decoration - a rule, a spacer, an icon beside a label that already says it.</param>
        /// <returns>The same control, so calls can be chained.</returns>
        public static T Decorative<T>(this T control) where T : Control
        {
            AutomationProperties.SetAccessibilityView(control, AccessibilityView.Raw);
            return control;
        }
    }
}
