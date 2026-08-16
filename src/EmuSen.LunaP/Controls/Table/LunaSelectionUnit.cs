namespace EmuSen.LunaP.Controls
{
    // WHAT a LunaTable selection is made of, where LunaSelectionMode says how many - see
    // docs/LunaP.md §67.
    //
    // The other half of the split LunaSelectionMode's own comment set up. TreeDataGrid spells this
    // as None / Row / Cell / Multiple in one enum, which cannot express single-cell and multi-cell
    // as different things because it has spent the same member on both questions. Two properties
    // multiply where one enum adds: Row and Cell against None, Single and Multiple is six
    // behaviours from five members, and a third unit would cost one member rather than three.
    //
    // Row is the default and is what the table has always done, so a caller who never names this
    // sees no change at all (§26.13).
    /// <summary>Whether a LunaTable&lt;T&gt; selects whole rows or individual cells.</summary>
    public enum LunaSelectionUnit
    {
        /// <summary>Selection is a row, which is the default and what the table has always done.</summary>
        Row,

        /// <summary>Selection is one cell of one row, moved with the arrow keys and extended with Shift.</summary>
        Cell,
    }
}
