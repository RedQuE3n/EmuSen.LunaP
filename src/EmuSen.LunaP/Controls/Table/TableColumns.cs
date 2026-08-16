using System;
using System.Collections.Generic;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;

namespace EmuSen.LunaP.Controls
{
    // THE COLUMNS, AND THE GRIDS THEY BECOME - see docs/LunaP.md §27, §27.10 for the shared size
    // groups, and §58 for the gutter.
    //
    // A COLUMN IS DECLARED ONCE AND RESOLVED HERE. LunaColumn<T> is what a caller writes and is a
    // declaration with no behaviour; ColumnSpec is what this control holds, with the width already
    // parsed. Everything else in the kit reads `_columns`, and this is the only file that writes it.
    //
    // TWO INDICES LIVE HERE AND MUST NOT BE CONFUSED - a COLUMN index, which is what a caller wrote,
    // and a GRID index, which is where that column sits once a gutter may be in front of it.
    // GridColumn is the single translation between them and its comment is where that argument
    // lives; what matters at this level is that every Grid.SetColumn and every ColumnDefinition
    // lookup in all fourteen files goes through it.
    //
    // THE HEADER AND EVERY ROW ARE SEPARATE GRIDS, which is why Define exists rather than one set of
    // ColumnDefinitions being shared, and why Resized has to write the header's dragged widths back
    // into the specs before the rows can be brought into line. Define's own comment carries the two
    // Avalonia defects behind the shared sizing - both silent, both measured, one fixed upstream
    // after 12.1.0 shipped.
    public partial class LunaTable<T> where T : class
    {
        private readonly List<ColumnSpec> _columns = new();

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

            _columns.Add(new ColumnSpec(
                column.Header, column.Text, GridLength.Parse(column.Width), column.Sort,
                column.Commit, column.Validate,
                column.MinWidth, column.MaxWidth, column.IsVisible,
                column.Kind, column.Checked, column.Toggle, column.Build,
                column.Alignment, column.VerticalAlignment));
            Rebuild();

            // A saved layout can only be matched once the columns it describes exist, and there is
            // no ordering rule that puts TableKey after them - `new LunaTable<T> { TableKey = "x" }`
            // followed by three Column calls is the shape an object initializer invites. Restore is
            // idempotent and refuses a layout whose column count does not match, so calling it after
            // every column costs two comparisons and removes the ordering trap entirely.
            Restore();
            return this;
        }

        // Width is resolved here rather than kept as the caller's string, because a GridLength is
        // what the Grid wants and parsing it once at declaration is what makes a bad width fail at
        // the call site instead of at layout. Commit and Validate ride along unresolved - they are
        // the caller's own delegates and there is nothing to resolve.
        private readonly record struct ColumnSpec(
            string Header,
            Func<T, string> Text,
            GridLength Width,
            Comparison<T>? Sort,
            Action<T, string>? Commit,
            Func<T, string, string?>? Validate,
            double? MinWidth,
            double? MaxWidth,
            bool IsVisible,
            LunaCellKind Kind,
            Func<T, bool>? Checked,
            Action<T, bool>? Toggle,
            Func<T, Control>? Build,
            HorizontalAlignment? Alignment,
            VerticalAlignment? VerticalAlignment)
        {
            // Matches LunaColumn<T>.IsEditable rather than restating it loosely: a Check column is
            // changed by being ticked and must not answer to the text editor, or F2 stops at it.
            public bool IsEditable => Kind == LunaCellKind.Text && Commit is not null;
        }

        // The last column with anything to its right, which is what decides where a vertical rule
        // and a resize grip stop. A hidden column is not something on the right.
        private int LastVisibleColumn
        {
            get
            {
                int last = -1;
                for (int i = 0; i < _columns.Count; i++) if (_columns[i].IsVisible) last = i;
                return last;
            }
        }

