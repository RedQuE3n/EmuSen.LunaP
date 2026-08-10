using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace EmuSen.LunaP.Theme
{
    // How hard something is working, in the three bands every dashboard shares.
    public enum LoadLevel
    {
        Nominal,
        Busy,
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
    public static class LunaPalette
    {
        public static readonly ISolidColorBrush Surface = Brush("#1E1E1E");
        public static readonly ISolidColorBrush InputSurface = Brush("#252526");
        public static readonly ISolidColorBrush Void = Brush("#000000");

        public static readonly ISolidColorBrush Text = Brush("#D4D4D4");
        public static readonly ISolidColorBrush MeterText = Brush("#DCDCDC");
        public static readonly ISolidColorBrush Muted = Brush("#808080");
        public static readonly ISolidColorBrush SectionHeader = Brush("#9CDCFE");
        public static readonly ISolidColorBrush Warning = Brush("#D08770");

        // Outcome, not load. Deliberately NOT the ramp's green/gold/red: §2.1 refused to give an
        // input conflict the same key as a hot subsystem because that would encode a relationship
        // that does not exist, and the same argument makes these three their own - see §22.9.
        //
        // The values are the ones six sites across two applications had already hard-coded:
        // IndianRed, SeaGreen and Goldenrod.
        public static readonly ISolidColorBrush Error = Brush("#CD5C5C");
        public static readonly ISolidColorBrush Success = Brush("#2E8B57");
        public static readonly ISolidColorBrush Info = Brush("#DAA520");

        public static readonly ISolidColorBrush Nominal = Brush("#32CD32");
        public static readonly ISolidColorBrush Busy = Brush("#FFD700");
        public static readonly ISolidColorBrush Hot = Brush("#FF4500");

        public static readonly FontFamily MonoFont = new("Consolas,Menlo,monospace");

        public const double HintFontSize = 11;
        public const double HeaderFontSize = 14;

        public const double BusyPercent = 60;
        public const double HotPercent = 85;

        // The one place that decides what "getting busy" means, so no two dashboards disagree - see docs/LunaP.md §2.2.
        public static LoadLevel LevelFor(double percent) =>
            percent >= HotPercent ? LoadLevel.Hot : percent >= BusyPercent ? LoadLevel.Busy : LoadLevel.Nominal;

        // The static answer, for code that cannot take a themed one; MeterRow uses pseudo-classes instead so a theme reaches it.
        public static ISolidColorBrush ForLoad(double percent) => LevelFor(percent) switch
        {
            LoadLevel.Hot => Hot,
            LoadLevel.Busy => Busy,
            _ => Nominal,
        };

        private static ImmutableSolidColorBrush Brush(string hex) => new(Color.Parse(hex));
    }
}
