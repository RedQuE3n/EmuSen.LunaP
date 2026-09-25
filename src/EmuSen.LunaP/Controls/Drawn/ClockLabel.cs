using System;
using System.Globalization;
using Avalonia;
using Avalonia.Threading;

namespace EmuSen.LunaP.Controls
{
    // FontText whose text is a time in a .NET format, either a fixed moment or the clock's own - see docs/LunaP.md §101.4.
    /// <summary>A FontText showing a time in a .NET date and time format: a fixed Time, or, while Live, the current time updated each second.</summary>
    public class ClockLabel : FontText
    {
        public static readonly StyledProperty<DateTime?> TimeProperty = AvaloniaProperty.Register<ClockLabel, DateTime?>(nameof(Time));
        public static readonly StyledProperty<string> FormatProperty = AvaloniaProperty.Register<ClockLabel, string>(nameof(Format), "HH:mm");
        public static readonly StyledProperty<bool> LiveProperty = AvaloniaProperty.Register<ClockLabel, bool>(nameof(Live));

        private DispatcherTimer? _timer;

        public ClockLabel()
        {
            Wrap = false;
            Refresh();
        }

        /// <summary>The moment shown; null shows the current time when the text is refreshed.</summary>
        public DateTime? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

        /// <summary>A .NET custom or standard date and time format, "HH:mm" by default.</summary>
        public string Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

        /// <summary>Whether the text follows the clock once a second while the control is shown. False by default.</summary>
        public bool Live { get => GetValue(LiveProperty); set => SetValue(LiveProperty, value); }

        /// <summary>The text for a moment in the current format; an unreadable format shows the moment in the invariant sortable form.</summary>
        /// <param name="moment">The time to format.</param>
        /// <returns>The formatted text.</returns>
        public string Formatted(DateTime moment)
        {
            try { return moment.ToString(Format, CultureInfo.CurrentCulture); }
            catch (FormatException) { return moment.ToString("s", CultureInfo.InvariantCulture); }
        }

        private void Refresh() => Text = Formatted(Time ?? DateTime.Now);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TimeProperty || change.Property == FormatProperty) Refresh();
            if (change.Property == LiveProperty) Tick();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Tick();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _timer?.Stop();
            _timer = null;
        }

        private void Tick()
        {
            _timer?.Stop();
            _timer = null;
            if (!Live || VisualRoot is null) return;
            _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh());
            _timer.Start();
        }
    }
}
