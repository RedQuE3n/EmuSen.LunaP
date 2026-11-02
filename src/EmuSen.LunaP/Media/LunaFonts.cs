using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    /// <summary>The interface's typefaces, bundled with the toolkit under the SIL Open Font License: Barlow for words, Barlow Condensed for display and titles, Adwaita Mono for the symbol glyphs they lack.</summary>
    public static class LunaFonts
    {
        /// <summary>Where the bundled font files are copied beside an application, relative to its base directory.</summary>
        public const string Folder = "LunaP/Fonts";

        /// <summary>Barlow, for rows, body text and captions on every form.</summary>
        public static readonly FontFamily Body = new("avares://EmuSen.LunaP/Assets/Fonts/Barlow#Barlow");

        /// <summary>Barlow Condensed, for display words and titles on every form.</summary>
        public static readonly FontFamily Condensed = new("avares://EmuSen.LunaP/Assets/Fonts/BarlowCondensed#Barlow Condensed");

        /// <summary>Barlow Regular's file, for text drawn from a font file.</summary>
        public static string BodyFile => FileOf("Barlow-Regular.ttf");

        /// <summary>Barlow Medium's file.</summary>
        public static string BodyMediumFile => FileOf("Barlow-Medium.ttf");

        /// <summary>Barlow Condensed Regular's file.</summary>
        public static string CondensedFile => FileOf("BarlowCondensed-Regular.ttf");

        /// <summary>Barlow Condensed Medium's file.</summary>
        public static string CondensedMediumFile => FileOf("BarlowCondensed-Medium.ttf");

        /// <summary>The fonts' licence, copied beside them.</summary>
        public static string LicenceFile => FileOf("OFL.txt");

        /// <summary>Adwaita Mono, the one face for the symbol glyphs Barlow lacks: the arrows, triangles, ticks, warning sign, dot, stars and boxes the interface draws, whose line beside Barlow's stays within 2 px of a plain one.</summary>
        public static readonly FontFamily Symbols = new("avares://EmuSen.LunaP/Assets/Fonts/AdwaitaMono#Adwaita Mono");

        // One bundled face for every symbol block the interface draws, so a glyph Barlow lacks never depends on the machine's fonts, on what the process loaded first, or on a face whose line is taller - see docs/LunaP.md §208.13.
        /// <summary>The font manager's fallbacks for a glyph the text's own face lacks: the arrows and the symbol blocks to Adwaita Mono.</summary>
        public static IReadOnlyList<FontFallback> Fallbacks { get; } =
        [
            new FontFallback { FontFamily = Symbols, UnicodeRange = UnicodeRange.Parse("U+2190-21FF,U+2300-23FF,U+25A0-25FF,U+2600-27BF,U+2B00-2BFF") },
        ];

        /// <summary>Adwaita Mono's licence, copied beside the fonts.</summary>
        public static string SymbolsLicenceFile => FileOf("AdwaitaMono-OFL-1.1.txt");

        private static string FileOf(string name) => Path.Combine(AppContext.BaseDirectory, Folder, name);
    }
}
