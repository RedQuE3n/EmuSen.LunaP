using System;
using System.Collections.Generic;
using Avalonia.Controls;
using EmuSen.LunaP.Commands;

namespace EmuSen.LunaP.Controls
{
    // The strip along the top: File, Edit, View - see docs/LunaP.md §26.4.
    //
    // Avalonia's Menu, given a LunaMenu model instead of a XAML tree. What that buys is not the
    // saved keystrokes, it is that the menu bar is now built from the same objects as the toolbar,
    // the context menu and the key bindings, so "Save" is one thing with one enabled state rather
    // than four declarations somebody has to keep in step.
    //
    // A top-level entry is a plain MenuItem and not an ActionMenuItem, deliberately: File is not a
    // command and cannot be invoked, checked, disabled or bound to a key. Giving it an action so
    // that one type covered both would mean every menu bar carried three actions nobody could
    // trigger, each of which would then turn up in the shortcut binder's walk.
    /// <summary>The menu strip along the top of a window, built from LunaMenu models.</summary>
    public class MenuBar : Menu
    {
        // Menu's own theme, not a subclass's. Without this the Fluent ControlTheme never reaches
        // this class and the menu bar renders as an empty strip - §5.5 and §14.1 record the trap,
        // and the failure here would be a window that simply has no menus with no error anywhere.
        //
        // Load-bearing, measured: removing this line turns two tests red. It is worth knowing that
        // the same removal on ActionButton turns none red, so this is not a formality applied
        // uniformly - see the comment at the top of Controls/ActionControls.cs and §26.11.
        protected override Type StyleKeyOverride => typeof(Menu);

        public const string StyleClass = "luna-menu-bar";

        public MenuBar() => Classes.Add(StyleClass);

        private IReadOnlyList<LunaMenu> _menus = Array.Empty<LunaMenu>();

        // What this bar is currently showing. Kept so AppWindow can hand the same list to the
        // shortcut binder without the caller having to remember it too.
        public IReadOnlyList<LunaMenu> Menus => _menus;

        public void SetMenus(params LunaMenu[] menus) => SetMenus((IEnumerable<LunaMenu>)menus);

        // Replaces the whole bar. Rebuilt wholesale rather than diffed for the same reason
        // MeterList is (§5.2): the list is a handful of items, it changes when the application's
        // shape changes rather than per frame, and a diff would be more code than the thing it
        // optimises.
        public void SetMenus(IEnumerable<LunaMenu> menus)
        {
            if (menus is null) throw new ArgumentNullException(nameof(menus));

            var built = new List<LunaMenu>();
            var items = new List<Control>();

            foreach (LunaMenu menu in menus)
            {
                built.Add(menu);
                items.Add(new MenuItem { Header = menu.Title, ItemsSource = Commands.Menus.Items(menu) });
            }

            _menus = built;
            ItemsSource = items;
        }
    }
}
