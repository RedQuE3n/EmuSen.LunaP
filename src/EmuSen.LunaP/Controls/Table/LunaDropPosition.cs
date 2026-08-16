namespace EmuSen.LunaP.Controls
{
    // Where a dragged row would land - see docs/LunaP.md §71.
    //
    // The same four the reference has, and Inside is the one that earns the enum. Before and After
    // are a reorder; Inside is a REPARENT, which only means anything in a tree (§55) and means
    // something entirely different to the caller - "put this file in that folder" rather than "put
    // this row between those two". A boolean would have collapsed the two and forced every caller
    // with a tree to work the distinction out from indices.
    //
    // None is a real answer rather than an absence: it is what a drag over a row that refuses the
    // drop reports, and what the indicator draws nothing for.
    /// <summary>Where a dragged row would land relative to the row under the pointer.</summary>
    public enum LunaDropPosition
    {
        /// <summary>Nowhere - the drop is refused here.</summary>
        None,

        /// <summary>Above the target row, at the same level.</summary>
        Before,

        /// <summary>Below the target row, at the same level.</summary>
        After,

        /// <summary>Into the target row, which is a reparent rather than a reorder.</summary>
        Inside,
    }
}
