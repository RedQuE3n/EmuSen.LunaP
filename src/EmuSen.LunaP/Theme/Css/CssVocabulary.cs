using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using EmuSen.LunaP.Controls;

namespace EmuSen.LunaP.Theme
{
    // THE VOCABULARY: every element name a theme may write, every state and part each one admits,
    // and every CSS property that maps to an Avalonia one.
    //
    // This is the half of CssTheme that grows. A new control adds an entry here and nowhere else in
    // the parser, which is why it is its own file (§29.4) - and it is also the half a consumer's
    // documentation test reads through ElementNames/PropertyNames, so an addition here is an
    // addition to a published vocabulary and gets a line in CHANGELOG.md (§26.13).
    public static partial class CssTheme
    {
        // Which control each element name selects, and the pseudo-classes and template parts it admits.
        //
        // StyleKey and StyleClass are the §30 correction. A control that pins StyleKeyOverride is
        // styled by Avalonia AS the type it names, and a type selector matches the STYLE KEY rather
        // than the runtime type - so `OfType(typeof(Dropdown))` asks for a control whose style key
        // is Dropdown, and there is never one. The rule then compiled, warned about nothing, and
        // styled nothing, which is worse than a rule that was refused because the author cannot
        // tell. Such an element selects its style-key type narrowed by the class the control adds
        // to itself, which reaches exactly the LunaP control and not the stock one it borrows from.
        private sealed record ElementSpec(
            Type Target,
            IReadOnlyDictionary<string, string> Classes,
            IReadOnlyDictionary<string, PartSpec> Parts,
            Type? StyleKey = null,
            string? StyleClass = null);

        // Shadowed lists the CSS properties on this part that a STATELESS rule can never win,
        // mapped to the advice to give instead - see docs/LunaP.md §40. A part whose colour comes
        // from state styles is the case: those bind at StyleTrigger, which outranks the Style
        // priority every host rule lands at, so the rule parses, matches, and loses in silence.
        // Naming it here turns that into a warning, which is the whole of §30's argument.
        private sealed record PartSpec(Type Target, string Name, IReadOnlyDictionary<string, string>? Shadowed = null);

        private static readonly IReadOnlyDictionary<string, string> NoClasses = new Dictionary<string, string>();
        private static readonly IReadOnlyDictionary<string, PartSpec> NoParts = new Dictionary<string, PartSpec>();

        private static readonly IReadOnlyDictionary<string, ElementSpec> Elements = BuildElements();

        // THE PALETTE HALF OF THE VOCABULARY, WHICH DID NOT EXIST UNTIL §79.4.
        //
        // A rule's element name and property name were both checked against the lists above, and a
        // ':root' declaration was checked for the '--luna-' prefix and nothing else. So
        // `--luna-surfce: #123456` parsed, invented a LunaSurfce resource nobody reads, left the real
        // LunaSurface at its default, and warned about none of it - a theme author looking at an
        // unchanged colour with no way to find out why. That is §30.5's silent failure in the one
        // place §30 did not sweep.
        //
        // REFLECTED OVER LunaPalette RATHER THAN LISTED HERE, because a list is a second place to
        // forget: adding a colour to Palette.axaml and LunaPalette is already guarded in both
        // directions by LunaPaletteTests (§2.1), and hanging the CSS vocabulary off the same fields
        // means a new token is spelled once and reaches all three. Selected by TYPE rather than by
        // name so LunaPalette's threshold constants - BusyPercent, HotPercent - are not mistaken for
        // colours; the two font sizes are taken by their suffix, which is the same rule the parser
        // itself uses to decide a token is a number.
        private static readonly IReadOnlySet<string> PaletteKeys = BuildPaletteKeys();

        private static IReadOnlySet<string> BuildPaletteKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (System.Reflection.FieldInfo field in typeof(LunaPalette).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                bool paints = typeof(Avalonia.Media.ISolidColorBrush).IsAssignableFrom(field.FieldType)
                    || field.FieldType == typeof(Avalonia.Media.FontFamily);

                bool sizes = field.FieldType == typeof(double)
                    && field.Name.EndsWith("FontSize", StringComparison.Ordinal);

                if (paints || sizes) keys.Add("Luna" + field.Name);
            }

            return keys;
        }

        // A token names a palette key, or the Color beside it: Palette.axaml spells every colour
        // twice and a theme may override either spelling, which the parser already handles by
        // writing both. See docs/LunaP.md §79.4.
        private static bool IsKnownToken(string resourceKey) =>
            PaletteKeys.Contains(resourceKey)
            || (resourceKey.EndsWith("Color", StringComparison.Ordinal)
                && PaletteKeys.Contains(resourceKey[..^"Color".Length]));

        /// <summary>Every palette token a theme's <c>:root</c> block may set, as written in CSS.</summary>
        public static IReadOnlyList<string> TokenNames =>
            PaletteKeys.Select(TokenFor).OrderBy(n => n, StringComparer.Ordinal).ToList();

