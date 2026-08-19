using System;
using System.Collections;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // A search box, optionally preceded by a labelled facet dropdown - see docs/LunaP.md §14.2.
    /// <summary>A search box, optionally preceded by a labelled facet dropdown.</summary>
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

        // Raised when the USER changes the search text or the facet, and not when an application
        // sets SearchText itself. It said "from any cause" until §80.1, which was the behaviour and
        // not the intent: the two sibling controls that raise a selection event both suppress the
        // programmatic case (Dropdown.Chose is not raised by Fill, LunaList.Chose not by Refresh or
        // Select), and SearchText's own summary has always promised the same thing here.
        /// <summary>Raised when the user changes the search text or the facet. Deferred by SearchDelay, so typing raises it once the typing stops rather than per keystroke. Not raised by setting SearchText, so restoring a saved filter cannot look like a search.</summary>
        public event Action? Changed;

        // Raised for Enter in the search box, which the library uses to launch the first match.
        /// <summary>Raised when Enter is pressed in the search box, which also brings any pending Changed forward.</summary>
        public event Action? Submitted;

        /// <summary>What is typed in the search box. Setting it does not raise Changed.</summary>
        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        /// <summary>The grey text shown in the search box while it is empty.</summary>
        public string Placeholder
        {
            get => GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        // "Console:" and the like; only shown alongside the dropdown.
        /// <summary>The label beside the facet dropdown.</summary>
        public string FacetLabel
        {
            get => GetValue(FacetLabelProperty);
            set => SetValue(FacetLabelProperty, value);
        }

        /// <summary>Whether the facet dropdown is shown at all. False leaves just the search box.</summary>
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
        /// <summary>How long typing must pause before Changed is raised. Zero raises it on every keystroke, which suits an in-memory list and not a query.</summary>
        public TimeSpan SearchDelay
        {
            get => GetValue(SearchDelayProperty);
            set => SetValue(SearchDelayProperty, value);
        }

        /// <summary>The selected facet, or null when none is chosen. Readable before the template has been applied.</summary>
        public object? Facet => _facet?.SelectedItem ?? _pendingSelection;

        // Held until the template exists, so a caller can fill the facets from its constructor.
        /// <summary>Fills the facet dropdown, without raising Changed.</summary>
        /// <param name="items">The facet values. Their ToString is what is shown.</param>
        /// <param name="selected">Which to select, or null for none. Safe to call before the control has a template.</param>
        public void SetFacets(IEnumerable items, object? selected)
        {
            _pendingFacets = items;
            _pendingSelection = selected;
            _facet?.Fill(items, selected);
        }

        // A Group holding two named controls rather than a named thing in its own right. The parts
        // are what a reader interacts with, and the template names them from Placeholder and
        // FacetLabel - properties that already held exactly the words a label wants. A PLACEHOLDER
        // IS NOT A LABEL: it is announced separately where it is announced at all, and it vanishes
        // the moment the user types, which is precisely when they might want reminding what the box
        // was for. See docs/LunaP.md §24.2.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

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
                // The property, not the TextChanged event, so a Text set from anywhere keeps
                // SearchText in step. Which cause it was is decided in the handler, not here.
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

            // A box whose text ALREADY equals SearchText is the template binding echoing a
            // programmatic set back, not somebody typing: typing changes the box first, so the two
            // still differ when this runs. That distinction is the whole of the suppression, and it
            // needs no flag - which is why this is not a Suppressor like Dropdown._filling, even
            // though it enforces the same rule. Measured before the sync below, because the sync is
            // what makes them equal. docs/LunaP.md §80.1.
            bool echoed = string.Equals(_search?.Text ?? "", SearchText, StringComparison.Ordinal);

            SetCurrentValue(SearchTextProperty, _search?.Text ?? "");

            if (echoed) return;

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

        /// <summary>Puts the keyboard in the search box.</summary>
        public void FocusSearch() => _search?.Focus();

        // The match every caller wants: case-insensitive, and an empty search matches everything.
        /// <summary>The match this bar means by filtering: case-insensitive substring, with an empty search matching everything.</summary>
        /// <param name="search">What was typed. Null or empty matches everything.</param>
        /// <param name="candidate">The text to test. Null matches nothing unless the search is empty.</param>
        /// <returns>True if the candidate should be shown. Public so a caller filters its own list the same way the bar would.</returns>
        public static bool Matches(string? search, string? candidate) =>
            string.IsNullOrWhiteSpace(search)
            || (candidate ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
