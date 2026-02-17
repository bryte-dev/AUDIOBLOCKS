using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace AudioBlocks.App.Controls
{
    /// <summary>
    /// Vertical/horizontal fader control styled like a mixing console channel strip.
    /// Drag the cap to change value. Supports wheel and double-click reset.
    /// </summary>
    public class FaderControl : Control
    {
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<FaderControl, double>(nameof(Minimum), 0.0);
        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<FaderControl, double>(nameof(Maximum), 1.0);
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<FaderControl, double>(nameof(Value), 0.5, coerce: CoerceValue);
        public static readonly StyledProperty<double> DefaultValueProperty =
            AvaloniaProperty.Register<FaderControl, double>(nameof(DefaultValue), 0.5);
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<FaderControl, string>(nameof(Label), "");
        public static readonly StyledProperty<IBrush?> AccentProperty =
            AvaloniaProperty.Register<FaderControl, IBrush?>(nameof(Accent), new SolidColorBrush(Color.Parse("#4DD0E1")));
        public static readonly StyledProperty<bool> IsVerticalProperty =
            AvaloniaProperty.Register<FaderControl, bool>(nameof(IsVertical), false);

        public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double DefaultValue { get => GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
        public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public IBrush? Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
        public bool IsVertical { get => GetValue(IsVerticalProperty); set => SetValue(IsVerticalProperty, value); }

        public event EventHandler<double>? ValueChanged;

        private bool isDragging;
        private Point dragStart;
        private double dragStartValue;

        static FaderControl()
        {
            AffectsRender<FaderControl>(ValueProperty, MinimumProperty, MaximumProperty, AccentProperty, IsVerticalProperty);
        }

        private static double CoerceValue(AvaloniaObject obj, double val)
        {
            var f = (FaderControl)obj;
            return Math.Clamp(val, f.Minimum, f.Maximum);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ValueProperty)
                ValueChanged?.Invoke(this, (double)e.NewValue!);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2) { Value = DefaultValue; e.Handled = true; return; }
            e.Handled = true;
            isDragging = true;
            dragStart = e.GetPosition(this);
            dragStartValue = Value;
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!isDragging) return;
            var pos = e.GetPosition(this);
            double range = Maximum - Minimum;

            if (IsVertical)
            {
                double dy = dragStart.Y - pos.Y;
                double trackH = Bounds.Height - 20;
                Value = Math.Clamp(dragStartValue + (dy / trackH) * range, Minimum, Maximum);
            }
            else
            {
                double dx = pos.X - dragStart.X;
                double trackW = Bounds.Width - 16;
                Value = Math.Clamp(dragStartValue + (dx / trackW) * range, Minimum, Maximum);
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            isDragging = false;
            e.Pointer.Capture(null);
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            double step = (Maximum - Minimum) * 0.02;
            Value = Math.Clamp(Value + e.Delta.Y * step, Minimum, Maximum);
            e.Handled = true;
        }

        private IBrush GetBrush(string key, string fb)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var r) && r is IBrush b) return b;
            return new SolidColorBrush(Color.Parse(fb));
        }

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            double norm = (Maximum > Minimum) ? (Value - Minimum) / (Maximum - Minimum) : 0;
            var accent = Accent ?? new SolidColorBrush(Color.Parse("#4DD0E1"));
            var trackBg = GetBrush("KnobTrack", "#333840");
            var capBg = GetBrush("KnobBody", "#2A2D35");
            var capBorder = GetBrush("KnobRing", "#3A3F48");
            var labelBrush = GetBrush("KnobLabel", "#A0A6B0");

            if (IsVertical)
            {
                double trackW = 4;
                double trackX = w / 2 - trackW / 2;
                double labelH = string.IsNullOrEmpty(Label) ? 0 : 16;
                double trackH = h - 20 - labelH;
                double trackY = 4;

                // Track groove
                ctx.DrawRectangle(trackBg, null, new Rect(trackX, trackY, trackW, trackH), 2, 2);

                // Filled portion
                double fillH = norm * trackH;
                ctx.DrawRectangle(accent, null, new Rect(trackX, trackY + trackH - fillH, trackW, fillH), 2, 2);

                // Tick marks
                var tickPen = new Pen(trackBg, 0.5);
                for (int i = 0; i <= 10; i++)
                {
                    double ty = trackY + trackH * (1.0 - i / 10.0);
                    double tw = i % 5 == 0 ? 10 : 5;
                    ctx.DrawLine(tickPen, new Point(w / 2 - tw, ty), new Point(w / 2 + tw, ty));
                }

                // Fader cap
                double capH = 14, capW = w - 4;
                double capY = trackY + trackH - fillH - capH / 2;
                var capRect = new Rect(2, capY, capW, capH);
                ctx.DrawRectangle(capBg, new Pen(capBorder, 1), capRect, 4, 4);
                // Grip lines on cap
                var gripPen = new Pen(accent, 1);
                ctx.DrawLine(gripPen, new Point(6, capY + capH / 2 - 2), new Point(capW - 4, capY + capH / 2 - 2));
                ctx.DrawLine(gripPen, new Point(6, capY + capH / 2 + 2), new Point(capW - 4, capY + capH / 2 + 2));

                // Label
                if (!string.IsNullOrEmpty(Label))
                {
                    var lt = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Normal), 10, labelBrush);
                    ctx.DrawText(lt, new Point(w / 2 - lt.Width / 2, h - labelH));
                }
            }
            else
            {
                double trackH = 4;
                double labelH = string.IsNullOrEmpty(Label) ? 0 : 16;
                double trackY = (h - labelH) / 2 - trackH / 2;
                double trackW = w - 16;
                double trackX = 8;

                // Track groove
                ctx.DrawRectangle(trackBg, null, new Rect(trackX, trackY, trackW, trackH), 2, 2);

                // Filled portion
                double fillW = norm * trackW;
                ctx.DrawRectangle(accent, null, new Rect(trackX, trackY, fillW, trackH), 2, 2);

                // Tick marks
                var tickPen = new Pen(trackBg, 0.5);
                for (int i = 0; i <= 10; i++)
                {
                    double tx = trackX + trackW * (i / 10.0);
                    double th = i % 5 == 0 ? 6 : 3;
                    ctx.DrawLine(tickPen, new Point(tx, trackY - th), new Point(tx, trackY + trackH + th));
                }

                // Fader cap
                double capW = 14, capH = h - labelH - 4;
                double capX = trackX + fillW - capW / 2;
                var capRect = new Rect(capX, 2, capW, capH);
                ctx.DrawRectangle(capBg, new Pen(capBorder, 1), capRect, 4, 4);
                var gripPen = new Pen(accent, 1);
                ctx.DrawLine(gripPen, new Point(capX + capW / 2 - 2, 6), new Point(capX + capW / 2 - 2, capH - 2));
                ctx.DrawLine(gripPen, new Point(capX + capW / 2 + 2, 6), new Point(capX + capW / 2 + 2, capH - 2));

                if (!string.IsNullOrEmpty(Label))
                {
                    var lt = new FormattedText(Label, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, new Typeface("Inter", FontStyle.Normal, FontWeight.Normal), 10, labelBrush);
                    ctx.DrawText(lt, new Point(w / 2 - lt.Width / 2, h - labelH));
                }
            }
        }

        protected override Size MeasureOverride(Size a)
        {
            if (IsVertical)
                return new Size(double.IsInfinity(a.Width) ? 36 : Math.Min(36, a.Width),
                                double.IsInfinity(a.Height) ? 120 : a.Height);
            return new Size(double.IsInfinity(a.Width) ? 200 : a.Width,
                            double.IsInfinity(a.Height) ? 32 : Math.Min(40, a.Height));
        }
    }
}
