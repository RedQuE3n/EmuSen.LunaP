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
