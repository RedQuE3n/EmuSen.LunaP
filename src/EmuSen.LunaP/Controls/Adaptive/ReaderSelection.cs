using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A strip's button, styled as the toggle it is, which a reader hears as a tab, selected while chosen - see docs/LunaP.md §213.8.
    internal sealed class SectionTab(Func<bool> isSelected, Action select) : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(ToggleButton);

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SelectableItemPeer(this, () => Content as string, isSelected, select, () => Parent as Control, type: AutomationControlType.TabItem);
    }

    // A stack a reader hears as a choice of one, its selection whichever child reports itself selected (§213.8).
    internal sealed class SelectionStack : StackPanel
    {
        public AutomationControlType Selects { get; init; } = AutomationControlType.List;

        protected override AutomationPeer OnCreateAutomationPeer() => SingleSelectionPeer.OfChildren(this, Selects);
    }

    // A drawn cell of a choice of one, such as a page's action, which a reader hears selected while it is the focused one (§213.8).
    internal sealed class SelectableCell(Func<bool> isSelected, Action select) : Panel
    {
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SelectableItemPeer(this, () => null, isSelected, select, () => Parent as Control);
    }

    // The keyboard focus kept on a layout's focused drawn row while the layout holds it, so a reader follows the pad (§213.8).
    internal static class ReaderFocus
    {
        // Whether the focus is the layout's own or one of its selectable drawn rows' or cells'.
        public static bool Held(Control layout) =>
            TopLevel.GetTopLevel(layout)?.FocusManager?.GetFocusedElement() is { } had
            && (ReferenceEquals(had, layout) || had is MenuRow { Selection: not null } or SelectableCell && layout.IsVisualAncestorOf((Control)had));

        // Gives a held focus to the focused row, attached before the old one is taken out, else back to the layout.
        public static void Pass(Control layout, bool held, Control? row)
        {
            if (held && !(row?.Focus() ?? false)) layout.Focus();
        }
    }
}
