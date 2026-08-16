using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using EmuSen.LunaP.Settings;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // Where the default store roots itself when the host names nothing - see docs/LunaP.md §43.
    //
    // WHY THIS IS A TEST AND NOT A PARAGRAPH IN THE MAN PAGE. The defect it guards is invisible from
    // inside the project that causes it: a suite that writes into ~/.config/testhost/ passes, stays
    // green, and leaves the evidence in a directory nobody diffs. It was found by looking at a
    // machine, not by reading code, and looking at a machine is not a thing CI does.
    //
    // These assertions are about a PATH rather than about the filesystem, deliberately. The obvious
    // test - "assert ~/.config/testhost does not exist" - would be red or green depending on which
    // other repositories had been built on the machine that ran it, which is the very property being
    // fixed. A test that inherits the bug it guards is not a guard.
    public class SettingsRootTests
    {
        private static string ConfigRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // The whole point. Sabotage: delete the IsSharedRunnerName branch from ForApplication and
        // this is red immediately, with the shared path in the message.
        [Fact]
        public void The_default_store_is_not_rooted_in_the_real_config_directory_under_a_test_runner()
        {
            string root = JsonSettingsStore.ForApplication().Directory(null);

            Assert.False(
                root.StartsWith(ConfigRoot, StringComparison.Ordinal),
                $"The default store rooted itself at {root}, inside the user's real configuration "
                + $"directory {ConfigRoot}. Under a test runner the entry assembly is "
                + $"'{Assembly.GetEntryAssembly()?.GetName().Name}', which is the same name for every "
                + "project on the machine, so this directory is shared with every other project's "
                + "test suite - in both directions. See docs/LunaP.md §43.");
        }

        // Where it goes instead, stated exactly, so a change of destination is a decision somebody
        // has to make on purpose rather than something that drifts.
        [Fact]
        public void It_is_rooted_under_this_test_projects_own_output_directory()
        {
            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, "lunap-settings"),
                JsonSettingsStore.ForApplication().Directory(null));
        }

        // The other direction, and the reason the check above cannot simply be "always divert": a
        // name the host passed is honoured whatever it says. Without this, a guard that diverted
        // unconditionally would pass every assertion here and break every real application.
        [Fact]
        public void A_name_the_host_passes_is_honoured_even_when_it_is_the_runners_own_name()
        {
            Assert.Equal(
                Path.Combine(ConfigRoot, "testhost"),
                JsonSettingsStore.ForApplication("testhost").Directory(null));
        }

        // What an actual application gets, unchanged by any of this. This is the behaviour a
        // consumer already depends on, so it is asserted rather than assumed.
        [Fact]
        public void A_real_application_still_gets_a_folder_under_the_per_user_config_directory()
        {
            Assert.Equal(
                Path.Combine(ConfigRoot, "Hotaru"),
                JsonSettingsStore.ForApplication("Hotaru").Directory(null));
        }

        // The divert is stated rather than silent, for a host that installed somewhere to state it
        // to. Diagnostics is null by default, so this is the only way anybody sees it at all.
        [Fact]
        public void The_divert_reports_itself_to_a_host_that_is_listening()
        {
            string? reported = null;
            LunaSettings.Diagnostics = m => reported = m;

            try
            {
                JsonSettingsStore.ForApplication();
            }
            finally
            {
                LunaSettings.Diagnostics = null;
            }

            Assert.NotNull(reported);
            Assert.Contains("lunap-settings", reported);
            Assert.Contains("LunaSettings.Store", reported);
        }

        // THE EXPENSIVE MISTAKE, WHICH NOTHING ABOVE CAN CATCH.
        //
        // Every assertion in this class runs inside a test host, so every one of them exercises the
        // diverting branch. Widen the match until a real application hits it too and they all stay
        // green - while every consumer's users silently lose their window layout and theme to a bin
        // directory that the next `dotnet clean` deletes. The failure is invisible from here and
        // expensive there, which is the exact combination that has to become an assertion.
        //
        // Reflection because the predicate is private and should stay private: making it public to
        // test it would add API surface to the package for the benefit of one test. That trade goes
        // the other way in a library a consumer cannot patch.
        [Theory]
        // Runners: one name shared by every project, and the architecture variants of the first.
        [InlineData("testhost", true)]
        [InlineData("testhost.x86", true)]
        [InlineData("testhost.arm64", true)]
        [InlineData("TestHost", true)]
        [InlineData("vstest.console", true)]
        [InlineData("dotnet-vstest", true)]
        // Applications. The third is what Microsoft.Testing.Platform reports - the test project
        // itself, which is already distinct per project and must not be diverted.
        [InlineData("Hotaru", false)]
        [InlineData("EmuSen.Mistress", false)]
        [InlineData("EmuSen.LunaP.Tests", false)]
        // The bare-StartsWith bug, pinned as a case so it cannot come back. Both of these matched
        // before the rule was tightened, and the first is a name an application could plausibly have.
        [InlineData("TestHostApp", false)]
        [InlineData("testhostile", false)]
        // The dot rule applies to every runner name rather than only to testhost's architecture
        // variants, so this one matches. Kept as a case because it is the boundary, and because
        // writing it down is cheaper than rediscovering which side of the line it falls on: nothing
        // called `vstest.console.<something>` is an application whose settings anyone would miss.
        [InlineData("vstest.console.reporter", true)]
        public void Matching_is_tight_enough_not_to_catch_a_real_application(string name, bool expected)
        {
            MethodInfo? predicate = typeof(JsonSettingsStore)
                .GetMethod("IsSharedRunnerName", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.True(predicate is not null,
                "JsonSettingsStore.IsSharedRunnerName has been renamed or removed. It decides whether a "
                + "consumer's settings live in their configuration directory or in a bin folder, so it "
                + "needs a test either way - rename this one rather than deleting it. See docs/LunaP.md §43.3.");

            Assert.Equal(expected, (bool)predicate!.Invoke(null, new object?[] { name })!);
        }

        // THE REPORTED BUG, END TO END, through the types that caused it rather than through the
        // store alone.
        //
        // Everything above tests where ForApplication points. This tests what a consumer actually
        // did: show a ToolWindow with a WindowKey, close it, and let LunaP save the placement -
        // without ever assigning LunaSettings.Store, because nothing said they had to. That is the
        // path that put `pegasus` and `pegasus-signin` into ~/.config/testhost/windows.json on the
        // machine where this was found. See docs/LunaP.md §43.1.
        //
        // It asserts the file's LOCATION and not merely the store's opinion of it, because the two
        // could disagree - WindowPlacementStore reads LunaSettings.Store on every call, and a
        // regression that captured the root once at type load would pass every other test here.
        [Fact]
        public Task A_window_saved_through_the_default_store_lands_outside_the_users_config_directory() =>
            UiTest.Run(() =>
            {
                ISettingsStore? original = LunaSettings.Store;
                string key = "settings-root-probe-" + Guid.NewGuid().ToString("N");

                try
                {
                    // Exactly what a consumer who configures nothing gets.
                    LunaSettings.Store = JsonSettingsStore.ForApplication();

                    var window = new ToolWindow { Width = 300, Height = 200, WindowKey = key };
                    window.Show();
                    window.Close();

                    string root = LunaSettings.Store.Directory(null);
                    string written = Path.Combine(root, WindowPlacementStore.FileName);

                    Assert.True(File.Exists(written),
                        $"Closing a keyed ToolWindow wrote no {WindowPlacementStore.FileName} under {root}, so this "
                        + "test is no longer exercising the path it was written for - placement saving has moved.");

                    Assert.False(
                        written.StartsWith(ConfigRoot, StringComparison.Ordinal),
                        $"A window closed under the default store wrote {written}, inside the user's real "
                        + $"configuration directory {ConfigRoot} - shared with every other project's test "
                        + "suite on this machine. See docs/LunaP.md §43.");

                    Assert.NotNull(WindowPlacementStore.Load(key));
                }
                finally
                {
                    // The key is a fresh Guid so a stale file from a previous run can never make the
                    // assertions above pass - which means the file has to be removed here, or every
                    // run leaves another entry behind. Leaving litter in a bin directory while
                    // testing a fix for litter in a config directory would be a poor joke.
                    string diverted = LunaSettings.Store.Directory(null);
                    LunaSettings.Store = original!;

                    if (Directory.Exists(diverted)) Directory.Delete(diverted, recursive: true);
                }
            });

        // And it stays quiet when there is nothing to say, so the message above means something.
        [Fact]
        public void Naming_an_application_reports_nothing()
        {
            string? reported = null;
            LunaSettings.Diagnostics = m => reported = m;

            try
            {
                JsonSettingsStore.ForApplication("Hotaru");
            }
            finally
            {
                LunaSettings.Diagnostics = null;
            }

            Assert.Null(reported);
        }
    }
}
