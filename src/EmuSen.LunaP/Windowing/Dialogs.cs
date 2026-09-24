using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace EmuSen.LunaP.Windowing
{
    // The OS file/folder pickers and the three small modals, once, instead of per call site - see docs/LunaP.md §6 and §8.4.
    /// <summary>The platform file and folder pickers, and the three small modal dialogs, in one place.</summary>
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
            await SheetLayer.ShowDialog<bool>(DialogWindow.Confirm(title, message, acceptText, cancelText), owner);

        /// <summary>Shows a modal message with a single dismiss button.</summary>
        /// <param name="owner">The window to sit over.</param>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">What went wrong, written for whoever is looking at the screen.</param>
        /// <returns>A task that completes when the dialog is dismissed.</returns>
        public static async Task ErrorAsync(Window owner, string title, string message) =>
            await SheetLayer.ShowDialog<bool>(DialogWindow.Notice(title, message, "Close"), owner);

        // THE THIRD OF THE THREE, and it was missing until §84.2 rather than refused.
        //
        // Confirm asks a question, Error reports a fault, and this states something that is neither:
        // "Choose a folder first", "Nothing to copy", "Six items were renamed". Without it a caller
        // reaches for ErrorAsync and dresses a normal state as a failure - which is not cosmetic,
        // because a user who is shown errors for ordinary conditions stops reading them.
        //
        // No mechanism is added: DialogWindow.Notice already takes a null cancel button and renders
        // exactly this, and ErrorAsync IS this call with an error's title. LunaPY has had `message`
        // beside `confirm` and `error` since it was written, so this is the C# half catching up to
        // its own sibling rather than a new idea.
        //
        // FOR A SENTENCE, NOT FOR OUTPUT. Anything past a few lines wants MessageWindow, which is
        // not modal and can be left open beside the thing it describes (§84.3).
        /// <summary>Shows a modal informational message with a single dismiss button, for something that is neither a question nor a fault.</summary>
        /// <param name="owner">The window to sit over.</param>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">What the user needs to know. A sentence or two; use MessageWindow for output long enough to scroll.</param>
        /// <param name="acceptText">The caption of the dismiss button.</param>
        /// <returns>A task that completes when the dialog is dismissed.</returns>
        public static async Task MessageAsync(Window owner, string title, string message, string acceptText = "OK") =>
            await SheetLayer.ShowDialog<bool>(DialogWindow.Notice(title, message, acceptText), owner);

        // THE FOURTH SMALL MODAL, for the one thing the other three cannot ask: a word. A name for a
        // new collection, a rename. Null for Cancel, Escape and closing, as Confirm is false for them;
        // never an empty string, because the accept button waits for text (§89).
        /// <summary>Asks for one line of text in a modal dialog, such as a name.</summary>
        /// <param name="owner">The window to sit over. The dialog is modal to it.</param>
        /// <param name="title">The dialog title.</param>
        /// <param name="message">What is being asked for. Also the text box's accessible name.</param>
        /// <param name="initial">The text the box starts with, selected so that typing replaces it.</param>
        /// <param name="acceptText">The caption of the accepting button, which is disabled while the text is blank.</param>
        /// <param name="cancelText">The caption of the cancelling button.</param>
        /// <returns>The text, trimmed and never empty, or null if the dialog was dismissed any other way.</returns>
        public static async Task<string?> PromptAsync(Window owner, string title, string message, string initial = "",
            string acceptText = "OK", string cancelText = "Cancel") =>
            await SheetLayer.ShowDialog<string>(new PromptWindow(title, message, initial, acceptText, cancelText), owner);

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
