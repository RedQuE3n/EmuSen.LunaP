using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // The container one tile's content sits in, and the thing the selection ring is drawn by - see
    // docs/LunaP.md §88.2.
    //
    // PUBLIC SO A THEME CAN NAME IT. A host restyling the ring writes `luna|TileGridItem.selected`,
    // and an internal container would leave it only a class selector on some anonymous Border. It is
    // a ContentControl rather than a Border because the ring belongs to the theme: the template draws
    // it outset six pixels around the content, and a host that wants a different ring replaces the
    // template rather than fighting a Border whose BorderBrush the control sets in code.
    //
    // A grid owns its containers and reuses them as the view scrolls, so an instance's Content is
    // the host's tile control for its whole life and only the item behind it changes. Nothing here
    // should be held by a host across a scroll; ask the grid for Selected instead.
    /// <summary>The container a TileGrid puts each tile's content in, which draws the selection ring and carries the <c>selected</c> class.</summary>
    public class TileGridItem : ContentControl
    {
        /// <summary>The style class a selected tile's container carries, for a host that restyles the ring.</summary>
        public const string SelectedClass = "selected";

        internal Func<string?> Speak { get; set; } = () => null;
        internal Func<bool> IsChosen { get; set; } = () => false;
        internal Action Choose { get; set; } = () => { };
        internal Func<Control?> Grid { get; set; } = () => null;

        // Which item this container currently shows, in the grid's own list, or -1 while pooled.
        internal int Index { get; set; } = -1;

        // Whether the item was ever bound, and to what - so a container realised again for the item it
        // already shows is not bound twice, which is the BindTile contract (§88.2).
        internal object? BoundItem { get; set; }

        internal bool HasBinding { get; set; }

        /// <summary>Whether this container is drawn as the selected tile. Mirrors the <c>selected</c> class, which is what the theme matches.</summary>
        public bool IsSelected => Classes.Contains(SelectedClass);

        internal void SetSelected(bool selected) => Classes.Set(SelectedClass, selected);

        // A ListItem named by the grid's Label, because a cover grid is a list of things to pick and
        // a reader moving through it needs each thing's name rather than the tile's internals.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new SelectableItemPeer(this, () => Speak(), () => IsChosen(), () => Choose(), () => Grid());
    }
}
