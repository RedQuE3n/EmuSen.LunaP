using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EmuSen.LunaP.Tests
{
    // A TEST THAT CANNOT FAIL, CAUGHT BY THE SABOTAGE IT SURVIVED - see docs/LunaP.md §77.5.
    //
    // HeadlessUnitTestSession.Dispatch has three overloads, and one of them is
    // Dispatch<TResult>(Func<TResult>, CancellationToken). An `async () => { ... }` lambda binds to
    // it with TResult inferred as Task, so the call returns Task<Task>. Handing that back as a
    // [Fact]'s Task awaits only the OUTER task - which completes at the body's first `await` - and
    // every assertion after that point runs detached, on a thread nobody is watching, with its
    // failure swallowed.
    //
    // The symptom is a green test that is asserting nothing. Two guards in FileDropTests were
    // written this way; both survived the sabotage that should have killed them, and both turned red
    // the moment the shape was fixed. Nothing about the code looked wrong, which is exactly why this
    // is an assertion and not a paragraph asking the next author to remember (§28's precedent).
    //
    // THE FIX IS Unwrap(), which returns the inner task so the body is actually awaited. A helper
    // per test class is how it is spelled - see FileDropTests.Run.
    //
    // This scan is deliberately textual. The mistake is a compile-time overload choice with no
    // runtime trace, so there is nothing to reflect over: by the time a test runs, the evidence is
    // a Task somebody already dropped.
    public class DispatchShapeTests
    {
        // `Dispatch(async` on one line, allowing for whitespace. Also catches Dispatch(async () =>
        // spread across lines, because the `async` follows the paren directly in every spelling of
        // this mistake.
        private static readonly Regex AsyncDispatch = new(@"\bDispatch\s*\(\s*async\b", RegexOptions.Compiled);

        [Fact]
        public void No_test_hands_an_async_lambda_straight_to_dispatch()
        {
            string root = RepoRoot();
            var offenders = new List<string>();

            foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
                         .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                  && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                         // This file, necessarily: it spells the shape out in its own header and
                         // twice more in the self-test below. It contains no Dispatch call to get
                         // wrong, so excluding it costs the scan nothing.
                         .Where(f => Path.GetFileName(f) != "DispatchShapeTests.cs")
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (AsyncDispatch.IsMatch(lines[i]))
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }

            Assert.True(offenders.Count == 0,
                "An async lambda passed straight to Dispatch binds to Dispatch<TResult> with TResult = Task, "
                + "so the call returns Task<Task> and only the outer task is awaited. Every assertion after the "
                + "body's first `await` is swallowed and the test passes no matter what it asserts.\n\n"
                + string.Join("\n", offenders)
                + "\n\nWrap it: `Session.Dispatch(body, default).Unwrap()`. See docs/LunaP.md §77.5.");
        }

        // The guard has to be able to see its own subject, or it is the thing it is testing for.
        [Fact]
        public void The_scan_matches_the_shape_it_is_looking_for()
        {
            Assert.Matches(AsyncDispatch, "public Task A() => Session.Dispatch(async () =>");
            Assert.Matches(AsyncDispatch, "Session.Dispatch( async ()=>{}");

            // And does not fire on the correct spellings.
            Assert.DoesNotMatch(AsyncDispatch, "Session.Dispatch(body, default).Unwrap()");
            Assert.DoesNotMatch(AsyncDispatch, "public Task A() => Session.Dispatch(() =>");
            Assert.DoesNotMatch(AsyncDispatch, "private static Task Run(Func<Task> body)");
        }

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
    }
}
