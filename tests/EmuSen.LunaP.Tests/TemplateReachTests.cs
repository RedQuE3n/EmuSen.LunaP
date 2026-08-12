using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using EmuSen.LunaP.Commands;
using EmuSen.LunaP.Controls;
using EmuSen.LunaP.Theme;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // THE FOURTH-TIME GUARD - see docs/LunaP.md §28.1.
    //
    // A style that matches nothing is the most repeated defect in this repository. §5.5 recorded
    // it for ButtonBar, §14.1 for the three widget wrappers, §26.11 for the action controls, §27.3
    // for LunaTable<T>. All four look identical from outside: a control with no template, no
    // exception, and nothing on screen. All four were fixed by a paragraph asking the next author
    // to remember, which is why there were four.
    //
    // This is the assertion that would have caught every one of them, and it needs no author to
    // remember anything: EVERY CONCRETE TEMPLATED CONTROL IN THE KIT IS FOUND BY REFLECTION, put
    // in a window, shown, and required to have a visual tree. A control added tomorrow is in this
    // test the moment it compiles.
    //
    // WHY NOT LINT THE SELECTOR TEXT, which was the first attempt. Two reasons, the second
    // decisive. It would only catch the one cause - `luna|X` where `:is(luna|X)` was needed - and
    // "no template" has others: a missing StyleKeyOverride (§5.5), a style file not included, a
    // renamed part. And the text is not there to read: Avalonia's XAML compiler compiles
    // Controls.axaml to IL and STRIPS IT from the resource blob, so a packaged LunaP contains only
    // `avares://EmuSen.LunaP/!AvaloniaResourceXamlInfo`. Reading the .axaml would have meant
    // reading the developer's working copy and calling it the shipped artefact.
    public class TemplateReachTests
    {
        private static readonly Assembly Kit = typeof(SectionHeader).Assembly;

        // Controls whose constructor needs something. The point of naming them here rather than
        // skipping anything unconstructable is that a new control with a required argument fails
        // the completeness test below until somebody adds it - the list cannot be forgotten, only
        // extended.
        private static readonly Dictionary<Type, Func<Control>> Factories = new()
        {
            [typeof(ActionButton)] = () => new ActionButton(new LunaAction("Go")),
            [typeof(ActionToggle)] = () => new ActionToggle(new LunaAction("Grid") { IsCheckable = true }),
            [typeof(ActionMenuItem)] = () => new ActionMenuItem(new LunaAction("Open")),
        };

        // Every concrete templated control in the kit. TemplatedControl is exactly the right net:
        // ContentControl, ItemsControl, Menu and TabControl all derive from it, while SectionHeader,
        // HintText and MonoText are TextBlocks - styled by property setters, with no template to
        // fail to get - and fall outside it by construction rather than by an exclusion list.
        private static IEnumerable<Type> Kinds() =>
            Kit.GetTypes()
                .Where(t => t.Namespace == "EmuSen.LunaP.Controls")
                .Where(t => t.IsPublic && !t.IsAbstract && typeof(TemplatedControl).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        // A generic control is closed over string, which satisfies both `where T : class`
        // constraints in the kit. WHICH ARGUMENT DOES NOT MATTER AND THE CHOICE IS THE POINT: the
        // trap §27.3 records is that a style selector cannot name LunaTable<T> for any T, so if
        // the theme is wrong this fails for string exactly as it would for anything else.
        private static Type Close(Type type) =>
            type.IsGenericTypeDefinition ? type.MakeGenericType(typeof(string)) : type;

        public static TheoryData<string> Names()
        {
            var data = new TheoryData<string>();
            foreach (Type type in Kinds()) data.Add(type.FullName!);
            return data;
        }

        private static Control Make(Type type)
        {
            Type closed = Close(type);
            if (Factories.TryGetValue(closed, out Func<Control>? factory)) return factory();

            return (Control)(Activator.CreateInstance(closed)
                ?? throw new InvalidOperationException($"{closed.Name} could not be constructed."));
        }

        // THE GUARD. A control with no template has no visual children - no exception, no warning,
        // nothing drawn. That is the whole symptom, and it is one assertion.
        [Theory]
        [MemberData(nameof(Names))]
        public Task Every_templated_control_in_the_kit_gets_a_template(string typeName) => UiTest.Run(() =>
        {
            Type type = Kit.GetType(typeName)!;
            Control control = Make(type);

            var window = new ToolWindow { Width = 400, Height = 300, Content = control };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.True(control.GetVisualChildren().Any(),
                $"{Readable(type)} was shown and has no visual children, so no template reached it. "
                + "Either its style selector does not match (a bare `luna|X` selector is an EXACT type match and "
                + "never reaches a subclass or a generic - write `:is(luna|X)`), or it derives from a stock Avalonia "
                + "control without pinning StyleKeyOverride to that control. See docs/LunaP.md §28.1.");

            window.Close();
        });

        // The completeness half, and the reason the sweep above cannot be quietly narrowed: a
        // control that cannot be constructed has to be given a factory, not skipped. Without this,
        // the natural response to a control with a required constructor argument is a try/catch
        // that turns it into a silent pass.
        [Fact]
        public void Every_kind_can_be_constructed_or_has_a_factory()
        {
            var unbuildable = new List<string>();

            foreach (Type type in Kinds())
            {
                Type closed = Close(type);
                if (Factories.ContainsKey(closed)) continue;
                if (closed.GetConstructor(Type.EmptyTypes) is not null) continue;

                unbuildable.Add(Readable(type));
            }

            Assert.True(unbuildable.Count == 0,
                $"{string.Join(", ", unbuildable)} cannot be constructed and has no entry in Factories, "
                + "so the template sweep would not cover it. Add a factory rather than an exclusion.");
        }

        // The same exactness by the other route, and the one place it is still a live decision.
        // CssTheme.TryCompile builds OfType(spec.Target), so an element name for a subclassed type
        // would let a host write a rule that parses, reports no warning, and styles nothing -
        // worse than a rule that was refused, because the host has no way to tell.
        //
        // §27.3 left the OfType-versus-Is choice open rather than change what every existing
        // element name matches. THIS IS WHAT MAKES LEAVING IT OPEN SAFE: the vocabulary may go on
        // using OfType only while nothing in it is subclassed, and the day that stops being true
        // this fails and the choice gets made rather than discovered by a theme author.
        [Fact]
        public void Every_css_element_name_selects_a_type_nothing_derives_from()
        {
            var wrong = new List<string>();

            foreach (string element in CssTheme.ElementNames)
            {
                Type? target = Kit.GetTypes().FirstOrDefault(t => !t.IsGenericType && Kebab(t.Name) == element);
                if (target is null)
                {
                    wrong.Add($"'{element}' is in the CSS vocabulary but no type in the kit is named for it.");
                    continue;
                }

                string[] derived = Kit.GetTypes().Where(t => t != target && DerivesFrom(t, target)).Select(Readable).ToArray();
                if (derived.Length > 0)
                {
                    wrong.Add($"'{element}' compiles to OfType({target.Name}), but {string.Join(", ", derived)} "
                        + $"derive{(derived.Length == 1 ? "s" : "")} from it and would not be reached. "
                        + "Either keep the subclassed type out of the vocabulary or change CssTheme.TryCompile to Is(...).");
                }
            }

            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        }

        // Open generics are walked too: LunaTable<T>'s base chain reaches the abstract LunaTable
        // with no type argument supplied, which is exactly the relationship that has to be seen.
        private static bool DerivesFrom(Type candidate, Type target)
        {
            for (Type? at = candidate.BaseType; at is not null; at = at.BaseType)
            {
                if (at == target || (at.IsGenericType && at.GetGenericTypeDefinition() == target)) return true;
            }

            return false;
        }

        // LunaTable`1 reads as LunaTable<T>, so a failure names something findable in the source.
        private static string Readable(Type type) =>
            type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] + "<T>" : type.Name;

        // The same transform CssTheme applies to a type name to get its element name.
        private static string Kebab(string name) =>
            string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
