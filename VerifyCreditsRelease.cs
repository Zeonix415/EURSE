using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

class VerifyLiveRelease {
    [STAThread]
    static int Main(string[] args) {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var type = assembly.GetType("Eurse.RobRunPatch");
        var method = type.GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic);
        var directory = (string)method.Invoke(null, new object[] { Path.GetFullPath(args[1]) });
        if (!File.Exists(Path.Combine(directory, "runtime", "python.exe")) ||
            File.Exists(Path.Combine(directory, "Update.ps1")) || assembly.GetType("Eurse.Session") != null)
            throw new Exception("Release contains an incorrect backend or the old simulation.");
        bool ready = false;
        EventHandler onIdle = null;
        onIdle = delegate {
            var window = Forms.Application.OpenForms.Cast<Forms.Form>().FirstOrDefault(form => form.Text == "EURSE");
            if (window == null || !window.Visible) return;
            Forms.Application.Idle -= onIdle;
            var dashboard = window.GetType().GetField("dashboard", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            var grid = (Grid)dashboard.GetType().GetProperty("View", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dashboard, null);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            timer.Tick += delegate {
                timer.Stop();
                ready = grid.FindName("StartPatch") is Button && grid.FindName("StopPatch") is Button &&
                    grid.FindName("SelectProcess") == null && grid.FindName("Scenario") == null &&
                    ((ListBox)grid.FindName("Events")).Items.Count >= 2;
                var credits = (TextBlock)grid.FindName("Credits");
                ready = ready && credits != null && new[] { "UUsataire", "Kacpix", "eyesshut", "damianull" }.All(name => credits.Text.Contains(name));
                var read = dashboard.GetType().GetMethod("ReadHelperOutput", BindingFlags.Instance | BindingFlags.NonPublic);
                var state = (TextBlock)grid.FindName("ConnectionState");
                var detail = (TextBlock)grid.FindName("ConnectionDetail");
                read.Invoke(dashboard, new object[] { "INFO", "EURSE_BATTLE joining" });
                ready = ready && state.Text == "CONNECTING";
                read.Invoke(dashboard, new object[] { "INFO", "EURSE_BATTLE joined" });
                ready = ready && state.Text == "CONNECTED" && detail.Text == "No EURSE relay needed";
                read.Invoke(dashboard, new object[] { "INFO", "EURSE_BATTLE joined_route" });
                ready = ready && state.Text == "CONNECTED" && detail.Text == "EURSE route was active";
                read.Invoke(dashboard, new object[] { "INFO", "EURSE_BATTLE left" });
                ready = ready && state.Text == "LEFT BATTLE";
                var image = new RenderTargetBitmap((int)grid.ActualWidth, (int)grid.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                image.Render(grid);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(image));
                using (var file = File.Create(@"work\eurse-live-screen.png")) png.Save(file);
                window.Close();
            };
            timer.Start();
        };
        Forms.Application.Idle += onIdle;
        assembly.EntryPoint.Invoke(null, null);
        if (!ready) throw new Exception("Live-only dashboard did not open.");
        Console.WriteLine("Rebuilt executable unpacked its offline runtime and opened the live-only dashboard.");
        return 0;
    }
}

