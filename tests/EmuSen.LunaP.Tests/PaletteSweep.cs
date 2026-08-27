using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Tests
{
    // THE ONE DEFINITION OF "PAINTS IN THIS TOOLKIT'S COLOURS" - see docs/LunaP.md §48 and §85.
    //
    // This was §48's measurement, written inline in FormControlTests and applied to nine named form
    // controls. §85 widened the subject to every control Avalonia ships, and two files asking the
    // same question in two hand-copied ways is one edit away from disagreeing about the answer - so
    // the measurement moved here and both callers ask it.
    //
    // WHAT COUNTS AS PAINTING, and why it is these three properties and not a pixel read. A control
    // resolves Background, Foreground and BorderBrush from the theme; those are the values a style
    // or a resource override actually moves, and reading them back gets the answer for every visual
    // in the tree including template parts a selector never named. Reading pixels instead would
    // fold in antialiasing, opacity and overlap, and could not say WHICH control was wrong.
    //
    // WHAT COUNTS AS CLEAN, since a loose reading makes the whole thing vacuous. Either the colour
    // appears in LunaPalette, or it is fully transparent - a control that paints nothing is not
    // painting Fluent's grey. Anything else is Fluent's palette showing through, which is the seam
    // §21.2 caught from the consumer's side and §48 set out to close.
    //
    // READING THE RESOLVED BRUSH RATHER THAN THE STYLE THAT SET IT IS THE POINT. A Setter that names
    // the right resource and never matches is the §5.5 symptom, and it reads identically to a
    // control that was styled correctly right up until somebody looks.
    internal static class PaletteSweep
    {
        // Every colour LunaPalette declares, by value. Read by reflection rather than listed, so a
        // palette that gains a colour does not need this file edited.
        //
        // THIS IS THE DARK COLUMN AND ONLY THE DARK COLUMN, which LunaPalette says about itself:
        // "a static field cannot follow a theme variant any more than it can follow a loaded theme".
        // It is the right set to compare a rendered control against, because the render sweeps run
        // in the default variant, which is Dark. It is the WRONG set to compare a resource against
        // in the light column, and doing so reports the correct light accent as a stray - see
        // PaletteFor, and §85.12 for the run where that actually happened.
        internal static HashSet<Color> Palette() =>
            typeof(LunaPalette)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => f.GetValue(null))
                .OfType<ISolidColorBrush>()
                .Select(b => b.Color)
                .ToHashSet();

        // Every colour actually painted anywhere in a control's realised visual tree, itself
        // included. The null-coalescing chains are because the three properties live on different
        // base types - TemplatedControl has all three, Panel and Border only a Background, TextBlock
        // only a Foreground - and a control is only ever one of them.
        internal static IEnumerable<(Visual Visual, string Property, Color Colour)> Painted(Visual root)
        {
            foreach (Visual visual in root.GetSelfAndVisualDescendants())
            {
                if (visual is not Control control) continue;

                foreach ((string name, IBrush? brush) in new (string, IBrush?)[]
                {
                    ("Background", (control as TemplatedControl)?.Background ?? (control as Panel)?.Background ?? (control as Border)?.Background),
                    ("Foreground", (control as TemplatedControl)?.Foreground ?? (control as TextBlock)?.Foreground),
                    ("BorderBrush", (control as TemplatedControl)?.BorderBrush ?? (control as Border)?.BorderBrush),
                })
                {
                    if (brush is ISolidColorBrush { Color: { A: > 0 } colour }) yield return (visual, name, colour);
                }
            }
        }

        // The colours a control paints that the palette does not contain, deduplicated and readable.
        // Empty means clean.
        internal static string[] Strays(Visual root)
        {
            HashSet<Color> palette = Palette();

            return Painted(root)
                .Where(p => !palette.Contains(p.Colour))
                .Select(p => $"{Readable(p.Visual.GetType())}.{p.Property} = {p.Colour}")
                .Distinct()
                .ToArray();
        }

        // The palette as the LIVE APPLICATION resolves it, in one variant. Built by enumerating
        // Theme/Palette.axaml's own keys and resolving each one, rather than by listing colours
        // here: the palette is spelled twice on purpose (§2.1) and this must not become a third.
        //
        // COLOUR KEYS ONLY, AND THAT IS THE WHOLE CORRECTNESS OF THIS METHOD. PaletteVariantTests
        // measured the trap: the palette's BRUSHES are declared once, outside the theme
        // dictionaries, with their Color bound by DynamicResource - so one brush instance serves
        // both variants and reports whichever is currently ACTIVE. Asking it for the light column
        // while the app is in dark mode hands back the dark colour, which looks like an answer and
        // is not one. The Color keys are per-variant and honest, so they are the ones read here.
        //
        // Taking both would not have failed loudly; it would have quietly built a set holding both
        // columns at once, which accepts a dark colour as a valid light one. §85.12.
        internal static HashSet<Color> PaletteFor(ThemeVariant variant)
        {
            var colours = new HashSet<Color>();

            foreach (string key in KeysOf(AvaloniaXamlLoader.Load(new Uri("avares://EmuSen.LunaP/Theme/Palette.axaml"))))
            {
                if (Application.Current!.TryFindResource(key, variant, out object? value) && value is Color colour)
                    colours.Add(colour);
            }

            return colours;
        }

        // Every string key declared anywhere in a resource tree, theme dictionaries included.
        //
        // StyleInclude AND ResourceInclude BOTH HAVE TO BE FOLLOWED, and forgetting them is not a
        // small miss: a walk without them reports the live application as having ZERO resources,
        // because every dictionary in it hangs off an include. That is what the first attempt at
        // this did, and a walk that returns nothing looks exactly like a walk that found nothing
        // wrong.
        internal static SortedSet<string> KeysOf(object? root)
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);

            void Walk(object? node, int depth)
            {
                if (node is null || depth > 14 || !seen.Add(node)) return;

                if (node is ResourceDictionary dictionary)
                {
                    foreach (object key in dictionary.Keys) if (key is string name) keys.Add(name);
                    foreach (IResourceProvider merged in dictionary.MergedDictionaries) Walk(merged, depth + 1);
                    foreach (KeyValuePair<ThemeVariant, IThemeVariantProvider> pair in dictionary.ThemeDictionaries)
                        Walk(pair.Value, depth + 1);
                }

                if (node is Styles styles)
                {
                    Walk(styles.Resources, depth + 1);
                    foreach (IStyle child in styles) Walk(child, depth + 1);
                }

                if (node is Style style)
                {
                    Walk(style.Resources, depth + 1);
                    foreach (IStyle child in style.Children) Walk(child, depth + 1);
                }

                if (node is ControlTheme theme)
                {
                    Walk(theme.Resources, depth + 1);
                    foreach (IStyle child in theme.Children) Walk(child, depth + 1);
                }

                if (node is StyleInclude styleInclude) Walk(styleInclude.Loaded, depth + 1);
                if (node is ResourceInclude resourceInclude) Walk(resourceInclude.Loaded, depth + 1);
            }

            Walk(root, 0);
            return keys;
        }

        // Generic control names arrive from reflection as "LunaList`1", which is not what anybody
        // writing the fix will grep for.
        internal static string Readable(Type type) =>
            type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] + "<T>" : type.Name;
    }
}
