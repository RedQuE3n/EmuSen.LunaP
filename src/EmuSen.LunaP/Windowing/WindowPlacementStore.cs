using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Windowing
{
    // Where a remembered window was, as plain data - see docs/LunaP.md §8.1.
    /// <summary>Where a remembered window was, as plain data.</summary>
    public sealed class WindowPlacement
    {
        /// <summary>Left edge in screen pixels.</summary>
        public int X { get; set; }
        /// <summary>Top edge in screen pixels.</summary>
        public int Y { get; set; }
        /// <summary>Window width when it was saved.</summary>
        public double Width { get; set; }
        /// <summary>Window height when it was saved.</summary>
        public double Height { get; set; }
        /// <summary>Whether the window was maximized, in which case the size above is the restored one.</summary>
        public bool Maximized { get; set; }
    }

    // One windows.json keyed by ToolWindow.WindowKey; opt-in, so a window without a key is never remembered.
    /// <summary>Reads and writes remembered window placement, keyed by a window's opt-in key.</summary>
    public static class WindowPlacementStore
    {
        /// <summary>The file window placements are kept in.</summary>
        public const string FileName = "windows.json";

        // Read through LunaSettings.Store rather than a captured file object: a host may replace the store after this type is first touched.
        private static Dictionary<string, WindowPlacement> All() =>
            LunaSettings.Store.Load<Dictionary<string, WindowPlacement>>(null, FileName) ?? new Dictionary<string, WindowPlacement>();

        /// <summary>Reads back the placement saved under a key.</summary>
        /// <param name="key">The WindowKey the placement was saved under.</param>
        /// <returns>The saved placement, or null if nothing was saved for that key.</returns>
        public static WindowPlacement? Load(string key) =>
            All().TryGetValue(key, out WindowPlacement? p) ? p : null;

        /// <summary>Saves one placement, leaving every other key in the file alone.</summary>
        /// <param name="key">The WindowKey to save under.</param>
        /// <param name="placement">Where the window was.</param>
        public static void Save(string key, WindowPlacement placement)
        {
            Dictionary<string, WindowPlacement> all = All();
            all[key] = placement;
            LunaSettings.Store.Save(null, FileName, all);
        }

        // WHAT A CLOSING WINDOW SHOULD WRITE DOWN, as a pure function of what it can see - see docs/LunaP.md §75.4.
        //
        // A window that is COVERING the screen - maximized or full screen - has the screen's bounds
        // as its own, so saving them would record the screen as the window's restored size. Reopen
        // it, leave the covering state, and the "normal" window is the size of the display with its
        // title bar off the top. That is the failure this whole function exists to prevent, and it
        // is why the current geometry is deliberately NOT trusted in either state.
        //
        // So a covering window defers to whatever was stored last time, which is the last geometry
        // the window is known to have had while it was an ordinary window. WITH NOTHING STORED IT
        // WRITES ZEROES RATHER THAN THE SCREEN, which is not a sentinel invented here: RestorePlacement
        // already ignores a non-positive size, and IsOnAScreen already answers false for an empty
        // rectangle. A window maximized on its very first run therefore remembers the flag and no
        // geometry, and reopens maximized at its own default size - which is the honest record of
        // the fact that nobody ever saw it as a normal window.
        //
        // This is SPLIT OUT AND PURE for the reason §8.1 split IsOnAScreen out: the rule can then be
        // tested against real full-screen geometry, which the headless platform never produces. It
        // stores WindowState faithfully and never moves or resizes the window to match, so an
        // end-to-end test of this rule would pass just as happily without it (§75.4).
        /// <summary>Works out what a closing window should save, given that a maximized or full-screen window's own bounds are the screen's rather than its own.</summary>
        /// <param name="state">The state the window is closing in.</param>
        /// <param name="position">Where the window is now, used only when it is not covering the screen.</param>
        /// <param name="width">The window's current width, used only when it is not covering the screen.</param>
        /// <param name="height">The window's current height, used only when it is not covering the screen.</param>
        /// <param name="previous">What was stored under this key last time, which is where a covering window's geometry comes from. Null means nothing was ever stored, and the result carries no geometry at all.</param>
        /// <returns>The placement to save.</returns>
        public static WindowPlacement PlacementToSave(
            WindowState state, PixelPoint position, double width, double height, WindowPlacement? previous)
        {
            bool covering = state is WindowState.Maximized or WindowState.FullScreen;

            return new WindowPlacement
            {
                X = covering ? previous?.X ?? 0 : position.X,
                Y = covering ? previous?.Y ?? 0 : position.Y,
                Width = covering ? previous?.Width ?? 0 : width,
                Height = covering ? previous?.Height ?? 0 : height,

                // FULL SCREEN IS NOT REMEMBERED AS A STATE, and that asymmetry with Maximized is the
                // decision rather than an oversight - see docs/LunaP.md §75.5. A window that reopens
                // maximized still has its title bar and its close button; one that reopens full
                // screen has neither, and the key that would let it out belongs to the application
                // rather than to this toolkit. Somebody who wants it back sets IsFullScreen at
                // startup, which is one line and is theirs to decide.
                Maximized = state == WindowState.Maximized,
            };
        }

        // A monitor that is no longer attached would otherwise restore a window off every screen, where it cannot be dragged back.
        /// <summary>Whether a saved rectangle still lands on a connected display, so a window is not restored onto a monitor that has been unplugged.</summary>
        /// <param name="screens">The screens as Avalonia reports them. Null is treated as unknown and answers true, because refusing to restore is worse than restoring somewhere odd.</param>
        /// <param name="bounds">The rectangle the window would be restored to.</param>
        /// <returns>True if the rectangle overlaps any screen.</returns>
        public static bool IsOnAScreen(Screens? screens, PixelRect bounds)
        {
            if (screens is null) return true;

            var all = new List<PixelRect>(screens.All.Count);
            foreach (Screen screen in screens.All) all.Add(screen.Bounds);
            return IsOnAScreen(all, bounds);
        }

        // Split out from the Screens overload so the rule itself is testable without a display - see docs/LunaP.md §8.1.
        /// <summary>The overlap test itself, taking plain rectangles so it can be exercised without a display.</summary>
        /// <param name="screenBounds">The screen rectangles to test against. An empty list answers true.</param>
        /// <param name="bounds">The rectangle the window would be restored to.</param>
        /// <returns>True if the rectangle overlaps any of them.</returns>
        public static bool IsOnAScreen(IReadOnlyList<PixelRect> screenBounds, PixelRect bounds)
        {
            // Nothing to check against is not the same as "off screen"; refusing here would strand the window at the default position instead.
            if (screenBounds.Count == 0) return true;

            foreach (PixelRect screen in screenBounds)
            {
                if (screen.Intersects(bounds)) return true;
            }

            return false;
        }
    }
}
