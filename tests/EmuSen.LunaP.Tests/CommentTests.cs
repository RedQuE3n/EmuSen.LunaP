using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // A COMMENT MAY NOT BE WRITTEN TWICE - see docs/LunaP.md §74.8.
    //
    // CLAUDE.md's rule is that notes go beside the thing they explain, and that a comment which has
    // drifted from that thing is worse than no comment because it is believed. §74.2 measured what
    // that cost in one file: five arguments sitting over members they were not about, every one an
    // insertion into a file too big to see the whole of, and one of them - the three-state sort
    // cycle - sixty-six lines from the method that implements it.
    //
    // THE SPLIT THAT FIXED IT INTRODUCED THE SAME DEFECT IN A NEW FORM, which is why this exists as
    // an assertion rather than a paragraph. Fourteen new files each got a header explaining what the
    // file is for, and NINE of those headers re-argued something a member comment below already
    // argued - `TableSorting.cs` restated most of `Heading`'s case for a heading being a Button,
    // `TableFrozen.cs` restated `Pin`'s whole clip-and-not-cover mechanism. Two copies of one
    // argument is exactly the thing that drifts, and nothing would have noticed the day one of them
    // was edited.
    //
    // WHAT IS CHECKED IS THE SHOUTED PHRASE, and the narrowness is the point. This codebase opens a
    // real argument with a capitalised clause - "CLIPPED AND NOT COVERED", "THE FLAT CASE IS THE OLD
    // CASE" - so those phrases are a reliable marker for "here is a claim being made" and a poor
    // marker for anything else. Four words or more, because three catches ordinary emphasis
    // ("NOT", "AND THAT IS") and produces noise that would get the guard suppressed rather than
    // obeyed. Prose that merely repeats an idea in different words is NOT caught, and cannot be
    // without a judgement no test can make; this catches the copy-paste, which is the case that
    // actually happened nine times in one afternoon.
    //
    // A FILE HEADER IS ALLOWED TO ORIENT AND NOT TO ARGUE. The distinction the guard enforces is
    // that a header may say what the file holds, why those members are together, and where the
    // argument lives - but the argument itself belongs beside the code, once.
    public class CommentTests
    {
        // Four or more consecutive shouted words. Apostrophes and hyphens are inside a word
        // ("USER'S", "READ-ONLY"); a digit or a lowercase letter ends the run.
        private static readonly Regex Shouted = new(@"\b[A-Z][A-Z'\-]*(?:\s+[A-Z][A-Z'\-]*){3,}\b");

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
                .SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal);

        // The header is everything above the first type declaration; the body is the rest.
        //
        // SPLIT AT THE FIRST DECLARATION AND NOT AT THE FIRST BRACE, because a namespace has a brace
        // and would put the whole file in the body - a scan that can never fail.
        //
        // `readonly` IS IN THE MODIFIER LIST BECAUSE LEAVING IT OUT WAS THE FIRST DRAFT'S DEFECT, and
        // it is the exact failure the floors below exist to catch. Without it `public readonly record
        // struct` does not match, so LunaCell.cs and LunaRowDrop.cs returned null and were skipped in
        // silence - two files quietly unchecked, with the suite green. Found by counting what the
        // scan reached rather than by reading the pattern.
        //
        // A file with no type declaration at all is skipped. There is one, AssemblyInfo.cs, which is
        // attributes and nothing else and has no members for a header to duplicate.
        private static (string Head, string Body)? Halves(string source)
        {
            Match at = Regex.Match(
                source,
                @"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|abstract|sealed|static|partial|readonly|ref|\s)*\b(?:class|record|struct|interface|enum)\b",
                RegexOptions.Multiline);

            return at.Success ? (source[..at.Index], source[at.Index..]) : null;
        }

        [Fact]
        public void No_file_header_restates_an_argument_made_beside_the_code()
        {
            string root = RepoRoot();
            var repeated = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

            foreach (string file in SourceFiles(root))
            {
                if (Halves(File.ReadAllText(file)) is not { } halves) continue;

                foreach (Match m in Shouted.Matches(halves.Head))
                {
                    if (!halves.Body.Contains(m.Value, StringComparison.Ordinal)) continue;

                    string where = Path.GetRelativePath(root, file);
                    if (!repeated.TryGetValue(where, out SortedSet<string>? phrases))
                    {
                        repeated[where] = phrases = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    phrases.Add(m.Value);
                }
            }

            Assert.True(repeated.Count == 0,
                "These file headers repeat an argument that is also made beside the code:\n\n"
                + string.Join("\n", repeated.Select(r => $"  {r.Key}\n      {string.Join("\n      ", r.Value)}"))
                + "\n\nTwo copies of one argument is the thing that drifts, and the copy beside the code is "
                + "the one a reader with the file open will trust (CLAUDE.md). Cut it from the header and "
                + "leave the header saying what the file holds and where the argument lives. "
                + "See docs/LunaP.md §74.8.");
        }

        // THE TRAP THIS GUARD WOULD OTHERWISE FALL INTO, and it is CitationTests' trap exactly
        // (§44): the assertion above passes when the scan finds nothing. A wrong root, a bin filter
        // that swallows the tree, a Halves regex that stops matching and returns null for every
        // file - all of them are green.
        //
        // SO THE FLOORS ARE ON WHAT WAS SCANNED AND NOT ONLY ON THE FILE COUNT. A file count alone
        // would survive a Halves that returned null for everything, and a header count alone would
        // survive one that put the whole file in the head - both of which turn the real assertion
        // into a no-op while it reports green.
        //
        // THE FLOOR ON `split` IS THE ONE THAT EARNED ITS PLACE. It caught the missing `readonly`
        // above, where 119 of 122 files split and the two unchecked ones were invisible - so the
        // gap between the file count and the split count is checked rather than each on its own.
        //
        // MEASURED, NOT ESTIMATED, and the first draft of this file had estimated numbers in this
        // comment and a floor of 500 against a real 397. The suite failed on its first run for that
        // reason, which is the right outcome and the reason it is written down: a floor guessed high
        // is a red test, but a floor guessed low is a guard that quietly stops guarding. Today's
        // figures are 122 files, 121 split, 118 shouted phrases in headers, 397 in bodies; the floors
        // sit about a quarter under, so ordinary editing never touches them.
        [Fact]
        public void The_scan_actually_reads_headers_and_bodies()
        {
            string root = RepoRoot();
            List<string> files = SourceFiles(root).ToList();

            Assert.True(files.Count >= 90, $"The scan found {files.Count} .cs files, which cannot be right.");

            int split = 0, inHeads = 0, inBodies = 0;
            foreach (string file in files)
            {
                if (Halves(File.ReadAllText(file)) is not { } halves) continue;

                split++;
                inHeads += Shouted.Matches(halves.Head).Count;
                inBodies += Shouted.Matches(halves.Body).Count;
            }

            // EXACTLY ONE, NOT "A FEW". This was written as `<= 3` and the sabotage that removed
            // `readonly` from Halves went green through it: three files stopped splitting, three was
            // within tolerance, and the guard written to catch that precise failure did not. A floor
            // with slack in it is a floor the failure fits through. See docs/LunaP.md §74.8.
            Assert.True(files.Count - split <= 1,
                $"{files.Count - split} of {files.Count} files did not split into a header and a body. Only "
                + "AssemblyInfo.cs should - anything else is a declaration form Halves does not recognise, "
                + "and every such file is silently unchecked by the assertion above.");

            Assert.True(split >= 90, $"Only {split} files split into a header and a body.");
            Assert.True(inHeads >= 90, $"Only {inHeads} shouted phrases were found in headers - the split is wrong.");
            Assert.True(inBodies >= 300, $"Only {inBodies} shouted phrases were found in bodies - the split is wrong.");
        }
    }
}
