using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // The repo, found by walking up from the test binary until docs/LunaP.md appears. The man
        // page is not copied to the output directory and neither are the sources being scanned, so
        // something has to bridge from `bin` back to the tree.
        //
        // NOT [CallerFilePath], WHICH IS WHAT THIS WAS AND WHICH CI KILLED ON ALL THREE PLATFORMS.
        // §31 turned on SourceLink and symbol packages, and CI sets ContinuousIntegrationBuild=true,
        // which enables deterministic source paths - every embedded source path is rewritten to `/_`
        // so that a build is byte-identical regardless of where it was checked out. So
        // [CallerFilePath] compiled to `/_/tests/EmuSen.LunaP.Tests/CitationTests.cs`, a path that
        // exists nowhere, and the walk found no repository at all. That is a real property of this
        // package's own build settings, not a CI quirk, and it would come back the moment anybody
        // reached for the same trick. See docs/LunaP.md §44.1.
        //
        // AppContext.BaseDirectory is unaffected - it is resolved at run time from where the
        // assembly actually sits, which is `<repo>/tests/EmuSen.LunaP.Tests/bin/<config>/net10.0`.
        private static string RepoRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "LunaP.md")))
            {
                dir = dir.Parent;
            }

            Assert.True(dir is not null,
                $"Walked up from {AppContext.BaseDirectory} without finding docs/LunaP.md. This test reads "
                + "the source tree rather than the build output, so it only works when the tests are run "
                + "from inside the repository. See docs/LunaP.md §44.1.");

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

        private static readonly Regex FileCitation = new(@"\b([A-Za-z0-9_][A-Za-z0-9_.-]*\.md)\b");

        // Named on purpose and deliberately absent, for two different reasons.
        //
        // CLAUDE.md is gitignored - local working configuration, and this project must not carry
        // documentation about how an assistant should behave - so requiring it would turn a
        // deliberate absence into a suite that is red on CI and green nowhere else.
        //
        // EmuSen_Project_Overview_v2.md stayed behind when LunaP left EmuSen (§19, §20). LunaApp.cs
        // names it while saying in the same breath that the measurement behind it is unreachable,
        // and spells it out rather than writing a § precisely so it is not read as a live citation.
        // That is the §44 register working as intended - a retired source, retired in the open - and
        // an exclusion here rather than a rewritten comment is what keeps it that way.
        private static readonly HashSet<string> AbsentByDesign =
            new(StringComparer.OrdinalIgnoreCase) { "CLAUDE.md", "EmuSen_Project_Overview_v2.md" };

        // TRACKED, NOT MERELY PRESENT - and that distinction is the whole of this guard.
        //
        // Four comments in shipped source pointed at PLAN-table.md while it was untracked. It sat in
        // the working copy the whole time, so every local run was green and a fresh clone - CI's,
        // and a consumer reading the package's SourceLink - had nothing to open. A guard that asked
        // the filesystem would have agreed with the working copy and missed it completely, which is
        // §44's point arriving through the other half of the sentence: a citation that reads as
        // authority and delivers nothing. See docs/LunaP.md §83.2.
        [Fact]
        public void Every_file_cited_from_code_is_in_the_repository()
        {
            string root = RepoRoot();
            HashSet<string> tracked = Tracked(root);

            var broken = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (string file in SourceFiles(root))
            {
                foreach (Match m in FileCitation.Matches(File.ReadAllText(file)))
                {
                    string cited = m.Groups[1].Value;
                    if (tracked.Contains(cited) || AbsentByDesign.Contains(cited)) continue;

                    if (!broken.TryGetValue(cited, out SortedSet<string>? where))
                    {
                        broken[cited] = where = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    where.Add(Path.GetRelativePath(root, file));
                }
            }

            Assert.True(broken.Count == 0,
                "These files are cited from the repository but are not in it:\n\n"
                + string.Join("\n", broken.Select(b => $"  {b.Key}\n      {string.Join("\n      ", b.Value)}"))
                + "\n\nA file that exists only in your working copy is a file a clone cannot open, so the "
                + "citation reads as authority and delivers nothing. Commit it, or move what it says into "
                + "docs/LunaP.md and repoint the comment.");
        }

        // Asks git rather than the disk, for the reason above. A checkout without a .git directory
        // would make this vacuous, so the count is floored the same way the scan above is.
        private static HashSet<string> Tracked(string root)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "ls-files")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            using System.Diagnostics.Process? git = System.Diagnostics.Process.Start(psi);
            Assert.True(git is not null, "git could not be started, so tracked files cannot be listed.");

            string output = git!.StandardOutput.ReadToEnd();
            git.WaitForExit();

            HashSet<string> names = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => Path.GetFileName(p.Trim()))
                .Where(n => n.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.True(names.Count >= 100,
                $"git ls-files reported {names.Count} files, which cannot be right - this guard would "
                + "pass vacuously. Is the suite running outside a checkout?");

            return names;
        }
    }
}
