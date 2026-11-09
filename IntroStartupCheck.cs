using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using MediaPlayer = System.Windows.Media.MediaPlayer;
using Eurse;

class IntroStartupCheck {
    [STAThread]
    static void Main() {
        CheckTimeline();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var screen = Screen.FromPoint(Cursor.Position);
        CaptureStyle();
        using (var startup = new ConsoleStartup(screen.WorkingArea))
        using (var intro = new IntroWindow(screen.Bounds, startup))
        using (var app = new ApplicationContext())
        using (var closeAttempt = new Timer { Interval = 800 }) {
            var audio = Read<IntroAudio>(intro, "audio");
            var player = Read<MediaPlayer>(audio, "player");
            player.IsMuted = true;
            bool played = false, closedEarly = false, passed = false;
            double peak = 0;
            var elapsed = Stopwatch.StartNew();
            intro.Paint += delegate {
                double position = player.Position.TotalSeconds;
                if (position > .5) {
                    played = true;
                    peak = Math.Max(peak, player.Volume);
                }
            };
            closeAttempt.Tick += delegate {
                closeAttempt.Stop();
                intro.Close();
                closedEarly = !intro.Visible;
            };
            intro.Shown += delegate { closeAttempt.Start(); };
            intro.FormClosed += delegate {
                passed = startup.Error == null && startup.Progress == 1 && startup.Window != null &&
                    startup.Window.IsLayoutReady && startup.Window.Visible;
                app.ExitThread();
            };
            intro.Show();
            Application.Run(app);
            Assert(passed, startup.Error == null ? "Console not ready at handoff" : startup.Error.ToString());
            Assert(!closedEarly && elapsed.Elapsed.TotalSeconds >= 12 && elapsed.Elapsed.TotalSeconds < 20, "Intro closed too early or late");
            Assert(played && peak >= .16 && peak <= .171, "Audio did not reach expected low level");
            Assert(!File.Exists(Read<string>(audio, "path")), "Temporary audio leaked");
            Console.WriteLine("Real console ready at handoff after {0:0.00}s. Low-volume playback and cleanup passed.", elapsed.Elapsed.TotalSeconds);
        }
    }

    private static void CheckTimeline() {
        var timeline = new IntroTimeline();
        double previous = 0;
        for (int i = 0; i <= 1500; i++) {
            double t = i / 100.0;
            double prepared = t < 5 ? .38 : t < 14 ? .92 : 1;
            timeline.Advance(t, prepared);
            Assert(timeline.Progress <= prepared && timeline.Progress >= previous, "progress outran real startup");
            if (t < 14) Assert(double.IsPositiveInfinity(timeline.FadeStart), "fade before ready");
            previous = timeline.Progress;
        }
        Assert(timeline.Progress == 1 && timeline.FadeStart >= 14 && timeline.Finish > timeline.FadeStart, "final handoff");
        Console.WriteLine("Progress remains capped by startup work, including a slow mount.");
    }

    private static void CaptureStyle() {
        var bounds = new Rectangle(0, 0, 1180, 720);
        using (var startup = new ConsoleStartup(bounds))
        using (var intro = new IntroWindow(bounds, startup))
        using (var output = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb)) {
            var backdrop = Read<DesktopBackdrop>(intro, "backdrop");
            var image = typeof(DesktopBackdrop).GetField("image", BindingFlags.NonPublic | BindingFlags.Instance);
            var captured = (Bitmap)image.GetValue(backdrop);
            if (captured != null) captured.Dispose();
            var sample = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(sample))
            using (var brush = new LinearGradientBrush(bounds, Color.FromArgb(112, 123, 124), Color.FromArgb(27, 40, 54), 48))
                g.FillRectangle(brush, bounds);
            image.SetValue(backdrop, sample);
            var timeline = Read<IntroTimeline>(intro, "timeline");
            for (int i = 0; i <= 682; i++) timeline.Advance(i / 100.0, .92);
            using (var g = Graphics.FromImage(output)) intro.RenderFrame(g, 6.82);
            output.Save(@"work\muted-intro.png", ImageFormat.Png);
        }
    }

    private static T Read<T>(object owner, string name) {
        return (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    }

    private static void Assert(bool result, string failure) {
        if (!result) throw new Exception(failure);
    }
}
