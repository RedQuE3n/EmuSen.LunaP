using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // One meter, as plain data - the layering rule keeps core telemetry types out of here, so callers project onto this.
    public readonly record struct MeterEntry(string Label, double Percent, string ValueText);

    // A vertical run of MeterRows. Grouping stays with the caller: the group headers are core/DianaOS vocabulary - see docs/LunaP.md §5.2.
    public class MeterList : TemplatedControl
    {
        public static readonly StyledProperty<IReadOnlyList<MeterEntry>> MetersProperty =
            AvaloniaProperty.Register<MeterList, IReadOnlyList<MeterEntry>>(nameof(Meters), new List<MeterEntry>());

        private StackPanel? _panel;

        public IReadOnlyList<MeterEntry> Meters
        {
            get => GetValue(MetersProperty);
            set => SetValue(MetersProperty, value);
        }

        // A container, and it needs to be one rather than nothing: the rows inside it are the
        // content, and a Group is what tells a reader "several related things follow". Unnamed by
        // default - what a run of meters is *about* ("Core load", "Audio") is the caller's word,
        // and §5.2 already decided the group headers stay with the caller for the same reason.
        // AutomationProperties.Name on the MeterList is how they supply it.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _panel = e.NameScope.Find<StackPanel>("PART_Rows");
            Rebuild();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MetersProperty) Rebuild();
        }

        // Rebuilt wholesale rather than diffed - see docs/LunaP.md §5.2 for why that is not the waste it looks like.
        private void Rebuild()
        {
            if (_panel is null) return;

            _panel.Children.Clear();
            foreach (MeterEntry entry in Meters)
            {
                _panel.Children.Add(new MeterRow { Label = entry.Label, Percent = entry.Percent, ValueText = entry.ValueText });
            }
        }
    }
}
