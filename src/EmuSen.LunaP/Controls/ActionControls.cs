using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // The three controls a LunaAction can appear as - see docs/LunaP.md §26.4.
    //
    // Each one is a stock Avalonia control that has been taught to FOLLOW an action rather than to
    // hold a copy of it. That distinction is the whole reason these types exist: setting
    // `new Button { Content = action.Text, Command = action }` reads as enough, and it is enough
    // right up until the first action whose label changes - "Pause" becoming "Resume" - at which
    // point the button is showing a caption from whenever the window was built.
    //
    // Every one of them pins its style key, AND THE TRAP TURNS OUT NOT TO BITE UNIFORMLY - which
    // was found by removing each override in turn and running the suite, not by reading anything.
    // Dropping it from ActionMenuItem turns a test red: the item keeps its Header, its gesture and
    // its enabled state, and draws nothing. Dropping it from MenuBar turns two red. Dropping it
    // from ActionButton or ActionToggle turns NOTHING red - Avalonia finds those a theme anyway.
    //
    // Why the four differ was not established, and guessing in a comment is worse than saying so.
    // The overrides stay on all four regardless: one line each, and the alternative is depending
    // on a fallback that demonstrably does not apply to every control in this same file. §5.5 and
    // §14.1 record the two occasions the trap did bite; §26.11 records this measurement.

    // What subscribes and, more importantly, what unsubscribes.
    //
    // An action lives as long as the application; a menu item lives as long as the window that
    // shows it. Subscribing in a constructor and never detaching means every closed window's menu
    // items stay reachable from the action's Changed list forever - a leak that grows with the
    // number of times a dialog is opened, which is exactly the shape nobody notices in testing.
    //
    // The logical tree rather than the visual one, because a MenuItem inside a popup is not in the
    // visual tree of its window until the menu is opened, and an item that only followed its
    // action while the menu was on screen would show a stale label for the first frame every time.
    internal static class ActionSync
    {
        public static void Follow(Control target, LunaAction action, Action apply)
        {
            void OnChanged(LunaAction _) => apply();

            target.AttachedToLogicalTree += (_, _) =>
            {
                action.Changed += OnChanged;

                // Applied on attach as well as on change: an action edited while the control was
                // detached raised nothing this control heard.
                apply();
            };

            target.DetachedFromLogicalTree += (_, _) => action.Changed -= OnChanged;

            // And once now, so a control asserted on before it is ever shown - which is what a
            // test does - already reads correctly.
            apply();
        }

        // "Save (Ctrl+S)", plus the help text on a second line where there is one. A toolbar button
        // is the one surface with no room to explain itself, and the shortcut is worth repeating
        // here because a user who never opens the menu has nowhere else to learn it.
        public static string? Tip(LunaAction action)
        {
            string head = action.Shortcut is { } gesture ? $"{action.Text} ({gesture})" : action.Text;
            return string.IsNullOrWhiteSpace(action.HelpText) ? head : head + "\n" + action.HelpText;
        }
    }

    // One menu entry, following one action.
    /// <summary>A menu entry that follows one action's label, enabled state and checked state.</summary>
    public class ActionMenuItem : MenuItem
    {
        protected override Type StyleKeyOverride => typeof(MenuItem);

        // A style key spent is a type a selector can no longer name (§30), so every control
        // that pins one publishes the class that names it instead. Uniform rather than
        // added-when-needed: the class costs nothing, and the day this control gains a style
        // file or a CSS element name the selector already has something to match. Enforced by
        // StyleKeyTests, which is why this cannot be forgotten on the next one.
        public const string StyleClass = "luna-action-menu-item";

        private readonly LunaAction _action;

        // Guards the write-back below against the sync it is meant to correct - the general form
        // of the "do not echo my own write" flag the kit already owns (§21.1).
        private readonly Suppressor _syncing = new();

        public ActionMenuItem(LunaAction action)
        {
            Classes.Add(StyleClass);
            _action = action ?? throw new ArgumentNullException(nameof(action));

            if (action.Submenu is { } submenu)
            {
                // A submenu's parent is not a command: clicking it opens the menu. Subscribing to
                // Click as well would fire a handler for an action the caller only meant as a
                // heading.
                ItemsSource = Menus.Items(submenu);
            }
            else
            {
                // NOT Command. A checkable MenuItem flips its own IsChecked when clicked, and a
                // command that flips the action's state too would leave the two arguing about
                // which of them just happened. Click is the one signal meaning "the user did
                // this"; everything else here is the action telling the control what is true.
                Click += OnClicked;
            }

            ActionSync.Follow(this, action, Apply);
        }

        public LunaAction Action => _action;

        private void OnClicked(object? sender, RoutedEventArgs e)
        {
            _action.Invoke();
            Apply();
        }

        // THE ORDER OF THESE TWO EVENTS IS NOT WORTH DEPENDING ON, so this does not depend on it.
        //
        // Avalonia's MenuItem toggles IsChecked itself as part of handling a click, and whether
        // that happens before or after the Click event reaches the handler above is an
        // implementation detail of a control this toolkit does not own. Rather than encode a guess
        // about it, the control treats the ACTION as the only truth and puts IsChecked back
        // whenever anything else moves it. Both orders converge on the same answer:
        //
        //   toggle first  -> restored to the old state -> Click -> action flips -> Apply
        //   Click first   -> action flips -> Apply -> toggle -> restored from the action
        //
        // It also gets a case neither order covers: a grouped action clicked while already
        // checked, where the honest answer is "nothing changes" and an unguarded toggle would
        // uncheck the only checked member of a radio set.
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != IsCheckedProperty || _syncing.IsSuppressing || !_action.IsCheckable) return;
            if (IsChecked == _action.IsChecked) return;

            using (_syncing.Suppress()) SetCurrentValue(IsCheckedProperty, _action.IsChecked);
        }

        private void Apply()
        {
            using (_syncing.Suppress())
            {
                Header = _action.Text;
                IsEnabled = _action.IsEnabled;
                InputGesture = _action.Shortcut;
                ToggleType = _action.IsCheckable ? MenuItemToggleType.CheckBox : MenuItemToggleType.None;
                SetCurrentValue(IsCheckedProperty, _action.IsChecked);
                AutomationProperties.SetHelpText(this, _action.HelpText ?? string.Empty);
            }
        }
    }

    // One toolbar button, following one action. Not checkable - ActionToggle is that.
    /// <summary>A toolbar button that follows one action; ActionToggle is the checkable version.</summary>
    public class ActionButton : Button
    {
        protected override Type StyleKeyOverride => typeof(Button);

        // A style key spent is a type a selector can no longer name (§30), so every control
        // that pins one publishes the class that names it instead. Uniform rather than
        // added-when-needed: the class costs nothing, and the day this control gains a style
        // file or a CSS element name the selector already has something to match. Enforced by
        // StyleKeyTests, which is why this cannot be forgotten on the next one.
        public const string StyleClass = "luna-action-button";

        public ActionButton(LunaAction action)
        {
            Classes.Add(StyleClass);
            Action = action ?? throw new ArgumentNullException(nameof(action));

            // Command, unlike the menu item above, because a plain Button has no state of its own
            // to argue with. This also buys the enabled state from ICommand.CanExecute rather than
            // from a property sync, which is one fewer thing that can be out of step.
            Command = action;

            ActionSync.Follow(this, action, Apply);
        }

        public LunaAction Action { get; }

        private void Apply()
        {
            Content = Action.Text;

            // BOTH, AND THE REASON IS A MEASUREMENT. Binding Command already disables the button
            // correctly - Avalonia's ICommandSource pushes CanExecute into IsEnabledCore, and the
            // button greys out and stops responding. What it does NOT do is move IsEnabled, which
            // stays true: the disabling shows up in IsEffectivelyEnabled instead. So a caller
            // reading `button.IsEnabled` on a button for a disabled action is told "true", and
            // ActionToggle - which has no Command and syncs the property directly - would answer
            // "false" for the same action. Two controls in one file disagreeing about what
            // disabled means is the kind of thing nobody finds until it is a bug report, so this
            // mirrors the state onto the property as well. See docs/LunaP.md §26.11.
            IsEnabled = Action.IsEnabled;

            ToolTip.SetTip(this, ActionSync.Tip(Action));

            // The visible word stays the accessible name - a person saying "click save" needs
            // "Save" to be what the button is called (§24.2) - so everything that distinguishes
            // one button from another goes in help text, which is announced after it.
            AutomationProperties.SetHelpText(this, Action.HelpText ?? string.Empty);
        }
    }

    // One toolbar button for a checkable action: pressed means on.
    /// <summary>A toolbar button for a checkable action, where pressed means checked.</summary>
    public class ActionToggle : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        // A style key spent is a type a selector can no longer name (§30), so every control
        // that pins one publishes the class that names it instead. Uniform rather than
        // added-when-needed: the class costs nothing, and the day this control gains a style
        // file or a CSS element name the selector already has something to match. Enforced by
        // StyleKeyTests, which is why this cannot be forgotten on the next one.
        public const string StyleClass = "luna-action-toggle";

        private readonly Suppressor _syncing = new();

        public ActionToggle(LunaAction action)
        {
            Classes.Add(StyleClass);
            Action = action ?? throw new ArgumentNullException(nameof(action));

            // Same reasoning as ActionMenuItem: a ToggleButton flips itself on click, so binding
            // Command as well would invoke the action for a state change the action had not agreed
            // to. Click means the user; everything else is the action reporting.
            Click += (_, _) =>
            {
                Action.Invoke();
                Apply();
            };

            ActionSync.Follow(this, action, Apply);
        }

        public LunaAction Action { get; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != IsCheckedProperty || _syncing.IsSuppressing) return;
            if (IsChecked == Action.IsChecked) return;

            using (_syncing.Suppress()) SetCurrentValue(IsCheckedProperty, Action.IsChecked);
        }

        private void Apply()
        {
            using (_syncing.Suppress())
            {
                Content = Action.Text;
                IsEnabled = Action.IsEnabled;
                SetCurrentValue(IsCheckedProperty, Action.IsChecked);
                ToolTip.SetTip(this, ActionSync.Tip(Action));
                AutomationProperties.SetHelpText(this, Action.HelpText ?? string.Empty);
            }
        }
    }
}
