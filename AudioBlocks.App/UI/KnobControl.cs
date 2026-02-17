using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace AudioBlocks.App.Controls
{
    /// <summary>
    /// Rotary knob control — drag vertically to change value.
    /// Styled like a synth/pedalboard knob.
    /// </summary>
    public class KnobControl : Control
    {
        // ===== Styled/Direct Properties =====
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

        // ===== Drag state =====
        private bool isDragging;
        private Point dragStart;
        private double dragStartValue;
        private const double Sensitivity = 0.005; // per pixel

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

        // ===== Input =====
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
            double dy = dragStart.Y - pos.Y; // up = positive
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
            double step = (Maximum - Minimum) * 0.02;
            Value = Math.Clamp(Value + e.Delta.Y * step, Minimum, Maximum);
            e.Handled = true;
        }

        // ===== Render =====
        public override void Render(DrawingContext ctx)
        {
            double size = Math.Min(Bounds.Width, Bounds.Height - 24);
            if (size <= 0) return;

            double cx = Bounds.Width / 2;
            double knobY = size / 2 + 2;
            double radius = size / 2 - 2;

            // Normalized value 0..1
            double norm = (Maximum > Minimum) ? (Value - Minimum) / (Maximum - Minimum) : 0;

            // Arc: -135° to +135° (270° range)
            double startAngle = -135;
            double angle = startAngle + norm * 270;
            double rad = angle * Math.PI / 180;

            // Outer ring (track)
            var trackPen = new Pen(new SolidColorBrush(Color.Parse("#333840")), 3);
            ctx.DrawEllipse(null, trackPen, new Point(cx, knobY), radius, radius);

            // Active arc
            var accentBrush = KnobColor ?? new SolidColorBrush(Color.Parse("#FF6B6B"));
            var accentPen = new Pen(accentBrush, 3);

            // Draw filled arc via line segments
            var arcGeo = new StreamGeometry();
            using (var sgc = arcGeo.Open())
            {
                double startRad = startAngle * Math.PI / 180;
                sgc.BeginFigure(
                    new Point(cx + radius * Math.Cos(startRad), knobY + radius * Math.Sin(startRad)),
                    false);

                int steps = Math.Max(2, (int)(norm * 30));
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
            var bodyBrush = new SolidColorBrush(Color.Parse("#2A2D35"));
            var bodyPen = new Pen(new SolidColorBrush(Color.Parse("#3A3F48")), 1.5);
            ctx.DrawEllipse(bodyBrush, bodyPen, new Point(cx, knobY), radius - 5, radius - 5);

            // Pointer line
            double ptrLen = radius - 10;
            var ptrPen = new Pen(accentBrush, 2.5);
            ctx.DrawLine(ptrPen,
                new Point(cx + ptrLen * 0.3 * Math.Cos(rad), knobY + ptrLen * 0.3 * Math.Sin(rad)),
                new Point(cx + ptrLen * Math.Cos(rad), knobY + ptrLen * Math.Sin(rad)));

            // Label text
            if (!string.IsNullOrEmpty(Label))
            {
                var labelText = new FormattedText(Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.Normal),
                    10, new SolidColorBrush(Color.Parse("#8A8F98")));
                ctx.DrawText(labelText, new Point(cx - labelText.Width / 2, knobY + radius + 4));
            }

            // Display value text
            if (!string.IsNullOrEmpty(DisplayValue))
            {
                var valText = new FormattedText(DisplayValue,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter", FontStyle.Normal, FontWeight.SemiBold),
                    9, new SolidColorBrush(Color.Parse("#CCCCCC")));
                ctx.DrawText(valText, new Point(cx - valText.Width / 2, knobY - 5));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 56 : Math.Min(56, availableSize.Width),
                double.IsInfinity(availableSize.Height) ? 72 : Math.Min(72, availableSize.Height));
        }
    }
}
