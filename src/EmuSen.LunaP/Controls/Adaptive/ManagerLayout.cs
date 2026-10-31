using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EmuSen.LunaP.Commands;

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
        private readonly MeterRow _space = new() { MinWidth = 200, IsVisible = false };
        private readonly Button _applyButton = new() { Content = "Apply…" };
        private readonly Border _planSheet = new() { IsVisible = false, Padding = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 720 };
        private readonly TextBlock _planText = new() { TextWrapping = TextWrapping.Wrap };
        private readonly Grid _contentGrid = new();
        private List<T> _rows = new();
        private bool _running;

        /// <summary>A manager with no rows, columns or marks; the tool adds its columns with Column and its rows with SetRows.</summary>
        public ManagerLayout()
        {
            Mark = new LunaAction("Mark", () => CycleSelected()) { PadButton = PadGlyphButton.West, HelpText = "Cycles the chosen row through the changes it is offered." };
            Apply = new LunaAction("Apply…", () => OpenPlan()) { PadButton = PadGlyphButton.Start, IsMain = true, IsEnabled = false, HelpText = "Shows what the marked changes will do, then does them." };
            _applyButton.Command = Apply;
            AutomationProperties.SetName(_applyButton, "Apply the marked changes");
            AutomationProperties.SetName(_space, "Space");
            AutomationProperties.SetName(_progress, "Progress");
            foreach (LunaTable<T> table in new[] { _desktopTable, _bigTable })
            {
                table.Key = row => KeyOf(row);
                table.Column(new LunaColumn<T>("Mark", row => MarkCell(row), row => MarkOf(row)?.Label ?? "not marked") { Width = "Auto" });
                table.AddHandler(Avalonia.Controls.Primitives.SelectingItemsControl.SelectionChangedEvent, (_, _) => ShowDetail());
            }
            AutomationProperties.SetName(_desktopTable, "Rows");
            AutomationProperties.SetName(_bigTable, "Rows");

            var confirm = new Button { Content = "Apply" };
            confirm.Click += (_, _) => Confirm();
            var cancel = new Button { Content = "Cancel" };
            cancel.Click += (_, _) => CancelPlan();
            AutomationProperties.SetName(confirm, "Apply");
            AutomationProperties.SetName(cancel, "Cancel");
            _planSheet.Classes.Add(PlanClass);
            _planSheet.Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Apply these changes?", FontWeight = FontWeight.SemiBold },
                    new ScrollViewer { Content = _planText, MaxHeight = 360 },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, confirm } },
                },
            };
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

        /// <summary>The action that cycles the chosen row through its marks: the X button on a pad.</summary>
        public LunaAction Mark { get; }

        /// <summary>The main action: shows the plan for the marked rows, enabled only while some are marked and nothing runs.</summary>
        public LunaAction Apply { get; }

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
            if (marked.Count == 0 || _running) return false;
            CloseDetail();
            _planText.Text = (Plan ?? Summarize ?? DefaultSummary)(marked);
            _planSheet.IsVisible = true;
            FocusFirstIn(_planSheet);
            return true;
        }

        /// <summary>Confirms the plan: the marked rows are handed to Applying and the bar shows progress until Finish.</summary>
        /// <returns>Whether a plan was open.</returns>
        public bool Confirm()
        {
            if (!IsPlanOpen) return false;
            _planSheet.IsVisible = false;
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
            FocusRole(AdaptiveRole.Content);
            return true;
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
        }

        /// <summary>Ends the running work: the progress line gives way to the summary, and the marks are cleared or kept.</summary>
        /// <param name="clearMarks">Whether the marks are cleared, as after work that did what they asked.</param>
        public void Finish(bool clearMarks = true)
        {
            _running = false;
            _progress.IsVisible = false;
            _progressLine.IsVisible = false;
            if (clearMarks) _marks.Clear();
            Changed();
        }

        /// <summary>The space meter: the words, such as "312 MB · 410 GB free", and how full the disk is.</summary>
        /// <param name="text">What the meter says.</param>
        /// <param name="fraction">How full, 0 to 1.</param>
        public void SetSpace(string text, double fraction)
        {
            _space.IsVisible = true;
            _space.Label = "Space";
            _space.ValueText = text;
            _space.Percent = Math.Clamp(fraction, 0, 1) * 100;
        }

        /// <summary>Gives the frame the tool's own actions; Mark and Apply are added after them.</summary>
        /// <param name="actions">The tool's actions in order.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="actions"/> is null.</exception>
        public void SetToolActions(IEnumerable<LunaAction> actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            SetActions(actions.Concat(new[] { LunaAction.Separator(), Mark, Apply }), new[] { Apply });
        }

        /// <inheritdoc/>
        public override bool Press(PadGlyphButton button)
        {
            if (IsPlanOpen)
            {
                if (button == PadGlyphButton.East) return CancelPlan();
                if (button == PadGlyphButton.Start) return Confirm();
                return false;
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
            _summary.Text = marked.Count == 0 ? "Nothing marked" : (Summarize ?? DefaultSummary)(marked);
            Apply.IsEnabled = marked.Count > 0 && !_running;
            Mark.IsEnabled = !_running;
            _desktopTable.Refresh(_desktopTable.Models.ToList());
            _bigTable.Refresh(_bigTable.Models.ToList());
            MarksChanged?.Invoke();
        }

        private static string DefaultSummary(IReadOnlyList<ManagerMarked<T>> marked) =>
            $"{marked.Count} marked: " + string.Join(", ", marked.GroupBy(m => m.Mark.Label).Select(g => $"{g.Key} {g.Count()}"));

        private Control ApplyBar()
        {
            var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(4) };
            var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            right.Children.Add(_applyButton);
            right.Children.Add(_space);
            DockPanel.SetDock(right, Dock.Right);
            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            left.Children.Add(_progress);
            left.Children.Add(_progressLine);
            left.Children.Add(_summary);
            bar.Children.Add(right);
            bar.Children.Add(left);
            return bar;
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
