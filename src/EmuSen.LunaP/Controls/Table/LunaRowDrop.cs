using System.Collections.Generic;

namespace EmuSen.LunaP.Controls
{
    // What one drop is, as a LunaTable describes it - see docs/LunaP.md §71.
    //
    // MODELS, LIKE EVERY OTHER SEAM ON THIS CONTROL. The reference reports a drop as a row visual and
    // a DragEventArgs; a caller then has to get from a TreeDataGridRow back to their own object. Here
    // the answer is already the caller's own type, for the reason LunaCell<T> is (§67.2) and Selected
    // has been since §27.6: the model is the one identity that survives a sort, a refresh and a
    // rebuild, and it is the only thing the caller can act on.
    //
    // A LIST RATHER THAN ONE ROW, because SelectionMode.Multiple exists and dragging a selection of
    // four rows is the obvious thing to do with one. In display order, which is what SelectedItems
    // promises and what a caller inserting them somewhere else needs (§54).
    //
    // Target is nullable because a drop past the last row has nothing under it. That is a real drop -
    // "put it at the end" - and reporting it as no target with an After position is more honest than
    // inventing the last row as a target.
    /// <summary>One drop on a LunaTable&lt;T&gt;: what was dragged, where it landed, and how.</summary>
    /// <typeparam name="T">The row model.</typeparam>
    /// <param name="Rows">The models being dragged, in display order.</param>
    /// <param name="Target">The model under the pointer, or null past the end of the table.</param>
    /// <param name="Position">Where the drop would land relative to the target.</param>
    public readonly record struct LunaRowDrop<T>(
        IReadOnlyList<T> Rows,
        T? Target,
        LunaDropPosition Position)
        where T : class;
}
