using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EmuSen.LunaP.Controls;

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

    /// <summary>One partial of a struck bell: its frequency as a multiple of the note, its amplitude and how long it takes to die away.</summary>
    /// <param name="Ratio">The partial's frequency over the note's.</param>
    /// <param name="Amplitude">Its peak level.</param>
    /// <param name="Decay">When, after the strike, it has fallen exponentially to a ten-thousandth.</param>
    public readonly record struct BellPartial(double Ratio, double Amplitude, double Decay);

    /// <summary>A bell for each strike, struck when the host says, its pitch climbing a scale: the splash's sound, as data.</summary>
    /// <param name="Root">The first bell's pitch in hertz.</param>
    /// <param name="Ladder">The scale's steps in semitones above the root within an octave; the bells climb it, an octave higher each time round.</param>
    /// <param name="Partials">The partials every strike is made of.</param>
    /// <param name="Attack">How long a partial takes to rise to its peak.</param>
    /// <param name="Tail">How long a partial sounds after its decay before it stops.</param>
    /// <param name="Detune">The widest random detune of a partial, as a fraction of its frequency either way.</param>
    /// <param name="DetuneSeed">The seed of the generator that detunes the partials, so every render is the same.</param>
    /// <param name="Echo">The echo's delay, in seconds; 0 for none, the sound fully dry.</param>
    /// <param name="Feedback">How much of the echo is fed back into it.</param>
    /// <param name="Gain">The level of the whole mix.</param>
    /// <param name="Lowpass">The corner, in hertz, of the low-pass filter the whole mix passes through.</param>
    /// <param name="LowpassQ">The filter's Q in decibels, as a Web Audio low-pass reads it.</param>
    public sealed record SplashChime(double Root, IReadOnlyList<int> Ladder, IReadOnlyList<BellPartial> Partials,
        double Attack, double Tail, double Detune, uint DetuneSeed, double Echo, double Feedback, double Gain, double Lowpass, double LowpassQ)
    {
        /// <summary>The pitch of a bell along the ladder, in equal temperament from the root.</summary>
        /// <param name="bell">The bell's index, 0 for the root.</param>
        /// <returns>The pitch in hertz.</returns>
        public double NoteOf(int bell) => Root * Math.Pow(2, (12 * (bell / Ladder.Count) + Ladder[bell % Ladder.Count]) / 12.0);
    }

    /// <summary>One strike of a bell on the splash's ladder: which rung, when, and how it sounds.</summary>
    /// <param name="Note">The ladder rung struck, 0 for G4.</param>
    /// <param name="Start">When it is struck, in seconds from the sound's start.</param>
    /// <param name="Decay">The factor the partials' falls are multiplied by.</param>
    /// <param name="Gain">The strike's level.</param>
    /// <param name="Lowpass">The corner, in hertz, of the low-pass the strike passes through.</param>
    public readonly record struct LunaBell(int Note, double Start, double Decay, double Gain, double Lowpass = 2400);

    // The cues are Moonrise's bell on its ladder, never copied from anyone's sounds - see docs/LunaP.md §220.
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

        private static readonly IReadOnlyDictionary<LunaSoundCue, LunaBell[]> Designs = new Dictionary<LunaSoundCue, LunaBell[]>
        {
            [LunaSoundCue.Move] = [new(8, 0, 0.3, 0.12)],
            [LunaSoundCue.Section] = [new(5, 0, 0.55, 0.18)],
            [LunaSoundCue.Choose] = [new(5, 0, 0.5, 0.22), new(8, 0.07, 0.6, 0.22)],
            [LunaSoundCue.Back] = [new(8, 0, 0.45, 0.18), new(5, 0.07, 0.5, 0.18)],
            [LunaSoundCue.Favourite] = [new(5, 0, 0.55, 0.17), new(7, 0.055, 0.55, 0.17), new(8, 0.11, 0.55, 0.17), new(10, 0.165, 0.55, 0.17)],
            [LunaSoundCue.Launch] = [new(0, 0, 0.7, 0.24), new(1, 0.075, 0.7, 0.24), new(2, 0.15, 0.7, 0.24), new(3, 0.225, 0.7, 0.24), new(4, 0.3, 0.7, 0.24), new(5, 0.375, 1.2, 0.24)],
            [LunaSoundCue.Saved] = [new(7, 0, 0.45, 0.18), new(9, 0.08, 0.6, 0.18)],
            [LunaSoundCue.Loaded] = [new(9, 0, 0.45, 0.18), new(7, 0.08, 0.6, 0.18)],
            [LunaSoundCue.Refused] = [new(0, 0, 0.25, 0.26, 900), new(0, 0.11, 0.25, 0.22, 900)],
        };

        private static readonly LunaBell[] QuickSelectBells = [new(4, 0, 0.45, 0.22), new(6, 0.06, 0.55, 0.22)];

        private static readonly ConcurrentDictionary<LunaSoundCue, byte[]> Made = new();
        private static readonly ConcurrentDictionary<int, byte[]> MadeBells = new();
        private static readonly Lazy<byte[]> MadeQuick = new(() => RenderBells(SplashTimeline.Moonrise.Chime, QuickSelectBells));

        /// <summary>Every cue, in declaration order.</summary>
        public static IReadOnlyList<LunaSoundCue> Cues { get; } = Enum.GetValues<LunaSoundCue>();

        /// <summary>The strikes of ES-DE's quick system select, which has no cue of its own.</summary>
        public static IReadOnlyList<LunaBell> QuickSelect => QuickSelectBells;

        /// <summary>The bells a cue is made of.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>The cue's strikes.</returns>
        public static IReadOnlyList<LunaBell> BellsOf(LunaSoundCue cue) => Designs[cue];

        /// <summary>A cue's samples, made once.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>48 kHz stereo 32-bit float samples, interleaved.</returns>
        public static byte[] Samples(LunaSoundCue cue) => Made.GetOrAdd(cue, c => RenderBells(SplashTimeline.Moonrise.Chime, Designs[c]));

        /// <summary>The quick system select's samples, made once.</summary>
        /// <returns>48 kHz stereo 32-bit float samples, interleaved.</returns>
        public static byte[] QuickSelectSamples() => MadeQuick.Value;

        /// <summary>One bell on a ladder rung, as a console rings it in Choose a console; made once.</summary>
        /// <param name="rung">The rung, 0 for G4, up to 12 for B6 and beyond as the ladder climbs.</param>
        /// <returns>48 kHz stereo 32-bit float samples, interleaved.</returns>
        public static byte[] BellSamples(int rung)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(rung);
            return MadeBells.GetOrAdd(rung, r => RenderBells(SplashTimeline.Moonrise.Chime, [new LunaBell(r, 0, 0.6, 0.18)]));
        }

        /// <summary>Whether a cue may play over running gameplay, under the game; the others play only in menus and cards.</summary>
        /// <param name="cue">The cue asked about.</param>
        /// <returns>True for Saved and Loaded.</returns>
        public static bool PlaysOverGameplay(LunaSoundCue cue) => cue is LunaSoundCue.Saved or LunaSoundCue.Loaded;

        /// <summary>Synthesises a chime's bells, one per strike time, into 48 kHz stereo 32-bit float samples starting at the first strike.</summary>
        /// <param name="chime">The chime to synthesise.</param>
        /// <param name="strikes">When each bell is struck, in seconds, in ladder order; the samples begin at the first.</param>
        /// <returns>The samples, interleaved; empty for no strikes.</returns>
        public static byte[] Render(SplashChime chime, IReadOnlyList<double> strikes)
        {
            ArgumentNullException.ThrowIfNull(chime);
            ArgumentNullException.ThrowIfNull(strikes);
            var bells = new LunaBell[strikes.Count];
            for (int n = 0; n < bells.Length; n++) bells[n] = new LunaBell(n, strikes[n], 1, chime.Gain, chime.Lowpass);
            return RenderBells(chime, bells);
        }

        // The Web Audio graph of the draft: dry partials, a fed-back delay line and a low-pass biquad per corner, trimmed where the mix falls 60 dB under its loudest sample - see docs/LunaP.md §219.4 and §220.1.
        /// <summary>Synthesises bells into 48 kHz stereo 32-bit float samples starting at the first strike; the chime gives the ladder, partials, attack, tail, detune, echo and filter Q, the bells their rung, time, decay, gain and corner.</summary>
        /// <param name="chime">The chime whose bell is struck.</param>
        /// <param name="bells">The strikes, mixed where they overlap.</param>
        /// <returns>The samples, interleaved; empty for no strikes.</returns>
        public static byte[] RenderBells(SplashChime chime, IReadOnlyList<LunaBell> bells)
        {
            ArgumentNullException.ThrowIfNull(chime);
            ArgumentNullException.ThrowIfNull(bells);
            if (bells.Count == 0) return [];
            double start = double.MaxValue, lastEnd = 0;
            foreach (LunaBell b in bells) start = Math.Min(start, b.Start);
            foreach (LunaBell b in bells)
                foreach (BellPartial p in chime.Partials) lastEnd = Math.Max(lastEnd, b.Start - start + Fall(chime, p, b) + chime.Tail);
            int delay = chime.Echo > 0 ? Math.Max(1, (int)Math.Round(chime.Echo * Rate)) : 0;
            int frames = (int)Math.Ceiling(lastEnd * Rate) + delay * 10 + 1;
            var dry = new Dictionary<double, double[]>();
            var detune = new SplashRandom(chime.DetuneSeed);
            foreach (LunaBell b in bells)
            {
                if (!dry.TryGetValue(b.Lowpass, out double[]? buffer)) dry[b.Lowpass] = buffer = new double[frames];
                int first = (int)Math.Round((b.Start - start) * Rate);
                foreach (BellPartial p in chime.Partials)
                {
                    double hz = chime.NoteOf(b.Note) * p.Ratio * (1 + (detune.Next() - 0.5) * 2 * chime.Detune), amplitude = b.Gain * p.Amplitude, fall = Fall(chime, p, b);
                    double w = 2 * Math.PI * hz / Rate, rate = Math.Log(0.0001 / p.Amplitude) / (fall - chime.Attack);
                    int count = (int)((fall + chime.Tail) * Rate);
                    for (int i = 0; i < count && first + i < frames; i++)
                    {
                        double at = (double)i / Rate;
                        double level = at < chime.Attack ? amplitude * at / chime.Attack : at < fall ? amplitude * Math.Exp(rate * (at - chime.Attack)) : b.Gain * 0.0001;
                        buffer[first + i] += level * Math.Sin(w * i);
                    }
                }
            }
            var mix = new float[frames];
            var echo = new double[frames];
            foreach ((double corner, double[] line) in dry)
            {
                double w0 = 2 * Math.PI * corner / Rate, cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * Math.Pow(10, chime.LowpassQ / 20)), a0 = 1 + alpha;
                double b0 = (1 - cos) / 2 / a0, b1 = (1 - cos) / a0, b2 = b0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0;
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                Array.Clear(echo);
                for (int i = 0; i < frames; i++)
                {
                    echo[i] = delay > 0 && i >= delay ? line[i - delay] + chime.Feedback * echo[i - delay] : 0;
                    double x = line[i] + echo[i];
                    double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    (x2, x1, y2, y1) = (x1, x, y1, y);
                    mix[i] += (float)y;
                }
            }
            double loudest = 0;
            foreach (float v in mix) loudest = Math.Max(loudest, Math.Abs(v));
            int end = frames;
            while (end > 0 && Math.Abs(mix[end - 1]) < loudest * 0.001) end--;
            var bytes = new byte[end * 8];
            for (int i = 0; i < end; i++)
            {
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 8), mix[i]);
                BitConverter.TryWriteBytes(bytes.AsSpan(i * 8 + 4), mix[i]);
            }
            return bytes;
        }

        // When a partial of a strike has fallen to a ten-thousandth: its decay scaled by the strike's, never under the attack and 10 ms.
        private static double Fall(SplashChime chime, BellPartial p, LunaBell b) => Math.Max(chime.Attack + 0.01, p.Decay * b.Decay);
    }
}
