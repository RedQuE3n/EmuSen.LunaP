using System;
using System.IO;
using Avalonia.Media;

namespace EmuSen.LunaP.Media
{
    /// <summary>The interface's typefaces, bundled with the toolkit under the SIL Open Font License: Barlow for words, Barlow Condensed for display and titles.</summary>
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

        private static string FileOf(string name) => Path.Combine(AppContext.BaseDirectory, Folder, name);
    }
}
