using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Controls
{
    // Which pane keeps its size when the window is resized.
    public enum SplitSide
    {
        First,
        Second,
    }

    // Two panes and a divider the user can drag - see docs/LunaP.md §26.6.
    //
    // Qt's QSplitter, and the evidence that it was missing is unusually direct: §21.2 found
    // `CheatDatabaseWindow.axaml:52` faking one as `ColumnDefinitions="2*,12,3*"`, with the 12 as
    // a spacer column and no way to move it. There is no GridSplitter anywhere in any of the three
    // repositories surveyed - not because nobody wanted a draggable divider, but because getting
    // one means three column definitions, a GridSplitter, two minimum widths and somewhere to keep
    // the size the user chose, and at that point people write `2*,12,3*` instead.
    //
    // ONE PANE IS FIXED AND THE OTHER IS ELASTIC, which is a departure from QSplitter's
    // proportional model and is the more useful behaviour for what a splitter is actually used
    // for. A sidebar next to a document does not want to grow when the window is widened; the
    // document does. Qt says the same thing with stretch factors, less directly. `Fixed` is which
    // side that is, so a right-hand panel works as well as a left-hand one.
    //
    // THE DIVIDER IS REMEMBERED IN PIXELS, NOT AS A FRACTION, and that is deliberate. A fraction
    // survives a window resize by moving the divider, which is exactly the thing the user was
    // being precise about when they dragged it: they made the sidebar wide enough for the longest
    // filename, not wide enough for 22% of a window they may never open at that size again.
    public class SplitPane : TemplatedControl
    {
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<SplitPane, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public static readonly StyledProperty<object?> FirstProperty =
            AvaloniaProperty.Register<SplitPane, object?>(nameof(First));

        public static readonly StyledProperty<object?> SecondProperty =
            AvaloniaProperty.Register<SplitPane, object?>(nameof(Second));

        public static readonly StyledProperty<SplitSide> FixedProperty =
            AvaloniaProperty.Register<SplitPane, SplitSide>(nameof(Fixed));

        public static readonly StyledProperty<double> FixedSizeProperty =
            AvaloniaProperty.Register<SplitPane, double>(nameof(FixedSize), 240);

        public static readonly StyledProperty<double> MinFirstProperty =
            AvaloniaProperty.Register<SplitPane, double>(nameof(MinFirst), 80);

        public static readonly StyledProperty<double> MinSecondProperty =
            AvaloniaProperty.Register<SplitPane, double>(nameof(MinSecond), 80);

        public static readonly StyledProperty<double> SplitterThicknessProperty =
            AvaloniaProperty.Register<SplitPane, double>(nameof(SplitterThickness), 4);

        public static readonly StyledProperty<string> DividerLabelProperty =
            AvaloniaProperty.Register<SplitPane, string>(nameof(DividerLabel), "Resize panes");

        private Grid? _grid;
        private ContentPresenter? _firstPresenter;
        private ContentPresenter? _secondPresenter;
        private GridSplitter? _splitter;
        private Border? _rule;
        private DefinitionBase? _fixedDefinition;

        // The divider's position and the property that mirrors it are two views of one number, and
        // each has to be able to move the other: dragging updates FixedSize, and a caller setting
        // FixedSize moves the divider. Without a guard that is a loop; this is the general form of
        // the flag six sites hand-rolled (§21.1), which the kit already owns.
        private readonly Suppressor _syncing = new();

        // Saved 400ms after the drag stops rather than on every pixel of it. A drag raises a
        // property change per frame, and writing panes.json sixty times a second would be a full
        // read-modify-write of the file per frame - the same reasoning §21.1 used for a search box
        // re-querying a database per keystroke, and the same tool.
        private Debounce? _save;

        // Setting this is what enables persistence; a pane without one is never remembered, on the
        // same opt-in principle as ToolWindow.WindowKey.
        //
        // Assigning it restores immediately when the pane is already on screen, rather than only
        // at the next attach. AppWindow hands a divider its key when a panel is added, which is
        // routinely after the window is open, and a key that only took effect on the following
        // attach would look like persistence that works except when you use it.
        public string? PaneKey
        {
            get => _paneKey;
            set
            {
                _paneKey = value;
                if (_attached) Restore();
            }
        }

        private string? _paneKey;

        // Tracked rather than asked for. `this.GetVisualRoot()` does not compile here even with
        // Avalonia.VisualTree imported, and rather than go looking for whatever replaced it in
        // 12.1.0 this uses the answer the two overrides below already have.
        private bool _attached;

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        // Left or top. Null collapses the pane and its divider together, which is how AppWindow
        // closes a side panel without the layout keeping a gap where it used to be.
        public object? First
        {
            get => GetValue(FirstProperty);
            set => SetValue(FirstProperty, value);
        }

        // Right or bottom.
        public object? Second
        {
            get => GetValue(SecondProperty);
            set => SetValue(SecondProperty, value);
        }

        // Which pane holds its size when the container changes size. The other one takes the rest.
        public SplitSide Fixed
        {
            get => GetValue(FixedProperty);
            set => SetValue(FixedProperty, value);
        }

        // The fixed pane's size, in device-independent pixels. Follows the divider while it is
        // dragged, so a caller can read it, save it, or set it to move the divider from a menu.
        public double FixedSize
        {
            get => GetValue(FixedSizeProperty);
            set => SetValue(FixedSizeProperty, value);
        }

        // How small each pane may be dragged. Not zero by default: a pane dragged to nothing looks
        // exactly like a pane that failed to render, and the divider left behind is four pixels
        // wide and hard to find again with a mouse.
        public double MinFirst
        {
            get => GetValue(MinFirstProperty);
            set => SetValue(MinFirstProperty, value);
        }

        public double MinSecond
        {
            get => GetValue(MinSecondProperty);
            set => SetValue(MinSecondProperty, value);
        }

        // The grab area, not a hairline: the visible rule is drawn by the theme and can be one
        // pixel, but a target a mouse has to hit cannot be.
        public double SplitterThickness
        {
            get => GetValue(SplitterThicknessProperty);
            set => SetValue(SplitterThicknessProperty, value);
        }

        // WHAT THE DIVIDER IS CALLED, and it needs a name because it is a control rather than a
        // decoration - which was measured rather than assumed. Avalonia's GridSplitter is
        // `Focusable` and a tab stop, and it MOVES ON THE ARROW KEYS: focused, one Left press took
        // a 200pt pane to 190. So a keyboard user reaches it, can use it, and - before this - was
        // told nothing about what they had landed on. The whole-window guard in
        // AccessibilityTests found it, which is the second time that particular test has caught a
        // control nobody had thought about (§24.1 was the first).
        //
        // Defaulted rather than left empty, because a name is only useless when it is wrong: two
        // dividers both called "Resize panes" is worse than one and much better than none.
        // AppWindow names each of its own after the panel it resizes.
        public string DividerLabel
        {
            get => GetValue(DividerLabelProperty);
            set => SetValue(DividerLabelProperty, value);
        }

        // UIA's Pane, which is what a resizable region of a window is. Unnamed unless the caller
        // says otherwise - what the two halves are about is the caller's word, exactly as §5.2
        // decided for a run of meters.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Pane);

        // Writes the divider's position now rather than waiting out the delay. Called on the way
        // out of the visual tree, because a window closed straight after a drag would otherwise
        // lose the last thing the user did to it.
        public void SaveNow()
        {
            _save?.Cancel();
            if (PaneKey is not { } key) return;

            // Edits the size and leaves everything else in the record alone. A side panel shares
            // this key and owns the Collapsed half of it - see PaneLayoutStore.Update.
            PaneLayoutStore.Update(key, layout => layout.Size = FixedSize);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            Unsubscribe();

            _grid = e.NameScope.Find<Grid>("PART_Grid");
            _firstPresenter = e.NameScope.Find<ContentPresenter>("PART_First");
            _secondPresenter = e.NameScope.Find<ContentPresenter>("PART_Second");
            _splitter = e.NameScope.Find<GridSplitter>("PART_Splitter");
            _rule = e.NameScope.Find<Border>("PART_Rule");

            Rebuild();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _attached = true;

            // Before the first layout pass, so a remembered divider is where it was rather than
            // jumping there once the window is already on screen.
            Restore();
        }

        // A size of zero is treated as "nothing saved" rather than as a divider dragged shut. The
        // minimums make a genuine zero unreachable by dragging, so a zero in the file is a record
        // written before a size ever was - a side panel saving that it was closed, most often.
        private void Restore()
        {
            if (PaneKey is { } key && PaneLayoutStore.Load(key) is { Size: > 0 } saved)
            {
                FixedSize = saved.Size;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            SaveNow();
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == FirstProperty || change.Property == SecondProperty ||
                change.Property == OrientationProperty || change.Property == FixedProperty ||
                change.Property == MinFirstProperty || change.Property == MinSecondProperty ||
                change.Property == SplitterThicknessProperty)
            {
                Rebuild();
                return;
            }

            if (change.Property != FixedSizeProperty) return;

            ApplySize();

            // Only a size the user chose is worth keeping. A caller assigning FixedSize during
            // construction is describing a default, and saving that would overwrite the divider
            // the user dragged last time with the one the programmer typed.
            if (!_syncing.IsSuppressing) return;

            _save ??= new Debounce(TimeSpan.FromMilliseconds(400), SaveNow);
            _save.Poke();
        }

        // Everything that changes the SHAPE of the grid: how many tracks, which way round, which
        // one is fixed. Kept apart from ApplySize because a drag changes the size several times a
        // second and rebuilding the definitions under a splitter mid-drag would take the ground
        // out from under it.
        private void Rebuild()
        {
            if (_grid is null || _firstPresenter is null || _secondPresenter is null || _splitter is null) return;

            Unsubscribe();

            bool horizontal = Orientation == Orientation.Horizontal;
            bool hasFirst = First is not null;
            bool hasSecond = Second is not null;
            bool split = hasFirst && hasSecond;

            _firstPresenter.Content = First;
            _secondPresenter.Content = Second;
            _firstPresenter.IsVisible = hasFirst;
            _secondPresenter.IsVisible = hasSecond;
            _splitter.IsVisible = split;
            if (_rule is not null) _rule.IsVisible = split;

            // A divider with nothing on one side of it is not a divider, so it takes no space
            // either - otherwise closing a side panel leaves a four-pixel scar down the window.
            GridLength gap = split ? GridLength.Auto : new GridLength(0);
            GridLength first = !hasFirst ? new GridLength(0)
                : !split || Fixed == SplitSide.Second ? new GridLength(1, GridUnitType.Star)
                : new GridLength(Math.Max(0, FixedSize));
            GridLength second = !hasSecond ? new GridLength(0)
                : !split || Fixed == SplitSide.First ? new GridLength(1, GridUnitType.Star)
                : new GridLength(Math.Max(0, FixedSize));

            _grid.ColumnDefinitions.Clear();
            _grid.RowDefinitions.Clear();

            if (horizontal)
            {
                var a = new ColumnDefinition { Width = first, MinWidth = hasFirst && split ? MinFirst : 0 };
                var b = new ColumnDefinition { Width = gap };
                var c = new ColumnDefinition { Width = second, MinWidth = hasSecond && split ? MinSecond : 0 };
                _grid.ColumnDefinitions.Add(a);
                _grid.ColumnDefinitions.Add(b);
                _grid.ColumnDefinitions.Add(c);

                Grid.SetColumn(_firstPresenter, 0);
                Grid.SetColumn(_splitter, 1);
                Grid.SetColumn(_secondPresenter, 2);
                Grid.SetRow(_firstPresenter, 0);
                Grid.SetRow(_splitter, 0);
                Grid.SetRow(_secondPresenter, 0);

                _splitter.ResizeDirection = GridResizeDirection.Columns;
                _splitter.Width = SplitterThickness;
                _splitter.Height = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment = VerticalAlignment.Stretch;
                PlaceRule(column: 1, row: 0, width: 1, height: double.NaN);

                _fixedDefinition = Fixed == SplitSide.First ? a : c;
            }
            else
            {
                var a = new RowDefinition { Height = first, MinHeight = hasFirst && split ? MinFirst : 0 };
                var b = new RowDefinition { Height = gap };
                var c = new RowDefinition { Height = second, MinHeight = hasSecond && split ? MinSecond : 0 };
                _grid.RowDefinitions.Add(a);
                _grid.RowDefinitions.Add(b);
                _grid.RowDefinitions.Add(c);

                Grid.SetRow(_firstPresenter, 0);
                Grid.SetRow(_splitter, 1);
                Grid.SetRow(_secondPresenter, 2);
                Grid.SetColumn(_firstPresenter, 0);
                Grid.SetColumn(_splitter, 0);
                Grid.SetColumn(_secondPresenter, 0);

                _splitter.ResizeDirection = GridResizeDirection.Rows;
                _splitter.Height = SplitterThickness;
                _splitter.Width = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment = VerticalAlignment.Stretch;
                PlaceRule(column: 0, row: 1, width: double.NaN, height: 1);

                _fixedDefinition = Fixed == SplitSide.First ? a : c;
            }

            if (split && _fixedDefinition is not null) _fixedDefinition.PropertyChanged += OnDefinitionChanged;
        }

        // The visible hairline, which lives in the same grid cell as the splitter and on top of
        // it. One pixel across the short way and stretched the long way, so the four points the
        // mouse can grab do not become four points of painted divider.
        private void PlaceRule(int column, int row, double width, double height)
        {
            if (_rule is null) return;

            Grid.SetColumn(_rule, column);
            Grid.SetRow(_rule, row);
            _rule.Width = width;
            _rule.Height = height;
            _rule.HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            _rule.VerticalAlignment = double.IsNaN(height) ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        }

        // The caller moved the divider by setting the property. Only touches the definition when
        // it disagrees, so this is a no-op during a drag - the definition is already the value
        // that told FixedSize what it is.
        private void ApplySize()
        {
            if (_fixedDefinition is null || _syncing.IsSuppressing) return;

            double size = Math.Max(0, FixedSize);

            switch (_fixedDefinition)
            {
                case ColumnDefinition column when !column.Width.IsAbsolute || Math.Abs(column.Width.Value - size) > 0.01:
                    column.Width = new GridLength(size);
                    break;
                case RowDefinition row when !row.Height.IsAbsolute || Math.Abs(row.Height.Value - size) > 0.01:
                    row.Height = new GridLength(size);
                    break;
            }
        }

        // The user moved the divider. GridSplitter writes the new length straight onto the
        // definition, so this is the only place a drag can be observed at all - there is no
        // "dragged" event on the splitter that reports where it ended up.
        private void OnDefinitionChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            GridLength length;
            if (e.Property == ColumnDefinition.WidthProperty) length = (GridLength)e.NewValue!;
            else if (e.Property == RowDefinition.HeightProperty) length = (GridLength)e.NewValue!;
            else return;

            // A star length here is the grid re-expressing a pane that is no longer fixed, not a
            // position the user chose; taking its Value would record "1" as a pixel width.
            if (!length.IsAbsolute) return;

            using (_syncing.Suppress()) SetCurrentValue(FixedSizeProperty, length.Value);
        }

        private void Unsubscribe()
        {
            if (_fixedDefinition is null) return;

            _fixedDefinition.PropertyChanged -= OnDefinitionChanged;
            _fixedDefinition = null;
        }
    }
}
