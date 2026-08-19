namespace EmuSen.LunaP.Settings
{
    // The seam between LunaP and whatever a host keeps its settings in - see docs/LunaP.md §19.
    /// <summary>The seam between LunaP and whatever a host keeps its settings in.</summary>
    public interface ISettingsStore
    {
        // A category is a subdirectory; null is the root. Resolving a path is all this does - an
        // implementation creates the directory when it WRITES one, not when it is asked where a
        // category lives, so a caller reading this must not assume the folder is there. It said
        // "created if it does not exist" until §80.3, which no implementation has ever done.
        /// <summary>The directory a category resolves to, whether or not it exists yet.</summary>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <returns>The full path.</returns>
        string Directory(string? category);

        // Null for missing, unreadable or corrupt - callers fall back to their own defaults rather than crash.
        /// <summary>Reads one file back, or answers null when it is absent or unreadable.</summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <param name="fileName">The file name, including its extension.</param>
        /// <returns>The value, or null. An implementation must not throw for a missing or corrupt file: the toolkit calls this during window construction and treats null as no saved state.</returns>
        T? Load<T>(string? category, string fileName) where T : class;

        // False when the write failed; a setting that cannot reach the disk must not take the program with it.
        /// <summary>Writes one file, answering whether it worked rather than throwing.</summary>
        /// <typeparam name="T">The type being serialized.</typeparam>
        /// <param name="category">The subdirectory, or null for the root.</param>
        /// <param name="fileName">The file name, including its extension.</param>
        /// <param name="value">What to write.</param>
        /// <returns>True if it was written. An implementation must not throw: a settings failure is not worth losing a window close over.</returns>
        bool Save<T>(string? category, string fileName, T value) where T : class;
    }
}
