using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace EmuSen.LunaP.Windowing
{
    // The OS file/folder pickers and the two small modals, once, instead of per call site - see docs/LunaP.md §6 and §8.4.
    /// <summary>The platform file and folder pickers, and the two small modal dialogs, in one place.</summary>
    public static class Dialogs
    {
        // False for cancel, for Escape, and for closing the window - anything that is not a deliberate yes.
        /// <summary>Asks a yes/no question in a modal dialog.</summary>
        /// <param name="owner">The window to sit over. The dialog is modal to it.</param>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">The question.</param>
        /// <param name="acceptText">The caption of the accepting button.</param>
        /// <param name="cancelText">The caption of the cancelling button.</param>
        /// <returns>True if the accepting button was pressed. Closing the dialog any other way answers false.</returns>
        public static async Task<bool> ConfirmAsync(Window owner, string title, string message,
            string acceptText = "OK", string cancelText = "Cancel") =>
            await MessageWindow.Confirm(title, message, acceptText, cancelText).ShowDialog<bool>(owner);

        /// <summary>Shows a modal message with a single dismiss button.</summary>
        /// <param name="owner">The window to sit over.</param>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">What went wrong, written for whoever is looking at the screen.</param>
        /// <returns>A task that completes when the dialog is dismissed.</returns>
        public static async Task ErrorAsync(Window owner, string title, string message) =>
            await MessageWindow.Notice(title, message, "Close").ShowDialog<bool>(owner);

        // Null means the user cancelled, or the control is not in a window yet.
        /// <summary>Asks the platform for a folder.</summary>
        /// <param name="owner">Any visual in the window the picker should belong to.</param>
        /// <param name="title">The picker title.</param>
        /// <param name="startIn">The folder to open at. Ignored if it does not exist.</param>
        /// <returns>The chosen path, or null if the user cancelled.</returns>
        public static async Task<string?> PickFolderAsync(Visual owner, string title, string? startIn = null)
        {
            if (TopLevel.GetTopLevel(owner) is not { } top) return null;

            IReadOnlyList<IStorageFolder> picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                SuggestedStartLocation = await StartLocation(top, startIn),
            });

            return picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
        }

        // The picked file's name comes back too - callers that show it want the leaf, not the whole path.
        /// <summary>Asks the platform for an existing file.</summary>
        /// <param name="owner">Any visual in the window the picker should belong to.</param>
        /// <param name="title">The picker title.</param>
        /// <param name="types">The file types to offer. Null offers everything.</param>
        /// <param name="startIn">The folder to open at. Ignored if it does not exist.</param>
        /// <returns>The full path and the display name, or null if the user cancelled. The name is given separately because a platform may hand back a path that is not one a user would recognise.</returns>
        public static async Task<(string Path, string Name)?> PickFileAsync(Visual owner, string title,
            IReadOnlyList<FilePickerFileType>? types = null, string? startIn = null)
        {
            if (TopLevel.GetTopLevel(owner) is not { } top) return null;

            IReadOnlyList<IStorageFile> picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = types,
                SuggestedStartLocation = await StartLocation(top, startIn),
            });

            if (picked.Count == 0 || picked[0].TryGetLocalPath() is not { } path) return null;

            return (path, picked[0].Name);
        }

        /// <summary>Asks the platform where to write a file.</summary>
        /// <param name="owner">Any visual in the window the picker should belong to.</param>
        /// <param name="title">The picker title.</param>
        /// <param name="suggestedName">The name to offer.</param>
        /// <param name="types">The file types to offer. Null offers everything.</param>
        /// <param name="startIn">The folder to open at.</param>
        /// <param name="defaultExtension">Appended when the user types a name without one.</param>
        /// <returns>The chosen path, or null if the user cancelled. Nothing is written: choosing a path is all this does.</returns>
        public static async Task<string?> SaveFileAsync(Visual owner, string title, string? suggestedName = null,
            IReadOnlyList<FilePickerFileType>? types = null, string? startIn = null, string? defaultExtension = null)
        {
            if (TopLevel.GetTopLevel(owner) is not { } top) return null;

            IStorageFile? picked = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = defaultExtension,
                FileTypeChoices = types,
                SuggestedStartLocation = await StartLocation(top, startIn),
            });

            return picked?.TryGetLocalPath();
        }

        // A path that no longer exists is not an error here; the picker just opens wherever it would have anyway.
        private static async Task<IStorageFolder?> StartLocation(TopLevel top, string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                return await top.StorageProvider.TryGetFolderFromPathAsync(new Uri(path));
            }
            catch (UriFormatException)
            {
                return null;
            }
        }
    }
}
