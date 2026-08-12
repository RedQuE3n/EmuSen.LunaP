using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Windowing
{
    // A window with a menu bar, a toolbar, a status line, panels down the sides and content in the
    // middle - see docs/LunaP.md §26.8.
    //
    // Qt calls this QMainWindow, and the reason it is worth having as a type rather than as a
    // paragraph in a README is that the arrangement is not obvious to get right and is identical
    // every time: the menu and toolbar dock to the top in that order, the status line to the
    // bottom, the panels take the edges of what is left, and the central content takes the rest.
    // Written by hand that is a DockPanel, three nested splitters, and the ordering rule that
    // whichever child is added last fills - which is the kind of thing that works, gets copied,
    // and then has to be understood again by whoever is adding a fourth panel.
    //
    // IT EXTENDS ToolWindow AND CHANGES NOTHING IT INHERITED. Remembered geometry, the restyle
    // hook and the Escape behaviour all work exactly as they do on a plain ToolWindow, and every
    // part of the shell is empty until something is put in it: an AppWindow with no menus, no
    // toolbar, no panels and no status line lays out identically to a ToolWindow with the same
    // content. §9.1 refused a base class that altered behaviour by being inherited, and §21.4
    // found six windows in one application that had already refused ToolWindow itself - a shell
    // that imposed chrome on anybody who derived from it would earn the same treatment.
    public class AppWindow : ToolWindow
    {
        private readonly MenuBar _menuBar = new();
        private readonly ToolBar _toolBar = new();
        private readonly Controls.StatusBar _statusBar = new();
        private readonly ContentControl _centralHost = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

        // The three dividers are built once and stay in the tree, with a null pane where a panel
        // is absent or closed. SplitPane collapses a null pane AND its divider, so this costs an
        // empty grid track and buys the alternative's whole problem: re-parenting the central
        // content every time somebody toggles a panel, which is how a control ends up with two
        // logical parents and an exception nobody can reproduce.
        //
        // Nesting order is left outermost, then right, then bottom. That is what makes a left
        // panel full height and a bottom panel sit under the middle of the window rather than
        // across the whole of it - the arrangement every editor with these three panels uses.
        private readonly SplitPane _bottomSplit;
        private readonly SplitPane _rightSplit;
        private readonly SplitPane _leftSplit;

        private readonly List<SidePanel> _panels = new();
        private IReadOnlyList<KeyBinding> _bound = Array.Empty<KeyBinding>();
        private bool _statusUsed;

        public AppWindow()
        {
            _bottomSplit = new SplitPane { Orientation = Orientation.Vertical, Fixed = SplitSide.Second, First = _centralHost };
            _rightSplit = new SplitPane { Orientation = Orientation.Horizontal, Fixed = SplitSide.Second, First = _bottomSplit };
            _leftSplit = new SplitPane { Orientation = Orientation.Horizontal, Fixed = SplitSide.First, Second = _rightSplit };

            _menuBar.IsVisible = false;
            _toolBar.IsVisible = false;
            _statusBar.IsVisible = false;

            var root = new DockPanel();
            DockPanel.SetDock(_menuBar, Dock.Top);
            DockPanel.SetDock(_toolBar, Dock.Top);
            DockPanel.SetDock(_statusBar, Dock.Bottom);
            root.Children.Add(_menuBar);
            root.Children.Add(_toolBar);
            root.Children.Add(_statusBar);

            // Last, because DockPanel gives the last child whatever is left - which is the rule
            // that makes this a shell rather than four strips.
            root.Children.Add(_leftSplit);

            Content = root;
        }

        public MenuBar MenuBar => _menuBar;

        public ToolBar ToolBar => _toolBar;

        public Controls.StatusBar StatusBar => _statusBar;

        public IReadOnlyList<SidePanel> Panels => _panels;

        // What goes in the middle. A property rather than Content, because Content is the whole
        // window and this is the part of it that is not chrome; a caller that set Content would
        // throw the menu bar away and wonder where it went.
        public object? Central
        {
            get => _centralHost.Content;
            set => _centralHost.Content = value;
        }

        // The message in the bottom-left. Setting it is what makes the status bar appear.
        public string Status
        {
            get => _statusBar.Status;
            set
            {
                _statusBar.Status = value;
                ShowStatusBar();
            }
        }

        // The right-hand end of the status line: a progress bar, a pair of buttons, a mode
        // indicator. Whatever a caller puts there, exactly as StatusBar takes today.
        public object? StatusContent
        {
            get => _statusBar.Content;
            set
            {
                _statusBar.Content = value;
                ShowStatusBar();
            }
        }

        public void SetMenus(params LunaMenu[] menus) => SetMenus((IEnumerable<LunaMenu>)menus);

        public void SetMenus(IEnumerable<LunaMenu> menus)
        {
            _menuBar.SetMenus(menus);

            // An empty menu bar is a strip of nothing that still takes a row of height, which on a
            // small tool window is a visible waste and looks like a rendering fault.
            _menuBar.IsVisible = _menuBar.Menus.Count > 0;
            Rebind();
        }

        public void SetToolBar(params LunaAction[] actions) => SetToolBar((IEnumerable<LunaAction>)actions);

        public void SetToolBar(IEnumerable<LunaAction> actions)
        {
            _toolBar.SetActions(actions);
            _toolBar.IsVisible = _toolBar.Actions.Count > 0;
            Rebind();
        }

        // Docks a panel to the side it names. One panel per side: a second one for the same side
        // replaces the first, because two panels on one edge is the tabbed-dock feature §26.12
        // records as deliberately absent, and silently stacking them would be a worse answer than
        // saying so.
        public SidePanel AddPanel(SidePanel panel)
        {
            if (panel is null) throw new ArgumentNullException(nameof(panel));

            for (int i = _panels.Count - 1; i >= 0; i--)
            {
                if (_panels[i].Side != panel.Side) continue;

                _panels[i].OpenChanged -= OnPanelOpenChanged;
                _panels.RemoveAt(i);
            }

            _panels.Add(panel);
            panel.OpenChanged += OnPanelOpenChanged;

            SplitPane split = SplitFor(panel.Side);

            // The divider is a keyboard-operable control (§26.11), so it is named after what it
            // resizes rather than left as the generic default - three panels in a shell would
            // otherwise be three tab stops all called "Resize panes".
            if (!string.IsNullOrWhiteSpace(panel.Title)) split.DividerLabel = "Resize " + panel.Title;

            // The divider takes the panel's key, so the width the user drags and the open/closed
            // state the panel saves end up as one record - see PaneLayoutStore.Update for why that
            // needs two writers of one entry rather than two entries.
            split.PaneKey = panel.PanelKey;
            if (split.PaneKey is null || PaneLayoutStore.Load(split.PaneKey) is not { Size: > 0 })
            {
                split.FixedSize = panel.PanelSize;
            }

            Place(panel);
            return panel;
        }

        // Every panel's toggle, in the order they were added - for a View menu, which is where Qt
        // puts them too. A caller can of course build its own menu from panel.ToggleAction.
        public IReadOnlyList<LunaAction> PanelToggles()
        {
            var toggles = new List<LunaAction>(_panels.Count);
            foreach (SidePanel panel in _panels) toggles.Add(panel.ToggleAction);
            return toggles;
        }

        private SplitPane SplitFor(PanelSide side) => side switch
        {
            PanelSide.Left => _leftSplit,
            PanelSide.Right => _rightSplit,
            _ => _bottomSplit,
        };

        // A closed panel is taken out of the layout rather than merely hidden: an invisible pane
        // still owns its grid track and its divider, so hiding it would leave the middle of the
        // window ending 240 pixels early with nothing in the gap.
        private void Place(SidePanel panel)
        {
            SplitPane split = SplitFor(panel.Side);
            object? content = panel.IsOpen ? panel : null;

            if (panel.Side == PanelSide.Left) split.First = content;
            else split.Second = content;
        }

        private void OnPanelOpenChanged(SidePanel panel) => Place(panel);

        // Once shown it stays, even if the message is cleared. A status bar that appeared and
        // vanished with the text would move every control in the window by its own height each
        // time an operation finished, which is a worse fault than a blank strip.
        private void ShowStatusBar()
        {
            if (_statusUsed) return;

            _statusUsed = true;
            _statusBar.IsVisible = true;
        }

        // Rebound wholesale whenever the menus or the toolbar change, because an action removed
        // from the menu must lose its key with it - otherwise a command a window no longer offers
        // is still one keystroke away.
        private void Rebind()
        {
            Menus.Unbind(this, _bound);

            var actions = new List<LunaAction>();
            foreach (LunaMenu menu in _menuBar.Menus) actions.AddRange(menu.Commands());
            actions.AddRange(_toolBar.Actions);

            _bound = Menus.BindShortcuts(this, actions);
        }
    }
}
