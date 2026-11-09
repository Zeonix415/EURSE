using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;

namespace Eurse {
    sealed class ConsoleStartup : IDisposable {
        private readonly Rectangle area;
        private Task<ImageBrush> photo;
        private bool mounted;
        internal ConsoleWindow Window { get; private set; }
        internal double Progress { get; private set; }
        internal string Status { get; private set; }
        internal Exception Error { get; private set; }

        internal ConsoleStartup(Rectangle area) {
            this.area = area;
            Status = "Loading visual assets";
        }

        internal void Begin() {
            photo = Task.Run(new Func<ImageBrush>(Dashboard.CreatePhoto));
        }

        internal void Advance(double time) {
            if (Progress == 1 || Error != null) return;
            if (time >= 35) {
                Error = new TimeoutException("The session console did not finish initializing.");
                return;
            }
            if (photo == null || !photo.IsCompleted) return;
            try {
                if (Window == null) {
                    var backdrop = photo.GetAwaiter().GetResult();
                    Progress = .38;
                    Status = "Initializing session console";
                    if (time < 1.14) return;
                    Window = new ConsoleWindow(backdrop);
                    Window.StartPosition = FormStartPosition.Manual;
                    Window.Location = new Point(
                        area.Left + Math.Max(0, (area.Width - Window.Width) / 2),
                        area.Top + Math.Max(0, (area.Height - Window.Height) / 2));
                    Progress = .92;
                    Status = "Preparing workspace";
                }
                if (!mounted && time >= IntroTimeline.MountAt) {
                    Status = "Opening console";
                    mounted = true;
                    Window.RevealUnderIntro();
                }
                if (mounted && Window.IsDisposed)
                    throw new InvalidOperationException("The console was closed during startup.");
                if (mounted && Window.IsLayoutReady) {
                    Progress = 1;
                    Status = "Console ready";
                }
            } catch (Exception e) {
                Error = e;
            }
        }

        public void Dispose() {
            if (Window != null) Window.Dispose();
        }
    }
}
