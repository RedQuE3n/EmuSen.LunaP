using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // Dialogs.PromptAsync: one line of text, never empty, null for anything but a deliberate answer - see docs/LunaP.md §89.
    public class PromptTests
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PromptTests).GetTypeInfo().Assembly);

        private static (ToolWindow Owner, Task<string?> Answer, Window Prompt) Ask(string initial = "")
        {
            var owner = new ToolWindow { Width = 400, Height = 300 };
            owner.Show();
            Task<string?> answer = Dialogs.PromptAsync(owner, "New Collection", "Name the collection", initial, "Create");
            Dispatcher.UIThread.RunJobs();
            Window prompt = owner.OwnedWindows.Single();
            return (owner, answer, prompt);
        }

        private static T Part<T>(Window window, string name) where T : Control =>
            window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        [Fact]
        public Task The_text_typed_comes_back_trimmed() => Session.Dispatch(() =>
        {
            var (owner, answer, prompt) = Ask();
            Part<TextBox>(prompt, "PART_Answer").Text = "  Platformers ";
            Click(Part<Button>(prompt, "PART_Accept"));
            Dispatcher.UIThread.RunJobs();

            Assert.True(answer.IsCompleted);
            Assert.Equal("Platformers", answer.Result);
            owner.Close();
        }, default);

        // The accept button waits for text, so a blank name cannot be the answer at all.
        [Fact]
        public Task Blank_text_cannot_be_accepted() => Session.Dispatch(() =>
        {
            var (owner, answer, prompt) = Ask();
            Button accept = Part<Button>(prompt, "PART_Accept");
            Assert.False(accept.IsEnabled);

            Part<TextBox>(prompt, "PART_Answer").Text = "   ";
            Assert.False(accept.IsEnabled);

            Part<TextBox>(prompt, "PART_Answer").Text = "RPGs";
            Assert.True(accept.IsEnabled);
            Assert.False(answer.IsCompleted);
            owner.Close();
        }, default);

        [Fact]
        public Task Cancel_and_Escape_answer_null() => Session.Dispatch(() =>
        {
            var (owner, answer, prompt) = Ask("Old name");
            Click(Part<Button>(prompt, "PART_Cancel"));
            Dispatcher.UIThread.RunJobs();
            Assert.Null(answer.Result);

            var (_, escaped, second) = (owner, Dialogs.PromptAsync(owner, "Rename", "Name", "Old name"), (Window?)null);
            Dispatcher.UIThread.RunJobs();
            second = owner.OwnedWindows.Single();
            second.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Dispatcher.UIThread.RunJobs();
            Assert.Null(escaped.Result);
            owner.Close();
        }, default);

        [Fact]
        public Task The_initial_text_is_there_and_names_the_box_for_a_reader() => Session.Dispatch(() =>
        {
            var (owner, _, prompt) = Ask("Old name");
            TextBox box = Part<TextBox>(prompt, "PART_Answer");

            Assert.Equal("Old name", box.Text);
            Assert.Equal("Name the collection", Avalonia.Automation.AutomationProperties.GetName(box));
            Assert.Equal("New Collection", prompt.Title);
            owner.Close();
        }, default);
    }
}
