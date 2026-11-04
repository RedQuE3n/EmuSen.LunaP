using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Controls
{
    /// <summary>The two forms a window built on AdaptiveLayout is arranged in.</summary>
    public enum AdaptiveForm
    {
        /// <summary>The desktop's: navigation beside the content, the detail beneath it, a toolbar above, for a pointer and a keyboard.</summary>
        Desktop,
        /// <summary>The big screen's: sections in a row changed by the shoulders, the content in the menus' type, the detail its own page, the actions as buttons and a help bar, for a pad.</summary>
        BigScreen,
        /// <summary>A card over what it pauses or covers: the content in a panel at the left over a scrim, the detail in the room beside it, the header as the panel's title row, no navigation, and the actions in the help bar alone.</summary>
        Card,
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
        /// <summary>The title row: a small picture, a title on one line, a subtitle and facts.</summary>
        Header,
    }

    /// <summary>What a window's header shows: a title on one line, never wrapped, with an optional picture before it, a subtitle and a line of facts.</summary>
    /// <param name="Title">The window's or the thing's name.</param>
    /// <param name="Subtitle">Smaller words under the title, such as where the page is; null for none.</param>
    /// <param name="Picture">A small picture before the title, such as a game's cover; null for none.</param>
    /// <param name="Facts">A line of facts or a status; null for none.</param>
    public sealed record AdaptiveHeader(string Title, string? Subtitle = null, IImage? Picture = null, string? Facts = null);

    /// <summary>Where the big screen's form puts the detail role; the desktop always puts it beneath the content.</summary>
    public enum AdaptiveDetailPlacement
    {
        /// <summary>A page of its own, opened by A on the content and closed by B.</summary>
        Page,
        /// <summary>Beside the content, always shown, as a supporting pane: the content on the left at ContentShare of the width, the detail on the right.</summary>
        Beside,
    }

    /// <summary>One section of a window's navigation: a key the window switches on, the words shown, and an optional group heading.</summary>
    /// <param name="Key">What the window switches on, never shown.</param>
    /// <param name="Label">The words the section is shown with.</param>
    /// <param name="Group">The heading it sits under in the desktop's sidebar; null for none.</param>
    /// <param name="Status">One line about the section, such as a count: right-aligned in the desktop's sidebar and heard with the name on the big screen; null for none (docs/LunaP.md §209.5).</param>
    public sealed record AdaptiveSection(string Key, string Label, string? Group = null, string? Status = null);

    // A window described by roles and arranged for the form it is shown in - see docs/LunaP.md §202.
    /// <summary>A window's parts declared by role and arranged for its form: side by side on the desktop, stacked in the menus' type on the big screen, with the pad's focus and the help bar following the roles.</summary>
    public class AdaptiveLayout : Border
    {
        /// <summary>Defines the Form property.</summary>
        public static readonly StyledProperty<AdaptiveForm> FormProperty =
            AvaloniaProperty.Register<AdaptiveLayout, AdaptiveForm>(nameof(Form));

        /// <summary>Defines the DetailPlacement property.</summary>
        public static readonly StyledProperty<AdaptiveDetailPlacement> DetailPlacementProperty =
            AvaloniaProperty.Register<AdaptiveLayout, AdaptiveDetailPlacement>(nameof(DetailPlacement));

        /// <summary>Defines the ContentShare property.</summary>
        public static readonly StyledProperty<double> ContentShareProperty =
            AvaloniaProperty.Register<AdaptiveLayout, double>(nameof(ContentShare), 0.4);

        /// <summary>Defines the AcceptWord property.</summary>
        public static readonly StyledProperty<string?> AcceptWordProperty = AvaloniaProperty.Register<AdaptiveLayout, string?>(nameof(AcceptWord));

        /// <summary>Defines the SideWord property.</summary>
        public static readonly StyledProperty<string?> SideWordProperty = AvaloniaProperty.Register<AdaptiveLayout, string?>(nameof(SideWord));

        /// <summary>Defines the BackWord property.</summary>
        public static readonly StyledProperty<string> BackWordProperty = AvaloniaProperty.Register<AdaptiveLayout, string>(nameof(BackWord), "Back");

        /// <summary>Defines the SwapsAcceptAndBack property.</summary>
        public static readonly StyledProperty<bool> SwapsAcceptAndBackProperty =
            AvaloniaProperty.Register<AdaptiveLayout, bool>(nameof(SwapsAcceptAndBack));

        /// <summary>Defines the PadFamily property.</summary>
        public static readonly StyledProperty<PadFamily> PadFamilyProperty =
            AvaloniaProperty.Register<AdaptiveLayout, PadFamily>(nameof(PadFamily), PadFamily.Xbox);

        /// <summary>The pad whose printing the help bar draws: lettered rings by default (Xbox's A, B, X and Y), or the family the host's session knows - see docs/LunaP.md §208.10.</summary>
        public PadFamily PadFamily
        {
            get => GetValue(PadFamilyProperty);
            set => SetValue(PadFamilyProperty, value);
        }

        /// <summary>The word the help bar gives the shoulders, "Section" by default; a window names what its sections are, such as "Console".</summary>
        public string SectionWord
        {
            get => _sectionWord;
            set { _sectionWord = value ?? throw new ArgumentNullException(nameof(value)); RefreshHints(); }
        }

        private string _sectionWord = "Section";

        /// <summary>The style class on the big screen's button for the main action, which stands first in its row.</summary>
        public const string MainClass = "adaptive-main";

        private static readonly AdaptiveRole[] FocusOrder = { AdaptiveRole.Navigation, AdaptiveRole.Content, AdaptiveRole.Detail, AdaptiveRole.Actions };

        private readonly SourceList _sidebar = new();
        private readonly WholeRow _sectionRow = new() { Spacing = 0, Selects = AutomationControlType.Tab };
        private readonly ToolBar _toolBar = new();
        private readonly WrapPanel _actionRow = new() { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 8 };
        private readonly TextBox _search = new() { MinWidth = 180 };
        private readonly HintBar _hintBar = new() { FontSize = LunaTokens.BigScreenType.Caption };
        private readonly ScrollViewer _sidebarScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        private readonly Border _navigationHost = new() { Name = "AdaptiveNavigation" };
        private readonly Border _contentHost = new() { Name = "AdaptiveContent" };
        private readonly Border _detailHost = new() { Name = "AdaptiveDetail" };
        private readonly Border _actionsHost = new() { Name = "AdaptiveActions" };
        private readonly Border _searchHost = new() { Name = "AdaptiveSearch" };
        private readonly Border _statusHost = new() { Name = "AdaptiveStatus" };
        private readonly AdaptiveHeaderRow _headerHost = new() { Name = "AdaptiveHeader" };

        // The Card form's parts: the panel the roles are placed into, the rows' and the pane's hosts in the body type, and the scrim under them (§207).
        private readonly MenuPanel _cardPanel = new() { Name = "AdaptiveCard", Anchor = MenuAnchor.Edge, WidthFraction = CardShare, MaxWidthToHeight = 4, TitleSize = 32, TitleMinScale = 0.55, TitleMaxLines = 1, PanelCornerRadius = 0, LetterCase = Media.LetterCase.None, SubtitleLetterCase = Media.LetterCase.None, HintGlyphStyle = PadGlyphStyle.Outline };
        private readonly Border _cardRows = new() { Name = "AdaptiveCardRows" };
        private readonly Border _cardPane = new() { Name = "AdaptiveCardPane" };
        private readonly Panel _cardScrim = new() { Name = "AdaptiveCardScrim" };
        private static readonly IBrush DefaultScrim = new ImmutableSolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0));
        // A question's cover dims the screen beneath to the OnArt scrim's depth, 72 per cent, so the card alone is read (§208.11).
        private static readonly IBrush CoverScrim = new ImmutableSolidColorBrush(Color.FromArgb(0xB8, 0, 0, 0));

        /// <summary>The class a tool puts on its pane's title, drawn by the frame in Barlow Condensed at the Title size on the big screen (§208.11).</summary>
        public const string PaneTitleClass = "pane-title";

        private readonly Style _paneTitle = new(x => x.OfType<TextBlock>().Class(PaneTitleClass))
        {
            Setters =
            {
                new Setter(TextBlock.FontFamilyProperty, Media.LunaFonts.Condensed),
                new Setter(TextBlock.FontSizeProperty, LunaTokens.BigScreenType.Title),
                new Setter(TextBlock.FontWeightProperty, FontWeight.Normal),
                new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("LunaTextStrong")),
            },
        };

        /// <summary>The panel's share of the width in the Card form, 40 per cent.</summary>
        public const double CardShare = 0.4;

        private readonly List<SplitPane> _splits = new();
        private SplitPane? _sectionSplit;
        private IReadOnlyList<AdaptiveSection> _sections = Array.Empty<AdaptiveSection>();
        private IReadOnlyList<LunaAction> _actions = Array.Empty<LunaAction>();
        private HashSet<LunaAction> _undrawn = new();
        private IReadOnlyList<KeyBinding> _bindings = Array.Empty<KeyBinding>();
        private Control? _content;
        private Control? _detail;
        private Control? _status;
        private AdaptiveHeader? _header;
        private string? _selectedSection;
        private bool _hasSearch;
        private bool _detailOpen;
        private string _title = string.Empty;

        // A page of the window's own over the content, the List card over the big screen, and the filters both forms draw (docs/LunaP.md §208).
        private readonly Panel _root = new() { Name = "AdaptiveLayers" };
        private readonly Border _pageHost = new() { Name = "AdaptivePage" };
        private Control? _page;
        private readonly Panel _listScrim = new() { Name = "AdaptiveListTools", IsVisible = false };
        private readonly Panel _coverScrim = new() { Name = "AdaptiveCover", IsVisible = false };
        private readonly Border _hintBand = new() { Name = "AdaptiveHelpBar", BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(48, 10, 48, 6) };
        private readonly MenuPanel _listCard = new() { Name = "AdaptiveListCard", Anchor = MenuAnchor.Left, WidthFraction = CardShare, MaxWidthToHeight = 4, TitleSize = 32, PanelCornerRadius = 8, Title = "List" };
        private readonly StackPanel _listRows = new() { Spacing = 8 };
        private readonly ComboBox _filterBox = new() { MinWidth = 140, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        private readonly ComboBox _cardFilter = new();
        private IReadOnlyList<AdaptiveSection> _filters = Array.Empty<AdaptiveSection>();
        private IReadOnlyList<LunaAction> _listActions = Array.Empty<LunaAction>();
        private string? _selectedFilter;
        private bool _settingFilter;

        /// <summary>An empty layout in the desktop form; parts are given by role.</summary>
        public AdaptiveLayout()
        {
            Focusable = false;
            _sidebarScroll.Content = _sidebar;
            _sidebar.Chose += key => SelectSection(key, raise: true);
            _search.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) SearchChanged?.Invoke(SearchText);
            };
            AutomationProperties.SetName(_search, "Search");
            AutomationProperties.SetName(_sidebar, "Sections");
            AutomationProperties.SetName(_toolBar, "Actions");
            _hintBar[!HintBar.PadFamilyProperty] = this[!PadFamilyProperty];
            _hintBar.FontPath = Media.LunaFonts.BodyFile;
            _hintBar.FontSize = 24;
            _hintBar.Bind(HintBar.IconColorProperty, _hintBar.GetResourceObservable("LunaTextStrongColor", c => c is Color colour ? colour : _hintBar.IconColor));
            _hintBar.Bind(HintBar.TextColorProperty, _hintBar.GetResourceObservable("LunaTextColor", c => c is Color colour ? colour : _hintBar.TextColor));
            _hintBand[!Border.BorderBrushProperty] = new DynamicResourceExtension("LunaBorder");
            _hintBand.Child = _hintBar;
            _cardPanel[!MenuPanel.HintFamilyProperty] = this[!PadFamilyProperty];
            _listCard[!MenuPanel.HintFamilyProperty] = this[!PadFamilyProperty];
            SetUpCard();
            SetUpListTools();
            SetUpSectionStrip();
            Arrange();
        }

        // The big screen's section strip: names in Caption upper case on nothing, the chosen one strong with a 3-unit accent underline (§208.10).
        private void SetUpSectionStrip()
        {
            StyleStrip(_sectionRow);
            // The frame's strip: the underline as wide as the name, the names 32 apart, as the design draws them (docs/LunaP.md §211.3).
            _sectionRow.Styles.Add(new Style(x => x.OfType<ToggleButton>())
            {
                Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0, 8)), new Setter(Layoutable.MarginProperty, new Thickness(16, 0)) },
            });
        }

        // The strip's look on any row of toggle buttons, so an item's page draws its sections as the frame does (§209).
        internal static void StyleStrip(Panel row)
        {
            row.Styles.Clear();
            var plain = new Style(x => x.OfType<ToggleButton>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                    new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0, 0, 0, 3)),
                    new Setter(TemplatedControl.PaddingProperty, new Thickness(16, 8)),
                    new Setter(TemplatedControl.FontSizeProperty, LunaTokens.BigScreenType.Caption),
                    new Setter(TemplatedControl.FontFamilyProperty, Media.LunaFonts.Body),
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(Layoutable.MinHeightProperty, 44.0 * LunaTokens.TextScale),
                    new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("LunaText")),
                },
            };
            var chosen = new Style(x => x.OfType<ToggleButton>().Class(":checked"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BorderBrushProperty, new DynamicResourceExtension("LunaAccent")),
                    new Setter(TemplatedControl.ForegroundProperty, new DynamicResourceExtension("LunaTextStrong")),
                },
            };
            var presenter = new Style(x => x.OfType<ToggleButton>().Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
                    new Setter(ContentPresenter.BorderBrushProperty, Brushes.Transparent),
                    new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(0, 0, 0, 3)),
                    new Setter(ContentPresenter.CornerRadiusProperty, new CornerRadius(0)),
                },
            };
            var chosenPresenter = new Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
                    new Setter(ContentPresenter.BorderBrushProperty, new DynamicResourceExtension("LunaAccent")),
                    new Setter(ContentPresenter.ForegroundProperty, new DynamicResourceExtension("LunaTextStrong")),
                },
            };
            row.Styles.Add(plain);
            row.Styles.Add(chosen);
            row.Styles.Add(presenter);
            row.Styles.Add(chosenPresenter);
        }

        /// <summary>The form the parts are arranged in; changing it rearranges them, keeping the selection and the focused role.</summary>
        public AdaptiveForm Form
        {
            get => GetValue(FormProperty);
            set => SetValue(FormProperty, value);
        }

        /// <summary>Where the big screen puts the detail: its own page (the default) or beside the content; the desktop is unchanged by it - see docs/LunaP.md §205.</summary>
        public AdaptiveDetailPlacement DetailPlacement
        {
            get => GetValue(DetailPlacementProperty);
            set => SetValue(DetailPlacementProperty, value);
        }

        /// <summary>The content's share of the width when the detail is beside it on the big screen, between 0.2 and 0.8. 0.4 by default.</summary>
        public double ContentShare
        {
            get => GetValue(ContentShareProperty);
            set => SetValue(ContentShareProperty, value);
        }

        /// <summary>What the help bar calls A on the content, such as Choose; null leaves the generated word, Select or Open. Null by default (docs/LunaP.md §210.5).</summary>
        public string? AcceptWord
        {
            get => GetValue(AcceptWordProperty);
            set => SetValue(AcceptWordProperty, value);
        }

        /// <summary>What the help bar calls Left and Right, after A, such as Value or Moment; null draws no such entry. Null by default (docs/LunaP.md §217).</summary>
        public string? SideWord
        {
            get => GetValue(SideWordProperty);
            set => SetValue(SideWordProperty, value);
        }

        /// <summary>What the help bar calls B, such as Resume where B returns to a game. "Back" by default (docs/LunaP.md §210.5).</summary>
        public string BackWord
        {
            get => GetValue(BackWordProperty);
            set => SetValue(BackWordProperty, value);
        }

        /// <summary>Whether the help bar and the pad take East as Select and South as Back, as a player's swap of the two asks; false by default.</summary>
        public bool SwapsAcceptAndBack
        {
            get => GetValue(SwapsAcceptAndBackProperty);
            set => SetValue(SwapsAcceptAndBackProperty, value);
        }

        // The detail stands beside the content on the big screen or in a card, so it has no page to open.
        private bool DetailBeside => Form == AdaptiveForm.Card || (Form == AdaptiveForm.BigScreen && DetailPlacement == AdaptiveDetailPlacement.Beside);

        /// <summary>The header role: drawn as a title row above everything on the desktop and the big screen, and as the card's title row in the Card form, so no window draws its own; null shows none.</summary>
        public AdaptiveHeader? Header
        {
            get => _header;
            set { _header = value; ShowHeader(); }
        }

        /// <summary>The chosen row's one action, drawn at the right of the desktop's header row and never on the big screen, where A carries it; null for none - see docs/LunaP.md §215.</summary>
        public LunaAction? HeaderAction
        {
            get => _headerHost.Action;
            set => _headerHost.Action = value;
        }

        /// <summary>The panel the Card form places the content, the detail, the header and the help bar into, for a host that animates it or a test that measures it; it is shown only in that form.</summary>
        public MenuPanel CardPanel => _cardPanel;

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
            set { _content = value; _contentHost.Child = value; if (Form == AdaptiveForm.Card) _cardPanel.Hints = Hints; }
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
                if (IsCoverOpen && CoverHints is { } covered) return covered;
                PadGlyphButton accept = SwapsAcceptAndBack ? PadGlyphButton.East : PadGlyphButton.South, back = SwapsAcceptAndBack ? PadGlyphButton.South : PadGlyphButton.East;
                if (_content is not null && (AcceptWord ?? AcceptHint) is { } accepted) hints.Add(new HintEntry(accepted) { Button = accept });
                if (SideWord is { } side) hints.Add(new HintEntry(side) { Button = PadGlyphButton.DPadLeftRight });
                hints.Add(new HintEntry(BackWord) { Button = back });
                if (_sections.Count > 1 && Form != AdaptiveForm.Card) hints.Add(new HintEntry(_sectionWord) { Button = PadGlyphButton.Shoulders });
                // The face buttons' actions, then List, then the main action last (docs/LunaP.md §211.2).
                foreach (LunaAction action in Padded().Where(a => !a.IsMain))
                    hints.Add(new HintEntry(action.Text) { Button = action.PadButton });
                if (ListToolsOnSelect) hints.Add(new HintEntry("List") { Button = PadGlyphButton.Select });
                foreach (LunaAction action in Padded().Where(a => a.IsMain))
                    hints.Add(new HintEntry(action.Text) { Button = action.PadButton });
                return hints;
            }
        }

        /// <summary>The word the help bar gives A on the content: Open where A opens the detail's page, Select otherwise; a layout may name the focused row's own action instead, or null to leave A unnamed.</summary>
        protected virtual string? AcceptHint => IsDetailOpen || _detail is null || DetailBeside ? "Select" : "Open";

        /// <summary>Redraws the help bar from Hints, after what a hint names has changed, such as the focused row.</summary>
        protected virtual void RefreshHints()
        {
            _hintBar.Entries = Hints;
            AutomationProperties.SetName(_hintBar, string.Join(", ", Hints.Select(h => h.Label)));
            if (Form == AdaptiveForm.Card) _cardPanel.Hints = Hints;
        }

        /// <summary>The list's filters, a second kind of section with one chosen at a time: a dropdown beside the search box on the desktop, a row of the List card on the big screen - see docs/LunaP.md §208.6.</summary>
        public IReadOnlyList<AdaptiveSection> Filters => _filters;

        /// <summary>The chosen filter's key, or null when there are none.</summary>
        public string? SelectedFilter => _selectedFilter;

        /// <summary>Raised when a person chooses a filter, never by SetFilters or SelectFilter.</summary>
        public event Action<string>? FilterChosen;

        /// <summary>Gives the list its filters and the chosen one, without raising FilterChosen; none takes the dropdown and the row away.</summary>
        /// <param name="filters">The filters in order; their keys must be distinct.</param>
        /// <param name="selected">The chosen filter's key; null or an unknown key chooses the first.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="filters"/> is null.</exception>
        /// <exception cref="System.ArgumentException">Two filters share a key.</exception>
        public void SetFilters(IEnumerable<AdaptiveSection> filters, string? selected = null)
        {
            if (filters is null) throw new ArgumentNullException(nameof(filters));
            List<AdaptiveSection> list = filters.ToList();
            if (list.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new ArgumentException("Two filters share a key.", nameof(filters));
            _filters = list;
            _selectedFilter = list.Any(f => f.Key == selected) ? selected : list.FirstOrDefault()?.Key;
            FillFilters();
            Arrange();
            FilterChanged();
        }

        /// <summary>Chooses a filter as the application, without raising FilterChosen.</summary>
        /// <param name="key">The filter's key.</param>
        /// <returns>Whether a filter has that key.</returns>
        public bool SelectFilter(string key) => SelectFilter(key, raise: false);

        /// <summary>Steps the chosen filter, wrapping at the ends, and raises FilterChosen.</summary>
        /// <param name="by">How many filters to move; negative moves back.</param>
        /// <returns>Whether there was a filter to move to.</returns>
        public bool StepFilter(int by)
        {
            if (_filters.Count < 2) return false;
            int at = Math.Max(0, _filters.ToList().FindIndex(f => f.Key == _selectedFilter));
            return SelectFilter(_filters[((at + by) % _filters.Count + _filters.Count) % _filters.Count].Key, raise: true);
        }

        /// <summary>The list-wide actions the List card offers under Search and Filter, such as a check for updates; each closes the card when invoked.</summary>
        /// <param name="actions">The actions in order.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> is null.</exception>
        public void SetListActions(IEnumerable<LunaAction> actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            _listActions = actions.ToList();
            FillListTools();
        }

        /// <summary>Whether the big screen has a List card to open: a search box, filters or list actions.</summary>
        public bool HasListTools => _hasSearch || _filters.Count > 0 || _listActions.Count > 0;

        // Select opens the card on the big screen where no action of the window's claims that button, so an action given Select keeps it.
        private bool ListToolsOnSelect => HasListTools && Form == AdaptiveForm.BigScreen && !Padded().Any(a => a.PadButton == PadGlyphButton.Select);

        /// <summary>Whether the List card is open over the big screen's content.</summary>
        public bool IsListToolsOpen => _listScrim.IsVisible;

        /// <summary>The panel the List card is drawn in, for a test that measures it; it is in the tree only on the big screen.</summary>
        public MenuPanel ListCard => _listCard;

        /// <summary>Opens the List card over the big screen's content with the focus on its first row; nothing on the desktop, in a card, or without tools.</summary>
        /// <returns>Whether the card was opened.</returns>
        public bool OpenListTools()
        {
            if (Form != AdaptiveForm.BigScreen || !HasListTools || IsListToolsOpen) return false;
            FillListTools();
            // The card joins the tree only while it is open, and what it covers is marked as a sheet layer marks a covered sheet, out of reach and excused by the audit.
            if (!_root.Children.Contains(_listScrim)) _root.Children.Add(Detach(_listScrim));
            foreach (Control beneath in _root.Children.Where(c => !ReferenceEquals(c, _listScrim))) beneath.SetValue(SheetLayer.IsCoveredProperty, true);
            _listScrim.IsVisible = true;
            if (!FocusFirstIn(_listRows)) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (IsListToolsOpen) FocusFirstIn(_listRows); }, Avalonia.Threading.DispatcherPriority.Loaded);
            return true;
        }

        /// <summary>Closes the List card and returns the focus to the content.</summary>
        /// <returns>Whether the card was open.</returns>
        public bool CloseListTools()
        {
            if (!IsListToolsOpen) return false;
            _listScrim.IsVisible = false;
            _root.Children.Remove(_listScrim);
            foreach (Control beneath in _root.Children) beneath.ClearValue(SheetLayer.IsCoveredProperty);
            if (!FocusRole(AdaptiveRole.Content)) Avalonia.Threading.Dispatcher.UIThread.Post(() => Refocus(AdaptiveRole.Content), Avalonia.Threading.DispatcherPriority.Loaded);
            return true;
        }

        /// <summary>Opens a page of the window's own in the content's place on the big screen, such as a row's actions, which B closes; nothing on the desktop or in a card - see docs/LunaP.md §208.4.</summary>
        /// <param name="page">The page's control.</param>
        /// <returns>Whether the page was opened.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="page"/> is null.</exception>
        public bool OpenPage(Control page)
        {
            if (page is null) throw new ArgumentNullException(nameof(page));
            if (Form != AdaptiveForm.BigScreen) return false;
            CloseListTools();
            _page = page;
            _pageHost.Child = page;
            Arrange();
            if (!FocusRole(AdaptiveRole.Detail)) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_page is not null) Refocus(AdaptiveRole.Detail); }, Avalonia.Threading.DispatcherPriority.Loaded);
            return true;
        }

        /// <summary>Closes the page and returns the focus to the content.</summary>
        /// <returns>Whether a page was open.</returns>
        public bool ClosePage()
        {
            if (_page is null) return false;
            _page = null;
            _pageHost.Child = null;
            Arrange();
            if (!FocusRole(AdaptiveRole.Content)) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_page is null) Refocus(AdaptiveRole.Content); }, Avalonia.Threading.DispatcherPriority.Loaded);
            return true;
        }

        /// <summary>Whether a page of the window's own is open in the content's place.</summary>
        public bool IsPageOpen => _page is not null;

        /// <summary>Whether a cover of the layout's own is open over the big screen, such as a question's card.</summary>
        public bool IsCoverOpen => _coverScrim.IsVisible;

        /// <summary>Opens a card centred over the big screen under the scrim, what lies beneath marked covered; a layout's question takes this form - see docs/LunaP.md §208.11.</summary>
        /// <param name="card">The card's control, centred and sized to its content.</param>
        /// <returns>Whether it was opened; false on the desktop or in a card, where the layout draws it in place.</returns>
        protected bool OpenCover(Control card)
        {
            if (card is null) throw new ArgumentNullException(nameof(card));
            if (Form != AdaptiveForm.BigScreen) return false;
            CloseListTools();
            _coverScrim.Children.Clear();
            _coverScrim.Background = CoverScrim;
            // The help band stays undimmed under the cover, naming the question's answers, and the card is centred above it (docs/LunaP.md §211.3).
            Thickness safe = Windowing.SheetLayer.GetSafeArea(this);
            _coverScrim.Margin = new Thickness(-safe.Left, -safe.Top, -safe.Right, _hintBand.IsEffectivelyVisible ? _hintBand.Bounds.Height - safe.Bottom : -safe.Bottom);
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;
            _coverScrim.Children.Add(Detach(card));
            if (!_root.Children.Contains(_coverScrim)) _root.Children.Add(Detach(_coverScrim));
            foreach (Control beneath in _root.Children.Where(c => !ReferenceEquals(c, _coverScrim))) beneath.SetValue(SheetLayer.IsCoveredProperty, true);
            _coverScrim.IsVisible = true;
            RefreshHints();
            return true;
        }

        /// <summary>Closes the cover and gives its card back to the layout.</summary>
        /// <returns>The card, or null when no cover was open.</returns>
        protected Control? CloseCover()
        {
            if (!IsCoverOpen) return null;
            Control? card = _coverScrim.Children.Count > 0 ? _coverScrim.Children[0] : null;
            _coverScrim.Children.Clear();
            _coverScrim.IsVisible = false;
            _root.Children.Remove(_coverScrim);
            foreach (Control beneath in _root.Children) beneath.ClearValue(SheetLayer.IsCoveredProperty);
            RefreshHints();
            return card;
        }

        /// <summary>The help bar's entries while a cover is open, in place of the roles'; null keeps the roles'.</summary>
        protected virtual IReadOnlyList<HintEntry>? CoverHints => null;

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
        public void SetActions(IEnumerable<LunaAction> actions, IEnumerable<LunaAction> drawnElsewhere) => SetActions(actions, drawnElsewhere, null);

        /// <summary>Declares the window's actions, those it draws itself, and those the big screen draws as buttons: the rest are the desktop toolbar's alone there, reached on the big screen through A, X and Select - see docs/LunaP.md §208.4.</summary>
        /// <param name="actions">The actions in order, separators included.</param>
        /// <param name="drawnElsewhere">Those among them the layout does not draw.</param>
        /// <param name="bigScreenButtons">Those the big screen draws as buttons; null draws every action there, as before.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> or <paramref name="drawnElsewhere"/> is null.</exception>
        public void SetActions(IEnumerable<LunaAction> actions, IEnumerable<LunaAction> drawnElsewhere, IEnumerable<LunaAction>? bigScreenButtons)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            if (drawnElsewhere is null) throw new ArgumentNullException(nameof(drawnElsewhere));
            Menus.Unbind(this, _bindings);
            _undrawn = drawnElsewhere.ToHashSet();
            _bigScreenButtons = bigScreenButtons?.ToHashSet();
            _actions = actions.ToList();
            _bindings = Menus.BindShortcuts(this, _actions);
            Arrange();
        }

        private HashSet<LunaAction>? _bigScreenButtons;

        /// <summary>Opens the detail's page on the big screen and puts the focus in it; does nothing on the desktop or where the detail is beside the content.</summary>
        /// <returns>Whether a page was opened.</returns>
        public bool OpenDetail()
        {
            if (Form != AdaptiveForm.BigScreen || DetailBeside || _detail is null || _detailOpen) return false;
            _detailOpen = true;
            Arrange();
            // A page whose controls come from a template has none until it is laid out, so the focus waits for that (docs/LunaP.md §200).
            if (!FocusRole(AdaptiveRole.Detail)) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_detailOpen) Refocus(AdaptiveRole.Detail); }, Avalonia.Threading.DispatcherPriority.Loaded);
            return true;
        }

        /// <summary>Closes the detail's page and returns the focus to the content.</summary>
        /// <returns>Whether a page was open.</returns>
        public bool CloseDetail()
        {
            if (!_detailOpen) return false;
            _detailOpen = false;
            Arrange();
            if (!FocusRole(AdaptiveRole.Content)) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!_detailOpen) Refocus(AdaptiveRole.Content); }, Avalonia.Threading.DispatcherPriority.Loaded);
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
                AdaptiveRole.Detail => _page is not null ? _pageHost : _detailHost,
                AdaptiveRole.Actions => _actionsHost,
                AdaptiveRole.Search => _searchHost,
                AdaptiveRole.Header => _headerHost,
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
            // A list's chosen row takes the focus ahead of its first row, so one row carries both marks (§208.10).
            InputElement? first = host.GetVisualDescendants().OfType<ListBoxItem>()
                .FirstOrDefault(i => i.IsSelected && i.Focusable && i.IsEffectivelyEnabled && i.IsEffectivelyVisible);
            first ??= host.GetVisualDescendants().OfType<InputElement>()
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
            if (SwapsAcceptAndBack) button = button switch { PadGlyphButton.South => PadGlyphButton.East, PadGlyphButton.East => PadGlyphButton.South, _ => button };
            // While the List card is open its rows take A through the focused control; B or Select closes it, and nothing reaches past it; a cover is its layout's to answer.
            if (IsCoverOpen) return false;
            if (IsListToolsOpen) return button is PadGlyphButton.East or PadGlyphButton.Select ? CloseListTools() : false;
            switch (button)
            {
                case PadGlyphButton.LeftShoulder when Form != AdaptiveForm.Card: return StepSection(-1);
                case PadGlyphButton.RightShoulder when Form != AdaptiveForm.Card: return StepSection(1);
                case PadGlyphButton.Select when ListToolsOnSelect && !IsPageOpen: return OpenListTools();
                case PadGlyphButton.South when Form == AdaptiveForm.BigScreen && !IsDetailOpen && !IsPageOpen && FocusedRole is null or AdaptiveRole.Content:
                    return OpenDetail();
                case PadGlyphButton.East:
                    if (ClosePage()) return true;
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

        // The help band 58 high across the screen and down to its foot, its entries centred in the 58 and its first glyph at the content's edge (docs/LunaP.md §211.4).
        private void PlaceHelpBand()
        {
            Thickness safe = Windowing.SheetLayer.GetSafeArea(this);
            _hintBand.MinHeight = 58 + safe.Bottom;
            _hintBand.Margin = new Thickness(-safe.Left, 0, -safe.Right, -safe.Bottom);
            _hintBand.Padding = new Thickness(safe.Left + 16, 0, safe.Right + 16, safe.Bottom);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == Windowing.SheetLayer.SafeAreaProperty && Form == AdaptiveForm.BigScreen) PlaceHelpBand();
            if (change.Property == FormProperty || change.Property == DetailPlacementProperty || change.Property == ContentShareProperty || change.Property == SwapsAcceptAndBackProperty)
            {
                AdaptiveRole? focused = FocusedRole;
                if (Form == AdaptiveForm.Desktop || DetailBeside) _detailOpen = false;
                Arrange();
                if (focused is { } role) Avalonia.Threading.Dispatcher.UIThread.Post(() => Refocus(role), Avalonia.Threading.DispatcherPriority.Loaded);
            }
            if (change.Property == AcceptWordProperty || change.Property == BackWordProperty || change.Property == SideWordProperty) RefreshHints();
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
                .Select(g => new SourceListGroup(g.Key, g.Select(s => new SourceListItem(s.Key, s.Label, s.Status)).ToList())), _selectedSection);
            _sectionRow.Children.Clear();
            foreach (AdaptiveSection section in _sections)
            {
                string key = section.Key;
                var button = new SectionTab(() => key == _selectedSection, () => SelectSection(key, raise: true)) { Content = section.Label, IsChecked = section.Key == _selectedSection };
                AutomationProperties.SetName(button, section.Status is { Length: > 0 } status ? $"{section.Label}, {status}" : section.Label);
                button.Click += (_, _) => SelectSection(key, raise: true);
                _sectionRow.Children.Add(button);
            }
            _sectionRow.Chosen = Math.Max(0, _sections.ToList().FindIndex(s => s.Key == _selectedSection));
        }

        // The sidebar as wide as its longest section's name, within reason, so a console's name is not cut on the desktop.
        private void FitSidebar()
        {
            if (_sectionSplit is null) return;
            _sidebar.Measure(Size.Infinity);
            if (_sidebar.DesiredSize.Width > 0) _sectionSplit.FixedSize = Math.Clamp(Math.Ceiling(_sidebar.DesiredSize.Width) + 16, 180, 320);
        }

        /// <inheritdoc/>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Avalonia.Threading.Dispatcher.UIThread.Post(FitSidebar, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void FillActions()
        {
            _toolBar.SetActions(_actions.Where(a => !_undrawn.Contains(a)));
            _actionRow.Children.Clear();
            foreach (LunaAction action in _actions.Where(a => !a.IsSeparator && a.Submenu is null && !_undrawn.Contains(a) && (_bigScreenButtons is null || _bigScreenButtons.Contains(a))).OrderByDescending(a => a.IsMain))
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
            _pageHost.IsVisible = false;
            _cardPanel.Child = null;
            _cardPanel.Pane = null;
            _cardRows.Child = null;
            _cardPane.Child = null;
            foreach (SplitPane split in _splits)
            {
                split.First = null;
                split.Second = null;
            }
            _splits.Clear();
            _sectionSplit = null;
            FillNavigation();
            FillActions();
            bool big = Form != AdaptiveForm.Desktop;
            MenuLook.SetIsOn(this, big);

            _searchHost.Child ??= _search;
            _searchHost.IsVisible = _hasSearch;
            _navigationHost.IsVisible = _sections.Count > 0;
            _actionsHost.IsVisible = Form == AdaptiveForm.BigScreen
                ? _actions.Any(a => !a.IsSeparator && !_undrawn.Contains(a) && (_bigScreenButtons is null || _bigScreenButtons.Contains(a)))
                : _actions.Any(a => !a.IsSeparator);
            _statusHost.IsVisible = _status is not null;

            ShowHeader();
            Control arranged = Form switch { AdaptiveForm.Card => ArrangeCard(), AdaptiveForm.BigScreen => ArrangeBigScreen(), _ => ArrangeDesktop() };
            bool cardOpen = Form == AdaptiveForm.BigScreen && IsListToolsOpen;
            _root.Children.Clear();
            _root.Children.Add(Detach(arranged));
            if (cardOpen)
            {
                arranged.SetValue(SheetLayer.IsCoveredProperty, true);
                _root.Children.Add(Detach(_listScrim));
            }
            else
            {
                arranged.ClearValue(SheetLayer.IsCoveredProperty);
                _listScrim.IsVisible = false;
            }
            Child = _root;
        }

        // The List card over the layout: a left panel on the scrim whose rows are stock controls in the look, Search, Filter and the list actions.
        private void SetUpListTools()
        {
            _listScrim.Children.Add(_listCard);
            _listScrim.Bind(Panel.BackgroundProperty, _listScrim.GetResourceObservable("LunaScrim", r => r as IBrush ?? DefaultScrim));
            MenuPanel.SetFontPath(_listCard, Media.LunaFonts.CondensedFile);
            _listCard.Child = _listRows;
            AutomationProperties.SetName(_filterBox, "Show");
            AutomationProperties.SetName(_cardFilter, "Filter");
            MenuRows.SetLabel(_cardFilter, "Filter");
            _filterBox.SelectionChanged += (_, _) => FilterPicked(_filterBox);
            _cardFilter.SelectionChanged += (_, _) => FilterPicked(_cardFilter);
            _listScrim.KeyDown += (_, e) => { if (e.Key == Key.Escape && CloseListTools()) e.Handled = true; };
        }

        private void FillListTools()
        {
            _listRows.Children.Clear();
            if (_hasSearch)
            {
                var search = new Button { Content = "Search", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                AutomationProperties.SetName(search, "Search");
                search.Click += (_, _) => { CloseListTools(); _search.Focus(); };
                _listRows.Children.Add(search);
            }
            if (_filters.Count > 0) _listRows.Children.Add(_cardFilter);
            foreach (LunaAction action in _listActions.Where(a => !a.IsSeparator))
            {
                var button = new ActionButton(action) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                button.Click += (_, _) => CloseListTools();
                _listRows.Children.Add(button);
            }
        }

        private bool SelectFilter(string key, bool raise)
        {
            if (!_filters.Any(f => f.Key == key)) return false;
            bool moved = _selectedFilter != key;
            _selectedFilter = key;
            FillFilters();
            if (moved) FilterChanged();
            if (raise && moved) FilterChosen?.Invoke(key);
            return true;
        }

        /// <summary>Called when the chosen filter changes, by the person or the application, before FilterChosen; a layout that narrows its content by the filter does so here.</summary>
        protected virtual void FilterChanged()
        {
        }

        // The dropdown and the card's row show the same filters and choice; a change from either is the person's.
        private void FillFilters()
        {
            _settingFilter = true;
            foreach (ComboBox box in new[] { _filterBox, _cardFilter })
            {
                box.ItemsSource = _filters.Select(f => f.Label).ToList();
                box.SelectedIndex = _filters.ToList().FindIndex(f => f.Key == _selectedFilter);
            }
            _settingFilter = false;
        }

        private void FilterPicked(ComboBox box)
        {
            if (_settingFilter || box.SelectedIndex < 0 || box.SelectedIndex >= _filters.Count) return;
            SelectFilter(_filters[box.SelectedIndex].Key, raise: true);
        }

        private static bool FocusFirstIn(Control root)
        {
            foreach (InputElement element in root.GetVisualDescendants().OfType<InputElement>())
                if (element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible && element.Focus(NavigationMethod.Directional)) return true;
            return false;
        }

        // The card's fixed parts: the panel over the scrim, the rows in Barlow at the big screen's Row size and in the words' own case, the pane in Barlow (§207.2).
        private void SetUpCard()
        {
            _cardScrim.Children.Add(_cardPanel);
            _cardScrim.Bind(Panel.BackgroundProperty, _cardScrim.GetResourceObservable("LunaScrim", r => r as IBrush ?? DefaultScrim));
            // The pane: a raised surface with the card radius and the pane's gutter, so its words never sit on the screen beneath (§210).
            _cardPane[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
            _cardPane.Bind(Border.PaddingProperty, _cardPane.GetObservable(MenuPanel.ScaleProperty, u => new Thickness(24 * u)));
            _cardPane.Bind(Border.CornerRadiusProperty, _cardPane.GetObservable(MenuPanel.ScaleProperty, u => new CornerRadius(8 * u)));
            MenuPanel.SetFontPath(_cardPanel, Media.LunaFonts.CondensedFile);
            MenuPanel.SetFontPath(_cardRows, Media.LunaFonts.BodyFile);
            MenuPanel.SetFontPath(_cardPane, Media.LunaFonts.BodyFile);
            _cardRows.Styles.Add(new Style(x => x.OfType<MenuRow>())
            {
                Setters =
                {
                    new Setter(MenuRow.TextSizeProperty, LunaTokens.BigScreenType.Row), new Setter(MenuRow.LetterCaseProperty, Media.LetterCase.None),
                    new Setter(MenuRow.InsetProperty, 24.0), new Setter(MenuRow.FilledMarksProperty, true), new Setter(MenuRow.MutedColorProperty, new DynamicResourceExtension("LunaMutedColor")),
                },
            });
        }

        private Control ArrangeCard()
        {
            Detach(_hintBar);
            _contentHost.IsVisible = true;
            _detailHost.IsVisible = _detail is not null;
            _detailHost.Margin = default;
            _cardRows.Child = Detach(_contentHost);
            _cardPanel.Child = _cardRows;
            if (_detail is not null)
            {
                _cardPane[!IsVisibleProperty] = _detail[!IsVisibleProperty];
                _cardPane.Child = Detach(_detailHost);
                _cardPanel.Pane = _cardPane;
            }
            _cardPanel.Hints = Hints;
            Detach(_headerHost);
            Detach(_cardScrim);
            return _cardScrim;
        }

        // The header as a title row of its own on the desktop and the big screen, or as the card panel's title, subtitle, picture and footer.
        private void ShowHeader()
        {
            AdaptiveHeader? h = _header;
            _cardPanel.Title = h?.Title;
            _cardPanel.Subtitle = h?.Subtitle;
            _cardPanel.TitleIcon = h?.Picture;
            _cardPanel.Footer = h?.Facts;
            _headerHost.IsVisible = h is not null && Form != AdaptiveForm.Card;
            bool card = h is null || Form == AdaptiveForm.Card;
            _headerHost.Form = Form == AdaptiveForm.BigScreen ? AdaptiveForm.BigScreen : AdaptiveForm.Desktop;
            _headerHost.Header = card ? null : h;
        }

        private Control ArrangeDesktop()
        {
            _navigationHost.Child = _sidebarScroll;
            _actionsHost.Child = _toolBar;
            _search.ClearValue(TextBox.FontFamilyProperty);
            _search.ClearValue(TextBox.FontSizeProperty);
            _search.MinWidth = 180;
            _search.ClearValue(MinHeightProperty);
            _hintBand.ClearValue(MinHeightProperty);
            _hintBand.Padding = new Thickness(48, 10, 48, 6);
            _hintBand.ClearValue(MarginProperty);
            _hintBar.ClearValue(VerticalAlignmentProperty);
            _hintBar.FontSize = 24;
            _hintBar.ClearValue(HintBar.EntrySpacingProperty);
            _hintBar.ClearValue(HintBar.IconTextSpacingProperty);
            _hintBar.ClearValue(HintBar.IconScaleProperty);
            _detailHost.ClearValue(TextElement.FontFamilyProperty);
            _detailHost.ClearValue(TextElement.FontSizeProperty);
            _detailHost.Styles.Remove(_paneTitle);
            _detailHost.IsVisible = _detail is not null;
            _contentHost.IsVisible = true;
            _detailHost.Margin = default;

            var top = new DockPanel { LastChildFill = true, Margin = new Thickness(4) };
            DockPanel.SetDock(_searchHost, Dock.Right);
            DockPanel.SetDock(_filterBox, Dock.Right);
            _searchHost.VerticalAlignment = VerticalAlignment.Center;
            _filterBox.IsVisible = _filters.Count > 0;
            top.Children.Add(Detach(_searchHost));
            top.Children.Add(Detach(_filterBox));
            top.Children.Add(Detach(_actionsHost));
            _actionsHost.Margin = default;

            Control main = _detail is null
                ? Detach(_contentHost)
                : Split(new SplitPane { Orientation = Orientation.Vertical, First = Detach(_contentHost), Second = Detach(_detailHost), Fixed = SplitSide.Second, FixedSize = 180, MinFirst = 120, MinSecond = 80, DividerLabel = "Resize the detail" });
            Control body = _sections.Count == 0
                ? main
                : _sectionSplit = Split(new SplitPane { First = Detach(_navigationHost), Second = main, Fixed = SplitSide.First, FixedSize = 180, MinFirst = 120, MinSecond = 320, DividerLabel = "Resize the sections" });
            FitSidebar();

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
            Grid.SetRow(Detach(_headerHost), 0);
            Grid.SetRow(top, 1);
            Grid.SetRow(body, 2);
            Grid.SetRow(Detach(_statusHost), 3);
            Detach(_hintBar);
            grid.Children.Add(_headerHost);
            grid.Children.Add(top);
            grid.Children.Add(body);
            grid.Children.Add(_statusHost);
            return grid;
        }

        private Control ArrangeBigScreen()
        {
            _navigationHost.Child = _sectionRow;
            _actionsHost.Child = _actionRow;
            _hintBar.Entries = Hints;
            AutomationProperties.SetName(_hintBar, string.Join(", ", Hints.Select(h => h.Label)));
            bool beside = DetailBeside && _detail is not null;
            bool ownPage = _page is not null;
            bool page = ownPage || (!beside && _detailOpen && _detail is not null);
            _contentHost.IsVisible = !page;
            _detailHost.IsVisible = (page && !ownPage) || (beside && !ownPage);
            _pageHost.IsVisible = ownPage;

            // The strip 44 high with 12 above and 16 below, the search box 240 by 44 (docs/LunaP.md §211.3).
            var top = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 12, 16, 16), MinHeight = 44 };
            DockPanel.SetDock(_searchHost, Dock.Right);
            top.Children.Add(Detach(_searchHost));
            top.Children.Add(Detach(_navigationHost));

            // Beside the content the detail stands next to the status too, since the apply bar belongs to the list (§208.2).
            bool statusInColumn = beside && _status is not null;
            var middle = new Grid { Margin = new Thickness(16, 0) };
            Control contentPart = Detach(_contentHost);
            if (statusInColumn)
            {
                var column = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
                Grid.SetRow(_contentHost, 0);
                Grid.SetRow(Detach(_statusHost), 1);
                column.Children.Add(_contentHost);
                column.Children.Add(_statusHost);
                contentPart = column;
            }
            middle.Children.Add(contentPart);
            middle.Children.Add(Detach(_detailHost));
            middle.Children.Add(Detach(_pageHost));
            Grid.SetColumn(contentPart, 0);
            Grid.SetColumn(_detailHost, beside ? 1 : 0);
            Grid.SetColumn(_pageHost, 0);
            Grid.SetColumnSpan(_pageHost, beside ? 2 : 1);
            if (beside)
            {
                double share = Math.Clamp(double.IsFinite(ContentShare) ? ContentShare : 0.4, 0.2, 0.8);
                middle.ColumnDefinitions = new ColumnDefinitions { new(share, GridUnitType.Star), new(1 - share, GridUnitType.Star) };
                _detailHost.Margin = new Thickness(16, 0, 0, 0);
                // The pane: a raised surface with the card radius and the pane's gutter (§208.10).
                _detailHost[!Border.BackgroundProperty] = new DynamicResourceExtension("LunaSurfaceRaised");
                _detailHost.CornerRadius = new CornerRadius(8);
                _detailHost.Padding = new Thickness(24);
            }
            else
            {
                _detailHost.Margin = default;
                _detailHost.ClearValue(Border.BackgroundProperty);
                _detailHost.CornerRadius = default;
                _detailHost.Padding = default;
            }

            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto,Auto") };
            Detach(_hintBar);
            _hintBand.Child = _hintBar;
            Control[] rows = { Detach(_headerHost), top, middle, Detach(_actionsHost), statusInColumn ? new Border() : Detach(_statusHost), _hintBand };
            _actionsHost.Margin = new Thickness(16, 8);
            _hintBar.Margin = default;
            _search.FontFamily = Media.LunaFonts.Body;
            _search.FontSize = LunaTokens.BigScreenType.Caption;
            _search.MinWidth = 240;
            _search.MinHeight = 44;
            PlaceHelpBand();
            _hintBar.VerticalAlignment = VerticalAlignment.Center;
            _hintBar.FontSize = LunaTokens.BigScreenType.Caption;
            _hintBar.EntrySpacing = 40;
            _hintBar.IconTextSpacing = 10;
            _hintBar.IconScale = 1.5;
            // The pane's words in Barlow at the big screen's Body size, whatever the tool put in it, and its title in the Condensed face (§208.10, §208.11).
            TextElement.SetFontFamily(_detailHost, Media.LunaFonts.Body);
            TextElement.SetFontSize(_detailHost, LunaTokens.BigScreenType.Body);
            if (!_detailHost.Styles.Contains(_paneTitle)) _detailHost.Styles.Add(_paneTitle);
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
