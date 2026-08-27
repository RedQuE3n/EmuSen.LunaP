using System.Collections.Generic;

namespace EmuSen.LunaP.Theme
{
    // ONE THEME, AS TEXT, PLUS ENOUGH TO SAY WHERE IT CAME FROM - see docs/LunaP.md §86.11.
    //
    // Text rather than a parsed ResourceDictionary, because parsing is LunaTheme's job and there
    // are two formats. A source that had to return a parsed theme would have to know the
    // difference between .axaml and restricted CSS, which is the whole of §12.2 and is not
    // something a place-to-get-files should carry.
    /// <summary>One theme's text, the format it is written in, and where it came from.</summary>
    /// <param name="Name">The theme's name, as it appears in Available.</param>
    /// <param name="Format">Which format the text is written in: LunaTheme.Extension or LunaTheme.CssExtension.</param>
    /// <param name="Text">The theme itself, unparsed.</param>
    /// <param name="Origin">Where this one came from, for a diagnostic. A file path, for a folder source.</param>
    public sealed record ThemeDocument(string Name, string Format, string Text, string Origin);

    // WHERE LUNATHEME FINDS THE THEMES A USER HAS WRITTEN - see docs/LunaP.md §86.11.
    //
    // THIS EXISTS BECAUSE `ISettingsStore` USED TO CARRY IT, and carrying it made that interface
    // demand a filesystem. Its third method was `string Directory(string? category)`, which is a
    // path, so every implementation of a settings store had to be file-backed or hand back a path
    // it did not mean. A consumer keeping settings in SQLite had to answer it anyway and wrote
    // down that the seam "leaks a file model on purpose". §86.5 is that finding.
    //
    // The two are genuinely different jobs. A settings store keeps values the program wrote. This
    // keeps documents a PERSON wrote, by hand, in a text editor, and the program only reads them.
    // Conflating them let the second requirement dictate the first's interface.
    //
    // A FOLDER IS THE DEFAULT AND NOT THE CONTRACT. `FolderThemeSource` is what a host gets
    // without asking, and it is where hand-written theme files belong. But nothing here says
    // "path": a source can serve themes from resources compiled into an application, from a
    // database, or from a dictionary in a test - which is what makes the whole of LunaTheme
    // reachable without touching a disk.
    /// <summary>Where LunaTheme finds the themes a user has written.</summary>
    public interface IThemeSource
    {
        // In the order they should be offered. LunaTheme prepends BuiltIn and filters it out of
        // whatever arrives here, because BuiltIn is its concept and not a source's.
        /// <summary>The theme names this source can open, without extensions, in the order they should be offered.</summary>
        /// <returns>The names available. Empty when there are none, which is the ordinary state of a fresh install.</returns>
        IReadOnlyList<string> Names();

        // May throw: LunaTheme catches, reports through LunaSettings.Report and keeps the theme
        // already in force, which is the same contract a broken file on disk has always had. An
        // implementation does not have to be careful, and one that IS careful can simply answer null.
        /// <summary>Opens one theme by name.</summary>
        /// <param name="name">A name from Names.</param>
        /// <returns>The theme, or null when this source has no theme by that name. Null is not a failure: LunaTheme reports it and leaves the current theme in force.</returns>
        ThemeDocument? Open(string name);

        // FOR A MESSAGE TO A PERSON, NOT A PATH TO OPEN, and that distinction is the entire reason
        // this interface can exist where `ISettingsStore.Directory` could not. "theme 'dusk' not
        // found in <origin>" has to say something, and a source that is not a folder answers with
        // something honest like "(in memory)" rather than inventing a path.
        /// <summary>Where these themes come from, in words a person can act on. A folder path for a folder source; something like "(in memory)" for one that is not.</summary>
        string Origin { get; }
    }
}
