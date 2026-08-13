using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Threading;

namespace EmuSen.LunaP.Controls
{
    // The non-generic half, which exists so the theme can name it - see docs/LunaP.md §27.
    //
    // A generic control cannot carry a XAML style selector: `luna|LunaTable` has no way to say
    // which T, and `LunaList<T>` sidesteps the whole question by borrowing ListBox's theme and
    // having no template of its own. A table cannot do that, because the one thing it adds to a
    // list is a HEADER ROW, and that has to come from somewhere.
    //
    // So the parts and the look live on this class, which XAML can select, and the models live on
    // the generic one below. An Avalonia style selector matches subclasses, so a style written for
    // `luna|LunaTable` reaches every `LunaTable<T>` there will ever be.
    /// <summary>The non-generic base of LunaTable&lt;T&gt;, which exists so that a theme can name the type.</summary>
    public abstract class LunaTable : TemplatedControl
    {
        // The scrollbar is the reason this is worth a note. Fluent's ScrollViewer overlays its
        // scrollbar rather than taking layout space from the content, so the rows keep the full
        // width when one appears and the header above them stays lined up. If that ever changes,
        // the symptom is a header that drifts right of its cells by about seventeen pixels the
        // moment a table gets long enough to scroll.
        /// <summary>The header row from the template, or null before the template has been applied.</summary>
        protected Grid? HeaderGrid;
        /// <summary>The row list from the template, or null before the template has been applied.</summary>
        protected ListBox? Rows;

        // A Table, not a DataGrid, and the distinction is a promise rather than a label: UIA's
        // DataGrid and Table types come with IGridProvider and ITableProvider, which let a reader
        // ask for "row 4, column 2" and navigate a grid as a grid. This control implements neither
        // - it is a list of rows that happen to be laid out in columns - so claiming the type
        // would advertise navigation that is not there. What it does instead is give every ROW a
        // name built from its own cells, "name: Site, type: text, pg: 1", which is the useful
        // half of a table for a reader and is honestly deliverable. §27.3.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            HeaderGrid = e.NameScope.Find<Grid>("PART_Header");
            Rows = e.NameScope.Find<ListBox>("PART_Rows");
            OnPartsAttached();
        }

