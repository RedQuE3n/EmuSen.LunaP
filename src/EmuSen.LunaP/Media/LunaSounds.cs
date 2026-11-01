using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EmuSen.LunaP.Media
{
    /// <summary>The interface's nine sound cues.</summary>
    public enum LunaSoundCue
    {
        /// <summary>The focus moved one place.</summary>
        Move,
        /// <summary>A section or a console was stepped.</summary>
        Section,
        /// <summary>Something was chosen.</summary>
        Choose,
        /// <summary>Back one level.</summary>
        Back,
        /// <summary>A favourite was marked or cleared.</summary>
        Favourite,
        /// <summary>A game is starting.</summary>
        Launch,
        /// <summary>A state or a setting was saved.</summary>
        Saved,
        /// <summary>A state was loaded.</summary>
        Loaded,
        /// <summary>A press was refused.</summary>
        Refused,
    }

    /// <summary>One tone of a synthesised sound: its start and end pitch in hertz, when it starts and how long it lasts, in seconds.</summary>
    /// <param name="From">The pitch at the start.</param>
    /// <param name="To">The pitch at the end, glided to linearly.</param>
    /// <param name="Start">When the tone starts.</param>
    /// <param name="Length">How long it lasts.</param>
    public readonly record struct LunaTone(double From, double To, double Start, double Length);

    // The cues synthesised from tones written here, never copied from anyone's sounds - see docs/LunaP.md §206.6.
    /// <summary>The interface's sound cues, synthesised as 48 kHz stereo 32-bit float samples, and the rules for playing them.</summary>
    public static class LunaSounds
    {
        /// <summary>The sample rate of every cue.</summary>
        public const int Rate = 48000;

        /// <summary>The cues' volume until the player changes it: 60%.</summary>
        public const double DefaultVolume = 0.6;

        /// <summary>Whether the cues are on until the player turns them off.</summary>
        public const bool OnByDefault = true;

        /// <summary>How far under the game a cue that may play over gameplay is mixed, in decibels.</summary>
        public const double UnderGameplayDecibels = -12;

        private static readonly IReadOnlyDictionary<LunaSoundCue, LunaTone[]> Designs = new Dictionary<LunaSoundCue, LunaTone[]>
        {
            [LunaSoundCue.Move] = [new(1500, 1500, 0, 0.03)],
            [LunaSoundCue.Section] = [new(520, 780, 0, 0.07)],
            [LunaSoundCue.Choose] = [new(700, 1400, 0, 0.09)],
            [LunaSoundCue.Back] = [new(1400, 700, 0, 0.09)],
            [LunaSoundCue.Favourite] = [new(880, 880, 0, 0.05), new(1109, 1109, 0.05, 0.05), new(1319, 1319, 0.1, 0.08)],
            [LunaSoundCue.Launch] = [new(330, 1320, 0, 0.25)],
            [LunaSoundCue.Saved] = [new(880, 880, 0, 0.06), new(1320, 1320, 0.06, 0.06)],
            [LunaSoundCue.Loaded] = [new(1320, 1320, 0, 0.06), new(880, 880, 0.06, 0.06)],
            [LunaSoundCue.Refused] = [new(220, 220, 0, 0.12)],
        };

        private static readonly ConcurrentDictionary<LunaSoundCue, byte[]> Made = new();

        /// <summary>Every cue, in declaration order.</summary>
        public static IReadOnlyList<LunaSoundCue> Cues { get; } = Enum.GetValues<LunaSoundCue>();

        /// <summary>The tones a cue is made of.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>The tones the cue is made of.</returns>
        public static IReadOnlyList<LunaTone> TonesOf(LunaSoundCue cue) => Designs[cue];

        /// <summary>A cue's samples, made once.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>48 kHz stereo 32-bit float samples, interleaved.</returns>
        public static byte[] Samples(LunaSoundCue cue) => Made.GetOrAdd(cue, c => Render(Designs[c]));

        /// <summary>Whether a cue may play over running gameplay, under the game; the others play only in menus and cards.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>True for Saved and Loaded.</returns>
        public static bool PlaysOverGameplay(LunaSoundCue cue) => cue is LunaSoundCue.Saved or LunaSoundCue.Loaded;

        // Each tone a sine with a quarter of its octave, a 3 ms rise and an exponential fall, the pitch glided linearly.
        /// <summary>Synthesises tones into 48 kHz stereo 32-bit float samples.</summary>
        /// <param name="tones">The tones, mixed where they overlap.</param>
        /// <returns>The samples, interleaved.</returns>
        public static byte[] Render(IReadOnlyList<LunaTone> tones)
        {
            double end = 0;
            foreach (LunaTone t in tones) end = Math.Max(end, t.Start + t.Length);
            int frames = (int)Math.Ceiling(end * Rate);
            var mix = new float[frames];
            foreach (LunaTone t in tones)
            {
                int first = (int)(t.Start * Rate), count = (int)(t.Length * Rate);
                double phase = 0;
                for (int i = 0; i < count && first + i < frames; i++)
                {
                    double at = (double)i / Rate, hz = t.From + (t.To - t.From) * i / count;
                    phase += 2 * Math.PI * hz / Rate;
                    double envelope = Math.Min(1, at / 0.003) * Math.Exp(-4 * at / t.Length);
                    mix[first + i] += (float)(0.3 * envelope * (Math.Sin(phase) + 0.25 * Math.Sin(2 * phase)));
                }
            }
            var bytes = new byte[frames * 8];
            for (int i = 0; i < frames; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 8), mix[i]);
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 8 + 4), mix[i]);
            }
            return bytes;
        }
    }
}