        // Named after the control, so a rename moves the CSS name with it rather than leaving a selector that matches nothing.
        private static IReadOnlyDictionary<string, ElementSpec> BuildElements()
        {
            var specs = new[]
            {
                Element(typeof(SectionHeader)),
                Element(typeof(HintText)),
                Element(typeof(MonoText)),
                Element(typeof(ErrorText)),
                Element(typeof(MeterRow),
                    classes: new Dictionary<string, string> { ["nominal"] = ":nominal", ["busy"] = ":busy", ["hot"] = ":hot" },
                    parts: new Dictionary<string, PartSpec>
                    {
                        // The bar's colour is the one thing in the kit that a stateless rule can
                        // never set: MeterRow.axaml gives it three state styles, and a selector
                        // carrying a pseudo-class binds at StyleTrigger, above any host rule.
                        // Refused with advice rather than accepted and ignored (§40).
                        ["bar"] = new(typeof(ProgressBar), "PART_Bar", new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["color"] = "the :nominal, :busy and :hot state styles. Name the state - "
                                + "'meter-row.busy .bar { color: ... }' - or restyle all three at once "
                                + "by setting the --luna-nominal, --luna-busy and --luna-hot tokens",
                        }),
                    }),
                Element(typeof(MeterList)),
                Element(typeof(FieldRow)),
                Element(typeof(PathPickerRow)),
                Element(typeof(ButtonBar)),
                Element(typeof(StatusBar)),
                Element(typeof(FilterBar), parts: new Dictionary<string, PartSpec>
                {
                    ["search"] = new(typeof(TextBox), "PART_Search"),

                    // ComboBox, and NOT TextBlock, which is what this said until §39.3, and not
                    // Dropdown either. Two separate reasons, both of them §30's lesson:
                    //
                    //   PART_Facet is a Dropdown, so the TextBlock here named a type that is not in
                    //   the template - the selector matched nothing and `filter-bar .facet` was
                    //   silently dead, exactly like the four element names §30 found.
                    //   Dropdown pins StyleKeyOverride to ComboBox, and a type selector matches the
                    //   STYLE KEY rather than the runtime type, so naming Dropdown here would have
                    //   been dead in the same way for a different reason.
                    //
                    // The PART_ name is what makes this unambiguous, so no style class is needed as
                    // it is for the element selectors.
                    ["facet"] = new(typeof(ComboBox), "PART_Facet"),
                }),
                Element(typeof(ConsolePane), parts: new Dictionary<string, PartSpec>
                {
                    ["output"] = new(typeof(SelectableTextBlock), "PART_Output"),
                    ["input"] = new(typeof(TextBox), "PART_Input"),
                    ["prompt"] = new(typeof(TextBlock), "PART_Prompt"),
                }),
                Element(typeof(EmptyState), parts: new Dictionary<string, PartSpec>
                {
                    ["message"] = new(typeof(TextBlock), "PART_Message"),
                    ["detail"] = new(typeof(TextBlock), "PART_Detail"),
                }),
                Element(typeof(RgbaImageView)),

                // The four that borrow a stock control's theme, and therefore its style key. Each
                // one needs the key and the class or its rule reaches nothing at all - §30 measured
                // all four silently doing nothing before this line existed.
                Element(typeof(LunaSwitch), styleKey: typeof(ToggleSwitch), styleClass: LunaSwitch.StyleClass),
                Element(typeof(Dropdown), styleKey: typeof(ComboBox), styleClass: Dropdown.StyleClass),
                Element(typeof(Tabs), styleKey: typeof(TabControl), styleClass: Tabs.StyleClass),

                // The shell (§26). A theme reaches these by the same names the controls have -
                // menu-bar, tool-bar, card, split-pane, side-panel - and only through parts that
                // exist, which is what stops a rule silently matching nothing.
                Element(typeof(MenuBar), styleKey: typeof(Menu), styleClass: MenuBar.StyleClass),
                Element(typeof(ToolBar)),
                Element(typeof(Card), parts: new Dictionary<string, PartSpec>
                {
                    ["header"] = new(typeof(ContentPresenter), "PART_Header"),
                    ["content"] = new(typeof(ContentPresenter), "PART_Content"),
                }),
                Element(typeof(SplitPane), parts: new Dictionary<string, PartSpec>
                {
                    // The divider's own colour, for a theme that wants it louder or quieter than
                    // the border token - which is the one place a 3:1 default may reasonably be
                    // overridden downwards by somebody who has decided it for themselves.
                    ["rule"] = new(typeof(Border), "PART_Rule"),
                }),
                Element(typeof(SidePanel), parts: new Dictionary<string, PartSpec>
                {
                    ["title"] = new(typeof(TextBlock), "PART_Title"),
                    ["close"] = new(typeof(Button), "PART_Close"),
                    ["content"] = new(typeof(ContentPresenter), "PART_Content"),
                }),
            };

            return specs.ToDictionary(s => Kebab(s.Target.Name), s => s, StringComparer.Ordinal);
        }

        private static ElementSpec Element(
            Type target,
            IReadOnlyDictionary<string, string>? classes = null,
            IReadOnlyDictionary<string, PartSpec>? parts = null,
            Type? styleKey = null,
            string? styleClass = null) =>
            new(target, classes ?? NoClasses, parts ?? NoParts, styleKey, styleClass);

        private static readonly IReadOnlyDictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["color"] = "Foreground",
            ["background"] = "Background",
            ["background-color"] = "Background",
            ["font-family"] = "FontFamily",
            ["font-size"] = "FontSize",
            ["font-weight"] = "FontWeight",
        };
    }
}
