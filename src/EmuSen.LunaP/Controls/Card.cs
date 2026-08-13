using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Automation;

namespace EmuSen.LunaP.Controls
{
    // A titled surface to put a group of related things on - see docs/LunaP.md §26.9.
    //
    // Qt calls it a QGroupBox, and §21.2 found the kit's consumers building one three times over:
    // `ActiveCheatsWindow.axaml:19`, `CheatDatabaseWindow.axaml:46` and `MainWindow.axaml:64` each
    // paint a Border with `{DynamicResource SystemChromeLowColor}`. That resource is FLUENTTHEME'S,
    // not LunaP's, which is the interesting part of the finding: §4's centralisation check greps
    // for hard-coded hex and these three are not hard-coded, they are sourced from the wrong
    // dictionary. A theme that restyles the whole application leaves all three of them behind.
    //
    // So the card is not new chrome, it is the chrome those three already drew, given a Luna key
    // so a theme can reach it. The header is optional and collapses when absent - a card with no
    // title is a plain raised surface, which is what MainWindow's use of it actually was.
    /// <summary>A titled surface for grouping related controls, painted from the toolkit's own palette.</summary>
    public class Card : HeaderedContentControl
    {
        // Whether there is a header at all. A direct property so the template can bind its
        // visibility, and read-only because Header is the input and this is the derived answer -
        // exactly the arrangement FieldRow.HasHint uses, for the same reason.
        public static readonly DirectProperty<Card, bool> HasHeaderProperty =
            AvaloniaProperty.RegisterDirect<Card, bool>(nameof(HasHeader), o => o.HasHeader);

        public bool HasHeader => Header is string text ? !string.IsNullOrWhiteSpace(text) : Header is not null;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // HasHeader is computed, so nothing raises for it on its own; this is what tells the
            // template to look again when the title arrives or is taken away.
            if (change.Property == HeaderProperty) RaisePropertyChanged(HasHeaderProperty, default, default);
        }

        // A Group named by its header, which is precisely what a group box is for: it tells a
        // reader that the several controls inside belong together and what they are collectively
        // about. Unnamed when there is no header - a bare surface is decoration and claiming
        // otherwise would put an empty group in the reader's way.
        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Group, name: () => Header as string);
    }
}
