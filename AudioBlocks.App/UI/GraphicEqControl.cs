using AudioBlocks.App.Effects;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Globalization;

namespace AudioBlocks.App.Controls
{
    /// <summary>
    /// Interactive 10-band graphic EQ visualizer with draggable band handles.
    /// </summary>
    public class GraphicEqControl : Control
    {
        private GraphicEqEffect? effect;
        private int dragBand = -1;
        private const float MaxDb = 12f;
        private const double HandleRadius = 6;
        private const double LabelHeight = 18;
        private const double LeftMargin = 30;
        private const double RightMargin = 8;

        public GraphicEqEffect? Effect
        {
            get => effect;
            set { effect = value; InvalidateVisual(); }
        }

        public event Action<int, float>? GainChanged;

        static GraphicEqControl()
        {
            FocusableProperty.OverrideDefaultValue<GraphicEqControl>(true);
        }

        private IBrush GetBrush(string key, string fallback)
        {
            if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b) return b;
            return new SolidColorBrush(Color.Parse(fallback));
        }

        private double BandToX(int i, double w)
        {
            double usable = w - LeftMargin - RightMargin;
            return LeftMargin + usable * i / (GraphicEqEffect.BandCount - 1);
        }

        private double DbToY(float db, double h)
        {
            double usable = h - LabelHeight;
            return usable / 2.0 * (1.0 - db / MaxDb);
        }

        private float YToDb(double y, double h)
        {
            double usable = h - LabelHeight;
            return (float)((1.0 - 2.0 * y / usable) * MaxDb);
        }

