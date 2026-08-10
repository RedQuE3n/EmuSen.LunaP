using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Tests
{
    // The threading and state primitives - see docs/LunaP.md §21.1.
    //
    // Nothing here races a clock. §8.2 settled that for the polling tests and it applies just as
    // well to Debounce: a test that waits for a real timer inside a dispatcher it is itself
    // blocking is measuring the machine, not the code. The timer assertions are about state, and
    // the coalescing assertions drain the dispatcher explicitly with RunJobs.
    public class ThreadingTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ThreadingTests).GetTypeInfo().Assembly);

        [Fact]
        public Task Run_is_inline_when_already_on_the_ui_thread() => Session.Dispatch(() =>
        {
            bool ran = false;
            UiThread.Run(() => ran = true);

            // Not "it ran eventually" - it ran before Run returned. WindowSlot depends on this:
            // Current has to be set by the time Show() comes back.
            Assert.True(ran);
        }, default);

        [Fact]
        public Task Post_defers_even_when_called_from_the_ui_thread() => Session.Dispatch(() =>
        {
            bool ran = false;
            UiThread.Post(() => ran = true);

            Assert.False(ran);

            Dispatcher.UIThread.RunJobs();
            Assert.True(ran);
        }, default);

        [Fact]
        public Task Latest_presents_only_the_newest_of_several_offers() => Session.Dispatch(() =>
        {
            var seen = new List<string>();
            var latest = new Latest<string>(seen.Add);

            latest.Offer("one");
            latest.Offer("two");
            latest.Offer("three");

            Dispatcher.UIThread.RunJobs();

            // One callback, carrying the newest. The middle values are dropped by design - this
            // exists so a slow screen does not force a fast producer to queue.
            Assert.Equal(new[] { "three" }, seen);
        }, default);

        // The defect the three copied implementations share, pinned so this one cannot regress into
        // them. They clear the scheduled flag AFTER presenting, so a value offered during the
        // present can neither schedule nor be picked up, and sits until something else arrives.
        // Reversing the order to clear-then-present is the whole fix - see docs/LunaP.md §21.1.
        [Fact]
        public Task Latest_does_not_strand_a_value_offered_while_presenting() => Session.Dispatch(() =>
        {
            var seen = new List<string>();
            Latest<string>? latest = null;

            latest = new Latest<string>(value =>
            {
                seen.Add(value);

                // Exactly the shape that strands: a producer offering again while the UI thread is
                // inside the callback for the previous value.
                if (value == "first") latest!.Offer("second");
            });

            latest.Offer("first");
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { "first", "second" }, seen);
        }, default);

        [Fact]
        public void Suppressor_is_inactive_until_a_scope_is_opened()
        {
            var suppressor = new Suppressor();
            Assert.False(suppressor.IsSuppressing);

            using (suppressor.Suppress()) Assert.True(suppressor.IsSuppressing);

            Assert.False(suppressor.IsSuppressing);
        }

        // The reason this is a counter and not the bool every hand-rolled copy used: an inner scope
        // closing must not re-enable notifications while an outer one is still open.
        [Fact]
        public void Suppressor_nests()
        {
            var suppressor = new Suppressor();

            using (suppressor.Suppress())
            {
                using (suppressor.Suppress()) Assert.True(suppressor.IsSuppressing);

                Assert.True(suppressor.IsSuppressing);
            }

            Assert.False(suppressor.IsSuppressing);
        }

        // A double dispose must not drive the depth below zero, which would reopen a scope that is
        // still meant to be closed the next time one is opened.
        [Fact]
        public void Suppressor_scope_survives_being_disposed_twice()
        {
            var suppressor = new Suppressor();

            IDisposable scope = suppressor.Suppress();
            scope.Dispose();
            scope.Dispose();

            using (suppressor.Suppress()) Assert.True(suppressor.IsSuppressing);

            Assert.False(suppressor.IsSuppressing);
        }

        [Fact]
        public Task Debounce_holds_the_action_until_it_is_flushed() => Session.Dispatch(() =>
        {
            int runs = 0;
            var debounce = new Debounce(TimeSpan.FromSeconds(30), () => runs++);

            debounce.Poke();
            debounce.Poke();
            debounce.Poke();

            // Thirty seconds is chosen so the timer cannot possibly fire during the test: what is
            // being asserted is that three pokes are one pending action, not how long a clock takes.
            Assert.True(debounce.IsPending);
            Assert.Equal(0, runs);

            debounce.Flush();

            Assert.False(debounce.IsPending);
            Assert.Equal(1, runs);
        }, default);

        [Fact]
        public Task Debounce_cancel_drops_the_pending_action() => Session.Dispatch(() =>
        {
            int runs = 0;
            var debounce = new Debounce(TimeSpan.FromSeconds(30), () => runs++);

            debounce.Poke();
            debounce.Cancel();

            Assert.False(debounce.IsPending);

            // Flush after a cancel does nothing: there is nothing outstanding to bring forward.
            debounce.Flush();
            Assert.Equal(0, runs);
        }, default);

        [Fact]
        public Task Dropdown_fill_still_does_not_raise_chose() => Session.Dispatch(() =>
        {
            var dropdown = new Dropdown();
            int chose = 0;
            dropdown.Chose += _ => chose++;

            dropdown.Fill(new[] { "a", "b", "c" }, "b");

            // The behaviour Fill has always had, re-asserted because its bool became a Suppressor.
            Assert.Equal(0, chose);
            Assert.Equal("b", dropdown.SelectedItem);
        }, default);

        [Fact]
        public Task FilterBar_raises_changed_immediately_when_no_delay_is_set() => Session.Dispatch(() =>
        {
            (Window window, FilterBar bar, TextBox search) = ShowFilterBar();
            int changed = 0;
            bar.Changed += () => changed++;

            search.Text = "zel";

            // The default, and the behaviour every existing consumer already depends on.
            Assert.Equal(TimeSpan.Zero, bar.SearchDelay);
            Assert.Equal(1, changed);

            window.Close();
        }, default);

        [Fact]
        public Task FilterBar_defers_changed_when_a_delay_is_set() => Session.Dispatch(() =>
        {
            (Window window, FilterBar bar, TextBox search) = ShowFilterBar();
            bar.SearchDelay = TimeSpan.FromSeconds(30);

            int changed = 0;
            bar.Changed += () => changed++;

            search.Text = "z";
            search.Text = "ze";
            search.Text = "zel";

            // Three keystrokes, no work done yet. Without the delay this would read 3, which is
            // three re-queries of an on-disk database for one word.
            Assert.Equal(0, changed);

            // SearchText is NOT deferred - only the notification is, so a caller reading it from
            // inside Changed sees what was actually typed.
            Assert.Equal("zel", bar.SearchText);

            window.Close();
        }, default);

        [Fact]
        public Task FilterBar_enter_brings_a_pending_change_forward() => Session.Dispatch(() =>
        {
            (Window window, FilterBar bar, TextBox search) = ShowFilterBar();
            bar.SearchDelay = TimeSpan.FromSeconds(30);

            var order = new List<string>();
            bar.Changed += () => order.Add("changed");
            bar.Submitted += () => order.Add("submitted");

            search.Text = "zel";
            search.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Enter,
            });

            // Changed has to land BEFORE Submitted, or a caller that filters on one and acts on the
            // other acts on the results of the previous keystroke.
            Assert.Equal(new[] { "changed", "submitted" }, order);

            window.Close();
        }, default);

        // A FilterBar needs a template before PART_Search exists, and a template needs the theme -
        // §3.1 is why the harness loads the real one rather than building a FluentTheme by hand.
        private static (Window Window, FilterBar Bar, TextBox Search) ShowFilterBar()
        {
            var bar = new FilterBar();
            var window = new Window { Width = 300, Height = 80, Content = bar };
            window.Show();

            TextBox search = bar.FindPart<TextBox>()
                ?? throw new InvalidOperationException("FilterBar has no TextBox - it was not templated.");

            return (window, bar, search);
        }
    }
}
