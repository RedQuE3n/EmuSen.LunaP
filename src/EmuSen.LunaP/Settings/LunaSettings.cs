using System;

namespace EmuSen.LunaP.Settings
{
    // The one place a host points LunaP at its own settings and its own log - see docs/LunaP.md §19.
    public static class LunaSettings
    {
        private static ISettingsStore? _store;

        // Resolved on first use rather than at type load, so a host that assigns one at startup is never a moment too late.
        public static ISettingsStore Store
        {
            get => _store ??= JsonSettingsStore.ForApplication();
            set => _store = value;
        }

        // Set once by the host; null discards, which is what tests and any caller with nowhere to print want.
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
        public static void Report(string message) => Diagnostics?.Invoke(message);
    }
}
