using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
    // WHICH COLUMN A CELL IS, ON ANY CONTROL AT ALL - see docs/LunaP.md §57.
    //
    // This was an instance property on TableCell until a cell stopped always being one. A Check
    // column's cell is a stock Avalonia CheckBox and a Template column's cell is whatever the caller
    // built, and neither can be made to carry a field - so the index moves to an attached property,
    // which is Avalonia's own answer to "annotate a control you did not write".
    //
    // NOT READ BACK OFF Grid.GetColumn, which was true before there were cell kinds and is still the
    // reason this exists at all: an expander puts a panel between the cell and the row grid (§55), so
    // the cell's own Grid.Column is 0 whatever column it belongs to. Storing the index makes every
    // lookup independent of how deep the cell sits.
    //
    // THE DEFAULT IS -1 AND NOT 0, so "no column was set" is distinguishable from "column 0". The
    // lookup walks every descendant of a row, and a template column's cell is a caller's own control
    // with a caller's own children under it; at a default of 0 the first of those children answers as
    // column 0 and Edit opens an editor on somebody's Button. Sabotaged by changing it, which turns
    // the lookup and Edit's refusal red together.
    internal static class TableCells
    {
        public static readonly AttachedProperty<int> ColumnProperty =
            AvaloniaProperty.RegisterAttached<TableCell, Control, int>("Column", -1);

        public static void SetColumn(Control cell, int column) => cell.SetValue(ColumnProperty, column);

        public static int GetColumn(Control cell) => cell.GetValue(ColumnProperty);

        // WHOSE ROW CONTENT A DIRECT CHILD OF THE ROW GRID IS - see docs/LunaP.md §72.
        //
        // A SECOND MARKER AND NOT THE ONE ABOVE, and they answer two different questions that the
        // expander column is enough to separate. Column says "this control IS column n's cell", set
        // on the cell however deep it sits, and it is what Cell() and Edit look for. Owner says
        // "this direct child of the row grid EXISTS BECAUSE OF column n, and goes away with it" -
        // which is the wrapper when there is one, and is also the vertical rule, which is not a cell
        // at all and has no business answering to Cell().
        //
        // Conflating them was tried and does not work in either direction: putting Column on the
        // wrapper makes Cell() return the wrapper, so TextCellOf casts it to TableCell, gets null,
        // and Edit silently refuses the expander column. Reading Owner off Grid.GetColumn instead
        // cannot tell a cell from a selection box or an open editor sitting in the same column, and
        // a fill that removed those would take the caret out of a cell the user was typing in.
        //
        // -1 for the same reason: everything else in a row grid - the gutter, the frozen edge, a
        // selection box, an editor - must read "not mine", and at a default of 0 all four would be
        // torn out the moment column 0 scrolled away.
        public static readonly AttachedProperty<int> OwnerProperty =
            AvaloniaProperty.RegisterAttached<TableCell, Control, int>("Owner", -1);

        public static void SetOwner(Control child, int column) => child.SetValue(OwnerProperty, column);

        public static int GetOwner(Control child) => child.GetValue(OwnerProperty);
    }

    // One cell of a LunaTable row - see docs/LunaP.md §50.6.
    //
    // A PLAIN TextBlock UNTIL THE TABLE COULD BE EDITED, and this type exists for exactly one
    // reason: a reader has to be able to read and change an editable cell without a pointer.
    // Avalonia's TextBlockAutomationPeer offers no IValueProvider - measured, not assumed - so a
    // screen reader could hear a cell's text and had no way to set it, which is the difference
    // between a table it can inspect and a table it can use.
    //
    // INTERNAL, because it is how the table draws a cell rather than something a consumer builds.
    // Making it public would put a type in the API surface whose only purpose is to carry two
    // delegates the table already owns, and §32's rule is that everything public is something a
    // consumer can see and cannot patch.
    //
    // The delegates are set by LunaTable when it builds the row, and a null Write is what "read-only"
    // means here - the same convention as LunaColumn.Commit, so there is one idea of "editable"
    // rather than two that can disagree.
    internal sealed class TableCell : TextBlock
    {
        internal Func<string>? Read { get; set; }

        internal Action<string>? Write { get; set; }

        protected override AutomationPeer OnCreateAutomationPeer() => new TableCellPeer(this);
    }

    // What a reader gets on an editable cell. IsReadOnly is the honest half: a column with no Commit
    // still answers as a value, it just refuses to take a new one, which is what IValueProvider is
    // for rather than hiding the provider entirely.
    internal sealed class TableCellPeer : ControlAutomationPeer, IValueProvider
    {
        private readonly TableCell _cell;

        public TableCellPeer(TableCell cell)
            : base(cell) => _cell = cell;

        public bool IsReadOnly => _cell.Write is null;

        // Read through the projection rather than off the TextBlock, so a cell whose editor is open
        // -            and whose TextBlock is therefore hidden and stale - still answers with the model's
        // current value.
        public string? Value => _cell.Read?.Invoke() ?? _cell.Text;

        public void SetValue(string? value) => _cell.Write?.Invoke(value ?? string.Empty);

        // A cell is a piece of text a reader can land on, so it stays in the control view rather
        // than being hidden the way MeterRow's inner progress bar is (§24.2) - there, the row spoke
        // for the bar; here, the cell is the only thing that knows this column's value.
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
    }
}
