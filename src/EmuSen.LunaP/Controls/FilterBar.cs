using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // A search box, optionally preceded by a labelled facet dropdown - see docs/LunaP.md §14.2.
    public class FilterBar : TemplatedControl
    {
        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<FilterBar, string>(nameof(SearchText), string.Empty,
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<string> PlaceholderProperty =
            AvaloniaProperty.Register<FilterBar, string>(nameof(Placeholder), "Search");

        public static readonly StyledProperty<string> FacetLabelProperty =
            AvaloniaProperty.Register<FilterBar, string>(nameof(FacetLabel), string.Empty);

        public static readonly StyledProperty<bool> ShowFacetProperty =
            AvaloniaProperty.Register<FilterBar, bool>(nameof(ShowFacet));

        // Zero, so this changes nothing for anybody already using the control. Two consumers want
        // it non-zero and both re-read storage on every keystroke without it - docs/LunaP.md §21.1.
        public static readonly StyledProperty<TimeSpan> SearchDelayProperty =
            AvaloniaProperty.Register<FilterBar, TimeSpan>(nameof(SearchDelay), TimeSpan.Zero);

        private TextBox? _search;
        private Dropdown? _facet;
        private IEnumerable? _pendingFacets;
        private object? _pendingSelection;

        // Built on demand, and rebuilt when the delay changes. Null means "no delay", which is the
        // default and is a real state rather than a zero-length timer - a DispatcherTimer with a
        // zero interval still defers to the next dispatcher pass, which would turn the documented
        // default of "synchronous" into "one frame later" for every existing caller.
        private Debounce? _debounce;

        // Raised whenever the search text or the facet changes, from any cause.
        public event Action? Changed;

        // Raised for Enter in the search box, which the library uses to launch the first match.
        public event Action? Submitted;

        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        // "Console:" and the like; only shown alongside the dropdown.
        public string FacetLabel
        {
            get => GetValue(FacetLabelProperty);
            set => SetValue(FacetLabelProperty, value);
        }

        public bool ShowFacet
        {
            get => GetValue(ShowFacetProperty);
            set => SetValue(ShowFacetProperty, value);
        }

        // How long to wait after the last keystroke before raising Changed. Zero raises it
        // immediately, which is what this control has always done.
        //
        // SearchText still updates on every keystroke; only the notification waits. A caller reads
        // SearchText from inside Changed, so delaying both would be the same thing said twice, and
        // delaying the property would break anything binding to it.
        //
        // The FACET IS NEVER DEBOUNCED. Picking from a dropdown is a deliberate act that happens
        // once, not a stream of half-formed input, and making the user wait after it would read as
        // the application being slow.
        public TimeSpan SearchDelay
        {
            get => GetValue(SearchDelayProperty);
            set => SetValue(SearchDelayProperty, value);
        }

        public object? Facet => _facet?.SelectedItem ?? _pendingSelection;

        // Held until the template exists, so a caller can fill the facets from its constructor.
        public void SetFacets(IEnumerable items, object? selected)
        {
            _pendingFacets = items;
            _pendingSelection = selected;
            _facet?.Fill(items, selected);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_search is not null)
            {
                _search.PropertyChanged -= OnSearchPropertyChanged;
                _search.KeyDown -= OnSearchKeyDown;
            }
            if (_facet is not null) _facet.Chose -= OnFacetChose;

            _search = e.NameScope.Find<TextBox>("PART_Search");
            _facet = e.NameScope.Find<Dropdown>("PART_Facet");

            if (_search is not null)
            {
                // The property, not the TextChanged event - only this reacts to a Text set that did not come from typing.
                _search.PropertyChanged += OnSearchPropertyChanged;
                _search.KeyDown += OnSearchKeyDown;
            }

            if (_facet is not null)
            {
                _facet.Chose += OnFacetChose;
                if (_pendingFacets is not null) _facet.Fill(_pendingFacets, _pendingSelection);
            }
        }

        private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != TextBox.TextProperty) return;

            SetCurrentValue(SearchTextProperty, _search?.Text ?? "");

            if (SearchDelay <= TimeSpan.Zero)
            {
                Changed?.Invoke();
                return;
            }

            EnsureDebounce().Poke();
        }

        private void OnSearchKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;

            // Enter means "I have finished typing", so anything still waiting on the delay runs
            // now. Without this a caller that filters on Changed and acts on Submitted would act
            // on the results of the previous keystroke.
            _debounce?.Flush();
            Submitted?.Invoke();
        }

        private Debounce EnsureDebounce()
        {
            // Rebuilt when the delay changes, because DispatcherTimer's interval is fixed at
            // construction here and a stale one would keep the old timing silently.
            if (_debounce is null || _lastDelay != SearchDelay)
            {
                _debounce?.Cancel();
                _lastDelay = SearchDelay;
                _debounce = new Debounce(_lastDelay, () => Changed?.Invoke());
            }

            return _debounce;
        }

        private TimeSpan _lastDelay;

        // A pending filter belongs to a bar that is on screen. Leaving the timer running after the
        // control is torn down would fire Changed into a window that has gone.
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _debounce?.Cancel();
            base.OnDetachedFromVisualTree(e);
        }

        private void OnFacetChose(object? _)
        {
            _pendingSelection = _facet?.SelectedItem;
            Changed?.Invoke();
        }

        public void FocusSearch() => _search?.Focus();

        // The match every caller wants: case-insensitive, and an empty search matches everything.
        public static bool Matches(string? search, string? candidate) =>
            string.IsNullOrWhiteSpace(search)
            || (candidate ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
