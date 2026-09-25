using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EmuSen.LunaP.Automation;
using EmuSen.LunaP.Media;
using EmuSen.LunaP.Theme;

namespace EmuSen.LunaP.Controls
{
    /// <summary>What a DeviceStatusBar shows: each radio's state, or null when the device has none, and the battery's charge.</summary>
    /// <param name="Bluetooth">Whether Bluetooth is on; null when the device has no Bluetooth.</param>
    /// <param name="Wifi">Whether Wi-Fi is on; null when the device has no Wi-Fi.</param>
    /// <param name="Cellular">Whether the cellular radio is on; null when the device has none.</param>
    /// <param name="BatteryPercent">The battery's charge from 0 to 100; null when the device has no battery.</param>
    /// <param name="Charging">Whether the battery is charging.</param>
    public sealed record DeviceStatus(bool? Bluetooth = null, bool? Wifi = null, bool? Cellular = null, int? BatteryPercent = null, bool Charging = false);

    /// <summary>The indicators a DeviceStatusBar may show.</summary>
    [Flags]
    public enum DeviceIndicators
    {
        /// <summary>No indicator at all.</summary>
        None = 0,
        /// <summary>The Bluetooth radio.</summary>
        Bluetooth = 1,
        /// <summary>The Wi-Fi radio.</summary>
        Wifi = 2,
        /// <summary>The cellular radio.</summary>
        Cellular = 4,
        /// <summary>The battery's icon.</summary>
        Battery = 8,
        /// <summary>The battery's charge as a percentage beside its icon.</summary>
        BatteryPercentage = 16,
        /// <summary>Every indicator.</summary>
        All = Bluetooth | Wifi | Cellular | Battery | BatteryPercentage,
    }

    // Radio and battery indicators from plain data the host reads, with icons from files or drawn, on a rounded background - see docs/LunaP.md §101.5.
    /// <summary>A row of device indicators: Bluetooth, Wi-Fi and cellular when on, and the battery's level and charge. The host supplies the state; the control reads nothing from the system.</summary>
    public class DeviceStatusBar : Control
    {
        /// <summary>The icon keys Icons may name: one per radio, and one per battery level.</summary>
        public static readonly IReadOnlyList<string> IconKeys = new[] { "bluetooth", "wifi", "cellular", "battery_charging", "battery_full", "battery_high", "battery_medium", "battery_low" };

        public static readonly StyledProperty<DeviceStatus?> StatusProperty = AvaloniaProperty.Register<DeviceStatusBar, DeviceStatus?>(nameof(Status));
        public static readonly StyledProperty<DeviceIndicators> IndicatorsProperty = AvaloniaProperty.Register<DeviceStatusBar, DeviceIndicators>(nameof(Indicators), DeviceIndicators.All);
        public static readonly StyledProperty<IReadOnlyDictionary<string, string>?> IconsProperty = AvaloniaProperty.Register<DeviceStatusBar, IReadOnlyDictionary<string, string>?>(nameof(Icons));
        public static readonly StyledProperty<double> IconHeightProperty = AvaloniaProperty.Register<DeviceStatusBar, double>(nameof(IconHeight), 16);
        public static readonly StyledProperty<string?> FontPathProperty = AvaloniaProperty.Register<DeviceStatusBar, string?>(nameof(FontPath));
        public static readonly StyledProperty<double> TextScaleProperty = AvaloniaProperty.Register<DeviceStatusBar, double>(nameof(TextScale), 1);
        public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<DeviceStatusBar, Color>(nameof(Color), LunaPalette.Text.Color);
        public static readonly StyledProperty<Color> BackgroundColorProperty = AvaloniaProperty.Register<DeviceStatusBar, Color>(nameof(BackgroundColor), Colors.Transparent);
        public static readonly StyledProperty<double> BackgroundCornerRadiusProperty = AvaloniaProperty.Register<DeviceStatusBar, double>(nameof(BackgroundCornerRadius));
        public static readonly StyledProperty<Thickness> PaddingProperty = AvaloniaProperty.Register<DeviceStatusBar, Thickness>(nameof(Padding));
        public static readonly StyledProperty<double> EntrySpacingProperty = AvaloniaProperty.Register<DeviceStatusBar, double>(nameof(EntrySpacing), 6);

