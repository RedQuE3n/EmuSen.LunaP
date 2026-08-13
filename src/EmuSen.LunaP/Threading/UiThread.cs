using System;
using Avalonia.Threading;

namespace EmuSen.LunaP.Threading
{
    // Getting onto the UI thread, which every application that touches a control from a worker
    // has to do and which four of them wrote out by hand - see docs/LunaP.md §21.1.
    //
    // This is deliberately two methods rather than one, because "marshal to the UI thread" is
    // two different requests and conflating them is how the wrong one gets used:
    //
    //   Run   - already there? do it now. Otherwise queue it.
    //   Post  - queue it regardless, even when the caller is the UI thread itself.
    //
    // Run is what almost everything wants. Post exists for the case where a caller must not
    // re-enter itself - raising an event from inside a layout pass, say - and needs the work to
    // happen after the current one finishes rather than inside it.
    /// <summary>Getting work onto the UI thread from a worker, without each caller rediscovering how.</summary>
    public static class UiThread
    {
        // True when the caller is already the UI thread. Worth having in its own right: the
        // honest answer to "should this be marshalled" is sometimes "no", and code that cannot
        // ask has to guess.
        /// <summary>Whether the calling thread is the UI thread.</summary>
        public static bool IsCurrent => Dispatcher.UIThread.CheckAccess();

        // Inline when already on the thread, queued otherwise.
        //
        // The inline half is not an optimisation, it is the reason WindowSlot has always done it
        // this way: always posting would mean a caller on the UI thread does not see the effect
        // until after it returns, so `slot.Show(...); slot.Current` would read null. A seam whose
        // observable behaviour depends on which thread called it is worse than no seam.
        //
        // NOT EVERYTHING SHOULD GO THROUGH HERE, and §11.2 is the recorded case: EmuSen's
        // DrainPendingFromEmulationThread must run on the thread that owns the emulator core, and
        // marshalling it was exactly wrong while being right for every other caller of the same
        // class. If the work belongs to another thread, do not call this - there is no flag to
        // pass, because a flag would only move the same decision somewhere less visible.
        /// <summary>Runs an action on the UI thread, inline if already there and by posting otherwise.</summary>
        /// <param name="action">What to run. Called synchronously when the caller is already on the UI thread, so it must not assume it has been deferred.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="action"/> is null.</exception>
        public static void Run(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        }

        // Always queued, never inline.
        /// <summary>Queues an action to run on the UI thread later, always deferring even when called from the UI thread.</summary>
        /// <param name="action">What to run. Use this rather than Run when the work must not happen inside the current call - finishing a layout before measuring it, say.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="action"/> is null.</exception>
        public static void Post(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            Dispatcher.UIThread.Post(action);
        }
    }
}
