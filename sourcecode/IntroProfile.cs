using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Eurse {
    sealed class IntroWindow : Form {
        private readonly double[] samples = new double[6];
        private int sampleCount;
        private long stamp;
        private void Stamp(int index) {
            long now = Stopwatch.GetTimestamp();
            if (index > 0) samples[index - 1] += (now - stamp) * 1000.0 / Stopwatch.Frequency;
            stamp = now;
        }
        private const int TargetFps = 144;
        private static readonly string[] Credits = { "UUsataire", "Kacpix", "eyesshut", "damianull" };
        private readonly IntroTimeline timeline = new IntroTimeline(new Random());
        private FramePacer frame;
        private readonly BufferedGraphicsContext backgroundCache = new BufferedGraphicsContext();
        private BufferedGraphics background;
        private Size backgroundSize;
        private bool fadingOut;
        internal event Action RevealConsole = delegate {};
        private readonly Stopwatch clock = new Stopwatch();
        private readonly RainLine[] rain = new RainLine[112];
        private readonly LogoDrop[] drops = new LogoDrop[40];
        private readonly GraphicsPath wordmark = new GraphicsPath();
        private readonly Font creditsLabel = new Font("Segoe UI Semibold", 17, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font creditName = new Font("Segoe UI Semibold", 39, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font skipLabel = new Font("Consolas", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly StringFormat center = new StringFormat { Alignment = StringAlignment.Center };
        private readonly Color ink = Color.FromArgb(4, 11, 19);
        private readonly PointF[] waterEdge = new PointF[19];
        private readonly PointF[] waterCurrent = new PointF[36];
        private RectangleF logoBounds;

        private struct RainLine {
            public double X, Y, Speed;
            public float Length, Alpha;
        }

        private struct LogoDrop {
            public float X, Y, Radius;
            public double Phase, Cycle;
        }

        public IntroWindow(Rectangle screen) {
            Text = "EURSE";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screen;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = ink;
            Opacity = 0;

            using (var font = new FontFamily("Segoe UI"))
                wordmark.AddString("EURSE", font, (int)FontStyle.Bold, 128, PointF.Empty, StringFormat.GenericTypographic);
            var bounds = wordmark.GetBounds();
            using (var matrix = new Matrix()) {
                matrix.Translate(-bounds.Left - bounds.Width / 2, -bounds.Top);
                wordmark.Transform(matrix);
            }

            logoBounds = wordmark.GetBounds();
            var random = new Random();
            for (int i = 0; i < rain.Length; i++) {
                rain[i] = new RainLine {
                    X = random.NextDouble(), Y = random.NextDouble(), Speed = 115 + random.NextDouble() * 155,
                    Length = 12 + (float)random.NextDouble() * 29, Alpha = 20 + (float)random.NextDouble() * 41
                };
            }
            for (int i = 0; i < drops.Length; i++) {
                drops[i] = new LogoDrop {
                    X = bounds.Width * ((float)random.NextDouble() - .5f),
                    Y = bounds.Height * (float)random.NextDouble(),
                    Radius = 1.7f + (float)random.NextDouble() * 3.7f,
                    Phase = random.NextDouble() * 3.0, Cycle = 2.1 + random.NextDouble() * 1.5
                };
            }

        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            clock.Start();
            frame = new FramePacer(Handle, TargetFps);
        }

        protected override void WndProc(ref Message message) {
            if (message.Msg != FramePacer.Message) {
                base.WndProc(ref message);
                return;
            }
            double time = clock.Elapsed.TotalSeconds;
            if (time >= timeline.Finish) {
                Close();
                return;
            }
            if (!fadingOut && time >= timeline.FadeStart) {
                fadingOut = true;
                RevealConsole();
            }
            double targetOpacity = .96 * Ease(time / 1.04) * (1 - Ease((time - timeline.FadeStart) / .82));
            if (Math.Abs(Opacity - targetOpacity) >= 1.0 / 255) Opacity = targetOpacity;
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) {
            StopFrames();
            Console.WriteLine("Frames: {0}", sampleCount);
            foreach (var v in samples) Console.WriteLine("{0:0.00}", v / sampleCount);
            base.OnFormClosed(e);
        }

        private void StopFrames() {
            if (frame == null) return;
            frame.Dispose();
            frame = null;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) {
            if (keyData == Keys.Escape) {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {}

        protected override void OnPaint(PaintEventArgs e) {
            try {
                RenderFrame(e.Graphics, clock.Elapsed.TotalSeconds);
            } finally {
                if (frame != null) frame.FramePainted();
            }
        }

        internal void RenderFrame(Graphics g, double time) {
            int width = ClientSize.Width, height = ClientSize.Height;
            if (width < 1 || height < 1) return;
            if (background == null || backgroundSize.Width != width || backgroundSize.Height != height)
                CacheBackground(g, width, height);
            Stamp(0);
            background.Render(g);
            Stamp(1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            DrawRain(g, width, height, time);
            Stamp(2);
            float scale = Math.Max(.3f, Math.Min(1.45f, Math.Min(width / 940f, height / 785f)));
            var state = g.Save();
            g.TranslateTransform(width / 2, height * .475f);
            g.ScaleTransform(scale, scale);

            double logoOpacity = Ease((time - .69) / 1.04);
            if (logoOpacity > 0) {
                var logoState = g.Save();
                float rise = (float)Ease((time - 1.92) / 1.24);
                g.TranslateTransform(0, -47 - rise * 96);
                DrawLogo(g, time, logoOpacity);
                g.Restore(logoState);
            }

            Stamp(3);
            double creditsOpacity = Ease((time - 2.68) / .64);
            if (creditsOpacity > 0) {
                using (var brush = new SolidBrush(Color.FromArgb(Alpha(creditsOpacity, 228), 142, 202, 221)))
                    g.DrawString("credits to:", creditsLabel, brush, new PointF(0, -29), center);

                for (int i = 0; i < Credits.Length; i++) {
                    double local = time - IntroTimeline.CreditsStart - i * timeline.CreditLength;
                    double fadeIn = Ease(local / .52);
                    double fadeOut = i == Credits.Length - 1 ? 0 : Ease((local - timeline.CreditLength + .3) / .52);
                    double opacity = fadeIn * (1 - fadeOut);
                    if (opacity <= 0) continue;
                    float y = 2 + (float)((1 - fadeIn) * 14 - fadeOut * 12);
                    using (var brush = new SolidBrush(Color.FromArgb(Alpha(opacity, 255), 234, 247, 250)))
                        g.DrawString(Credits[i], creditName, brush, new PointF(0, y), center);
                }
                Stamp(4);
                DrawWater(g, new RectangleF(-234, 85, 468, 17), time, creditsOpacity);
            }
            Stamp(5);
            g.Restore(state);

            using (var brush = new SolidBrush(Color.FromArgb(Alpha(Ease((time - 1.1) / .7), 146), 160, 192, 206)))
                g.DrawString("ESC  /  SKIP", skipLabel, brush, width - 119, height - 37);
            Stamp(6);
            sampleCount++;
        }

        private void CacheBackground(Graphics target, int width, int height) {
            if (background != null) background.Dispose();
            backgroundCache.MaximumBuffer = new Size(width + 1, height + 1);
            background = backgroundCache.Allocate(target, new Rectangle(0, 0, width, height));
            backgroundSize = new Size(width, height);
            Graphics canvas = background.Graphics;
            canvas.Clear(ink);
            using (var outline = new GraphicsPath()) {
                outline.AddEllipse(new RectangleF(-width * .12f, -height * .28f, width * 1.24f, height * 1.5f));
                using (var glow = new PathGradientBrush(outline)) {
                    glow.CenterColor = Color.FromArgb(157, 10, 51, 67);
                    glow.SurroundColors = new[] { Color.Transparent };
                    canvas.FillPath(glow, outline);
                }
            }
        }

        private void DrawRain(Graphics g, float width, float height, double time) {
            double visibility = Ease(time / 1.15);
            using (var pen = new Pen(Color.Transparent, 1)) {
                foreach (var line in rain) {
                    double distance = line.Speed * time;
                    float x = (float)Wrap(line.X * (width + 80) - distance * .22, width + 80) - 40;
                    float y = (float)Wrap(line.Y * (height + 80) + distance, height + 80) - 40;
                    pen.Color = Color.FromArgb(Alpha(visibility, (int)line.Alpha), 118, 199, 223);
                    g.DrawLine(pen, x, y, x - line.Length * .22f, y + line.Length);
                }
            }
        }

        private void DrawLogo(Graphics g, double time, double opacity) {
            RectangleF shape = logoBounds;
            using (var halo = new Pen(Color.FromArgb(Alpha(opacity, 36), 80, 187, 230), 5))
                g.DrawPath(halo, wordmark);
            using (var gradient = new LinearGradientBrush(shape,
                Color.FromArgb(Alpha(opacity, 255), 245, 253, 255),
                Color.FromArgb(Alpha(opacity, 255), 104, 181, 209), LinearGradientMode.Vertical))
                g.FillPath(gradient, wordmark);
            using (var highlight = new Pen(Color.FromArgb(Alpha(opacity, 120), 227, 251, 255), .65f))
                g.DrawPath(highlight, wordmark);

            var clipped = g.Save();
            g.SetClip(wordmark, CombineMode.Intersect);
            double rainTime = Math.Max(0, time - .86);
            double visibility = opacity * Ease(rainTime / .56);
            foreach (var drop in drops) {
                double age = Wrap(rainTime + drop.Phase, drop.Cycle);
                float slide = (float)Math.Pow(age / drop.Cycle, 2) * 21;
                float x = drop.X - slide * .17f;
                float y = drop.Y + slide;
                float r = drop.Radius;
                double life = visibility * Ease(age / .24) * (1 - Ease((age - 1.05) / .52));
                if (life <= 0 || !wordmark.IsVisible(x, y)) continue;

                using (var bead = new SolidBrush(Color.FromArgb(Alpha(life, 145), 13, 68, 95)))
                    g.FillEllipse(bead, x - r, y - r, r * 2, r * 2.55f);
                using (var shine = new SolidBrush(Color.FromArgb(Alpha(life, 239), 238, 253, 255)))
                    g.FillEllipse(shine, x - r * .58f, y - r * .63f, r * .85f, r * .67f);
                if (age < .48) {
                    float ring = r + (float)age * 11;
                    using (var ripple = new Pen(Color.FromArgb(Alpha(visibility * (1 - age / .48), 140), 243, 254, 255), .8f))
                        g.DrawEllipse(ripple, x - ring, y - ring, 2 * ring, 2 * ring);
                }
            }
            g.Restore(clipped);
        }

        private void DrawWater(Graphics g, RectangleF bar, double time, double opacity) {
            double progress = timeline.Progress(time);
            using (var capsule = Rounded(bar)) {
                using (var bed = new SolidBrush(Color.FromArgb(Alpha(opacity, 241), 15, 38, 52)))
                    g.FillPath(bed, capsule);

                if (progress > 0) {
                    float front = bar.Left + (float)progress * bar.Width;
                    float swell = 7 * (float)Math.Min(1, Math.Min(progress * 12, (1 - progress) * 16));
                    var edge = waterEdge;
                    for (int i = 0; i < edge.Length; i++) {
                        float y = bar.Top - 1 + (bar.Height + 2) * i / (edge.Length - 1);
                        float x = front + swell * (float)(Math.Sin(i * .34 - time * 7) * .72 + Math.Sin(i * .59 + time * 4) * .28);
                        edge[i] = new PointF(x, y);
                    }
                    using (var water = new GraphicsPath()) {
                        water.AddLine(bar.Left - 2, bar.Top - 1, edge[0].X, edge[0].Y);
                        water.AddLines(edge);
                        water.AddLine(edge[edge.Length - 1].X, bar.Bottom + 1, bar.Left - 2, bar.Bottom + 1);
                        water.CloseFigure();
                        var clipped = g.Save();
                        g.SetClip(capsule);
                        using (var tint = new LinearGradientBrush(bar,
                            Color.FromArgb(Alpha(opacity, 255), 132, 247, 247),
                            Color.FromArgb(Alpha(opacity, 255), 14, 118, 178), LinearGradientMode.Vertical))
                            g.FillPath(tint, water);

                        g.SetClip(water, CombineMode.Intersect);
                        using (var foam = new Pen(Color.FromArgb(Alpha(opacity, 220), 211, 255, 252), 2.1f))
                            g.DrawLines(foam, edge);
                        using (var current = new Pen(Color.FromArgb(Alpha(opacity, 84), 221, 255, 255), 1)) {
                            var wave = waterCurrent;
                            for (int layer = 0; layer < 2; layer++) {
                                for (int i = 0; i < wave.Length; i++) {
                                    float x = bar.Left + bar.Width * i / (wave.Length - 1);
                                    float y = bar.Top + 4 + layer * 6 + (float)Math.Sin(i * .46 - time * 6 + layer * 2) * 1.65f;
                                    wave[i] = new PointF(x, y);
                                }
                                g.DrawLines(current, wave);
                            }
                        }
                        using (var bubbles = new SolidBrush(Color.FromArgb(Alpha(opacity, 146), 224, 255, 255))) {
                            for (int i = 0; i < 24; i++) {
                                float x = bar.Left + (float)Wrap(i * 52.77 + time * (19 + i % 4 * 6), bar.Width);
                                float y = bar.Top + 3 + (float)Wrap(i * 3.71 - time * (1.2 + i % 4), bar.Height - 6);
                                float radius = .8f + .25f * (i % 4);
                                g.FillEllipse(bubbles, x, y, radius * 2, radius * 2);
                            }
                        }
                        g.Restore(clipped);
                    }
                }

                using (var framePen = new Pen(Color.FromArgb(Alpha(opacity, 183), 107, 177, 196), 1))
                    g.DrawPath(framePen, capsule);
            }
        }

        private static GraphicsPath Rounded(RectangleF rect) {
            var path = new GraphicsPath();
            float diameter = rect.Height;
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 90, 180);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 180);
            path.CloseFigure();
            return path;
        }

        private static double Wrap(double value, double period) {
            return value - Math.Floor(value / period) * period;
        }

        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
        private static double Ease(double value) {
            double t = Clamp(value);
            return t * t * (3 - 2 * t);
        }
        private static int Alpha(double value, int max) { return (int)(Clamp(value) * max); }

        protected override void Dispose(bool disposing) {
            if (disposing && !IsDisposed) {
                StopFrames();
                clock.Stop();
                if (background != null) background.Dispose();
                backgroundCache.Dispose();
                wordmark.Dispose();
                creditsLabel.Dispose();
                creditName.Dispose();
                skipLabel.Dispose();
                center.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
