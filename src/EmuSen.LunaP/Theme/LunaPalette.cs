using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Theme
{
    // How hard something is working, in the three bands every dashboard shares.
    /// <summary>How hard something is working, in the three bands a dashboard colours by.</summary>
    public enum LoadLevel
    {
        /// <summary>Working normally: below the busy threshold.</summary>
        Nominal,
        /// <summary>Working hard: at or above the busy threshold, below the hot one.</summary>
        Busy,
        /// <summary>Working at or beyond the hot threshold.</summary>
        Hot,
    }

    // The C# half of Theme/Palette.axaml, for controls built in code; LunaPaletteTests pins the two
    // together - see docs/LunaP.md §2.1.
    //
    // THESE ARE THE DARK COLUMN, and they are only the dark column. The palette gained a light one
    // in §23 and a static field cannot follow a theme variant any more than it can follow a loaded
    // theme - §12.1 already made exactly that point about a computed brush, which is why MeterRow
    // uses pseudo-classes instead. Anything that must be correct in both variants has to resolve
    // LunaSurface, LunaText and the rest as a DynamicResource and let the tree do it.
    //
    // This is not a gap waiting to be filled with a second set of fields. It is what a static is:
    // the answer for code that has no resource host to ask, and the wrong tool for code that has.
    /// <summary>The C# half of the palette, for controls built in code rather than in XAML.</summary>
    public static class LunaPalette
    {
        /// <summary>The window background everything else sits on.</summary>
        public static readonly ISolidColorBrush Surface = Brush("#1E1E1E");
        /// <summary>The background of a field, a console or a card - a surface something is entered or read in.</summary>
        public static readonly ISolidColorBrush InputSurface = Brush("#252526");
        /// <summary>True black, for an image view letterboxing an area with no pixels in it.</summary>
        public static readonly ISolidColorBrush Void = Brush("#000000");

        // Card edges, the rule under a panel header, the divider a splitter is dragged by. Picked
        // against WCAG 1.4.11's 3:1 rather than by eye, because a divider you have to find with a
        // mouse is a control and not decoration - 3.27:1 on the dark surface, and docs/LunaP.md
        // §26.9 has the light column and the test.
        /// <summary>Card edges, the rule under a panel header, and the divider a splitter is dragged by.</summary>
        public static readonly ISolidColorBrush Border = Brush("#6E6E6E");

        /// <summary>Body text, and the default foreground for anything that does not choose otherwise.</summary>
        public static readonly ISolidColorBrush Text = Brush("#D4D4D4");
        /// <summary>The label and value on a meter row, a shade brighter than body text so it reads against a filled bar.</summary>
        public static readonly ISolidColorBrush MeterText = Brush("#DCDCDC");
        /// <summary>Hint text, an empty state, and anything else that should recede.</summary>
        public static readonly ISolidColorBrush Muted = Brush("#808080");
        /// <summary>A section heading, and a card header.</summary>
        public static readonly ISolidColorBrush SectionHeader = Brush("#9CDCFE");
        /// <summary>Text that warns without being an error - a setting that will not take effect until restart.</summary>
        public static readonly ISolidColorBrush Warning = Brush("#D08770");

        // Outcome, not load. Deliberately NOT the ramp's green/gold/red: §2.1 refused to give an
        // input conflict the same key as a hot subsystem because that would encode a relationship
        // that does not exist, and the same argument makes these three their own - see §22.9.
        //
        // The values are the ones six sites across two applications had already hard-coded:
        // IndianRed, SeaGreen and Goldenrod.
        /// <summary>An outcome that failed. Not the load ramp: outcome and load are unrelated (see the note above).</summary>
        public static readonly ISolidColorBrush Error = Brush("#CD5C5C");
        /// <summary>An outcome that succeeded.</summary>
        public static readonly ISolidColorBrush Success = Brush("#2E8B57");
        /// <summary>An outcome worth reading that is neither success nor failure.</summary>
        public static readonly ISolidColorBrush Info = Brush("#DAA520");

        /// <summary>A meter working normally, at the bottom of the load ramp.</summary>
        public static readonly ISolidColorBrush Nominal = Brush("#32CD32");
        /// <summary>A meter working hard, in the middle of the load ramp.</summary>
        public static readonly ISolidColorBrush Busy = Brush("#FFD700");
        /// <summary>A meter at the top of the load ramp.</summary>
        public static readonly ISolidColorBrush Hot = Brush("#FF4500");

        /// <summary>The monospaced family a console pane and a hex view are set in, with fallbacks for each platform.</summary>
        public static readonly FontFamily MonoFont = new("Consolas,Menlo,monospace");

        /// <summary>Point size for hint text and other secondary lines.</summary>
        public const double HintFontSize = 11;
        /// <summary>Point size for a section heading.</summary>
        public const double HeaderFontSize = 14;

        /// <summary>The percentage at which a meter stops being nominal and starts being busy.</summary>
        public const double BusyPercent = 60;
        /// <summary>The percentage at which a meter becomes hot.</summary>
        public const double HotPercent = 85;

        // The one place that decides what "getting busy" means, so no two dashboards disagree - see docs/LunaP.md §2.2.
        /// <summary>Puts a percentage into one of the three load bands, using the thresholds above.</summary>
        /// <param name="percent">How loaded the thing is, 0 to 100. Values outside that range are not clamped: anything at or above <see cref="HotPercent"/> is hot.</param>
        /// <returns>The band <paramref name="percent"/> falls in.</returns>
        public static LoadLevel LevelFor(double percent) =>
            percent >= HotPercent ? LoadLevel.Hot : percent >= BusyPercent ? LoadLevel.Busy : LoadLevel.Nominal;

        // The static answer, for code that cannot take a themed one; MeterRow uses pseudo-classes instead so a theme reaches it.
        /// <summary>The ramp colour for a load, for code with no resource host to ask.</summary>
        /// <param name="percent">How loaded the thing is, 0 to 100.</param>
        /// <returns>
        /// The dark-variant brush for that band. This does not follow the theme variant or a loaded
        /// theme, because a static field cannot; a control that must track either should resolve
        /// <c>LunaNominal</c>, <c>LunaBusy</c> or <c>LunaHot</c> as a dynamic resource instead.
        /// </returns>
        public static ISolidColorBrush ForLoad(double percent) => LevelFor(percent) switch
        {
            LoadLevel.Hot => Hot,
            LoadLevel.Busy => Busy,
            _ => Nominal,
        };

        private static ImmutableSolidColorBrush Brush(string hex) => new(Color.Parse(hex));
    }
}
