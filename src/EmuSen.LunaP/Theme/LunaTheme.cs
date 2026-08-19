using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using EmuSen.LunaP.Settings;

namespace EmuSen.LunaP.Theme
{
    // Which theme is in use, persisted next to the rest of the config.
    /// <summary>Which theme is in use, as persisted alongside the rest of a host's configuration.</summary>
    public sealed class ThemeChoice
    {
        /// <summary>The theme name the user last chose, as it appears in Available.</summary>
        public string Name { get; set; } = LunaTheme.BuiltIn;
    }

    // Loads a user theme over the built-in palette - see docs/LunaP.md §12.
    /// <summary>Loads and applies a user theme over the built-in palette.</summary>
    public static class LunaTheme
    {
        /// <summary>The name of the theme LunaP ships with, used when nothing has been chosen.</summary>
        public const string BuiltIn = "Built-in";
        /// <summary>The extension of an Avalonia styles theme file.</summary>
        public const string Extension = ".axaml";
        /// <summary>The extension of a restricted-CSS theme file.</summary>
        public const string CssExtension = ".css";

        // Tried in this order, so a name spelled both ways resolves to the .axaml - see docs/LunaP.md §12.2.
        /// <summary>The theme file extensions, tried in this order, so a name spelled both ways resolves to the .axaml.</summary>
        public static readonly IReadOnlyList<string> Extensions = new[] { Extension, CssExtension };

        /// <summary>The file the chosen theme name is remembered in.</summary>
        public const string ChoiceFileName = "luna.json";

        // The category the themes folder is, which is also the shape cheats/<name>.json already uses - see `man hier`.
        /// <summary>The settings category theme files are read from.</summary>
        public const string ThemeCategory = "themes";

        // The one dictionary a theme's keys live in; kept so applying a second theme replaces rather than stacks.
        private static ResourceDictionary? _applied;

        // The styles half, which only a .css theme produces; removed with the dictionary.
        private static Styles? _appliedStyles;

        /// <summary>The theme currently applied. BuiltIn until something else is applied successfully.</summary>
        public static string Current { get; private set; } = BuiltIn;

        // WHICH VARIANT THE PALETTE RESOLVES THROUGH, and it defaults to Dark rather than to the
        // system - see docs/LunaP.md §23.
        //
        // Dark is not a preference here, it is the absence of a behaviour change. Every consumer of
        // this toolkit has been dark since it existed, because the palette had no other column;
        // making it follow the desktop would turn a version bump into "the application looks
        // different now" for anybody on a light machine, and §9.1 already refused a base class that
        // altered behaviour by being inherited. The same argument applies to a palette that alters
        // behaviour by being upgraded.
        //
        // ThemeVariant.Default is the opt-in for following the desktop, and it is one line:
        //
        //     LunaTheme.Variant = ThemeVariant.Default;   // before LunaApp.Configure(...)
        //
        // This matters beyond LunaP's own keys. LunaTheme.axaml includes a bare <FluentTheme/>,
        // which follows the variant whatever LunaP does, so leaving the two to disagree is what
        // produced the dark-on-dark measured in §23.1.
        /// <summary>Light or dark. Set this before calling ApplyVariant; changing it afterwards does nothing on its own.</summary>
        public static ThemeVariant Variant { get; set; } = ThemeVariant.Dark;

        // Applied by LunaApp.Configure. Separate from ApplySaved so an application that builds its
        // own AppBuilder can still get the variant right without taking the theme loader too.
        /// <summary>Pushes Variant onto the application, so palette keys resolve to the right column.</summary>
        /// <param name="app">The application to set it on. Defaults to Application.Current.</param>
        public static void ApplyVariant(Application? app = null)
        {
            app ??= Application.Current;
            if (app is null) return;

            app.RequestedThemeVariant = Variant;
        }

        // Raised only when Application.Styles changed, which is the one case an open window must be restyled - see docs/LunaP.md §12.3.
        /// <summary>Raised after a theme is applied, for a window that has to re-read something the styles do not reach on their own.</summary>
        public static event Action? StylesChanged;

        // Detaching and reattaching the content is what re-runs the style pass over controls that are already realized.
        /// <summary>Forces a control tree to pick up styles applied after it was realised.</summary>
        /// <param name="root">The window or control to restyle. Its content is detached and reattached, which is what makes already-realised controls re-evaluate their styles.</param>
        public static void Restyle(ContentControl root)
        {
            object? content = root.Content;
            if (content is null) return;

            root.Content = null;
            root.Content = content;
        }

        // NOT created by asking - Available guards with Exists below for exactly that reason, and a
        // fresh install has no themes folder until something writes one. It said "created on
        // demand" until §80.3. A consumer telling a user where to drop a theme file should create
        // it: System.IO.Directory.CreateDirectory(LunaTheme.Directory).
        /// <summary>The folder theme files are read from, whether or not it exists yet.</summary>
        public static string Directory => LunaSettings.Store.Directory(ThemeCategory);

