using System;
using Avalonia;
using Avalonia.Controls;

namespace EmuSen.LunaP.Settings
{
    // The one place a host points LunaP at its own settings and its own log - see docs/LunaP.md §19.
    /// <summary>The one place a host points LunaP at its own settings store and its own diagnostics.</summary>
    public static class LunaSettings
    {
        private static ISettingsStore? _store;

        // Resolved on first use rather than at type load, so a host that assigns one at startup is never a moment too late.
        /// <summary>Where the toolkit reads and writes window placement, pane layout and the theme choice. Process-global: set it once at startup, before any window is shown.</summary>
        public static ISettingsStore Store
        {
            get => _store ??= JsonSettingsStore.ForApplication();
            set => _store = value;
        }

        // THE STORE A CONTROL SHOULD USE, RESOLVED DOWN THE LOGICAL TREE - see docs/LunaP.md §86.12.
        //
        // `Store` above is process-global, and §86.3 is the finding that filling a seam from ambient
        // static state is the thing this toolkit had wrong: five components REACHED for a store
        // rather than being handed one, so two windows could not use two stores and a test could not
        // isolate without mutating process state.
        //
        // AN INHERITED ATTACHED PROPERTY IS AVALONIA'S OWN ANSWER, and it is why this is not a
        // `Settings` property on each control. Set it on a window and every descendant sees it - the
        // split panes, the side panels, the tables, including the ones AppWindow builds internally
        // and the ones a consumer nests three levels down inside their own layout. A property per
        // control plus manual propagation works right up until somebody puts a LunaTable somewhere
        // nobody thought to plumb, and then silently writes to the wrong store.
        //
        // RESOLUTION IS STILL IMPLICIT, and saying otherwise would be a claim this does not earn.
        // What changes is the SCOPE: one value per tree rather than one per process, with the
        // process-global as the last resort. A control in no tree, or in a tree nobody set this on,
        // behaves exactly as it did before.
        /// <summary>The settings store a control and its descendants should use. Unset means the process-wide Store.</summary>
        public static readonly AttachedProperty<ISettingsStore?> StoreProperty =
            AvaloniaProperty.RegisterAttached<Control, ISettingsStore?>(
                "Store", typeof(LunaSettings), defaultValue: null, inherits: true);

        /// <summary>Reads the store set on a control or inherited by it, without falling back to the process-wide one.</summary>
        /// <param name="element">The control to read from.</param>
        /// <returns>The store set on this control or inherited from an ancestor, or null when none is. Use For to get the store a control should actually use.</returns>
        public static ISettingsStore? GetStore(Control element) => element.GetValue(StoreProperty);

        /// <summary>Sets the store a control and everything inside it should use.</summary>
        /// <param name="element">The control to set it on, usually a window.</param>
        /// <param name="value">The store, or null to go back to inheriting.</param>
        public static void SetStore(Control element, ISettingsStore? value) => element.SetValue(StoreProperty, value);

        // The one every control in the toolkit calls. Named `For` rather than `GetStore` so the two
        // are not confusable at a call site: one asks what was SET, this asks what to USE.
        /// <summary>The store a control should use: the nearest one set on it or an ancestor, or the process-wide Store.</summary>
        /// <param name="element">The control asking.</param>
        /// <returns>Never null. Falls back to Store, which itself falls back to a JSON tree named after the application.</returns>
        public static ISettingsStore For(Control element) => GetStore(element) ?? Store;

        // STANDARD ERROR WHEN NOBODY IS LISTENING, RATHER THAN NOTHING - see docs/LunaP.md §86.4.
        //
        // This started as null-means-discard, and that inverted the one rule this toolkit is
        // otherwise careful about. The common path is as quiet as a host wants it; the FAILURES
        // were the part that defaulted to silence. Everything arriving here is a recoverable
        // problem whose defining quality is that the user's evidence says it should have worked -
        // a theme file that would not parse, a settings write that failed, two menu commands
        // claiming one keyboard shortcut (§26.5) - and a host that has not yet thought about
        // diagnostics is precisely the host that needs to see them.
        //
        // MEASURED IN A CONSUMER, which is why this is a change and not a preference. Deleting the
        // sink installation from BIMA-CSharp's Program.cs left ALL TWENTY of its tests green,
        // because nothing in that suite runs Main. The entire diagnostic channel could be
        // disconnected and the suite had no opinion about it.
        //
        // SILENCE IS STILL AVAILABLE AND IS NOW A DECISION: assign a delegate that does nothing.
        //
        //     LunaSettings.Diagnostics = _ => { };
        //
        // That is the half of this that matters. Not that standard error beats nothing, but that a
        // host which has said nothing gets the loud default, and a host that wants quiet has to say
        // so - which is the only arrangement in which a silent channel means somebody chose one.
        /// <summary>Where the toolkit reports a settings failure it swallowed. Null - the default - sends them to standard error; assign a delegate that does nothing to silence them. Process-global, like Store.</summary>
        public static Action<string>? Diagnostics { get; set; }

        // "Something the application asked for could not be honoured, and why."
        //
        // It began as "this file would not load", which is still most of what arrives here: the
        // theme loader and the settings store both carry on best-effort and use this so that
        // carrying on does not happen in silence. §26.5 widened it to a second kind of thing -
        // two menu commands claiming one keyboard shortcut, where Avalonia runs the first and
        // ignores the second while the menu goes on showing the key beside both. That is the same
        // shape of problem (recoverable, invisible, and the user's evidence says it should have
        // worked) and it goes to the same sink rather than to a second one nobody would install.
        /// <summary>Reports a diagnostic message to whatever Diagnostics is set to, or to standard error when nothing is installed.</summary>
        /// <param name="message">What happened. Written for somebody reading a log, not for a user.</param>
        public static void Report(string message)
        {
            // Read once into a local: this is process-global and a host may replace it from
            // another thread, and a null-conditional call would re-read between the check and the
            // invoke.
            Action<string>? sink = Diagnostics;
            if (sink is not null)
            {
                // The installed sink gets the message and nothing else. It does NOT also go to
                // standard error - a host that has taken responsibility for diagnostics has taken
                // it, and duplicating into a stream it cannot see would be noise it cannot turn
                // off. `DocumentedContractTests` pins that.
                sink(message);
                return;
            }

            // STANDARD ERROR AND NOT STANDARD OUTPUT. A diagnostic is not the program's output;
            // on stdout it would land in whatever pipe a caller had arranged and corrupt it.
            //
            // The prefix is there because this arrives in a stream the toolkit does not own,
            // interleaved with whatever else the host writes, and "theme 'x' not found" with no
            // attribution is a line somebody has to go looking for the source of.
            Console.Error.WriteLine("LunaP: " + message);
        }
    }
}
