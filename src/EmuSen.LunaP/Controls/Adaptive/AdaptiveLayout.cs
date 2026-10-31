using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Commands;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The two forms a window built on AdaptiveLayout is arranged in.</summary>
    public enum AdaptiveForm
    {
        /// <summary>The desktop's: navigation beside the content, the detail beneath it, a toolbar above, for a pointer and a keyboard.</summary>
        Desktop,
        /// <summary>The big screen's: sections in a row changed by the shoulders, the content in the menus' type, the detail its own page, the actions as buttons and a help bar, for a pad.</summary>
        BigScreen,
    }

    /// <summary>What a part of a window is, which decides where AdaptiveLayout puts it and where the pad's focus goes.</summary>
    public enum AdaptiveRole
    {
        /// <summary>The sections or filters the content is chosen by.</summary>
        Navigation,
        /// <summary>The list or the main view.</summary>
        Content,
        /// <summary>What is known of the content's chosen item.</summary>
        Detail,
        /// <summary>The window's commands.</summary>
        Actions,
        /// <summary>The text the content is searched by.</summary>
        Search,
        /// <summary>A line of state beneath everything else.</summary>
        Status,
    }

    /// <summary>One section of a window's navigation: a key the window switches on, the words shown, and an optional group heading.</summary>
    /// <param name="Key">What the window switches on, never shown.</param>
    /// <param name="Label">The words the section is shown with.</param>
    /// <param name="Group">The heading it sits under in the desktop's sidebar; null for none.</param>
    public sealed record AdaptiveSection(string Key, string Label, string? Group = null);

    // A window described by roles and arranged for the form it is shown in - see docs/LunaP.md §202.
    /// <summary>A window's parts declared by role and arranged for its form: side by side on the desktop, stacked in the menus' type on the big screen, with the pad's focus and the help bar following the roles.</summary>
    public class AdaptiveLayout : Border
    {
        /// <summary>Defines the Form property.</summary>
        public static readonly StyledProperty<AdaptiveForm> FormProperty =
            AvaloniaProperty.Register<AdaptiveLayout, AdaptiveForm>(nameof(Form));

        /// <summary>The style class on the big screen's button for the main action, which stands first in its row.</summary>
        public const string MainClass = "adaptive-main";

        private static readonly AdaptiveRole[] FocusOrder = { AdaptiveRole.Navigation, AdaptiveRole.Content, AdaptiveRole.Detail, AdaptiveRole.Actions };

        private readonly SourceList _sidebar = new();
        private readonly StackPanel _sectionRow = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        private readonly ToolBar _toolBar = new();
        private readonly StackPanel _actionRow = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
        private readonly TextBox _search = new() { MinWidth = 180 };
        private readonly HintBar _hintBar = new() { FontSize = 18 };
        private readonly ScrollViewer _sidebarScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly ScrollViewer _sectionScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly ScrollViewer _actionScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly Border _navigationHost = new() { Name = "AdaptiveNavigation" };
        private readonly Border _contentHost = new() { Name = "AdaptiveContent" };
        private readonly Border _detailHost = new() { Name = "AdaptiveDetail" };
        private readonly Border _actionsHost = new() { Name = "AdaptiveActions" };
        private readonly Border _searchHost = new() { Name = "AdaptiveSearch" };
        private readonly Border _statusHost = new() { Name = "AdaptiveStatus" };

        private readonly List<SplitPane> _splits = new();
        private IReadOnlyList<AdaptiveSection> _sections = Array.Empty<AdaptiveSection>();
        private IReadOnlyList<LunaAction> _actions = Array.Empty<LunaAction>();
        private HashSet<LunaAction> _undrawn = new();
        private IReadOnlyList<KeyBinding> _bindings = Array.Empty<KeyBinding>();
        private Control? _content;
        private Control? _detail;
        private Control? _status;
        private string? _selectedSection;
        private bool _hasSearch;
        private bool _detailOpen;
        private string _title = string.Empty;

        /// <summary>An empty layout in the desktop form; parts are given by role.</summary>
        public AdaptiveLayout()
        {
            Focusable = false;
            _sidebarScroll.Content = _sidebar;
            _sectionScroll.Content = _sectionRow;
            _actionScroll.Content = _actionRow;
            _sidebar.Chose += key => SelectSection(key, raise: true);
            _search.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) SearchChanged?.Invoke(SearchText);
            };
            AutomationProperties.SetName(_search, "Search");
            AutomationProperties.SetName(_sidebar, "Sections");
            AutomationProperties.SetName(_toolBar, "Actions");
            Arrange();
        }

        /// <summary>The form the parts are arranged in; changing it rearranges them, keeping the selection and the focused role.</summary>
        public AdaptiveForm Form
        {
            get => GetValue(FormProperty);
            set => SetValue(FormProperty, value);
        }

        /// <summary>The window's name, which a reader hears for the layout as a whole.</summary>
        /// <exception cref="System.ArgumentNullException">The value is null.</exception>
        public string Title
        {
            get => _title;
            set => _title = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>The navigation's sections, in order; set with SetSections.</summary>
        public IReadOnlyList<AdaptiveSection> Sections => _sections;

        /// <summary>The key of the chosen section, or null when there are none.</summary>
        public string? SelectedSection => _selectedSection;

        /// <summary>The content role: the list or the main view.</summary>
        public Control? Content
        {
            get => _content;
            set { _content = value; _contentHost.Child = value; }
        }

        /// <summary>The detail role; on the big screen it is a page of its own, opened with OpenDetail.</summary>
        public Control? Detail
        {
            get => _detail;
            set { _detail = value; _detailHost.Child = value; Arrange(); }
        }

        /// <summary>The status role: a line beneath everything else.</summary>
        public Control? Status
        {
            get => _status;
            set { _status = value; _statusHost.Child = value; Arrange(); }
        }

        /// <summary>The actions, declared once: a toolbar on the desktop, buttons and help-bar entries on the big screen.</summary>
        public IReadOnlyList<LunaAction> Actions => _actions;

        /// <summary>Whether the layout shows a search box.</summary>
        public bool HasSearch
        {
            get => _hasSearch;
            set { _hasSearch = value; Arrange(); }
        }

        /// <summary>The words shown in the empty search box.</summary>
        public string? SearchHint
        {
            get => _search.PlaceholderText;
            set => _search.PlaceholderText = value;
        }

        /// <summary>The search box's text, never null.</summary>
        public string SearchText
        {
            get => _search.Text ?? string.Empty;
            set => _search.Text = value;
        }

        /// <summary>Whether the detail's page is open on the big screen; always false on the desktop, where the detail is always shown.</summary>
        public bool IsDetailOpen => _detailOpen && Form == AdaptiveForm.BigScreen;

        /// <summary>Raised when a person chooses a section, never by SetSections or SelectSection.</summary>
        public event Action<string>? SectionChosen;

        /// <summary>Raised when the search text changes.</summary>
        public event Action<string>? SearchChanged;

        /// <summary>The help bar's entries, generated from the roles present and the actions' pad buttons, never written by hand.</summary>
        public IReadOnlyList<HintEntry> Hints
        {
            get
            {
                var hints = new List<HintEntry>();
                if (_content is not null) hints.Add(new HintEntry(IsDetailOpen ? "Select" : _detail is null ? "Select" : "Open") { Button = PadGlyphButton.South });
                hints.Add(new HintEntry("Back") { Button = PadGlyphButton.East });
                if (_sections.Count > 1) hints.Add(new HintEntry("Section") { Button = PadGlyphButton.Shoulders });
                foreach (LunaAction action in Padded().OrderByDescending(a => a.IsMain))
                    hints.Add(new HintEntry(action.Text) { Button = action.PadButton });
                return hints;
            }
        }

        /// <summary>Gives the navigation its sections and the chosen one, without raising SectionChosen.</summary>
        /// <param name="sections">The sections in order; their keys must be distinct.</param>
        /// <param name="selected">The chosen section's key; null or an unknown key chooses the first.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="sections"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Two sections share a key.</exception>
        public void SetSections(IEnumerable<AdaptiveSection> sections, string? selected = null)
        {
            if (sections is null) throw new ArgumentNullException(nameof(sections));
            List<AdaptiveSection> list = sections.ToList();
            if (list.Select(s => s.Key).Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new ArgumentException("Two sections share a key.", nameof(sections));
            _sections = list;
            _selectedSection = list.Any(s => s.Key == selected) ? selected : list.FirstOrDefault()?.Key;
            Arrange();
        }

        /// <summary>Chooses a section as the application, without raising SectionChosen.</summary>
        /// <param name="key">The section's key.</param>
        /// <returns>Whether a section has that key.</returns>
        public bool SelectSection(string key) => SelectSection(key, raise: false);

        /// <summary>Steps the chosen section as the shoulders do, wrapping at the ends, and raises SectionChosen.</summary>
        /// <param name="by">How many sections to move; negative moves back.</param>
        /// <returns>Whether there was a section to move to.</returns>
        public bool StepSection(int by)
        {
            if (_sections.Count < 2) return false;
            int at = Math.Max(0, _sections.ToList().FindIndex(s => s.Key == _selectedSection));
            int next = ((at + by) % _sections.Count + _sections.Count) % _sections.Count;
            return SelectSection(_sections[next].Key, raise: true);
        }

        /// <summary>Declares the window's actions; each is drawn as the form draws it and its shortcut is bound on the layout.</summary>
        /// <param name="actions">The actions in order, separators included.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> is null.</exception>
        public void SetActions(IEnumerable<LunaAction> actions) => SetActions(actions, Array.Empty<LunaAction>());

        /// <summary>Declares the window's actions, some of which the window draws itself, such as an apply bar's button; those keep their pad button, help-bar entry and shortcut.</summary>
        /// <param name="actions">The actions in order, separators included.</param>
        /// <param name="drawnElsewhere">Those among them the layout does not draw.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> or <paramref name="drawnElsewhere"/> is null.</exception>
        public void SetActions(IEnumerable<LunaAction> actions, IEnumerable<LunaAction> drawnElsewhere)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            if (drawnElsewhere is null) throw new ArgumentNullException(nameof(drawnElsewhere));
            Menus.Unbind(this, _bindings);
            _undrawn = drawnElsewhere.ToHashSet();
            _actions = actions.ToList();
            _bindings = Menus.BindShortcuts(this, _actions);
            Arrange();
        }

        /// <summary>Opens the detail's page on the big screen and puts the focus in it; does nothing on the desktop.</summary>
        /// <returns>Whether a page was opened.</returns>
        public bool OpenDetail()
        {
            if (Form != AdaptiveForm.BigScreen || _detail is null || _detailOpen) return false;
            _detailOpen = true;
            Arrange();
            FocusRole(AdaptiveRole.Detail);
            return true;
        }

        /// <summary>Closes the detail's page and returns the focus to the content.</summary>
        /// <returns>Whether a page was open.</returns>
        public bool CloseDetail()
        {
            if (!_detailOpen) return false;
            _detailOpen = false;
            Arrange();
            FocusRole(AdaptiveRole.Content);
            return true;
        }

        /// <summary>The panel a role's part is placed in, for a host or a test that needs its bounds; null when the role is not shown in this form.</summary>
        /// <param name="role">The role asked about.</param>
        /// <returns>The panel holding the role, or null.</returns>
        public Control? HostOf(AdaptiveRole role)
        {
            Control host = role switch
            {
                AdaptiveRole.Navigation => _navigationHost,
                AdaptiveRole.Content => _contentHost,
                AdaptiveRole.Detail => _detailHost,
                AdaptiveRole.Actions => _actionsHost,
                AdaptiveRole.Search => _searchHost,
                _ => _statusHost,
            };
            return host.IsEffectivelyVisible && host.GetVisualAncestors().Contains(this) ? host : null;
        }

        /// <summary>The role holding the keyboard focus, or null when the focus is outside the layout.</summary>
        public AdaptiveRole? FocusedRole
        {
            get
            {
                if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused) return null;
                foreach (AdaptiveRole role in Enum.GetValues<AdaptiveRole>())
                {
                    if (HostOf(role) is { } host && (ReferenceEquals(host, focused) || focused.GetVisualAncestors().Contains(host))) return role;
                }
                return null;
            }
        }

        /// <summary>Moves the keyboard focus to the first control a person can operate in a role.</summary>
        /// <param name="role">The role to focus.</param>
        /// <returns>Whether the role is shown and holds such a control.</returns>
        public bool FocusRole(AdaptiveRole role)
        {
            if (HostOf(role) is not { } host) return false;
            InputElement? first = host.GetVisualDescendants().OfType<InputElement>()
                .FirstOrDefault(e => e.Focusable && e.IsEffectivelyEnabled && e.IsEffectivelyVisible && !ReferenceEquals(e, host));
            return first is not null && first.Focus(NavigationMethod.Directional);
        }

        /// <summary>Moves the focus to the next or previous role in the order navigation, content, detail, actions, skipping those not shown; F6 and Shift+F6 do the same.</summary>
        /// <param name="by">1 for the next role, -1 for the previous.</param>
        /// <returns>Whether the focus moved.</returns>
        public bool MoveFocus(int by)
        {
            AdaptiveRole[] shown = FocusOrder.Where(r => HostOf(r) is not null).ToArray();
            if (shown.Length == 0) return false;
            int at = FocusedRole is { } now ? Array.IndexOf(shown, now) : -1;
            for (int step = 1; step <= shown.Length; step++)
            {
                int next = at < 0 ? (by >= 0 ? step - 1 : shown.Length - step) : ((at + by * step) % shown.Length + shown.Length) % shown.Length;
                if (FocusRole(shown[next])) return true;
            }
            return false;
        }

        /// <summary>A pad button, in ES-DE's grammar: the shoulders step the sections, B goes back a level, an action's own button invokes it, and South on the content opens the detail's page on the big screen.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the layout used it; a button it declines is the host's, such as B at the top level closing the window.</returns>
        public virtual bool Press(PadGlyphButton button)
        {
            switch (button)
            {
                case PadGlyphButton.LeftShoulder: return StepSection(-1);
                case PadGlyphButton.RightShoulder: return StepSection(1);
                case PadGlyphButton.South when Form == AdaptiveForm.BigScreen && !IsDetailOpen && FocusedRole == AdaptiveRole.Content:
                    return OpenDetail();
                case PadGlyphButton.East:
                    if (CloseDetail()) return true;
                    if (FocusedRole is AdaptiveRole.Detail or AdaptiveRole.Actions or AdaptiveRole.Search) return FocusRole(AdaptiveRole.Content);
                    if (FocusedRole == AdaptiveRole.Content && Form == AdaptiveForm.Desktop) return FocusRole(AdaptiveRole.Navigation);
                    return false;
            }
            LunaAction? action = Padded().FirstOrDefault(a => a.PadButton == button);
            if (action is null || !action.IsEnabled) return false;
            action.Invoke();
            return true;
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FormProperty)
            {
                AdaptiveRole? focused = FocusedRole;
                if (Form == AdaptiveForm.Desktop) _detailOpen = false;
                Arrange();
                if (focused is { } role) Avalonia.Threading.Dispatcher.UIThread.Post(() => Refocus(role), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            if (e.Key == Key.F6) e.Handled = MoveFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            else if (e.Key == Key.Escape && IsDetailOpen) e.Handled = CloseDetail();
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Pane, () => string.IsNullOrEmpty(_title) ? null : _title);

        // The menus' type re-templates stock controls, so the focus is put back once the new templates are in.
        private void Refocus(AdaptiveRole role)
        {
            if (FocusedRole == role) return;
            if (!FocusRole(role)) FocusRole(AdaptiveRole.Content);
        }

        private IEnumerable<LunaAction> Padded() => _actions.Where(a => a.PadButton is not null && !a.IsSeparator);

        private bool SelectSection(string key, bool raise)
        {
            if (!_sections.Any(s => s.Key == key)) return false;
            bool moved = _selectedSection != key;
            _selectedSection = key;
            FillNavigation();
            if (raise && moved) SectionChosen?.Invoke(key);
            return true;
        }

        private void FillNavigation()
        {
            _sidebar.Fill(_sections.GroupBy(s => s.Group ?? string.Empty)
                .Select(g => new SourceListGroup(g.Key, g.Select(s => new SourceListItem(s.Key, s.Label)).ToList())), _selectedSection);
            _sectionRow.Children.Clear();
            foreach (AdaptiveSection section in _sections)
            {
                var button = new ToggleButton { Content = section.Label, IsChecked = section.Key == _selectedSection };
                AutomationProperties.SetName(button, section.Label);
                string key = section.Key;
                button.Click += (_, _) => SelectSection(key, raise: true);
                _sectionRow.Children.Add(button);
            }
        }

        private void FillActions()
        {
            _toolBar.SetActions(_actions.Where(a => !_undrawn.Contains(a)));
            _actionRow.Children.Clear();
            foreach (LunaAction action in _actions.Where(a => !a.IsSeparator && a.Submenu is null && !_undrawn.Contains(a)).OrderByDescending(a => a.IsMain))
            {
                var button = new ActionButton(action);
                if (action.IsMain) button.Classes.Add(MainClass);
                _actionRow.Children.Add(button);
            }
        }

        private void Arrange()
        {
            _navigationHost.Child = null;
            _actionsHost.Child = null;
            foreach (SplitPane split in _splits)
            {
                split.First = null;
                split.Second = null;
            }
            _splits.Clear();
            FillNavigation();
            FillActions();
            bool big = Form == AdaptiveForm.BigScreen;
            MenuLook.SetIsOn(this, big);

            _searchHost.Child ??= _search;
            _searchHost.IsVisible = _hasSearch;
            _navigationHost.IsVisible = _sections.Count > 0;
            _actionsHost.IsVisible = _actions.Any(a => !a.IsSeparator);
            _statusHost.IsVisible = _status is not null;

            Child = big ? ArrangeBigScreen() : ArrangeDesktop();
        }

        private Control ArrangeDesktop()
        {
            _navigationHost.Child = _sidebarScroll;
            _actionsHost.Child = _toolBar;
            _detailHost.IsVisible = _detail is not null;
            _contentHost.IsVisible = true;

            var top = new DockPanel { LastChildFill = true, Margin = new Thickness(4) };
            DockPanel.SetDock(_searchHost, Dock.Right);
            _searchHost.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(Detach(_searchHost));
            top.Children.Add(Detach(_actionsHost));
            _actionsHost.Margin = default;

            Control main = _detail is null
                ? Detach(_contentHost)
                : Split(new SplitPane { Orientation = Orientation.Vertical, First = Detach(_contentHost), Second = Detach(_detailHost), Fixed = SplitSide.Second, FixedSize = 180, MinFirst = 120, MinSecond = 80, DividerLabel = "Resize the detail" });
            Control body = _sections.Count == 0
                ? main
                : Split(new SplitPane { First = Detach(_navigationHost), Second = main, Fixed = SplitSide.First, FixedSize = 180, MinFirst = 120, MinSecond = 320, DividerLabel = "Resize the sections" });

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            Grid.SetRow(top, 0);
            Grid.SetRow(body, 1);
            Grid.SetRow(Detach(_statusHost), 2);
            Detach(_hintBar);
            grid.Children.Add(top);
            grid.Children.Add(body);
            grid.Children.Add(_statusHost);
            return grid;
        }

        private Control ArrangeBigScreen()
        {
            _navigationHost.Child = _sectionScroll;
            _actionsHost.Child = _actionScroll;
            _hintBar.Entries = Hints;
            AutomationProperties.SetName(_hintBar, string.Join(", ", Hints.Select(h => h.Label)));
            bool page = _detailOpen && _detail is not null;
            _contentHost.IsVisible = !page;
            _detailHost.IsVisible = page;

            var top = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 12) };
            DockPanel.SetDock(_searchHost, Dock.Right);
            top.Children.Add(Detach(_searchHost));
            top.Children.Add(Detach(_navigationHost));

            var middle = new Grid { Margin = new Thickness(16, 0) };
            middle.Children.Add(Detach(_contentHost));
            middle.Children.Add(Detach(_detailHost));

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto") };
            Control[] rows = { top, middle, Detach(_actionsHost), Detach(_statusHost), _hintBar };
            _actionsHost.Margin = new Thickness(16, 8);
            _hintBar.Margin = new Thickness(16, 8);
            for (int i = 0; i < rows.Length; i++)
            {
                Detach(rows[i]);
                Grid.SetRow(rows[i], i);
                grid.Children.Add(rows[i]);
            }
            return grid;
        }

        private SplitPane Split(SplitPane split)
        {
            _splits.Add(split);
            return split;
        }

        private static Control Detach(Control control)
        {
            switch (control.Parent)
            {
                case Panel panel: panel.Children.Remove(control); break;
                case Decorator decorator: decorator.Child = null; break;
            }
            return control;
        }
    }
}