        // THE GUTTER DOWN THE LEFT - see docs/LunaP.md §58.
        //
        // Null, the default, means there is no gutter and a table's grids have exactly the columns
        // they always had. Nothing about a table without one pays for this existing (§26.13).
        //
        // TAKES THE DISPLAY INDEX AS WELL AS THE MODEL, and that second argument is the whole reason
        // this is a delegate rather than a bool. TreeDataGrid's row header is a row NUMBER and
        // nothing else - its cell is a string with no projection behind it - which serves a list of
        // records and serves this toolkit's actual subject badly. A memory viewer wants addresses
        // down the left and a disassembly wants them too, and both are on the model.
        //
        // So both are expressible: `(_, i) => (i + 1).ToString()` numbers the rows, and
        // `(row, _) => row.Address.ToString("X4")` labels them. The index is the DISPLAYED one,
        // counted down the view after sorting and flattening, which is the only number that matches
        // what the user is looking at and is not otherwise reachable from a caller's projection.
        /// <summary>What to show in the gutter down the left, given a row and its displayed position. Null - the default - means no gutter.</summary>
        /// <remarks>
        /// The index is the row's position in what is currently DISPLAYED, so it counts down the screen
        /// under a sort rather than following the order given to Refresh. Number the rows with
        /// <c>(_, i) =&gt; (i + 1).ToString()</c>, or label them from the model and ignore it.
        /// </remarks>
        public Func<T, int, string>? RowHeader
        {
            get => _rowHeader;
            set
            {
                _rowHeader = value;
                Rebuild();
                Show();
                Pin();
            }
        }

        private Func<T, int, string>? _rowHeader;

        /// <summary>The gutter's width, in Avalonia's own notation. "Auto" by default, which fits the widest label.</summary>
        public string RowHeaderWidth { get; set; } = "Auto";

        /// <summary>What sits above the gutter, in the header row. Empty by default, which is the spreadsheet's empty corner.</summary>
        public string RowHeaderCaption { get; set; } = string.Empty;

        // Where each row sits in the view, so the gutter can be told without a scan. Empty and
        // untouched when there is no gutter to feed. §58.
        private readonly Dictionary<object, int> _position = new();

        // Where a row sits in the view. Zero when there is no gutter, because nothing asks then, and
        // zero for a row the map has not seen - which is a row not currently displayed, and there is
        // no honest position to give it.
        private int PositionOf(T item) => _position.TryGetValue(KeyOf(item), out int at) ? at : 0;

        // ONE PLACE THAT KNOWS THE GUTTER SHIFTS EVERY COLUMN RIGHT BY ONE. Every Grid.SetColumn and
        // every ColumnDefinition lookup in this control goes through this rather than using a column
        // index directly, because the two indices are genuinely different things: a COLUMN index is
        // what a caller wrote and what a remembered layout, a sort and Edit(item, 2) are written in,
        // and a GRID index is where that column sits once a gutter may be in front of it. Conflating
        // them is how a gutter would silently move a saved layout onto the wrong columns.
        private int GridColumn(int column) => _rowHeader is null ? column : column + 1;

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

