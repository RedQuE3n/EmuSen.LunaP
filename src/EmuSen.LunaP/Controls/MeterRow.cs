using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // One row of a load/level dashboard: a label, a percentage bar, a value - see docs/LunaP.md §5.2.
    /// <summary>One dashboard row: a label, a percentage bar coloured by load band, and a value.</summary>
    public class MeterRow : TemplatedControl
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<MeterRow, string>(nameof(Label), string.Empty);

        public static readonly StyledProperty<double> PercentProperty =
            AvaloniaProperty.Register<MeterRow, double>(nameof(Percent));

        public static readonly StyledProperty<string> ValueTextProperty =
            AvaloniaProperty.Register<MeterRow, string>(nameof(ValueText), string.Empty);

        /// <summary>What the meter is measuring, shown at the left.</summary>
        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        // Drives both the bar and, through the :nominal/:busy/:hot pseudo-classes, its colour.
        /// <summary>How full the bar is, 0 to 100. This also picks the colour band, through the nominal, busy and hot thresholds.</summary>
        public double Percent
        {
            get => GetValue(PercentProperty);
            set => SetValue(PercentProperty, value);
        }

        // Shown verbatim; the caller decides whether that is "62.0%" or "13/128".
        /// <summary>The reading shown at the right. Free text, so a byte count or a temperature can sit beside a percentage bar.</summary>
        public string ValueText
        {
            get => GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        public MeterRow() => ApplyLoadLevel();

        // A meter reads as ONE progress bar named for what it measures, not as a group containing
        // an anonymous one. The template's ProgressBar is marked AccessibilityView="Raw" so it does
        // not appear as a second, nameless node saying the same thing - see docs/LunaP.md §24.2.
        //
        // ValueText is the item status rather than the name, and it is deliberately NOT exposed as
        // a range value. The property's own contract is that "the caller decides whether that is
        // '62.0%' or '13/128'", so Percent and ValueText need not agree: a row showing 13 of 128
        // slots sets Percent to 10 for the bar's length and ValueText to "13/128" for the reader.
        // An IRangeValueProvider would announce "10 percent" over the top of that, which is a
        // number the caller never asked for and, for the half of the uses that are counts rather
        // than proportions, simply wrong.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ProgressBar,
                name: () => Label, status: () => ValueText);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PercentProperty) ApplyLoadLevel();
        }

        // A pseudo-class rather than a computed brush, so the ramp follows a loaded theme - see docs/LunaP.md §12.1.
        private void ApplyLoadLevel()
        {
            LoadLevel level = LunaPalette.LevelFor(Percent);
            Toggle(":nominal", level == LoadLevel.Nominal);
            Toggle(":busy", level == LoadLevel.Busy);
            Toggle(":hot", level == LoadLevel.Hot);
        }

        // Avalonia 12's IPseudoClasses has Add/Remove but no Set.
        private void Toggle(string name, bool on)
        {
            if (on) PseudoClasses.Add(name);
            else PseudoClasses.Remove(name);
        }
    }
}
