using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace AudioBlocks.App.Controls
{
    /// <summary>
    /// Rotary knob control — drag vertically to change value.
    /// Theme-aware rendering for light/dark modes.
    /// </summary>
    public class KnobControl : Control
    {
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<KnobControl, double>(nameof(Minimum), 0.0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<KnobControl, double>(nameof(Maximum), 1.0);

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<KnobControl, double>(nameof(Value), 0.5, coerce: CoerceValue);

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<KnobControl, string>(nameof(Label), "");

        public static readonly StyledProperty<string> DisplayValueProperty =
            AvaloniaProperty.Register<KnobControl, string>(nameof(DisplayValue), "");

        public static readonly StyledProperty<IBrush?> KnobColorProperty =
            AvaloniaProperty.Register<KnobControl, IBrush?>(nameof(KnobColor), new SolidColorBrush(Color.Parse("#FF6B6B")));

        public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public string DisplayValue { get => GetValue(DisplayValueProperty); set => SetValue(DisplayValueProperty, value); }
        public IBrush? KnobColor { get => GetValue(KnobColorProperty); set => SetValue(KnobColorProperty, value); }

        public event EventHandler<double>? ValueChanged;

        private bool isDragging;
        private Point dragStart;
        private double dragStartValue;
        private const double Sensitivity = 0.004;

        static KnobControl()
        {
            AffectsRender<KnobControl>(ValueProperty, MinimumProperty, MaximumProperty, KnobColorProperty);
        }

        private static double CoerceValue(AvaloniaObject obj, double val)
        {
            var knob = (KnobControl)obj;
            return Math.Clamp(val, knob.Minimum, knob.Maximum);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ValueProperty)
                ValueChanged?.Invoke(this, (double)change.NewValue!);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
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
            double dy = dragStart.Y - pos.Y;
            double range = Maximum - Minimum;
            Value = Math.Clamp(dragStartValue + dy * Sensitivity * range, Minimum, Maximum);
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
            double step = (Maximum - Minimum) * 0.015;
            Value = Math.Clamp(Value + e.Delta.Y * step, Minimum, Maximum);
            e.Handled = true;
        }

        private IBrush GetThemeBrush(string key, string fallback)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush brush)
                return brush;
            return new SolidColorBrush(Color.Parse(fallback));
        }

        public override void Render(DrawingContext ctx)
        {
            double size = Math.Min(Bounds.Width, Bounds.Height - 30);
            if (size <= 0) return;

            double cx = Bounds.Width / 2;
            double knobY = size / 2 + 2;
            double radius = size / 2 - 3;

            double norm = (Maximum > Minimum) ? (Value - Minimum) / (Maximum - Minimum) : 0;

            double startAngle = -135;
            double angle = startAngle + norm * 270;
            double rad = angle * Math.PI / 180;

            // Track ring
            var trackBrush = GetThemeBrush("KnobTrack", "#333840");
            var trackPen = new Pen(trackBrush, 3.5);
            ctx.DrawEllipse(null, trackPen, new Point(cx, knobY), radius, radius);

            // Active arc
            var accentBrush = KnobColor ?? new SolidColorBrush(Color.Parse("#FF6B6B"));
            var accentPen = new Pen(accentBrush, 3.5);

            var arcGeo = new StreamGeometry();
            using (var sgc = arcGeo.Open())
            {
                double startRad = startAngle * Math.PI / 180;
                sgc.BeginFigure(
                    new Point(cx + radius * Math.Cos(startRad), knobY + radius * Math.Sin(startRad)),
                    false);

                int steps = Math.Max(2, (int)(norm * 36));
                for (int i = 1; i <= steps; i++)
                {
                    double a = startAngle + (norm * 270) * i / steps;
                    double r = a * Math.PI / 180;
                    sgc.LineTo(new Point(cx + radius * Math.Cos(r), knobY + radius * Math.Sin(r)));
                }
                sgc.EndFigure(false);
            }
            ctx.DrawGeometry(null, accentPen, arcGeo);

            // Knob body
            var bodyBrush = GetThemeBrush("KnobBody", "#2A2D35");
            var ringBrush = GetThemeBrush("KnobRing", "#3A3F48");
            var bodyPen = new Pen(ringBrush, 1.5);
            ctx.DrawEllipse(bodyBrush, bodyPen, new Point(cx, knobY), radius - 6, radius - 6);

            // Pointer line
            double ptrLen = radius - 11;
            var ptrPen = new Pen(accentBrush, 2.5);
            ctx.DrawLine(ptrPen,
                new Point(cx + ptrLen * 0.3 * Math.Cos(rad), knobY + ptrLen * 0.3 * Math.Sin(rad)),
                new Point(cx + ptrLen * Math.Cos(rad), knobY + ptrLen * Math.Sin(rad)));

            // Label
            if (!string.IsNullOrEmpty(Label))
            {
                var labelBrush = GetThemeBrush("KnobLabel", "#A0A6B0");
                var labelText = new FormattedText(Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Normal),
                    11, labelBrush);
                ctx.DrawText(labelText, new Point(cx - labelText.Width / 2, knobY + radius + 5));
            }

            // Display value
            if (!string.IsNullOrEmpty(DisplayValue))
            {
                var valBrush = GetThemeBrush("KnobValue", "#E0E0E0");
                var valText = new FormattedText(DisplayValue,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    10, valBrush);
                ctx.DrawText(valText, new Point(cx - valText.Width / 2, knobY - 6));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 68 : Math.Min(68, availableSize.Width),
                double.IsInfinity(availableSize.Height) ? 88 : Math.Min(88, availableSize.Height));
        }
    }
}
