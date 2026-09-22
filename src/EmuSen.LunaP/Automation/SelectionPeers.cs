using System;
using System.Collections.Generic;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace EmuSen.LunaP.Automation
{
    // The two peers a single-selection list built from plain controls needs - see docs/LunaP.md §88.6.
    //
    // TileGrid<T> and SourceList are not ListBoxes, so Avalonia's ListBox and ListBoxItem peers are
    // not there to lean on, and without these a reader would meet a focusable control reporting
    // nothing about which of its items is chosen. LunaTablePeer already answers ISelectionProvider
    // for a table; this is the same answer without the scroll half, shared by both new controls
    // because they need identical sentences and two copies would be two places to drift.
    //
    // Selection is never required and never multiple: both controls hold at most one choice, and
    // an empty choice is a real state (nothing picked yet, or the picked item was refreshed away).
    internal sealed class SingleSelectionPeer : LunaAutomationPeer, ISelectionProvider
    {
        private readonly Func<IReadOnlyList<AutomationPeer>> _selection;

        public SingleSelectionPeer(
            Control owner,
            AutomationControlType type,
            Func<IReadOnlyList<AutomationPeer>> selection)
            : base(owner, type)
        {
            _selection = selection;
        }

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;

        public IReadOnlyList<AutomationPeer> GetSelection() => _selection();
    }

    // One item of such a list. Select() is a person acting through assistive technology, so the
    // owner treats it as a user's choice and raises its Chose - the opposite of a host's Select,
    // which a reader never calls. Add and Remove collapse to Select and to nothing, because a
    // single-selection list has no second slot to add into and removing the only choice would
    // report a null the controls' events do not carry.
    internal sealed class SelectableItemPeer : LunaAutomationPeer, ISelectionItemProvider
    {
        private readonly Func<bool> _isSelected;
        private readonly Action _select;
        private readonly Func<Control?> _container;

        public SelectableItemPeer(
            Control owner,
            Func<string?> name,
            Func<bool> isSelected,
            Action select,
            Func<Control?> container,
            Func<string?>? status = null)
            : base(owner, AutomationControlType.ListItem, name: name, status: status)
        {
            _isSelected = isSelected;
            _select = select;
            _container = container;
        }

        public bool IsSelected => _isSelected();

        public ISelectionProvider? SelectionContainer =>
            _container() is { } list ? CreatePeerForElement(list).GetProvider<ISelectionProvider>() : null;

        public void Select() => _select();

        public void AddToSelection() => _select();

        public void RemoveFromSelection()
        {
        }
    }
}
