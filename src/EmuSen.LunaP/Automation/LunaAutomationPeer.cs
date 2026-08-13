using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace EmuSen.LunaP.Automation
{
    // What a LunaP control tells a screen reader it is - see docs/LunaP.md §24.
    //
    // THE DEFECT THIS EXISTS TO FIX IS NOT "THE NAMES WERE EMPTY". It is that the controls were
    // not in the automation tree at all, and the mechanism is worth understanding before touching
    // anything here, because it is counter-intuitive and it bites silently.
    //
    // Avalonia's Control.OnCreateAutomationPeer returns a NoneAutomationPeer unless a control
    // overrides it. A NoneAutomationPeer reports IsControlElement = false, which means assistive
    // technology walking the CONTROL VIEW of the tree - the view a screen reader navigates - does
    // not visit the node. Separately, Avalonia hides a templated control's own template parts from
    // that same view, on the entirely reasonable assumption that the control speaks for its parts.
    //
    // Put those together and a templated control that never overrides OnCreateAutomationPeer
    // vanishes twice over: the control is skipped because it is a None peer, and its label and
    // value are skipped because they are template parts of a control that was supposed to speak
    // for them. Measured on MeterRow before this type existed (§24.1):
    //
    //     NoneAutomationPeer      type=None  isControlElement=False  name=""        <- the row
    //       TextBlockAutomationPeer type=Text isControlElement=False name="CPU"     <- its label
    //       ProgressBarAutomationPeer type=ProgressBar isControlElement=True name="" <- the bar
    //       TextBlockAutomationPeer type=Text isControlElement=False name="62.0%"   <- its value
    //
    // The only thing in the control view was a nameless progress bar. A dashboard of eight meters
    // announced as eight anonymous percentages with nothing to say what any of them measured.
    //
    // ONE PEER RATHER THAN NINE. Every LunaP control needs the same three answers - what kind of
    // thing am I, what am I called, what else is worth saying - and nine near-identical five-line
    // subclasses would be nine places for that answer to drift. The control type is a constructor
    // argument and the strings are delegates, so a control reports its live property rather than
    // whatever the string was when the peer was built.
    /// <summary>What a LunaP control reports itself as to a screen reader.</summary>
    public class LunaAutomationPeer : ControlAutomationPeer
    {
        private readonly AutomationControlType _type;
        private readonly Func<string?>? _name;
        private readonly Func<string?>? _help;
        private readonly Func<string?>? _status;

        // `name`, `help` and `status` are read every time they are asked for, never cached. A meter
        // row's value changes several times a second and a peer holding the string it was built
        // with would report the reading from whenever the window opened.
        /// <summary>An automation peer that answers from delegates, so a control built in code can say what it is without a peer class of its own.</summary>
        /// <param name="owner">The control this peer speaks for.</param>
        /// <param name="type">What kind of control a screen reader should announce it as.</param>
        /// <param name="name">Supplies the accessible name, called each time it is asked for rather than captured, so a control whose label changes stays correct. Null falls back to the base peer.</param>
        /// <param name="help">Supplies the help text the same way.</param>
        /// <param name="status">Supplies the item status - the sentence a reader adds after the name and role, for state that is not the value.</param>
        public LunaAutomationPeer(
            Control owner,
            AutomationControlType type,
            Func<string?>? name = null,
            Func<string?>? help = null,
            Func<string?>? status = null)
            : base(owner)
        {
            _type = type;
            _name = name;
            _help = help;
            _status = status;
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => _type;

        // THE CALLER WINS. base.GetNameCore() reads AutomationProperties.Name and falls back to
        // whatever AutomationProperties.LabeledBy points at, so an application that has named a
        // control keeps its name and the control's own property is only the default. Doing this the
        // other way round would make the attached property silently useless on exactly the controls
        // that most need it - and a toolkit that ignores the standard Avalonia way of naming things
        // is worse than one that does not name them at all, because the caller's fix looks applied.
        protected override string? GetNameCore()
        {
            string? explicitly = base.GetNameCore();
            return string.IsNullOrWhiteSpace(explicitly) ? Read(_name) : explicitly;
        }

        protected override string? GetHelpTextCore()
        {
            string? explicitly = base.GetHelpTextCore();
            return string.IsNullOrWhiteSpace(explicitly) ? Read(_help) : explicitly;
        }

        // The changing part of a reading, kept apart from the name. A meter is "CPU" and its status
        // is "62.0%"; folding them into one string would mean the name of the control changed every
        // frame, and a name is supposed to be how you refer to a thing rather than what it says.
        protected override string? GetItemStatusCore()
        {
            string? explicitly = base.GetItemStatusCore();
            return string.IsNullOrWhiteSpace(explicitly) ? Read(_status) : explicitly;
        }

        // Empty and whitespace both become null, because a name of " " is a name as far as the
        // fallback above is concerned and would shadow the caller's.
        private static string? Read(Func<string?>? source)
        {
            string? value = source?.Invoke();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
