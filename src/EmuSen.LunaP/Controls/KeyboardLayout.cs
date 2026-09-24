using System;
using System.Collections.Generic;
using System.Linq;

namespace EmuSen.LunaP.Controls
{
    // The keys of an on-screen keyboard as plain data, each with the width it takes - see docs/LunaP.md §91.2.
    /// <summary>The rows of keys an on-screen keyboard shows, with the widths they take.</summary>
    public sealed class KeyboardLayout
    {
        /// <summary>The key that erases the character before the caret.</summary>
        public const string Erase = "Erase";

        /// <summary>The key that types a space.</summary>
        public const string Space = "Space";

        /// <summary>The key that switches letters between lower and upper case.</summary>
        public const string Shift = "Shift";

        /// <summary>The key that closes the keyboard, keeping what was typed.</summary>
        public const string Done = "Done";

        /// <summary>The key that moves to the next layout the keyboard was given.</summary>
        public const string Next = "Next";

        /// <summary>A layout for codes: the sixteen hexadecimal digits, the separators codes are written with, and the keys to erase, space and finish.</summary>
        public static KeyboardLayout Code { get; } = new("Code", new[]
        {
            Row("1 2 3 4 5 6 7 8"),
            Row("9 0 A B C D E F"),
            Row("+ : - . Space:2 Erase:2"),
            Row("Next:4 Done:4"),
        });

        // The sixteen letters of the NES Game Genie in the order its own code wheel prints them.
        /// <summary>A layout for NES Game Genie codes: the sixteen letters they are written in, and the keys to erase, space and finish.</summary>
        public static KeyboardLayout GameGenie { get; } = new("Game Genie", new[]
        {
            Row("A P Z L G I T Y"),
            Row("E O X U K S V N"),
            Row("+ - Space:2 Erase:4"),
            Row("Next:4 Done:4"),
        });

        /// <summary>A layout for words: digits, the English alphabet, common punctuation, shift and space.</summary>
        public static KeyboardLayout Letters { get; } = new("Letters", new[]
        {
            Row("1 2 3 4 5 6 7 8 9 0"),
            Row("q w e r t y u i o p"),
            Row("a s d f g h j k l '"),
            Row("Shift z x c v b n m , ."),
            Row("Next:2 - Space:4 Erase:2 Done:1"),
        });

        /// <summary>Makes a layout from rows of keys.</summary>
        /// <param name="name">What the layout is called, which is what the key that switches to it says.</param>
        /// <param name="rows">Each row's keys, left to right, with the width each takes in key units.</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="rows"/> is null.</exception>
        /// <exception cref="ArgumentException">There are no rows, a row is empty, or a key is blank or narrower than one unit.</exception>
        public KeyboardLayout(string name, IEnumerable<IReadOnlyList<(string Key, int Width)>> rows)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Rows = rows?.Select(r => (IReadOnlyList<(string, int)>)r.ToArray()).ToArray() ?? throw new ArgumentNullException(nameof(rows));

            if (Rows.Count == 0) throw new ArgumentException("A layout needs at least one row.", nameof(rows));
            foreach (IReadOnlyList<(string Key, int Width)> row in Rows)
            {
                if (row.Count == 0) throw new ArgumentException("A row needs at least one key.", nameof(rows));
                if (row.Any(k => string.IsNullOrEmpty(k.Key) || k.Width < 1))
                    throw new ArgumentException("Every key needs text and a width of at least one.", nameof(rows));
            }
        }

        /// <summary>What the layout is called.</summary>
        public string Name { get; }

        /// <summary>The rows, top first; each key with its width in key units.</summary>
        public IReadOnlyList<IReadOnlyList<(string Key, int Width)>> Rows { get; }

        /// <summary>Reads one row written as keys separated by spaces, each optionally followed by a colon and its width.</summary>
        /// <param name="keys">For example "1 2 3 Space:4 Erase:2", where Space takes four units and Erase two.</param>
        /// <returns>The row's keys with their widths, in the shape the constructor takes.</returns>
        public static IReadOnlyList<(string Key, int Width)> Row(string keys) =>
            keys.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(k =>
            {
                int colon = k.LastIndexOf(':');
                return colon > 0 && int.TryParse(k[(colon + 1)..], out int width) ? (k[..colon], width) : (k, 1);
            }).ToArray();
    }
}
