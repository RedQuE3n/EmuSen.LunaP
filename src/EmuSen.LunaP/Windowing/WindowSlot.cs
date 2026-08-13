using System;
using Avalonia.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Windowing
{
    // "At most one of these, else bring it forward" - the pattern seven call sites hand-wrote - see docs/LunaP.md §8.3.
    /// <summary>Keeps at most one window of a kind open, bringing the existing one forward instead of making a second.</summary>
    public sealed class WindowSlot<TWindow> where TWindow : Window
    {
        public TWindow? Current { get; private set; }

        public bool IsOpen => Current is not null;

        // Creates the window, or refreshes and activates the one already open.
        public void Show(Window? owner, Func<TWindow> create, Action<TWindow>? refresh = null) => UiThread.Run(() =>
        {
            if (Current is { } existing)
            {
                refresh?.Invoke(existing);
                existing.Activate();
                return;
            }

            TWindow window = create();
            Current = window;
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(Current, window)) Current = null;
            };

            if (owner is null) window.Show();
            else window.Show(owner);
        });

        // Never creates and never activates: a core swap should not pop up a dashboard nobody asked for, or steal focus mid-game.
        //
        // This one marshals and that is not free of consequence - §11.2 records a caller for which
        // marshalling was exactly wrong, because its work had to run on the thread owning the core.
        // Such a caller must not come through here.
        public void RefreshIfOpen(Action<TWindow> refresh) => UiThread.Run(() =>
        {
            if (Current is { } existing) refresh(existing);
        });

        public void Close() => UiThread.Run(() => Current?.Close());
    }
}
