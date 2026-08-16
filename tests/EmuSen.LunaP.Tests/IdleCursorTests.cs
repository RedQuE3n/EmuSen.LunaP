using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // IdleCursor - see docs/LunaP.md §76.
    //
    // NOTHING HERE WAITS FOR A CLOCK, and that is not a shortcut. A DispatcherTimer cannot advance
    // inside Session.Dispatch: the dispatch call owns the loop while the test body runs, so the
    // timer never gets to raise. Measured, and it is 0 ticks rather than a slow tick - a 30ms timer
    // slept past by 80ms and pumped with RunJobs five more times fired zero times (§76.9).
    //
    // So the delay here is THIRTY SECONDS, chosen so the timer cannot fire even in principle, and
    // every transition is driven by Hide() and Show() - which are public because a caller entering
    // full screen wants them, not because a test does. This is the same bargain ThreadingTests
    // already struck for Debounce itself, and the reason is written at the top of that file too.
    public class IdleCursorTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(IdleCursorTests).GetTypeInfo().Assembly);

        // Long enough that the idle path cannot possibly run during a test.
        private static readonly TimeSpan Never = TimeSpan.FromSeconds(30);

        private static void Move(InputElement target, Point to)
        {
            target.RaiseEvent(new PointerEventArgs(
                InputElement.PointerMovedEvent, target,
                new Avalonia.Input.Pointer(0, PointerType.Mouse, true), target, to, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.None));

            Dispatcher.UIThread.RunJobs();
        }

        [Fact]
        public Task Hiding_sets_the_invisible_cursor_and_revealing_takes_it_away() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            using var idle = new IdleCursor(window, Never);
            Assert.False(idle.IsHidden);
            Assert.False(window.IsSet(InputElement.CursorProperty));

            idle.Hide();

            Assert.True(idle.IsHidden);
            Assert.Equal(StandardCursorType.None, Kind(window));

            idle.Show();

            Assert.False(idle.IsHidden);
            Assert.False(window.IsSet(InputElement.CursorProperty));

            window.Close();
        }, default);

        [Fact]
        public Task Moving_the_pointer_brings_it_back() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            using var idle = new IdleCursor(window, Never);
            idle.Hide();
            Assert.True(idle.IsHidden);

            Move(window, new Point(10, 10));

            Assert.False(idle.IsHidden);

            window.Close();
        }, default);

        // §76.3, and it is the assertion that would have caught the whole feature failing quietly on
        // a platform that raises PointerMoved for a stationary pointer. Deterministic because a
        // hidden cursor that STAYS hidden is the same claim as a delay that gets to elapse.
        [Fact]
        public Task A_move_to_the_same_point_is_not_activity() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            using var idle = new IdleCursor(window, Never);

            // One real move to establish where the pointer is, then the same point repeatedly -
            // which is what an activating window delivers.
            Move(window, new Point(10, 10));
            idle.Hide();

            for (int i = 0; i < 5; i++) Move(window, new Point(10, 10));

            Assert.True(idle.IsHidden,
                "A move to the point the pointer was already at counted as activity, so the delay could never elapse.");

            // A different point is activity.
            Move(window, new Point(11, 10));
            Assert.False(idle.IsHidden);

            window.Close();
        }, default);

        // §76.4. Restoring by assignment would make the cursor a LOCAL value and beat the style that
        // owns it ever after, which nothing would report.
        [Fact]
        public Task An_inherited_cursor_is_still_inherited_afterwards() => Session.Dispatch(() =>
        {
            var child = new Border { Width = 50, Height = 50 };
            var window = new ToolWindow { Width = 200, Height = 150, Content = child };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(child.IsSet(InputElement.CursorProperty));

            using var idle = new IdleCursor(child, Never);
            idle.Hide();
            Assert.True(idle.IsHidden);

            idle.Show();

            Assert.False(child.IsSet(InputElement.CursorProperty),
                "The cursor was restored by assignment, so a value that was inherited is now local.");

            window.Close();
        }, default);

        // The other half of §76.4: a control that DID own its cursor gets that one back.
        [Fact]
        public Task A_local_cursor_is_put_back_exactly() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            var hand = new Cursor(StandardCursorType.Hand);
            window.Cursor = hand;

            using var idle = new IdleCursor(window, Never);
            idle.Hide();
            Assert.Equal(StandardCursorType.None, Kind(window));

            idle.Show();

            Assert.True(window.IsSet(InputElement.CursorProperty));
            Assert.Same(hand, window.Cursor);

            window.Close();
        }, default);

        [Fact]
        public Task A_cursor_changed_by_somebody_else_is_not_clobbered() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();
            window.Cursor = new Cursor(StandardCursorType.Hand);

            using var idle = new IdleCursor(window, Never);
            idle.Hide();
            Assert.True(idle.IsHidden);

            // Somebody else takes ownership while it is hidden - a busy indicator, say.
            var wait = new Cursor(StandardCursorType.Wait);
            window.Cursor = wait;

            idle.Show();

            Assert.Same(wait, window.Cursor);

            window.Close();
        }, default);

        [Fact]
        public Task Disposing_brings_the_pointer_back_and_stops_watching() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            var idle = new IdleCursor(window, Never);
            idle.Hide();
            Assert.True(idle.IsHidden);

            idle.Dispose();

            Assert.False(idle.IsHidden);
            Assert.False(window.IsSet(InputElement.CursorProperty));

            // Stopped watching: it cannot be made to hide again.
            idle.Hide();
            Assert.False(idle.IsHidden);
            Assert.False(window.IsSet(InputElement.CursorProperty));

            window.Close();
        }, default);

        // §76.2: a control mid-drag handles PointerMoved itself, and that still has to count.
        [Fact]
        public Task A_move_a_child_consumes_still_counts_as_activity() => Session.Dispatch(() =>
        {
            var child = new Border { Width = 50, Height = 50 };
            var window = new ToolWindow { Width = 200, Height = 150, Content = child };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            child.AddHandler(InputElement.PointerMovedEvent, (object? _, PointerEventArgs e) => e.Handled = true,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

            using var idle = new IdleCursor(window, Never);
            idle.Hide();
            Assert.True(idle.IsHidden);

            Move(child, new Point(5, 5));

            Assert.False(idle.IsHidden, "A child handled the move, so the window never saw the activity.");

            window.Close();
        }, default);

        // §76.2 again, from the other side: a child with its OWN cursor keeps it, so the pointer
        // reappears over a sortable table heading. Recorded as a guard rather than as a sentence.
        [Fact]
        public Task A_child_with_its_own_cursor_keeps_it() => Session.Dispatch(() =>
        {
            var child = new Border { Width = 50, Height = 50, Cursor = new Cursor(StandardCursorType.Hand) };
            var window = new ToolWindow { Width = 200, Height = 150, Content = child };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            using var idle = new IdleCursor(window, Never);
            idle.Hide();

            // The window inherits down to children that set nothing, which is what makes attaching
            // to a Window work at all - but this child set something.
            Assert.Equal(StandardCursorType.None, Kind(window));
            Assert.Equal(StandardCursorType.Hand, Kind(child));

            window.Close();
        }, default);

        [Fact]
        public Task Hiding_and_revealing_are_announced_once_each() => Session.Dispatch(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            using var idle = new IdleCursor(window, Never);
            var seen = new List<bool>();
            idle.HiddenChanged += on => seen.Add(on);

            idle.Hide();
            idle.Hide();      // already hidden
            idle.Show();
            idle.Show();      // already visible

            Assert.Equal(new[] { true, false }, seen);

            window.Close();
        }, default);

        [Fact]
        public void A_target_is_required_and_a_delay_must_be_positive()
        {
            Assert.Throws<ArgumentNullException>(() => new IdleCursor(null!));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new IdleCursor(new Border(), TimeSpan.Zero));
        }

        // The enum is not readable off Cursor directly, so its rendered name is what there is.
        private static StandardCursorType? Kind(InputElement target)
        {
            string? text = target.Cursor?.ToString();
            return text is null ? null : Enum.Parse<StandardCursorType>(text);
        }
    }
}
