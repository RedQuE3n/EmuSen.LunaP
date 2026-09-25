using System;
using System.Collections.Concurrent;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace EmuSen.LunaP.Media
{
    // Typefaces read from font files, one per full path, and never added to Avalonia's shared font manager - see docs/LunaP.md §98.3.
    /// <summary>Loads typefaces from font files, caching one per path, without installing them in the application's font manager.</summary>
    public static class FontFiles
    {
        // A collection of one, held by nobody but the typeface, so the font manager never learns of it.
        private static readonly Uri Key = new("fonts:EmuSen.LunaP.FontFiles");

        private static readonly ConcurrentDictionary<string, Lazy<GlyphTypeface?>> Cache = new(StringComparer.Ordinal);

        /// <summary>How many paths have been asked for, each read at most once.</summary>
        public static int Count => Cache.Count;

        /// <summary>The typeface in a font file, or null when the file is missing or not a font. Read once per full path.</summary>
        /// <param name="path">The font file.</param>
        /// <returns>The typeface, the same instance for every call with the same full path, or null.</returns>
        public static GlyphTypeface? Load(string path)
        {
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
            return Cache.GetOrAdd(full, p => new Lazy<GlyphTypeface?>(() => Read(p))).Value;
        }

        private static GlyphTypeface? Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var stream = new MemoryStream(File.ReadAllBytes(path), writable: false);
                var collection = new EmbeddedFontCollection(Key, Key);
                return collection.TryAddGlyphTypeface(stream, out GlyphTypeface? typeface) ? typeface : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                return null;
            }
        }

        /// <summary>The application's default typeface, which text falls back to when no font file is given or it cannot be read.</summary>
        public static GlyphTypeface Default =>
            FontManager.Current.TryGetGlyphTypeface(new Typeface(FontFamily.Default), out GlyphTypeface? typeface) && typeface is not null
                ? typeface
                : throw new InvalidOperationException("The font manager has no default typeface.");
    }
}