        /// <summary>Called once the template has been applied and HeaderGrid and Rows are available, which is where a subclass replays anything configured before that.</summary>
        protected abstract void OnPartsAttached();
    }

    // A list with columns - see docs/LunaP.md §27.
    //
    // The evidence for this is one site and it is worth stating exactly, because §21's rule is
    // that one hand-roll is a hazard note rather than a roadmap entry. The site is not in a
    // consumer of this toolkit yet: it is `bima/viewer.py:460`, in the Python application BIMA-C
    // is porting, and it reads
    //
    //     self.tree.setHeaderLabels(["name", "type", "pg"])
    //     ...
    //     QTreeWidgetItem(self.tree, [f.name, f.type, str(f.page + 1)])
    //
    // Every row added at the top level: a three-column list in a tree widget, with the tree part
    // unused. That shape is the whole specification, and it is why this control is FLAT and has no
    // expander. A hierarchical view is a different control and §27.4 says so rather than leaving
    // the absence to be discovered.
    //
    // TAKES PROJECTIONS, NOT AN INTERFACE, exactly as LunaList<T> does and for the same §1 reason:
    // a column is a header and a Func<T, string>, so a caller's own model needs no attribute, no
    // base class and no knowledge that LunaP exists.
    /// <summary>A flat list with columns, where each column is a header and a projection from the model.</summary>
    public class LunaTable<T> : LunaTable where T : class
    {
        private readonly List<ColumnSpec> _columns = new();
        private readonly List<Head> _heads = new();
        private readonly Suppressor _filling = new();

        // _items is the order the caller gave to Refresh and never changes under a sort. _view is
        // what is on screen. They are the same list until a header is clicked, and keeping them
        // apart is what makes the third click - back to arrival order - possible at all.
        private IReadOnlyList<T> _items = Array.Empty<T>();
        private IReadOnlyList<T> _view = Array.Empty<T>();

        // Which column is sorted, and which way. -1 is the third state and the initial one.
        private int _sortColumn = -1;
        private bool _sortDescending;

        // A selection asked for before there was anywhere to put it. Null is a real value here -
        // "select nothing" - so the flag rather than the field says whether one is waiting.
        private T? _pending;
        private bool _hasPending;

        // What makes two items "the same item" across a refresh, so the selection survives one.
        // Defaults to reference identity, which is right for a cached model and wrong for rows
        // rebuilt from disk on every poll - the same default, and the same trap, as LunaList<T>.
        /// <summary>How a row is matched to a model across a Refresh, so a selection survives a rebuild.</summary>
        /// <remarks>
        /// Defaults to the item itself, which means REFERENCE IDENTITY for a class. Right for a cached
        /// model handed back unchanged; wrong for rows rebuilt on every poll, where every item is a new
        /// object, nothing matches, and the selection is lost each refresh. Give a stable key (an id, a
        /// path) when the models are rebuilt rather than reused.
        /// </remarks>
        public Func<T, object?> Key { get; set; } = item => item;

        // Raised only for a real user choice, never for the selection restored during a refresh.
        /// <summary>Raised when the user picks a row, with the model rather than the row. Not raised for a selection restored by Refresh.</summary>
        public event Action<T?>? Chose;

        // The DISPLAYED order, which is the arrival order until a header is clicked and the sorted
        // order afterwards. A caller reading this back to write a report gets what the user is
        // looking at rather than what was handed in, which is the only reading of "currently shown"
        // that stays true once the table can sort.
        /// <summary>The models currently shown, in the order they are displayed in.</summary>
        public IReadOnlyList<T> Models => _view;

        // The selected model. Unlike LunaList<T>, which puts STRINGS in its ListBox and has to map
        // an index back, this one puts the models in directly - so there is no index arithmetic
        // here at all. That difference is worth knowing if the two are ever merged: LunaList's
        // string projection is the older design, and this is what it would look like without it.
        /// <summary>The selected model, or null when nothing is selected.</summary>
        public T? Selected => Rows?.SelectedItem as T;

        // A column, in the order it will appear. `width` is a GridLength as XAML spells one -
        // "*", "2*", "120" - and defaults to an equal share.
        //
        // AUTO IS ACCEPTED AND MADE TO WORK, which takes a little machinery: an Auto column in the
        // header and an Auto column in each row size themselves independently, so left alone they
        // would all be different widths and nothing would line up. Every column is therefore put
        // in a shared size group and the root is a shared size scope, which is Avalonia's own
        // mechanism for exactly this - and see Define below, because half of that mechanism does
        // not work the way it reads and this control shipped for a while with it silently off.
        /// <summary>Adds a column. Call once per column, before or after the template is applied.</summary>
        /// <param name="header">The column heading.</param>
        /// <param name="text">Turns a model into this cell text. Called for every row on every Refresh, so it should be cheap and free of side effects.</param>
        /// <param name="width">An Avalonia column width - "*", "Auto", or a number of pixels. Headers and cells share a size group, so they stay aligned.</param>
                /// <returns>The same table, so columns can be chained.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="header"/> or <paramref name="text"/> is null.</exception>
        public LunaTable<T> Column(string header, Func<T, string> text, string width = "*") =>
            Column(new LunaColumn<T>(header, text) { Width = width });

        // The form that carries behaviour, and the LAST Column overload that will ever be added -
        // anything a column grows from here is an init-only property on LunaColumn<T>, which is
        // additive by construction.
        //
        // THE TERSE OVERLOAD ABOVE DELEGATES HERE ON PURPOSE. Two ways to declare a column is a
        // deliberate convenience (§27), but two ways to BUILD one would be a defect waiting to
        // happen: the day a column gains a fifth property, the form somebody forgot to update
        // silently produces a different column. There is one path to a ColumnSpec, and a test
        // asserts the two forms are indistinguishable.
        /// <summary>Adds a column described by a LunaColumn&lt;T&gt;, which is how a column carries a sort.</summary>
        /// <param name="column">The column. Its Header and Text are required; Width and Sort have defaults.</param>
        /// <returns>The same table, so columns can be chained.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="column"/> is null.</exception>
        public LunaTable<T> Column(LunaColumn<T> column)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));

            _columns.Add(new ColumnSpec(column.Header, column.Text, GridLength.Parse(column.Width), column.Sort));
            Rebuild();
            return this;
        }

        // Replaces the contents and puts the selection back, the same operation LunaList.Refresh
        // performs and for the same reason: "rebuild the list" and "keep the selection" are one
        // thing that only looks like two.
        /// <summary>Replaces every row, keeping the selection if Key still matches something.</summary>
        /// <param name="items">The new models, in display order. Safe to call before the control has a template.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="items"/> is null.</exception>
        public void Refresh(IEnumerable<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            _items = items.ToList();
            Show();
        }

        // Puts _view in the ListBox and the selection back on top of it. Refresh and a header click
        // are the same operation from here down: both replace what is displayed and both have to
        // leave the selection where the user put it.
        //
        // THE SORT IS RE-APPLIED, WHICH IS THE POINT. New rows arriving under an active sort land in
        // sorted order - a table that quietly reverted to arrival order on the next poll would be a
        // table whose sort lasted until the data moved, which for a polling window is about a second.
        private void Show()
        {
            object? wasSelected = Selected is { } previous ? Key(previous) : null;

            _view = Ordered();

            if (Rows is null) return;

            using (_filling.Suppress())
            {
                Rows.ItemsSource = _view;

                // Null when the previously selected row is gone, which is a real answer rather
                // than a failure to restore: selecting its neighbour would be a guess.
                Rows.SelectedItem = wasSelected is null
                    ? null
                    : _view.FirstOrDefault(item => Equals(Key(item), wasSelected));
            }
        }

        // ORDERBY AND NOT List<T>.Sort, and the difference is visible rather than academic.
        // List<T>.Sort is an unstable introsort: rows that compare equal come out in an arbitrary
        // order that changes between runs, so a table sorted by a column with ties reshuffles its
        // equal rows every time it is refreshed. LINQ's OrderBy is documented stable, so ties keep
        // the order the caller gave to Refresh.
        //
        // It also projects rather than reorders: _items stays in arrival order, so the third click
        // on a header has somewhere to return to. Sorting in place would make the unsorted state
        // unreachable, which is most of why §27 chose a three-state cycle at all.
        private IReadOnlyList<T> Ordered()
        {
            if (_sortColumn < 0 || _sortColumn >= _columns.Count) return _items;
            if (_columns[_sortColumn].Sort is not { } comparison) return _items;

            var comparer = Comparer<T>.Create(comparison);
            return _sortDescending
                ? _items.OrderByDescending(item => item, comparer).ToList()
                : _items.OrderBy(item => item, comparer).ToList();
        }

        // Selects by model. Does not raise Chose - a caller setting the selection knows what it set.
        //
        // HELD UNTIL THE TEMPLATE EXISTS, which is not a nicety. A window in this toolkit is built
        // in its constructor: the table is filled and a row is selected long before anything is
        // shown, and an early Select that returned quietly would leave the caller looking at a
        // table with nothing highlighted and no error to explain it. FilterBar.SetFacets holds
        // pending facets for exactly the same reason (§14.2).
        //
        // Found by looking at a render rather than by a test: the row simply was not highlighted.
        /// <summary>Selects a model without raising Chose.</summary>
        /// <param name="item">The model to select, matched by Key. Null clears the selection.</param>
        public void Select(T? item)
        {
            if (Rows is null)
            {
                _pending = item;
                _hasPending = true;
                return;
            }

            using (_filling.Suppress())
            {
                Rows.SelectedItem = item is null
                    ? null
                    : _view.FirstOrDefault(candidate => Equals(Key(candidate), Key(item)));
            }
        }

        protected override void OnPartsAttached()
        {
            if (Rows is null) return;

            Rows.SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(Selected);
            };

            Rebuild();

            // Items set before the template existed - a caller filling the table from its
            // constructor, which is the normal way a window is built in this toolkit.
            if (_items.Count > 0) Show();

            if (!_hasPending) return;

            _hasPending = false;
            Select(_pending);
            _pending = null;
        }

        private void Rebuild()
        {
            if (HeaderGrid is null || Rows is null || _columns.Count == 0) return;

            // One group name per column, unique to this table, so two tables on one page do not
            // silently size each other's columns.
            string scope = "LunaTable" + GetHashCode().ToString("X");

            Define(HeaderGrid, scope);
            HeaderGrid.Children.Clear();
            _heads.Clear();

            for (int i = 0; i < _columns.Count; i++)
            {
                Control cell = Heading(i);
                Grid.SetColumn(cell, i);
                HeaderGrid.Children.Add(cell);
            }

            Rows.ItemTemplate = new FuncDataTemplate<T>((item, _) => Row(item, scope), supportsRecycling: true);
            ShowSortState();
        }

        // A SORTABLE HEADING IS A BUTTON; AN UNSORTABLE ONE STAYS A TEXTBLOCK.
        //
        // The button is not for the look - it is styled flat, and the theme spends more lines taking
        // Fluent's chrome off it than putting anything on. It is there because a heading that only
        // responds to a click is a sort a keyboard user does not have, and §24 is the section about
        // exactly this class of miss. A Button brings focus, Tab, Space and Enter, an invoke peer and
        // a focus adorner, all of which would otherwise be hand-built on a TextBlock and half of
        // which would be forgotten.
        //
        // The converse matters as much: a column with no comparison is left a plain TextBlock rather
        // than made into a button that does nothing. An inert tab stop costs a keyboard user a press
        // and tells them nothing, which is worse than not being a stop at all.
        private Control Heading(int index)
        {
            ColumnSpec column = _columns[index];

            var label = new TextBlock
            {
                Text = column.Header,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (column.Sort is null)
            {
                _heads.Add(new Head(label, null));
                return label;
            }

            // Hidden rather than blank in the unsorted state, and never a neutral "sortable" mark.
            // Three states with a glyph in all three reads as three sorts; two glyphs and nothing
            // reads as what it is - two sorted states and off.
            //
            // Raw in the automation tree because a screen reader announcing "black up-pointing
            // triangle" after the column name is noise. The state is carried on the button's own
            // name instead, where a reader will actually meet it.
            var glyph = new TextBlock
            {
                FontWeight = Avalonia.Media.FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                IsVisible = false,
            };

            glyph.Classes.Add("sort");
            AutomationProperties.SetAccessibilityView(glyph, AccessibilityView.Raw);

            var button = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { label, glyph },
                },
            };

            button.Classes.Add("heading");

            int clicked = index;
            button.Click += (_, _) => Cycle(clicked);

            _heads.Add(new Head(button, glyph));
            return button;
        }

        // ASCENDING, DESCENDING, THEN BACK TO THE ORDER REFRESH WAS GIVEN.
        //
        // Two states is the commoner convention and this departs from it knowingly. The order a
        // caller hands to Refresh carries meaning in this toolkit far more often than in a database
        // front end - log order, file order, the order a scan found things in - and a two-state
        // cycle makes that order unreachable the moment somebody clicks a header. The cost is a
        // third click that will surprise somebody; the alternative is a table that can lose
        // information the caller deliberately put in it. docs/LunaP.md §27.
        private void Cycle(int index)
        {
            if (_sortColumn != index)
            {
                _sortColumn = index;
                _sortDescending = false;
            }
            else if (!_sortDescending)
            {
                _sortDescending = true;
            }
            else
            {
                _sortColumn = -1;
                _sortDescending = false;
            }

            Show();
            ShowSortState();
        }

        // UPDATES THE HEADINGS IN PLACE AND NEVER REBUILDS THEM, which is a keyboard requirement
        // rather than a performance one. A user who reached a heading with Tab and pressed Space is
        // focused on that button; replacing it with a new one drops focus to the top of the window
        // and leaves them nowhere, having just used the control exactly as intended.
        private void ShowSortState()
        {
            for (int i = 0; i < _heads.Count && i < _columns.Count; i++)
            {
                if (_heads[i].Glyph is not { } glyph) continue;

                bool sorted = i == _sortColumn;
                glyph.IsVisible = sorted;
                glyph.Text = sorted ? (_sortDescending ? "▼" : "▲") : string.Empty;

                AutomationProperties.SetName(
                    _heads[i].Cell,
                    sorted
                        ? $"{_columns[i].Header}, sorted {(_sortDescending ? "descending" : "ascending")}"
                        : $"{_columns[i].Header}, not sorted");
            }
        }

        private Control Row(T? item, string scope)
        {
            var grid = new Grid();
            Define(grid, scope);

            if (item is null) return grid;

            var spoken = new List<string>(_columns.Count);

            for (int i = 0; i < _columns.Count; i++)
            {
                string value = _columns[i].Text(item) ?? string.Empty;
                spoken.Add($"{_columns[i].Header}: {value}");

                var cell = new TextBlock
                {
                    Text = value,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }

            // WHAT A READER HEARS, and the reason it is built here rather than left to Avalonia.
            // A row of bare TextBlocks in a Grid announces as its concatenated text at best -
            // "Site text 1" - which is three values with nothing to say which column each came
            // from. Pairing every value with its header turns that into "name: Site, type: text,
            // pg: 1", which is the information a column layout is carrying visually. §27.3.
            AutomationProperties.SetName(grid, string.Join(", ", spoken));
            return grid;
        }

        // POPULATES THE GRID'S OWN COLLECTION, AND NEVER ASSIGNS A NEW ONE. Not a style preference:
        // swapping this back to `grid.ColumnDefinitions = new ColumnDefinitions { ... }` turns the
        // shared sizing off again, silently, with no error and no visible change to the code's
        // intent.
        //
        // Avalonia 12.1.0 registers a definition with its shared size scope when it is ADDED to the
        // collection a Grid already owns, and does not when a ready-made collection is ASSIGNED to
        // the Grid. An assigned definition keeps a SharedSizeGroup that reads back correctly and
        // shares nothing - so every column sizes alone while looking, from the outside and from any
        // test that compares group names, exactly like a column that is sharing.
        //
        // The symptom is small, which is why it shipped. Star and absolute columns resolve to the
        // same number in both grids without needing to share, so they line up anyway; only an Auto
        // column exposes it, drifting by the difference between the widest heading and the widest
        // cell. A bold "type" heading six pixels wider than "text" put every cell in that column six
        // pixels right of its own heading.
        //
        // Fixed upstream by AvaloniaUI/Avalonia#21848, "register assigned definition collections
        // with their shared size group", merged 2026-07-26 - after 12.1.0 was released on
        // 2026-07-09. Populating works on 12.1.0 as it stands, so this costs no version bump, and it
        // stays correct whenever the upstream fix does arrive. docs/LunaP.md §27.7 carries the
        // measurement, the reduction to two plain grids, and why the guard that watched this could
        // not have caught it.
        //
        // AND ONLY AUTO COLUMNS JOIN THE GROUP, WHICH IS THE OTHER HALF AND WAS LEARNED THE HARD
        // WAY. A shared size group makes a STAR column behave as Auto - measured at 360.0 outside a
        // scope against 36.0 inside one, on two otherwise identical grids - so grouping every column
        // fixed the alignment and stopped the table filling its own width. That is Avalonia #19114,
        // open, and #6455 before it.
        //
        // Sharing only the Auto columns is not a workaround for that; it is what was needed all
        // along. Absolute columns are identical in both grids by definition, and a star column
        // resolves from whatever the other columns leave over - so once the Auto columns agree, the
        // remainder agrees, and star lines up without being told to. §27.7's own measurement said as
        // much before the cause was known: pre-fix, the star and absolute columns were already at
        // delta 0.0 and only the Auto column was out. §27.10.
        private void Define(Grid grid, string scope)
        {
            grid.ColumnDefinitions.Clear();
            for (int i = 0; i < _columns.Count; i++)
            {
                GridLength width = _columns[i].Width;

                grid.ColumnDefinitions.Add(new ColumnDefinition(width)
                {
                    SharedSizeGroup = width.IsAuto ? scope + "_" + i : null,
                });
            }
        }

        private readonly record struct ColumnSpec(
            string Header, Func<T, string> Text, GridLength Width, Comparison<T>? Sort);

        // The heading control for a column, and its glyph when it has one. Held so that a sort can
        // update what is already on screen rather than building it again - see ShowSortState.
        private readonly record struct Head(Control Cell, TextBlock? Glyph);
    }
}
