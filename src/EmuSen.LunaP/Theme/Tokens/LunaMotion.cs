using System;
using Avalonia.Animation.Easings;

namespace EmuSen.LunaP.Theme
{
    /// <summary>The interface's motion tokens: what kind of change a movement is, which fixes its duration and easing.</summary>
    public enum LunaMotionToken
    {
        /// <summary>No animation: a list's cursor, the help bar's change.</summary>
        Instant,
        /// <summary>120 ms, quadratic ease-out: the ring between tiles, a tile's scale, a stepped value.</summary>
        Quick,
        /// <summary>200 ms, quadratic ease-out: a card or a pane arriving.</summary>
        Enter,
        /// <summary>160 ms, quadratic ease-in: a card or a pane leaving.</summary>
        Exit,
        /// <summary>320 ms, cubic ease-out: moving between screens.</summary>
        Pan,
        /// <summary>400 ms, quadratic ease-out: a carousel or a shelf stepping, as ES-DE's own.</summary>
        Reel,
        /// <summary>150 ms, linear: facts fading while a list scrolls.</summary>
        Fade,
        /// <summary>1.5 s before a hero picture would turn into its clip, which then cross-fades over Enter (docs/LunaP.md §209.3).</summary>
        Poster,
    }

    // Durations and easings of the motion tokens, and reduced motion - see docs/LunaP.md §206.5.
    /// <summary>The duration and easing of each motion token, shortened to nothing (fades to 120 ms) when motion is reduced.</summary>
    public static class LunaMotion
    {
        /// <summary>How long a fade lasts while motion is reduced.</summary>
        public static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(120);

        /// <summary>How long the focus waits idle before its ring starts to breathe.</summary>
        public static readonly TimeSpan BreathDelay = TimeSpan.FromSeconds(2);

        /// <summary>One breath of the idle ring, full to dim and back.</summary>
        public static readonly TimeSpan BreathPeriod = TimeSpan.FromMilliseconds(1600);

        /// <summary>The idle ring's opacity at the bottom of a breath.</summary>
        public const double BreathLow = 0.7;

        /// <summary>Whether motion is reduced, by the system's preference or the player's switch. False by default.</summary>
        public static bool Reduced { get; set; }

        /// <summary>Whether the idle focus ring breathes: not while motion is reduced.</summary>
        public static bool Breathes => !Reduced;

        /// <summary>A token's full duration, ignoring reduced motion.</summary>
        /// <param name="token">The motion token asked about.</param>
        /// <returns>The token's full duration.</returns>
        public static TimeSpan Nominal(LunaMotionToken token) => TimeSpan.FromMilliseconds(token switch
        {
            LunaMotionToken.Quick => 120,
            LunaMotionToken.Enter => 200,
            LunaMotionToken.Exit => 160,
            LunaMotionToken.Pan => 320,
            LunaMotionToken.Reel => 400,
            LunaMotionToken.Fade => 150,
            LunaMotionToken.Poster => 1500,
            _ => 0,
        });

        /// <summary>How long a movement of the token lasts: its nominal duration, or nothing while motion is reduced.</summary>
        /// <param name="token">The motion token asked about.</param>
        /// <returns>The duration to animate a position or a size over.</returns>
        public static TimeSpan Duration(LunaMotionToken token) => Reduced ? TimeSpan.Zero : Nominal(token);

        /// <summary>How long a fade of the token lasts: its nominal duration, or 120 ms while motion is reduced; nothing for Instant.</summary>
        /// <param name="token">The motion token asked about.</param>
        /// <returns>The duration to animate an opacity over.</returns>
        public static TimeSpan FadeDuration(LunaMotionToken token) =>
            token == LunaMotionToken.Instant ? TimeSpan.Zero : Reduced ? ReducedFade : Nominal(token);

        /// <summary>A token's easing.</summary>
        /// <param name="token">The motion token asked about.</param>
        /// <returns>A new easing of the token's curve.</returns>
        public static Easing Easing(LunaMotionToken token) => token switch
        {
            LunaMotionToken.Exit => new QuadraticEaseIn(),
            LunaMotionToken.Pan => new CubicEaseOut(),
            LunaMotionToken.Fade or LunaMotionToken.Instant => new LinearEasing(),
            _ => new QuadraticEaseOut(),
        };
    }
}
