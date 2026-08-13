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

        private sealed record PartSpec(Type Target, string Name);

        private static readonly IReadOnlyDictionary<string, string> NoClasses = new Dictionary<string, string>();
        private static readonly IReadOnlyDictionary<string, PartSpec> NoParts = new Dictionary<string, PartSpec>();

        private static readonly IReadOnlyDictionary<string, ElementSpec> Elements = BuildElements();

        // Named after the control, so a rename moves the CSS name with it rather than leaving a selector that matches nothing.
        private static IReadOnlyDictionary<string, ElementSpec> BuildElements()
        {
            var specs = new[]
            {
                Element(typeof(SectionHeader)),
                Element(typeof(HintText)),
                Element(typeof(MonoText)),
                Element(typeof(MeterRow),
                    classes: new Dictionary<string, string> { ["nominal"] = ":nominal", ["busy"] = ":busy", ["hot"] = ":hot" },
                    parts: new Dictionary<string, PartSpec> { ["bar"] = new(typeof(ProgressBar), "PART_Bar") }),
                Element(typeof(MeterList)),
                Element(typeof(FieldRow)),
                Element(typeof(PathPickerRow)),
                Element(typeof(ButtonBar)),
                Element(typeof(StatusBar)),
                Element(typeof(FilterBar), parts: new Dictionary<string, PartSpec>
                {
                    ["search"] = new(typeof(TextBox), "PART_Search"),
                    ["facet"] = new(typeof(TextBlock), "PART_Facet"),
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
