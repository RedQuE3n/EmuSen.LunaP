using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmuSen.LunaP.Theme
{
    // THE DEFAULT SOURCE: A FOLDER OF HAND-WRITTEN THEME FILES - see docs/LunaP.md §86.11.
    //
    // This is where the behaviour that used to live inside LunaTheme went when `ISettingsStore`
    // stopped carrying a directory. Nothing about it is new; what is new is that it is one
    // implementation of a seam rather than the only thing LunaTheme could do.
    //
    // THE FOLDER IS NOT CREATED BY ASKING. `Names` guards with Exists for exactly that reason, and
    // a fresh install has no themes folder until somebody writes one. The summary on LunaTheme's
    // old `Directory` property said "created on demand" until §80.3, which no implementation has
    // ever done. A consumer telling a user where to drop a theme file should call `EnsureExists`.
    /// <summary>Reads themes from a folder of hand-written .axaml and .css files.</summary>
    public sealed class FolderThemeSource : IThemeSource
    {
        /// <summary>A source reading from one folder.</summary>
        /// <param name="folder">The folder to read from. It need not exist.</param>
        public FolderThemeSource(string folder) => Folder = folder;

        /// <summary>The folder this reads from, whether or not it exists yet.</summary>
        public string Folder { get; }

        /// <inheritdoc/>
        public string Origin => Folder;

        /// <summary>Creates the folder, so a consumer can tell a user where to put a theme file and have somewhere to point at.</summary>
        public void EnsureExists() => Directory.CreateDirectory(Folder);

        // One name however many formats spell it, alphabetically, case-insensitively. The dedupe is
        // not tidiness: a theme present as both dusk.axaml and dusk.css is ONE theme with a
        // resolution order (§12.2), and offering it twice in a menu is offering a choice that does
        // not exist.
        /// <summary>The theme names in the folder, without extensions, alphabetically.</summary>
        /// <returns>Each name found, once however many formats spell it. Empty when the folder does not exist, which is the ordinary state of a fresh install.</returns>
        public IReadOnlyList<string> Names()
        {
            if (!Directory.Exists(Folder)) return Array.Empty<string>();

            return LunaTheme.Extensions
                .SelectMany(ext => Directory.EnumerateFiles(Folder, "*" + ext))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }

        // The first format that exists wins; a theme is one name, whatever it is written in.
        /// <summary>Opens one theme file by name, trying each extension in LunaTheme.Extensions in order.</summary>
        /// <param name="name">A theme name, without an extension.</param>
        /// <returns>The theme and the path it came from, or null when no file of that name exists. Reading a file that DOES exist may throw, which LunaTheme catches and reports.</returns>
        public ThemeDocument? Open(string name)
        {
            string? path = LunaTheme.Extensions
                .Select(ext => Path.Combine(Folder, name + ext))
                .FirstOrDefault(File.Exists);

            // Reading is deliberately NOT wrapped here. LunaTheme catches, reports and keeps the
            // theme already in force, and duplicating that would mean a read failure and a
            // missing file both arriving as null - which are different things a user needs told
            // apart.
            return path is null
                ? null
                : new ThemeDocument(name, Path.GetExtension(path), File.ReadAllText(path), path);
        }
    }
}
