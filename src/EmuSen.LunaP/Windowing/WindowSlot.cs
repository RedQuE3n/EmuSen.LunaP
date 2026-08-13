using System;
using Avalonia.Controls;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Windowing
{
    // "At most one of these, else bring it forward" - the pattern seven call sites hand-wrote - see docs/LunaP.md §8.3.
    /// <summary>Keeps at most one window of a kind open, bringing the existing one forward instead of making a second.</summary>
    public sealed class WindowSlot<TWindow> where TWindow : Window
    {
        /// <summary>The open window, or null when none is open.</summary>
        public TWindow? Current { get; private set; }

        /// <summary>Whether a window is currently open in this slot.</summary>
        public bool IsOpen => Current is not null;

        // Creates the window, or refreshes and activates the one already open.
        /// <summary>Shows the window, or brings the open one forward and refreshes it instead of opening a second.</summary>
        /// <param name="owner">The parent window, so the child stays above it. Null for a top-level window with no owner.</param>
        /// <param name="create">Builds the window. Called only when none is open, so it must not be relied on to run on every Show.</param>
        /// <param name="refresh">Runs against the window whether it was just created or already open, which is where the work that must happen every time belongs.</param>
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
        /// <summary>Runs an update against the window if one is open, and does nothing otherwise.</summary>
        /// <param name="refresh">Runs on the UI thread against the open window. Not called when the slot is empty, so a poll loop can call this unconditionally.</param>
        public void RefreshIfOpen(Action<TWindow> refresh) => UiThread.Run(() =>
        {
            if (Current is { } existing) refresh(existing);
        });

        /// <summary>Closes the window if one is open. Does nothing otherwise.</summary>
        public void Close() => UiThread.Run(() => Current?.Close());
    }
}
