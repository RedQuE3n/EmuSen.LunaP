using System;

namespace EmuSen.LunaP.Controls
{
    // One column of a LunaTable<T> - see docs/LunaP.md §27.
    //
    // WHY THIS EXISTS BESIDE THE THREE-ARGUMENT Column(...) RATHER THAN REPLACING IT. A column began
    // as a heading, a projection and a width, which fits in a method signature and reads well at the
    // call site. Sorting is the first thing that does not fit: it is per-column, it is optional, and
    // it is the first of several - editing and alignment are queued behind it - so carrying on would
    // mean three more optional parameters and then an overload per combination of them.
    //
    // An init-only property on a sealed class is the growth path that stays additive: adding one
    // needs no new overload and breaks no call site. The terse form is kept because most columns are
    // plain - the measured shape is three columns of strings (§27.2) - and it delegates to this, so
    // there is one path from a declaration to a column rather than two that can drift.
    //
    // TAKES A COMPARISON OVER THE MODEL, NOT OVER THE PROJECTED TEXT, and that is the whole reason
    // Sort is declared here instead of being inferred from Text. Sorting the displayed string is a
    // bug that looks like it works: "10" sorts before "9", a number formatted with separators sorts
    // by its separator, and a date written 2/1/2026 sorts by its day. The type that knows how to
    // order two rows is the caller's own, and the caller has one - the same reason Text is a
    // projection rather than an interface this toolkit asks a model to implement.
    /// <summary>One column of a LunaTable&lt;T&gt;: a heading, a projection, and whatever else that column does.</summary>
    /// <typeparam name="T">The row model.</typeparam>
    public sealed class LunaColumn<T> where T : class
    {
        /// <summary>Creates a column.</summary>
        /// <param name="header">The column heading.</param>
        /// <param name="text">Turns a model into this column's cell text. Called for every row on every Refresh, so it should be cheap and free of side effects.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="header"/> or <paramref name="text"/> is null.</exception>
        public LunaColumn(string header, Func<T, string> text)
        {
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The column heading.</summary>
        public string Header { get; }

        /// <summary>Turns a model into this column's cell text.</summary>
        public Func<T, string> Text { get; }

        /// <summary>An Avalonia column width - "*", "2*", "Auto", or a number of pixels. Headers and cells share a size group, so they stay aligned.</summary>
        public string Width { get; init; } = "*";

        // Null is the default and means the column cannot be sorted. A heading with no comparison is
        // left as a label rather than made into a dead button: a control that takes focus and does
        // nothing when pressed is worse for a keyboard user than one that was never a stop.
        /// <summary>How two models compare for this column, or null when the column cannot be sorted.</summary>
        /// <remarks>
        /// Compare the MODELS rather than the strings <see cref="Text"/> produces - "10" sorts before "9",
        /// and a formatted number sorts by its thousands separator. The sort is stable, so rows that compare
        /// equal keep the order they were given to Refresh in.
        /// </remarks>
        public Comparison<T>? Sort { get; init; }
    }
}
