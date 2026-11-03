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
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // One item's page: a hero shaped for video, the cover, the name and facts, the actions, the sections and their content, in both forms - see docs/LunaP.md §209.
    /// <summary>An item's page declared as data: a hero with a poster slot, the cover, the title and facts, the actions under them, sections stepped by the shoulders, and rows, tiles or text as each section's content; arranged for the big screen or the desktop.</summary>
    public partial class PageLayout : Border
    {
        /// <summary>Defines the Form property.</summary>
        public static readonly StyledProperty<AdaptiveForm> FormProperty = AvaloniaProperty.Register<PageLayout, AdaptiveForm>(nameof(Form));

        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty = AvaloniaProperty.Register<PageLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>The big screen's design height; the page is laid out in units of it and scaled by MenuPanel.Scale.</summary>
        public const double DesignHeight = 800;

        /// <summary>The hero's height on the big screen and on the desktop.</summary>
        public const double HeroHeight = 360, DesktopHeroHeight = 200;

        /// <summary>The largest cover on the big screen, kept clear of the content beneath it, and the desktop's.</summary>
        public const double CoverWidth = 240, CoverMaxHeight = 268, DesktopCoverWidth = 160, DesktopCoverHeight = 200;

        /// <summary>The safe area's side margin and where the title, the actions and the sections start on the big screen.</summary>
        public const double Side = 64, TitleLeft = 336;

        /// <summary>The tops of the title, the facts, the actions, the sections and the content on the big screen.</summary>
        public const double TitleTop = 226, FactsTop = 286, ActionsTop = 328, StripTop = 396, ContentTop = 464;

        /// <summary>The help bar's top and height on the big screen; the safe area lies under it.</summary>
        public const double HelpBarTop = 702, HelpBarHeight = 58;

        /// <summary>A row's height and the rows shown at once on the big screen; the rest scroll whole.</summary>
        public const double RowHeight = 54;

        private readonly PosterSlot _poster = new() { Name = "PagePoster", IsHitTestVisible = false };
        private readonly Border _cover = new() { Name = "PageCover", ClipToBounds = true };
        private readonly TextBlock _title = new() { Name = "PageTitle", TextWrapping = TextWrapping.NoWrap };
        private readonly TextBlock _facts = new() { Name = "PageFacts", TextWrapping = TextWrapping.NoWrap };
        private readonly Viewbox _titleFit = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly Viewbox _factsFit = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly StackPanel _actionRow = new() { Name = "PageActions", Orientation = Orientation.Horizontal, Spacing = 12 };
        private readonly WholeRow _strip = new() { Spacing = 8, Name = "PageSections", Selects = AutomationControlType.Tab };
        private readonly SelectionStack _tabs = new() { Name = "PageTabs", Orientation = Orientation.Horizontal, Spacing = 24, Selects = AutomationControlType.Tab };
        private readonly SelectionStack _bigRows = new() { Name = "PageRows" };
        private MenuRow? _focusRow;
        private readonly Border _contentHost = new() { Name = "PageContent" };
        private readonly HintBar _hintBar = new();
        private readonly Border _hintBand = new() { Name = "PageHelpBar" };
        private readonly Button _back = new() { Name = "PageBack", Content = "‹ Back" };
        private readonly Panel _viewer = new() { Name = "PageViewer", IsVisible = false };
        private readonly Image _viewerPicture = new() { Name = "PageViewerPicture", Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, IsHitTestVisible = false };
        private readonly TextBlock _viewerCaption = new() { Name = "PageViewerCaption", TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        private readonly HintBar _viewerHints = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        private PageHero? _hero;
        private IReadOnlyList<PageAction> _actions = Array.Empty<PageAction>();
        private IReadOnlyList<AdaptiveSection> _sections = Array.Empty<AdaptiveSection>();
        private string? _selected;
        private string? _trail, _pageTitle;
        private PageContent? _content;
        private int _actionAt;
        private bool _inActions = true;
        private string? _focusKey;
        private int _firstShown;

        /// <summary>An empty page in the desktop form.</summary>
        public PageLayout()
        {
            Focusable = true;
            ClipToBounds = true;
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            _viewerHints[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            StyleBar(_viewerHints);
            AdaptiveLayout.StyleStrip(_strip);
            _back.Click += (_, _) => Back?.Invoke();
            AutomationProperties.SetName(_back, "Back");
            AutomationProperties.SetName(_actionRow, "Actions");
            AutomationProperties.SetName(_strip, "Sections");
            GotFocus += (_, e) => { if (ReferenceEquals(e.Source, this) && Big) _focusRow?.Focus(); };
            Arrange();
        }

        /// <summary>The form the page is arranged in: Desktop, or BigScreen; the Card form is drawn as the big screen.</summary>
        public AdaptiveForm Form { get => GetValue(FormProperty); set => SetValue(FormProperty, value); }

        /// <summary>The pad whose printing the help bar and the actions' glyphs draw.</summary>
        public PadFamily PadFamily { get => GetValue(PadFamilyProperty); set => SetValue(PadFamilyProperty, value); }

        private bool Big => Form != AdaptiveForm.Desktop;

        /// <summary>The hero: the poster, the cover, the title and the facts; null shows an empty top.</summary>
        public PageHero? Hero { get => _hero; set { _hero = value; FillHero(); } }

        /// <summary>The poster slot under the hero, for a host that gives it a clip or a test that measures it.</summary>
        public PosterSlot Poster => _poster;

        /// <summary>The actions under the title, in order; set with SetActions.</summary>
        public IReadOnlyList<PageAction> Actions => _actions;

        /// <summary>The sections, in order; set with SetSections.</summary>
        public IReadOnlyList<AdaptiveSection> Sections => _sections;

        /// <summary>The chosen section's key, or null when there are none.</summary>
        public string? SelectedSection => _selected;

        /// <summary>The content of the chosen section or deeper page.</summary>
        public PageContent? Content { get => _content; set { _content = value; FillContent(); } }

        /// <summary>Gives the help bar's entries for what is focused, generated from what the host declares; South's entry names what A does.</summary>
        public Func<PageFocus, IReadOnlyList<PageHint>>? HintsFor { get; set; }

        /// <summary>Called for B and for the desktop's Back button; true when the host used it, such as closing a deeper page.</summary>
        public Func<bool>? Back { get; set; }

        /// <summary>Raised when an action is pressed, with its key.</summary>
        public event Action<string>? ActionInvoked;

        /// <summary>Raised when Left or Right steps an action's value, or a desktop's split button picks one, with the key and how far.</summary>
        public event Action<string, int>? ActionStepped;

        /// <summary>Raised when a person chooses a section, never by SetSections or SelectSection.</summary>
        public event Action<string>? SectionChosen;

        /// <summary>Raised when a row is chosen with A, a click or Enter.</summary>
        public event Action<string>? RowChosen;

        /// <summary>Raised when a row's value is stepped, by Left and Right or the desktop's choice, with the key and how far.</summary>
        public event Action<string, int>? RowStepped;

        /// <summary>Raised when a text row is typed in on the desktop and left, with the key and the text.</summary>
        public event Action<string, string>? RowEdited;

        /// <summary>Raised when a tile is chosen with A, a double click or Enter.</summary>
        public event Action<string>? TileChosen;

        /// <summary>Raised when a desktop tile's button is clicked, with the tile's key and the button's.</summary>
        public event Action<string, string>? TileButton;

        /// <summary>Raised when one of the content's desktop buttons is clicked, with its key.</summary>
        public event Action<string>? ContentButton;

        /// <summary>Raised when a pad button the hints give a key to is pressed, with that key.</summary>
        public event Action<string>? HintInvoked;

        /// <summary>Raised when the focus moves to another action, row or tile.</summary>
        public event Action<PageFocus>? FocusChanged;

        /// <summary>Raised when Left or Right steps the viewer, with how far.</summary>
        public event Action<int>? ViewerStepped;

        /// <summary>What is focused now.</summary>
        public PageFocus Focused => new(_inActions, _inActions ? (_actionAt < _actions.Count ? _actions[_actionAt].Key : null) : _focusKey);

        /// <summary>The help bar's entries as drawn now.</summary>
        public IReadOnlyList<HintEntry> Hints => _hintBar.Entries ?? Array.Empty<HintEntry>();

        /// <summary>Whether a deeper page is shown, its parent and its own name in the strip.</summary>
        public bool IsDeeper => _pageTitle is not null;

        /// <summary>Whether the viewer is open over the page.</summary>
        public bool IsViewerOpen => _viewer.IsVisible;

        /// <summary>Gives the page its actions, keeping the focused one where it still is.</summary>
        /// <param name="actions">The actions in order, the main one first by convention.</param>
        public void SetActions(IEnumerable<PageAction> actions)
        {
            string? focused = Focused.InActions ? Focused.Key : null;
            _actions = actions?.ToList() ?? throw new ArgumentNullException(nameof(actions));
            int at = focused is null ? -1 : _actions.ToList().FindIndex(a => a.Key == focused);
            _actionAt = at >= 0 ? at : Math.Max(0, _actions.ToList().FindIndex(a => a.IsMain));
            FillActions();
            RefreshHints();
        }

        /// <summary>Gives the page its sections and the chosen one, leaving a deeper page; raises nothing.</summary>
        /// <param name="sections">The sections in order; their keys must be distinct.</param>
        /// <param name="selected">The chosen section's key; null or an unknown key chooses the first.</param>
        public void SetSections(IEnumerable<AdaptiveSection> sections, string? selected = null)
        {
            _sections = sections?.ToList() ?? throw new ArgumentNullException(nameof(sections));
            if (_sections.Select(s => s.Key).Distinct().Count() != _sections.Count) throw new ArgumentException("Two sections share a key.", nameof(sections));
            _selected = _sections.Any(s => s.Key == selected) ? selected : _sections.FirstOrDefault()?.Key;
            _trail = _pageTitle = null;
            FillStrip();
            RefreshHints();
        }

        /// <summary>Chooses a section without raising SectionChosen.</summary>
        /// <param name="key">The section's key.</param>
        /// <returns>Whether there is such a section.</returns>
        public bool SelectSection(string key)
        {
            if (!_sections.Any(s => s.Key == key)) return false;
            _selected = key;
            _trail = _pageTitle = null;
            FillStrip();
            return true;
        }

        /// <summary>Shows a deeper page: the strip names its parent and then the page, the shoulders are declined, and B is the host's to close it.</summary>
        /// <param name="parent">The parent section's or page's name.</param>
        /// <param name="title">The page's name.</param>
        /// <param name="content">What it shows.</param>
        public void ShowDeeper(string parent, string title, PageContent content)
        {
            _trail = parent ?? throw new ArgumentNullException(nameof(parent));
            _pageTitle = title ?? throw new ArgumentNullException(nameof(title));
            FillStrip();
            _focusKey = null;
            _firstShown = 0;
            Content = content;
            FocusContent();
        }

        /// <summary>Shows the chosen section's strip again after a deeper page.</summary>
        public void LeaveDeeper()
        {
            _trail = _pageTitle = null;
            FillStrip();
            RefreshHints();
        }

        /// <summary>Puts the focus on an action, the main one where no key is given.</summary>
        /// <param name="key">The action's key, or null.</param>
        /// <returns>Whether there was an action to focus.</returns>
        public bool FocusActions(string? key = null)
        {
            if (_actions.Count == 0) return false;
            int at = key is null ? _actions.ToList().FindIndex(a => a.IsMain) : _actions.ToList().FindIndex(a => a.Key == key);
            _actionAt = Math.Max(0, at);
            _inActions = true;
            Refocused();
            return true;
        }

        /// <summary>Puts the focus in the content, on the row or tile given or the first one.</summary>
        /// <param name="key">A row's or tile's key, or null for the first.</param>
        /// <returns>Whether the content has anything to focus.</returns>
        public bool FocusContent(string? key = null)
        {
            IReadOnlyList<string> keys = Keys();
            if (keys.Count == 0) return false;
            _focusKey = key is not null && keys.Contains(key) ? key : _focusKey is not null && keys.Contains(_focusKey) ? _focusKey : keys[0];
            _inActions = false;
            Refocused();
            return true;
        }

        /// <summary>Opens the viewer over the page: a picture on the void, scaled down to fit, with its caption in a bar at the foot.</summary>
        /// <param name="picture">The picture, or null for its caption alone.</param>
        /// <param name="caption">One line about it.</param>
        public void ShowViewer(IImage? picture, string caption)
        {
            _viewerPicture.Source = picture;
            _viewerCaption.Text = caption;
            _viewer.IsVisible = true;
            RefreshHints();
        }

        /// <summary>Closes the viewer.</summary>
        /// <returns>Whether it was open.</returns>
        public bool CloseViewer()
        {
            if (!_viewer.IsVisible) return false;
            _viewer.IsVisible = false;
            _viewerPicture.Source = null;
            RefreshHints();
            return true;
        }

        /// <summary>Moves the focus with the directional pad or the arrow keys: across the actions, between the actions and the content, and through the rows or tiles; Left and Right step a value where there is one.</summary>
        /// <param name="dx">-1 left, 1 right, 0 neither.</param>
        /// <param name="dy">-1 up, 1 down, 0 neither.</param>
        /// <returns>Whether anything moved or stepped.</returns>
        public bool Move(int dx, int dy)
        {
            if (_viewer.IsVisible)
            {
                if (dx == 0) return false;
                ViewerStepped?.Invoke(dx);
                return true;
            }
            if (_inActions) return MoveInActions(dx, dy);
            return MoveInContent(dx, dy);
        }

        // Left and Right on a stepped action step its value; past either end of it they move to the neighbour, so every action is reached (§209.4).
        private bool MoveInActions(int dx, int dy)
        {
            if (dy > 0) return FocusContent();
            if (dy < 0 || dx == 0 || _actions.Count == 0) return false;
            PageAction here = _actions[_actionAt];
            if (here.Value is not null && here.Choices is { Count: > 1 } choices)
            {
                int at = choices.ToList().IndexOf(here.Value);
                if (at >= 0 && at + dx >= 0 && at + dx < choices.Count)
                {
                    ActionStepped?.Invoke(here.Key, dx);
                    return true;
                }
            }
            int next = _actionAt + dx;
            if (next < 0 || next >= _actions.Count) return false;
            _actionAt = next;
            Refocused();
            return true;
        }

        private bool MoveInContent(int dx, int dy)
        {
            IReadOnlyList<string> keys = Keys();
            int at = _focusKey is null ? -1 : keys.ToList().IndexOf(_focusKey);
            switch (_content)
            {
                case PageContent.Rows rows:
                    if (dx != 0)
                    {
                        if (rows.Items.FirstOrDefault(r => r.Key == _focusKey) is { Kind: PageRowKind.Steps, IsEnabled: true } stepped)
                        {
                            RowStepped?.Invoke(stepped.Key, dx);
                            return true;
                        }
                        return false;
                    }
                    if (dy < 0 && at <= 0) return FocusActions(_actions.ElementAtOrDefault(_actionAt)?.Key);
                    if (dy > 0 && at >= keys.Count - 1) return false;
                    _focusKey = keys[at + dy];
                    Refocused();
                    return true;
                case PageContent.Tiles:
                    if (dy < 0) return FocusActions(_actions.ElementAtOrDefault(_actionAt)?.Key);
                    if (dy > 0 || dx == 0) return false;
                    if (at + dx < 0 || at + dx >= keys.Count) return false;
                    _focusKey = keys[at + dx];
                    Refocused();
                    return true;
                default:
                    return dy < 0 && FocusActions(_actions.ElementAtOrDefault(_actionAt)?.Key);
            }
        }

        /// <summary>A pad button: A does what the focus holds, the shoulders step the sections, B goes to Back, and a button the hints give a key raises HintInvoked.</summary>
        /// <param name="button">The button pressed.</param>
        /// <returns>Whether the page used it; one declined is the host's.</returns>
        public bool Press(PadGlyphButton button)
        {
            if (button == PadGlyphButton.East && !_viewer.IsVisible && Back?.Invoke() == true) return true;
            if (button == PadGlyphButton.East && _viewer.IsVisible && !HasKey(button)) return CloseViewer();
            if (button == PadGlyphButton.South && !_viewer.IsVisible) return Accept();
            if (button is PadGlyphButton.LeftShoulder or PadGlyphButton.RightShoulder && !_viewer.IsVisible)
                return !IsDeeper && StepSection(button == PadGlyphButton.LeftShoulder ? -1 : 1);
            if (CurrentHints().FirstOrDefault(h => h.Button == button && h.Key is not null) is { } hint)
            {
                HintInvoked?.Invoke(hint.Key!);
                return true;
            }
            return false;
        }

        private bool HasKey(PadGlyphButton button) => CurrentHints().Any(h => h.Button == button && h.Key is not null);

        private bool Accept()
        {
            if (_inActions)
            {
                if (_actions.ElementAtOrDefault(_actionAt) is not { IsEnabled: true } action) return false;
                ActionInvoked?.Invoke(action.Key);
                return true;
            }
            switch (_content)
            {
                case PageContent.Rows rows when rows.Items.FirstOrDefault(r => r.Key == _focusKey) is { IsEnabled: true } row:
                    RowChosen?.Invoke(row.Key);
                    return true;
                case PageContent.Tiles tiles when tiles.Items.Any(t => t.Key == _focusKey):
                    TileChosen?.Invoke(_focusKey!);
                    return true;
            }
            return false;
        }

        /// <summary>Steps the chosen section by one, wrapping, raising SectionChosen.</summary>
        /// <param name="by">-1 for the previous section, 1 for the next.</param>
        /// <returns>Whether there were two or more sections to step between.</returns>
        public bool StepSection(int by)
        {
            if (_sections.Count < 2) return false;
            int at = Math.Max(0, _sections.ToList().FindIndex(s => s.Key == _selected));
            Choose(_sections[((at + by) % _sections.Count + _sections.Count) % _sections.Count].Key);
            return true;
        }

        private void Choose(string key)
        {
            _selected = key;
            _trail = _pageTitle = null;
            FillStrip();
            SectionChosen?.Invoke(key);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FormProperty) Arrange();
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || Big) return;
            if (e.Key == Key.Escape || e.Key == Key.Back && e.Source is not TextBox) e.Handled = Back?.Invoke() == true;
        }

        /// <inheritdoc/>
        protected override AutomationPeer OnCreateAutomationPeer() => new LunaAutomationPeer(this, AutomationControlType.Pane, () => _hero?.Title);

        private IReadOnlyList<PageHint> CurrentHints() => HintsFor?.Invoke(Focused) ?? Array.Empty<PageHint>();

        private void RefreshHints()
        {
            var entries = CurrentHints().Select(h => new HintEntry(h.Text) { Button = h.Button }).ToList();
            if (_viewer.IsVisible) _viewerHints.Entries = entries;
            else _hintBar.Entries = entries;
            AutomationProperties.SetName(_hintBar, string.Join(", ", entries.Select(e => e.Label)));
        }

        private void Refocused()
        {
            FillActionsFocus();
            FillContentFocus();
            RefreshHints();
            FocusChanged?.Invoke(Focused);
        }

        private IReadOnlyList<string> Keys() => _content switch
        {
            PageContent.Rows r => r.Items.Select(i => i.Key).ToList(),
            PageContent.Tiles t => t.Items.Select(i => i.Key).ToList(),
            _ => Array.Empty<string>(),
        };
    }
}
