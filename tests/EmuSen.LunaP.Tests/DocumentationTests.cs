using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // EVERY PUBLIC TYPE SAYS WHAT IT IS, to the only reader who cannot open the file.
    //
    // This repository writes its reasoning in `//` blocks beside the code, and CLAUDE.md says to
    // keep doing that. The blind spot is who that convention serves: somebody with the file open.
    // A consumer typing `new LunaAction(` has the DLL and the .xml beside it and nothing else, and
    // until §33 that .xml did not exist - sixty-three public types offering IntelliSense nothing at
    // all.
    //
    // WHY TYPES AND NOT MEMBERS, which is the whole shape of this guard. CS1591 would demand a
    // sentence for all 460 public members, and 99 of those are Avalonia StyledProperty fields and
    // protected overrides of OnApplyTemplate and friends, where the only available sentence
    // restates the name. "Gets or sets the Text." teaches nobody anything and rots exactly as fast
    // as a real comment. §33 records the trade: the rule is enforced where it carries information,
    // and CS1591 is suppressed in both .csproj files with that reason written beside it.
    //
    // The <summary> is the WHAT, in a sentence. The `//` block above the type keeps the WHY, at
    // whatever length the argument needs - they are two audiences, not two attempts at one job.
    public class DocumentationTests
    {
        public static TheoryData<string> Packages() => new() { "EmuSen.LunaP", "EmuSen.LunaP.Testing" };

        [Theory]
        [MemberData(nameof(Packages))]
        public void Every_public_type_has_a_summary(string package)
        {
            Assembly assembly = Assembly.Load(package);
            HashSet<string> documented = Summaries(assembly);

            string[] missing = assembly.GetTypes()
                .Where(Documentable)
                .Select(t => "T:" + Key(t))
                .Where(k => !documented.Contains(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            Assert.True(missing.Length == 0,
                $"{missing.Length} public type(s) in {package} have no <summary>, so a consumer's "
                + "IntelliSense shows them as a bare name:\n\n  "
                + string.Join("\n  ", missing.Select(k => k[2..]))
                + "\n\nAdd a /// <summary> saying WHAT it is in one sentence. The `//` block above the "
                + "type keeps the WHY and does not move. See docs/LunaP.md §33.");
        }

        // A summary that exists and says nothing is the failure this guard is most likely to invite,
        // so it is worth one assertion. The threshold is deliberately low - it catches `///` left
        // empty by a merge or filled with a placeholder, not prose somebody thought about.
        [Theory]
        [MemberData(nameof(Packages))]
        public void No_summary_is_empty_or_a_placeholder(string package)
        {
            Assembly assembly = Assembly.Load(package);
            XDocument doc = XDocument.Load(DocPath(assembly));

            var bad = new List<string>();
            foreach (XElement member in doc.Descendants("member"))
            {
                string name = (string?)member.Attribute("name") ?? string.Empty;
                if (!name.StartsWith("T:", StringComparison.Ordinal)) continue;

                string text = string.Concat(member.Element("summary")?.Nodes()
                    .Select(n => n is XText t ? t.Value : string.Empty) ?? Array.Empty<string>()).Trim();

                if (text.Length < 12 || text.Equals("TODO", StringComparison.OrdinalIgnoreCase))
                    bad.Add($"{name[2..]}: \"{text}\"");
            }

            Assert.True(bad.Count == 0,
                "These <summary> elements are empty or too short to be telling anyone anything:\n\n  "
                + string.Join("\n  ", bad) + "\n\nSee docs/LunaP.md §33.");
        }

        private static HashSet<string> Summaries(Assembly assembly)
        {
            XDocument doc = XDocument.Load(DocPath(assembly));
            return doc.Descendants("member")
                .Where(m => m.Element("summary") is not null)
                .Select(m => (string?)m.Attribute("name") ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        // The .xml the compiler writes beside the .dll, and which `dotnet pack` puts in the package.
        // Its absence is a failure worth its own message: it means GenerateDocumentationFile was
        // turned off, which no other test would notice.
        private static string DocPath(Assembly assembly)
        {
            string path = Path.ChangeExtension(assembly.Location, ".xml");
            Assert.True(File.Exists(path),
                $"No XML documentation beside {Path.GetFileName(assembly.Location)}. "
                + "GenerateDocumentationFile is off, and a consumer's IntelliSense is empty. See docs/LunaP.md §33.");
            return path;
        }

        // The same filter ApiSurfaceTests uses, and for the same reasons: the XAML compiler's
        // generated types are public, uncallable, and not ours to document (§32.3).
        private static bool Documentable(Type type)
        {
            if (type.Name.Contains('<') || type.Name.Contains('$')) return false;
            if (type.Namespace?.StartsWith("CompiledAvaloniaXaml", StringComparison.Ordinal) == true) return false;
            if (type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false)) return false;

            return type.IsPublic || type.IsNestedPublic;
        }

        // The spelling the compiler uses in the .xml: namespace-qualified, nested types joined by a
        // dot, and a generic arity written as `1 rather than <T>.
        private static string Key(Type type)
        {
            string name = type.FullName ?? type.Name;
            return name.Replace('+', '.');
        }
    }
}
