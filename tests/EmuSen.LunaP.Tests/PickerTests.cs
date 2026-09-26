using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // RatingPicker and DateStepper, set with the arrow keys alone - see docs/LunaP.md §160.
    public class PickerTests
    {
        private static void Press(Control target, Key key) =>
            target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = target });

        private static ToolWindow Host(Control control, double w = 400, double h = 100)
        {
            control.HorizontalAlignment = HorizontalAlignment.Left;
            control.VerticalAlignment = VerticalAlignment.Top;
            return Show(control, w, h);
        }

        [Fact]
        public Task Left_and_right_step_a_rating_by_half_stars_and_stop_at_none_and_all() => UiTest.Run(() =>
        {
            var picker = new RatingPicker { Value = 0.6 };
            var chosen = new List<double>();
            picker.Chose += chosen.Add;
            ToolWindow window = Host(picker);
            Press(picker, Key.Right);
            Assert.Equal(0.7, picker.Value, 6);
            Press(picker, Key.Left);
            Press(picker, Key.Left);
            Assert.Equal(0.5, picker.Value, 6);
            Press(picker, Key.End);
            Press(picker, Key.Right);
            Assert.Equal(1, picker.Value, 6);
            Press(picker, Key.Home);
            Press(picker, Key.Left);
            Assert.Equal(0, picker.Value, 6);
            Assert.Equal(new[] { 0.7, 0.6, 0.5, 1, 0 }, chosen.ConvertAll(v => Math.Round(v, 6)));

            // A value between steps is snapped before it is stepped, so 0.73 goes to 0.8, not 0.83.
            picker.Value = 0.73;
            Press(picker, Key.Right);
            Assert.Equal(0.8, picker.Value, 6);

            picker.StepsPerStar = 1;
            picker.Value = 0.5;
            Press(picker, Key.Right);
            Assert.Equal(0.6, picker.Value, 6);
            Assert.Equal("3 of 5", ControlAutomationPeer.CreatePeerForElement(picker).GetName());
            Assert.Equal(AutomationControlType.Slider, ControlAutomationPeer.CreatePeerForElement(picker).GetAutomationControlType());
            window.Close();
        });

        [Fact]
        public Task Code_setting_a_rating_raises_nothing_and_the_filled_stars_end_at_the_value() => UiTest.Run(() =>
        {
            var picker = new RatingPicker { StarSize = 40 };
            int raised = 0;
            picker.Chose += _ => raised++;
            ToolWindow window = Host(picker);
            picker.Value = 0.5;
            Assert.Equal(0, raised);
            Assert.Equal(new Size(200, 40), picker.Bounds.Size);
            RenderedFrame f = Frame(window);
            // The third star's centre: its left half filled, its right half not.
            Assert.Equal(LunaPalette.Warning.Color, At(f, 97, 22));
            Assert.Equal(LunaPalette.Muted.Color, At(f, 103, 22));
            Assert.Equal(LunaPalette.Warning.Color, At(f, 20, 22));
            Assert.Equal(LunaPalette.Muted.Color, At(f, 180, 22));
            Assert.True(picker is ISidewaysAdjustable);
            window.Close();
        });

        [Fact]
        public Task A_date_steps_one_part_at_a_time_and_enter_moves_to_the_next_part() => UiTest.Run(() =>
        {
            var stepper = new DateStepper { Value = new DateTime(1991, 8, 23) };
            var chosen = new List<DateTime?>();
            stepper.Chose += chosen.Add;
            ToolWindow window = Host(stepper);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(1992, 8, 23), stepper.Value);
            Press(stepper, Key.Enter);
            Assert.Equal(DateSegment.Month, stepper.Segment);
            Press(stepper, Key.Left);
            Assert.Equal(new DateTime(1992, 7, 23), stepper.Value);
            Press(stepper, Key.Enter);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(1992, 7, 24), stepper.Value);
            Press(stepper, Key.Enter);
            Assert.Equal(DateSegment.Year, stepper.Segment);
            Assert.Equal(3, chosen.Count);
            Assert.Equal("1992-07-24", stepper.Text);
            Assert.Equal("1992-07-24, year", ControlAutomationPeer.CreatePeerForElement(stepper).GetName());
            window.Close();
        });

        [Fact]
        public Task The_day_stays_inside_its_month_and_wraps_within_it() => UiTest.Run(() =>
        {
            var stepper = new DateStepper { Value = new DateTime(2000, 1, 31), Segment = DateSegment.Month };
            ToolWindow window = Host(stepper);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(2000, 2, 29), stepper.Value);
            stepper.Segment = DateSegment.Year;
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(2001, 2, 28), stepper.Value);
            stepper.Segment = DateSegment.Day;
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(2001, 2, 1), stepper.Value);
            Press(stepper, Key.Left);
            Assert.Equal(new DateTime(2001, 2, 28), stepper.Value);
            stepper.Segment = DateSegment.Month;
            stepper.Value = new DateTime(2001, 12, 15);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(2001, 1, 15), stepper.Value);
            window.Close();
        });

        [Fact]
        public Task No_date_starts_at_the_start_date_and_the_first_year_steps_back_to_none() => UiTest.Run(() =>
        {
            var stepper = new DateStepper { MinimumYear = 1980, MaximumYear = 1982, StartDate = new DateTime(1981, 5, 6) };
            ToolWindow window = Host(stepper);
            Assert.Equal("No date", stepper.Text);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(1981, 5, 6), stepper.Value);
            Press(stepper, Key.Right);
            Press(stepper, Key.Right);
            Assert.Equal(new DateTime(1982, 5, 6), stepper.Value);
            Press(stepper, Key.Left);
            Press(stepper, Key.Left);
            Assert.Equal(new DateTime(1980, 5, 6), stepper.Value);
            Press(stepper, Key.Left);
            Assert.Null(stepper.Value);
            stepper.Value = new DateTime(1981, 1, 1);
            Press(stepper, Key.Delete);
            Assert.Null(stepper.Value);
            Assert.True(stepper is ISidewaysAdjustable);
            window.Close();
        });

        [Fact]
        public Task The_part_being_changed_is_drawn_on_the_accent_while_focused() => UiTest.Run(() =>
        {
            var stepper = new DateStepper { Value = new DateTime(1991, 8, 23), FontSize = 20 };
            ToolWindow window = Host(stepper);
            stepper.Focus(NavigationMethod.Directional);
            RenderedFrame year = Frame(window);
            stepper.Segment = DateSegment.Day;
            RenderedFrame day = Frame(window);
            double h = stepper.Bounds.Height / 2;
            Assert.True(Accents(year, 4, stepper.Bounds.Width * 0.45, h) > Accents(year, stepper.Bounds.Width * 0.7, stepper.Bounds.Width, h));
            Assert.True(Accents(day, stepper.Bounds.Width * 0.7, stepper.Bounds.Width, h) > Accents(day, 4, stepper.Bounds.Width * 0.45, h));
            window.Close();
        });

        // One click on a fresh control, at a point between stars' points as well as on one, since the row must take a click anywhere (§160.4).
        private static double ClickRating(double x)
        {
            var picker = new RatingPicker { StarSize = 40 };
            ToolWindow window = Host(picker, 400, 100);
            Point p = picker.TranslatePoint(new Point(x, 20), window)!.Value;
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            window.Close();
            return picker.Value;
        }

        private static DateSegment ClickDate(double fraction)
        {
            var stepper = new DateStepper { Value = new DateTime(1991, 8, 23), FontSize = 20 };
            ToolWindow window = Host(stepper, 400, 100);
            Point p = stepper.TranslatePoint(new Point(stepper.Bounds.Width * fraction, 10), window)!.Value;
            window.MouseDown(p, MouseButton.Left);
            window.MouseUp(p, MouseButton.Left);
            window.Close();
            Assert.Equal(new DateTime(1991, 8, 23), stepper.Value);
            return stepper.Segment;
        }

        [Theory]
        [InlineData(110, 0.6)]
        [InlineData(85, 0.5)]
        [InlineData(5, 0.1)]
        public Task A_click_sets_the_rating_on_the_step_whose_right_edge_is_first_past_the_pointer(double x, double value) => UiTest.Run(() =>
            Assert.Equal(value, ClickRating(x), 6));

        [Theory]
        [InlineData(0.1, DateSegment.Year)]
        [InlineData(0.5, DateSegment.Month)]
        [InlineData(0.9, DateSegment.Day)]
        public Task A_click_chooses_the_part_of_the_date_under_the_pointer_and_leaves_the_date(double fraction, DateSegment segment) => UiTest.Run(() =>
            Assert.Equal(segment, ClickDate(fraction)));

        private static int Accents(RenderedFrame f, double from, double to, double y)
        {
            int n = 0;
            for (double x = from; x < to; x++) if (At(f, x, y) == LunaPalette.Accent.Color) n++;
            return n;
        }
    }
}
