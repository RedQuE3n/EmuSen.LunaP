using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Testing;
using EmuSen.LunaP.Windowing;
using static EmuSen.LunaP.Tests.DrawnSupport;

namespace EmuSen.LunaP.Tests
{
    // StarRating, BadgeStrip, HintBar, ClockLabel and DeviceStatusBar - see docs/LunaP.md §101.
    public class IndicatorControlTests
    {
        private static ToolWindow Place(Control control, double w, double h, Size fraction)
        {
            var canvas = new NormalizedCanvas();
            NormalizedCanvas.SetSize(control, fraction);
            canvas.Children.Add(control);
            return Show(canvas, w, h);
        }

        [Fact]
        public Task A_rating_cuts_the_filled_stars_at_its_value_and_sizes_from_its_height() => UiTest.Run(() =>
        {
            string filled = Flat("star-filled", 10, 10, Colors.Red);
            string unfilled = Flat("star-unfilled", 10, 10, Colors.Blue);
            var rating = new StarRating { Value = 0.45, StarCount = 4, FilledPath = filled, UnfilledPath = unfilled };
            ToolWindow window = Place(rating, 400, 100, new Size(0, 0.5));
            Assert.Equal(new Size(200, 50), rating.Bounds.Size);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, At(f, 85, 25));
            Assert.Equal(Colors.Blue, At(f, 95, 25));

            string half = Flat("star-half", 10, 10, Color.FromArgb(128, 255, 0, 0));
            rating.FilledPath = half;
            Assert.NotEqual(Colors.Black, At(Frame(window), 50, 25));
            Color overlaid = At(Frame(window), 50, 25);
            rating.Overlay = false;
            Color alone = At(Frame(window), 50, 25);
            Assert.True(overlaid.B > 50 && alone.B < 10, $"{overlaid} against {alone}");
            window.Close();
        });

        [Fact]
        public Task The_built_in_star_draws_when_no_image_is_given() => UiTest.Run(() =>
        {
            var rating = new StarRating { Value = 1, StarCount = 1 };
            ToolWindow window = Place(rating, 100, 100, new Size(0, 1));
            Assert.Equal(EmuSen.LunaP.Theme.LunaPalette.Warning.Color, At(Frame(window), 50, 50));
            Assert.Equal(Colors.Black, At(Frame(window), 2, 2));
            Assert.Equal("1 of 1", Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(rating).GetName());
            window.Close();
        });

        [Fact]
        public Task Badges_pack_into_cells_in_order_and_align_as_a_group() => UiTest.Run(() =>
        {
            string icon = Flat("badge", 10, 10, Colors.Lime);
            var strip = new BadgeStrip { Icons = new[] { icon, icon }, Lines = 1, ItemsPerLine = 5, ItemMargin = new Size(10, 0) };
            IReadOnlyList<Rect> cells = strip.Cells(new Size(540, 100));
            Assert.Equal(new[] { new Rect(0, 0, 100, 100), new Rect(110, 0, 100, 100) }, cells);
            strip.ContentHorizontalAlignment = HorizontalAlignment.Right;
            Assert.Equal(new Rect(440, 0, 100, 100), strip.Cells(new Size(540, 100))[1]);
            strip.Direction = Orientation.Vertical;
            strip.Lines = 2;
            strip.ItemsPerLine = 2;
            strip.Icons = new[] { icon, icon, icon };
            strip.ContentHorizontalAlignment = HorizontalAlignment.Left;
            Assert.Equal(new Rect(0, 50, 265, 50), strip.Cells(new Size(540, 100))[1]);
            Assert.Equal(new Rect(275, 0, 265, 50), strip.Cells(new Size(540, 100))[2]);

            ToolWindow window = Place(strip, 540, 100, new Size(1, 1));
            Assert.Equal(Colors.Lime, At(Frame(window), 130, 25));
            window.Close();
        });

