using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // One adjustable number: its label, its value and default, a slider stepped by its step, and a reset above the slider's right end so a pad reaches it by up - see docs/LunaP.md §94.2.
    /// <summary>A labelled slider over a range with a step, showing its value, its default when it differs, and a button that resets it.</summary>
    public class SliderRow : Border
    {
        private readonly TextBlock _label = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _value = new() { VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, MinWidth = 48, Margin = new Thickness(12, 0, 0, 0) };
        private readonly TextBlock _default = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        private readonly Slider _slider = new() { Minimum = 0, Maximum = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, -4, 0, 0) };
        private readonly Button _reset = new() { Content = "Reset", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        private readonly Suppressor _setting = new();
        private double _defaultValue, _step;

        public SliderRow()
        {
            _default[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("LunaMuted");
            Padding = new Thickness(0, 4);

            var top = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(_reset, Dock.Right);
            DockPanel.SetDock(_default, Dock.Right);
            DockPanel.SetDock(_value, Dock.Right);
            top.Children.Add(_reset);
            top.Children.Add(_default);
            top.Children.Add(_value);
            top.Children.Add(_label);
            Child = new StackPanel { Spacing = 0, Children = { top, _slider } };

            _slider.ValueChanged += (_, e) =>
            {
                Show();
                if (!_setting.IsSuppressing) ValueChanged?.Invoke(e.NewValue);
            };
            _reset.Click += (_, _) =>
            {
                if (_slider.Value != _defaultValue) _slider.Value = _defaultValue;
            };
            Configure();
        }

        /// <summary>What the number is, shown above the slider's left end and given to the slider and the reset button as their names.</summary>
        public string Label
        {
            get => _label.Text ?? string.Empty;
            set
            {
                _label.Text = value;
                AutomationProperties.SetName(_slider, value);
                AutomationProperties.SetName(_reset, $"{ResetText} {value}");
            }
        }

        /// <summary>The text on the reset button. "Reset" by default.</summary>
        public string ResetText
        {
            get => _reset.Content as string ?? string.Empty;
            set
            {
                _reset.Content = value;
                AutomationProperties.SetName(_reset, $"{value} {Label}");
            }
        }

        /// <summary>The lowest value. 0 by default.</summary>
        public double Minimum
        {
            get => _slider.Minimum;
            set { using (_setting.Suppress()) _slider.Minimum = value; Configure(); }
        }

        /// <summary>The highest value. 1 by default. A range with no width leaves the slider disabled.</summary>
        public double Maximum
        {
            get => _slider.Maximum;
            set { using (_setting.Suppress()) _slider.Maximum = value; Configure(); }
        }

        /// <summary>How far one press of an arrow key moves the value, and the grid a drag snaps to from Minimum. Zero or less means a hundredth of the range.</summary>
        public double Step
        {
            get => _step;
            set { _step = value; Configure(); }
        }

        /// <summary>The value. Setting it does not raise ValueChanged; a value outside the range is shown at the nearer end.</summary>
        public double Value
        {
            get => _slider.Value;
            set { using (_setting.Suppress()) _slider.Value = value; Show(); }
        }

        /// <summary>The value the reset button returns to, shown beside the value while the two differ. 0 by default.</summary>
        public double DefaultValue
        {
            get => _defaultValue;
            set { _defaultValue = value; Show(); }
        }

        /// <summary>Whether the value is the default, to within a thousandth of the step.</summary>
        public bool IsDefault => Math.Abs(Value - _defaultValue) <= EffectiveStep / 1000;

        /// <summary>Raised when the user moves the slider or presses the reset button, with the new value. Not raised by setting Value.</summary>
        public event Action<double>? ValueChanged;

        private double EffectiveStep => _step > 0 ? _step : Math.Max((Maximum - Minimum) / 100, double.Epsilon);

        private void Configure()
        {
            double step = EffectiveStep;
            _slider.SmallChange = step;
            _slider.LargeChange = step * 10;
            _slider.TickFrequency = step;
            _slider.IsEnabled = Maximum > Minimum;
            Show();
        }

        private void Show()
        {
            int decimals = Math.Max(Decimals(EffectiveStep), Math.Max(Decimals(Minimum), Decimals(_defaultValue)));
            _value.Text = Format(Value, decimals);
            _default.Text = IsDefault ? string.Empty : $"default {Format(_defaultValue, decimals)}";
            _default.IsVisible = !IsDefault;
        }

        private static string Format(double value, int decimals) => value.ToString("F" + decimals, CultureInfo.InvariantCulture);

        // The places a number needs to be written exactly, up to four, so a step of 0.05 shows 0.35 and not 0.3.
        private static int Decimals(double value)
        {
            for (int places = 0; places < 4; places++)
                if (Math.Abs(Math.Round(value, places) - value) < 1e-9) return places;
            return 4;
        }
    }
}
