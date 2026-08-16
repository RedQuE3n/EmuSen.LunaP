using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Windowing
{
    // Hides the pointer once it has been still for a while, and brings it back the moment it moves -
    // see docs/LunaP.md §76.
    //
    // A NORMAL OBJECT ATTACHED TO A CONTROL, NOT A PROPERTY ON ToolWindow, and that is the design
    // rather than a convenience. §8.1 keeps ToolWindow deliberately thin, and a timer plus a pointer
    // subscription is not thin. More to the point, "the whole window" is only one of the two things
    // callers want: an emulator frontend in a window wants the cursor hidden over the VIDEO SURFACE
    // and perfectly visible over the toolbar beside it, which a window-level flag cannot express.
    // Attaching to an InputElement covers both, because a Window is one.
    //
    //     _idle = new IdleCursor(this);                       // the whole window, three seconds
    //     _idle = new IdleCursor(screen, TimeSpan.FromSeconds(1));
    //
    // ONLY POINTER MOVEMENT COUNTS AS ACTIVITY. Keystrokes deliberately do not, because the
    // application this is for is one somebody is holding four keys down in - a cursor that reappeared
    // on every input would never be hidden at all, which is the whole feature inverted.
    //
    // DISPOSE IT. The cursor is restored on disposal, and a hidden cursor left behind by an object
    // nobody unsubscribed is an application whose pointer is gone for good.
    /// <summary>Hides the pointer over a control once it has been still for a while, and restores it as soon as it moves.</summary>
    public sealed class IdleCursor : IDisposable
    {
        // Shared, because a Cursor holds a platform handle and one per instance would be one handle
        // per window for a cursor that is the same everywhere.
        private static readonly Cursor Invisible = new(StandardCursorType.None);

        /// <summary>How long the pointer must be still before it is hidden, when no other delay is given.</summary>
        public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(3);

        private readonly InputElement _target;
        private readonly Debounce _idle;

        private Cursor? _restore;
        private bool _restoreWasLocal;
        private Point? _last;
        private bool _disposed;

        /// <summary>Hides the pointer over a control after DefaultDelay of stillness.</summary>
        /// <param name="target">The control to hide the pointer over. A Window covers everything in it.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="target"/> is null.</exception>
        public IdleCursor(InputElement target) : this(target, DefaultDelay)
        {
        }

        // Two constructors rather than one with an optional parameter. Adding an optional parameter
        // later is source-compatible but BINARY-breaking: a consumer who upgrades the package
        // without recompiling gets a MissingMethodException, and §26.13's standard is that a
        // consumer who upgrades and changes nothing has the same application. An added overload
        // costs a line and breaks nobody.
        /// <summary>Hides the pointer over a control after a given period of stillness.</summary>
        /// <param name="target">The control to hide the pointer over. A Window covers everything in it.</param>
        /// <param name="delay">How long the pointer must be still first. Each movement restarts it.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="delay"/> is zero or negative.</exception>
        public IdleCursor(InputElement target, TimeSpan delay)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _idle = new Debounce(delay, Hide);

            // TUNNEL AND handledEventsToo, so that a child which consumes the move still counts as
            // activity. A table in the middle of a column drag handles PointerMoved itself, and a
            // cursor that vanished mid-drag because the control underneath was doing its job would
            // be the most confusing possible version of this feature.
            _target.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved,
                RoutingStrategies.Tunnel, handledEventsToo: true);

            // The clock starts now rather than on the first movement: a window that opens under a
            // pointer nobody then touches should still end up with a hidden cursor.
            _idle.Poke();
        }

        /// <summary>Whether the pointer is hidden right now.</summary>
        public bool IsHidden { get; private set; }

        /// <summary>Raised when the pointer is hidden or brought back. The argument is the new state.</summary>
        public event Action<bool>? HiddenChanged;

        // For a caller with its own idea of activity - a gamepad, a media player leaving playback.
        /// <summary>Brings the pointer back if it is hidden, and restarts the delay.</summary>
        public void Show()
        {
            Reveal();
            if (!_disposed) _idle.Poke();
        }

        // Hides now rather than after the delay, for a caller that already knows the pointer is not
        // wanted: entering full screen is the case this exists for, where waiting three seconds to
        // lose a cursor nobody is using reads as the feature being broken. The idle path calls this
        // too, so there is one way for the cursor to go.
        /// <summary>Hides the pointer immediately, without waiting out the delay. Does nothing if it is already hidden.</summary>
        public void Hide()
        {
            if (IsHidden || _disposed) return;

            // Whether the value was LOCAL matters more than what it was. Restoring a value that was
            // inherited or came from a style by assigning it back would turn it into a local one,
            // which then wins over the style that owns it for the rest of the control's life.
            _restoreWasLocal = _target.IsSet(InputElement.CursorProperty);
            _restore = _restoreWasLocal ? _target.Cursor : null;

            IsHidden = true;
            _target.Cursor = Invisible;
            HiddenChanged?.Invoke(true);
        }

        /// <summary>Restores the pointer and stops watching. Not disposing this leaves an application whose pointer never comes back.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _idle.Cancel();
            _target.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            Reveal();
        }

        // A MOVE THAT DID NOT MOVE IS NOT ACTIVITY, and this is the guard that makes the feature
        // work at all rather than a refinement of it. PointerMoved arrives for reasons that are not
        // the user moving the mouse - a window activating under a stationary pointer is the common
        // one - and treating those as activity means a cursor that can never quite reach its delay.
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            Point position = e.GetPosition(_target);
            if (_last is { } previous && previous == position) return;

            _last = position;
            Show();
        }

        private void Reveal()
        {
            if (!IsHidden) return;
            IsHidden = false;

            // Somebody else has set a cursor since this one was hidden, so the value being held is
            // stale and putting it back would undo their change rather than this one.
            if (!ReferenceEquals(_target.Cursor, Invisible))
            {
                HiddenChanged?.Invoke(false);
                return;
            }

            if (_restoreWasLocal) _target.Cursor = _restore;
            else _target.ClearValue(InputElement.CursorProperty);

            HiddenChanged?.Invoke(false);
        }
    }
}
