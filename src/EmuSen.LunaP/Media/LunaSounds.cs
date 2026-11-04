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

        // The Web Audio graph of the draft: dry partials plus a fed-back delay line through its low-pass biquad, trimmed where it falls 60 dB under the loudest sample - see docs/LunaP.md §219.4.
        /// <summary>Synthesises a chime's bells, one per strike time, into 48 kHz stereo 32-bit float samples starting at the first strike.</summary>
        /// <param name="chime">The chime to synthesise.</param>
        /// <param name="strikes">When each bell is struck, in seconds, in ladder order; the samples begin at the first.</param>
        /// <returns>The samples, interleaved; empty for no strikes.</returns>
        public static byte[] Render(SplashChime chime, IReadOnlyList<double> strikes)
        {
            ArgumentNullException.ThrowIfNull(chime);
            ArgumentNullException.ThrowIfNull(strikes);
            if (strikes.Count == 0) return [];
            double start = strikes[0], last = start;
            foreach (double t in strikes) last = Math.Max(last, t);
            int delay = chime.Echo > 0 ? Math.Max(1, (int)Math.Round(chime.Echo * Rate)) : 0;
            double longest = 0;
            foreach (BellPartial p in chime.Partials) longest = Math.Max(longest, p.Decay + chime.Tail);
            double lastEnd = last - start + longest;
            int frames = (int)Math.Ceiling(lastEnd * Rate) + delay * 10 + 1;
            var dry = new double[frames];
            var detune = new Controls.SplashRandom(chime.DetuneSeed);
            for (int n = 0; n < strikes.Count; n++)
            {
                int first = (int)Math.Round((strikes[n] - start) * Rate);
                foreach (BellPartial p in chime.Partials)
                {
                    double hz = chime.NoteOf(n) * p.Ratio * (1 + (detune.Next() - 0.5) * 2 * chime.Detune);
                    double w = 2 * Math.PI * hz / Rate, fall = Math.Log(0.0001 / p.Amplitude) / (p.Decay - chime.Attack);
                    int count = (int)((p.Decay + chime.Tail) * Rate);
                    for (int i = 0; i < count && first + i < frames; i++)
                    {
                        double at = (double)i / Rate;
                        double level = at < chime.Attack ? p.Amplitude * at / chime.Attack : at < p.Decay ? p.Amplitude * Math.Exp(fall * (at - chime.Attack)) : 0.0001;
                        dry[first + i] += level * Math.Sin(w * i);
                    }
                }
            }
            var mix = new float[frames];
            var echo = new double[frames];
            double loudest = 0;
            double w0 = 2 * Math.PI * chime.Lowpass / Rate, cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * Math.Pow(10, chime.LowpassQ / 20)), a0 = 1 + alpha;
            double b0 = (1 - cos) / 2 / a0, b1 = (1 - cos) / a0, b2 = b0, a1 = -2 * cos / a0, a2 = (1 - alpha) / a0;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            for (int i = 0; i < frames; i++)
            {
                echo[i] = delay > 0 && i >= delay ? dry[i - delay] + chime.Feedback * echo[i - delay] : 0;
                double x = chime.Gain * (dry[i] + echo[i]);
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                (x2, x1, y2, y1) = (x1, x, y1, y);
                mix[i] = (float)y;
                loudest = Math.Max(loudest, Math.Abs(mix[i]));
            }
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
