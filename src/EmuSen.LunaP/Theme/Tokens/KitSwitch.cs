using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace EmuSen.LunaP.Theme
{
    // A kit switch's change notification, each subscriber told on the dispatcher it subscribed from, whichever thread set the switch - see docs/LunaP.md §214.1.
    internal sealed class KitSwitch
    {
        private readonly List<(Dispatcher Dispatcher, Action Handler)> _subscribers = new();

        public void Add(Action? handler)
        {
            if (handler is null) return;
            lock (_subscribers) _subscribers.Add((Dispatcher.CurrentDispatcher, handler));
        }

        public void Remove(Action? handler)
        {
            if (handler is null) return;
            lock (_subscribers)
            {
                int at = _subscribers.FindLastIndex(s => s.Handler == handler);
                if (at >= 0) _subscribers.RemoveAt(at);
            }
        }

        public void Raise()
        {
            (Dispatcher Dispatcher, Action Handler)[] now;
            lock (_subscribers) now = _subscribers.ToArray();
            foreach ((Dispatcher dispatcher, Action handler) in now)
                if (dispatcher.CheckAccess()) handler();
                else dispatcher.Post(handler);
        }
    }
}
