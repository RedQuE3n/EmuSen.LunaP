namespace EmuSen.LunaP.Controls
{
    // WHAT A CELL IS MADE OF - see docs/LunaP.md §57.
    //
    // Until §57 a cell was a TextBlock and there was nothing to choose, so there was no enum: the
    // column had a Text projection and that was the whole of what a cell could be. A checkbox column
    // is the case that breaks it - a boolean rendered as "True"/"False" is a value the user has to
    // read instead of see, and it is the commonest column in an office table after a name.
    //
    // AN ENUM AND NOT A SUBCLASS HIERARCHY. LunaColumn<T> is sealed and stays sealed: a caller writes
    // a column declaratively and the table decides how to draw it, which is the same split as Text
    // being a projection rather than an interface the model implements (§27). Three kinds is also the
    // whole list - anything a fourth would want is a Template, which is what Template is for.
    //
    // NOT SET DIRECTLY. Every kind but Text needs delegates that only make sense together, so the
    // kind is chosen by the factory that also takes them: LunaColumn<T>.Check and
    // LunaColumn<T>.Template. A check column without a Checked projection is not a thing that should
    // be possible to declare and then fail at draw time.
    /// <summary>What one column's cells are made of.</summary>
    public enum LunaCellKind
    {
        /// <summary>Text from the column's projection, editable when the column has a Commit.</summary>
        Text,

        /// <summary>A checkbox, toggleable when the column has a Toggle and read-only otherwise.</summary>
        Check,

        /// <summary>Whatever control the column's Build returns.</summary>
        Template,
    }
}
