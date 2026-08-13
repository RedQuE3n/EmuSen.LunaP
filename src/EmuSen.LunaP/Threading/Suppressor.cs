using System;

namespace EmuSen.LunaP.Threading
{
    // "I am writing to these controls; their change handlers should not write back."
    //
    // Six hand-written bools across two applications, all guarding the same thing and all with a
    // comment saying so - see docs/LunaP.md §21.1. The kit already solved it once, privately, for
    // one control: Dropdown.Fill sets a flag around an items-and-selection swap so Chose does not
    // fire for the reset. That solution was written about dropdowns and is not about dropdowns.
    //
    //     private readonly Suppressor _updating = new();
    //
    //     void Refresh()
    //     {
    //         using (_updating.Suppress())
    //         {
    //             list.ItemsSource = rows;
    //             list.SelectedItem = chosen;
    //         }
    //     }
    //
    //     void OnSelectionChanged()
    //     {
    //         if (_updating.IsSuppressing) return;
    //         ...
    //     }
    //
    // A COUNTER, NOT A BOOL, and that is the one thing this adds over what it replaces. Every
    // hand-rolled version assigns true and then false, so a nested update - a refresh that calls
    // a helper that refreshes something else - re-enables notifications halfway through the outer
    // one, at which point the guard is worse than absent because the code reads as if it is
    // protected. Depth counting makes nesting correct instead of subtly broken.
    //
    // NOT THREAD-SAFE, deliberately. This guards UI event handlers, which are a UI-thread
    // concern; making it interlocked would invite use as a general mutual-exclusion primitive,
    // which it is not and should not become.
    /// <summary>Marks a region in which a control's change handlers must not write back.</summary>
    public sealed class Suppressor
    {
        private int _depth;

        // True while at least one Suppress() scope is open.
        /// <summary>Whether any scope is currently open. False once every scope taken has been disposed.</summary>
        public bool IsSuppressing => _depth > 0;

        // Opens a scope. Dispose closes it - `using` is the intended shape, so an exception
        // thrown mid-update cannot leave notifications suppressed for the rest of the process.
        /// <summary>Opens a scope. Scopes nest: suppression ends when the outermost one is disposed, not the first.</summary>
        /// <returns>A handle to dispose when the scope ends. Disposing it twice is harmless.</returns>
        public IDisposable Suppress()
        {
            _depth++;
            return new Scope(this);
        }

        private sealed class Scope : IDisposable
        {
            private Suppressor? _owner;

            internal Scope(Suppressor owner) => _owner = owner;

            // Null after the first call, so a double dispose - which `using` inside a method that
            // also disposes by hand will produce - cannot drive the depth below zero and reopen
            // an outer scope that is still meant to be closed.
            public void Dispose()
            {
                if (_owner is null) return;

                _owner._depth--;
                _owner = null;
            }
        }
    }
}
