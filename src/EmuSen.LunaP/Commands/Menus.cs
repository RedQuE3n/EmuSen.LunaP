using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Commands
{
    // Turning actions into the Avalonia controls that show them - see docs/LunaP.md §26.4.
    //
    // The direction matters and is worth stating: this reads a LunaMenu and produces controls. It
    // never writes back, and nothing built here is kept - a caller that wants the menu to change
    // changes the ACTION. That is what keeps a menu bar, a context menu and a key binding built
    // from the same File menu from being three copies that drift.
    /// <summary>Builds the Avalonia controls that show actions, and the key bindings that make their shortcuts work.</summary>
    public static class Menus
    {
        // One menu's worth of entries. Separators become Avalonia's Separator, which is a control
        // and not a menu item, which is precisely why LunaAction carries a flag instead of the
        // caller composing two collections.
        /// <summary>Builds the menu items for one menu, each following its action.</summary>
        /// <param name="menu">The menu to build.</param>
        /// <returns>Controls to put in a MenuItem or ContextMenu, with separators rendered as lines.</returns>
        public static IReadOnlyList<Control> Items(LunaMenu menu)
        {
            if (menu is null) throw new ArgumentNullException(nameof(menu));

            return Items(menu.Items);
        }

        /// <summary>Builds menu items for a sequence of actions, each following its action.</summary>
        /// <param name="actions">The actions, in order.</param>
        /// <returns>Controls to put in a MenuItem or ContextMenu.</returns>
        public static IReadOnlyList<Control> Items(IEnumerable<LunaAction> actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));

            var items = new List<Control>();
            foreach (LunaAction action in actions)
            {
                items.Add(action.IsSeparator ? new Separator() : new ActionMenuItem(action));
            }

            return items;
        }

        // A right-click menu, which is the surface the toolkit has never had at all: not one of
        // the four applications surveyed in §21 has a context menu anywhere, and the reason is
        // visible in the code - building one by hand is a MenuItem per command with its own
        // Click handler, its own enabled state and no relationship to the menu bar entry that does
        // the same thing. Given actions, it is one call.
        /// <summary>A context menu built from actions, written inline.</summary>
        /// <param name="actions">The actions, in order.</param>
        /// <returns>A ContextMenu to assign to a control.</returns>
        public static ContextMenu Context(params LunaAction[] actions) => Context((IEnumerable<LunaAction>)actions);

        /// <summary>A context menu built from a sequence of actions.</summary>
        /// <param name="actions">The actions, in order.</param>
        /// <returns>A ContextMenu to assign to a control.</returns>
        public static ContextMenu Context(IEnumerable<LunaAction> actions) =>
            new() { ItemsSource = Items(actions) };

        // Makes the shortcuts work when no menu is open, which is where a keyboard user spends all
        // of their time - see docs/LunaP.md §26.5.
        //
        // A MenuItem's InputGesture is DISPLAY ONLY. It draws "Ctrl+S" on the right of the item
        // and binds nothing whatsoever, which is a trap worth naming because the menu then looks
        // exactly like a working one: the shortcut is written in the place a user goes to learn
        // it, and pressing it does nothing. The key binding is a separate act, and this is it.
        //
        // Returns what it added so a caller rebuilding its menus can remove precisely those again.
        // Clearing the target's KeyBindings wholesale would throw away any the application set for
        // itself, which is a toolkit deciding it owns a collection it merely contributes to.
        /// <summary>Binds every shortcut in these menus, including nested ones, so the keys actually work.</summary>
        /// <param name="target">What the keys are bound on, usually the window.</param>
        /// <param name="menus">The menus whose actions carry shortcuts.</param>
        /// <returns>The bindings that were added, to hand to Unbind when the menus are replaced. A menu item shows a gesture without binding it, which is why this call exists at all.</returns>
        public static IReadOnlyList<KeyBinding> BindShortcuts(InputElement target, IEnumerable<LunaMenu> menus)
        {
            if (menus is null) throw new ArgumentNullException(nameof(menus));

            var actions = new List<LunaAction>();
            foreach (LunaMenu menu in menus) actions.AddRange(menu.Commands());
            return BindShortcuts(target, actions);
        }

        /// <summary>Binds the shortcut of every action that has one.</summary>
        /// <param name="target">What the keys are bound on, usually the window.</param>
        /// <param name="actions">The actions to bind. Those without a Shortcut are skipped.</param>
        /// <returns>The bindings that were added, to hand to Unbind later.</returns>
        public static IReadOnlyList<KeyBinding> BindShortcuts(InputElement target, IEnumerable<LunaAction> actions)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (actions is null) throw new ArgumentNullException(nameof(actions));

            var added = new List<KeyBinding>();
            var claimed = new Dictionary<KeyGesture, LunaAction>();

            foreach (LunaAction action in actions)
            {
                if (action.Shortcut is not { } gesture || action.IsSeparator) continue;

                // TWO COMMANDS ON ONE KEY IS A DEFECT, AND A SILENT ONE. Avalonia runs the first
                // matching binding and stops, so the second action simply never fires - and the
                // menu still shows its shortcut next to it, so the evidence on screen says it
                // should have worked. Reported rather than thrown, on the same principle the theme
                // loader follows: a menu that is wrong in one entry should still open.
                if (claimed.TryGetValue(gesture, out LunaAction? already))
                {
                    // THE SAME ACTION TWICE IS NOT A CONFLICT, and getting this wrong would make
                    // the diagnostic worse than useless. An action that appears in the File menu
                    // and on the toolbar is the arrangement this whole design is for, and it
                    // arrives here twice; reporting "Ctrl+S is bound to both Save and Save" would
                    // train a reader to ignore the message that catches the real collision.
                    if (ReferenceEquals(already, action)) continue;

                    LunaSettings.Report(
                        $"{gesture} is bound to both '{already.Text}' and '{action.Text}'; only '{already.Text}' will fire.");
                    continue;
                }

                claimed[gesture] = action;

                var binding = new KeyBinding { Gesture = gesture, Command = action };
                target.KeyBindings.Add(binding);
                added.Add(binding);
            }

            return added;
        }

        /// <summary>Removes bindings previously added by BindShortcuts.</summary>
        /// <param name="target">The element they were bound on.</param>
        /// <param name="bindings">The bindings to remove. Leaving these behind is how a key outlives the menu that advertised it.</param>
        public static void Unbind(InputElement target, IEnumerable<KeyBinding> bindings)
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (bindings is null) return;

            foreach (KeyBinding binding in bindings) target.KeyBindings.Remove(binding);
        }
    }
}
