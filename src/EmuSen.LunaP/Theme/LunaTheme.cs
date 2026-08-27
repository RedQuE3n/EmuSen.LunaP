using System;
using System.Collections.Generic;
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

        // The category the themes folder is: one <name>.json per theme, under the settings root.
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

        // THE SEAM, AND KNOWINGLY A THIRD PROCESS-GLOBAL OF THE SAME FAMILY AS LunaSettings.Store.
        //
        // Worth naming rather than hiding: §86.3 records that filling a seam from ambient static
        // state is the thing this toolkit gets wrong, and this pass adds one more of them. Pass 2's
        // job is separating two responsibilities that were fused into one interface; where a seam
        // is installed FROM is pass 3's question and it threads all of them together. Doing both at
        // once makes one change nobody can review.
        //
        // Latched on first use rather than at type load, so a host that assigns one at startup is
        // never a moment too late - the same arrangement, and the same reason, as LunaSettings.Store.
        /// <summary>Where theme files are read from. Defaults to a FolderThemeSource over the application's themes folder. Process-global: set it once at startup.</summary>
        public static IThemeSource Source
        {
            get => _source ??= new FolderThemeSource(JsonSettingsStore.ForApplication().Directory(ThemeCategory));
            set => _source = value;
        }

        private static IThemeSource? _source;

        // NARROWED AT §86.11, AND THE SUMMARY SAYS SO RATHER THAN THE CHANGE BEING SILENT.
        //
        // This was `LunaSettings.Store.Directory(ThemeCategory)`, so the themes folder followed
        // whatever settings store a host had installed. That linkage is what made ISettingsStore
        // demand a filesystem (§86.5). Themes now come from `Source`, which need not be a folder at
        // all - so this answers the folder when it is one and empty when it is not.
        //
        // A consumer that wants the folder AND wants to create it should hold a FolderThemeSource
        // and call EnsureExists. The folder is still not created by asking, which is what §80.3
        // corrected the old summary to say.
        /// <summary>The folder theme files are read from when Source is a FolderThemeSource, whether or not it exists yet. Empty when Source is not a folder.</summary>
        public static string Directory => (Source as FolderThemeSource)?.Folder ?? string.Empty;

        // Built-in first, then whatever the source offers. A name is listed once however many formats spell it.
        /// <summary>The themes a user can choose, found by asking Source what it has.</summary>
        /// <returns>BuiltIn first, then each theme the source offers, by name without its extension. A name present as both .axaml and .css appears once.</returns>
        public static IReadOnlyList<string> Available()
        {
            var names = new List<string> { BuiltIn };
            try
            {
                names.AddRange(Source.Names()
                    .Where(n => !string.IsNullOrEmpty(n) && !string.Equals(n, BuiltIn, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                // A source that throws must not take the theme menu with it. An application that
                // cannot list themes should still open, still offer BuiltIn, and say why not.
                LunaSettings.Report($"{Source.Origin}: {ex.Message} No themes could be listed.");
            }

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

            if (Opened(name) is not { } document) return false;
            if (Read(document) is not { } content) return false;

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

        // TWO WAYS TO GET NOTHING, REPORTED DIFFERENTLY, because they need different things from
        // the person reading the message: "there is no theme by that name" is a name to correct,
        // "the source threw" is something broken.
        private static ThemeDocument? Opened(string name)
        {
            try
            {
                ThemeDocument? document = Source.Open(name);
                if (document is null) LunaSettings.Report($"theme '{name}' not found in {Source.Origin}.");
                return document;
            }
            catch (Exception ex)
            {
                // A source that cannot be read must never take the program down with it - the same
                // rule that has always applied to a broken theme file.
                LunaSettings.Report($"{Source.Origin}: {ex.Message} Falling back to the previous theme.");
                return null;
            }
        }

        private static (ResourceDictionary Resources, Styles? Styles)? Read(ThemeDocument document)
        {
            try
            {
                return document.Format.Equals(CssExtension, StringComparison.OrdinalIgnoreCase)
                    ? ReadCss(document)
                    : AvaloniaRuntimeXamlLoader.Load(document.Text) is ResourceDictionary dictionary
                        ? (dictionary, null)
                        : Reported(document.Origin, "the file is not a ResourceDictionary");
            }
            catch (Exception ex)
            {
                // A broken theme must never take the program down with it - the same rule Galaxia applies to config.
                return Reported(document.Origin, ex.Message);
            }
        }

        private static (ResourceDictionary, Styles?) ReadCss(ThemeDocument document)
        {
            CssThemeResult css = CssTheme.Parse(document.Text);

            // Skipped rules are reported but do not refuse the theme - see docs/LunaP.md §12.2.
            if (css.Warnings.Count > 0) LunaSettings.Report($"{document.Origin}: {string.Join(" ", css.Warnings)}");

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
