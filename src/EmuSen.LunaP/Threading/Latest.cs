using System;
using System.Threading;
using Avalonia.Threading;

namespace EmuSen.LunaP.Threading
{
    // "A worker produces faster than the screen can show it; show the newest and drop the rest."
    //
    // Three applications wrote this out and the three implementations were byte-identical, which
    // is the clearest signal in the whole survey that it belonged here - see docs/LunaP.md §21.1.
    // A frame source, a telemetry poll and a log tail all want it, and none of them wants a queue:
    // a stale frame is not worth drawing, it is worth skipping.
    //
    // The contract: Offer is safe from any thread and never blocks. `present` runs on the UI
    // thread, at most once per value that is still current when it gets there, and never
    // concurrently with itself.
    //
    // Reference types only. The lock-free path below is Interlocked.Exchange over a field, which
    // needs a reference; boxing a struct to fit would allocate per offer and this exists to avoid
    // exactly that. A caller with a struct can wrap it in one.
    public sealed class Latest<T> where T : class
    {
        private readonly Action<T> _present;

        private T? _pending;
        private int _scheduled;

        public Latest(Action<T> present) =>
            _present = present ?? throw new ArgumentNullException(nameof(present));

        // Hands over a value. The previous one, if it has not been shown yet, is dropped.
        public void Offer(T value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            Interlocked.Exchange(ref _pending, value);

            // At most one callback outstanding. A second offer arriving before the first is drained
            // only replaces the value; it does not queue more work.
            if (Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
            {
                Dispatcher.UIThread.Post(Drain);
            }
        }

        private void Drain()
        {
            T? value = Interlocked.Exchange(ref _pending, null);

            // CLEARED BEFORE PRESENTING, AND NOT AFTER, WHICH IS THE ONE PLACE THIS DIFFERS FROM
            // THE THREE COPIES IT REPLACES. They reset the flag in a `finally` after the hand-off,
            // so an offer arriving during the present could neither schedule (the flag still read
            // 1) nor be picked up (the flag then went to 0 with nothing queued). That value sat
            // there until the next offer pushed it out.
            //
            // At 60 frames a second nobody could see it - the next frame arrived 16 ms later and
            // carried the fix with it. It shows when the stream STOPS: pause an emulator and the
            // final frame is the one at risk, which is the frame somebody is about to sit and look
            // at. Clearing first means a late offer can always schedule again.
            Interlocked.Exchange(ref _scheduled, 0);

            if (value is not null) _present(value);

            // Closing the same window from the other side. An offer landing between the two
            // Exchanges above set _pending while _scheduled still read 1, so it could not queue
            // anything. Nobody else will notice it, so this does.
            if (Volatile.Read(ref _pending) is not null
                && Interlocked.CompareExchange(ref _scheduled, 1, 0) == 0)
            {
                Dispatcher.UIThread.Post(Drain);
            }
        }
    }
}
