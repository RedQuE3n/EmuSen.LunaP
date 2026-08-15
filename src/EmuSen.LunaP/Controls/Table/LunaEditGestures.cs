using System;

namespace EmuSen.LunaP.Controls
{
    // Which gestures open a cell editor in a LunaTable - see docs/LunaP.md §56.
    //
    // FLAGS, BECAUSE THE TWO ARE INDEPENDENT. A table can sensibly want F2 without double-click - a
    // grid where a double-click already means "open this row" - or double-click without F2, or
    // neither, driving editing entirely from the application's own menu through LunaTable.Edit. An
    // enum of named combinations would have to grow a member per pair; a flags enum does not.
    //
    // TreeDataGrid's BeginEditGestures carries seven values including Tap, TextInput and
    // WhenSelected. Those three are a different feature rather than a different spelling: they begin
    // an edit from a gesture that also means something else, which needs a rule for resolving the
    // collision. They are absent here and named in §56 rather than left to be discovered.
    /// <summary>Which gestures open a cell editor in a LunaTable&lt;T&gt;.</summary>
    [Flags]
    public enum LunaEditGestures
    {
        /// <summary>No gesture opens an editor; only a call to Edit does.</summary>
        None = 0,

        /// <summary>Double-clicking a cell opens it.</summary>
        DoubleTap = 1,

        /// <summary>F2 opens the first editable cell of the selected row.</summary>
        F2 = 2,

        /// <summary>Both, which is what the table has always done.</summary>
        Default = DoubleTap | F2,
    }
}
