using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // SliderRow - see docs/LunaP.md §94.2. A value moved by its step, set without an event, and reset to its default by a person.
    public class SliderRowTests
    {
        private static (ToolWindow Window, SliderRow Row, List<double> Changes) Show(SliderRow row)
        {
            var changes = new List<double>();
            row.ValueChanged += v => changes.Add(v);
            var window = new ToolWindow { Width = 420, Height = 200, Content = row };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            UiTest.Capture(window);
            return (window, row, changes);
        }

        private static Slider Slider(SliderRow row) => row.GetVisualDescendants().OfType<Slider>().Single();

        private static Button ResetButton(SliderRow row) => row.GetVisualDescendants().OfType<Button>().Single(b => b.FindAncestorOfType<Slider>() is null);

        private static string[] Texts(SliderRow row) => row.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? "").ToArray();

        private static void Key(Control target, Key key) =>
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

        private static SliderRow Scan() => new() { Label = "Scanline hardness", Minimum = -20, Maximum = 0, Step = 1, DefaultValue = -8, Value = -8 };

        [Fact]
        public Task The_arrow_keys_move_the_value_by_its_step_and_a_value_set_in_code_raises_nothing() => UiTest.Run(() =>
        {
            var (window, row, changes) = Show(Scan());
            Slider slider = Slider(row);
            slider.Focus();

            Key(slider, Avalonia.Input.Key.Right);
            Assert.Equal(-7, row.Value, 6);
            Key(slider, Avalonia.Input.Key.Left);
            Key(slider, Avalonia.Input.Key.Left);
            Assert.Equal(-9, row.Value, 6);
            Assert.Equal(new[] { -7.0, -8.0, -9.0 }, changes);

            row.Value = -3;
            Assert.Equal(3, changes.Count);
            Assert.Contains("-3", Texts(row));
            window.Close();
        });

        [Fact]
        public Task The_default_is_shown_while_the_value_differs_and_reset_returns_to_it() => UiTest.Run(() =>
        {
            var (window, row, changes) = Show(Scan());
            Assert.True(row.IsDefault);
            Assert.DoesNotContain(Texts(row), t => t.StartsWith("default", System.StringComparison.Ordinal));

            row.Value = -12;
            Assert.False(row.IsDefault);
            Assert.Contains("default -8", Texts(row));

            ResetButton(row).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(-8, row.Value, 6);
            Assert.True(row.IsDefault);
            Assert.Equal(new[] { -8.0 }, changes);
            Assert.DoesNotContain(Texts(row), t => t.StartsWith("default", System.StringComparison.Ordinal));
            window.Close();
        });

        [Theory]
        [InlineData(0.05, 0.35, "0.35")]
        [InlineData(1.0, -8.0, "-8")]
        [InlineData(0.01, 0.031, "0.031")]
        [InlineData(0.0, 0.5, "0.50")]
        public Task The_value_is_written_to_the_places_its_step_and_default_need(double step, double value, string shown) => UiTest.Run(() =>
        {
            var (window, row, _) = Show(new SliderRow { Label = "x", Minimum = 0, Maximum = step == 1.0 ? 0 : 1, Step = step, DefaultValue = value });
            if (step == 1.0) row.Minimum = -20;
            row.Value = value;
            Assert.Equal(shown, Texts(row)[1]);
            window.Close();
        });

        [Fact]
        public Task A_range_with_no_width_cannot_be_moved_and_the_parts_are_named_for_a_reader() => UiTest.Run(() =>
        {
            var (window, row, _) = Show(new SliderRow { Label = "[ SECTION ]", Minimum = 0, Maximum = 0, Step = 1 });
            Assert.False(Slider(row).IsEnabled);
            Assert.Equal("[ SECTION ]", AutomationProperties.GetName(Slider(row)));
            Assert.Equal("Reset [ SECTION ]", AutomationProperties.GetName(ResetButton(row)));
            window.Close();
        });
    }
}
