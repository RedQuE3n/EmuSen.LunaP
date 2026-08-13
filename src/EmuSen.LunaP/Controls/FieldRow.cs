using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A settings field: bold label, optional grey explanation, then whatever control the caller puts in Content - see docs/LunaP.md §5.4.
    /// <summary>A settings field: a bold label, an optional explanation, and whatever control the caller supplies.</summary>
    public class FieldRow : ContentControl
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<FieldRow, string>(nameof(Label), string.Empty);

        public static readonly StyledProperty<string> HintProperty =
            AvaloniaProperty.Register<FieldRow, string>(nameof(Hint), string.Empty);

        public static readonly DirectProperty<FieldRow, bool> HasHintProperty =
            AvaloniaProperty.RegisterDirect<FieldRow, bool>(nameof(HasHint), o => o.HasHint);

        // WHAT IS WRONG WITH THIS FIELD, AND A STRING RATHER THAN A BOOL, because a field that is
        // invalid without saying why is a field the user cannot fix. There is no separate
        // IsValid: the message IS the state, so the two can never disagree - a control carrying
        // both would eventually be set invalid with an empty message, or valid with a stale one.
        //
        // NOT WIRED TO DataValidationErrors, which is Avalonia's binding-driven mechanism and the
        // obvious thing to reach for. It is the wrong shape here twice over: it reports errors
        // raised by a BINDING, and LunaP's controls are built in code and given values directly
        // (§5.2), so there is frequently no binding to raise one. And it would put the decision
        // about what counts as invalid inside the control, when the only thing that knows a
        // ROM directory must exist is the application. String in, string out, caller decides -
        // the same seam LunaTable's Validate uses, so an invalid field and an invalid cell are
        // one idea (§49).
        public static readonly StyledProperty<string> ErrorProperty =
            AvaloniaProperty.Register<FieldRow, string>(nameof(Error), string.Empty);

        public static readonly DirectProperty<FieldRow, bool> HasErrorProperty =
            AvaloniaProperty.RegisterDirect<FieldRow, bool>(nameof(HasError), o => o.HasError);

        private TextBlock? _labelBlock;

        /// <summary>The label to the left of the field.</summary>
        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        // Left empty, the hint collapses rather than reserving blank space.
        /// <summary>An explanation shown under the field. Empty collapses the row rather than leaving a blank line.</summary>
        public string Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        /// <summary>Whether a hint was given, which is what collapses the hint row when none was.</summary>
        public bool HasHint => !string.IsNullOrEmpty(Hint);

        // Left empty, the field is valid and the message collapses - the common case stays quiet
        // and a caller never has to say "no error" out loud.
        /// <summary>What is wrong with this field. Empty means valid, and collapses the message rather than leaving a blank line.</summary>
        public string Error
        {
            get => GetValue(ErrorProperty);
            set => SetValue(ErrorProperty, value);
        }

        /// <summary>Whether this field is currently invalid, which is exactly whether an error message was given.</summary>
        public bool HasError => !string.IsNullOrEmpty(Error);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HintProperty)
            {
                RaisePropertyChanged(HasHintProperty, !HasHint, HasHint);
            }

            if (change.Property == ErrorProperty)
            {
                RaisePropertyChanged(HasErrorProperty, !HasError, HasError);
            }

            // Swapping the content or renaming the field both invalidate the pairing below.
            if (change.Property == ContentProperty || change.Property == LabelProperty) LabelTheContent();
        }

        // THE ERROR GOES IN ItemStatus BECAUSE AVALONIA HAS NOWHERE BETTER, and that is a measured
        // statement rather than a shrug. Reflecting over Avalonia 12.1.0's automation surface -
        // every overridable member of AutomationPeer, every attached property on
        // AutomationProperties, every interface in Avalonia.Automation.Provider - returns ZERO
        // members whose name contains "Valid" or "Error". UIA has IsDataValidForForm; Avalonia does
        // not expose it. So there is no way to tell a screen reader "this field is invalid" as a
        // state, and the choice is only about which sentence carries the message.
        //
        // ItemStatus and not HelpText, because HelpText is already the hint - advice that is true
        // whether or not anything is wrong - and overwriting it when a field goes invalid would
        // destroy the explanation at the moment it is most useful. ItemStatus is defined as the
        // state that is not the value, which is what an error message is, and MeterRow already
        // uses it for exactly that shape of thing (§24.2).
        //
        // WHAT THIS DOES NOT DO, recorded as a hazard rather than claimed: nothing here makes a
        // reader announce the error the moment it appears. AutomationProperties.LiveSetting exists
        // and would be the mechanism, but the message is a template part, and template parts are
        // outside the control view a reader navigates - so whether a live region on one would
        // announce at all is unverified, and this suite cannot verify it. A reader that visits the
        // field gets the message; a reader that has moved on is not interrupted. §49.3.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group,
                name: () => Label, help: () => Hint, status: () => Error);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _labelBlock = e.NameScope.Find<TextBlock>("PART_Label");
            LabelTheContent();
        }

        // THE FIELD'S LABEL IS A SIBLING OF THE THING IT LABELS, WHICH IS THE PROBLEM.
        //
        // A caller writes `new FieldRow { Label = "Save folder", Content = new TextBox() }`. The
        // TextBox is what the keyboard lands on, and it has no name of its own; the words "Save
        // folder" live in a TextBlock next door, which a screen reader has no reason to associate
        // with it. Measured before this: tabbing through a settings window reached five text boxes
        // and every one announced as an unnamed edit field (§24.1).
        //
        // LabeledBy rather than writing Name onto the caller's control, because it does not
        // overwrite anything: Avalonia falls back to the labelled-by peer's name only when the
        // control has no name of its own, so a caller who has already named their TextBox keeps it.
        // Setting Name directly would silently win that argument, and the caller would have no way
        // to say "no, mine".
        //
        // The guard is `is Control`: Content takes an object, and a string or a shape has nothing
        // to attach a property to.
        private void LabelTheContent()
        {
            if (_labelBlock is null || Content is not Control content) return;

            AutomationProperties.SetLabeledBy(content, _labelBlock);
        }
    }
}
