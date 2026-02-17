using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace AudioBlocks.App.Controls
{
    /// <summary>
    /// DAW-style segmented level meter with green/orange/red zones and peak hold.
    /// </summary>
    public class LevelMeter : Control
    {
        public static readonly StyledProperty<double> LevelProperty =
            AvaloniaProperty.Register<LevelMeter, double>(nameof(Level), 0.0);

        public static readonly StyledProperty<double> PeakProperty =
            AvaloniaProperty.Register<LevelMeter, double>(nameof(Peak), 0.0);

        public static readonly StyledProperty<bool> ClippingProperty =
            AvaloniaProperty.Register<LevelMeter, bool>(nameof(Clipping), false);

        public double Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
        public double Peak { get => GetValue(PeakProperty); set => SetValue(PeakProperty, value); }
        public bool Clipping { get => GetValue(ClippingProperty); set => SetValue(ClippingProperty, value); }

        // Thresholds (linear)
        private const double OrangeThreshold = 0.5;   // -6dB
        private const double RedThreshold = 0.85;      // -1.4dB
        private const double ClipThreshold = 0.98;

        static LevelMeter()
        {
            AffectsRender<LevelMeter>(LevelProperty, PeakProperty, ClippingProperty);
        }

        private IBrush GetBrush(string key, string fallback)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b)
                return b;
            return new SolidColorBrush(Color.Parse(fallback));
        }

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            var bgBrush = GetBrush("SurfaceBg", "#2A2D35");
            var cornerRadius = 4.0;

            // Background
            ctx.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h), cornerRadius, cornerRadius);

            double level = Math.Clamp(Level, 0, 1);
            double peak = Math.Clamp(Peak, 0, 1);
            double fillWidth = level * (w - 4);
            double innerH = h - 4;
            double innerY = 2;
            double innerX = 2;

            if (fillWidth > 0)
            {
                // Draw segments
                int segments = (int)(fillWidth / 3);
                double segW = 2;
                double gap = 1;

                for (int i = 0; i < segments && (innerX + i * (segW + gap)) < (w - 2); i++)
                {
                    double segX = innerX + i * (segW + gap);
                    double segNorm = segX / w;

                    IBrush segBrush;
                    if (segNorm > RedThreshold)
                        segBrush = new SolidColorBrush(Color.Parse("#EF4444"));
                    else if (segNorm > OrangeThreshold)
                        segBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
                    else
                        segBrush = new SolidColorBrush(Color.Parse("#22C55E"));

                    ctx.DrawRectangle(segBrush, null, new Rect(segX, innerY, segW, innerH), 1, 1);
                }
            }

            // Peak hold indicator (thin line)
            if (peak > 0.01)
            {
                double peakX = innerX + peak * (w - 4);
                IBrush peakBrush;
                if (peak > ClipThreshold)
                    peakBrush = new SolidColorBrush(Color.Parse("#EF4444"));
                else if (peak > OrangeThreshold)
                    peakBrush = new SolidColorBrush(Color.Parse("#F59E0B"));
                else
                    peakBrush = new SolidColorBrush(Color.Parse("#22C55E"));

                var peakPen = new Pen(peakBrush, 2);
                ctx.DrawLine(peakPen, new Point(peakX, innerY), new Point(peakX, innerY + innerH));
            }

            // Clip indicator (red dot top-right)
            if (Clipping || peak > ClipThreshold)
            {
                var clipBrush = new SolidColorBrush(Color.Parse("#EF4444"));
                ctx.DrawEllipse(clipBrush, null, new Point(w - 6, 6), 4, 4);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? 18 : Math.Min(24, availableSize.Height));
        }
    }
}
