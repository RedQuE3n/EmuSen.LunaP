using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace EmuSen.LunaP.Windowing
{
    // Files dropped onto a control, as paths - see docs/LunaP.md §77.
    //
    // WHAT THIS REMOVES IS FOUR LINES AND TWO SILENT FAILURES, which is the only reason it exists:
    // Avalonia already extracts the files, and a helper over something the platform does well would
    // be worse than nothing (§77.1 refuses five other things on exactly that ground).
    //
    // The two failures are both "nothing happens, and nothing says why":
    //
    //   1. DragDrop.SetAllowDrop was never set, so no drag event is ever raised at all.
    //   2. DragOver never set DragEffects, so the platform refuses the drop before Drop is reached.
    //
    // Neither produces an error, a warning, or a mark on screen. Both are the whole feature not
    // working, and the second one is worse because the code that matters - the Drop handler - is
    // present, correct, and never called.
    //
    //     _drop = new FileDrop(this, paths => Load(paths[0]));
    //     _drop.Accept = paths => paths.Count == 1;
    //
    // PATHS RATHER THAN IStorageItem, to match Dialogs, which has returned string? since §6. A
    // consumer of this toolkit opens files by path, and handing back a storage item so that every
    // caller can write the same TryGetLocalPath line is a seam that pays nobody.
    //
    // DISPOSE IT. It restores whatever AllowDrop was before it, so a control that already accepted
    // drops for its own reasons still does.
    /// <summary>Accepts files dropped onto a control and hands their paths to a callback.</summary>
    public sealed class FileDrop : IDisposable
    {
        private readonly InputElement _target;
        private readonly Action<IReadOnlyList<string>> _dropped;
        private readonly bool _allowedDropBefore;
        private bool _disposed;

        /// <summary>Starts accepting dropped files on a control.</summary>
        /// <param name="target">The control to accept drops on. A Window covers everything in it.</param>
        /// <param name="dropped">Runs on the UI thread with the local paths of the dropped files, never empty.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="target"/> or <paramref name="dropped"/> is null.</exception>
        public FileDrop(InputElement target, Action<IReadOnlyList<string>> dropped)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _dropped = dropped ?? throw new ArgumentNullException(nameof(dropped));

            _allowedDropBefore = DragDrop.GetAllowDrop(_target);
            DragDrop.SetAllowDrop(_target, true);

            _target.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            _target.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        // Optional, because most callers take anything. Consulted on the way over as well as on the
        // drop, so a refusal shows as the "no entry" pointer BEFORE the user lets go rather than as
        // nothing happening after they do.
        /// <summary>An optional filter on the paths. Return false to refuse the drop, which the pointer shows while the drag is still over the control. Null accepts everything.</summary>
        public Func<IReadOnlyList<string>, bool>? Accept { get; set; }

        /// <summary>Stops accepting drops and puts the control's previous AllowDrop back.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _target.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            _target.RemoveHandler(DragDrop.DropEvent, OnDrop);
            DragDrop.SetAllowDrop(_target, _allowedDropBefore);
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            bool take = Wanted(e, out _);
            e.DragEffects = take ? DragDropEffects.Copy : DragDropEffects.None;

            // HANDLED ONLY WHEN TAKING IT. A refusal is left unhandled on purpose, so a FileDrop on
            // an ancestor still gets to answer for itself - the inner control declining a file is
            // not the same as the window declining it.
            if (take) e.Handled = true;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            if (!Wanted(e, out IReadOnlyList<string> paths)) return;

            e.Handled = true;
            _dropped(paths);
        }

        private bool Wanted(DragEventArgs e, out IReadOnlyList<string> paths)
        {
            paths = Paths(e.DataTransfer);
            if (paths.Count == 0) return false;

            return Accept?.Invoke(paths) ?? true;
        }

        // A FILE WITH NO LOCAL PATH IS NOT OFFERED, and a drop of nothing but those is refused rather
        // than delivered empty. A drag can carry an item that does not exist on this disk - out of a
        // remote share on some platforms, or a virtual file out of an archive viewer - and there is
        // no path to give the caller for one. Refusing is the honest answer; handing over a shorter
        // list than the user dropped, with no way to know, is not.
        private static IReadOnlyList<string> Paths(IDataTransfer? data)
        {
            if (data is null) return Array.Empty<string>();

            IStorageItem[]? files = data.TryGetFiles();
            if (files is null || files.Length == 0) return Array.Empty<string>();

            var paths = new List<string>(files.Length);
            foreach (IStorageItem file in files)
            {
                if (file.TryGetLocalPath() is { Length: > 0 } local) paths.Add(local);
            }

            return paths;
        }
    }
}
