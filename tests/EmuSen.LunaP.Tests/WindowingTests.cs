using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // ToolWindow/PollingWindow/WindowSlot - see docs/LunaP.md §8.
    public class WindowingTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WindowingTests).GetTypeInfo().Assembly);

        private readonly string _configDir;

        // Keeps remembered geometry off whoever is running the suite, by pointing LunaP's own settings seam at a temporary directory.
        public WindowingTests()
        {
            _configDir = Path.Combine(Path.GetTempPath(), "lunap-windowing-" + Guid.NewGuid().ToString("N"));
            LunaSettings.Store = new JsonSettingsStore(_configDir);
        }

        public void Dispose()
        {
            LunaSettings.Store = new JsonSettingsStore(Path.Combine(Path.GetTempPath(), "lunap-unset"));
            if (Directory.Exists(_configDir)) Directory.Delete(_configDir, recursive: true);
        }

        private sealed class CountingWindow : PollingWindow
        {
            public int Refreshes;

            public CountingWindow()
            {
                Width = 200;
                Height = 150;
                StartPolling();
            }

            protected override TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(20);

            protected override void Refresh() => Refreshes++;
        }

        [Fact]
        public Task Polling_primes_itself_once_before_any_tick() => Session.Dispatch(() =>
        {
            var window = new CountingWindow();

            // Constructed but never shown: the priming Refresh has run, the timer has not.
            Assert.Equal(1, window.Refreshes);
        }, default);

        // Asserted on the timer's own state rather than a tick count: a count-based test would have to race a real clock to mean anything.
        [Fact]
        public Task Polling_stops_while_the_window_is_hidden() => Session.Dispatch(() =>
        {
            var window = new CountingWindow();

            // Never shown: primed, but nothing scheduled.
            Assert.False(window.IsPolling);

            window.Show();
            Assert.True(window.IsPolling);

            window.Hide();
            Assert.False(window.IsPolling);

            int atHide = window.Refreshes;
            window.Show();

            Assert.True(window.IsPolling);

            // Coming back repaints immediately rather than showing however stale it got.
            Assert.True(window.Refreshes > atHide);
        }, default);

        [Fact]
        public Task Polling_stops_while_the_window_is_minimised() => Session.Dispatch(() =>
        {
            var window = new CountingWindow();
            window.Show();
            Assert.True(window.IsPolling);

            window.WindowState = WindowState.Minimized;
            Assert.False(window.IsPolling);

            int atMinimise = window.Refreshes;
            window.WindowState = WindowState.Normal;

            Assert.True(window.IsPolling);
            Assert.True(window.Refreshes > atMinimise);
        }, default);

        [Fact]
        public Task Closing_a_polling_window_stops_it() => Session.Dispatch(() =>
        {
            var window = new CountingWindow();
            window.Show();
            Assert.True(window.IsPolling);

            window.Close();

            // A timer left running holds the closed window alive and keeps calling into it.
            Assert.False(window.IsPolling);
        }, default);

        [Fact]
        public Task A_polling_window_that_never_called_start_still_polls_once_opened() => Session.Dispatch(() =>
        {
            var window = new ForgetfulWindow();
            Assert.Equal(0, window.Refreshes);

            window.Show();
            Assert.True(window.Refreshes > 0);
        }, default);

        private sealed class ForgetfulWindow : PollingWindow
        {
            public int Refreshes;

            public ForgetfulWindow()
            {
                Width = 200;
                Height = 150;
            }

            protected override TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(20);

            protected override void Refresh() => Refreshes++;
        }

        [Fact]
        public Task Escape_closes_only_when_a_window_opts_in() => Session.Dispatch(() =>
        {
            var closing = new ToolWindow { Width = 200, Height = 150, ClosesOnEscape = true };
            closing.Show();
            closing.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Assert.False(closing.IsVisible);

            var staying = new ToolWindow { Width = 200, Height = 150 };
            staying.Show();
            staying.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Assert.True(staying.IsVisible);
        }, default);

        [Fact]
        public Task A_window_with_no_key_is_never_remembered() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 321, Height = 234 };
            window.Show();
            window.Close();

            Assert.False(File.Exists(Path.Combine(_configDir, WindowPlacementStore.FileName)));
        }, default);

        [Fact]
        public Task A_keyed_window_remembers_its_size() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { WindowKey = "test.sized", Width = 321, Height = 234 };
            window.Show();
            window.Close();

            WindowPlacement? saved = WindowPlacementStore.Load("test.sized");
            Assert.NotNull(saved);
            Assert.Equal(321, saved!.Width);
            Assert.Equal(234, saved.Height);

            var reopened = new ToolWindow { WindowKey = "test.sized", Width = 100, Height = 100 };
            reopened.Show();

            Assert.Equal(321, reopened.Width);
            Assert.Equal(234, reopened.Height);
            reopened.Close();
        }, default);

        // A monitor that is no longer attached must not strand a window where it cannot be dragged back.
        [Theory]
        // Fully inside the primary screen.
        [InlineData(100, 100, 400, 300, true)]
        // Straddling the edge - still reachable, so still allowed.
        [InlineData(1800, 500, 400, 300, true)]
        // On a second monitor that is still attached.
        [InlineData(2200, 100, 400, 300, true)]
        // Where a now-unplugged monitor used to be.
        [InlineData(4000, 100, 400, 300, false)]
        // Negative space above and left of everything.
        [InlineData(-900, -900, 400, 300, false)]
        public void An_off_screen_position_is_rejected_against_the_attached_screens(
            int x, int y, int width, int height, bool expected)
        {
            var screens = new[]
            {
                new PixelRect(0, 0, 1920, 1080),
                new PixelRect(1920, 0, 1920, 1080),
            };

            Assert.Equal(expected, WindowPlacementStore.IsOnAScreen(screens, new PixelRect(x, y, width, height)));
        }

        // Not the same as "off screen": with nothing to check against, refusing would strand the window at the default position.
        [Fact]
        public void No_known_screens_allows_the_remembered_position()
        {
            Assert.True(WindowPlacementStore.IsOnAScreen(Array.Empty<PixelRect>(), new PixelRect(4000, 4000, 100, 100)));
            Assert.True(WindowPlacementStore.IsOnAScreen((Screens?)null, new PixelRect(4000, 4000, 100, 100)));
        }

        // ------------------------------------------------------------------ full screen (§75)

        // THE RULE, HANDED THE GEOMETRY THE HEADLESS PLATFORM WILL NOT PRODUCE - see docs/LunaP.md §75.4.
        //
        // Avalonia.Headless stores WindowState and never acts on it: a window put into FullScreen
        // keeps the position and size it already had. So an end-to-end test of this rule passes
        // whether or not the rule exists, and the outcome is only assertable against the pure
        // function. 1920x1080 at (0,0) is what a real platform would have replaced the window's own
        // bounds with by the time it closes.
        [Theory]
        // Full screen: the stored 321x234 at (120, 90) survives, and the flag is NOT set.
        [InlineData(WindowState.FullScreen, 120, 90, 321d, 234d, false)]
        // Maximized: same geometry rule, and the flag IS set.
        [InlineData(WindowState.Maximized, 120, 90, 321d, 234d, true)]
        // Ordinary: the live geometry is the answer and the stored one is ignored.
        [InlineData(WindowState.Normal, 0, 0, 1920d, 1080d, false)]
        public void A_window_covering_the_screen_saves_the_geometry_it_had_before(
            WindowState state, int x, int y, double width, double height, bool maximized)
        {
            var stored = new WindowPlacement { X = 120, Y = 90, Width = 321, Height = 234 };

            WindowPlacement saved = WindowPlacementStore.PlacementToSave(
                state, new PixelPoint(0, 0), 1920, 1080, stored);

            Assert.Equal(x, saved.X);
            Assert.Equal(y, saved.Y);
            Assert.Equal(width, saved.Width);
            Assert.Equal(height, saved.Height);
            Assert.Equal(maximized, saved.Maximized);
        }

        // §75.6: the case §8.1's rule never covered. With nothing stored, the fallback was the live
        // geometry - which is the screen, which is the whole thing the branch exists to distrust.
        [Theory]
        [InlineData(WindowState.FullScreen, false)]
        [InlineData(WindowState.Maximized, true)]
        public void A_window_covering_the_screen_on_its_first_run_remembers_no_geometry(
            WindowState state, bool maximized)
        {
            WindowPlacement saved = WindowPlacementStore.PlacementToSave(
                state, new PixelPoint(0, 0), 1920, 1080, previous: null);

            // Zero rather than the screen: RestorePlacement already ignores a non-positive size and
            // IsOnAScreen already refuses an empty rectangle, so this reopens at the window's own
            // default size instead of at the size of the monitor.
            Assert.Equal(0, saved.Width);
            Assert.Equal(0, saved.Height);
            Assert.Equal(0, saved.X);
            Assert.Equal(0, saved.Y);
            Assert.Equal(maximized, saved.Maximized);

            Assert.False(
                WindowPlacementStore.IsOnAScreen(
                    new[] { new PixelRect(0, 0, 1920, 1080) },
                    new PixelRect(saved.X, saved.Y, (int)saved.Width, (int)saved.Height)),
                "An empty remembered rectangle must not be treated as a position worth restoring.");
        }

        // THE WIRING, AND IT IS ONE STEP WEAKER THAN THE RULE ABOVE - see docs/LunaP.md §75.4.
        //
        // What this discriminates is that RememberPlacement consults the STORED placement at all
        // when it closes full screen. The stored rectangle is deliberately different from the live
        // one, so code that ignores it saves the live one and fails here. It cannot assert the
        // outcome, because the live geometry on this platform is not the screen's.
        [Fact]
        public Task Closing_full_screen_reaches_for_the_stored_placement() => Session.Dispatch(() =>
        {
            WindowPlacementStore.Save("test.full", new WindowPlacement { X = 11, Y = 22, Width = 800, Height = 600 });

            var window = new ToolWindow { WindowKey = "test.full", Width = 321, Height = 234 };
            window.Show();
            window.Position = new PixelPoint(120, 90);

            window.IsFullScreen = true;
            window.Close();

            WindowPlacement? saved = WindowPlacementStore.Load("test.full");
            Assert.NotNull(saved);
            Assert.Equal(800, saved!.Width);
            Assert.Equal(600, saved.Height);
            Assert.Equal(11, saved.X);
            Assert.Equal(22, saved.Y);

            // And a window closed full screen does not reopen with no way out of it (§75.5).
            Assert.False(saved.Maximized);
        }, default);

        [Fact]
        public Task Full_screen_goes_in_and_comes_back_out() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            Assert.False(window.IsFullScreen);

            window.ToggleFullScreen();
            Assert.True(window.IsFullScreen);
            Assert.Equal(WindowState.FullScreen, window.WindowState);

            window.ToggleFullScreen();
            Assert.False(window.IsFullScreen);
            Assert.Equal(WindowState.Normal, window.WindowState);

            window.Close();
        }, default);

        // The half that is easy to get wrong: a maximized window must not be un-maximized by a trip
        // through full screen and back.
        [Fact]
        public Task Leaving_full_screen_returns_to_the_state_it_came_from() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();
            window.WindowState = WindowState.Maximized;

            window.ToggleFullScreen();
            Assert.Equal(WindowState.FullScreen, window.WindowState);

            window.ToggleFullScreen();
            Assert.Equal(WindowState.Maximized, window.WindowState);

            window.Close();
        }, default);

        // §75.2: the property setter is not the only way in. The platform's own full-screen
        // affordance and a caller setting WindowState directly both bypass it, and the state to
        // return to has to be captured on the way past either way.
        [Fact]
        public Task Full_screen_entered_without_the_property_is_still_tracked() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();
            window.WindowState = WindowState.Maximized;

            // Not IsFullScreen, and not ToggleFullScreen.
            window.WindowState = WindowState.FullScreen;
            Assert.True(window.IsFullScreen);

            window.ToggleFullScreen();
            Assert.Equal(WindowState.Maximized, window.WindowState);

            window.Close();
        }, default);

        // §75.2: the event exists so a checkable menu item can FOLLOW the window instead of keeping
        // its own answer, which is §26.3's rule. It has to fire for a change this toolkit did not
        // make, or the tick still goes stale in exactly the case it was added for.
        [Fact]
        public Task Full_screen_announces_itself_however_it_was_entered() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            var seen = new List<bool>();
            window.FullScreenChanged += on => seen.Add(on);

            window.ToggleFullScreen();
            window.ToggleFullScreen();

            // Not through the property or the toggle: this is the platform's own affordance.
            window.WindowState = WindowState.FullScreen;
            window.WindowState = WindowState.Normal;

            Assert.Equal(new[] { true, false, true, false }, seen);

            // A state change that leaves the answer where it was says nothing.
            window.WindowState = WindowState.Maximized;
            window.WindowState = WindowState.Normal;
            Assert.Equal(4, seen.Count);

            window.Close();
        }, default);

        // Coming out of full screen into a minimized window reads as the application having closed.
        [Fact]
        public Task Full_screen_never_returns_to_minimised() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();
            window.WindowState = WindowState.Minimized;

            window.WindowState = WindowState.FullScreen;
            window.ToggleFullScreen();

            Assert.Equal(WindowState.Normal, window.WindowState);
            window.Close();
        }, default);

        [Fact]
        public Task A_slot_opens_one_window_and_then_reuses_it() => Session.Dispatch(() =>
        {
            var slot = new WindowSlot<ToolWindow>();
            int created = 0;
            int refreshed = 0;

            slot.Show(null, () => { created++; return new ToolWindow { Width = 200, Height = 150 }; });
            ToolWindow? first = slot.Current;

            slot.Show(null, () => { created++; return new ToolWindow { Width = 200, Height = 150 }; }, _ => refreshed++);

            Assert.Equal(1, created);
            Assert.Equal(1, refreshed);
            Assert.Same(first, slot.Current);

            slot.Close();
            Assert.False(slot.IsOpen);
            Assert.Null(slot.Current);
        }, default);

        [Fact]
        public Task A_slot_reopens_after_the_window_was_closed_by_the_user() => Session.Dispatch(() =>
        {
            var slot = new WindowSlot<ToolWindow>();

            slot.Show(null, () => new ToolWindow { Width = 200, Height = 150 });
            ToolWindow first = slot.Current!;
            first.Close();

            Assert.False(slot.IsOpen);

            slot.Show(null, () => new ToolWindow { Width = 200, Height = 150 });
            Assert.True(slot.IsOpen);
            Assert.NotSame(first, slot.Current);
            slot.Close();
        }, default);

        // The core-swap case: refresh what is open, but never conjure a window nobody asked for.
        [Fact]
        public Task Refresh_if_open_never_creates_a_window() => Session.Dispatch(() =>
        {
            var slot = new WindowSlot<ToolWindow>();
            int refreshed = 0;

            slot.RefreshIfOpen(_ => refreshed++);
            Assert.False(slot.IsOpen);
            Assert.Equal(0, refreshed);

            slot.Show(null, () => new ToolWindow { Width = 200, Height = 150 });
            slot.RefreshIfOpen(_ => refreshed++);
            Assert.Equal(1, refreshed);

            slot.Close();
        }, default);
    }
}
