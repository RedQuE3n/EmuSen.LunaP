using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // "There is nothing here, and here is why" - see docs/LunaP.md §22.9.
    //
    // Three places build this by hand, and one of them says in a comment why the kit's nearest
    // existing seam was the wrong one: CoretopWindow declares a plain TextBlock as "muted but
    // body-sized, not a HintText: this one is the window's whole content when no core is loaded".
    // That is the distinction this control exists for. A HintText is an aside under something
    // else and is 11pt by definition; an empty state IS the something else.
    //
    // Message and Detail rather than one string, because every hand-rolled version wanted both:
    // what is missing, and what to do about it. Detail is optional and hidden when empty, so a
    // bare "No results" does not leave a gap where a second line would be.
    /// <summary>A centred message saying that there is nothing to show, and why.</summary>
    public class EmptyState : TemplatedControl
    {
        public static readonly StyledProperty<string> MessageProperty =
            AvaloniaProperty.Register<EmptyState, string>(nameof(Message), string.Empty);

        public static readonly StyledProperty<string> DetailProperty =
            AvaloniaProperty.Register<EmptyState, string>(nameof(Detail), string.Empty);

        /// <summary>The main line: what is not here, in a few words.</summary>
        public string Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        // The second line: what would put something here. Hidden when empty.
        /// <summary>A second, smaller line saying what to do about it. Empty collapses it.</summary>
        public string Detail
        {
            get => GetValue(DetailProperty);
            set => SetValue(DetailProperty, value);
        }

        /// <summary>Whether a detail line was given, which is what collapses it when none was.</summary>
        public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // The template binds a part's visibility to HasDetail, which is not a styled property
            // and therefore does not raise anything of its own; this is what tells it to look again.
            if (change.Property == DetailProperty) RaisePropertyChanged(HasDetailProperty, default, default);
        }

        // A direct property so the template can bind to it. Read-only: Detail is the input, this is
        // the derived answer, and letting a caller set it would let the two disagree.
        public static readonly DirectProperty<EmptyState, bool> HasDetailProperty =
            AvaloniaProperty.RegisterDirect<EmptyState, bool>(nameof(HasDetail), o => o.HasDetail);

        // THE SHARPEST CASE IN THE WHOLE ACCESSIBILITY PASS, and worth stating plainly: before
        // this, the one control whose entire job is to explain why a window is empty was the one
        // thing a screen reader could not see. Both its lines are template parts, so Avalonia hid
        // them expecting the control to speak for them, and the control had no peer to speak with.
        // A sighted user got "No cores loaded - open a ROM to begin"; a screen reader got silence
        // and an apparently empty window. Measured in docs/LunaP.md §24.1.
        //
        // Text rather than Group: this IS the content, which is the same distinction §22.9 drew
        // when it refused to make an empty state a HintText.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Text,
                name: () => Message, help: () => Detail);
    }
}
