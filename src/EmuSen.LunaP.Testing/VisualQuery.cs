using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace EmuSen.LunaP.Testing
{
    // Typed visual-tree lookups, so a control test asserts about a rendered part rather than a field it happens to hold.
    /// <summary>Typed visual-tree lookups, for asserting about a rendered template part rather than a field a control happens to hold.</summary>
    public static class VisualQuery
    {
        /// <summary>The first descendant of the given type, in depth-first visual-tree order.</summary>
        /// <typeparam name="T">The visual type to look for.</typeparam>
        /// <param name="root">The visual to search under. Not included in the search.</param>
        /// <returns>The first match, or <c>null</c> if the tree holds none. A control that has not been shown has no visual tree at all, so this returns <c>null</c> for a reason that has nothing to do with the type.</returns>
        public static T? FindPart<T>(this Visual root) where T : Visual =>
            root.GetVisualDescendants().OfType<T>().FirstOrDefault();

        /// <summary>Every descendant of the given type, in depth-first visual-tree order.</summary>
        /// <typeparam name="T">The visual type to look for.</typeparam>
        /// <param name="root">The visual to search under. Not included in the search.</param>
        /// <returns>The matches, lazily. The sequence walks the tree as it is enumerated, so it must not outlive the layout it describes.</returns>
        public static IEnumerable<T> FindParts<T>(this Visual root) where T : Visual =>
            root.GetVisualDescendants().OfType<T>();

        /// <summary>How many descendants of the given type the visual tree holds.</summary>
        /// <typeparam name="T">The visual type to count.</typeparam>
        /// <param name="root">The visual to search under. Not included in the count.</param>
        /// <returns>The number of matches. Zero for a control that was never shown, which is worth ruling out before reading it as a templating failure.</returns>
        public static int CountParts<T>(this Visual root) where T : Visual =>
            root.GetVisualDescendants().OfType<T>().Count();

        // For windows that build their own tree in code: there is no XAML namescope for GetControl to search.
        /// <summary>The single descendant of the given type carrying that <c>Name</c>.</summary>
        /// <typeparam name="T">The control type to look for.</typeparam>
        /// <param name="root">The visual to search under. Not included in the search.</param>
        /// <param name="name">The <c>Name</c> to match, exactly.</param>
        /// <returns>The first match.</returns>
        /// <exception cref="System.InvalidOperationException">No descendant of that type carries that name. Unlike <see cref="FindPart{T}"/> this throws rather than returning null, because a named part that is missing is a broken template rather than an empty result.</exception>
        public static T FindNamed<T>(this Visual root, string name) where T : Control =>
            root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }
}
