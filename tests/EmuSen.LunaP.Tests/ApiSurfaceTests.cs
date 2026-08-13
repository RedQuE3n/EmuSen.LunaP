using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // THE PUBLIC SURFACE, WRITTEN DOWN, so that changing it is a thing somebody did rather than a
    // thing that happened - see docs/LunaP.md §32.
    //
    // CLAUDE.md opens by saying a consumer cannot patch this and can only take a version. Everything
    // in this repository follows from that sentence, and yet the surface it describes was the one
    // thing nothing checked: a renamed parameter, a widened return type, a property quietly turned
    // internal, and the first person to find out is somebody whose build broke on `dotnet update`.
    //
    // WHY A REFLECTED SNAPSHOT AND NOT Microsoft.CodeAnalysis.PublicApiAnalyzers, which is the
    // obvious tool and was the first plan. Three reasons, in order of weight:
    //
    //   1. It is a PackageReference, and CLAUDE.md is explicit that one is a decision carrying a §
    //      and a licence. The analyzer would be PrivateAssets="all" and reach no consumer, so the
    //      layering rule survives - but "no dependency at all" is strictly better than "a dependency
    //      that does not escape", and here it costs about eighty lines.
    //   2. This repository already has the mechanism. EMUSEN_UI_BASELINE writes render baselines
    //      from an environment variable (§10.2); EMUSEN_API_APPROVE writes this one the same way, so
    //      there is one convention for "regenerate the thing I am asserting against" rather than two.
    //   3. The guards this suite already trusts find their own subjects by reflection -
    //      TemplateReachTests (§28.1), TemplateOrderTests (§28.2), StyleKeyTests (§30.6). This is
    //      the same shape, and a reader who understands one understands all four.
    //
    // AND THE BASELINE IS COMMITTED, unlike the render ones, which is worth being clear about
    // because §10.2 says the opposite in a nearly identical sentence. A .frame baseline is one
    // machine's font rendering and is worthless elsewhere. An API surface is the same on every
    // machine that compiles the code, so it is a reviewable artefact and belongs in the diff - it
    // IS the review.
    public class ApiSurfaceTests
    {
        // Both packages. The harness is published too, so a consumer's test project takes a version
        // of it and can be broken by it exactly as an application can be broken by the toolkit.
        public static TheoryData<string> Packages() => new() { "EmuSen.LunaP", "EmuSen.LunaP.Testing" };

        private const string ApproveVariable = "EMUSEN_API_APPROVE";

        [Theory]
        [MemberData(nameof(Packages))]
        public void The_public_surface_is_what_the_baseline_says(string package)
        {
            Assembly assembly = Assembly.Load(package);
            string actual = Describe(assembly);
            string path = BaselinePath(package);

            // Regeneration is deliberate and loud, the same bargain §10.2 struck: you cannot approve
            // a change by accident, and the approval is a file in the diff that somebody reviews.
            if (Environment.GetEnvironmentVariable(ApproveVariable) == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, actual);
                return;
            }

            Assert.True(File.Exists(path),
                $"No API baseline for {package} at {path}. Generate it with {ApproveVariable}=1 dotnet test.");

            string expected = File.ReadAllText(path);
            if (string.Equals(Normalise(expected), Normalise(actual), StringComparison.Ordinal)) return;

            Assert.Fail(
                $"{package}'s public surface no longer matches its baseline.\n\n"
                + Diff(Normalise(expected), Normalise(actual))
                + $"\n\nEvery line here is something a consumer can see and cannot patch. If the change is "
                + $"intended, re-run with {ApproveVariable}=1 dotnet test and COMMIT THE BASELINE - the diff "
                + "on that file is the review, and it belongs in CHANGELOG.md too if anything was removed "
                + "or renamed. See docs/LunaP.md §32.");
        }

        // THE BASELINE CANNOT GUARD ITS OWN RENDERER. Delete the nullability walk and regenerate,
        // and the file agrees with itself again while quietly saying less than it did - which is
        // the failure §32.5 described in the first place, reintroduced by the fix for it.
        //
        // So the three properties that were hard to get right are asserted against the description
        // directly, on real members chosen because each one breaks a different plausible shortcut.
        [Fact]
        public void The_snapshot_records_nullability_in_both_directions()
        {
            string surface = Describe(typeof(EmuSen.LunaP.Settings.ISettingsStore).Assembly);

            // A non-null return and a nullable parameter ON ONE MEMBER. A renderer that reads the
            // wrong state, or hard-codes either answer, gets exactly one half of this line wrong.
            Assert.Contains("public abstract string Directory(string? category)", surface, StringComparison.Ordinal);

            // Nullability INSIDE a generic argument - the part that is not a one-liner, since
            // Func<T, object?> and Func<T, object>? are different contracts and neither is Func<T, object>.
            Assert.Contains("public System.Func<T, object?> Key { get; set; }", surface, StringComparison.Ordinal);

            // Nullable<T> is spelled by the type itself, so asking the annotation for a `?` as well
            // renders `int??`. Nothing in either package should ever produce one.
            Assert.DoesNotContain("??", surface, StringComparison.Ordinal);
        }

        private static string Normalise(string text) => text.Replace("\r\n", "\n").TrimEnd();

        // GROUPED BY TYPE, and counted rather than set-differenced. The first version of this method
        // compared the two files as flat sets of lines, and the first sabotage run against it - a
        // `public void Poke()` added to Card - reported "(only ordering changed)" and named nothing,
        // because Threading.Debounce already has a method with that exact signature and a set
        // difference cannot see that one line became two.
        //
        // A member signature is only unique WITHIN its type, which is the whole reason the report is
        // built the same way the file is: type header, then its own members. "Card gained
        // `public void Poke()`" is the sentence somebody reviewing a version bump needs; a bare line
        // with no owner is not.
        private static string Diff(string expected, string actual)
        {
            var want = Parse(expected);
            var got = Parse(actual);
            var sb = new StringBuilder();

            foreach (string name in want.Keys.Except(got.Keys).OrderBy(n => n, StringComparer.Ordinal))
                sb.Append("  TYPE GONE: ").Append(name).Append('\n');
            foreach (string name in got.Keys.Except(want.Keys).OrderBy(n => n, StringComparer.Ordinal))
                sb.Append("  TYPE NEW:  ").Append(name).Append('\n');

            foreach (string name in want.Keys.Intersect(got.Keys).OrderBy(n => n, StringComparer.Ordinal))
            {
                (string wantHead, List<string> wantMembers) = want[name];
                (string gotHead, List<string> gotMembers) = got[name];

                var lines = new List<string>();

                // A changed base type or interface list is as breaking as a removed method.
                if (!string.Equals(wantHead, gotHead, StringComparison.Ordinal))
                {
                    lines.Add("      was: " + wantHead);
                    lines.Add("      now: " + gotHead);
                }

                foreach (string member in Missing(wantMembers, gotMembers)) lines.Add("      GONE: " + member);
                foreach (string member in Missing(gotMembers, wantMembers)) lines.Add("      NEW:  " + member);

                if (lines.Count == 0) continue;
                sb.Append("  ").Append(name).Append('\n');
                foreach (string line in lines) sb.Append(line).Append('\n');
            }

            if (sb.Length == 0) sb.Append("  (the files differ only in whitespace or ordering)\n");
            return sb.ToString();
        }

        // Multiset difference: a signature present twice on one side and once on the other is a
        // real change, and Except would swallow it.
        private static IEnumerable<string> Missing(List<string> from, List<string> other)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in other) counts[line] = counts.GetValueOrDefault(line) + 1;

            foreach (string line in from)
            {
                if (counts.GetValueOrDefault(line) > 0) counts[line]--;
                else yield return line;
            }
        }

        // The file's own shape: a type header in column zero, its members indented under it.
        private static Dictionary<string, (string Header, List<string> Members)> Parse(string text)
        {
            var map = new Dictionary<string, (string, List<string>)>(StringComparer.Ordinal);
            string current = string.Empty;
            List<string> members = new();

            foreach (string raw in text.Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                if (raw[0] == ' ')
                {
                    members.Add(raw.Trim());
                    continue;
                }

                // Keyed on the name alone, so a changed base type reports as a header change on the
                // same type rather than as one type vanishing and another appearing.
                int at = raw.IndexOf(" : ", StringComparison.Ordinal);
                current = at < 0 ? raw : raw[..at];
                members = new List<string>();
                map[current] = (raw, members);
            }

            return map;
        }

        // Walks up from the test binary to the project that owns it, so approval writes to the file
        // in the working copy rather than to the copy under bin/ that nobody would ever commit.
        private static string BaselinePath(string package)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EmuSen.LunaP.Tests.csproj")))
                dir = dir.Parent;

            if (dir is null) throw new InvalidOperationException("EmuSen.LunaP.Tests.csproj was not found above the test binary.");
            return Path.Combine(dir.FullName, "ApiSurface", package + ".txt");
        }

        // ------------------------------------------------------------------ the description itself

        // Public AND protected. A protected member is API too: this toolkit is meant to be
        // subclassed - ToolWindow, PollingWindow and AppWindow exist to be - and removing a
        // protected method breaks a consumer just as loudly as removing a public one.
        // NULLABILITY IS PART OF THE SIGNATURE, and until §39 this file could not see it - `string?`
        // and `string` were the same line, so tightening a parameter (source-breaking for any
        // consumer with <Nullable>enable</Nullable>) approved itself in silence. §32.5 named this as
        // the first extension the file should get.
        //
        // NullabilityInfoContext is not thread-safe and is documented as such, so one is made per
        // description rather than shared in a static.
        //
        // READ STATE OR WRITE STATE, which is a real choice and not a detail. A member can differ:
        // `string? Name { get; set; }` on a type that never returns null reads non-null and writes
        // nullable. This file reports what a CONSUMER IS PROMISED, because that is what breaks them:
        //
        //   returns, fields, properties, out parameters -> ReadState, what they get back
        //   ordinary parameters                         -> WriteState, what they may pass in
        //
        // Get that backwards and the guard reports the wrong direction of change as safe.
        private static string Describe(Assembly assembly)
        {
            var nullability = new NullabilityInfoContext();

            var sb = new StringBuilder();
            sb.Append("# ").Append(assembly.GetName().Name).Append(" public API surface\n");
            sb.Append("# Generated by ApiSurfaceTests. Do not hand-edit; see docs/LunaP.md §32.\n");

            foreach (Type type in assembly.GetTypes()
                         .Where(Visible)
                         .OrderBy(t => t.Namespace, StringComparer.Ordinal)
                         .ThenBy(Name, StringComparer.Ordinal))
            {
                sb.Append('\n').Append(TypeLine(type)).Append('\n');
                foreach (string member in Members(type, nullability)) sb.Append("    ").Append(member).Append('\n');
            }

            return sb.ToString();
        }

        // THE XAML COMPILER EMITS PUBLIC TYPES, and they are not API. Avalonia's compiler generates
        // CompiledAvaloniaXaml.!AvaloniaResources with a Build:/ and a Populate:/ method PER .axaml
        // FILE, plus !XamlLoader and two XamlIlContext helpers. Nothing can call them by name - the
        // method names contain ':' and '!' - and they change whenever a theme file is added or
        // renamed, which §29 did sixteen times in one commit. Left in, the baseline would have
        // churned thirty-odd lines for a refactor that changed no API at all, and a baseline that
        // cries wolf is a baseline somebody approves without reading.
        //
        // Excluded by their own namespace rather than by "keep only EmuSen.LunaP.*", deliberately: a
        // public type that turns up in some third namespace is exactly the kind of leak this file
        // should show, so the filter names what it removes instead of what it keeps.
        private static bool Visible(Type type)
        {
            if (type.Name.Contains('<') || type.Name.Contains('$')) return false;
            if (type.Namespace?.StartsWith("CompiledAvaloniaXaml", StringComparison.Ordinal) == true) return false;
            if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)) return false;

            return type.IsPublic || type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
        }

        private static string TypeLine(Type type)
        {
            var parts = new List<string>();
            if (type.IsNestedFamily || type.IsNestedFamORAssem) parts.Add("protected"); else parts.Add("public");

            if (type.IsEnum) parts.Add("enum");
            else if (type.IsInterface) parts.Add("interface");
            else if (type.IsValueType) parts.Add("struct");
            else
            {
                if (type.IsAbstract && type.IsSealed) parts.Add("static");
                else
                {
                    if (type.IsAbstract) parts.Add("abstract");
                    if (type.IsSealed) parts.Add("sealed");
                }

                parts.Add("class");
            }

            string head = string.Join(' ', parts) + " " + FullName(type);

            // The base type is part of the contract: a control that stops deriving from
            // TemplatedControl breaks every selector and every `is` check a consumer wrote.
            var bases = new List<string>();
            if (!type.IsEnum && !type.IsInterface && type.BaseType is { } b && b != typeof(object))
                bases.Add(FullName(b));
            bases.AddRange(DeclaredInterfaces(type).Select(FullName).OrderBy(n => n, StringComparer.Ordinal));

            return bases.Count == 0 ? head : head + " : " + string.Join(", ", bases);
        }

        // ONLY WHAT THIS TYPE ADDS. GetInterfaces() is transitive, so every control would list the
        // fifteen interfaces it inherits from Avalonia's Control - unreadable, and worse, it would
        // make an Avalonia upgrade that adds one interface to a base class churn every line in this
        // file. What a LunaP type declares for itself is a LunaP decision; what it inherits is
        // Avalonia's, and is recorded once as the base type.
        private static IEnumerable<Type> DeclaredInterfaces(Type type)
        {
            var inherited = new HashSet<Type>(type.BaseType?.GetInterfaces() ?? Array.Empty<Type>());
            Type[] all = type.GetInterfaces();
            foreach (Type i in all)
            {
                foreach (Type via in i.GetInterfaces()) inherited.Add(via);
            }

            return all.Where(i => i.IsPublic && !inherited.Contains(i));
        }

        private static IEnumerable<string> Members(Type type, NullabilityInfoContext nullability)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var lines = new List<string>();

            foreach (FieldInfo f in type.GetFields(Flags).Where(f => Exposed(f.IsPublic, f.IsFamily, f.IsFamilyOrAssembly)))
            {
                if (f.Name.Contains('<')) continue;
                string mods = Access(f.IsPublic, f.IsFamily, f.IsFamilyOrAssembly);
                if (f.IsLiteral) mods += " const";
                else
                {
                    if (f.IsStatic) mods += " static";
                    if (f.IsInitOnly) mods += " readonly";
                }

                lines.Add($"{mods} {Render(f.FieldType, nullability.Create(f), read: true)} {f.Name}"
                    + (f.IsLiteral ? $" = {Literal(f.GetRawConstantValue())}" : string.Empty));
            }

            foreach (PropertyInfo p in type.GetProperties(Flags))
            {
                MethodInfo? get = p.GetMethod, set = p.SetMethod;
                var accessors = new List<string>();
                if (get is not null && Exposed(get.IsPublic, get.IsFamily, get.IsFamilyOrAssembly)) accessors.Add("get");
                if (set is not null && Exposed(set.IsPublic, set.IsFamily, set.IsFamilyOrAssembly)) accessors.Add("set");
                if (accessors.Count == 0) continue;

                MethodInfo any = get ?? set!;
                string mods = Access(any.IsPublic, any.IsFamily, any.IsFamilyOrAssembly);
                if (any.IsStatic) mods += " static";
                if (any.IsAbstract) mods += " abstract";
                else if (any.IsVirtual && !any.IsFinal) mods += IsOverride(any) ? " override" : " virtual";

                lines.Add($"{mods} {Render(p.PropertyType, nullability.Create(p), read: true)} {p.Name} {{ {string.Join("; ", accessors)}; }}");
            }

            foreach (EventInfo e in type.GetEvents(Flags))
            {
                MethodInfo? add = e.AddMethod;
                if (add is null || !Exposed(add.IsPublic, add.IsFamily, add.IsFamilyOrAssembly)) continue;
                string mods = Access(add.IsPublic, add.IsFamily, add.IsFamilyOrAssembly);
                if (add.IsStatic) mods += " static";
                lines.Add($"{mods} event {FullName(e.EventHandlerType!)} {e.Name}");
            }

            foreach (ConstructorInfo c in type.GetConstructors(Flags)
                         .Where(c => Exposed(c.IsPublic, c.IsFamily, c.IsFamilyOrAssembly)))
            {
                lines.Add($"{Access(c.IsPublic, c.IsFamily, c.IsFamilyOrAssembly)} {Name(type)}({Parameters(c, nullability)})");
            }

            foreach (MethodInfo m in type.GetMethods(Flags)
                         .Where(m => Exposed(m.IsPublic, m.IsFamily, m.IsFamilyOrAssembly)))
            {
                // Property and event accessors are already reported by the property or event itself.
                if (m.IsSpecialName && (m.Name.StartsWith("get_", StringComparison.Ordinal)
                    || m.Name.StartsWith("set_", StringComparison.Ordinal)
                    || m.Name.StartsWith("add_", StringComparison.Ordinal)
                    || m.Name.StartsWith("remove_", StringComparison.Ordinal))) continue;
                if (m.Name.Contains('<')) continue;

                string mods = Access(m.IsPublic, m.IsFamily, m.IsFamilyOrAssembly);
                if (m.IsStatic) mods += " static";
                if (m.IsAbstract) mods += " abstract";
                else if (m.IsVirtual && !m.IsFinal) mods += IsOverride(m) ? " override" : " virtual";

                string generics = m.IsGenericMethodDefinition
                    ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">"
                    : string.Empty;

                lines.Add($"{mods} {Render(m.ReturnType, nullability.Create(m.ReturnParameter), read: true)} "
                    + $"{m.Name}{generics}({Parameters(m, nullability)})");
            }

            return lines.OrderBy(l => l, StringComparer.Ordinal);
        }

        private static bool IsOverride(MethodInfo m) => m.GetBaseDefinition().DeclaringType != m.DeclaringType;

        private static bool Exposed(bool isPublic, bool isFamily, bool isFamilyOrAssembly) =>
            isPublic || isFamily || isFamilyOrAssembly;

        private static string Access(bool isPublic, bool isFamily, bool isFamilyOrAssembly) =>
            isPublic ? "public" : isFamilyOrAssembly ? "protected internal" : "protected";

        private static string Parameters(MethodBase method, NullabilityInfoContext nullability) =>
            string.Join(", ", method.GetParameters().Select(p =>
            {
                string prefix = p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : string.Empty;

                // An `out` hands something back, so it is read like a return; everything else is
                // written by the caller. See the note above Describe.
                string text = $"{prefix}{Render(p.ParameterType, nullability.Create(p), read: p.IsOut)} {p.Name}";
                return p.HasDefaultValue ? $"{text} = {Literal(p.RawDefaultValue)}" : text;
            }));

        private static string Literal(object? value) => value switch
        {
            null => "null",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "?",
        };

        private static string Name(Type type)
        {
            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick < 0) return name;
            return name[..tick] + "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.Name)) + ">";
        }

        // The C# spelling, because this file exists to be read by a person deciding whether a change
        // is breaking. "System.Void OnClosed" is the same information as "void OnClosed" and worse
        // at the only job it has.
        private static readonly Dictionary<Type, string> Aliases = new()
        {
            [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte", [typeof(char)] = "char", [typeof(decimal)] = "decimal",
            [typeof(double)] = "double", [typeof(float)] = "float", [typeof(int)] = "int",
            [typeof(uint)] = "uint", [typeof(long)] = "long", [typeof(ulong)] = "ulong",
            [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(object)] = "object",
            [typeof(string)] = "string",
        };

        // The same spelling as FullName, with `?` wherever the annotation says so - INCLUDING inside
        // generic arguments and array elements, which is where this stops being a one-liner.
        // `Func<string, string?>` and `Func<string?, string>` are different contracts and neither is
        // the same as `Func<string, string>`; NullabilityInfo mirrors the type's own shape through
        // GenericTypeArguments and ElementType, so the walk mirrors it back.
        //
        // A `?` on a value type is left to FullName, which already renders Nullable<T> as T? - asking
        // for both is how you get `int??`.
        private static string Render(Type type, NullabilityInfo? info, bool read)
        {
            if (type.IsByRef) type = type.GetElementType()!;
            if (info is null) return FullName(type);

            string suffix = Suffix(type, info, read);

            if (type.IsArray) return Render(type.GetElementType()!, info.ElementType, read) + "[]" + suffix;
            if (Aliases.TryGetValue(type, out string? alias)) return alias + suffix;
            if (type.IsGenericParameter) return type.Name + suffix;
            if (Nullable.GetUnderlyingType(type) is not null) return FullName(type);

            if (!type.IsGenericType) return FullName(type) + suffix;

            Type[] args = type.GetGenericArguments();
            NullabilityInfo[] argInfo = info.GenericTypeArguments;
            var rendered = new List<string>();
            for (int i = 0; i < args.Length; i++)
                rendered.Add(Render(args[i], i < argInfo.Length ? argInfo[i] : null, read));

            string ns = type.Namespace is null ? string.Empty : type.Namespace + ".";
            if (type.IsNested) ns = FullName(type.DeclaringType!) + ".";

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];

            return ns + name + "<" + string.Join(", ", rendered) + ">" + suffix;
        }

        // Unknown means the declaring context is not annotated, and is NOT the same as NotNull - a
        // consumer of an unannotated assembly is promised nothing either way, so nothing is written.
        private static string Suffix(Type type, NullabilityInfo info, bool read) =>
            !type.IsValueType && (read ? info.ReadState : info.WriteState) == NullabilityState.Nullable
                ? "?"
                : string.Empty;

        private static string FullName(Type type)
        {
            if (Aliases.TryGetValue(type, out string? alias)) return alias;
            if (type.IsByRef) return FullName(type.GetElementType()!);
            if (type.IsArray) return FullName(type.GetElementType()!) + "[]";
            if (type.IsGenericParameter) return type.Name;

            Type? nullable = Nullable.GetUnderlyingType(type);
            if (nullable is not null) return FullName(nullable) + "?";

            string ns = type.Namespace is null ? string.Empty : type.Namespace + ".";
            if (type.IsNested) ns = FullName(type.DeclaringType!) + ".";

            if (!type.IsGenericType) return ns + type.Name;

            string name = type.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];
            return ns + name + "<" + string.Join(", ", type.GetGenericArguments().Select(FullName)) + ">";
        }
    }
}
