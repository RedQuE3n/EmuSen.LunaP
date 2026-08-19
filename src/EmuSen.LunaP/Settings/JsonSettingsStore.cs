using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace EmuSen.LunaP.Settings
{
    // The default store: indented JSON under a directory the host names - see docs/LunaP.md §19.1.
    /// <summary>The default settings store: indented JSON under a directory the host names.</summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        // Deliberately the same shape EmuSen.Galaxia uses, so the files it already wrote still read.
        /// <summary>The serializer settings every file is read and written with: indented, case-insensitive, and tolerant of comments.</summary>
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };

        private readonly string _root;

        /// <summary>A store rooted at a directory you choose.</summary>
        /// <param name="root">The directory categories are created under. Created on demand; nothing is written until something is saved.</param>
        public JsonSettingsStore(string root) => _root = root;

        // Named after the entry assembly when the host says nothing, so an application that never
        // configures LunaP still gets its own folder.
        //
        // EXCEPT UNDER A TEST RUNNER, WHERE THE ENTRY ASSEMBLY IS NOT THE APPLICATION. Under
        // `dotnet test` with VSTest it is `testhost`, and that is not a guess that happens to be
        // wrong - it is the SAME wrong answer for every project on the machine. Two consumers' test
        // suites then share one directory under the user's real config root, and they share it in
        // both directions. The write is litter; the READ is a test restoring a window placement that
        // some other repository's suite saved last week, under a WindowKey they both happened to
        // call "main" - a failure that depends on what else has been built on that machine.
        //
        // Found rather than reasoned about: `~/.config/testhost/windows.json` here held `pegasus`
        // and `pegasus-signin`, written by a consumer whose suite never touches LunaSettings.Store
        // because nothing told it that it should. See docs/LunaP.md §43.
        //
        // The diverted root is the test project's own output directory - per-project by
        // construction rather than by heuristic, already ignored by git, wiped by `dotnet clean`,
        // and where somebody hunting for the file would think to look. Deliberately not a temp
        // directory keyed by a hash of that same path, which buys identical isolation and no
        // legibility.
        //
        // A name the host PASSES is honoured whatever it says, including "testhost". Only a name we
        // guessed is ever overridden.
        /// <summary>A store under the usual per-user configuration directory for this platform.</summary>
        /// <param name="programName">The folder to use under it. Defaults to the entry assembly name, except under a test runner - see the remarks.</param>
        /// <returns>A store rooted at that directory.</returns>
        /// <remarks>When <paramref name="programName"/> is null and the process is a test runner, the entry assembly is the runner rather than the application and carries the same name for every project. Rather than root a consumer's settings in a directory shared with every other project's test suite, the store is rooted under the test project's own output directory instead.</remarks>
        public static JsonSettingsStore ForApplication(string? programName = null)
        {
            if (programName is not null) return new(Path.Combine(ConfigRoot, programName));

            string? entry = Assembly.GetEntryAssembly()?.GetName().Name;
            if (!IsSharedRunnerName(entry)) return new(Path.Combine(ConfigRoot, entry ?? "LunaP"));

            string diverted = Path.Combine(AppContext.BaseDirectory, DivertedFolderName);
            LunaSettings.Report(
                $"The entry assembly is '{entry}', which is a test runner rather than an application and is "
                + $"named the same in every project. Settings are under {diverted} rather than a shared "
                + "directory in your real configuration folder. Assign LunaSettings.Store to choose your own.");

            return new(diverted);
        }

        // Named so that finding it in a bin directory answers its own question.
        private const string DivertedFolderName = "lunap-settings";

        private static string ConfigRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Runner names that are one name for every project on the machine.
        //
        // Microsoft.Testing.Platform is deliberately absent: it runs the test ASSEMBLY as the entry
        // point, so the name is already the test project's own and already distinct per project.
        // There is nothing to divert, and matching it would move settings for no reason.
        private static readonly string[] SharedRunnerNames = { "testhost", "vstest.console", "dotnet-vstest" };

        // Exact, or the name followed by a dot - `testhost` ships architecture variants named
        // `testhost.x86` and `testhost.arm64`.
        //
        // NOT A BARE StartsWith, which is what this was first written as. That matches an
        // application genuinely called `TestHostApp`, and matching it would silently move a real
        // user's settings out of their configuration directory into a bin folder that the next
        // `dotnet clean` deletes. The false positive is far more expensive than the false negative
        // it guards against, because a missed runner litters a directory and a wrong match destroys
        // somebody's window layout. `Matching_is_tight_enough_not_to_catch_a_real_application` is
        // the assertion; see docs/LunaP.md §43.3.
        private static bool IsSharedRunnerName(string? name) =>
            name is not null
            && Array.Exists(SharedRunnerNames, r =>
                name.Equals(r, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(r + ".", StringComparison.OrdinalIgnoreCase));

        // Path arithmetic only; Save is what creates a directory. §80.3.
        /// <summary>The directory a category resolves to, whether or not it exists yet.</summary>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <returns>The full path.</returns>
        public string Directory(string? category) =>
            category is null ? _root : Path.Combine(_root, category);

        /// <summary>Reads and deserializes one file, answering null for anything that does not work.</summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <param name="fileName">The file name, including its extension.</param>
        /// <returns>The value, or null when the file is missing, empty or malformed. Failures are reported through LunaSettings.Report rather than thrown.</returns>
        public T? Load<T>(string? category, string fileName) where T : class
        {
            // Resolved before the try and reused in the catch, so a handler cannot throw a second
            // time working out what to name in its own message.
            string path = fileName;
            try
            {
                path = PathFor(category, fileName);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            }
            catch (Exception ex)
            {
                LunaSettings.Report($"{path}: {ex.Message} Falling back to defaults.");
                return null;
            }
        }

        /// <summary>Serializes one value to a file, answering whether it worked.</summary>
        /// <typeparam name="T">The type being serialized.</typeparam>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <param name="fileName">The file name, including its extension.</param>
        /// <param name="value">What to write.</param>
        /// <returns>True if it was written. Failures are reported through LunaSettings.Report rather than thrown.</returns>
        // A FAILED WRITE USED TO BE COMPLETELY SILENT - see docs/LunaP.md §79.3.
        //
        // This catch was bare and returned false, while the summary above it promised the failure was
        // reported. That would be a documentation defect on its own; what made it cost something is
        // that NOT ONE CALLER READS THE BOOL. WindowPlacementStore, TableLayoutStore, PaneLayoutStore
        // and LunaTheme all call this and discard the result, so a read-only configuration directory
        // or a full disk lost a window's geometry, a table's columns and the chosen theme with no
        // exception, no diagnostic and no return value anybody looked at - the user simply found that
        // their layout never stuck.
        //
        // Load has reported since it was written; the asymmetry was an oversight rather than a
        // decision, which is why this now matches it rather than the callers being changed to check.
        // Reporting is the seam a host already has, and a store cannot know which failures its caller
        // considers fatal.
        public bool Save<T>(string? category, string fileName, T value) where T : class
        {
            string path = fileName;
            try
            {
                path = PathFor(category, fileName);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteAtomic(path, JsonSerializer.Serialize(value, Options));
                return true;
            }
            catch (Exception ex)
            {
                LunaSettings.Report($"{path}: {ex.Message} Nothing was written.");
                return false;
            }
        }

        private string PathFor(string? category, string fileName) => Path.Combine(Directory(category), fileName);

        // Full write then rename, so an interrupted save leaves the previous file intact instead of a truncated one.
        private static void WriteAtomic(string path, string contents)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
    }
}
