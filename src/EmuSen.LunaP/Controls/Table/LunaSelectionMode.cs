namespace EmuSen.LunaP.Controls
{
    // How many rows of a LunaTable can be selected at once - see docs/LunaP.md §54.
    //
    // THREE VALUES AND NOT FOUR, which is where this deliberately differs from the control it is at
    // parity with. TreeDataGrid's SelectionMode is None / Row / Cell / Multiple, which folds two
    // independent questions into one enum: how MANY things are selected, and WHAT KIND of thing a
    // selection is. Those are orthogonal - single-cell and multi-cell are both sensible - and an
    // enum that conflates them cannot express one of them.
    //
    // So the count lives here and the kind lives beside it. Parity is in what a caller can do, not
    // in the shape of the type they name (§54.4).
    /// <summary>How many rows of a LunaTable&lt;T&gt; may be selected at once.</summary>
    public enum LunaSelectionMode
    {
        /// <summary>Rows cannot be selected at all.</summary>
        None,

        /// <summary>One row at a time, which is the default and what the table has always done.</summary>
        Single,

        /// <summary>Any number of rows, extended with Ctrl and Shift.</summary>
        Multiple,
    }
}