        [Fact]
        public Task Hints_run_icon_then_label_with_their_spacings_and_background() => UiTest.Run(() =>
        {
            string icon = Flat("hint", 20, 10, Colors.White);
            var bar = new HintBar
            {
                Entries = new[] { new HintEntry("Select", icon), new HintEntry("Back", Glyph: "B") }, FontSize = 20, EntrySpacing = 30, IconTextSpacing = 8,
                Padding = new Thickness(5), BackgroundColor = Colors.Blue, IconColor = Colors.Red,
            };
            var canvas = new NormalizedCanvas();
            canvas.Children.Add(bar);
            ToolWindow window = Show(canvas, 600, 100);
            IReadOnlyList<(Rect Icon, Rect Label)> boxes = bar.Layout(bar.Bounds.Size);
            Assert.Equal(new Rect(5, 7, 40, 20), boxes[0].Icon);
            Assert.Equal(boxes[0].Icon.Right + 8, boxes[0].Label.X, 6);
            Assert.Equal(boxes[0].Label.Right + 30, boxes[1].Icon.X, 6);
            Assert.InRange(bar.Bounds.Width, boxes[1].Label.Right + 5 - 1, boxes[1].Label.Right + 5 + 1);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Red, At(f, 25, 17));
            Assert.Equal(Colors.Blue, At(f, 2, 17));
            window.Close();
        });

        [Fact]
        public Task A_clock_formats_its_time_and_falls_back_on_a_bad_format() => UiTest.Run(() =>
        {
            var clock = new ClockLabel { Time = new DateTime(2026, 9, 24, 7, 5, 0), Format = "HH:mm" };
            Assert.Equal("07:05", clock.Text);
            clock.Format = "yyyy-MM-dd";
            Assert.Equal("2026-09-24", clock.Text);
            clock.Format = "%";
            Assert.Equal("2026-09-24T07:05:00", clock.Text);
            Assert.False(clock.Wrap);
        });

        [Theory]
        [InlineData(95, false, "battery_full")]
        [InlineData(90, false, "battery_full")]
        [InlineData(89, false, "battery_high")]
        [InlineData(60, false, "battery_high")]
        [InlineData(30, false, "battery_medium")]
        [InlineData(29, false, "battery_low")]
        [InlineData(10, true, "battery_charging")]
        public Task The_battery_icon_follows_its_level(int percent, bool charging, string key) => UiTest.Run(() =>
            Assert.Equal(key, DeviceStatusBar.BatteryKey(percent, charging)));

        [Fact]
        public Task Status_shows_radios_that_are_on_and_the_battery_as_the_indicators_allow() => UiTest.Run(() =>
        {
            var bar = new DeviceStatusBar { Status = new DeviceStatus(Bluetooth: false, Wifi: true, BatteryPercent: 42) };
            Assert.Equal(new[] { "wifi", "battery_medium" }, bar.Shown().Keys);
            Assert.Equal("42%", bar.Shown().Percentage);
            bar.Indicators = DeviceIndicators.Battery;
            Assert.Equal(new[] { "battery_medium" }, bar.Shown().Keys);
            Assert.Null(bar.Shown().Percentage);
            bar.Status = new DeviceStatus(Wifi: true);
            Assert.Empty(bar.Shown().Keys);
        });

        [Fact]
        public Task Status_icons_come_from_files_where_given_and_are_drawn_otherwise() => UiTest.Run(() =>
        {
            string wifi = Flat("status-wifi", 10, 10, Colors.White);
            var bar = new DeviceStatusBar
            {
                Status = new DeviceStatus(Wifi: true, BatteryPercent: 100), IconHeight = 20, EntrySpacing = 10, Color = Colors.Lime,
                Icons = new Dictionary<string, string> { ["wifi"] = wifi }, Indicators = DeviceIndicators.Wifi | DeviceIndicators.Battery,
            };
            var canvas = new NormalizedCanvas();
            canvas.Children.Add(bar);
            ToolWindow window = Show(canvas, 300, 100);
            Assert.Equal(new Size(20 + 10 + 20, 20), bar.Bounds.Size);
            RenderedFrame f = Frame(window);
            Assert.Equal(Colors.Lime, At(f, 10, 10));
            Assert.Equal(Colors.Lime, At(f, 40, 0));
            window.Close();
        });

        // The Bluetooth rune, not a letter: a stem, arms that cross it, and the points that end at the box's sides (§191).
        [Fact]
        public Task Bluetooth_is_drawn_as_its_rune() => UiTest.Run(() =>
        {
            var bar = new DeviceStatusBar { Status = new DeviceStatus(Bluetooth: true), Indicators = DeviceIndicators.Bluetooth, IconHeight = 44, Color = Colors.White };
            var canvas = new Canvas { Width = 100, Height = 60, Background = Brushes.Black, Children = { bar } };
            Canvas.SetLeft(bar, 20);
            Canvas.SetTop(bar, 8);
            ToolWindow window = Show(canvas, 100, 60);
            RenderedFrame f = Frame(window);
            Assert.InRange(bar.Bounds.Width, 43, 45);
            bool Ink(double fx, double fy) => At(f, 20 + fx * 44, 8 + fy * 44).G > 128;
            Assert.True(Ink(0.5, 0.15) && Ink(0.5, 0.5) && Ink(0.5, 0.85), "the stem");
            Assert.True(Ink(0.3, 0.335) && Ink(0.3, 0.665), "the left points");
            Assert.True(Ink(0.7, 0.335) && Ink(0.7, 0.665), "the right points");
            Assert.False(Ink(0.3, 0.5) || Ink(0.7, 0.5) || Ink(0.5, 0.05) || Ink(0.15, 0.3), "open between the points and outside the rune");
            window.Close();
        });

        // Whatever indicator comes last, nothing is drawn past the bar's measured right edge; the Wi-Fi fan once reached seven pixels beyond it (§180.5).
        [Theory]
        [InlineData(DeviceIndicators.All)]
        [InlineData(DeviceIndicators.Bluetooth | DeviceIndicators.Wifi | DeviceIndicators.Battery)]
        [InlineData(DeviceIndicators.Bluetooth | DeviceIndicators.Wifi)]
        [InlineData(DeviceIndicators.Wifi)]
        [InlineData(DeviceIndicators.Bluetooth)]
        public Task No_indicator_draws_past_the_bar_s_right_edge(DeviceIndicators on) => UiTest.Run(() =>
        {
            var bar = new DeviceStatusBar { Status = new DeviceStatus(Bluetooth: true, Wifi: true, BatteryPercent: 64), Indicators = on, IconHeight = 40, EntrySpacing = 6, Color = Colors.White };
            var canvas = new Canvas { Width = 400, Height = 60, Background = Brushes.Black, Children = { bar } };
            Canvas.SetLeft(bar, 100);
            ToolWindow window = Show(canvas, 400, 60);
            RenderedFrame f = Frame(window);
            int first = 400, last = 0;
            for (int x = 0; x < 400; x++)
                for (int y = 0; y < 60; y++)
                    if (At(f, x, y).G > 40) (first, last) = (Math.Min(first, x), Math.Max(last, x));
            Assert.True(last + 1 <= bar.Bounds.Right + 0.5, $"{on}: ink to column {last}, the bar ends at {bar.Bounds.Right}");
            Assert.True(first >= bar.Bounds.Left - 0.5, $"{on}: ink from column {first}, the bar starts at {bar.Bounds.Left}");
            window.Close();
        });
    }
}
