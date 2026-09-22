using System.Collections.Generic;

namespace EmuSen.LunaP.Controls
{
    // What a SourceList is filled with - see docs/LunaP.md §88.3. Plain records, so a host builds its
    // sidebar from strings it already has and never from a type of this toolkit's vocabulary.
    //
    // Key is what selection is held by, and the only thing Chose reports: the text changes when a
    // count does, or when a host localises it, and a selection held by text would be lost on either.
    /// <summary>One selectable row of a SourceList: a stable key, the text shown, and an optional right-aligned badge such as a count.</summary>
    /// <param name="Key">What the row is known by: SelectedKey, Fill's selectedKey and Chose all use it. Must be unique across the whole list.</param>
    /// <param name="Text">What the row reads as, and its accessible name.</param>
    /// <param name="Badge">A short string drawn at the right-hand end, typically a count. Null or empty draws none.</param>
    public sealed record SourceListItem(string Key, string Text, string? Badge = null);

    // A heading and the rows under it. The heading is not selectable and the keyboard steps over it.
    /// <summary>A titled group of rows in a SourceList. The header is drawn small and in capitals, and cannot be selected.</summary>
    /// <param name="Header">The group's title, as written; it is drawn in capitals and a reader hears it as given.</param>
    /// <param name="Items">The rows in the group, in display order.</param>
    public sealed record SourceListGroup(string Header, IReadOnlyList<SourceListItem> Items);
}
