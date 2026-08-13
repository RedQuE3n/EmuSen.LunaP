using System;
using System.Collections.Generic;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Controls
{
    // What a user did to a table's columns, and which one they left it sorted by - see
    // docs/LunaP.md §27.11.
    /// <summary>The column widths a user dragged and the sort they left a table in, as plain data.</summary>
    public sealed class TableLayout
    {
        // Widths as Avalonia writes them - "*", "2*", "Auto", "150" - rather than as numbers,
        // because a dragged column and an untouched one are different KINDS of width and not two
        // values of one. Saving 404 for a star column would pin it at 404 on the next launch in a
        // window the user has since resized, which is the same class of mistake §26.6 records for
        // saving a divider as a fraction.
        /// <summary>Each column's width, in Avalonia's own notation, in column order.</summary>
        public List<string> Widths { get; set; } = new();

        // The HEADER, not the index. A caller who inserts a column at the front between two runs
        // would otherwise find the table sorted by its neighbour, silently and with the arrow
        // pointing confidently at the wrong column.
        /// <summary>The heading of the sorted column, or null when the table was left unsorted.</summary>
        public string? SortedBy { get; set; }

        /// <summary>Whether the sort was descending.</summary>
        public bool Descending { get; set; }
    }

    // One tables.json keyed by TableKey, exactly as PaneLayoutStore is one panes.json keyed by
    // PaneKey and WindowPlacementStore is one windows.json keyed by WindowKey (§8.1, §26.6).
    //
    // A third file rather than a third field in either of the others, for the reason §26.6 gave for
    // the second: these are different things with different lifetimes, and a consumer deleting one
    // to reset their window layout should not lose the columns they spent a minute arranging.
    //
    // Opt-in on the same principle as both: a table with no key is never written down, so nothing
    // starts writing files on behalf of every table in an application that upgrades.
    /// <summary>Reads and writes remembered table layout, keyed by a table's opt-in key.</summary>
    public static class TableLayoutStore
    {
        /// <summary>The file table layouts are kept in.</summary>
        public const string FileName = "tables.json";

        // Read through LunaSettings.Store on every call rather than through a captured file object,
        // because a host may replace the store after this type is first touched - the same reason
        // PaneLayoutStore gives, and the same three test fixtures do it.
        private static Dictionary<string, TableLayout> All() =>
            LunaSettings.Store.Load<Dictionary<string, TableLayout>>(null, FileName) ?? new Dictionary<string, TableLayout>();

        /// <summary>Reads back the layout saved under a key.</summary>
        /// <param name="key">The TableKey the layout was saved under.</param>
        /// <returns>The saved layout, or null if nothing was ever saved for that key.</returns>
        public static TableLayout? Load(string key) =>
            All().TryGetValue(key, out TableLayout? layout) ? layout : null;

        // Read, edit, write, following PaneLayoutStore rather than taking a whole record - not
        // because this key has two writers today, but because it is one file shared by every table
        // in an application and a whole-record write would need the same care anyway.
        /// <summary>Reads, edits and writes back the layout for one key, leaving every other key alone.</summary>
        /// <param name="key">The key to update. A layout is created for it if none exists.</param>
        /// <param name="edit">Mutates the layout in place. Runs before the file is written.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="key"/> or <paramref name="edit"/> is null.</exception>
        public static void Update(string key, Action<TableLayout> edit)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (edit is null) throw new ArgumentNullException(nameof(edit));

            Dictionary<string, TableLayout> all = All();
            if (!all.TryGetValue(key, out TableLayout? layout))
            {
                layout = new TableLayout();
                all[key] = layout;
            }

            edit(layout);
            LunaSettings.Store.Save(null, FileName, all);
        }
    }
}
