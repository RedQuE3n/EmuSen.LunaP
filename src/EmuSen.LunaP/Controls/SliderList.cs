using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // A long column of adjustable numbers under headings, virtualised, so a thousand cost what the dozen in view do - see docs/LunaP.md §97.
    /// <summary>A scrolling, virtualised column of SliderRows under headings: each SliderItem in its items is a row, each string a heading.</summary>
    public class SliderList : ItemsControl
    {
        /// <summary>The style class this control adds to itself, which its theme file selects on.</summary>
        public const string StyleClass = "luna-slider-list";

        // Half a viewport realised past each edge, so the row a pad moves onto next already exists - see §97.2.
        internal const double BufferViewports = 0.5;

        public SliderList()
        {
            Classes.Add(StyleClass);
            KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Continue);
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { CacheLength = BufferViewports });
            ItemTemplate = new FuncDataTemplate<object>((item, _) => Build(item), supportsRecycling: false);
        }

        /// <summary>Raised when a person moves a row's slider or presses its Reset, with the item, whose Value is already the new one. Not raised by setting Value.</summary>
        public event Action<SliderItem>? ValueChanged;

        /// <summary>The items that are rows, in order, whether or not a row shows them now and whether or not Search passes them.</summary>
        public IEnumerable<SliderItem> Sliders => (_whole ?? ItemsSource)?.OfType<SliderItem>() ?? Enumerable.Empty<SliderItem>();

        /// <summary>The items that are rows and pass Search, in order: all of Sliders while Search is empty.</summary>
        public IEnumerable<SliderItem> Matching => ItemsSource?.OfType<SliderItem>() ?? Enumerable.Empty<SliderItem>();

        /// <summary>Words the rows must match, each in a row's label or keywords, in any case; a heading that matches keeps every row under it. Empty shows every row. Empty by default.</summary>
        public string Search
        {
            get => _search;
            set
            {
                _search = value?.Trim() ?? string.Empty;
                Narrow();
            }
        }

        /// <summary>The rows that exist now: those in view and a few beyond, never one per item.</summary>
        public IEnumerable<SliderRow> Realized =>
            GetRealizedContainers().Where(c => c.IsVisible).Select(c => (c as ContentPresenter)?.Child).OfType<SliderRow>();

        /// <summary>The row showing an item now, or null when it is scrolled out of the realised range or not in this list.</summary>
        /// <param name="item">An item of this list.</param>
        /// <returns>The realised row, or null.</returns>
        public SliderRow? RowFor(SliderItem item) =>
            item?.Row is { } row && ReferenceEquals(row.FindAncestorOfType<SliderList>(), this) && row.IsEffectivelyVisible ? row : null;

        /// <summary>Scrolls an item's row into view, lays the list out and returns the row, or null when the item is not in this list or the list is not shown.</summary>
        /// <param name="item">An item of this list.</param>
        /// <returns>The row, now in view, or null.</returns>
        public SliderRow? Reveal(SliderItem item)
        {
            if (item is null || ItemsSource is null || ItemsView.IndexOf(item) < 0) return null;
            ScrollIntoView(item);
            UpdateLayout();
            return RowFor(item);
        }

        private ScrollViewer? _scroll;
        private IEnumerable? _whole;
        private string _search = string.Empty;
        private bool _narrowing;

        // The host's items shown whole, or the rows that match with the headings over them - see §97.8.
        private void Narrow()
        {
            if (_whole is null) return;
            if (_search.Length == 0)
            {
                if (ReferenceEquals(ItemsSource, _whole)) return;
                _narrowing = true;
                try { ItemsSource = _whole; }
                finally { _narrowing = false; }
                return;
            }
            var shown = new List<object>();
            string? heading = null;
            bool headingMatches = false;
            foreach (object? item in _whole)
            {
                if (item is null) continue;
                if (item is string text)
                {
                    heading = text;
                    headingMatches = FilterBar.MatchesWords(_search, text);
                    continue;
                }
                if (!headingMatches && !(item is SliderItem slider && FilterBar.MatchesWords(_search, slider.Label, slider.Keywords))) continue;
                if (heading is not null)
                {
                    shown.Add(heading);
                    heading = null;
                }
                shown.Add(item);
            }
            _narrowing = true;
            try { ItemsSource = shown; }
            finally { _narrowing = false; }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        }

        // New items start at the top, as a rebuilt column would; the old offset belonged to other rows.
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ItemsSourceProperty && _scroll is { } scroll) scroll.Offset = default;
            if (change.Property == ItemsSourceProperty && !_narrowing)
            {
                _whole = ItemsSource;
                if (_search.Length > 0) Narrow();
            }
            // Once the focus has left the list its container may be recycled again, or a row scrolled far away would stay built unseen - see §97.7.
            if (change.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin) KeyboardNavigation.SetTabOnceActiveElement(this, null);
        }

        // The panel keeps a container alive only while it is the tab-once element, and ItemsControl names the focused slider, not its container - see §97.3.
        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            for (Visual? at = e.Source as Visual; at is not null && !ReferenceEquals(at, this); at = at.GetVisualParent())
                if (at is Control container && IndexFromContainer(container) >= 0)
                {
                    KeyboardNavigation.SetTabOnceActiveElement(this, container);
                    return;
                }
        }

        private Control? Build(object? item) => item switch
        {
            SliderItem slider => BuildRow(slider),
            string heading => new SectionHeader { Text = heading, Margin = new Thickness(0, 10, 0, 4), TextWrapping = TextWrapping.Wrap },
            _ => null,
        };

        // A row is built for the item each time a container takes it, never rebound, so a row always names and shows its own item - see §97.1.
        private SliderRow BuildRow(SliderItem item)
        {
            var row = new SliderRow
            {
                Label = item.Label,
                Minimum = item.Minimum,
                Maximum = item.Maximum,
                Step = item.Step,
                DefaultValue = item.DefaultValue,
                Value = item.Value,
                Margin = new Thickness(0, 0, 0, 4),
            };
            if (item.Name is { Length: > 0 } name) row.Name = name;
            item.Row = row;
            row.ValueChanged += value =>
            {
                item.Held = value;
                ValueChanged?.Invoke(item);
            };
            return row;
        }
    }

    // One number's declaration and its value, which outlives the row that shows it - see docs/LunaP.md §97.1.
    /// <summary>One adjustable number shown by a SliderList: its label, range, step and default, and its value, kept while no row shows it.</summary>
    public sealed class SliderItem
    {
        private double _value;

        /// <summary>Declares a number.</summary>
        /// <param name="label">What the number is, the row's label.</param>
        /// <param name="minimum">The lowest value.</param>
        /// <param name="maximum">The highest value; equal to the minimum, the row's slider is disabled.</param>
        /// <param name="step">How far one press moves the value; zero or less means a hundredth of the range.</param>
        /// <param name="defaultValue">What Reset returns to.</param>
        /// <param name="value">The value to start from.</param>
        public SliderItem(string label, double minimum, double maximum, double step, double defaultValue, double value)
        {
            Label = label ?? string.Empty;
            Minimum = minimum;
            Maximum = maximum;
            Step = step;
            DefaultValue = defaultValue;
            _value = value;
        }

        /// <summary>What the number is, shown as its row label and given to the slider and Reset as their names.</summary>
        public string Label { get; }

        /// <summary>The lowest value the slider reaches.</summary>
        public double Minimum { get; }

        /// <summary>The highest value the slider reaches; equal to Minimum, the row is disabled.</summary>
        public double Maximum { get; }

        /// <summary>How far one press moves the value; zero or less means a hundredth of the range.</summary>
        public double Step { get; }

        /// <summary>The value Reset returns to, shown beside the value while the two differ.</summary>
        public double DefaultValue { get; }

        /// <summary>The name given to the row that shows this item, for a test or an automation tool to find it by. Null for none.</summary>
        public string? Name { get; init; }

        /// <summary>Further text a SliderList search matches besides the label, such as a parameter's id. Not shown. Null for none.</summary>
        public string? Keywords { get; init; }

        /// <summary>Whatever the host knows this number by, such as a parameter's id. Not shown.</summary>
        public object? Tag { get; init; }

        /// <summary>The value. Setting it moves the row showing it, if one does, and raises nothing.</summary>
        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                if (Row is { } row) row.Value = value;
            }
        }

        /// <summary>Whether the value is the default, to within a thousandth of the step, as SliderRow.IsDefault reckons it.</summary>
        public bool IsDefault => Math.Abs(_value - DefaultValue) <= (Step > 0 ? Step : Math.Max((Maximum - Minimum) / 100, double.Epsilon)) / 1000;

        // The last row built for this item; a row scrolled away keeps it, harmlessly, until the next is built.
        internal SliderRow? Row { get; set; }

        // A person's move, which the row already shows.
        internal double Held { set => _value = value; }
    }
}
