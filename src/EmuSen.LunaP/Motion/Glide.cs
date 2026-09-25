using System;
using Avalonia.Animation.Easings;

namespace EmuSen.LunaP.Motion
{
    // A value moving from one number to another over a span of a host's clock, read at any time value and never from wall time - see docs/LunaP.md §102.1.
    /// <summary>A number moving from From to To between Start and Start plus Duration on a clock the host owns, shaped by an easing; read it with ValueAt at any time.</summary>
    /// <param name="From">The value at Start and before it.</param>
    /// <param name="To">The value from Start plus Duration on.</param>
    /// <param name="Start">The time on the host's clock the move begins.</param>
    /// <param name="Duration">How long the move takes; zero or less jumps to To at once.</param>
    /// <param name="Easing">The shape of the move; null is linear.</param>
    public readonly record struct Glide(double From, double To, TimeSpan Start, TimeSpan Duration, Easing? Easing = null)
    {
        /// <summary>A glide already at rest at a value.</summary>
        /// <param name="value">The value it holds at every time.</param>
        /// <returns>A glide whose value is always the given one.</returns>
        public static Glide At(double value) => new(value, value, TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>The time on the host's clock the move is over.</summary>
        public TimeSpan End => Duration > TimeSpan.Zero ? Start + Duration : Start;

        /// <summary>The value at a time on the host's clock.</summary>
        /// <param name="now">The host's time.</param>
        /// <returns>From before Start, To from End on, and the eased value between.</returns>
        public double ValueAt(TimeSpan now)
        {
            if (Duration <= TimeSpan.Zero || now >= End) return To;
            if (now <= Start) return From;
            double t = (now - Start).Ticks / (double)Duration.Ticks;
            double eased = Easing?.Ease(t) ?? t;
            return From + (To - From) * eased;
        }

        /// <summary>Whether the move is over at a time.</summary>
        /// <param name="now">The host's time.</param>
        /// <returns>True from End on.</returns>
        public bool IsSettledAt(TimeSpan now) => now >= End;

        /// <summary>A new move to another value, starting at a time from wherever this one has got to, so an interrupted move continues without a jump.</summary>
        /// <param name="to">The new destination.</param>
        /// <param name="now">The host's time the new move starts.</param>
        /// <param name="duration">How long the new move takes.</param>
        /// <param name="easing">Its shape; null is linear.</param>
        /// <returns>The new glide.</returns>
        public Glide Toward(double to, TimeSpan now, TimeSpan duration, Easing? easing = null) => new(ValueAt(now), to, now, duration, easing);
    }
}
