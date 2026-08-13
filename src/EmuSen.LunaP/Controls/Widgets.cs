using System;
using System.Collections;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // Avalonia's ToggleSwitch, themed - see docs/LunaP.md §14.1.
    public class LunaSwitch : ToggleSwitch
    {
        // Without this the Fluent ToggleSwitch theme never reaches a subclass and ToggleSwitch.OnApplyTemplate throws on PART_MovingKnobs - see §14.1.
        protected override Type StyleKeyOverride => typeof(ToggleSwitch);

        // AND THIS IS THE OTHER HALF OF THAT BARGAIN, which was missing until §30. Avalonia matches
        // a type selector against the STYLE KEY, so the line above also stops `luna|LunaSwitch` from
        // ever selecting this control - our own styles and a host's CSS rule alike, silently. The
        // class is what a selector can still name: `ToggleSwitch.luna-switch` reaches this and not a
        // stock ToggleSwitch. Kept as a const because the CSS vocabulary and the XAML both spell it.
        public const string StyleClass = "luna-switch";

        public LunaSwitch() => Classes.Add(StyleClass);

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<LunaSwitch, string>(nameof(Label), string.Empty);

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != LabelProperty) return;

            // Both states, not Content: that puts the text beside the knob and keeps it there, which is
            // the line a CheckBox already drew. Content would stack it above, and the stock On/Off captions
            // say nothing a switch's own position does not. See docs/LunaP.md §14.1.
            OnContent = Label;
            OffContent = Label;
        }

        // AND HERE IS WHAT THAT DECISION COST, FOUND BY MEASURING RATHER THAN BY READING IT BACK.
        //
        // Avalonia's ToggleButtonAutomationPeer takes its name from Content. LunaSwitch puts the
        // label in OnContent and OffContent and leaves Content null - so a switch with a perfectly
        // good visible label announced as an unnamed button. "Button" and nothing else, for every
        // switch on a settings page. Measured in docs/LunaP.md §24.1.
        //
        // The §14.1 decision was still right; it just had a consequence nobody looked for. The peer
        // is where it gets paid, not the layout: the label goes back to being the accessible name
        // without moving the text on screen.
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaSwitchAutomationPeer(this);
    }

    // Subclassed from Avalonia's own rather than built on LunaAutomationPeer, and the reason is the
    // toggle pattern: ToggleButtonAutomationPeer implements IToggleProvider, which is how assistive
    // technology reads the switch's state and flips it. A LunaAutomationPeer would report a nicely
    // named control that nothing could tell was on or off, which trades one silence for another.
    internal sealed class LunaSwitchAutomationPeer : ToggleButtonAutomationPeer
    {
        public LunaSwitchAutomationPeer(LunaSwitch owner) : base(owner)
        {
        }

        // base first, so AutomationProperties.Name and LabeledBy still win - the same precedence
        // LunaAutomationPeer keeps, for the same reason.
        protected override string? GetNameCore()
        {
            string? explicitly = base.GetNameCore();
            if (!string.IsNullOrWhiteSpace(explicitly)) return explicitly;

            return Owner is LunaSwitch { Label: { Length: > 0 } label } ? label : null;
        }
    }

    // Avalonia's ComboBox, themed, with the selection reported as a plain callback rather than an event-args dance.
    public class Dropdown : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(ComboBox);

        // What a selector can name once the line above has taken the type away - see LunaSwitch and §30.
        public const string StyleClass = "luna-dropdown";

        // Raised only for a real user choice, never for the selection set while filling the list.
        public event Action<object?>? Chose;

        // Was a bare bool, and is a Suppressor now that the general form of this guard exists in
        // the kit - six more copies of it across two applications are what argued it in
        // (docs/LunaP.md §21.1). Behaviour is identical for a single Fill; what changes is that a
        // nested one can no longer re-enable Chose halfway through the outer one.
        private readonly Suppressor _filling = new();

        public Dropdown()
        {
            Classes.Add(StyleClass);
            SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(SelectedItem);
            };
        }

        // Replaces the items and the selection together, without Chose firing for the reset.
        public void Fill(IEnumerable items, object? selected)
        {
            using (_filling.Suppress())
            {
                ItemsSource = items;
                SelectedItem = selected;
            }
        }
    }

    // Avalonia's TabControl, themed, with the "append a tab" chore the frontends both hand-wrote.
    public class Tabs : TabControl
    {
        protected override Type StyleKeyOverride => typeof(TabControl);

        // What a selector can name once the line above has taken the type away - see LunaSwitch and §30.
        public const string StyleClass = "luna-tabs";

        public Tabs() => Classes.Add(StyleClass);

        public TabItem Add(string header, Control content)
        {
            var tab = new TabItem { Header = header, Content = content };
            Items.Add(tab);
            return tab;
        }

        // Everything after the tabs declared up front, for a list rebuilt when the console set changes.
        public void RemoveFrom(int index)
        {
            while (Items.Count > index) Items.RemoveAt(Items.Count - 1);
        }
    }
}
