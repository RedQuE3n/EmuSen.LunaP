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

        // WRITING IS THE CALLER'S JOB, WHICH IS WHY THIS IS AN ACTION AND NOT A SETTER PATH. The
        // column already turns a model into text one way; the reverse needs to know the model's
        // type, its property, and how to parse a string into it, and only the caller knows all
        // three. `(item, text) => item.Name = text` is the whole of it at the call site, and a
        // record with an init-only property is `(item, text) => Replace(item with { Name = text })`
        // - which a projection-based design could not have expressed at all.
        //
        // Null is the default and means the column is READ-ONLY. That is the important half: a table
        // is read-only until a caller says otherwise per column, so adding editing to this toolkit
        // changed no existing table's behaviour (§26.13).
        /// <summary>Writes an edited value back to the model, or null when the column cannot be edited.</summary>
        /// <remarks>
        /// Called only after <see cref="Validate"/> has returned null for the same text. Receives the raw
        /// string from the editor; parsing and conversion are the caller's, because only the caller knows
        /// what the column means.
        /// </remarks>
        public Action<T, string>? Commit { get; init; }

        // Returns the PROBLEM, not a bool, for the same reason FieldRow.Error is a string and there
        // is no IsValid beside it (§49.1): a cell that refuses a value without saying why is a cell
        // the user cannot fix. Null means valid, so the common case stays quiet.
        //
        // Runs BEFORE Commit and can veto it. Parsing lives here in practice - a Validate that tries
        // int.TryParse and returns "Not a number." is the shape this is for - which keeps Commit free
        // to assume the text is good.
        /// <summary>Checks an edited value, returning what is wrong with it, or null when it is acceptable. Null itself means the column never rejects anything.</summary>
        public Func<T, string, string?>? Validate { get; init; }

        // Asked in three places, and a property rather than three `Commit is not null` tests, because
        // "can this column be edited" is the question being asked and not "is this delegate set".
        /// <summary>Whether this column can be edited, which is exactly whether it was given a Commit.</summary>
        public bool IsEditable => Commit is not null;

        // BOUNDS ON A DRAG, and they only mean anything once a column can be dragged at all (§27.11).
        // A star column with no floor collapses to nothing the moment somebody pulls its neighbour
        // across, and a heading that has been dragged to two pixels is a column the user cannot get
        // back without knowing the layout is remembered and where the file is.
        //
        // Null means unbounded, which is what every column did before these existed - the Grid's own
        // defaults are 0 and infinity. §54.3.
        /// <summary>The narrowest this column may be dragged, in pixels, or null for no limit.</summary>
        public double? MinWidth { get; init; }

        /// <summary>The widest this column may be dragged, in pixels, or null for no limit.</summary>
        public double? MaxWidth { get; init; }

        // HIDDEN, NOT REMOVED, and the difference is the whole reason this is a property rather than
        // the caller just not declaring the column. A hidden column keeps its INDEX - so a remembered
        // layout still matches (§27.11 refuses one whose column count differs), a sort on a hidden
        // column survives being hidden, and LunaTable.Edit(item, 2) means the same thing whether or
        // not column 1 is on screen. Rebuilding the table without the column would move all three.
        /// <summary>Whether this column is shown. Hidden columns keep their index, so a remembered layout and a sort still match.</summary>
        public bool IsVisible { get; init; } = true;
    }
}
