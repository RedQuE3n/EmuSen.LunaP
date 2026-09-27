using System;
using System.Collections.Generic;

namespace EmuSen.LunaP.Commands
{
    // A titled run of actions: one top-level menu, or one submenu - see docs/LunaP.md §26.4.
    //
    // Plain data, and it stays plain data. It holds no controls, raises nothing and knows nothing
    // about a window - which is what lets the same File menu be handed to a menu bar, to a context
    // menu and to the shortcut binder without any of the three being able to disturb the other
    // two. MenuBar.SetMenus builds controls FROM this; it never keeps the controls in it.
    //
    // Nesting goes through LunaAction.Submenu rather than through a list of mixed types, which is
    // the same choice Qt makes and the reason a menu here has exactly one item type. See §26.3.
    /// <summary>A titled run of actions forming one top-level menu or one submenu.</summary>
    public sealed class LunaMenu
    {
        /// <summary>A titled menu, written inline with its actions.</summary>
        /// <param name="title">The top-level title, as shown in the menu bar.</param>
        /// <param name="items">The actions, in order. Use LunaAction.Separator for a divider and an action with a Submenu for a nested menu.</param>
        public LunaMenu(string title, params LunaAction[] items)
            : this(title, (IEnumerable<LunaAction>)(items ?? Array.Empty<LunaAction>()))
        {
        }

        /// <summary>A titled menu built from an existing sequence of actions.</summary>
        /// <param name="title">The top-level title, as shown in the menu bar.</param>
        /// <param name="items">The actions, in order, or null for an empty menu. Copied, so later changes to the sequence are not seen.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="title"/> is null.</exception>
        public LunaMenu(string title, IEnumerable<LunaAction> items)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Items = new List<LunaAction>(items ?? Array.Empty<LunaAction>());
        }

        // What the menu bar shows. Not an action's Text: a menu is not a command and cannot be
        // invoked, which is why this type exists rather than a LunaAction with children.
        /// <summary>The title shown in the menu bar.</summary>
        public string Title { get; }

        /// <summary>The actions in this menu, in order, including separators and submenu owners.</summary>
        public IReadOnlyList<LunaAction> Items { get; }

        // Every action reachable from here, submenus included, in the order a reader meets them.
        // The shortcut binder walks this: a key bound to an action three levels down works exactly
        // as well as one on a top-level item, and a caller should never have to flatten by hand to
        // get that. Separators are skipped - they are not commands and binding a key to one would
        // be binding a key to nothing.
        /// <summary>Every action this menu can reach, including those nested in submenus, flattened.</summary>
        /// <returns>The actions, in the order a reader meets them, with separators left out. A submenu's owner is returned before the actions it contains; only separators are skipped.</returns>
        public IEnumerable<LunaAction> Commands()
        {
            foreach (LunaAction item in Items)
            {
                if (item.IsSeparator) continue;

                // THE OWNER IS RETURNED TOO, and that is deliberate rather than an oversight - it is
                // what the walk has done since it was written and what ActionTests has asserted
                // since the same commit. A caller enumerating a menu is enumerating what is IN it,
                // and an owner carrying a HelpText or a Shortcut is still a thing the caller put
                // there. See §82.2: the <returns> tag claimed owners were left out for a day and a
                // half and was corrected to match this, not the other way round.
                yield return item;

                if (item.Submenu is not { } submenu) continue;

                foreach (LunaAction nested in submenu.Commands()) yield return nested;
            }
        }

        public override string ToString() => Title;
    }
}
