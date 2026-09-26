using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    // A control that takes Left and Right as its own, as a Slider or a closed ComboBox does. A host that
    // steers a window by directions alone (a gamepad's d-pad, a remote) moves the focus on every arrow, and
    // only knows the stock controls that must be sent the key instead; this marker is how a toolkit control
    // says it is one of them without the host naming its type - see docs/LunaP.md §160.1.
    /// <summary>Marks a control whose value the Left and Right keys change, so a host steering by directions sends it those keys rather than moving the focus sideways.</summary>
    public interface ISidewaysAdjustable
    {
    }

    // A StarRating a person can set: Left and Right step it, a click sets it at the pointer, and the stars
    // are drawn as §101.1 draws them, from the palette. The value keeps StarRating's 0-to-1 scale, so a host
    // that shows a rating with one and edits it with the other passes the same number. Steps are fractions
    // of a star (half stars by default, as a frontend's metadata editor offers them) - see docs/LunaP.md §160.2.
    /// <summary>An editable row of stars: Left and Right step the value by a fraction of a star, Home and End go to none and all, and a click sets it where the pointer is.</summary>
    public class RatingPicker : Control, ISidewaysAdjustable
    {
        public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<RatingPicker, double>(nameof(Value));
        public static readonly StyledProperty<int> StarCountProperty = AvaloniaProperty.Register<RatingPicker, int>(nameof(StarCount), 5);
        public static readonly StyledProperty<int> StepsPerStarProperty = AvaloniaProperty.Register<RatingPicker, int>(nameof(StepsPerStar), 2);
        public static readonly StyledProperty<double> StarSizeProperty = AvaloniaProperty.Register<RatingPicker, double>(nameof(StarSize), 28);

        static RatingPicker()
        {
            FocusableProperty.OverrideDefaultValue<RatingPicker>(true);
            AffectsMeasure<RatingPicker>(StarCountProperty, StarSizeProperty);
            AffectsRender<RatingPicker>(ValueProperty, StepsPerStarProperty);
        }

        /// <summary>The rating from 0 to 1, as StarRating takes it; a value set from code is kept as given, and a person's change lands on a step.</summary>
        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        /// <summary>How many stars the row holds. Five unless set.</summary>
        public int StarCount { get => GetValue(StarCountProperty); set => SetValue(StarCountProperty, value); }

        /// <summary>How many positions each star has: 2 gives half stars, 1 whole stars. Two unless set.</summary>
        public int StepsPerStar { get => GetValue(StepsPerStarProperty); set => SetValue(StepsPerStarProperty, value); }

        /// <summary>The height and width of one star in pixels. 28 unless set.</summary>
        public double StarSize { get => GetValue(StarSizeProperty); set => SetValue(StarSizeProperty, value); }

        /// <summary>Raised when a person changes the value with a key or the pointer; never when code sets it.</summary>
        public event Action<double>? Chose;

        private int Positions => Math.Max(1, StarCount) * Math.Max(1, StepsPerStar);

        /// <summary>The value one step from the present one, snapped to the steps first, and kept within 0 and 1.</summary>
        /// <param name="by">How many steps, negative for fewer stars.</param>
        /// <returns>The stepped value.</returns>
        public double Stepped(int by)
        {
            double at = Math.Round(Math.Clamp(Value, 0, 1) * Positions);
            return Math.Clamp(at + by, 0, Positions) / Positions;
        }

        private void Choose(double value)
        {
            value = Math.Clamp(value, 0, 1);
            if (value == Value) return;
            SetCurrentValue(ValueProperty, value);
            Chose?.Invoke(value);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;
            switch (e.Key)
            {
                case Key.Left: Choose(Stepped(-1)); break;
                case Key.Right: Choose(Stepped(1)); break;
                case Key.Home: Choose(0); break;
                case Key.End: Choose(1); break;
                default: return;
            }
            e.Handled = true;
        }

        // A click lands on the step whose right edge is nearest past the pointer, so clicking a star's right half gives the whole star.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Bounds.Width <= 0) return;
            Focus(NavigationMethod.Pointer);
            double x = e.GetPosition(this).X;
            Choose(Math.Ceiling(Math.Clamp(x / Bounds.Width, 0, 1) * Positions) / Positions);
            e.Handled = true;
        }

        protected override Size MeasureOverride(Size availableSize) => new(StarSize * Math.Max(1, StarCount), StarSize);

        public override void Render(DrawingContext context)
        {
            int n = Math.Max(1, StarCount);
            double w = Bounds.Width / n, h = Bounds.Height;
            double cut = Math.Clamp(Value, 0, 1) * Bounds.Width;
            var unfilled = new ImmutableSolidColorBrush(LunaPalette.Muted.Color);
            var filled = new ImmutableSolidColorBrush(LunaPalette.Warning.Color);
            using (context.PushClip(new Rect(cut, 0, Math.Max(0, Bounds.Width - cut), h)))
                for (int i = 0; i < n; i++) context.DrawGeometry(unfilled, null, StarRating.StarGeometry(new Rect(i * w, 0, w, h)));
            using (context.PushClip(new Rect(0, 0, cut, h)))
                for (int i = 0; i < n; i++) context.DrawGeometry(filled, null, StarRating.StarGeometry(new Rect(i * w, 0, w, h)));
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Slider, () => $"{Math.Round(Math.Clamp(Value, 0, 1) * Math.Max(1, StarCount), 1).ToString(CultureInfo.InvariantCulture)} of {Math.Max(1, StarCount)}");
    }

    /// <summary>The part of a date a DateStepper's Left and Right change.</summary>
    public enum DateSegment
    {
        /// <summary>The year, four digits.</summary>
        Year,

        /// <summary>The month, 1 to 12.</summary>
        Month,

        /// <summary>The day of the month.</summary>
        Day,
    }

    // A date entered with arrows alone: Left and Right change one part, Enter moves to the next part, and the
    // day is kept inside the month as the month and year change. A calendar popup needs a pointer or many
    // presses to reach a year thirty years back, and a text box needs a keyboard; this needs neither. No
    // date is a value of its own (null), reached by stepping the year below MinimumYear, and the first step
    // from none starts at StartDate - see docs/LunaP.md §160.3.
    /// <summary>A date entered with the arrow keys: Left and Right change the year, month or day, Enter moves to the next part, and null means no date.</summary>
    public class DateStepper : Control, ISidewaysAdjustable
    {
        public static readonly StyledProperty<DateTime?> ValueProperty = AvaloniaProperty.Register<DateStepper, DateTime?>(nameof(Value));
        public static readonly StyledProperty<DateSegment> SegmentProperty = AvaloniaProperty.Register<DateStepper, DateSegment>(nameof(Segment));
        public static readonly StyledProperty<int> MinimumYearProperty = AvaloniaProperty.Register<DateStepper, int>(nameof(MinimumYear), 1950);
        public static readonly StyledProperty<int> MaximumYearProperty = AvaloniaProperty.Register<DateStepper, int>(nameof(MaximumYear), 2099);
        public static readonly StyledProperty<DateTime> StartDateProperty = AvaloniaProperty.Register<DateStepper, DateTime>(nameof(StartDate), new DateTime(1990, 1, 1));
        public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<DateStepper>();
        public static readonly StyledProperty<string> NoDateTextProperty = AvaloniaProperty.Register<DateStepper, string>(nameof(NoDateText), "No date");

        static DateStepper()
        {
            FocusableProperty.OverrideDefaultValue<DateStepper>(true);
            AffectsMeasure<DateStepper>(FontSizeProperty, ValueProperty, NoDateTextProperty);
            AffectsRender<DateStepper>(ValueProperty, SegmentProperty);
        }

        /// <summary>The date, its time of day ignored; null when there is none.</summary>
        public DateTime? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        /// <summary>The part Left and Right change now; Enter moves it on, from the day back to the year. Year unless set.</summary>
        public DateSegment Segment { get => GetValue(SegmentProperty); set => SetValue(SegmentProperty, value); }

        /// <summary>The earliest year; stepping the year below it leaves no date. 1950 unless set.</summary>
        public int MinimumYear { get => GetValue(MinimumYearProperty); set => SetValue(MinimumYearProperty, value); }

        /// <summary>The latest year the year can be stepped to. 2099 unless set.</summary>
        public int MaximumYear { get => GetValue(MaximumYearProperty); set => SetValue(MaximumYearProperty, value); }

        /// <summary>The date the first step from no date gives. 1 January 1990 unless set.</summary>
        public DateTime StartDate { get => GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }

        /// <summary>The text's size in pixels, inherited like any text's.</summary>
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

        /// <summary>What is shown when there is no date. "No date" unless set.</summary>
        public string NoDateText { get => GetValue(NoDateTextProperty); set => SetValue(NoDateTextProperty, value); }

        /// <summary>Raised when a person changes the date with a key or the pointer; never when code sets it.</summary>
        public event Action<DateTime?>? Chose;

        /// <summary>The date one step along a part from the present one: the day kept within its month, the year within its bounds, and no date below the first year.</summary>
        /// <param name="segment">The part to step.</param>
        /// <param name="by">How many years, months or days; negative goes back.</param>
        /// <returns>The stepped date, or null for none.</returns>
        public DateTime? Stepped(DateSegment segment, int by)
        {
            if (Value is not { } now) return by == 0 ? null : StartDate.Date;
            int year = now.Year, month = now.Month, day = now.Day;
            switch (segment)
            {
                case DateSegment.Year:
                    year += by;
                    if (year < MinimumYear) return null;
                    year = Math.Min(year, Math.Max(MinimumYear, MaximumYear));
                    break;
                case DateSegment.Month:
                    month = ((month - 1 + by) % 12 + 12) % 12 + 1;
                    break;
                default:
                    day = ((day - 1 + by) % DateTime.DaysInMonth(year, month) + DateTime.DaysInMonth(year, month)) % DateTime.DaysInMonth(year, month) + 1;
                    break;
            }
            return new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
        }

        private void Choose(DateTime? value)
        {
            value = value?.Date;
            if (value == Value) return;
            SetCurrentValue(ValueProperty, value);
            Chose?.Invoke(value);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;
            switch (e.Key)
            {
                case Key.Left: Choose(Stepped(Segment, -1)); break;
                case Key.Right: Choose(Stepped(Segment, 1)); break;
                case Key.Enter: SetCurrentValue(SegmentProperty, (DateSegment)(((int)Segment + 1) % 3)); break;
                case Key.Delete or Key.Back: Choose(null); break;
                default: return;
            }
            e.Handled = true;
        }

        // A click chooses the part under the pointer, the year's four digits taking the left four tenths.
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Bounds.Width <= 0) return;
            Focus(NavigationMethod.Pointer);
            double x = e.GetPosition(this).X / Bounds.Width;
            SetCurrentValue(SegmentProperty, x < 0.4 ? DateSegment.Year : x < 0.7 ? DateSegment.Month : DateSegment.Day);
            e.Handled = true;
        }

        // The text as drawn: the ISO 8601 form, as a frontend's metadata editor writes release dates.
        /// <summary>The date as the control shows it: yyyy-MM-dd, or NoDateText when there is none.</summary>
        public string Text => Value is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : NoDateText;

        private FormattedText Layout(string text, IBrush brush) =>
            new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(TextElement.GetFontFamily(this)), FontSize, brush);

        protected override Size MeasureOverride(Size availableSize)
        {
            FormattedText widest = Layout("0000-00-00", LunaPalette.Text);
            FormattedText none = Layout(NoDateText, LunaPalette.Text);
            return new Size(Math.Max(widest.Width, none.Width) + 16, widest.Height + 8);
        }

        public override void Render(DrawingContext context)
        {
            // Drawn as a field, on the input surface inside a border, so it reads as something to set and not as a label.
            context.DrawRectangle(LunaPalette.InputSurface, new Pen(LunaPalette.Border, 1), new Rect(Bounds.Size).Deflate(0.5), 4, 4);
            FormattedText text = Layout(Text, Value is null ? LunaPalette.Muted : LunaPalette.Text);
            var origin = new Point(8, (Bounds.Height - text.Height) / 2);
            if (Value is not null && IsFocused)
            {
                // The part being changed sits on the accent, measured from the digits before it.
                (int start, int length) = Segment switch { DateSegment.Year => (0, 4), DateSegment.Month => (5, 2), _ => (8, 2) };
                double left = Layout(Text[..start], LunaPalette.Text).WidthIncludingTrailingWhitespace;
                double width = Layout(Text.Substring(start, length), LunaPalette.Text).WidthIncludingTrailingWhitespace;
                context.FillRectangle(LunaPalette.Accent, new Rect(origin.X + left - 2, origin.Y - 2, width + 4, text.Height + 4), 3);
            }
            context.DrawText(text, origin);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.Spinner, () => Value is null ? NoDateText : $"{Text}, {Segment.ToString().ToLowerInvariant()}");
    }
}