        private int HitTestBand(Point pos)
        {
            if (effect == null) return -1;
            double h = Bounds.Height;
            double w = Bounds.Width;

            for (int i = 0; i < GraphicEqEffect.BandCount; i++)
            {
                double bx = BandToX(i, w);
                double by = DbToY(effect.Gains[i], h);
                double dist = Math.Sqrt((pos.X - bx) * (pos.X - bx) + (pos.Y - by) * (pos.Y - by));
                if (dist <= HandleRadius + 6) return i;
            }
            return -1;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            dragBand = HitTestBand(e.GetPosition(this));
            if (dragBand >= 0)
            {
                e.Handled = true;
                e.Pointer.Capture(this);
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (dragBand < 0 || effect == null) return;

            var pos = e.GetPosition(this);
            float db = Math.Clamp(YToDb(pos.Y, Bounds.Height), -MaxDb, MaxDb);
            // Snap to 0 when close
            if (MathF.Abs(db) < 0.4f) db = 0f;
            effect.Gains[dragBand] = db;
            GainChanged?.Invoke(dragBand, db);
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (dragBand >= 0)
            {
                e.Pointer.Capture(null);
                dragBand = -1;
            }
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (effect == null) return;

            int band = HitTestBand(e.GetPosition(this));
            if (band < 0) return;

            float step = 0.5f * (float)e.Delta.Y;
            effect.Gains[band] = Math.Clamp(effect.Gains[band] + step, -MaxDb, MaxDb);
            GainChanged?.Invoke(band, effect.Gains[band]);
            InvalidateVisual();
            e.Handled = true;
        }

        public override void Render(DrawingContext ctx)
        {
            if (effect == null) return;

            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            var bgBrush = GetBrush("SurfaceBg", "#1A1D23");
            var gridPen = new Pen(GetBrush("DimText", "#333840"), 0.5);
            var zeroPen = new Pen(GetBrush("MutedText", "#555"), 1, DashStyle.Dash);
            var curvePen = new Pen(GetBrush("AccentCyan", "#4DD0E1"), 2);
            var boostBrush = new SolidColorBrush(Color.Parse("#2244DD88"));
            var cutBrush = new SolidColorBrush(Color.Parse("#22EF4444"));
            var handleStroke = new Pen(GetBrush("AccentCyan", "#4DD0E1"), 2);
            var handleFill = GetBrush("CardDefault", "#22252B");
            var labelBrush = GetBrush("DimText", "#6B7280");
            var dbLabelBrush = GetBrush("MutedText", "#555");

            // Background
            ctx.FillRectangle(bgBrush, new Rect(0, 0, w, h));

            // Grid lines (±3, ±6, ±9, ±12 dB)
            for (int db = -12; db <= 12; db += 3)
            {
                double y = DbToY(db, h);
                ctx.DrawLine(db == 0 ? zeroPen : gridPen, new Point(LeftMargin, y), new Point(w - RightMargin, y));
            }

            // dB labels on left
            foreach (int db in new[] { 12, 6, 0, -6, -12 })
            {
                double y = DbToY(db, h);
                var ft = new FormattedText(
                    db == 0 ? "0" : $"{db:+0;-0}",
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default), 9, dbLabelBrush);
                ctx.DrawText(ft, new Point(LeftMargin - ft.Width - 4, y - ft.Height / 2));
            }

            // Bars + handles
            double zeroY = DbToY(0, h);
            double barWidth = Math.Max(6, (w - LeftMargin - RightMargin) / (GraphicEqEffect.BandCount + 2));

            for (int i = 0; i < GraphicEqEffect.BandCount; i++)
            {
                double bx = BandToX(i, w);
                double by = DbToY(effect.Gains[i], h);

                // Bar fill
                double barTop = Math.Min(zeroY, by);
                double barH = Math.Abs(by - zeroY);
                if (barH > 0.5)
                {
                    var brush = effect.Gains[i] >= 0 ? boostBrush : cutBrush;
                    ctx.FillRectangle(brush, new Rect(bx - barWidth / 2, barTop, barWidth, barH));
                }

                // Level meter (thin line under the bar)
                if (effect.Levels[i] > 0.001f)
                {
                    float levelDb = 20f * MathF.Log10(Math.Max(effect.Levels[i], 0.0001f));
                    float clampedDb = Math.Clamp(levelDb, -MaxDb, MaxDb);
                    double ly = DbToY(clampedDb, h);
                    var meterPen = new Pen(new SolidColorBrush(Color.Parse("#44DD88")), 2);
                    ctx.DrawLine(meterPen, new Point(bx - barWidth / 4, ly), new Point(bx + barWidth / 4, ly));
                }

                // Handle circle
                bool active = i == dragBand;
                var fill = active ? GetBrush("AccentCyan", "#4DD0E1") : handleFill;
                var stroke = active ? new Pen(Brushes.White, 2) : handleStroke;
                ctx.DrawEllipse(fill, stroke, new Point(bx, by), HandleRadius, HandleRadius);
            }

            // Smooth curve through band handles
            var geometry = new StreamGeometry();
            using (var sgc = geometry.Open())
            {
                var first = new Point(BandToX(0, w), DbToY(effect.Gains[0], h));
                sgc.BeginFigure(first, false);

                for (int i = 0; i < GraphicEqEffect.BandCount - 1; i++)
                {
                    var p0 = new Point(BandToX(i, w), DbToY(effect.Gains[i], h));
                    var p1 = new Point(BandToX(i + 1, w), DbToY(effect.Gains[i + 1], h));
                    double tension = (p1.X - p0.X) / 3.0;
                    var cp1 = new Point(p0.X + tension, p0.Y);
                    var cp2 = new Point(p1.X - tension, p1.Y);
                    sgc.CubicBezierTo(cp1, cp2, p1);
                }
            }
            ctx.DrawGeometry(null, curvePen, geometry);

            // Frequency labels at bottom
            for (int i = 0; i < GraphicEqEffect.BandCount; i++)
            {
                double bx = BandToX(i, w);
                var ft = new FormattedText(
                    GraphicEqEffect.Labels[i],
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default), 9, labelBrush);
                ctx.DrawText(ft, new Point(bx - ft.Width / 2, h - LabelHeight + 3));
            }
        }
    }
}