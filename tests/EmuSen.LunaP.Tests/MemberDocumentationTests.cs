using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // WHAT EVERY MEMBER DOES, AND WHAT ITS ARGUMENTS MEAN, to the reader who has the DLL and the
    // .xml and nothing else - see docs/LunaP.md §41.
    //
    // §33 stopped at types, on an argument that was right and is not the whole story: CS1591 would
    // have demanded a sentence for all 460 public members, and 99 of them are Avalonia property
    // fields and protected framework overrides where the only available sentence restates the name.
    // §34.1 then wrote down what that left open, and it is most of the surface - `LunaTable<T>.Column`
    // takes a header, a projection and a width string, and the .xml said nothing about any of them.
    //
    // THE RULE THIS ENFORCES, chosen so it cannot be satisfied by filler:
    //
    //   1. Every member that is not excused below has a <summary>.
    //   2. Every parameter has a <param>. This is the half that carries the most and was entirely
    //      absent - a name and a type do not say whether a Func is called once or on every row.
    //   3. Every non-void return has a <returns>.
    //   4. None of the above is empty, a placeholder, or under twelve characters.
    //
    // WHAT IS EXCUSED, and why each one is not laziness:
    //
    //   Avalonia StyledProperty/DirectProperty/AttachedProperty fields - the backing field for a
    //   documented property. "The StyledProperty for Text" is the name, spelled longer.
    //   Protected overrides of framework methods - OnApplyTemplate's contract is Avalonia's, and
    //   restating it here is how it comes to disagree with Avalonia's.
    //   Compiler-generated members - record ToString/Equals/Deconstruct and friends are not ours.
    //   Property and event accessors - the property carries the sentence.
    //
    // THE TWO-WAY CHECK IS LOAD-BEARING. This test builds XML documentation IDs from reflection, and
    // a bug in that construction would report every member as undocumented forever - or, far worse,
    // silently match nothing and pass. So Every_documented_member_is_one_this_test_knows_about walks
    // the other way: every M:/P:/F:/E: key in the .xml must be one this file also generated. Get the
    // generic-arity or byref spelling wrong and that assertion says so by name.
    public class MemberDocumentationTests
    {
        public static TheoryData<string> Packages() => new() { "EmuSen.LunaP", "EmuSen.LunaP.Testing" };

        private const int Shortest = 12;

        [Theory]
        [MemberData(nameof(Packages))]
        public void Every_public_member_has_a_summary(string package)
        {
            Assembly assembly = Assembly.Load(package);
            Dictionary<string, XElement> docs = Documented(assembly);

            string[] missing = Subjects(assembly)
                .Where(m => !docs.ContainsKey(m.Id))
                .Select(m => m.Display)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToArray();

            Assert.True(missing.Length == 0,
                $"{missing.Length} public member(s) in {package} have no <summary>:\n\n  "
                + string.Join("\n  ", missing)
                + "\n\nA consumer sees these as a bare name and a type. Say what the member does, not "
                + "what it is called. See docs/LunaP.md §41.");
        }

        // The half §34.1 called out as entirely absent, and the half that carries the most: a
        // delegate parameter's type says nothing about when it is called or how often.
        [Theory]
        [MemberData(nameof(Packages))]
        public void Every_parameter_is_documented(string package)
        {
            Assembly assembly = Assembly.Load(package);
            Dictionary<string, XElement> docs = Documented(assembly);

            var missing = new List<string>();
            foreach (Subject subject in Subjects(assembly))
            {
                if (subject.Parameters.Length == 0) continue;
                if (!docs.TryGetValue(subject.Id, out XElement? doc)) continue;

                HashSet<string> named = doc.Elements("param")
                    .Select(p => (string?)p.Attribute("name") ?? string.Empty)
                    .ToHashSet(StringComparer.Ordinal);

                string[] absent = subject.Parameters.Where(p => !named.Contains(p)).ToArray();
                if (absent.Length > 0) missing.Add($"{subject.Display} -- no <param> for: {string.Join(", ", absent)}");
            }

            Assert.True(missing.Count == 0,
                $"{missing.Count} member(s) in {package} document some parameters and not others:\n\n  "
                + string.Join("\n  ", missing.OrderBy(m => m, StringComparer.Ordinal))
                + "\n\nSee docs/LunaP.md §41.");
        }

        [Theory]
        [MemberData(nameof(Packages))]
        public void Every_returning_member_says_what_it_returns(string package)
        {
            Assembly assembly = Assembly.Load(package);
            Dictionary<string, XElement> docs = Documented(assembly);

            string[] missing = Subjects(assembly)
                .Where(s => s.Returns)
                .Where(s => docs.TryGetValue(s.Id, out XElement? d) && d.Element("returns") is null)
                .Select(s => s.Display)
                .OrderBy(d => d, StringComparer.Ordinal)
                .ToArray();

            Assert.True(missing.Length == 0,
                $"{missing.Length} member(s) in {package} return something and do not say what:\n\n  "
                + string.Join("\n  ", missing)
                + "\n\nSee docs/LunaP.md §41.");
        }

        // Filler is the failure this whole guard invites, and it arrives pre-drifted - a sentence
        // that restated the name was never true of anything and never becomes false either.
        [Theory]
        [MemberData(nameof(Packages))]
        public void No_member_documentation_is_a_placeholder(string package)
        {
            XDocument doc = XDocument.Load(DocPath(Assembly.Load(package)));

            var bad = new List<string>();
            foreach (XElement member in doc.Descendants("member"))
            {
                string name = (string?)member.Attribute("name") ?? string.Empty;
                if (name.StartsWith("T:", StringComparison.Ordinal)) continue;

                foreach (XElement element in member.Elements())
                {
                    if (element.Name != "summary" && element.Name != "param" && element.Name != "returns") continue;

                    string text = Text(element);
                    string which = element.Name == "param"
                        ? $"<param name=\"{(string?)element.Attribute("name")}\">"
                        : $"<{element.Name}>";

                    if (text.Length < Shortest || text.Equals("TODO", StringComparison.OrdinalIgnoreCase))
                        bad.Add($"{name} {which} \"{text}\"");
                }
            }

            Assert.True(bad.Count == 0,
                $"{bad.Count} documentation element(s) in {package} are empty or too short to say "
                + $"anything (under {Shortest} characters):\n\n  "
                + string.Join("\n  ", bad.OrderBy(b => b, StringComparer.Ordinal))
                + "\n\nSee docs/LunaP.md §41.");
        }

        // THE GUARD ON THE GUARD. Every documented member key must be one Subjects() also produced.
        // A key in the .xml that this test never generates means either the ID construction below is
        // wrong - in which case the three assertions above are quietly checking nothing - or somebody
        // documented a member the exclusions claim is excused, which is worth knowing either way.
        [Theory]
        [MemberData(nameof(Packages))]
        public void Every_documented_member_is_one_this_test_knows_about(string package)
        {
            Assembly assembly = Assembly.Load(package);
            HashSet<string> known = Subjects(assembly).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

            // Excused members AND members of non-public types. Documenting an internal type is good
            // practice and must not fail here - ActionSync is internal and carries the explanation
            // of how a control follows an action, which is worth having. What this assertion is
            // actually for is an ID this file cannot construct, and a wrong ID is in neither set.
            HashSet<string> excused = Excused(assembly).ToHashSet(StringComparer.Ordinal);

            string[] unknown = Documented(assembly).Keys
                .Where(k => !k.StartsWith("T:", StringComparison.Ordinal)) // types are DocumentationTests' subject
                .Where(k => !known.Contains(k) && !excused.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            Assert.True(unknown.Length == 0,
                $"{unknown.Length} documented member(s) in {package} are not members this test knows "
                + "how to name:\n\n  " + string.Join("\n  ", unknown)
                + "\n\nEither the XML-ID construction in MemberDocumentationTests is wrong - and the "
                + "other assertions here are checking less than they appear to - or a member the "
                + "exclusions call excused was documented anyway. See docs/LunaP.md §41.4.");
        }

        // ---------------------------------------------------------------- subjects and exclusions

        private sealed record Subject(string Id, string Display, string[] Parameters, bool Returns);

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static IEnumerable<Subject> Subjects(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes().Where(Documentable))
            {
                foreach (MemberInfo member in type.GetMembers(Flags))
                {
                    if (!Exposed(member) || IsExcused(member)) continue;

                    switch (member)
                    {
                        case MethodInfo method:
                            yield return new Subject(Id(method), Display(type, method),
                                method.GetParameters().Select(p => p.Name!).ToArray(),
                                method.ReturnType != typeof(void));
                            break;

                        case ConstructorInfo ctor:
                            yield return new Subject(Id(ctor), Display(type, ctor),
                                ctor.GetParameters().Select(p => p.Name!).ToArray(), false);
                            break;

                        case PropertyInfo property:
                            yield return new Subject("P:" + DocType(type) + "." + property.Name,
                                $"{Name(type)}.{property.Name}",
                                property.GetIndexParameters().Select(p => p.Name!).ToArray(), false);
                            break;

                        case FieldInfo field:
                            yield return new Subject("F:" + DocType(type) + "." + field.Name,
                                $"{Name(type)}.{field.Name}", Array.Empty<string>(), false);
                            break;

                        case EventInfo evt:
                            yield return new Subject("E:" + DocType(type) + "." + evt.Name,
                                $"{Name(type)}.{evt.Name}", Array.Empty<string>(), false);
                            break;
                    }
                }
            }
        }

        // The excused members, as doc IDs, so the two-way check can tell "this test cannot name it"
        // from "this test deliberately does not ask for it".
        private static IEnumerable<string> Excused(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                bool documentable = Documentable(type);
                foreach (MemberInfo member in type.GetMembers(Flags))
                {
                    if (documentable && (!Exposed(member) || !IsExcused(member))) continue;

                    yield return member switch
                    {
                        MethodInfo m => Id(m),
                        ConstructorInfo c => Id(c),
                        PropertyInfo p => "P:" + DocType(type) + "." + p.Name,
                        FieldInfo f => "F:" + DocType(type) + "." + f.Name,
                        EventInfo e => "E:" + DocType(type) + "." + e.Name,
                        _ => "?:" + member.Name,
                    };
                }
            }
        }

        private static bool Exposed(MemberInfo member) => member switch
        {
            MethodBase m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
            FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
            PropertyInfo p => Exposed(p.GetMethod ?? (MemberInfo)p.SetMethod!),
            EventInfo e => Exposed(e.AddMethod!),
            _ => false,
        };

        private static bool IsExcused(MemberInfo member)
        {
            if (member.Name.Contains('<') || member.Name.Contains('$')) return true;
            if (member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) return true;

            switch (member)
            {
                // An enum's `value__` is the storage the compiler emits for it. There is nowhere to
                // put a /// on it and nothing to say; the enum type and its members carry the
                // meaning. It is the only IsSpecialName field in either package.
                case FieldInfo f when f.IsSpecialName:
                    return true;

                // The backing field for a property that carries the sentence already (§33.2).
                case FieldInfo field:
                    string field_type = field.FieldType.Name;
                    return field_type.StartsWith("StyledProperty", StringComparison.Ordinal)
                        || field_type.StartsWith("DirectProperty", StringComparison.Ordinal)
                        || field_type.StartsWith("AttachedProperty", StringComparison.Ordinal)
                        || field_type.StartsWith("RoutedEvent", StringComparison.Ordinal);

                case MethodInfo method:
                    // Accessors: the property or event is the subject.
                    if (method.IsSpecialName) return true;

                    // A framework override answers to Avalonia's contract, not to one written here.
                    return method.GetBaseDefinition().DeclaringType != method.DeclaringType;

                // The same argument as an overridden method, and it is not hypothetical: every
                // control that borrows a stock template overrides StyleKeyOverride, and the only
                // sentence available restates Avalonia's own documentation for it (§30).
                case PropertyInfo property when property.GetMethod is { } getter:
                    return getter.GetBaseDefinition().DeclaringType != property.DeclaringType;

                // NOT REQUIRED, BUT PERMITTED - §33.2's trade, applied again. `new ButtonBar()`
                // takes no arguments and makes the type it is named after, so the only sentence
                // available restates the type summary the reader was just given. A constructor WITH
                // parameters is a different thing entirely: the parameters carry decisions, and
                // those are required.
                //
                // Excused rather than skipped, so one that does carry something - ToolWindow's,
                // which is where "restores its own position and closes on Escape" belongs - still
                // passes the two-way check rather than being reported as documenting a ghost.
                case ConstructorInfo ctor when ctor.GetParameters().Length == 0:
                    return true;

                default:
                    return false;
            }
        }

        private static bool Documentable(Type type)
        {
            if (type.Name.Contains('<') || type.Name.Contains('$')) return false;
            if (type.Namespace?.StartsWith("CompiledAvaloniaXaml", StringComparison.Ordinal) == true) return false;
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) return false;

            return type.IsPublic || type.IsNestedPublic;
        }

        // ------------------------------------------------------------------ XML documentation IDs

        private static string Id(MethodInfo method)
        {
            string generics = method.IsGenericMethodDefinition
                ? "``" + method.GetGenericArguments().Length
                : string.Empty;

            return "M:" + DocType(method.DeclaringType!) + "." + method.Name + generics + Signature(method);
        }

        private static string Id(ConstructorInfo ctor) =>
            "M:" + DocType(ctor.DeclaringType!) + ".#ctor" + Signature(ctor);

        private static string Signature(MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0) return string.Empty;

            return "(" + string.Join(",", parameters.Select(p => DocParameter(p.ParameterType))) + ")";
        }

        // The spelling in the .xml: nested types joined by a dot, generic arity as `1 on the type,
        // and a generic ARGUMENT as `0 or ``0 depending on whether it came from the type or the
        // method. Byref and out become a trailing @; arrays keep their brackets.
        private static string DocType(Type type)
        {
            string name = (type.FullName ?? type.Name).Replace('+', '.');
            int tick = name.IndexOf('`');
            return tick < 0 ? name : name;
        }

        private static string DocParameter(Type type)
        {
            if (type.IsByRef) return DocParameter(type.GetElementType()!) + "@";
            if (type.IsArray) return DocParameter(type.GetElementType()!) + "[]";
            if (type.IsGenericParameter)
                return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;

            if (!type.IsGenericType)
                return (type.FullName ?? type.Name).Replace('+', '.');

            string name = type.GetGenericTypeDefinition().FullName!.Replace('+', '.');
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];

            return name + "{" + string.Join(",", type.GetGenericArguments().Select(DocParameter)) + "}";
        }

        private static string Name(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            return tick < 0 ? name : name[..tick] + "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.Name)) + ">";
        }

        private static string Display(Type type, MethodBase method)
        {
            string name = method is ConstructorInfo ? Name(type) : method.Name;
            var sb = new StringBuilder(Name(type)).Append('.').Append(name).Append('(');
            sb.Append(string.Join(", ", method.GetParameters().Select(p => $"{Short(p.ParameterType)} {p.Name}")));
            return sb.Append(')').ToString();
        }

        private static string Short(Type type)
        {
            if (type.IsByRef) return Short(type.GetElementType()!);
            if (type.IsArray) return Short(type.GetElementType()!) + "[]";
            if (!type.IsGenericType) return type.Name;

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(Short)) + ">";
        }

        // ---------------------------------------------------------------------------- the .xml

        private static Dictionary<string, XElement> Documented(Assembly assembly) =>
            XDocument.Load(DocPath(assembly)).Descendants("member")
                .Where(m => m.Attribute("name") is not null)
                .GroupBy(m => (string)m.Attribute("name")!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        private static string Text(XElement element) =>
            string.Concat(element.Nodes().Select(n => n switch
            {
                XText t => t.Value,
                XElement e => (string?)e.Attribute("cref") ?? (string?)e.Attribute("name") ?? e.Value,
                _ => string.Empty,
            })).Trim();

        private static string DocPath(Assembly assembly)
        {
            string path = Path.ChangeExtension(assembly.Location, ".xml");
            Assert.True(File.Exists(path),
                $"No XML documentation beside {Path.GetFileName(assembly.Location)}. See docs/LunaP.md §33.");
            return path;
        }
    }
}
