namespace EmuSen.LunaP.Controls
{
    // Where a cell is, as a LunaTable talks about cells - see docs/LunaP.md §67.
    //
    // A MODEL AND A COLUMN INDEX, and the model half is what makes this LunaP's rather than a copy
    // of the reference's. TreeDataGrid's CellIndex is (int ColumnIndex, IndexPath RowIndex): both
    // halves are positions, and a position is only meaningful until something is sorted, filtered,
    // expanded or refreshed. Every other selection API on this control is already in models -
    // Selected is a T, SelectedItems is a list of T, Edit and TryGetCell take the model - because
    // that is the one identity that survives a rebuild (§27.6, §55.4).
    //
    // The column stays an index because a column has no other identity here: LunaColumn<T> is a
    // declaration, not a thing a caller holds a reference to, and headers are not unique. It is the
    // same index Edit, TryGetCell and FrozenColumns all take, counted in the order columns were
    // added, so a hidden column keeps its place (§54.3, §58.2).
    //
    // A record struct, so two cells naming the same coordinate are equal without a caller writing
    // anything - which is what makes a HashSet of these work, and what makes an assertion in a test
    // read as one comparison instead of two.
    /// <summary>One cell of a LunaTable&lt;T&gt;: the row's model, and the column's index.</summary>
    /// <param name="Row">The row's model.</param>
    /// <param name="Column">The column index, in the order the columns were added.</param>
    public readonly record struct LunaCell<T>(T Row, int Column)
        where T : class;
}