            // THE GUTTER GOES FIRST AND SHARES A SIZE GROUP LIKE ANY OTHER AUTO COLUMN, which is
            // what keeps the caption over its own labels. Auto is the default width because a gutter
            // exists to be exactly as wide as its widest label and no wider - and Auto is precisely
            // the case §27.10 records as needing the shared group, so leaving it out would put the
            // caption a few pixels off the numbers under it. §58.
            if (_rowHeader is not null)
            {
                GridLength gutter = GridLength.Parse(RowHeaderWidth);
                grid.ColumnDefinitions.Add(new ColumnDefinition(gutter)
                {
                    SharedSizeGroup = gutter.IsAuto ? scope + "_gutter" : null,
                });
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                ColumnSpec column = _columns[i];

                // A HIDDEN COLUMN IS A ZERO-WIDTH ONE THAT SHARES NOTHING, rather than a definition
                // left out. Leaving it out would shift every index after it, and the index is what
                // a remembered layout, a sort and Edit(item, column) are all written in terms of.
                // Pinned at zero on all three of width, min and max, because an Auto or star column
                // with a MinWidth would still claim space with nothing in it.
                if (!column.IsVisible)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0))
                    {
                        MinWidth = 0,
                        MaxWidth = 0,
                    });
                    continue;
                }

                GridLength width = column.Width;

                var definition = new ColumnDefinition(width)
                {
                    SharedSizeGroup = width.IsAuto ? scope + "_" + i : null,
                };

                // Left null, these stay the Grid's own 0 and infinity, which is what every column
                // did before §54 - so a column that names neither is untouched.
                if (column.MinWidth is { } min) definition.MinWidth = min;
                if (column.MaxWidth is { } max) definition.MaxWidth = max;

                grid.ColumnDefinitions.Add(definition);
            }
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

            // THE CORNER, and it is a plain label rather than a heading Button even when every other
            // column is sortable. There is nothing to sort by: the gutter's contents are positions or
            // addresses, and "sort by row number" is either the identity or a lie. A button that
            // takes focus and does nothing is worse for a keyboard user than not being a stop at all,
            // which is the same argument Heading makes for an unsortable column (§27.3).
            if (_rowHeader is not null)
            {
                var corner = new TextBlock
                {
                    Text = RowHeaderCaption,
                    FontWeight = Avalonia.Media.FontWeight.Bold,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };

                corner.Classes.Add("row-header");
                Grid.SetColumn(corner, 0);
                HeaderGrid.Children.Add(corner);
            }

            // The last column with anything on its right. A grip after it would have nothing to
            // give the space to, and a hidden column is not something on its right - so with the
            // final two columns hidden, the grip belongs after the last one you can see.
            int lastVisible = LastVisibleColumn;

            for (int i = 0; i < _columns.Count; i++)
            {
                // A hidden column contributes no heading and no grip. Its ColumnDefinition is still
                // there, pinned to zero, so every index after it is unmoved.
                if (!_columns[i].IsVisible) continue;

                Control cell = Heading(i);
                Grid.SetColumn(cell, GridColumn(i));
                HeaderGrid.Children.Add(cell);

                if (i < lastVisible) HeaderGrid.Children.Add(Grip(i));
            }

            AddFrozenEdge(HeaderGrid);

            Rows.ItemTemplate = new FuncDataTemplate<T>((item, _) => Row(item, scope), supportsRecycling: true);
            ShowSortState();
        }

        // A GRIDSPLITTER AND NOT A THUMB, for the same reason a heading is a Button: a column width
        // a mouse can change and a keyboard cannot is a feature half the users of this toolkit do
        // not have. GridSplitter handles arrow keys, takes focus and carries an accessible name;
        // Thumb is the lighter primitive and gives a drag and nothing else.
        //
        // It sits in the column it resizes, aligned right, four pixels wide, so it straddles the
        // boundary with its neighbour. §26.6 made the same choice for SplitPane's divider and §26.11
        // records what happens when one loses its name.
        private Control Grip(int index)
        {
            var grip = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                Background = Avalonia.Media.Brushes.Transparent,
            };

            grip.Classes.Add("grip");
            AutomationProperties.SetName(grip, $"Resize {_columns[index].Header}");

            // DragDelta rather than DragCompleted, so the rows follow the pointer instead of
            // snapping when it is released. The save is debounced underneath, so a drag is one
            // write and not one per frame.
            grip.DragDelta += (_, _) => Resized();
            grip.DragCompleted += (_, _) => Resized();

            Grid.SetColumn(grip, GridColumn(index));
            return grip;
        }

        // The splitter edits the HEADER's definitions, and this is what makes that reach the rows.
        //
        // MEASURED, NOT ASSUMED: a width set on the header alone does not propagate. Only Auto
        // columns share a size group (§27.10), so a star or absolute column's header definition and
        // its row definitions are unrelated objects - setting the header's column 0 to 150 left
        // every row at 404 and put the cells 253 pixels right of their headings. So the header is
        // read back into the column specs, which are the one source of truth, and every realized row
        // is brought into line from there.
        private void Resized()
        {
            if (HeaderGrid is null) return;

            // Reads DEFINITIONS BY GRID INDEX and writes SPECS BY COLUMN INDEX, which is the whole
            // reason GridColumn exists: with a gutter in front, definition 0 is the gutter and
            // column 0 is definition 1. Reading them off by one puts every dragged width onto its
            // left-hand neighbour and saves that to disk.
            for (int i = 0; i < _columns.Count; i++)
            {
                int at = GridColumn(i);
                if (at >= HeaderGrid.ColumnDefinitions.Count) break;

                GridLength width = HeaderGrid.ColumnDefinitions[at].Width;
                if (width != _columns[i].Width) _columns[i] = _columns[i] with { Width = width };
            }

            if (Rows is not null)
            {
                foreach (Control container in Rows.GetRealizedContainers()) Widen(container);
            }

            Remember();
        }

        // Brings one row container's columns into line with the specs. Cheap enough to call per
        // container per drag frame - a virtualized list realizes tens of rows, not thousands (§27.7).
        private void Widen(Control? container)
        {
            if (container is null || RowGridIn(container) is not { } row) return;

            for (int i = 0; i < _columns.Count; i++)
            {
                int at = GridColumn(i);
                if (at >= row.ColumnDefinitions.Count) break;

                if (row.ColumnDefinitions[at].Width != _columns[i].Width)
                {
                    row.ColumnDefinitions[at].Width = _columns[i].Width;
                }
            }
        }
    }
}
