using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Commands;

namespace EmuSen.LunaP.Controls
{
    // The run of buttons under the menu bar - see docs/LunaP.md §26.4.
    //
    // ButtonBar already exists and this is not it. ButtonBar is a right-aligned run of Buttons the
    // caller built and owns, for the bottom of a window: OK, Cancel, Apply. A ToolBar is built
    // FROM ACTIONS, left-aligned, and every item in it follows its action's label, enabled state
    // and checked state - which is the difference between a row of buttons and a row of commands
    // that also appear in the menu bar. Keeping them separate is why neither has a mode switch.
    //
    // A checkable action becomes a pressed-in ActionToggle rather than a button, because that is
    // the only thing that distinguishes "grid is showing" from "show the grid" on a strip with no
    // room for words.
    /// <summary>The run of action buttons under the menu bar.</summary>
    public class ToolBar : ItemsControl
    {
        private IReadOnlyList<LunaAction> _actions = Array.Empty<LunaAction>();

        public IReadOnlyList<LunaAction> Actions => _actions;

        // ToolBar, which is UIA's own name for a run of commands, rather than ItemsControl's stock
        // List. ButtonBar records the same correction (§24.2): a reader told this is a list of
        // three items is invited to navigate it as data rather than to press one of them.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.ToolBar);

        public void SetActions(params LunaAction[] actions) => SetActions((IEnumerable<LunaAction>)actions);

        public void SetActions(IEnumerable<LunaAction> actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));

            var built = new List<LunaAction>();
            var items = new List<Control>();

            foreach (LunaAction action in actions)
            {
                built.Add(action);

                // A plain Separator, turned on its side by Theme/Controls/ToolBar.axaml's
                // `luna|ToolBar Separator` rule. Avalonia's Separator is the horizontal rule a
                // MENU wants, and dropped into a horizontal run of buttons unstyled it draws a
                // full-width bar that pushes everything after it off the end of the strip.
                items.Add(action.IsSeparator
                    ? new Separator()
                    : action.IsCheckable ? new ActionToggle(action) : new ActionButton(action));
            }

            _actions = built;
            ItemsSource = items;
        }
    }
}
