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
        /// <summary>The palette keys the theme overrode, ready to merge into an application resource dictionary.</summary>
        public ResourceDictionary Resources { get; } = new();

        /// <summary>The styles the theme produced, one per rule that compiled to something.</summary>
        public Styles Styles { get; } = new();

        // Not fatal by design: a theme written against a newer LunaP still loads - see docs/LunaP.md §12.2.
        /// <summary>What the parser could not use, one line each with a line number. Never fatal: a theme written against a newer LunaP still loads, minus the rules this version does not know.</summary>
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
        /// <summary>The selector a theme declares palette tokens under.</summary>
        public const string RootSelector = ":root";
        /// <summary>The prefix every palette token carries, so a theme cannot collide with a plain CSS property name.</summary>
        public const string TokenPrefix = "--luna-";

        // A syntax error refuses the whole file, the way a malformed .axaml theme already does.
        /// <summary>Parses a theme, collecting what it could not use rather than throwing.</summary>
        /// <param name="css">The theme text. Null is treated as empty.</param>
        /// <returns>The resources and styles it produced, and a warning for every declaration it refused. A syntax error refuses the whole file, the way a malformed .axaml theme already does.</returns>
        public static CssThemeResult Parse(string css) => new Parser(css ?? string.Empty).Run();

        // The vocabulary, read out of the real allow-lists so `man theme` and its drift test cannot describe a format that does not exist.
        /// <summary>Every element name a theme may use, read out of the real allow-list rather than a list kept beside it.</summary>
        public static IReadOnlyList<string> ElementNames => Elements.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Every property name a theme may set.</summary>
        public static IReadOnlyList<string> PropertyNames => Properties.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>The state classes an element supports, such as the load bands on a meter row.</summary>
        /// <param name="element">An element name from ElementNames.</param>
        /// <returns>The state names, without their leading dot. Empty for an element with no states, and for a name that is not an element at all.</returns>
        public static IReadOnlyList<string> StatesOf(string element) =>
            Elements.TryGetValue(element, out ElementSpec? spec) ? spec.Classes.Keys.ToList() : Array.Empty<string>();

        /// <summary>The template parts an element exposes, such as the output and input of a console pane.</summary>
        /// <param name="element">An element name from ElementNames.</param>
        /// <returns>The part names, without their leading dot. Empty for an element with no parts.</returns>
        public static IReadOnlyList<string> PartsOf(string element) =>
            Elements.TryGetValue(element, out ElementSpec? spec) ? spec.Parts.Keys.ToList() : Array.Empty<string>();

        // LunaSectionHeader becomes --luna-section-header; the inverse of what a :root declaration is read as.
        /// <summary>The token spelling of a palette resource key, so documentation and tests can name one without hard-coding the transformation.</summary>
        /// <param name="resourceKey">A palette key such as LunaSectionHeader.</param>
        /// <returns>The token a theme writes, such as --luna-section-header.</returns>
        public static string TokenFor(string resourceKey) => TokenPrefix[..2] + Kebab(resourceKey);
    }
}
