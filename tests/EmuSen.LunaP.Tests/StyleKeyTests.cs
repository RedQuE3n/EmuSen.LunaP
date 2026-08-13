using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // STYLE KEY SPENT, CLASS BACK. The rule this file exists to keep, and the reason it is a test
    // rather than a paragraph - see docs/LunaP.md §30.
    //
    // A control that pins StyleKeyOverride is styled by Avalonia AS the stock control it names, and
    // that is not optional: without it the Fluent ControlTheme never reaches the subclass and the
    // control renders as nothing, or throws (§5.5, §14.1). What nobody wrote down for four releases
    // is the price. Avalonia matches a TYPE SELECTOR against the style key, so the same line also
    // makes `luna|MenuBar` unable to select a MenuBar - by construction, there is no control whose
    // style key is MenuBar. Every selector naming such a control matches nothing, silently.
    //
    // THAT DEFECT SHIPPED. §30 measured four CSS element names - menu-bar, luna-switch, dropdown,
    // tabs - that parsed, warned about nothing, and styled nothing, three of them since 0.2.0. And
    // MenuBar's own .axaml had been dead since §26.
    //
    // WHY THE OTHER GUARDS DO NOT COVER THIS, which is the whole argument for a third one:
    //
    //   - TemplateReachTests (§28.1) requires a visual tree. A control that borrows a template
    //     ALWAYS has one, however dead its own style is. It cannot see this class of defect at all.
    //   - CssThemeTests' vocabulary sweep (§30.4) catches it end to end, but only for a control that
    //     is IN the CSS vocabulary. Four of these eight are not.
    //
    // So the uncovered case is the one that actually happened: a control with an overridden style
    // key gains a .axaml style file, whose selector names the type, and the file is dead on arrival.
    // The .axaml cannot be read back to check - Avalonia's compiler strips it from the resource blob
    // (§28.1) - so the rule is enforced from the other end. Every such control carries a class, so
    // whatever selector is written next has something that can match, and the idiom is already there
    // to copy.
    public class StyleKeyTests
    {
        private static readonly Assembly Kit = typeof(SectionHeader).Assembly;

        // Controls whose constructor needs something, same table and same reason as
        // TemplateReachTests: a control that cannot be constructed gets a factory, never a skip.
        private static readonly Dictionary<Type, Func<Control>> Factories = new()
        {
            [typeof(ActionButton)] = () => new ActionButton(new LunaAction("Go")),
            [typeof(ActionToggle)] = () => new ActionToggle(new LunaAction("Grid") { IsCheckable = true }),
            [typeof(ActionMenuItem)] = () => new ActionMenuItem(new LunaAction("Open")),
        };

        // Found by reflection rather than listed, so a control added tomorrow is in this test the
        // moment it compiles. DeclaredOnly matters: the override has to be this type's own, not one
        // inherited from a base that already pinned a key.
        private static IEnumerable<Type> Overriding() =>
            Kit.GetTypes()
                .Where(t => t.Namespace == "EmuSen.LunaP.Controls" && t.IsPublic && !t.IsAbstract)
                .Where(t => t.GetProperty(
                    "StyleKeyOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is not null)
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        private static Type Close(Type type) =>
            type.IsGenericTypeDefinition ? type.MakeGenericType(typeof(string)) : type;

        public static TheoryData<string> Names()
        {
            var data = new TheoryData<string>();
            foreach (Type type in Overriding()) data.Add(type.FullName!);
            return data;
        }

        private static Control Make(Type type)
        {
            Type closed = Close(type);
            if (Factories.TryGetValue(closed, out Func<Control>? factory)) return factory();

            return (Control)(Activator.CreateInstance(closed)
                ?? throw new InvalidOperationException($"{closed.Name} could not be constructed."));
        }

        [Theory]
        [MemberData(nameof(Names))]
        public Task A_control_that_pins_a_style_key_publishes_the_class_that_replaces_it(string typeName) =>
            UiTest.Run(() =>
            {
                Type type = Kit.GetType(typeName)!;
                Type closed = Close(type);

                FieldInfo? field = closed.GetField("StyleClass", BindingFlags.Public | BindingFlags.Static);
                Assert.True(
                    field is { IsLiteral: true, FieldType: { } t } && t == typeof(string),
                    $"{Readable(type)} overrides StyleKeyOverride, so Avalonia styles it as the control it "
                    + "names and NO type selector can reach it - `luna|X` asks for a style key that does not "
                    + "exist and matches nothing, with no error. It must declare "
                    + "`public const string StyleClass` and add it to itself, so a selector has a class to "
                    + "name instead. See docs/LunaP.md §30.");

                var name = (string)field!.GetRawConstantValue()!;
                Assert.False(string.IsNullOrWhiteSpace(name), $"{Readable(type)}.StyleClass is blank.");

                Control control = Make(type);
                Assert.True(control.Classes.Contains(name),
                    $"{Readable(type)} declares StyleClass \"{name}\" and does not add it to itself, so the "
                    + "class no selector can do without is missing from every instance. Add "
                    + "`Classes.Add(StyleClass)` to its constructor. See docs/LunaP.md §30.");
            });

        // The kit is not allowed to shrink to nothing quietly: a refactor that removed every override
        // would leave a Theory with no cases, which xunit reports as a pass.
        [Fact]
        public void The_sweep_has_subjects()
        {
            Assert.True(Overriding().Count() >= 8,
                $"Only {Overriding().Count()} controls were found overriding StyleKeyOverride; there were "
                + "eight when §30 was written. If one was genuinely removed, lower this number and say so.");
        }

        private static string Readable(Type type) =>
            type.IsGenericTypeDefinition ? type.Name[..type.Name.IndexOf('`')] + "<T>" : type.Name;
    }
}
