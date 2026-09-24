using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using EmuSen.LunaP.Theme;

using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Windowing
{
    // The base every LunaP window shares. Deliberately thin, and both of its features are opt-in - see docs/LunaP.md §8.
    /// <summary>The thin base every LunaP window shares, whose extra features are all opt-in.</summary>
    public class ToolWindow : Window
    {
        public static readonly StyledProperty<bool> ClosesOnEscapeProperty =
            AvaloniaProperty.Register<ToolWindow, bool>(nameof(ClosesOnEscape));

        // Setting this is what enables geometry persistence; a window without one is never remembered.
        /// <summary>The name this window saves its position under. Null means nothing is saved or restored, which is the default.</summary>
        public string? WindowKey { get; set; }

        // Bound rather than styled: FluentTheme's own Window ControlTheme otherwise wins and paints it near-black.
        /// <summary>A window that restores its own position and closes on Escape.</summary>
        public ToolWindow()
        {
            this[!BackgroundProperty] = new DynamicResourceExtension("LunaSurface");

            // A theme carrying rule blocks cannot reach a realized control on its own - see docs/LunaP.md §12.3.
            LunaTheme.StylesChanged += Restyle;
            Closed += (_, _) => LunaTheme.StylesChanged -= Restyle;
        }

        private void Restyle() => LunaTheme.Restyle(this);

        // Off by default: Escape inside a console pane means "stop what I am typing", not "close the window".
        //
        // THE SUMMARY BELOW SAID "True by default" UNTIL §84.4, TWO LINES UNDER THE COMMENT SAYING
        // THE OPPOSITE. Register is passed no defaultValue, so the property is false, and the `//`
        // was right the whole time. Nothing in the suite observed it: the only test touching this
        // property assigns it explicitly, so the default was never read back by anything. A
        // consumer reading the summary would have written `ClosesOnEscape = false` on a main window
        // believing it changed something, or - worse - relied on Escape working on a tool window
        // and found it did not.
        /// <summary>Whether Escape closes the window. False by default, which suits a main window; a tool window or a dialog usually wants it on.</summary>
        public bool ClosesOnEscape
        {
            get => GetValue(ClosesOnEscapeProperty);
            set => SetValue(ClosesOnEscapeProperty, value);
        }

        // COMPUTED FROM WindowState RATHER THAN STORED BESIDE IT - see docs/LunaP.md §75.2.
        //
        // A stored bool would be a second copy of a fact the window already holds, and the window is
        // not the only thing that writes it: the platform's own full-screen affordance, a window
        // manager shortcut, or a caller setting WindowState directly all move it without going
        // through here. A copy would then be wrong with no way to notice, which is the same failure
        // §26.3 records for a toolbar button that took a snapshot of its action's label.
        /// <summary>Whether the window is currently filling the screen with no chrome.</summary>
        public bool IsFullScreen
        {
            get => WindowState == WindowState.FullScreen;
            set
            {
                if (value == IsFullScreen) return;
                WindowState = value ? WindowState.FullScreen : _beforeFullScreen;
            }
        }

        // Where to go back to. Captured in OnPropertyChanged rather than in the setter above,
        // because the setter is not the only way in and a window that entered full screen by some
        // other route would otherwise be returned to a state it was in some time ago.
        private WindowState _beforeFullScreen = WindowState.Normal;

        // The gesture, as a method, because it is what a menu item and a key binding both want -
        // one LunaAction can carry it to both (§26.3). F11 is the consumer's to bind: this toolkit
        // does not own an application's keyboard.
        /// <summary>Enters full screen if the window is not in it, and leaves it if it is.</summary>
        public void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

        // WITHOUT THIS, EVERY CHECKABLE "Full Screen" MENU ITEM IS §26.3's DEFECT - see docs/LunaP.md §75.2.
        //
        // A LunaAction stores its own IsChecked and flips it when invoked, which is right for a
        // setting the action owns. Full screen is not one: the platform's own affordance and a
        // window-manager shortcut both move it without invoking anything, and the tick would then
        // say the opposite of the window. So the window announces the change and the surface
        // follows it, which is the same rule a toolbar button follows for its action's label.
        /// <summary>Raised whenever the window enters or leaves full screen, including when something other than this toolkit moved it. The argument is the new state.</summary>
        public event Action<bool>? FullScreenChanged;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != WindowStateProperty) return;

            WindowState previous = change.GetOldValue<WindowState>();
            WindowState now = change.GetNewValue<WindowState>();

            bool wasFull = previous == WindowState.FullScreen;
            bool isFull = now == WindowState.FullScreen;

            // Maximized to Minimized is not this event's business, and neither is anything else
            // that leaves the answer where it was.
            if (wasFull == isFull) return;

            // Never come back to Minimized: leaving full screen would then hide the window
            // altogether, which reads as the application having closed.
            if (isFull) _beforeFullScreen = previous == WindowState.Minimized ? WindowState.Normal : previous;

            FullScreenChanged?.Invoke(isFull);
        }

        protected override void OnOpened(System.EventArgs e)
        {
            base.OnOpened(e);
            RestorePlacement();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // Captured before the close completes, while the bounds are still real.
            RememberPlacement();
            base.OnClosing(e);
        }

        /// <summary>What the window was last closed with through <see cref="Close(object?)"/>, which a window shown on a sheet answers with in place of ShowDialog's result.</summary>
        public object? DialogResult { get; private set; }

        // Hides Window.Close(object) so the answer outlives a window that was never shown - see docs/LunaP.md §90.3.
        /// <summary>Closes the window with a result, which ShowDialog returns and <see cref="DialogResult"/> keeps.</summary>
        /// <param name="dialogResult">The answer the dialog gives, returned by ShowDialog and kept in DialogResult.</param>
        public new void Close(object? dialogResult)
        {
            DialogResult = dialogResult;
            base.Close(dialogResult);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (ClosesOnEscape && e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            base.OnKeyDown(e);
        }

        // THE ERGONOMIC HALF OF §86.12'S ATTACHED PROPERTY, and it is a convenience rather than a
        // second mechanism: this reads and writes `LunaSettings.StoreProperty` on this window and
        // nothing else. It is here because `window.Settings = store` is what a consumer reaches for
        // and `LunaSettings.SetStore(window, store)` is what they would have had to find.
        //
        // Setting it covers everything INSIDE the window too - panes, panels, tables - because the
        // property inherits down the logical tree. Set it before Show(): placement is restored in
        // OnOpened and saved in OnClosing, so a store assigned after the window is up is read for
        // the save and missed for the restore.
        /// <summary>The settings store this window and everything in it should use. Null - the default - means the process-wide LunaSettings.Store. Set it before Show().</summary>
        public ISettingsStore? Settings
        {
            get => LunaSettings.GetStore(this);
            set => LunaSettings.SetStore(this, value);
        }

        private void RestorePlacement()
        {
            if (WindowKey is null || WindowPlacementStore.Load(WindowKey, LunaSettings.For(this)) is not { } saved) return;

            if (saved.Width > 0 && saved.Height > 0)
            {
                Width = saved.Width;
                Height = saved.Height;
            }

            var bounds = new PixelRect(saved.X, saved.Y, (int)saved.Width, (int)saved.Height);
            if (WindowPlacementStore.IsOnAScreen(Screens, bounds))
            {
                Position = new PixelPoint(saved.X, saved.Y);
            }

            if (saved.Maximized) WindowState = WindowState.Maximized;
        }

        private void RememberPlacement()
        {
            if (WindowKey is null) return;

            // Both states whose bounds are the screen's rather than the window's need what was
            // stored last time; the rule itself, and why full screen belongs beside maximized here,
            // is in WindowPlacementStore.PlacementToSave - see docs/LunaP.md §75.4.
            WindowPlacement? previous = WindowState is WindowState.Maximized or WindowState.FullScreen
                ? WindowPlacementStore.Load(WindowKey, LunaSettings.For(this))
                : null;

            WindowPlacementStore.Save(WindowKey,
                WindowPlacementStore.PlacementToSave(WindowState, Position, Width, Height, previous),
                LunaSettings.For(this));
        }
    }
}
