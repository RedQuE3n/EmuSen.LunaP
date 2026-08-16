using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EmuSen.LunaP.Windowing;

namespace EmuSen.LunaP.Tests
{
    // FileDrop - see docs/LunaP.md §77.2.
    //
    // A REAL DRAG IS RAISED HERE, not a call to an internal method. DragEventArgs, DataTransfer and
    // DataTransferItem.CreateFile are all public and constructible in 12.1.0, so these go through
    // the same events the platform raises. That matters more than usual for this type, because both
    // defects it exists to prevent are in the WIRING rather than the logic: a test that called the
    // handler directly would pass with AllowDrop never set, which is defect one.
    //
    // THE FILES ARE REAL, and they have to be. IStorageItem is explicitly not implementable by user
    // code - the compiler says so in as many words - and Avalonia's only concrete implementations
    // are internal. What works is StorageProviderExtensions.TryGetFileFromPathAsync, which hands
    // back a real BclStorageFile even though the headless provider is a NoopStorageProvider with
    // CanOpen=false.
    public class FileDropTests : IDisposable
    {
        private static readonly HeadlessUnitTestSession Session =
            HeadlessUnitTestSession.GetOrStartForAssembly(typeof(FileDropTests).GetTypeInfo().Assembly);

        // AN async LAMBDA PASSED STRAIGHT TO Dispatch IS A TEST THAT CANNOT FAIL - see §77.5.
        //
        // Dispatch has a Dispatch<TResult>(Func<TResult>) overload, and `async () => { ... }` binds
        // to it with TResult inferred as Task. The result is Task<Task>: awaiting it awaits only the
        // OUTER task, which completes at the body's first await, so every assertion after that point
        // runs detached and its failure is swallowed. Two guards in this file were written that way
        // and stayed green under the sabotage that should have killed them.
        //
        // Unwrap() is the whole fix - it returns the inner task, so the body is actually awaited.
        // Every async body here goes through this, so the mistake cannot be made again by accident.
        private static Task Run(Func<Task> body) => Session.Dispatch(body, default).Unwrap();

        private static Task Run(Action body) => Session.Dispatch(body, default);

        private readonly string _dir;
        private readonly string _a;
        private readonly string _b;

        public FileDropTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "lunap-drop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _a = Path.Combine(_dir, "smw.sfc");
            _b = Path.Combine(_dir, "zelda.sfc");
            File.WriteAllText(_a, "a");
            File.WriteAllText(_b, "b");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        private static async Task<DataTransfer> Files(TopLevel top, params string[] paths)
        {
            var transfer = new DataTransfer();
            foreach (string path in paths)
            {
                IStorageFile? file = await top.StorageProvider.TryGetFileFromPathAsync(path);
                Assert.NotNull(file);
                transfer.Add(DataTransferItem.CreateFile(file!));
            }

            return transfer;
        }

        private static DragEventArgs Over(Interactive target, IDataTransfer data)
        {
            var e = new DragEventArgs(DragDrop.DragOverEvent, data, target, new Point(5, 5), KeyModifiers.None);
            target.RaiseEvent(e);
            return e;
        }

        private static void Drop(Interactive target, IDataTransfer data) =>
            target.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, target, new Point(5, 5), KeyModifiers.None));

        [Fact]
        public Task A_dropped_file_arrives_as_a_local_path() => Run(async () =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            IReadOnlyList<string>? got = null;
            using var drop = new FileDrop(window, paths => got = paths);

            Drop(window, await Files(window, _a));

            Assert.NotNull(got);
            Assert.Equal(new[] { _a }, got!);

            window.Close();
        });

        [Fact]
        public Task Several_files_arrive_together() => Run(async () =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            IReadOnlyList<string>? got = null;
            using var drop = new FileDrop(window, paths => got = paths);

            Drop(window, await Files(window, _a, _b));

            Assert.Equal(new[] { _a, _b }, got!);

            window.Close();
        });

        // DEFECT ONE. Without this the platform raises no drag event at all, and every handler is
        // correct and never called.
        [Fact]
        public Task The_target_is_told_to_accept_drops() => Run(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            Assert.False(DragDrop.GetAllowDrop(window));

            using var drop = new FileDrop(window, _ => { });
            Assert.True(DragDrop.GetAllowDrop(window), "Nothing set AllowDrop, so no drag event is ever raised.");

            window.Close();
        });

        // DEFECT TWO. Without an effect on the way over, the platform refuses the drop before Drop
        // is ever reached. The default is None, measured - so this assertion discriminates.
        [Fact]
        public Task Dragging_over_says_the_drop_will_be_taken() => Run(async () =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            using var drop = new FileDrop(window, _ => { });

            DragEventArgs over = Over(window, await Files(window, _a));

            Assert.Equal(DragDropEffects.Copy, over.DragEffects);
            Assert.True(over.Handled);

            window.Close();
        });

        [Fact]
        public Task A_drag_carrying_no_files_is_refused() => Run(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            bool called = false;
            using var drop = new FileDrop(window, _ => called = true);

            var text = new DataTransfer();
            text.Add(DataTransferItem.Create(DataFormat.Text, "not a file"));

            DragEventArgs over = Over(window, text);
            Assert.Equal(DragDropEffects.None, over.DragEffects);

            // Left unhandled on purpose, so a FileDrop on an ancestor still gets to answer.
            Assert.False(over.Handled);

            Drop(window, text);
            Assert.False(called);

            window.Close();
        });

        [Fact]
        public Task Accept_refuses_before_the_user_lets_go() => Run(async () =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            bool called = false;
            using var drop = new FileDrop(window, _ => called = true) { Accept = paths => paths.Count == 1 };

            DragEventArgs two = Over(window, await Files(window, _a, _b));
            Assert.Equal(DragDropEffects.None, two.DragEffects);

            Drop(window, await Files(window, _a, _b));
            Assert.False(called);

            DragEventArgs one = Over(window, await Files(window, _a));
            Assert.Equal(DragDropEffects.Copy, one.DragEffects);

            Drop(window, await Files(window, _a));
            Assert.True(called);

            window.Close();
        });

        [Fact]
        public Task Disposing_stops_accepting_and_restores_allow_drop() => Run(async () =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();

            bool called = false;
            var drop = new FileDrop(window, _ => called = true);
            drop.Dispose();

            Assert.False(DragDrop.GetAllowDrop(window));

            Drop(window, await Files(window, _a));
            Assert.False(called);

            window.Close();
        });

        // A control that already accepted drops for its own reasons keeps doing so afterwards.
        [Fact]
        public Task Disposing_leaves_a_target_that_already_allowed_drops_alone() => Run(() =>
        {
            var window = new ToolWindow { Width = 200, Height = 150 };
            window.Show();
            DragDrop.SetAllowDrop(window, true);

            var drop = new FileDrop(window, _ => { });
            drop.Dispose();

            Assert.True(DragDrop.GetAllowDrop(window), "Disposal clobbered an AllowDrop it did not set.");

            window.Close();
        });

        [Fact]
        public void A_target_and_a_callback_are_both_required()
        {
            Assert.Throws<ArgumentNullException>(() => new FileDrop(null!, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new FileDrop(new Border(), null!));
        }
    }
}
