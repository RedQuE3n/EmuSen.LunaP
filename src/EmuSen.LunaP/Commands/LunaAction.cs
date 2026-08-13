using System;
using System.Windows.Input;
using Avalonia.Input;

namespace EmuSen.LunaP.Commands
{
    // One command, wherever it appears - see docs/LunaP.md §26.3.
    //
    // "Save" is a menu item, a toolbar button, a context-menu entry and a Ctrl+S key binding, and
    // without a thing like this it is FOUR declarations that have to be kept in step by hand: four
    // labels, four enabled states, and a shortcut written once in a KeyBinding and once again as
    // the grey text on the right of the menu, where nothing checks that the two say the same key.
    // The failure is quiet in the worst way - the menu item greys out and the toolbar button does
    // not, so the button is clickable and does nothing.
    //
    // So this is Qt's QAction, and the borrowing is deliberate: the idea is thirty years old and
    // every toolkit that has grown a menu bar has arrived at it. A caller builds one action and
    // hands it to as many surfaces as it likes; each surface follows it.
    //
    // IT IS AN ICommand, which is the second half of the point. Avalonia's Button, MenuItem and
    // KeyBinding all take an ICommand already, so an action drops into a control this toolkit has
    // never heard of - and CanExecute is what makes a disabled action's shortcut do nothing rather
    // than firing into a handler that has to check for itself. A LunaP-shaped command object that
    // only LunaP controls could consume would be the §1 mistake in a new place.
    //
    // WHAT IT DELIBERATELY IS NOT: an icon. Qt's QAction carries a QIcon and this carries no
    // equivalent, because LunaP has no icon system at all - no set, no resolver, no theming for
    // one - and an `object Icon` property would be an invitation to put a raw Bitmap in a toolkit
    // that could not restyle it with the theme. Text and a shortcut are what the kit can render
    // honestly today. §26.12 records this as a gap rather than a decision that closed.
    /// <summary>One command object standing behind a menu item, a toolbar button, a context-menu entry and a key binding.</summary>
    public sealed class LunaAction : ICommand
    {
        private string _text;
        private string? _helpText;
        private KeyGesture? _shortcut;
        private bool _isEnabled = true;
        private bool _isChecked;
        private bool _isCheckable;

        private readonly Action<LunaAction>? _triggered;

        public LunaAction(string text, Action<LunaAction>? triggered = null)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _triggered = triggered;
        }

        // The zero-argument form, which is what most callers want. The one above exists because a
        // CHECKABLE action cannot use this one: reading `IsChecked` from inside the handler needs
        // the action, and the action is what is being constructed on that line.
        public LunaAction(string text, Action triggered)
            : this(text, triggered is null ? null : new Action<LunaAction>(_ => triggered()))
        {
        }

        // Every surface built from this action re-reads it and updates itself. Raised for Text,
        // HelpText, Shortcut, IsEnabled, IsCheckable and IsChecked alike - a menu item cares about
        // all six, and one event with six subscribers beats six events with one each.
        public event Action<LunaAction>? Changed;

        // ICommand's own, which is what a stock Avalonia Button listens to. Raised alongside
        // Changed whenever IsEnabled moves, and never otherwise: re-querying CanExecute because a
        // label changed would be work for nothing on every keystroke of a live-updating caption.
        public event EventHandler? CanExecuteChanged;

        // Raised after the handler runs, for a caller watching an action it did not construct -
        // the shell wiring a status line to "something happened", say. Not a substitute for the
        // handler: this fires for every invocation including a checkable's own state flip.
        public event Action<LunaAction>? Invoked;

        // What the menu item and the toolbar button say. Settable, because half the actions worth
        // having are "Pause"/"Resume" on one command rather than two.
        public string Text
        {
            get => _text;
            set => Set(ref _text, value ?? throw new ArgumentNullException(nameof(value)));
        }

        // The sentence after the label. Becomes the tooltip on a toolbar button and the accessible
        // help text everywhere, which is the one place a toolbar can explain itself: a button
        // reading "Strip" has room for no more, and "Removes the existing form fields" has to live
        // somewhere a reader can reach.
        public string? HelpText
        {
            get => _helpText;
            set => Set(ref _helpText, value);
        }

        // Written ONCE. A menu item shows it, the window binds it, and the toolbar tooltip
        // mentions it - all three read this property, so the three cannot disagree about which key
        // does what. KeyGesture rather than a string because it is Avalonia's own vocabulary and
        // `KeyGesture.Parse("Ctrl+S")` is what a caller writes anyway; a string property here
        // would mean this type owned a parser and its error handling, for nothing.
        public KeyGesture? Shortcut
        {
            get => _shortcut;
            set => Set(ref _shortcut, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;

                _isEnabled = value;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                Changed?.Invoke(this);
            }
        }

        // A checkable action is a setting rather than a command - "Show the sidebar", "Wrap
        // lines". Invoking one flips it BEFORE the handler runs, so the handler reads the new
        // state, which is Qt's behaviour and the only one that makes a one-line handler possible.
        public bool IsCheckable
        {
            get => _isCheckable;
            set => Set(ref _isCheckable, value);
        }

