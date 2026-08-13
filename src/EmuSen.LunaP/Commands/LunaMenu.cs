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
        public LunaMenu(string title, params LunaAction[] items)
            : this(title, (IEnumerable<LunaAction>)(items ?? Array.Empty<LunaAction>()))
        {
        }

        public LunaMenu(string title, IEnumerable<LunaAction> items)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Items = new List<LunaAction>(items ?? Array.Empty<LunaAction>());
        }

        // What the menu bar shows. Not an action's Text: a menu is not a command and cannot be
        // invoked, which is why this type exists rather than a LunaAction with children.
        public string Title { get; }

        public IReadOnlyList<LunaAction> Items { get; }

        // Every action reachable from here, submenus included, in the order a reader meets them.
        // The shortcut binder walks this: a key bound to an action three levels down works exactly
        // as well as one on a top-level item, and a caller should never have to flatten by hand to
        // get that. Separators are skipped - they are not commands and binding a key to one would
        // be binding a key to nothing.
        public IEnumerable<LunaAction> Commands()
        {
            foreach (LunaAction item in Items)
            {
                if (item.IsSeparator) continue;

                yield return item;

                if (item.Submenu is not { } submenu) continue;

                foreach (LunaAction nested in submenu.Commands()) yield return nested;
            }
        }

        public override string ToString() => Title;
    }
}
