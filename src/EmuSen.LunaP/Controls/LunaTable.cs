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
    public abstract class LunaTable : TemplatedControl
    {
        // The scrollbar is the reason this is worth a note. Fluent's ScrollViewer overlays its
        // scrollbar rather than taking layout space from the content, so the rows keep the full
        // width when one appears and the header above them stays lined up. If that ever changes,
        // the symptom is a header that drifts right of its cells by about seventeen pixels the
        // moment a table gets long enough to scroll.
        protected Grid? HeaderGrid;
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
    public class LunaTable<T> : LunaTable where T : class
    {
        private readonly List<ColumnSpec> _columns = new();
        private readonly Suppressor _filling = new();
        private IReadOnlyList<T> _items = Array.Empty<T>();

        // A selection asked for before there was anywhere to put it. Null is a real value here -
        // "select nothing" - so the flag rather than the field says whether one is waiting.
        private T? _pending;
        private bool _hasPending;

        // What makes two items "the same item" across a refresh, so the selection survives one.
        // Defaults to reference identity, which is right for a cached model and wrong for rows
        // rebuilt from disk on every poll - the same default, and the same trap, as LunaList<T>.
        public Func<T, object?> Key { get; set; } = item => item;

        // Raised only for a real user choice, never for the selection restored during a refresh.
        public event Action<T?>? Chose;

        public IReadOnlyList<T> Models => _items;

        // The selected model. Unlike LunaList<T>, which puts STRINGS in its ListBox and has to map
        // an index back, this one puts the models in directly - so there is no index arithmetic
        // here at all. That difference is worth knowing if the two are ever merged: LunaList's
        // string projection is the older design, and this is what it would look like without it.
        public T? Selected => Rows?.SelectedItem as T;

        // A column, in the order it will appear. `width` is a GridLength as XAML spells one -
        // "*", "2*", "120" - and defaults to an equal share.
        //
        // AUTO IS ACCEPTED AND MADE TO WORK, which takes a little machinery: an Auto column in the
        // header and an Auto column in each row size themselves independently, so left alone they
        // would all be different widths and nothing would line up. Every column is therefore put
        // in a shared size group and the root is a shared size scope, which is Avalonia's own
        // mechanism for exactly this.
        public LunaTable<T> Column(string header, Func<T, string> text, string width = "*")
        {
            if (header is null) throw new ArgumentNullException(nameof(header));
            if (text is null) throw new ArgumentNullException(nameof(text));

            _columns.Add(new ColumnSpec(header, text, GridLength.Parse(width)));
            Rebuild();
            return this;
        }

        // Replaces the contents and puts the selection back, the same operation LunaList.Refresh
        // performs and for the same reason: "rebuild the list" and "keep the selection" are one
        // thing that only looks like two.
        public void Refresh(IEnumerable<T> items)
        {
            if (items is null) throw new ArgumentNullException(nameof(items));

            object? wasSelected = Selected is { } previous ? Key(previous) : null;
            _items = items.ToList();

            if (Rows is null) return;

            using (_filling.Suppress())
            {
                Rows.ItemsSource = _items;

                // Null when the previously selected row is gone, which is a real answer rather
                // than a failure to restore: selecting its neighbour would be a guess.
                Rows.SelectedItem = wasSelected is null
                    ? null
                    : _items.FirstOrDefault(item => Equals(Key(item), wasSelected));
            }
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
                    : _items.FirstOrDefault(candidate => Equals(Key(candidate), Key(item)));
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
            if (_items.Count > 0) Refresh(_items);

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

            HeaderGrid.ColumnDefinitions = Definitions(scope);
            HeaderGrid.Children.Clear();

            for (int i = 0; i < _columns.Count; i++)
            {
                var label = new TextBlock
                {
                    Text = _columns[i].Header,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                Grid.SetColumn(label, i);
                HeaderGrid.Children.Add(label);
            }

            Rows.ItemTemplate = new FuncDataTemplate<T>((item, _) => Row(item, scope), supportsRecycling: true);
        }

        private Control Row(T? item, string scope)
        {
            var grid = new Grid { ColumnDefinitions = Definitions(scope) };
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

        private ColumnDefinitions Definitions(string scope)
        {
            var definitions = new ColumnDefinitions();
            for (int i = 0; i < _columns.Count; i++)
            {
                definitions.Add(new ColumnDefinition(_columns[i].Width) { SharedSizeGroup = scope + "_" + i });
            }

            return definitions;
        }

        private readonly record struct ColumnSpec(string Header, Func<T, string> Text, GridLength Width);
    }
}
