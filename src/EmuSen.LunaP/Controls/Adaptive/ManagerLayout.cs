using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A change a manager's row can be marked for, such as install or remove.</summary>
    /// <param name="Key">What the tool switches on, never shown.</param>
    /// <param name="Label">The words the mark is shown with, in the row and in the summary.</param>
    public sealed record ManagerMark(string Key, string Label);

    /// <summary>A row and the change it is marked for.</summary>
    /// <param name="Row">The tool's row that is marked.</param>
    /// <param name="Mark">The change the row is marked for.</param>
    /// <typeparam name="T">The tool's row type.</typeparam>
    public readonly record struct ManagerMarked<T>(T Row, ManagerMark Mark);

    // Synaptic's and Pamac's frame on AdaptiveLayout: filters, a marked sortable table, a detail, an apply bar - see docs/LunaP.md §203.
    /// <summary>A manager window's frame: sections as filters, a sortable table with a mark per row, a detail, and an apply bar that shows the plan before anything runs, with one progress line and a space meter; it holds no knowledge of what the rows are.</summary>
    /// <typeparam name="T">The tool's row type.</typeparam>
    public class ManagerLayout<T> : AdaptiveLayout where T : class
    {
        private static readonly IReadOnlyList<ManagerMark> NoMarks = Array.Empty<ManagerMark>();

        private readonly LunaTable<T> _desktopTable = new();
        private readonly LunaTable<T> _bigTable = new();
        private readonly Dictionary<object, ManagerMarked<T>> _marks = new();
        private readonly Border _detailBox = new() { Padding = new Thickness(8) };
        private readonly TextBlock _summary = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, MinWidth = 120, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _progressLine = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, IsVisible = false };
        private readonly ProgressBar _spaceBar = new() { Minimum = 0, Maximum = 1, Width = 120, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _spaceText = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly StackPanel _space = new() { Orientation = Orientation.Horizontal, Spacing = 8, IsVisible = false };
        private readonly Button _applyButton = new() { Content = "Apply…" };
        private readonly Border _planSheet = new() { IsVisible = false, Padding = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 720 };
        private readonly TextBlock _planText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly Grid _contentGrid = new();
        private readonly Grid _bar = new() { Margin = new Thickness(4) };
        private readonly Grid _words = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        private readonly StackPanel _barRight = new() { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        private List<T> _rows = new();
        private string? _notice;
        private bool _running;

        // The question's parts, the row's action and page, and the plan's options (docs/LunaP.md §208.3-§208.5).
        private readonly TextBlock _planHeading = new() { Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap };
        private readonly Button _confirm = new() { Content = "Apply" };
        private readonly Button _cancel = new() { Content = "Cancel" };
        private readonly StackPanel _planButtons = new() { Margin = new Thickness(0, 12, 0, 0) };
        private readonly StackPanel _planOptions = new() { Spacing = 4, IsVisible = false };
        private IReadOnlyList<LunaAction> _planOptionList = Array.Empty<LunaAction>();
        private TaskCompletionSource<bool>? _asking;
        private string _yes = "Apply", _no = "Cancel";
        private Func<T, LunaAction?>? _actionFor;
        private Func<T, IReadOnlyList<LunaAction>>? _pageFor;

        /// <summary>A manager with no rows, columns or marks; the tool adds its columns with Column and its rows with SetRows.</summary>
        public ManagerLayout()
        {
            Mark = new LunaAction("Mark", () => CycleSelected()) { PadButton = PadGlyphButton.West, HelpText = "Cycles the chosen row through the changes it is offered." };
            Apply = new LunaAction("Apply…", () => OpenPlan()) { PadButton = PadGlyphButton.Start, IsMain = true, IsEnabled = false, HelpText = "Shows what the marked changes will do, then does them." };
            More = new LunaAction("More", () => OpenRowPage()) { HelpText = "Opens the chosen row's page of everything it offers." };
            _applyButton.Command = Apply;
            AutomationProperties.SetName(_applyButton, "Apply the marked changes");
            AutomationProperties.SetName(_spaceBar, "Space");
            _space.Children.Add(_spaceBar);
            _space.Children.Add(_spaceText);
            AutomationProperties.SetName(_progress, "Progress");
            foreach (LunaTable<T> table in new[] { _desktopTable, _bigTable })
            {
                table.Key = row => KeyOf(row);
                table.FitsWidth = true;
                table.Column(new LunaColumn<T>("Mark", row => MarkCell(row), row => MarkOf(row)?.Label ?? "not marked") { Width = "Auto" });
                table.AddHandler(Avalonia.Controls.Primitives.SelectingItemsControl.SelectionChangedEvent, (_, _) => { ShowDetail(); RefreshHints(); });
            }
            AutomationProperties.SetName(_desktopTable, "Rows");
            AutomationProperties.SetName(_bigTable, "Rows");

            _confirm.Click += (_, _) => Confirm();
            _cancel.Click += (_, _) => CancelPlan();
            _planSheet.Classes.Add(PlanClass);
            // The question and the answers always whole, the words scrolling between them in what the content area leaves.
            _planHeading.Text = "Apply these changes?";
            DockPanel.SetDock(_planHeading, Dock.Top);
            DockPanel.SetDock(_planButtons, Dock.Bottom);
            DockPanel.SetDock(_planOptions, Dock.Bottom);
            _planSheet.Child = new DockPanel
            {
                LastChildFill = true,
                Children = { _planHeading, _planButtons, _planOptions, new ScrollViewer { Content = _planText, MaxHeight = 360, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled } },
            };
            ArrangeQuestion();
            _contentGrid.SizeChanged += (_, e) => { _contentHeight = e.NewSize.Height; CapPlan(); };
            _planSheet[!Border.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LunaSurface");
            _planSheet[!Border.BorderBrushProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("LunaBorder");
            _planSheet.BorderThickness = new Thickness(1);
            _contentGrid.Children.Add(_desktopTable);
            _contentGrid.Children.Add(_planSheet);
            Content = _contentGrid;
            Detail = _detailBox;
            Status = ApplyBar();
            SectionChosen += _ => Show();
            SearchChanged += _ => Show();
            SetToolActions(Array.Empty<LunaAction>());
        }

        /// <summary>The style class on the Apply summary sheet.</summary>
        public const string PlanClass = "manager-plan";

        /// <summary>The least height the plan sheet is allowed on the desktop, so that a host sized to its content grows to show the question, a few lines and the buttons (§203.7).</summary>
        public const double PlanFloor = 240;

        /// <summary>The least height on the big screen, where the title is larger and the answers are two rows (§208.6).</summary>
        public const double BigScreenPlanFloor = 420;

        private double _contentHeight;

        private void CapPlan() => _planSheet.MaxHeight = Math.Max(Form == AdaptiveForm.Desktop ? PlanFloor : BigScreenPlanFloor, _contentHeight);

        /// <summary>The action that cycles the chosen row through its marks: the X button on a pad.</summary>
        public LunaAction Mark { get; }

        /// <summary>The main action: shows the plan for the marked rows, enabled only while some are marked and nothing runs.</summary>
        public LunaAction Apply { get; }

        /// <summary>The action that opens the chosen row's page: the X button once PageFor is set, and never drawn as a button.</summary>
        public LunaAction More { get; }

        /// <summary>The chosen row's one action, which A does on the big screen where the detail is beside the content or there is none, and which the help bar names in Select's place; null for a row that has none - see docs/LunaP.md §208.3.</summary>
        public Func<T, LunaAction?>? ActionFor
        {
            get => _actionFor;
            set { _actionFor = value; RefreshHints(); }
        }

        /// <summary>The rows of the chosen row's page, which X opens in the content's place on the big screen, each closing the page when invoked; once set, Mark leaves X and is reached through the page - see docs/LunaP.md §208.4.</summary>
        public Func<T, IReadOnlyList<LunaAction>>? PageFor
        {
            get => _pageFor;
            set
            {
                _pageFor = value;
                Mark.PadButton = value is null ? PadGlyphButton.West : null;
                More.PadButton = value is null ? null : PadGlyphButton.West;
                RefreshHints();
            }
        }

        /// <summary>The words a row's page is titled with; the row's own text by default.</summary>
        public Func<T, string>? PageTitle { get; set; }

        /// <summary>Whether a row belongs under a filter (SelectedFilter); null shows every row under every filter.</summary>
        public Func<T, string, bool>? InFilter { get; set; }

        /// <summary>The table the desktop shows, every column the tool added.</summary>
        public LunaTable<T> Table => _desktopTable;

        /// <summary>The table the big screen shows, only the columns the tool added for it.</summary>
        public LunaTable<T> BigScreenTable => _bigTable;

        /// <summary>The marks a row is offered, in the order the X button cycles them; none for a row that offers no change.</summary>
        public Func<T, IReadOnlyList<ManagerMark>>? MarksFor { get; set; }

        /// <summary>Whether a row belongs under a section; null shows every row under every section.</summary>
        public Func<T, string, bool>? InSection { get; set; }

        /// <summary>The text a row is searched by; null leaves search to match every row.</summary>
        public Func<T, string>? SearchKey { get; set; }

        /// <summary>The key that names a row across refreshes, so its mark and selection survive; the row itself by default.</summary>
        public Func<T, object> RowKey { get; set; } = row => row;

        /// <summary>The detail for the chosen row, or for none.</summary>
        public Func<T?, Control?>? DetailFor { get; set; }

        /// <summary>The apply bar's one line for the marked rows; by default a count per mark.</summary>
        public Func<IReadOnlyList<ManagerMarked<T>>, string>? Summarize { get; set; }

        /// <summary>The Apply summary's text, what will happen, shown before anything runs; by default the summary line.</summary>
        public Func<IReadOnlyList<ManagerMarked<T>>, string>? Plan { get; set; }

        /// <summary>Raised when the plan is confirmed, with the marked rows; the tool runs them and reports through SetProgress and Finish.</summary>
        public event Action<IReadOnlyList<ManagerMarked<T>>>? Applying;

        /// <summary>Raised whenever a mark is set, changed or cleared.</summary>
        public event Action? MarksChanged;

        /// <summary>Every row the tool gave, in its order.</summary>
        public IReadOnlyList<T> Rows => _rows;

        /// <summary>The rows shown under the chosen section and search, in the table's order.</summary>
        public IReadOnlyList<T> Shown => CurrentTable.Models;

        /// <summary>The chosen row of the table the form shows, or null.</summary>
        public T? Selected => CurrentTable.Selected;

        /// <summary>The marked rows, in the order the tool's rows are given.</summary>
        public IReadOnlyList<ManagerMarked<T>> Marked =>
            _rows.Where(r => _marks.ContainsKey(KeyOf(r))).Select(r => new ManagerMarked<T>(r, _marks[KeyOf(r)].Mark)).ToList();

        /// <summary>Whether the plan is shown, waiting to be confirmed or cancelled.</summary>
        public bool IsPlanOpen => _planSheet.IsVisible;

        /// <summary>Whether confirmed work is running; marks and Apply wait until Finish.</summary>
        public bool IsRunning => _running;

        /// <summary>The apply bar's summary line as shown.</summary>
        public string SummaryText => _summary.Text ?? string.Empty;

        /// <summary>The plan's text as last shown.</summary>
        public string PlanText => _planText.Text ?? string.Empty;

        private LunaTable<T> CurrentTable => Form == AdaptiveForm.BigScreen ? _bigTable : _desktopTable;

        /// <summary>Adds a column to the desktop's table, and to the big screen's where asked.</summary>
        /// <param name="column">The column, any kind LunaTable takes.</param>
        /// <param name="bigScreen">Whether the big screen's list shows it too.</param>
        /// <returns>This layout, so columns chain.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="column"/> is null.</exception>
        public ManagerLayout<T> Column(LunaColumn<T> column, bool bigScreen = false)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            _desktopTable.Column(column);
            if (bigScreen) _bigTable.Column(column);
            return this;
        }

        /// <summary>Replaces the rows; marks of rows still present are kept, those of rows gone are dropped.</summary>
        /// <param name="rows">The rows in the tool's order.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="rows"/> is null.</exception>
        public void SetRows(IEnumerable<T> rows)
        {
            if (rows is null) throw new ArgumentNullException(nameof(rows));
            _rows = rows.ToList();
            var keys = _rows.Select(KeyOf).ToHashSet();
            foreach (object gone in _marks.Keys.Where(k => !keys.Contains(k)).ToList()) _marks.Remove(gone);
            Show();
            Changed();
        }

        /// <summary>The change a row is marked for, or null.</summary>
        /// <param name="row">The row asked about.</param>
        /// <returns>Its mark, or null when it is not marked.</returns>
        public ManagerMark? MarkOf(T row) => _marks.TryGetValue(KeyOf(row), out ManagerMarked<T> m) ? m.Mark : null;

        /// <summary>Marks a row for one of the changes it is offered, or clears its mark.</summary>
        /// <param name="row">The row to mark or clear.</param>
        /// <param name="key">The mark's key, or null to clear.</param>
        /// <returns>Whether the row now carries what was asked; false for a mark it is not offered or while work runs.</returns>
        public bool SetMark(T row, string? key)
        {
            if (_running) return false;
            if (key is null)
            {
                if (_marks.Remove(KeyOf(row))) Changed();
                return true;
            }
            ManagerMark? mark = Offered(row).FirstOrDefault(m => m.Key == key);
            if (mark is null) return false;
            _marks[KeyOf(row)] = new ManagerMarked<T>(row, mark);
            Changed();
            return true;
        }

        /// <summary>Moves a row to the next mark it is offered, and after the last back to none.</summary>
        /// <param name="row">The row whose mark moves on.</param>
        /// <returns>Its mark afterwards, or null.</returns>
        public ManagerMark? CycleMark(T row)
        {
            IReadOnlyList<ManagerMark> offered = Offered(row);
            if (_running || offered.Count == 0) return MarkOf(row);
            int at = MarkOf(row) is { } now ? offered.ToList().FindIndex(m => m.Key == now.Key) : -1;
            SetMark(row, at + 1 < offered.Count ? offered[at + 1].Key : null);
            return MarkOf(row);
        }

        /// <summary>Clears every mark.</summary>
        public void ClearMarks()
        {
            if (_marks.Count == 0 || _running) return;
            _marks.Clear();
            Changed();
        }

        /// <summary>Shows the plan for the marked rows; nothing runs until it is confirmed.</summary>
        /// <returns>Whether there was anything to plan.</returns>
        public bool OpenPlan()
        {
            IReadOnlyList<ManagerMarked<T>> marked = Marked;
            if (marked.Count == 0 || _running || IsPlanOpen) return false;
            _yes = "Apply";
            _no = "Cancel";
            OpenSheet("Apply these changes?", (Plan ?? Summarize ?? DefaultSummary)(marked), options: true);
            return true;
        }

        /// <summary>Asks the tool's own question in the plan's form, a title, the sentences, then No and Yes (the verbs on the desktop), the tables hidden meanwhile - see docs/LunaP.md §208.5.</summary>
        /// <param name="title">The question, such as "Approve Snes9x?".</param>
        /// <param name="sentences">What the player is told before answering.</param>
        /// <param name="yes">The desktop's word for yes.</param>
        /// <param name="no">The desktop's word for no.</param>
        /// <returns>True for yes; false for no, for B, or when a question or a run is already under way.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="title"/> or <paramref name="sentences"/> is null.</exception>
        public Task<bool> Ask(string title, string sentences, string yes = "Yes", string no = "No")
        {
            if (title is null) throw new ArgumentNullException(nameof(title));
            if (sentences is null) throw new ArgumentNullException(nameof(sentences));
            if (IsPlanOpen || _running) return Task.FromResult(false);
            _asking = new TaskCompletionSource<bool>();
            _yes = yes;
            _no = no;
            OpenSheet(title, sentences, options: false);
            return _asking.Task;
        }

        /// <summary>Checkable actions the plan's question offers as rows between its words and its answers, such as a scrape's kinds; the tool reads their IsChecked when the plan runs.</summary>
        /// <param name="options">The actions, those not checkable left out.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="options"/> is null.</exception>
        public void SetPlanOptions(IEnumerable<LunaAction> options)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));
            _planOptionList = options.ToList();
            FillPlanOptions();
        }

        /// <summary>The question's title as last shown.</summary>
        public string QuestionTitle => _planHeading.Text ?? string.Empty;

        private void OpenSheet(string title, string words, bool options)
        {
            CloseDetail();
            ClosePage();
            CloseListTools();
            _planHeading.Text = title;
            _planText.Text = words;
            ArrangeQuestion();
            _planOptions.IsVisible = options && _planOptions.Children.Count > 0;
            _planSheet.IsVisible = true;
            ShowTables();
            FocusFirstIn(_planSheet);
        }

        /// <summary>Confirms the plan: the marked rows are handed to Applying and the bar shows progress until Finish; a question of Ask's answers yes.</summary>
        /// <returns>Whether a plan or a question was open.</returns>
        public bool Confirm()
        {
            if (!IsPlanOpen) return false;
            _planSheet.IsVisible = false;
            ShowTables();
            if (_asking is { } asked)
            {
                _asking = null;
                FocusRole(AdaptiveRole.Content);
                asked.TrySetResult(true);
                return true;
            }
            IReadOnlyList<ManagerMarked<T>> marked = Marked;
            _running = true;
            SetProgress(0, "Starting…");
            Changed();
            Applying?.Invoke(marked);
            return true;
        }

        /// <summary>Closes the plan without running anything; the marks stay.</summary>
        /// <returns>Whether a plan was open.</returns>
        public bool CancelPlan()
        {
            if (!IsPlanOpen) return false;
            _planSheet.IsVisible = false;
            ShowTables();
            FocusRole(AdaptiveRole.Content);
            if (_asking is { } asked)
            {
                _asking = null;
                asked.TrySetResult(false);
            }
            return true;
        }

        /// <inheritdoc/>
        protected override void FilterChanged() => Show();

        /// <inheritdoc/>
        protected override string? AcceptHint => ActsOnRow ? (Selected is { } row && _actionFor!(row) is { IsEnabled: true } action ? action.Text : null) : base.AcceptHint;

        // A does the row's action where it has no page to open: the big screen with the detail beside the content, or none.
        private bool ActsOnRow => _actionFor is not null && Form == AdaptiveForm.BigScreen && (Detail is null || DetailPlacement == AdaptiveDetailPlacement.Beside);

        // The question's answers: No then Yes as full-width rows on the big screen, the first focused; the verbs on buttons at the right on the desktop.
        private void ArrangeQuestion()
        {
            bool big = Form != AdaptiveForm.Desktop;
            _planButtons.Children.Clear();
            _planButtons.Orientation = big ? Orientation.Vertical : Orientation.Horizontal;
            _planButtons.HorizontalAlignment = big ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
            _planButtons.Spacing = big ? 4 : 8;
            foreach (Button b in new[] { _cancel, _confirm })
            {
                b.HorizontalAlignment = big ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
                b.HorizontalContentAlignment = big ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            }
            _cancel.Content = big ? "No" : _no;
            _confirm.Content = big ? "Yes" : _yes;
            AutomationProperties.SetName(_cancel, (string)_cancel.Content);
            AutomationProperties.SetName(_confirm, (string)_confirm.Content);
            _planButtons.Children.Add(_cancel);
            _planButtons.Children.Add(_confirm);
            LunaTypeScale type = LunaTokens.Type(big ? LunaForm.BigScreen : LunaForm.Desktop);
            _planHeading.FontFamily = big ? Media.LunaFonts.Condensed : FontFamily.Default;
            _planHeading.FontWeight = big ? FontWeight.Normal : FontWeight.SemiBold;
            _planHeading.FontSize = type.Title;
            CapPlan();
        }

        private void FillPlanOptions()
        {
            _planOptions.Children.Clear();
            bool big = Form != AdaptiveForm.Desktop;
            foreach (LunaAction option in _planOptionList.Where(o => o.IsCheckable))
            {
                if (big)
                {
                    var row = new ToggleSwitch { IsChecked = option.IsChecked, HorizontalAlignment = HorizontalAlignment.Stretch };
                    MenuRows.SetLabel(row, option.Text);
                    row.IsCheckedChanged += (_, _) => option.IsChecked = row.IsChecked == true;
                    _planOptions.Children.Add(row);
                }
                else
                {
                    var box = new CheckBox { Content = option.Text, IsChecked = option.IsChecked };
                    AutomationProperties.SetName(box, option.Text);
                    box.IsCheckedChanged += (_, _) => option.IsChecked = box.IsChecked == true;
                    _planOptions.Children.Add(box);
                }
            }
            _planOptions.IsVisible = IsPlanOpen && _asking is null && _planOptions.Children.Count > 0;
        }

        // The row's page: its name in the Title size, then each action as a full-width button that closes the page once done.
        private void OpenRowPage()
        {
            if (Selected is not { } row || _pageFor?.Invoke(row) is not { Count: > 0 } actions) return;
            var panel = new StackPanel { Spacing = 4 };
            LunaTypeScale type = LunaTokens.Type(LunaForm.BigScreen);
            string title = PageTitle?.Invoke(row) ?? row.ToString() ?? string.Empty;
            panel.Children.Add(new TextBlock { Text = title, FontFamily = Media.LunaFonts.Condensed, FontSize = type.Title, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 12) });
            foreach (LunaAction action in actions.Where(a => !a.IsSeparator))
            {
                var button = new ActionButton(action) { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                button.Click += (_, _) => ClosePage();
                panel.Children.Add(button);
            }
            OpenPage(new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        }

        /// <summary>The apply bar's one progress line.</summary>
        /// <param name="fraction">How much is done, 0 to 1, or null when it is not known.</param>
        /// <param name="line">What is being done, in the tool's words.</param>
        public void SetProgress(double? fraction, string line)
        {
            _progress.IsVisible = true;
            _progress.IsIndeterminate = fraction is null;
            _progress.Value = Math.Clamp(fraction ?? 0, 0, 1);
            _progressLine.IsVisible = true;
            _progressLine.Text = line;
            _summary.IsVisible = false;
        }

        /// <summary>Ends the running work: the progress line gives way to the summary, and the marks are cleared or kept.</summary>
        /// <param name="clearMarks">Whether the marks are cleared, as after work that did what they asked.</param>
        public void Finish(bool clearMarks = true)
        {
            _running = false;
            _progress.IsVisible = false;
            _progressLine.IsVisible = false;
            _summary.IsVisible = true;
            if (clearMarks) _marks.Clear();
            Changed();
        }

        /// <summary>The space meter: a bar for how full the disk is, and the words beside it at their own width, such as "8.2 GB · 1.1 TB free".</summary>
        /// <param name="text">What the meter says.</param>
        /// <param name="fraction">How full, 0 to 1.</param>
        public void SetSpace(string text, double fraction)
        {
            _space.IsVisible = true;
            _spaceText.Text = text;
            _spaceBar.Value = Math.Clamp(fraction, 0, 1);
            AutomationProperties.SetItemStatus(_spaceBar, text);
        }

        /// <summary>The space meter's words as shown.</summary>
        public string SpaceText => _spaceText.Text ?? string.Empty;

        /// <summary>Words the apply bar shows in the summary's place while no row is marked, such as a refusal that is about no row; null clears them.</summary>
        /// <param name="words">The tool's words, or null.</param>
        public void SetNotice(string? words)
        {
            _notice = string.IsNullOrWhiteSpace(words) ? null : words;
            ShowSummary(Marked);
        }

        /// <summary>Gives the frame the tool's own actions; Mark and Apply are added after them.</summary>
        /// <param name="actions">The tool's actions in order.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> is null.</exception>
        public void SetToolActions(IEnumerable<LunaAction> actions) => SetToolActions(actions, null);

        /// <summary>Gives the frame the tool's own actions and those the big screen draws as buttons; the rest are the desktop toolbar's there, reached on the big screen through the row's action, its page and the List card (§208.4).</summary>
        /// <param name="actions">The tool's actions in order.</param>
        /// <param name="bigScreenButtons">Those the big screen draws as buttons; null draws every action there.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> is null.</exception>
        public void SetToolActions(IEnumerable<LunaAction> actions, IEnumerable<LunaAction>? bigScreenButtons)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            SetActions(actions.Concat(new[] { LunaAction.Separator(), Mark, More, Apply }), new[] { Apply, More }, bigScreenButtons?.Concat(new[] { Mark }));
        }

        /// <inheritdoc/>
        public override bool Press(PadGlyphButton button)
        {
            PadGlyphButton accept = SwapsAcceptAndBack ? PadGlyphButton.East : PadGlyphButton.South, back = SwapsAcceptAndBack ? PadGlyphButton.South : PadGlyphButton.East;
            if (IsPlanOpen)
            {
                if (button == back) return CancelPlan();
                if (button == PadGlyphButton.Start) return _asking is null && Confirm();
                return false;
            }
            if (button == accept && ActsOnRow && !IsPageOpen && !IsListToolsOpen && FocusedRole == AdaptiveRole.Content && Selected is { } row && _actionFor!(row) is { IsEnabled: true } action)
            {
                action.Invoke();
                return true;
            }
            return base.Press(button);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == FormProperty)
            {
                T? selected = (change.OldValue is AdaptiveForm.BigScreen ? _bigTable : _desktopTable).Selected;
                _contentGrid.Children.Remove(_desktopTable);
                _contentGrid.Children.Remove(_bigTable);
                _contentGrid.Children.Insert(0, CurrentTable);
                ShowTables();
                ArrangeBar();
                ArrangeQuestion();
                FillPlanOptions();
                Show();
                CurrentTable.Select(selected);
            }
            base.OnPropertyChanged(change);
        }

        private object KeyOf(T row) => RowKey(row);

        private IReadOnlyList<ManagerMark> Offered(T row) => MarksFor?.Invoke(row) ?? NoMarks;

        private void CycleSelected()
        {
            if (Selected is { } row) CycleMark(row);
        }

        private Control MarkCell(T row)
        {
            ManagerMark? mark = MarkOf(row);
            return new TextBlock { Text = (mark is null ? "☐" : "☑ " + mark.Label), VerticalAlignment = VerticalAlignment.Center };
        }

        private void Show()
        {
            string? section = SelectedSection;
            string search = SearchText.Trim();
            IEnumerable<T> shown = _rows;
            if (section is not null && InSection is { } inSection) shown = shown.Where(r => inSection(r, section));
            if (SelectedFilter is { } filter && InFilter is { } inFilter) shown = shown.Where(r => inFilter(r, filter));
            if (search.Length > 0 && SearchKey is { } searchKey) shown = shown.Where(r => searchKey(r).Contains(search, StringComparison.CurrentCultureIgnoreCase));
            List<T> list = shown.ToList();
            _desktopTable.Refresh(list);
            _bigTable.Refresh(list);
            ShowDetail();
        }

        private void ShowDetail() => _detailBox.Child = DetailFor?.Invoke(Selected);

        private void Changed()
        {
            IReadOnlyList<ManagerMarked<T>> marked = Marked;
            ShowSummary(marked);
            Apply.IsEnabled = marked.Count > 0 && !_running;
            Mark.IsEnabled = !_running;
            _desktopTable.Refresh(_desktopTable.Models.ToList());
            _bigTable.Refresh(_bigTable.Models.ToList());
            MarksChanged?.Invoke();
        }

        private void ShowSummary(IReadOnlyList<ManagerMarked<T>> marked) =>
            _summary.Text = marked.Count == 0 ? _notice ?? "Nothing marked" : (Summarize ?? DefaultSummary)(marked);

        private static string DefaultSummary(IReadOnlyList<ManagerMarked<T>> marked) =>
            $"{marked.Count} marked: " + string.Join(", ", marked.GroupBy(m => m.Mark.Label).Select(g => $"{g.Key} {g.Count()}"));

        // While the plan is open the table is hidden, so no row lies under the plan and the pad cannot reach one.
        private void ShowTables()
        {
            _desktopTable.IsVisible = !IsPlanOpen;
            _bigTable.IsVisible = !IsPlanOpen;
        }

        private Control ApplyBar()
        {
            _barRight.Children.Add(_applyButton);
            _barRight.Children.Add(_space);
            Grid.SetColumn(_progressLine, 1);
            Grid.SetColumn(_summary, 1);
            _words.Children.Add(_progress);
            _words.Children.Add(_progressLine);
            _words.Children.Add(_summary);
            _bar.Children.Add(_words);
            _bar.Children.Add(_barRight);
            ArrangeBar();
            return _bar;
        }

        // One line on the desktop, the words then Apply and the meter; two on the big screen, whose type is too large to share one.
        private void ArrangeBar()
        {
            bool big = Form == AdaptiveForm.BigScreen;
            _bar.ColumnDefinitions = big ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
            _bar.RowDefinitions = big ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            _bar.RowSpacing = big ? 8 : 0;
            _bar.ColumnSpacing = big ? 0 : 12;
            Grid.SetColumn(_barRight, big ? 0 : 1);
            Grid.SetRow(_barRight, big ? 1 : 0);
            _barRight.HorizontalAlignment = big ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        }

        private static void FocusFirstIn(Control root)
        {
            foreach (Avalonia.Input.InputElement element in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root).OfType<Avalonia.Input.InputElement>())
            {
                if (element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible && element.Focus()) return;
            }
        }
    }
}
