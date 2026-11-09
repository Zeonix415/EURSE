using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.IO;
using MediaPlayer = System.Windows.Media.MediaPlayer;
using System.Reflection;
using System.Windows.Forms;
using Eurse;

class IntroCheck {
    public readonly Stopwatch Clock = new Stopwatch();
    public readonly List<double> Paints = new List<double>();

    static void Assert(bool condition, string reason) {
        if (!condition) throw new Exception(reason);
    }

    static void CheckTimelines() {
        var finishTimes = new HashSet<double>();
        for (int seed = 0; seed < 100; seed++) {
            var curve = new IntroTimeline(new Random(seed));
            Assert(curve.Finish >= 12.7 && curve.Finish <= 15.71, "duration");
            Assert(curve.Progress(0) == 0 && curve.Progress(curve.FillEnd) == 1, "endpoints");
            finishTimes.Add(curve.Finish);
            double previous = 0;
            for (double t = .01; t < curve.Finish; t += .01) {
                double next = curve.Progress(t);
                Assert(next >= previous && next <= 1, "monotonicity");
                previous = next;
            }
        }
        Assert(finishTimes.Count > 90, "randomized duration");
        Console.WriteLine("100 randomized timelines validated");
    }

    static void Benchmark(int width, int height, int frames) {
        using (var splash = new IntroWindow(new Rectangle(0, 0, width, height)))
        using (var bitmap = new Bitmap(width, height))
        using (var canvas = Graphics.FromImage(bitmap)) {
            splash.RenderFrame(canvas, 4);
            var samples = new List<double>();
            for (int i = 0; i < frames; i++) {
                var clock = Stopwatch.StartNew();
                splash.RenderFrame(canvas, 3.1 + i * .04);
                samples.Add(clock.Elapsed.TotalMilliseconds);
            }
            samples.Sort();
            Console.WriteLine("{0}x{1}: render median {2:0.00} ms / p95 {3:0.00} ms",
                width, height, samples[frames / 2], samples[(int)(frames * .95)]);
        }
    }

    [STAThread]
    public static void Main() {
        CheckTimelines();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Benchmark(1920, 1080, 130);
        var screen = Screen.FromPoint(Cursor.Position);
        var filter = new IntroCheck();
        using (var splash = new IntroWindow(screen.Bounds)) {
            Assert(splash.Bounds == screen.Bounds && splash.Opacity == 0, "fullscreen initial state");
            var audio = typeof(IntroWindow).GetField("audio", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(splash);
            var player = (MediaPlayer)audio.GetType().GetField("player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(audio);
            var file = audio.GetType().GetField("path", BindingFlags.NonPublic | BindingFlags.Instance);
            bool played = false;
            player.IsMuted = true;
            splash.Paint += delegate {
                filter.Paints.Add(filter.Clock.Elapsed.TotalSeconds);
                if (!played && filter.Clock.Elapsed.TotalSeconds > 2) played = player.Position.TotalSeconds > .5;
            };
            var timeline = (IntroTimeline)typeof(IntroWindow).GetField("timeline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(splash);
            int reveals = 0;
            splash.RevealConsole += delegate { reveals++; };
            bool closeBlocked = false;
            using (var attempt = new Timer { Interval = 750 }) {
                attempt.Tick += delegate {
                    attempt.Stop();
                    splash.Close();
                    closeBlocked = splash.Visible;
                };
                splash.Shown += delegate { attempt.Start(); };
                filter.Clock.Start();
                splash.ShowDialog();
            }
            Assert(closeBlocked, "Intro was closed before loading finished");
            Assert(played && !File.Exists((string)file.GetValue(audio)), "Audio did not play or temporary audio was not removed");
            Console.WriteLine("Embedded MP3 played and cleaned up");
            Assert(reveals == 1 && Math.Abs(filter.Clock.Elapsed.TotalSeconds - timeline.Finish) < .5, "handoff");
            var steady = filter.Paints.Where(t => t >= 2 && t < timeline.FadeStart).ToArray();
            var times = steady.Skip(1).Select((time, i) => (time - steady[i]) * 1000).OrderBy(ms => ms).ToArray();
            Assert(times.Length > 0, "Missing paints");
            Console.WriteLine("Fullscreen run {0:0.00}s, {1:0.0} rendered FPS, frame interval p95 {2:0.00}ms",
                filter.Clock.Elapsed.TotalSeconds, (steady.Length - 1) / (steady.Last() - steady.First()), times[(int)(times.Length * .95)]);
        }
        Console.WriteLine("Intro close shortcut blocked until loading finishes");
    }
}
