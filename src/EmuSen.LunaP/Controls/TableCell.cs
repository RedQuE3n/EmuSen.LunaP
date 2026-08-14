using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace EmuSen.LunaP.Controls
{
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
        // WHICH COLUMN THIS IS, CARRIED ON THE CELL rather than read back off Grid.GetColumn.
        // Once §55 puts an expander in front of a cell, the cell is no longer a direct child of the
        // row grid - it sits inside a panel that is - so its Grid.GetColumn is 0 whatever column it
        // belongs to. Storing the index makes every lookup independent of how deep the cell is.
        internal int Column { get; set; }

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
