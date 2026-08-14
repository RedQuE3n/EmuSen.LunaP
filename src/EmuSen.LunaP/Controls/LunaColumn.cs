using System;
using Avalonia.Controls;

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
            : this(LunaCellKind.Text, header, text)
        {
        }

        private LunaColumn(LunaCellKind kind, string header, Func<T, string> text)
        {
            Kind = kind;
            Header = header ?? throw new ArgumentNullException(nameof(header));
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>The column heading.</summary>
        public string Header { get; }

        // TEXT STOPPED BEING HOW THE CELL IS DRAWN AND BECAME WHAT THE CELL SAYS, which is the one
        // idea that made §57 fit without a second kind of column. For a text cell the two are the
        // same thing. For a checkbox they are not: the cell shows a tick and this returns "yes", and
        // that string is what the row's spoken sentence and the automation value are built from.
        //
        // Which is why the other two kinds still take one, and why the template form REQUIRES one
        // rather than defaulting to the empty string. A template column is the one place a caller can
        // put something on screen that this toolkit cannot describe - an icon, a spark line, a button
        // - and a row sentence reading "status: " for it is §24 happening again on purpose.
        /// <summary>What this column's cell says, which is also its text for a Text column.</summary>
        public Func<T, string> Text { get; }

        // The kind is chosen by the constructor that also takes the delegates it needs, and is never
        // set: every kind but Text needs delegates that only mean anything together, and an
        // init-only Kind would let a caller declare a Check column with no Checked projection and
        // find out about it when a row was drawn.
        /// <summary>What this column's cells are made of. Text unless a kind-specific constructor was used.</summary>
        public LunaCellKind Kind { get; }

        // Read and written separately rather than as one two-way accessor, because the write is
        // OPTIONAL and its absence is what "read-only" means - the same convention as Commit for a
        // text column, so there is one idea of "editable" rather than one per kind.
        /// <summary>Reads a row's boolean for a Check column, and null for every other kind.</summary>
        public Func<T, bool>? Checked { get; private init; }

        // A REFUSAL IS EXPRESSED BY NOT WRITING, and that falls out of the table re-reading Checked
        // after every toggle rather than trusting the box. A Toggle that declines - "at least one
        // output has to stay on" - simply leaves the model alone and the tick goes back where it was.
        // What it cannot do is say WHY, which is the one thing a text column's Validate offers and
        // this does not; see docs/LunaP.md §57.4.
        /// <summary>Writes a ticked box back to the model for a Check column, or null when the column is read-only.</summary>
        public Action<T, bool>? Toggle { get; private init; }

        /// <summary>Builds the cell for a Template column, and null for every other kind.</summary>
        public Func<T, Control>? Build { get; private init; }

        // A CONSTRUCTOR PER KIND, AND NOT A STATIC FACTORY, which was the first attempt and lasted
        // exactly as long as it took to want a width. `LunaColumn<T>.Check("req", ...)` hands back a
        // finished object, and every other thing a column can be told - Width, Sort, MinWidth,
        // IsVisible - is an init-only property that can only be set at construction. A factory can
        // reach none of them, so the width would have had to become a parameter, and then the sort,
        // and the growth path this class exists to have would be gone by the second one.
        //
        // A constructor keeps the object initializer, so all three kinds are declared the same way
        // and every property already here applies to every one of them:
        //
        //     new LunaColumn<Row>("req", r => r.Required, (r, on) => r.Required = on) { Width = "40" }
        //
        // READ-ONLY UNLESS A WRITE IS GIVEN, which is the same default as every other column in this
        // control: a table is a readout until a caller says otherwise (§26.13).
        //
        // `spoken` is what the row sentence says for this column, and "yes"/"no" is the default
        // rather than "True"/"False" because the sentence reads "enabled: yes" and not "enabled:
        // True". A caller wanting other words - "on"/"off", "armed"/"safe" - passes them.
        /// <summary>Creates a column of checkboxes.</summary>
        /// <param name="header">The column heading.</param>
        /// <param name="read">Reads the row's boolean. Called for every row on every rebuild, so it should be cheap.</param>
        /// <param name="write">Writes a ticked box back to the model, or null - the default - for a read-only column.</param>
        /// <param name="spoken">What this column contributes to the row's spoken sentence. "yes" or "no" by default.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="header"/> or <paramref name="read"/> is null.</exception>
        public LunaColumn(
            string header,
            Func<T, bool> read,
            Action<T, bool>? write = null,
            Func<T, string>? spoken = null)
            : this(LunaCellKind.Check, header, Says(read, spoken))
        {
            Checked = read;
            Toggle = write;
        }

        // Runs before the base constructor, which is the only reason it exists: `read` has to be
        // checked before it is closed over, or a null one would surface as an ArgumentNullException
        // naming "text" - the private constructor's parameter - and send the caller looking at the
        // wrong argument.
        private static Func<T, string> Says(Func<T, bool> read, Func<T, string>? spoken)
        {
            if (read is null) throw new ArgumentNullException(nameof(read));

            return spoken ?? (item => read(item) ? "yes" : "no");
        }

        // THE ESCAPE HATCH, AND THE ONLY ONE THIS CONTROL NEEDS. Everything a column could grow -
        // a progress bar, an icon, a pair of buttons - is a control somebody can build, and a
        // toolkit that added a kind per idea would be maintaining a gallery inside a table.
        //
        // `spoken` IS REQUIRED HERE AND OPTIONAL ON THE CHECK FORM, and that asymmetry is the point:
        // a checkbox's sentence can be derived from the boolean, and an arbitrary control's cannot.
        // §24 is the section about nine controls a screen reader never reached; requiring the
        // sentence is what makes the unreachable version of this column impossible to declare.
        /// <summary>Creates a column whose cells are built by the caller.</summary>
        /// <param name="header">The column heading.</param>
        /// <param name="build">Builds the cell for a row. Called for every realised row, so it should be cheap; a null return leaves the cell empty.</param>
        /// <param name="spoken">What this column contributes to the row's spoken sentence. Required, because nothing else can describe an arbitrary control.</param>
        /// <exception cref="System.ArgumentNullException">Any argument is null.</exception>
        public LunaColumn(string header, Func<T, Control> build, Func<T, string> spoken)
            : this(LunaCellKind.Template, header, spoken) =>
            Build = build ?? throw new ArgumentNullException(nameof(build));

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
        //
        // NAMES THE KIND SINCE §57, AND THAT CLAUSE IS REACHABLE. Commit is an init-only property
        // like every other, so nothing stops `new LunaColumn<T>("armed", r => r.On) { Commit = ... }`
        // - a check column carrying a text writer that can never be used. Without the kind test that
        // column answers "editable", and F2 - which walks for the FIRST editable column - stops at a
        // cell with no text editor and never reaches the text column behind it.
        //
        // Ignored rather than rejected, because throwing would make a meaningless-but-harmless
        // declaration a crash at startup, and a checkbox is changed by being ticked: Toggle is where
        // that lives.
        /// <summary>Whether a text editor can be opened on this column, which is a Text column with a Commit.</summary>
        public bool IsEditable => Kind == LunaCellKind.Text && Commit is not null;

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
