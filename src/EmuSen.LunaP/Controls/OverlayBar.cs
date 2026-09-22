using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // OpenEmu's in-game bar: a strip of controls over the picture that appears when the pointer moves
    // and fades a fixed time after it stops - see docs/LunaP.md §88.4.
    //
    // The host places it, in a Grid cell over the element it watches, because only the host knows
    // what the bar floats over and where on it the bar belongs; the default style puts it at the
    // bottom centre. The bar does the one thing a host would otherwise hand-roll with a timer and
    // three flags: decide when it is showing.
    //
    // THE HIDING RULE IS OpenEmu's, read from GameControlsBar.swift. A timer fires HideAfter after the
    // last movement (fadeOutDelay, 1.5 s); if the pointer is over the bar then, it does not hide
    // (canFadeOut) and waits another interval. This bar re-arms on the pointer leaving instead of
    // polling, which is the same outcome without a timer running while somebody reads a tooltip.
    // KeepOpen is the second half of canFadeOut for a toolkit: a menu opened from the bar is a popup
    // outside its bounds, so "the pointer is over the bar" goes false the moment the menu is used.
    /// <summary>A bar of controls laid over another element, revealed when the pointer moves over that element and concealed a fixed time after it stops.</summary>
    public class OverlayBar : ContentControl
    {
        public static readonly StyledProperty<Control?> WatchProperty =
            AvaloniaProperty.Register<OverlayBar, Control?>(nameof(Watch));

        public static readonly StyledProperty<TimeSpan> HideAfterProperty =
            AvaloniaProperty.Register<OverlayBar, TimeSpan>(nameof(HideAfter), TimeSpan.FromSeconds(1.5));

        public static readonly StyledProperty<bool> KeepOpenProperty =
            AvaloniaProperty.Register<OverlayBar, bool>(nameof(KeepOpen));

        public static readonly DirectProperty<OverlayBar, bool> IsRevealedProperty =
            AvaloniaProperty.RegisterDirect<OverlayBar, bool>(nameof(IsRevealed), o => o.IsRevealed);

        private bool _revealed;
        private Debounce? _hide;
        private TimeSpan _hideDelay;
        private Control? _watched;

        public OverlayBar()
        {
            // Handled events too: a game view that marks pointer movement handled for its own input
            // must still reveal the bar, and it is the likeliest thing to be watched.
            AddHandler(PointerMovedEvent, OnPointerMovedOverBar, RoutingStrategies.Bubble, handledEventsToo: true);

            // Tabbing into a concealed bar reveals it, and it stays while focus is inside: a keyboard
            // user has no pointer to move, and a focused button nobody can see is a trap.
            AddHandler(GotFocusEvent, (_, _) => Reveal(), RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(LostFocusEvent, (_, _) =>
            {
                if (_revealed) Hide.Poke();
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            UpdatePseudoClasses();
        }

        /// <summary>The element whose pointer movement reveals the bar - typically the game view it floats over. The bar's own movement counts too.</summary>
        public Control? Watch
        {
            get => GetValue(WatchProperty);
            set => SetValue(WatchProperty, value);
        }

        /// <summary>How long after the last pointer movement the bar conceals itself. 1.5 seconds by default, OpenEmu's own delay.</summary>
        public TimeSpan HideAfter
        {
            get => GetValue(HideAfterProperty);
            set => SetValue(HideAfterProperty, value);
        }

        // True while something the bar opened is still in use - a menu, a slider being dragged in a
        // popup - so the timer cannot pull the bar out from under it. Setting it back to false
        // starts a fresh HideAfter rather than hiding at once, as the pointer leaving does.
        /// <summary>While true the bar never conceals itself; set it while a menu or popup opened from the bar is open. Clearing it restarts the hide delay.</summary>
        public bool KeepOpen
        {
            get => GetValue(KeepOpenProperty);
            set => SetValue(KeepOpenProperty, value);
        }

        /// <summary>Whether the bar is showing. While false it is transparent and ignores the pointer.</summary>
        public bool IsRevealed => _revealed;

        /// <summary>Raised with the new value whenever the bar is revealed or concealed, from any cause.</summary>
        public event Action<bool>? RevealedChanged;

        // Shows the bar and starts the hide delay, exactly as a pointer movement does.
        /// <summary>Shows the bar and starts the hide delay, as a pointer movement would.</summary>
        public void Reveal()
        {
            SetRevealed(true);
            Hide.Poke();
        }

        // THE CALLER OVERRULES THE RULE. Conceal is a host saying "not now" - a pause menu opening, a
        // switch to full-screen - and it hides even under the pointer or with KeepOpen set; the
        // rule governs the timer, not the host.
        /// <summary>Hides the bar at once and cancels the hide delay. Unlike the delay, this hides even while the pointer is over the bar or KeepOpen is set.</summary>
        public void Conceal()
        {
            _hide?.Cancel();
            SetRevealed(false);
        }

        // Whether the hide delay is running. Public because it is the one fact a host or a test cannot
        // otherwise see: after the delay passed with the pointer over the bar, IsRevealed alone does
        // not say whether the bar has decided to stay or simply not been asked yet.
        /// <summary>Whether the hide delay is running, so the bar will conceal itself when it elapses unless the pointer is over it or KeepOpen is set.</summary>
        public bool IsHidePending => _hide?.IsPending ?? false;

        private Debounce Hide
        {
            get
            {
                // Rebuilt when the delay changes, since a Debounce fixes its interval at construction.
                // A zero or negative delay becomes one millisecond, the shortest Debounce accepts.
                if (_hide is null || _hideDelay != HideAfter)
                {
                    _hide?.Cancel();
                    _hideDelay = HideAfter;
                    TimeSpan interval = HideAfter > TimeSpan.Zero ? HideAfter : TimeSpan.FromMilliseconds(1);
                    _hide = new Debounce(interval, OnHideElapsed);
                }

                return _hide;
            }
        }

        // The rule: not while the pointer is on the bar, not while KeepOpen, not while the keyboard
        // is inside it. Leaving the bar, clearing KeepOpen or tabbing out restarts the delay, so a
        // bar kept open is never stranded open.
        private void OnHideElapsed()
        {
            if (IsPointerOver || KeepOpen || IsKeyboardFocusWithin) return;
            SetRevealed(false);
        }

        private void SetRevealed(bool value)
        {
            if (_revealed == value) return;

            _revealed = value;
            UpdatePseudoClasses();
            RaisePropertyChanged(IsRevealedProperty, !value, value);
            RevealedChanged?.Invoke(value);
        }

        // Concealed means not hit-testable as well as invisible, so a click on the game where the bar
        // would be reaches the game. The theme draws the fade from the :revealed pseudo-class.
        private void UpdatePseudoClasses()
        {
            PseudoClasses.Set(":revealed", _revealed);
            IsHitTestVisible = _revealed;
        }

        private void OnPointerMovedOverBar(object? sender, PointerEventArgs e) => Reveal();

        private void OnWatchedPointerMoved(object? sender, PointerEventArgs e) => Reveal();

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (_revealed) Hide.Poke();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WatchProperty)
            {
                _watched?.RemoveHandler(PointerMovedEvent, OnWatchedPointerMoved);
                _watched = Watch;
                _watched?.AddHandler(PointerMovedEvent, OnWatchedPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
            }
            else if (change.Property == KeepOpenProperty && !KeepOpen && _revealed)
            {
                Hide.Poke();
            }
        }

        // A bar that is no longer on screen has nothing to hide, and a timer left running would fire
        // into a window that has gone.
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _hide?.Cancel();
            base.OnDetachedFromVisualTree(e);
        }

        // A ToolBar, because that is what the bar is: a run of commands. It stays in the automation
        // tree while concealed - a reader has no pointer to move, and hiding the controls from the
        // one user who cannot summon them would make them unreachable.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ToolBar);
    }
}
