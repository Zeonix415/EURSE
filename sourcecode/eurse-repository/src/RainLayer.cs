using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace Eurse {
    sealed class RainLayer : FrameworkElement {
        private const int Count = 150;
        private readonly DrawingVisual rain = new DrawingVisual();
        private readonly Stopwatch clock = new Stopwatch();
        private readonly RainDrop[] drops = new RainDrop[Count];
        private readonly Pen[] tails = new Pen[5];
        private readonly Pen[] heads = new Pen[5];
        private bool active;
        private TimeSpan previousFrame;

        private struct RainDrop {
            public double X, Y, Speed, Length, Phase;
            public int Layer;
        }

        internal RainLayer() {
            AddVisualChild(rain);
            ClipToBounds = true;
            var random = new Random();
            for (int layer = 0; layer < tails.Length; layer++) {
                tails[layer] = MakePen(13 + layer * 8, .55 + layer * .11);
                heads[layer] = MakePen(24 + layer * 14, .55 + layer * .11);
            }
            for (int i = 0; i < Count; i++) {
                int layer = i % tails.Length;
                drops[i] = new RainDrop {
                    X = random.NextDouble(), Y = random.NextDouble(), Layer = layer,
                    Speed = 130 + 62 * layer + random.NextDouble() * 105,
                    Length = 9 + 7 * layer + random.NextDouble() * 12,
                    Phase = random.NextDouble() * 6.28
                };
            }
        }

        protected override int VisualChildrenCount { get { return 1; } }
        protected override Visual GetVisualChild(int index) {
            if (index != 0) throw new ArgumentOutOfRangeException("index");
            return rain;
        }

        internal void SetActive(bool enable) {
            if (enable == active) return;
            active = enable;
            if (enable) {
                clock.Start();
                CompositionTarget.Rendering += Animate;
            } else {
                CompositionTarget.Rendering -= Animate;
                clock.Stop();
            }
        }

        private void Animate(object sender, EventArgs args) {
            var timestamp = ((RenderingEventArgs)args).RenderingTime;
            if (timestamp == previousFrame || ActualWidth < 1 || ActualHeight < 1) return;
            previousFrame = timestamp;
            Draw(clock.Elapsed.TotalSeconds);
        }

        internal void Draw(double time) {
            double width = ActualWidth + 96, height = ActualHeight + 96;
            using (var canvas = rain.RenderOpen()) {
                foreach (var drop in drops) {
                    double distance = drop.Speed * time;
                    double drift = 14 * Math.Sin(time * .7 + drop.Phase);
                    double tilt = .19 + .05 * Math.Sin(time * .72 + drop.Phase);
                    double x = Wrap(drop.X * width - distance * .22 + drift, width) - 48;
                    double y = Wrap(drop.Y * height + distance, height) - 48;
                    var head = new Point(x, y);
                    var middle = new Point(x + drop.Length * tilt * .39, y - drop.Length * .39);
                    var tail = new Point(x + drop.Length * tilt, y - drop.Length);
                    canvas.DrawLine(tails[drop.Layer], tail, middle);
                    canvas.DrawLine(heads[drop.Layer], middle, head);
                }
            }
        }

        private static Pen MakePen(int alpha, double width) {
            var brush = new SolidColorBrush(Color.FromArgb((byte)alpha, 177, 216, 231));
            var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            return pen;
        }

        private static double Wrap(double value, double period) {
            return value - Math.Floor(value / period) * period;
        }
    }
}

