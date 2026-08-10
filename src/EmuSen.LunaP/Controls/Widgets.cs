using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // Avalonia's ToggleSwitch, themed - see docs/LunaP.md §14.1.
    public class LunaSwitch : ToggleSwitch
    {
        // Without this the Fluent ToggleSwitch theme never reaches a subclass and ToggleSwitch.OnApplyTemplate throws on PART_MovingKnobs - see §14.1.
        protected override Type StyleKeyOverride => typeof(ToggleSwitch);

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
    }

    // Avalonia's ComboBox, themed, with the selection reported as a plain callback rather than an event-args dance.
    public class Dropdown : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(ComboBox);

        // Raised only for a real user choice, never for the selection set while filling the list.
        public event Action<object?>? Chose;

        // Was a bare bool, and is a Suppressor now that the general form of this guard exists in
        // the kit - six more copies of it across two applications are what argued it in
        // (docs/LunaP.md §21.1). Behaviour is identical for a single Fill; what changes is that a
        // nested one can no longer re-enable Chose halfway through the outer one.
        private readonly Suppressor _filling = new();

        public Dropdown() => SelectionChanged += (_, _) =>
        {
            if (!_filling.IsSuppressing) Chose?.Invoke(SelectedItem);
        };

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