        static DeviceStatusBar()
        {
            AffectsMeasure<DeviceStatusBar>(StatusProperty, IndicatorsProperty, IconsProperty, IconHeightProperty, FontPathProperty, TextScaleProperty, PaddingProperty, EntrySpacingProperty);
            AffectsRender<DeviceStatusBar>(ColorProperty, BackgroundColorProperty, BackgroundCornerRadiusProperty);
        }

        /// <summary>The device's state; null shows nothing.</summary>
        public DeviceStatus? Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

        /// <summary>Which indicators may show. All by default; a radio shows only while on, the battery only when the device has one.</summary>
        public DeviceIndicators Indicators { get => GetValue(IndicatorsProperty); set => SetValue(IndicatorsProperty, value); }

        /// <summary>Icon files by the keys in IconKeys; a key without one is drawn by the control.</summary>
        public IReadOnlyDictionary<string, string>? Icons { get => GetValue(IconsProperty); set => SetValue(IconsProperty, value); }

        /// <summary>Each icon's height in pixels, 16 by default.</summary>
        public double IconHeight { get => GetValue(IconHeightProperty); set => SetValue(IconHeightProperty, value); }

        /// <summary>The font file for the battery percentage; null for the application's default typeface.</summary>
        public string? FontPath { get => GetValue(FontPathProperty); set => SetValue(FontPathProperty, value); }

        /// <summary>The percentage's em size as a multiple of IconHeight, 1 by default.</summary>
        public double TextScale { get => GetValue(TextScaleProperty); set => SetValue(TextScaleProperty, value); }

        /// <summary>The icons' and text's colour, the palette's text colour by default.</summary>
        public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        /// <summary>A fill behind the row and its padding. Transparent by default.</summary>
        public Color BackgroundColor { get => GetValue(BackgroundColorProperty); set => SetValue(BackgroundColorProperty, value); }

        /// <summary>The background's corner radius in pixels. 0 by default.</summary>
        public double BackgroundCornerRadius { get => GetValue(BackgroundCornerRadiusProperty); set => SetValue(BackgroundCornerRadiusProperty, value); }

        /// <summary>Space between the background's edge and the indicators.</summary>
        public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

        /// <summary>The gap between indicators, in pixels. 6 by default.</summary>
        public double EntrySpacing { get => GetValue(EntrySpacingProperty); set => SetValue(EntrySpacingProperty, value); }

        /// <summary>The icon key for a battery at a charge: charging, then full from 90%, high from 60%, medium from 30%, else low.</summary>
        /// <param name="percent">The charge from 0 to 100.</param>
        /// <param name="charging">Whether the battery is charging.</param>
        /// <returns>One of the battery keys of IconKeys.</returns>
        public static string BatteryKey(int percent, bool charging) =>
            charging ? "battery_charging" : percent >= 90 ? "battery_full" : percent >= 60 ? "battery_high" : percent >= 30 ? "battery_medium" : "battery_low";

        /// <summary>The icon keys shown now, left to right, and the percentage text if any.</summary>
        /// <returns>The keys of the icons drawn, and the percentage text or null.</returns>
        public (IReadOnlyList<string> Keys, string? Percentage) Shown()
        {
            var keys = new List<string>();
            if (Status is not { } s) return (keys, null);
            DeviceIndicators on = Indicators;
            if (on.HasFlag(DeviceIndicators.Bluetooth) && s.Bluetooth == true) keys.Add("bluetooth");
            if (on.HasFlag(DeviceIndicators.Wifi) && s.Wifi == true) keys.Add("wifi");
            if (on.HasFlag(DeviceIndicators.Cellular) && s.Cellular == true) keys.Add("cellular");
            string? percentage = null;
            if (s.BatteryPercent is { } level)
            {
                if (on.HasFlag(DeviceIndicators.Battery)) keys.Add(BatteryKey(level, s.Charging));
                if (on.HasFlag(DeviceIndicators.BatteryPercentage)) percentage = level.ToString(CultureInfo.CurrentCulture) + "%";
            }

            return (keys, percentage);
        }

        private GlyphTypeface Typeface() => FontPath is { Length: > 0 } p && FontFiles.Load(p) is { } t ? t : FontFiles.Default;

        private double IconWidth(string key) =>
            Icons?.GetValueOrDefault(key) is { } path && PictureFiles.Intrinsic(path) is { Width: > 0, Height: > 0 } own ? IconHeight * own.Width / own.Height : IconHeight * (key.StartsWith("battery", StringComparison.Ordinal) ? 1.6 : 1);

