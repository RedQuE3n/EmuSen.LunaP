using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        /// <summary>Where a rejected edit says what is wrong, or null before the template has been applied.</summary>
        protected ErrorText? Message;

        // A Group, and §68.1 is the correction that says why it stops being one in LunaTable<T>.
        //
        // §27.3 refused the DataGrid type on the grounds that it "comes with IGridProvider and
        // ITableProvider", promising navigation this control did not have. Avalonia 12.1.0 has
        // NEITHER INTERFACE - enumerated, the whole provider list is Embedded, ExpandCollapse,
        // Invoke, RangeValue, Root, Scroll, SelectionItem, Selection, Toggle, Value - so no control
        // in this framework can advertise that navigation, and the reason does not hold here.
        //
        // This base keeps Group because it is what a table with no columns and no rows is. The
        // generic class overrides it once there is something to say.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            HeaderGrid = e.NameScope.Find<Grid>("PART_Header");
            Rows = e.NameScope.Find<ListBox>("PART_Rows");
            Message = e.NameScope.Find<ErrorText>("PART_Error");
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
    //
    // ONE TYPE ACROSS FOURTEEN FILES, split at §74 when the single file reached 3,801 lines - the
    // same move CssTheme made at 547 (§29.4), one order of magnitude up, and chosen the same way:
    // by what makes each file change. This one holds the DATA and the WIRING - the models, the view,
    // the key, and every framework override - and each sibling holds one feature with its own § over
    // it. `Controls/Table/` is the folder for the whole subject, satellites included, because
    // twenty-five table files loose in `Controls/` would bury the other eighteen in it.
    //
    // THE NAMESPACE DELIBERATELY DOES NOT FOLLOW THE FOLDER, for CssTheme's reason exactly:
    // `LunaTable<T>`, `LunaColumn<T>` and seven enums are public names a consumer has already
    // written a `using` for, and moving them to match a directory would be a breaking change bought
    // with nothing but tidiness.
    /// <summary>A flat list with columns, where each column is a header and a projection from the model.</summary>
    public partial class LunaTable<T> : LunaTable where T : class
    {
        // THIS FILE IS THE DATA AND THE WIRING, and each sibling is one feature. The division is not
        // arbitrary: what is here is what every other file needs and none of them owns - the models,
        // the view, the key that matches one to the other across a Refresh, and the four framework
        // overrides.
        //
        // THE OVERRIDES STAY TOGETHER BECAUSE THEY DISPATCH RATHER THAN DECIDE. OnKeyDown hands a key
        // to three different features in a fixed order and the ORDER is the content of it; every
        // handler OnPartsAttached registers had to choose a routing strategy against the ListBox
        // underneath, and those choices are a fact about the ListBox rather than about the feature
        // being wired. Filing either under one feature would put the argument somewhere that could
        // not see the other two. §74.3.
        //
        // "The selection is not the user's" - Refresh putting a row back, a cell unit moving the row
        // under the current cell, a mode switch clearing what was there. Every one of those assigns
        // Rows.SelectedItem, and without this each would raise Chose as though somebody had clicked.
        private readonly Suppressor _filling = new();

        // _items is the order the caller gave to Refresh and never changes under a sort. _view is
        // what is on screen. They are the same list until a header is clicked, and keeping them
        // apart is what makes the third click - back to arrival order - possible at all.
        private IReadOnlyList<T> _items = Array.Empty<T>();
        private IReadOnlyList<T> _view = Array.Empty<T>();

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

        // The DISPLAYED order, which is the arrival order until a header is clicked and the sorted
        // order afterwards. A caller reading this back to write a report gets what the user is
        // looking at rather than what was handed in, which is the only reading of "currently shown"
        // that stays true once the table can sort.
        /// <summary>The models currently shown, in the order they are displayed in.</summary>
        public IReadOnlyList<T> Models => _view;

        // Key can return null - a caller's projection is allowed to - and null cannot go in a
        // dictionary, so the model itself is the fallback. That degrades to reference identity for
        // exactly the rows whose key is missing rather than for the whole table.
        private object KeyOf(T item) => Key(item) ?? item;

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

            _view = Flatten();

            // WHERE EACH ROW SITS ON SCREEN, BUILT ONCE PER REBUILD rather than searched per row.
            // RowHeader is handed the displayed index, and a virtualising list asks for that index
            // once per realised container - so the obvious IndexOf(item) is an O(n) scan run per
            // visible row, which is fine at three rows and is a scan of ten thousand Key() calls per
            // screenful at ten thousand. First entry wins, for the same reason the selection lookup
            // takes the first match: a caller whose Key is not unique has one bug, not two.
            _position.Clear();
            if (_rowHeader is not null)
            {
                for (int i = 0; i < _view.Count; i++) _position.TryAdd(KeyOf(_view[i]), i);
            }

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

            DropCellsThatLeft();
        }

        protected override void OnPartsAttached()
        {
            if (Rows is null) return;

            Rows.SelectionChanged += (_, _) =>
            {
                if (!_filling.IsSuppressing) Chose?.Invoke(Selected);
            };

            // CLICKING A CELL SELECTS THAT CELL - see docs/LunaP.md §67.
            //
            // On the table and bubbling, rather than a handler per cell. A cell can be a TableCell, a
            // CheckBox or any control a caller returned from Build (§57), so there is no one type to
            // attach to - and a pointer that goes down on something INSIDE a template cell has to
            // count as a click on the cell, which walking up from the source is exactly how to decide.
            //
            // Never handled. A check cell's box still toggles, a template cell's own button still
            // fires, and a double-click still opens an editor: selecting is something that happens as
            // well as the click, not instead of it.
            AddHandler(
                Avalonia.Input.InputElement.PointerPressedEvent,
                (_, e) => ClickedCell(e),
                Avalonia.Interactivity.RoutingStrategies.Bubble);

            // THE DRAG STARTS ON THE TUNNEL, and there are two independent reasons, both measured.
            // Tunnelling runs root to target, so this control sees the press BEFORE the ListBox below
            // it does.
            //
            // ONE: the ListBox marks a press HANDLED when it selects the row - `pressed.Handled=True`
            // by the time the event reaches this control on the way back up - so a bubbling handler
            // never fires on the one gesture it exists for unless it opts into handled events.
            //
            // TWO, and this one no opt-in would fix: the ListBox also COLLAPSES a multi-selection to
            // the row that was pressed. Read on the way down the selection is still the four rows the
            // user picked; read on the way back up it is one, and dragging a group would silently
            // move only the row under the pointer. §71.3.
            AddHandler(
                Avalonia.Input.InputElement.PointerPressedEvent,
                (_, e) => RowPressed(e),
                Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Moved and released are TUNNELLING rather than bubbling, because once a drag has the
            // pointer captured the events arrive at this control directly and there is nothing below
            // to bubble from. Registered whatever CanReorderRows says, since it can be turned on
            // after the template exists - a "Reorder rows" menu item is the obvious caller - and the
            // handlers return on their first line when it is off.
            AddHandler(
                Avalonia.Input.InputElement.PointerMovedEvent,
                (_, e) => RowMoved(e),
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);

            AddHandler(
                Avalonia.Input.InputElement.PointerReleasedEvent,
                (_, e) => RowReleased(e),
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);

            Rows.ContainerPrepared += (_, e) =>
            {
                // A recycled container keeps the ColumnDefinitions it was built with, so a row
                // scrolling back into view after a resize would come back at the old widths. This is
                // the hook that catches it - and it fires for every container, so a row realized for
                // the first time after a drag is covered by the same line.
                Widen(e.Container);

                // AND THE ROW'S NAME, which has to happen here rather than in Row(): the grid is
                // built before anything parents it, so at that moment there is no container to put
                // the name on. This fires for every container including recycled ones, so a row
                // scrolled back into view is renamed for whatever model it now holds. §50.5.
                if (e.Container.DataContext is T model)
                {
                    AutomationProperties.SetName(e.Container, Spoken(model));
                    RowPrepared?.Invoke(model, e.Container);
                }
            };

            Rows.ContainerClearing += (_, e) =>
            {
                if (e.Container.DataContext is T model) RowClearing?.Invoke(model, e.Container);
            };

            // THE HEADER FOLLOWS THE ROWS SIDEWAYS - see docs/LunaP.md §59.
            //
            // The rows scroll inside the ListBox's own ScrollViewer; the header is the one part of
            // this control outside it, so it is the one part that has to be moved by hand. A render
            // transform rather than a margin or a second ScrollViewer: it is the cheapest thing that
            // moves a laid-out subtree, it costs no measure pass, and it cannot disturb the shared
            // size groups the header and rows use to line up at all (§27.10).
            //
            // ScrollChanged is a ROUTED event, so this catches the viewer inside the ListBox's
            // template without having to find it - which cannot be done here anyway, because the
            // ListBox has not applied its own template at this point.
            Rows.AddHandler(ScrollViewer.ScrollChangedEvent, (_, e) =>
            {
                // The event is a nudge and nothing more - what scrolled and by how much is read in
                // Pin, from the one viewer this control owns. Taking either from the event is what
                // §64.3 records going wrong.
                Pin();
            });

            // AND AGAIN AFTER EVERY LAYOUT, which is not belt-and-braces. A clip is computed from a
            // child's arranged Bounds, and the two moments that produce new children with no bounds
            // yet are exactly the two this control does not otherwise hear about: a row realised by
            // the virtualising panel while scrolling vertically, and a column resized under the
            // header. LayoutUpdated is the signal that everything has bounds again.
            //
            // Cheap when it does not apply: Pin returns on its first line for a table with no frozen
            // columns, which is every table that has not asked for them.
            //
            // AND THE CELL BOXES, for exactly the same reason and it took the same wrong turn first.
            // A row realised while scrolling, and a row rebuilt by Refresh, both arrive through
            // ContainerPrepared - where there is no row grid to put a box in yet. This is the hook
            // where everything exists. MarkCells early-outs just as sharply. §67.5.
            //
            // AND THE COLUMNS, FIRST OF THE THREE, because the other two are about children that
            // already exist and this is the one that decides which children there are. Pin computes a
            // clip from a child's arranged Bounds and skips a child that has none yet (§64.1), so a
            // cell added here is pinned on the next pass rather than on this one - which is the same
            // one-pass lag a row realized while scrolling has always had. FillColumns returns on its
            // first line for every table that has not asked for it. §72.
            LayoutUpdated += (_, _) =>
            {
                FillColumns();
                Pin();
                MarkCells();
            };

            // Bubbling, so it catches a heading, a cell editor and a caller's own control inside a
            // template column without any of them being registered. Only a flag is set: the
            // correction needs the ScrollViewer to have finished its own BringIntoView, and that has
            // not happened yet when this fires.
            AddHandler(
                GotFocusEvent,
                (_, _) => _focusMoved = true,
                Avalonia.Interactivity.RoutingStrategies.Bubble);

            ApplySelectionMode();
            Rebuild();
            Restore();

            // Items set before the template existed - a caller filling the table from its
            // constructor, which is the normal way a window is built in this toolkit.
            if (_items.Count > 0) Show();

            // A cell selected before there was a template. The cells themselves were kept all along -
            // they are held by key rather than by visual - but the row under the current one was
            // never put on the ListBox, because there was no ListBox. Without this, a table
            // configured from a constructor comes up with a boxed cell and Selected reading null.
            Sync();

            if (!_hasPending) return;

            _hasPending = false;
            Select(_pending);
            _pending = null;
        }

        // F2 EDITS THE SELECTED ROW, because double-click is a mouse gesture and a table whose cells
        // can only be opened with a pointer is a table half this toolkit's users cannot edit - the
        // same argument that made a sortable heading a Button and a column grip a GridSplitter
        // rather than a Thumb (§24).
        //
        // WHICH COLUMN DEPENDS ON THE UNIT, AND UNTIL §67 THERE WAS ONLY ONE ANSWER. This comment
        // used to read "the FIRST editable column, and not the selected one, because this control has
        // no concept of a focused cell" - which was true, and is the thing cell selection changed. In
        // a Cell unit there is a focused cell and F2 opens it; in a Row unit there is still no such
        // thing and the first editable column is still the only honest guess.
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled || IsEditing) return;

            // Before the cell arrows, because Alt+Up is a row move and plain Up is not - and a cell
            // unit would otherwise eat the key on its way past.
            if (MoveRow(e)) return;

            if (_selectionUnit == LunaSelectionUnit.Cell && MoveCell(e)) return;

            if (e.Key != Avalonia.Input.Key.F2) return;
            if (!EditGestures.HasFlag(LunaEditGestures.F2)) return;

            if (_selectionUnit == LunaSelectionUnit.Cell)
            {
                // No IsEditable test here on purpose: Edit already refuses a column with no Commit,
                // and one rule with two owners is one rule that will disagree with itself. A
                // duplicate test here also cannot be sabotaged into failing, which is how it was
                // found - the check was removed and every guard stayed green.
                if (SelectedCell is not { } at) return;

                Edit(at.Row, at.Column);
                e.Handled = IsEditing;
                return;
            }

            if (Selected is not { } item) return;

            int column = _columns.FindIndex(c => c.IsEditable);
            if (column < 0) return;

            Edit(item, column);
            e.Handled = IsEditing;
        }

    }
}
