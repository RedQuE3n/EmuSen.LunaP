using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace EmuSen.LunaP.Windowing
{
    // A dashboard that re-reads its source on a timer. Five windows hand-rolled this; none of them stopped while hidden - see docs/LunaP.md §8.2.
    /// <summary>A dashboard window that re-reads its source on a timer, and stops while it is hidden.</summary>
    public abstract class PollingWindow : ToolWindow
    {
        private DispatcherTimer? _timer;
        private bool _started;

        /// <summary>How often Refresh is called. Read once when polling starts, so returning a changing value does not retime a running timer.</summary>
        protected abstract TimeSpan RefreshInterval { get; }

        /// <summary>Reads whatever this window shows and updates its controls. Called on the UI thread on every tick, so it should be cheap and must not block.</summary>
        protected abstract void Refresh();

        // Whether the timer is actually running right now - false while hidden or minimised. Public so a test can assert suspension without racing a clock.
        /// <summary>Whether the refresh timer is currently running.</summary>
        public bool IsPolling => _timer?.IsEnabled ?? false;

        // Call at the end of a derived constructor for an immediate first paint; Opened calls it anyway, so forgetting is late, not fatal.
        /// <summary>Starts the timer, after a first immediate Refresh. Polling stops on its own when the window closes.</summary>
        protected void StartPolling()
        {
            if (_started) return;

            _started = true;
            _timer = new DispatcherTimer { Interval = RefreshInterval };
            _timer.Tick += (_, _) => Refresh();

            SyncTimer();
            Refresh();
        }

        // Public so a caller that changes what this window is looking at can repaint without waiting for the next tick.
        /// <summary>Refreshes immediately, without waiting for the next tick.</summary>
        public void RefreshNow()
        {
            if (_started) Refresh();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            StartPolling();
            SyncTimer();
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _timer = null;
            base.OnClosed(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsVisibleProperty || change.Property == WindowStateProperty) SyncTimer();
        }

        // Occlusion is not detectable portably, so this covers the two states that are: hidden and minimized.
        private void SyncTimer()
        {
            if (_timer is null) return;

            bool shouldRun = IsVisible && WindowState != WindowState.Minimized;
            if (shouldRun == _timer.IsEnabled) return;

            if (shouldRun)
            {
                _timer.Start();

                // Otherwise the first thing seen after restoring is however stale the data got while hidden.
                Refresh();
            }
            else
            {
                _timer.Stop();
            }
        }
    }
}
