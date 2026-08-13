using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace EmuSen.LunaP.Tests
{
    // Every § cited from code resolves to a section that exists - see docs/LunaP.md §44.
    //
    // WHY THIS IS AN ASSERTION RATHER THAN A RULE IN CLAUDE.md, WHERE IT ALREADY WAS. "Every §
    // cited from code must resolve" has been the written rule since the file existed, and it held -
    // this guard found 116 distinct citations across the toolkit and the suite and every one of
    // them resolved on the day it was written. That is a good result, and it is also exactly the
    // situation §28 describes: a rule kept by everybody remembering it is a rule that gets broken by
    // the first person who does not, and the breakage is silent. A citation into a section that
    // does not exist reads as authority and delivers nothing, which is worse than no citation.
    //
    // The failure mode this catches is not usually a typo. It is renumbering: §21.6 gets split, a
    // heading loses its number, a section is folded into a neighbour - and thirty pointers that were
    // correct on Tuesday are silently wrong on Wednesday, with nothing to notice it.
    public class CitationTests
    {
        private static readonly Regex Heading = new(@"^#{2,4}\s+(?:§\s*)?(\d+(?:\.\d+)*)\.?\s", RegexOptions.Multiline);
        private static readonly Regex Citation = new(@"§\s*(\d+(?:\.\d+)*)");

        // The repo, found from this file's own compile-time path. There is no other route: the man
        // page is not copied to the output directory, and neither is the source being scanned.
        private static string RepoRoot([CallerFilePath] string here = "")
        {
            DirectoryInfo? dir = new FileInfo(here).Directory;
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "LunaP.md")))
            {
                dir = dir.Parent;
            }

            Assert.True(dir is not null,
                $"Walked up from {here} without finding docs/LunaP.md. This test reads the source tree "
                + "rather than the build output, so it only works inside the repository.");

            return dir!.FullName;
        }

        private static IEnumerable<string> SourceFiles(string root) =>
            new[] { "src", "tests" }
                .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*.*", SearchOption.AllDirectories))
                .Where(f => f.EndsWith(".cs", StringComparison.Ordinal)
                         || f.EndsWith(".axaml", StringComparison.Ordinal)
                         || f.EndsWith(".csproj", StringComparison.Ordinal))
                // Build output contains generated copies and the committed API baseline's own text.
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Concat(new[] { "README.md", "CHANGELOG.md", "CLAUDE.md" }
                    .Select(f => Path.Combine(root, f))
                    .Where(File.Exists));

        private static HashSet<string> SectionsIn(string root) =>
            Heading.Matches(File.ReadAllText(Path.Combine(root, "docs", "LunaP.md")))
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

        [Fact]
        public void Every_section_cited_from_code_exists_in_the_man_page()
        {
            string root = RepoRoot();
            HashSet<string> sections = SectionsIn(root);

            var broken = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (string file in SourceFiles(root))
            {
                foreach (Match m in Citation.Matches(File.ReadAllText(file)))
                {
                    string cited = m.Groups[1].Value;
                    if (sections.Contains(cited)) continue;

                    if (!broken.TryGetValue(cited, out SortedSet<string>? where))
                    {
                        broken[cited] = where = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    where.Add(Path.GetRelativePath(root, file));
                }
            }

            Assert.True(broken.Count == 0,
                "These sections are cited but do not exist in docs/LunaP.md:\n\n"
                + string.Join("\n", broken.Select(b => $"  §{b.Key}\n      {string.Join("\n      ", b.Value)}"))
                + "\n\nA citation is an addition, never a substitute (CLAUDE.md), so the fix is to write "
                + "the section - not to delete the pointer. If a section was renumbered, every pointer "
                + "to its old number is now silently wrong and this is the only thing that would say so.");
        }

        // THE TRAP THIS GUARD WOULD OTHERWISE FALL INTO. The assertion above passes if the scan
        // finds nothing - a wrong path, a renamed directory, an extension that quietly stops
        // matching. A guard that goes silent when its input disappears is worse than no guard,
        // because the green tick is what gets read. See docs/LunaP.md §44.
        //
        // PER CATEGORY, NOT AS ONE TOTAL, and the first draft got this wrong. A single floor over
        // all files catches a scan that collapses to zero and nothing else: drop `.axaml` from the
        // filter and 18 files stop being checked while 84 remain, comfortably over any total floor
        // that ordinary editing would not trip. Each kind of file therefore has its own floor, which
        // is the only version that catches the failure the comment claims to catch.
        //
        // Floors are set well under the real counts (81 .cs, 18 .axaml, 3 .csproj, 199 sections,
        // 116 citations when written) so ordinary editing never touches them.
        [Fact]
        public void The_scan_actually_reads_every_kind_of_file_it_claims_to()
        {
            string root = RepoRoot();
            List<string> files = SourceFiles(root).ToList();

            int Count(string extension) =>
                files.Count(f => f.EndsWith(extension, StringComparison.Ordinal));

            Assert.True(Count(".cs") >= 50, $"The scan found {Count(".cs")} .cs files, which cannot be right.");
            Assert.True(Count(".axaml") >= 10, $"The scan found {Count(".axaml")} .axaml files - the theme is not being read.");
            Assert.True(Count(".csproj") >= 3, $"The scan found {Count(".csproj")} .csproj files - the project files carry citations too.");
            Assert.True(Count(".md") >= 3, $"The scan found {Count(".md")} markdown files - README, CHANGELOG and CLAUDE all cite sections.");

            int sections = SectionsIn(root).Count;
            int citations = files.Sum(f => Citation.Matches(File.ReadAllText(f)).Count);

            Assert.True(sections >= 150, $"The man page parse found {sections} sections, which cannot be right.");
            Assert.True(citations >= 100, $"The citation scan found {citations} citations, which cannot be right.");
        }
    }
}
