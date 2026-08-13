using System;
using Avalonia.Threading;

namespace EmuSen.LunaP.Threading
{
    // "Wait until they stop typing, then do the expensive thing once."
    //
    // Nothing in any consumer does this, and two of them want it badly enough to be worth
    // recording: one re-filters a ROM library on every keystroke and one re-queries an on-disk
    // cheat database on every keystroke - see docs/LunaP.md §21.1. Neither is wrong at ten
    // entries and both are wrong at ten thousand.
    //
    // Trailing edge only. Poke() restarts the clock; the action runs once, `delay` after the last
    // poke. A leading-edge variant (act now, then ignore for a while) is a different control with
    // a different feel and is not offered here rather than being offered badly - the search boxes
    // this exists for all want the trailing edge, because the first keystroke of a word is the
    // least informative one.
    //
    // UI-THREAD ONLY. DispatcherTimer raises on the dispatcher, so the action runs there and the
    // caller never has to marshal; the cost is that Poke() must be called from the UI thread too.
    // That matches every use it was written for - they are all reacting to a keystroke - and a
    // worker-thread producer wants Latest<T> instead.
    /// <summary>Waits until a burst of calls stops, then does the expensive work once.</summary>
    public sealed class Debounce
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _action;

        public Debounce(TimeSpan delay, Action action)
        {
            if (delay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));

            _action = action ?? throw new ArgumentNullException(nameof(action));
            _timer = new DispatcherTimer { Interval = delay };
            _timer.Tick += (_, _) =>
            {
                // Stopped before the action, not after: the action is allowed to poke again
                // (a search that discovers it needs a second pass), and stopping afterwards would
                // silently cancel that.
                _timer.Stop();
                _action();
            };
        }

        public bool IsPending => _timer.IsEnabled;

        // Restarts the delay. Stop-then-start rather than leaving it running, because a timer that
        // is merely still enabled would fire `delay` after the FIRST poke instead of the last,
        // which is the whole behaviour being bought here.
        public void Poke()
        {
            _timer.Stop();
            _timer.Start();
        }

        // Drops anything pending. For a window closing, or a filter being cleared by something
        // other than typing.
        public void Cancel() => _timer.Stop();

        // Runs now if something is pending, otherwise does nothing. For Enter, where the user has
        // said they are finished and waiting out the delay would just feel slow.
        public void Flush()
        {
            if (!_timer.IsEnabled) return;

            _timer.Stop();
            _action();
        }
    }
}
