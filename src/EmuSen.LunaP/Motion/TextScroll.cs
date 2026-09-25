using System;

namespace EmuSen.LunaP.Motion
{
    /// <summary>The way a text too long for its box moves by itself: not at all, up, or sideways in a loop.</summary>
    public enum TextScrollDirection
    {
        /// <summary>The text stays put and is cut where it does not fit.</summary>
        None,
        /// <summary>The lines move up until the last shows, pause, and start again from the top, fading in.</summary>
        Vertical,
        /// <summary>One line moves left in a loop, a second copy following the first after a gap.</summary>
        Horizontal,
    }

    // Where a self-scrolling text is at a time since it was shown: the two rules a looping line and a column of lines follow - see docs/LunaP.md §102.3.
    /// <summary>The position and opacity of a text that scrolls by itself, as a function of the time since it was shown; FontText and TextRowList read it, and a host can too.</summary>
    /// <param name="Delay">How long the text stays still before each pass.</param>
    /// <param name="Speed">How fast it moves, in pixels a second.</param>
    /// <param name="Gap">Horizontal: the distance, in pixels, between the end of one copy and the start of the next.</param>
    /// <param name="EndPause">Vertical: how long the last lines stay shown before the text starts again.</param>
    /// <param name="FadeIn">Vertical: how long the text takes to fade in at the top after each pass; the delay counts from its end.</param>
    /// <param name="WholePixels">Vertical: whether the column moves in whole-pixel steps rather than smoothly.</param>
    public readonly record struct TextScroll(TimeSpan Delay, double Speed, double Gap = 0, TimeSpan EndPause = default, TimeSpan FadeIn = default, bool WholePixels = false)
    {
        private double Step(double offset) => WholePixels ? Math.Floor(offset + 1e-9) : offset;

        /// <summary>A line's leftward offset at a time: still for Delay, then moving at Speed until one copy has replaced the other, then still again for Delay.</summary>
        /// <param name="elapsed">Time since the text was shown.</param>
        /// <param name="width">The line's own width in pixels.</param>
        /// <returns>The offset in pixels, from 0 to below width plus Gap.</returns>
        public double LoopOffset(TimeSpan elapsed, double width)
        {
            double period = width + Math.Max(0, Gap);
            if (Speed <= 0 || period <= 0 || elapsed <= Delay) return 0;
            double moving = period / Speed, cycle = Delay.TotalSeconds + moving;
            double into = elapsed.TotalSeconds % cycle;
            return into <= Delay.TotalSeconds ? 0 : Math.Min(period, (into - Delay.TotalSeconds) * Speed) % period;
        }

        /// <summary>A column's upward offset and opacity at a time: fading in (after the first pass), still for Delay, moving at Speed until the last line shows, then still for EndPause.</summary>
        /// <param name="elapsed">Time since the text was shown.</param>
        /// <param name="travel">How far the column must move for its last line to show: its height less the box's.</param>
        /// <returns>The offset in pixels and the opacity from 0 to 1.</returns>
        public (double Offset, double Opacity) RunAt(TimeSpan elapsed, double travel)
        {
            if (Speed <= 0 || travel <= 0 || elapsed <= Delay) return (0, 1);
            double delay = Delay.TotalSeconds, fade = Math.Max(0, FadeIn.TotalSeconds), moving = travel / Speed, pause = Math.Max(0, EndPause.TotalSeconds);
            double t = elapsed.TotalSeconds, first = delay + moving + pause;
            if (t < first) return (Step(Math.Min(travel, Math.Max(0, t - delay) * Speed)), 1);
            double cycle = fade + delay + moving + pause, into = (t - first) % cycle;
            if (into < fade) return (0, into / fade);
            into -= fade;
            if (into <= delay) return (0, 1);
            return (Step(Math.Min(travel, (into - delay) * Speed)), 1);
        }
    }
}
