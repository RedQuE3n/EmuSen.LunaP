using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Styling;

namespace EmuSen.LunaP.Theme
{
    // What a .css theme compiled to: palette resources, control styles, and every declaration that was skipped.
    /// <summary>What a CSS theme compiled to: palette resources, control styles, and every declaration that was skipped.</summary>
    public sealed class CssThemeResult
    {
        public ResourceDictionary Resources { get; } = new();

        public Styles Styles { get; } = new();

        // Not fatal by design: a theme written against a newer LunaP still loads - see docs/LunaP.md §12.2.
        public List<string> Warnings { get; } = new();
    }

    // The restricted CSS a theme may be written in - see docs/LunaP.md §12.2 for the grammar and its limits.
    //
    // ONE TYPE ACROSS FOUR FILES, split at §29.4 when the single file reached 547 lines. This one
    // holds the entry point and the vocabulary queries `man theme` reads; CssVocabulary.cs holds the
    // allow-lists, which is the half that grows every time a control is added; CssParser.cs holds the
    // parse itself; CssValues.cs holds the conversions from CSS text to Avalonia values.
    //
    // THE NAMESPACE DELIBERATELY DOES NOT FOLLOW THE FOLDER. These files sit in Theme/Css/ and stay
    // in EmuSen.LunaP.Theme, because `CssTheme` is a public name a consumer has already written a
    // `using` for - moving it to match the directory would be a breaking change bought with nothing
    // but tidiness.
    /// <summary>The restricted CSS dialect a host may write a theme in.</summary>
    public static partial class CssTheme
    {
        public const string RootSelector = ":root";
        public const string TokenPrefix = "--luna-";

        // A syntax error refuses the whole file, the way a malformed .axaml theme already does.
        public static CssThemeResult Parse(string css) => new Parser(css ?? string.Empty).Run();

        // The vocabulary, read out of the real allow-lists so `man theme` and its drift test cannot describe a format that does not exist.
        public static IReadOnlyList<string> ElementNames => Elements.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        public static IReadOnlyList<string> PropertyNames => Properties.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        public static IReadOnlyList<string> StatesOf(string element) =>
            Elements.TryGetValue(element, out ElementSpec? spec) ? spec.Classes.Keys.ToList() : Array.Empty<string>();

        public static IReadOnlyList<string> PartsOf(string element) =>
            Elements.TryGetValue(element, out ElementSpec? spec) ? spec.Parts.Keys.ToList() : Array.Empty<string>();

        // LunaSectionHeader becomes --luna-section-header; the inverse of what a :root declaration is read as.
        public static string TokenFor(string resourceKey) => TokenPrefix[..2] + Kebab(resourceKey);
    }
}
