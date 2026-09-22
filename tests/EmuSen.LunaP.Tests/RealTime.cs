using System;
using Avalonia.Threading;

namespace EmuSen.LunaP.Tests
{
    // Lets real time pass inside a dispatched test body, so a DispatcherTimer can fire - see
    // docs/LunaP.md §88.7.
    //
    // §76.9 measured that a DispatcherTimer never fires while a test body owns the dispatcher, and
    // that was true of the method it measured: RunJobs drains queued jobs and does not look at
    // timers. A NESTED DISPATCHER FRAME DOES, which §76.9 did not try. PushFrame runs the dispatcher's
    // own loop - timers included - until the frame is told to stop, and here the thing that stops it
    // is itself a DispatcherTimer. Timers fire in the order they fall due, so a control's 100 ms
    // timer fires before this 400 ms one on any machine, however loaded; what a slow machine changes
    // is how long the wait takes, not what has happened by the end of it.
    //
    // What that does NOT buy is a guarantee that something has NOT happened yet. A test may assert
    // "fired by the end of the wait" and never "not fired by the middle of it", and the tests that
    // use this are written that way.
    internal static class RealTime
    {
        internal static void Wait(TimeSpan span)
        {
            var frame = new DispatcherFrame();
            var stop = new DispatcherTimer { Interval = span };
            stop.Tick += (_, _) =>
            {
                stop.Stop();
                frame.Continue = false;
            };

            stop.Start();
            Dispatcher.UIThread.PushFrame(frame);
        }

        internal static void Wait(int milliseconds) => Wait(TimeSpan.FromMilliseconds(milliseconds));
    }
}
