using System;

namespace EmuSen.LunaP.Controls
{
    // Which rules a LunaTable draws between its cells - see docs/LunaP.md §56.
    //
    // FLAGS RATHER THAN FOUR NAMED COMBINATIONS, for the same reason LunaEditGestures is: horizontal
    // and vertical are independent choices, and All is their union rather than a third thing. This
    // is one of the few places where matching TreeDataGrid's shape exactly was also the right shape -
    // its GridLinesVisibility is None/Horizontal/Vertical/All and means the same four things.
    /// <summary>Which rules a LunaTable&lt;T&gt; draws between its cells.</summary>
    [Flags]
    public enum LunaGridLines
    {
        /// <summary>No rules, which is what the table has always drawn.</summary>
        None = 0,

        /// <summary>A rule under each row.</summary>
        Horizontal = 1,

        /// <summary>A rule to the right of each column.</summary>
        Vertical = 2,

        /// <summary>Rules in both directions, so every cell is boxed.</summary>
        All = Horizontal | Vertical,
    }
}
