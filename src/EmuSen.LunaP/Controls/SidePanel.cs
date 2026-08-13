using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Controls
{
    // Which edge of the shell a panel lives on.
    /// <summary>Which edge of a shell window a side panel is docked to.</summary>
    public enum PanelSide
    {
        Left,
        Right,
        Bottom,
    }

    // A titled panel docked to an edge, which can be closed and comes back where it was - see
    // docs/LunaP.md §26.7.
    //
    // This is Qt's QDockWidget with the floating taken out, and the omission is the design rather
    // than an unfinished edge of it. What a dock widget is actually used for - an explorer down
    // the left, an output pane along the bottom, a properties panel on the right - needs a title,
    // a way to shut it, a remembered width and a menu entry to get it back. What costs the other
    // nine tenths of QDockWidget is tearing it off into its own window, dragging it to a different
    // edge, and tabbing two of them together, which needs a drag protocol, floating windows,
    // hit-testing against every dock site and a layout format to serialise the result. §26.12
    // records that as a gap with its name on it; this control does not pretend to be that.
    //
    // THE TOGGLE IS AN ACTION, which is the join between this and §26.3. Qt has
    // QDockWidget::toggleViewAction() for exactly this reason: the View menu entry that shows and
    // hides a panel must be the same object as the panel's own close button, or the menu's tick
    // and the panel's state drift apart the first time somebody uses the button.
    /// <summary>A titled panel docked to one edge, which can be closed and comes back where it was.</summary>
    public class SidePanel : ContentControl
    {
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<SidePanel, string>(nameof(Title), string.Empty);

        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<SidePanel, bool>(nameof(IsOpen), true);

        public static readonly StyledProperty<PanelSide> SideProperty =
            AvaloniaProperty.Register<SidePanel, PanelSide>(nameof(Side));

        public static readonly StyledProperty<double> PanelSizeProperty =
            AvaloniaProperty.Register<SidePanel, double>(nameof(PanelSize), 240);

        public static readonly StyledProperty<bool> CanCloseProperty =
            AvaloniaProperty.Register<SidePanel, bool>(nameof(CanClose), true);

        private Button? _close;
        private LunaAction? _toggle;

        // Raised when the panel is shown or hidden, from any cause - the close button, the toggle
        // action, or a caller setting the property. AppWindow listens so it can take the pane out
        // of the layout rather than leaving an empty strip where it was.
        public event Action<SidePanel>? OpenChanged;

        // What the panel's header says, and what its toggle action is called in the View menu.
        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        // Which edge AppWindow docks it to. Ignored by a panel used on its own.
        public PanelSide Side
        {
            get => GetValue(SideProperty);
            set => SetValue(SideProperty, value);
        }

        // The width, or the height for a bottom panel. Only the starting value: once the panel is
        // in an AppWindow the divider owns this number and remembers what the user dragged it to.
        public double PanelSize
        {
            get => GetValue(PanelSizeProperty);
            set => SetValue(PanelSizeProperty, value);
        }

        // False hides the header's close button, for a panel that is the point of the window.
        // The toggle action still works - this is about the chrome, not about permission.
        public bool CanClose
        {
            get => GetValue(CanCloseProperty);
            set => SetValue(CanCloseProperty, value);
        }

        // Setting this is what makes the panel remembered: whether it was open, under the same key
        // the divider saves its size under, so "shut, and 320 wide when it comes back" is one
        // record. Never remembered without one, like every other opt-in in the kit.
        public string? PanelKey { get; set; }

        // The View-menu entry for this panel, created on first use and then the same object
        // forever. Checked means visible; invoking it toggles.
        //
        // Built lazily rather than in the constructor because most panels never appear in a menu,
        // and an action that exists is an action the shortcut binder walks and the theme picker
        // has to skip.
        public LunaAction ToggleAction => _toggle ??= BuildToggle();

        // UIA's Pane, which is what a dockable region is, named by its title. A reader moving
        // through a shell by landmark gets "Explorer, pane" rather than an unnamed group - and
        // that is the whole reason a panel has a title in the first place.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Pane, name: () => Title);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_close is not null) _close.Click -= OnCloseClick;
            _close = e.NameScope.Find<Button>("PART_Close");
            if (_close is not null) _close.Click += OnCloseClick;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // A panel the user shut last time stays shut. Done on attach rather than in the
            // constructor so the store a host installs at startup is the one that gets read.
            if (PanelKey is { } key && PaneLayoutStore.Load(key) is { } saved)
            {
                IsOpen = !saved.Collapsed;
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TitleProperty && _toggle is not null)
            {
                // The menu entry is named after the panel, so renaming the panel renames it.
                _toggle.Text = Title;
            }

            if (change.Property != IsOpenProperty) return;

            // A panel on its own has to hide itself, because there is nothing else to do it. In an
            // AppWindow the shell goes further and takes the pane out of the layout entirely
            // (§26.7) - hiding alone would leave the window ending 240 pixels early - but a panel
            // dropped into a plain Window is a perfectly reasonable thing to build, and one whose
            // close button visibly did nothing would be a trap.
            IsVisible = IsOpen;

            if (_toggle is not null) _toggle.IsChecked = IsOpen;

            if (PanelKey is { } key) PaneLayoutStore.Update(key, layout => layout.Collapsed = !IsOpen);

            OpenChanged?.Invoke(this);
        }

        private LunaAction BuildToggle()
        {
            var action = new LunaAction(Title.Length > 0 ? Title : "Panel", self => IsOpen = self.IsChecked)
            {
                IsCheckable = true,
                IsChecked = IsOpen,
                HelpText = "Shows or hides this panel.",
            };

            return action;
        }

        private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => IsOpen = false;
    }
}
