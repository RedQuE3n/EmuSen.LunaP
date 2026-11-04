using System;
using System.Collections.Generic;
using EmuSen.LunaP.Media;

namespace EmuSen.LunaP.Controls
{
    /// <summary>A part of a splash's timeline: when it starts and ends, in the timeline's own seconds before its pace.</summary>
    /// <param name="From">When the part starts.</param>
    /// <param name="To">When the part has finished.</param>
    public readonly record struct SplashSpan(double From, double To)
    {
        /// <summary>How far the part has gone at a time, 0 before it starts and 1 once it has finished.</summary>
        /// <param name="t">The time, in the timeline's own seconds.</param>
        /// <returns>The fraction, clamped to [0, 1].</returns>
        public double At(double t) => To <= From ? (t >= To ? 1 : 0) : Math.Clamp((t - From) / (To - From), 0, 1);
    }

    /// <summary>One planet of a splash's row: its name and its colour's hue in degrees.</summary>
    /// <param name="Name">What the planet stands for.</param>
    /// <param name="Hue">The hue, 0 to 360.</param>
    public sealed record SplashBody(string Name, double Hue);

    /// <summary>What a splash shows of one part of its frame: the part's name and its bounds in the control.</summary>
    /// <param name="Name">The part: title, caption, moon, halo, or a planet's name.</param>
    /// <param name="Bounds">Where it is drawn.</param>
    public sealed record SplashPart(string Name, Avalonia.Rect Bounds);

    // The draft's timeline as plain data, in its own seconds; Pace stretches them and Length holds the finished frame - see docs/LunaP.md §219.2.
    /// <summary>A splash's whole timing as data: every part's span, the pace they are stretched by, how long the splash lasts, the stars and the chime.</summary>
    /// <param name="Pace">How much every span is stretched: the frame at time t is the draft's frame at t / Pace.</param>
    /// <param name="Length">How long the splash lasts, in seconds; the finished frame holds from the last span's end until then.</param>
    /// <param name="Stars">The star field fading in.</param>
    /// <param name="Stroke">The moon's outline drawn.</param>
    /// <param name="Halo">The halo round the moon.</param>
    /// <param name="Fill">The moon filled to a crescent.</param>
    /// <param name="Appear">The first planet appearing; each later one is AppearStagger later.</param>
    /// <param name="AppearStagger">The delay between one planet's appearing and the next's.</param>
    /// <param name="Settle">The planets settling from their orbits into the row.</param>
    /// <param name="SettleRate">How much faster than Settle each planet's own settling runs.</param>
    /// <param name="SettleStagger">How much of its settling each later planet starts behind the one before.</param>
    /// <param name="OrbitSwing">How far round its orbit, in radians, a planet travels while it settles.</param>
    /// <param name="Sweep">The light sweeping along the row at a steady speed; each planet flares and rings its bell as it passes.</param>
    /// <param name="FlareWidth">How wide a planet's flare is about the sweep, in units of the height over 720.</param>
    /// <param name="Line">The row's line growing from the centre.</param>
    /// <param name="Words">The title and caption settling in.</param>
    /// <param name="StarCount">How many stars.</param>
    /// <param name="Seed">The seed of the generator that places the stars and the orbits.</param>
    /// <param name="Chime">The sound that goes with it.</param>
    public sealed record SplashTimeline(double Pace, double Length, SplashSpan Stars, SplashSpan Stroke, SplashSpan Halo, SplashSpan Fill,
        SplashSpan Appear, double AppearStagger, SplashSpan Settle, double SettleRate, double SettleStagger, double OrbitSwing,
        SplashSpan Sweep, double FlareWidth, SplashSpan Line, SplashSpan Words, int StarCount, uint Seed, SplashChime Chime)
    {
        /// <summary>Moonrise, the big screen's start-up splash approved on 2026-11-04: 5 s, the draft's spans stretched by 1.1, a bell for each planet.</summary>
        public static SplashTimeline Moonrise { get; } = new(
            Pace: 1.1, Length: 5.0,
            Stars: new(0, 0.9), Stroke: new(0.4, 1.6), Halo: new(0.9, 2.0), Fill: new(1.2, 2.0),
            Appear: new(1.15, 1.6), AppearStagger: 0.03, Settle: new(1.2, 2.7), SettleRate: 1.25, SettleStagger: 0.02, OrbitSwing: 2.6,
            Sweep: new(2.35, 3.65), FlareWidth: 34, Line: new(2.4, 3.7), Words: new(3.45, 4.15), StarCount: 220, Seed: 0x4d6f6f6e,
            Chime: new SplashChime(391.995, [0, 2, 4, 7, 9], [new(1, 1, 0.55), new(2.76, 0.18, 0.22), new(5.40, 0.06, 0.11), new(8.93, 0.02, 0.06)],
                Attack: 0.018, Tail: 0.05, Detune: 0.002, DetuneSeed: 0x42656c6c, Echo: 0, Feedback: 0, Gain: 0.03, Lowpass: 2400, LowpassQ: 0.5));

        /// <summary>The draft's own time for a time since the splash began: clamped to Length, then divided by Pace.</summary>
        /// <param name="seconds">Seconds since the splash began.</param>
        /// <returns>The time the frame is drawn for, in the draft's seconds.</returns>
        public double DraftTime(double seconds) => Math.Clamp(seconds, 0, Length) / Pace;
    }

    // Mulberry32, so a splash's stars and orbits are the same on every showing and in a seeded copy of the draft - see docs/LunaP.md §219.3.
    /// <summary>A small seeded generator of numbers in [0, 1), the one a splash places its stars and orbits with.</summary>
    public sealed class SplashRandom
    {
        private uint _state;

        /// <summary>A generator at the start of its sequence for a seed.</summary>
        /// <param name="seed">The seed the sequence starts from.</param>
        public SplashRandom(uint seed) => _state = seed;

        /// <summary>The next number of the sequence.</summary>
        /// <returns>A number in [0, 1).</returns>
        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                uint t = _state;
                t = (t ^ (t >> 15)) * (1 | t);
                t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>The first numbers of a seed's sequence.</summary>
        /// <param name="seed">The seed the sequence starts from.</param>
        /// <param name="count">How many numbers to give.</param>
        /// <returns>The numbers, in order.</returns>
        public static IReadOnlyList<double> First(uint seed, int count)
        {
            var random = new SplashRandom(seed);
            var numbers = new double[count];
            for (int i = 0; i < count; i++) numbers[i] = random.Next();
            return numbers;
        }
    }
}
