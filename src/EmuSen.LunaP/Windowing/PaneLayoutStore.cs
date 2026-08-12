using System;
using System.Collections.Generic;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Windowing
{
    // Where a divider was left, and whether a panel was open - see docs/LunaP.md §26.6.
    public sealed class PaneLayout
    {
        // The fixed pane's size in device-independent pixels. Not a fraction of the window, and
        // §26.6 carries the argument: a fraction re-applied at a different window size moves a
        // divider the user put somewhere on purpose.
        public double Size { get; set; }

        // Only SidePanel writes this. A SplitPane has no closed state - both its panes are content
        // the window is made of, and a divider dragged to the edge is not the same thing as a
        // panel somebody turned off.
        public bool Collapsed { get; set; }
    }

    // One panes.json keyed by PaneKey, exactly as WindowPlacementStore is one windows.json keyed
    // by WindowKey - see docs/LunaP.md §8.1 for the original and §26.6 for why this is a second
    // file rather than a second field in the first one.
    //
    // Opt-in, on the same principle: a pane without a key is never remembered, so a toolkit that
    // suddenly started writing files on behalf of every window that happened to have a splitter in
    // it is not what a consumer gets by upgrading.
    public static class PaneLayoutStore
    {
        public const string FileName = "panes.json";

        // Read through LunaSettings.Store on every call rather than through a captured file
        // object: a host may replace the store after this type is first touched, and three test
        // fixtures in this repository do exactly that.
        private static Dictionary<string, PaneLayout> All() =>
            LunaSettings.Store.Load<Dictionary<string, PaneLayout>>(null, FileName) ?? new Dictionary<string, PaneLayout>();

        public static PaneLayout? Load(string key) =>
            All().TryGetValue(key, out PaneLayout? layout) ? layout : null;

        // READ, EDIT, WRITE, rather than a Save that takes a whole record - because one key has two
        // writers. A side panel's size belongs to the SplitPane around it and its openness belongs
        // to the panel, and they are one entry on purpose: "the explorer is 320 wide and currently
        // shut" is one fact about one panel. Two whole-record writers would mean whichever saved
        // last silently discarded the other's field, which is the classic lost update and would
        // present as a panel that forgets its width whenever you close it.
        public static void Update(string key, Action<PaneLayout> edit)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (edit is null) throw new ArgumentNullException(nameof(edit));

            Dictionary<string, PaneLayout> all = All();
            if (!all.TryGetValue(key, out PaneLayout? layout))
            {
                layout = new PaneLayout();
                all[key] = layout;
            }

            edit(layout);
            LunaSettings.Store.Save(null, FileName, all);
        }
    }
}
