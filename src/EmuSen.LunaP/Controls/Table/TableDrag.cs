using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Controls
{
    // DRAGGING A ROW - see docs/LunaP.md §71.
    //
    // POINTER EVENTS AND CAPTURE, NOT DragDrop.DoDragDrop, and the choice is worth knowing before
    // somebody "upgrades" it. The platform drag-and-drop stack moves data BETWEEN controls and
    // applications; this moves a row inside one table, which is a different problem with a
    // different answer. Capture gives the whole gesture to this control, needs no data object, no
    // format string and no drop target registration - and it runs under Avalonia.Headless, so
    // every guard in TableDragTests drives the real gesture rather than asserting about wiring.
    //
    // What it gives up is stated rather than discovered: a row cannot be dragged OUT of this
    // table into something else. §71.5.
    //
    // The handlers are registered in OnPartsAttached rather than here, because WHICH ROUTING
    // STRATEGY each one takes is a fact about the ListBox underneath rather than about the gesture
    // (§71.3), and it belongs beside the other registrations that had to make the same choice. This
    // file is what those handlers do once they fire. §74.3.
    public partial class LunaTable<T> where T : class
    {
        // ROWS THE USER CAN REORDER - see docs/LunaP.md §71.
        //
        // Off by default, like every other item in this arc (§26.13). On, a row can be dragged with
        // the pointer or moved with Alt+Up/Down, and where it lands is reported rather than applied.
        /// <summary>Whether rows can be dragged into a new order. Off by default.</summary>
        public bool CanReorderRows { get; set; }

        // WHAT THE TABLE DOES WITH A DROP: NOTHING, AND THAT IS THE DESIGN.
        //
        // This control does not own the order. `_items` is a copy of what Refresh was handed, so
        // reordering it here would move the rows on screen and leave the caller's own collection
        // untouched - until the next Refresh put them back, which for a polling window is about a
        // second. A reorder that survives is one the caller made, so the caller is told and does it.
        //
        // It is the rule §57.4 already settled for a check column, one feature along: the table
        // re-reads the model rather than trusting the gesture. A Toggle that declines leaves the tick
        // where it was; a RowDropped nobody handles leaves the rows where they were. Both say the
        // same thing - the model is the truth and the control is a view of it.
        /// <summary>Raised when rows have been dropped somewhere. Reorder your own collection and call Refresh.</summary>
        public event Action<LunaRowDrop<T>>? RowDropped;

        // The veto, and it is a Func rather than an event for the reason Children and Key are: an
        // event cannot answer a question. A tree that refuses a folder dropped into its own child, a
        // table with a pinned first row, a drop that would break a caller's invariant - all of them
        // need to say no BEFORE the indicator promises the user it will work.
        //
        // Null means everything is allowed, which is what a caller who turned reordering on already
        // said.
        /// <summary>Decides whether one drop is allowed, or null - the default - to allow every drop.</summary>
        public Func<LunaRowDrop<T>, bool>? CanDrop { get; set; }

        // The row under the pointer when the press landed, and the flag for "a gesture is running":
        // every handler below returns on its first line when this is null.
        private T? _dragging;

        // WHAT IS MOVING, DECIDED WHEN THE DRAG STARTS AND NOT WHEN IT ENDS. Pressing a row collapses
        // a multi-selection to that row, so reading the selection at drop time always answers "one" -
        // the group the user picked is gone by then. Captured on the way down (§71.3) and held.
        private IReadOnlyList<T> _draggingRows = Array.Empty<T>();

        private Control? _dropLine;

        private Point _pressed;

        private bool _dragStarted;

        // Four pixels before a press becomes a drag, which is what stops a click that wobbled from
        // reordering the table. The same reason a double-click has a slop radius.
        private const double DragSlop = 4;

        private void RowPressed(Avalonia.Input.PointerPressedEventArgs e)
        {
            if (!CanReorderRows || IsEditing) return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (RowUnder(e.Source as Visual) is not { } row) return;

            _pressed = e.GetPosition(this);
            _dragging = row;
            _draggingRows = Dragged(row);
            _dragStarted = false;
        }

        private void RowMoved(Avalonia.Input.PointerEventArgs e)
        {
            if (_dragging is null) return;

            Point at = e.GetPosition(this);

            if (!_dragStarted)
            {
                if (Math.Abs(at.Y - _pressed.Y) < DragSlop && Math.Abs(at.X - _pressed.X) < DragSlop) return;

                _dragStarted = true;
                e.Pointer.Capture(this);
            }

            ShowDropLine(DropAt(at));
        }

        private void RowReleased(Avalonia.Input.PointerEventArgs e)
        {
            if (_dragging is null) return;

            // WORKED OUT BEFORE THE STATE IS CLEARED, because DropAt reads _dragging - it is what
            // decides which rows are moving and what they may not be dropped onto. The first version
            // cleared first and every drop came back null, which looked exactly like the release
            // never arriving.
            LunaRowDrop<T>? drop = _dragStarted ? DropAt(e.GetPosition(this)) : null;

            _dragging = null;
            _draggingRows = Array.Empty<T>();
            _dragStarted = false;
            e.Pointer.Capture(null);
            ShowDropLine(null);

            // AND RAISED AFTER, so a handler that reorders and calls Refresh does not find a drag
            // still in progress. The same order CellValueChanged is raised in (§56.3): the control
            // finishes what it was doing before telling anybody about it.
            if (drop is { } landing) RowDropped?.Invoke(landing);
        }

        // Where the pointer currently is, in the terms a caller is told about. Null when there is no
        // legal drop there, which is what stops the indicator promising something the drop will
        // refuse - the indicator and the drop read the same answer rather than deciding separately.
        private LunaRowDrop<T>? DropAt(Point at)
        {
            if (_dragging is null) return null;

            IReadOnlyList<T> moving = _draggingRows;

            Control? container = ContainerAt(at);
            if (container is null)
            {
                // Past the last row, which is a real drop - "put it at the end" - reported with no
                // target rather than by inventing the last row as one.
                return Allowed(new LunaRowDrop<T>(moving, null, LunaDropPosition.After));
            }

            if (container.DataContext is not T target) return null;

            // A row cannot be dropped onto itself or onto anything else being dragged: it would
            // report a move to where it already is, and a caller acting on it would remove and
            // reinsert a row for no reason.
            if (moving.Any(row => Equals(KeyOf(row), KeyOf(target)))) return null;

            Point local = at - container.TranslatePoint(new Point(0, 0), this)!.Value;
            double height = container.Bounds.Height;
            if (height <= 0) return null;

            // THE MIDDLE THIRD IS "INSIDE" ONLY IN A TREE. In a flat table there is no such thing as
            // dropping into a row, so the row splits in half and every position is a reorder;
            // offering Inside where it cannot mean anything would be an indicator that promises a
            // reparent no caller can perform.
            LunaDropPosition position = _children is null
                ? local.Y < height / 2 ? LunaDropPosition.Before : LunaDropPosition.After
                : local.Y < height / 3 ? LunaDropPosition.Before
                : local.Y > height * 2 / 3 ? LunaDropPosition.After
                : LunaDropPosition.Inside;

            return Allowed(new LunaRowDrop<T>(moving, target, position));
        }

        private LunaRowDrop<T>? Allowed(LunaRowDrop<T> drop) =>
            CanDrop is null || CanDrop(drop) ? drop : null;

        // The whole selection when the dragged row is part of it, and just that row otherwise -
        // which is what dragging one row out of a selection of four has to mean, or a user would
        // move three rows they had stopped pointing at.
        private IReadOnlyList<T> Dragged(T row)
        {
            IReadOnlyList<T> selected = SelectedItems;
            return selected.Any(candidate => Equals(KeyOf(candidate), KeyOf(row)))
                ? selected
                : new[] { row };
        }

        private Control? ContainerAt(Point at)
        {
            if (Rows is null) return null;

            foreach (Control container in Rows.GetRealizedContainers())
            {
                if (container.TranslatePoint(new Point(0, 0), this) is not { } corner) continue;

                if (at.Y >= corner.Y && at.Y <= corner.Y + container.Bounds.Height) return container;
            }

            return null;
        }

        private T? RowUnder(Visual? source)
        {
            if (source is null) return null;

            foreach (Visual step in source.GetSelfAndVisualAncestors())
            {
                if (step is ListBoxItem { DataContext: T model }) return model;
            }

            return null;
        }

        // WHERE THE ROW WILL LAND, DRAWN. A reorder with no indicator is a gesture whose result the
        // user finds out about afterwards, which for a list of forty rows means undoing by hand.
        //
        // A sibling in the row's own grid, spanning every column, for the reason the frozen seam and
        // the grid rules are (§63.1, §56.2): no wrapper, nothing about the row's tree changes, and it
        // is removed again rather than kept hidden - one control per drag rather than one per row for
        // ever, which is Row()'s own argument about hidden columns.
        private void ShowDropLine(LunaRowDrop<T>? drop)
        {
            if (_dropLine?.Parent is Grid host) host.Children.Remove(_dropLine);
            _dropLine = null;

            if (drop is not { } landing || landing.Position == LunaDropPosition.None) return;
            if (Rows is null) return;

            T? target = landing.Target ?? (_view.Count > 0 ? _view[^1] : null);
            if (target is null) return;

            if (!TryGetRow(target, out Control? container) || container is null) return;
            if (RowGridIn(container) is not { } grid) return;

            var line = new Border { Height = 2, IsHitTestVisible = false };
            line.Classes.Add(landing.Position == LunaDropPosition.Inside ? "drop-into" : "drop-line");

            line.VerticalAlignment = landing.Position == LunaDropPosition.Before
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;

            if (landing.Position == LunaDropPosition.Inside)
            {
                line.VerticalAlignment = VerticalAlignment.Stretch;
                line.Height = double.NaN;
            }

            Grid.SetColumn(line, 0);
            Grid.SetColumnSpan(line, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.Children.Add(line);
            _dropLine = line;
        }

        // MOVING A ROW WITHOUT A POINTER, which §24 makes a requirement rather than a nicety: a
        // reorder a mouse can do and a keyboard cannot is a feature half the users of this toolkit do
        // not have. It is the same argument that made a sortable heading a Button and a column grip a
        // GridSplitter.
        //
        // Alt+Up/Down, because Up/Down alone move the selection and Ctrl+Up/Down is the scroll-without
        // -moving idiom. Alt+arrow is what an editor uses to move a line, which is the same gesture
        // for the same reason.
        //
        // Reports the same drop the pointer would, so a caller has one handler rather than two.
        private bool MoveRow(Avalonia.Input.KeyEventArgs e)
        {
            if (!CanReorderRows || IsEditing) return false;
            if (!e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt)) return false;
            if (e.Key is not (Avalonia.Input.Key.Up or Avalonia.Input.Key.Down)) return false;
            if (Selected is not { } row) return false;

            int from = IndexOf(row);
            bool up = e.Key == Avalonia.Input.Key.Up;
            int to = up ? from - 1 : from + 1;
            if (from < 0 || to < 0 || to >= _view.Count) return false;

            var drop = new LunaRowDrop<T>(
                new[] { row },
                _view[to],
                up ? LunaDropPosition.Before : LunaDropPosition.After);

            if (Allowed(drop) is not { } allowed) return false;

            RowDropped?.Invoke(allowed);
            e.Handled = true;
            return true;
        }
    }
}