        // Setting this directly does NOT invoke the handler. That is the difference between the
        // application telling the action what is true and the user asking for a change, and
        // collapsing the two is how a settings dialog ends up applying everything twice on open.
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;

                _isChecked = value;
                Group?.Notify(this);
                Changed?.Invoke(this);
            }
        }

        // A rule rather than a command: a separator is an ACTION with nothing to do, not a second
        // item type. One list type, one loop, and a caller writes the divider where it goes rather
        // than composing two collections. Menus and toolbars render it as a line; everything else
        // ignores it.
        public bool IsSeparator { get; private init; }

        // What this action opens instead of doing. This is exactly Qt's arrangement, where a
        // submenu is reached through the action that owns it, and it means a nested menu needs no
        // new item type either.
        public LunaMenu? Submenu { get; set; }

        // Set by ActionGroup.Add. Null for the overwhelming majority of actions.
        public ActionGroup? Group { get; internal set; }

        public static LunaAction Separator() => new("-") { IsSeparator = true };

        // The user asking for it. A disabled action does nothing at all - not the handler, not the
        // state flip - so a stale toolbar button or a key binding that outlived its window cannot
        // reach a handler that was counting on being unreachable.
        public void Invoke()
        {
            if (!_isEnabled || IsSeparator) return;

            if (_isCheckable)
            {
                // A grouped action cannot be turned off by clicking it again, which is what makes
                // a group a set of radio buttons rather than a row of independent switches. Qt
                // draws the same distinction with QActionGroup.exclusive.
                if (Group is not null) IsChecked = true;
                else IsChecked = !_isChecked;
            }

            _triggered?.Invoke(this);
            Invoked?.Invoke(this);
        }

        bool ICommand.CanExecute(object? parameter) => _isEnabled && !IsSeparator;

        void ICommand.Execute(object? parameter) => Invoke();

        // The parameter is ignored on purpose, and this is where to say so: an action is bound to
        // one thing to do, decided when it was constructed. A caller needing per-invocation data
        // wants a different action, not the same one carrying a payload that its menu item, its
        // toolbar button and its key binding would each have to supply identically.
        public override string ToString() => IsSeparator ? "(separator)" : Text;

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value)) return;

            field = value;
            Changed?.Invoke(this);
        }
    }

    // Mutually exclusive checkable actions - see docs/LunaP.md §26.3.
    //
    // Qt's QActionGroup, and it exists for the same reason the toolkit already has Suppressor:
    // "check this one and uncheck the others" is written as a loop over siblings every time
    // somebody needs it, and the loop is where the re-entrancy bug lives. Unchecking the previous
    // member raises its Changed, whose subscriber updates a menu item, which must not come back
    // round and re-enter the group. This does the sweep with a flag for exactly that reason.
    //
    // A theme picker is the case that argued it in: three menu items, one checked, and the check
    // has to move when the theme changes from anywhere - a hotkey, a settings window, a theme file
    // going missing at startup. Setting IsChecked on the new member is the whole of it, and every
    // other member follows.
    /// <summary>A set of mutually exclusive checkable actions, of which at most one is checked at a time.</summary>
    public sealed class ActionGroup
    {
        private readonly System.Collections.Generic.List<LunaAction> _members = new();
        private bool _sweeping;

        public System.Collections.Generic.IReadOnlyList<LunaAction> Members => _members;

        // The member currently checked, or null before anything has been chosen. Null is a real
        // state and not a gap: a group of themes with none of them applied yet is exactly what a
        // freshly created group is.
        public LunaAction? Checked
        {
            get
            {
                foreach (LunaAction member in _members)
                {
                    if (member.IsChecked) return member;
                }

                return null;
            }
        }

        // Joining a group makes an action checkable, because there is no such thing as an
        // unchecked-and-uncheckable member of a radio set. Doing it here rather than making the
        // caller remember means one fewer way to build a group that silently does nothing.
        public LunaAction Add(LunaAction action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            if (action.Group is not null && action.Group != this)
                throw new InvalidOperationException($"'{action.Text}' is already in another ActionGroup.");

            action.IsCheckable = true;
            action.Group = this;
            if (!_members.Contains(action)) _members.Add(action);

            // An action added already checked wins, which is how a group is given its initial
            // selection: build the three, check the saved one, add all three.
            if (action.IsChecked) Notify(action);
            return action;
        }

        public LunaAction Add(string text, Action<LunaAction>? triggered = null) => Add(new LunaAction(text, triggered));

        // Called by a member whose IsChecked moved. Only a member becoming CHECKED sweeps: a
        // member reporting that it went false is either this sweep's own doing or a caller
        // emptying the group deliberately, and re-checking something to keep the invariant would
        // be the group arguing with the application about what is true.
        internal void Notify(LunaAction changed)
        {
            if (_sweeping || !changed.IsChecked) return;

            _sweeping = true;
            try
            {
                foreach (LunaAction member in _members)
                {
                    if (!ReferenceEquals(member, changed)) member.IsChecked = false;
                }
            }
            finally
            {
                _sweeping = false;
            }
        }
    }
}
