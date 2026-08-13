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

        // Named after the entry assembly when the host says nothing, so an application that never configures LunaP still gets its own folder.
        /// <summary>A store under the usual per-user configuration directory for this platform.</summary>
        /// <param name="programName">The folder to use under it. Defaults to the entry assembly name.</param>
        /// <returns>A store rooted at that directory.</returns>
        public static JsonSettingsStore ForApplication(string? programName = null) =>
            new(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                programName ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "LunaP"));

        /// <summary>The directory a category resolves to, created if it does not exist.</summary>
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
            try
            {
                string path = PathFor(category, fileName);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            }
            catch (Exception ex)
            {
                LunaSettings.Report($"{PathFor(category, fileName)}: {ex.Message} Falling back to defaults.");
                return null;
            }
        }

        /// <summary>Serializes one value to a file, answering whether it worked.</summary>
        /// <typeparam name="T">The type being serialized.</typeparam>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <param name="fileName">The file name, including its extension.</param>
        /// <param name="value">What to write.</param>
        /// <returns>True if it was written. Failures are reported through LunaSettings.Report rather than thrown.</returns>
        public bool Save<T>(string? category, string fileName, T value) where T : class
        {
            try
            {
                string path = PathFor(category, fileName);
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                WriteAtomic(path, JsonSerializer.Serialize(value, Options));
                return true;
            }
            catch
            {
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
