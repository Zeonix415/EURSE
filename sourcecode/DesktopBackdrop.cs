using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;

namespace Eurse {
    sealed class DesktopBackdrop : IDisposable {
        private readonly BufferedGraphicsContext cache = new BufferedGraphicsContext();
        private Bitmap image;
        private BufferedGraphics buffer;

        internal DesktopBackdrop(Rectangle screen) {
            try { image = Capture(screen); }
            catch (Win32Exception) {}
            catch (System.Runtime.InteropServices.ExternalException) {}
        }

        private static Bitmap Capture(Rectangle screen) {
            var area = new Rectangle(Point.Empty, screen.Size);
            using (var desktop = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppRgb))
            using (var miniature = new Bitmap(Math.Max(1, screen.Width / 6), Math.Max(1, screen.Height / 6), PixelFormat.Format32bppRgb)) {
                using (var canvas = Graphics.FromImage(desktop))
                    canvas.CopyFromScreen(screen.Location, Point.Empty, screen.Size);
                using (var canvas = Graphics.FromImage(miniature)) {
                    canvas.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    canvas.DrawImage(desktop, new Rectangle(Point.Empty, miniature.Size));
                }
                var result = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppRgb);
                try {
                    using (var canvas = Graphics.FromImage(result)) {
                        canvas.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        canvas.DrawImage(miniature, area);
                        using (var tint = new SolidBrush(Color.FromArgb(69, 9, 19, 30)))
                            canvas.FillRectangle(tint, area);
                        using (var shape = new GraphicsPath()) {
                            float width = screen.Width, height = screen.Height;
                            shape.AddEllipse(width * .12f, height * .06f, width * .76f, height * .85f);
                            using (var shade = new PathGradientBrush(shape)) {
                                shade.CenterColor = Color.FromArgb(94, 4, 13, 22);
                                shade.SurroundColors = new[] { Color.FromArgb(0, 4, 13, 22) };
                                canvas.FillPath(shade, shape);
                            }
                        }
                    }
                    return result;
                } catch {
                    result.Dispose();
                    throw;
                }
            }
        }

        internal void Draw(Graphics canvas, Color fallback) {
            if (buffer == null && image != null) {
                var size = image.Size;
                cache.MaximumBuffer = new Size(size.Width + 1, size.Height + 1);
                buffer = cache.Allocate(canvas, new Rectangle(Point.Empty, size));
                buffer.Graphics.DrawImageUnscaled(image, Point.Empty);
                image.Dispose();
                image = null;
            }
            if (buffer != null) buffer.Render(canvas);
            else canvas.Clear(fallback);
        }

        public void Dispose() {
            if (image != null) image.Dispose();
            if (buffer != null) buffer.Dispose();
            cache.Dispose();
        }
    }
}