        protected override Size MeasureOverride(Size availableSize)
        {
            (IReadOnlyList<string> keys, string? percentage) = Shown();
            if (keys.Count == 0 && percentage is null) return default;
            double w = keys.Sum(IconWidth) + Math.Max(0, keys.Count - 1) * EntrySpacing;
            if (percentage is not null) w += EntrySpacing + FontLayout.Measure(Typeface(), IconHeight * TextScale, percentage);
            return new Size(w + Padding.Left + Padding.Right, IconHeight + Padding.Top + Padding.Bottom);
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            (IReadOnlyList<string> keys, string? percentage) = Shown();
            if (keys.Count == 0 && percentage is null) return;
            if (BackgroundColor.A > 0) context.DrawRectangle(new ImmutableSolidColorBrush(BackgroundColor), null, bounds, BackgroundCornerRadius, BackgroundCornerRadius);
            double x = Padding.Left, y = Padding.Top;
            var brush = new ImmutableSolidColorBrush(Color);
            foreach (string key in keys)
            {
                var box = new Rect(x, y, IconWidth(key), IconHeight);
                if (Icons?.GetValueOrDefault(key) is { } path) PictureFiles.Draw(context, this, path, box, Color);
                else DrawBuiltIn(context, key, box, brush, Status?.BatteryPercent ?? 0);
                x += box.Width + EntrySpacing;
            }

            if (percentage is not null)
                FontLayout.Create(Typeface(), IconHeight * TextScale, percentage, 1.2, double.PositiveInfinity, double.PositiveInfinity, false, null)
                    .Draw(context, brush, new Rect(x, y, Math.Max(0, bounds.Width - x), IconHeight), TextAlignment.Left, 0.5);
        }

        // Plain drawn icons for a key with no file: a battery filled to its level, a Wi-Fi fan, and a letter for the other radios.
        private void DrawBuiltIn(DrawingContext context, string key, Rect box, IBrush brush, int percent)
        {
            var pen = new Pen(brush, Math.Max(1, box.Height / 10));
            if (key.StartsWith("battery", StringComparison.Ordinal))
            {
                var body = new Rect(box.X, box.Y + box.Height * 0.2, box.Width * 0.9, box.Height * 0.6);
                context.DrawRectangle(null, pen, body, 2, 2);
                context.FillRectangle(brush, new Rect(body.Right, body.Y + body.Height * 0.3, box.Width * 0.1, body.Height * 0.4));
                Rect inner = body.Deflate(pen.Thickness * 1.5);
                context.FillRectangle(brush, new Rect(inner.X, inner.Y, inner.Width * Math.Clamp(percent, 0, 100) / 100.0, inner.Height));
                return;
            }

            if (key == "wifi")
            {
                for (int i = 1; i <= 3; i++)
                {
                    double r = box.Height * i / 3.2;
                    var arc = new StreamGeometry();
                    using (StreamGeometryContext g = arc.Open())
                    {
                        var c = new Point(box.Center.X, box.Bottom);
                        g.BeginFigure(new Point(c.X - r * 0.7071, c.Y - r * 0.7071), false);
                        g.ArcTo(new Point(c.X + r * 0.7071, c.Y - r * 0.7071), new Size(r, r), 0, false, SweepDirection.Clockwise);
                        g.EndFigure(false);
                    }

                    context.DrawGeometry(null, pen, arc);
                }

                return;
            }

            FontLayout.Create(Typeface(), box.Height, key == "bluetooth" ? "B" : "C", 1, double.PositiveInfinity, double.PositiveInfinity, false, null)
                .Draw(context, brush, box, TextAlignment.Center, 0.5);
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new LunaAutomationPeer(this, AutomationControlType.StatusBar, Spoken);

        // What a screen reader hears: the radios that are on in words, then the battery's charge whether or not its number is drawn.
        private string Spoken()
        {
            var parts = new List<string>();
            foreach (string key in Shown().Keys)
            {
                if (key == "bluetooth") parts.Add("Bluetooth on");
                else if (key == "wifi") parts.Add("Wi-Fi on");
                else if (key == "cellular") parts.Add("cellular on");
            }

            if (Status?.BatteryPercent is { } level && (Indicators & (DeviceIndicators.Battery | DeviceIndicators.BatteryPercentage)) != 0)
                parts.Add($"battery {level.ToString(CultureInfo.CurrentCulture)}%" + (Status.Charging ? ", charging" : ""));
            return string.Join(", ", parts);
        }
    }
}