        // Built-in first, then whatever is on disk, alphabetically. A name is listed once however many formats spell it.
        /// <summary>The themes a user can choose, found by looking in the themes folder.</summary>
        /// <returns>BuiltIn first, then each theme file found, by name without its extension. A name present as both .axaml and .css appears once.</returns>
        public static IReadOnlyList<string> Available()
        {
            var names = new List<string> { BuiltIn };
            if (!System.IO.Directory.Exists(Directory)) return names;

            names.AddRange(Extensions
                .SelectMany(ext => System.IO.Directory.EnumerateFiles(Directory, "*" + ext))
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n) && !string.Equals(n, BuiltIn, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)!);

            return names;
        }

        // Applies and persists. False means the theme was unreadable and the previous one is still in force.
        /// <summary>Loads and applies a theme by name, leaving the current one in place if it cannot be read.</summary>
        /// <param name="name">A name from Available, or BuiltIn to go back to the shipped theme.</param>
        /// <returns>True if it was applied. False leaves Current untouched, so a bad theme file cannot leave the application unstyled.</returns>
        public static bool Apply(string name)
        {
            if (!TryApply(name)) return false;

            Current = name;
            LunaSettings.Store.Save(null, ChoiceFileName, new ThemeChoice { Name = name });
            return true;
        }

        // The saved name, whether or not it still resolves to a readable theme.
        /// <summary>The theme name remembered from last run, without applying it.</summary>
        public static string Saved => (LunaSettings.Store.Load<ThemeChoice>(null, ChoiceFileName) ?? new ThemeChoice()).Name;

        // Called once at startup. A theme since deleted or broken falls back to built-in without overwriting the saved choice,
        // so fixing the file and restarting is enough to get it back.
        /// <summary>Applies the theme remembered from last run. Called by the bootstrap, so an application using LunaApp.Configure needs no startup step.</summary>
        public static void ApplySaved()
        {
            if (TryApply(Saved)) return;

            TryApply(BuiltIn);
        }

        private static bool TryApply(string name)
        {
            if (Application.Current is not { } app) return false;

            if (string.Equals(name, BuiltIn, StringComparison.OrdinalIgnoreCase))
            {
                if (Remove(app)) StylesChanged?.Invoke();
                Current = BuiltIn;
                return true;
            }

            string? path = Resolve(name);
            if (path is null)
            {
                LunaSettings.Report($"theme '{name}' not found in {Directory}.");
                return false;
            }

            if (Read(path) is not { } content) return false;

            (ResourceDictionary loaded, Styles? styles) = content;
            bool touchedStyles = Remove(app);

            // Merged last, so its keys win over Theme/Palette.axaml's; every consumer uses DynamicResource and updates live.
            app.Resources.MergedDictionaries.Add(loaded);
            _applied = loaded;

            // Appended last for the same reason, so a theme's rules beat the Theme/Controls/ styles.
            if (styles is { Count: > 0 })
            {
                app.Styles.Add(styles);
                _appliedStyles = styles;
                touchedStyles = true;
            }

            Current = name;
            if (touchedStyles) StylesChanged?.Invoke();
            return true;
        }

        // The first format that exists wins; a theme is one name, whatever it is written in.
        private static string? Resolve(string name) =>
            Extensions.Select(ext => Path.Combine(Directory, name + ext)).FirstOrDefault(File.Exists);

        private static (ResourceDictionary Resources, Styles? Styles)? Read(string path)
        {
            try
            {
                return Path.GetExtension(path).Equals(CssExtension, StringComparison.OrdinalIgnoreCase)
                    ? ReadCss(path)
                    : AvaloniaRuntimeXamlLoader.Load(File.ReadAllText(path)) is ResourceDictionary dictionary
                        ? (dictionary, null)
                        : Reported(path, "the file is not a ResourceDictionary");
            }
            catch (Exception ex)
            {
                // A broken theme must never take the program down with it - the same rule Galaxia applies to config.
                return Reported(path, ex.Message);
            }
        }

        private static (ResourceDictionary, Styles?) ReadCss(string path)
        {
            CssThemeResult css = CssTheme.Parse(File.ReadAllText(path));

            // Skipped rules are reported but do not refuse the theme - see docs/LunaP.md §12.2.
            if (css.Warnings.Count > 0) LunaSettings.Report($"{path}: {string.Join(" ", css.Warnings)}");

            return (css.Resources, css.Styles);
        }

        private static (ResourceDictionary, Styles?)? Reported(string path, string why)
        {
            LunaSettings.Report($"{path}: {why} Falling back to the previous theme.");
            return null;
        }

        // True when Application.Styles was touched, which is the case a realized control cannot survive on its own.
        private static bool Remove(Application app)
        {
            bool touchedStyles = _appliedStyles is not null;
            if (_appliedStyles is not null)
            {
                app.Styles.Remove(_appliedStyles);
                _appliedStyles = null;
            }

            if (_applied is null) return touchedStyles;

            app.Resources.MergedDictionaries.Remove(_applied);
            _applied = null;
            return touchedStyles;
        }
    }
}
