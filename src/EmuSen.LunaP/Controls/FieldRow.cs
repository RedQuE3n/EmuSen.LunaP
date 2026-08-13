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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HintProperty)
            {
                RaisePropertyChanged(HasHintProperty, !HasHint, HasHint);
            }

            // Swapping the content or renaming the field both invalidate the pairing below.
            if (change.Property == ContentProperty || change.Property == LabelProperty) LabelTheContent();
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group,
                name: () => Label, help: () => Hint);

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
