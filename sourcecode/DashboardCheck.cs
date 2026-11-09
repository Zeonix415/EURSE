using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Eurse;

class DashboardCheck {
    [STAThread]
    static void Main() {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        using (var window = new ConsoleWindow()) {
            window.Show();
            Pump(300);
            var dashboard = (Dashboard)typeof(ConsoleWindow).GetField("dashboard", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            var layer = (RainLayer)typeof(Dashboard).GetField("rain", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dashboard);
            Assert(!dashboard.Find<Button>("Start").IsEnabled, "start enabled before attach");
            var button = dashboard.Find<Button>("SelectProcess");
            var surface = (Border)button.Template.FindName("Surface", button);
            var hover = (Border)button.Template.FindName("HoverGlow", button);
            button.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) {
                RoutedEvent = System.Windows.Input.Mouse.MouseEnterEvent
            });
            PumpUntil(() => hover.Opacity >= .06, 1700);
            button.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount) {
                RoutedEvent = System.Windows.Input.Mouse.MouseLeaveEvent
            });
            var pressSetter = typeof(System.Windows.Controls.Primitives.ButtonBase).GetProperty("IsPressed").GetSetMethod(true);
            pressSetter.Invoke(button, new object[] { true });
            PumpUntil(() => ((ScaleTransform)surface.RenderTransform).ScaleX < .994, 1700);
            pressSetter.Invoke(button, new object[] { false });
            PumpUntil(() => Math.Abs(((ScaleTransform)surface.RenderTransform).ScaleX - 1) < .001 && hover.Opacity < .01, 1700);

            var lastFrame = typeof(RainLayer).GetField("previousFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            var firstStamp = (TimeSpan)lastFrame.GetValue(layer);
            PumpUntil(() => (TimeSpan)lastFrame.GetValue(layer) > firstStamp, 1700);
            layer.SetActive(false);
            layer.Draw(.33);
            Capture(dashboard.View, "work/glass-idle.png");
            layer.Draw(.68);
            Capture(dashboard.View, "work/glass-rain.png");

            Press(dashboard.Find<Button>("SelectProcess"));
            var rows = dashboard.Find<ListBox>("ProcessList");
            PumpUntil(() => rows.Items.Count > 0, 4000);
            Assert(dashboard.Find<Grid>("PickerOverlay").Visibility == Visibility.Visible, "overlay not visible");
            using (var process = Process.GetCurrentProcess()) {
                dashboard.Find<TextBox>("ProcessSearch").Text = process.Id.ToString();
                var entry = rows.Items.Cast<ProcessEntry>().Single(p => p.Id == process.Id);
                Assert(entry.Name == process.ProcessName, "wrong process name");
                rows.SelectedItem = entry;
                Press(dashboard.Find<Button>("ProcessAttach"));
                Assert(dashboard.Find<Grid>("PickerOverlay").Visibility == Visibility.Collapsed, "overlay not closed");
                Assert(dashboard.Find<TextBlock>("TargetDetail").Text.Contains("PID " + process.Id), "wrong attached PID");
            }

            var advance = typeof(Dashboard).GetMethod("Advance", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 9; i++) advance.Invoke(dashboard, null);
            Assert(dashboard.Find<Button>("Start").IsEnabled, "start disabled after attach");
            Press(dashboard.Find<Button>("Start"));
            for (int i = 0; i < 55; i++) advance.Invoke(dashboard, null);
            Assert(dashboard.Find<TextBlock>("ClientState").Text == "CONNECTED", "join failed");
            Assert(dashboard.Find<TextBlock>("Party").Text == "4 / 4 PRESERVED", "party not preserved");
            Assert(dashboard.Find<ListBox>("Events").Items.Cast<TraceRow>().Any(row => row.Message.Contains("SESSION RECOVERY SUCCESSFUL")), "missing log");
            Pump(44);
            var log = dashboard.Find<ListBox>("Events");
            var newest = (ListBoxItem)log.ItemContainerGenerator.ContainerFromIndex(log.Items.Count - 1);
            Assert(newest != null && newest.Opacity < .99 && newest.RenderTransform is TranslateTransform, "new trace did not animate");
            PumpUntil(() => Math.Abs(newest.Opacity - 1) < .01, 1700);
            layer.Draw(2.4);
            Capture(dashboard.View, "work/glass-joined.png");
            window.Size = window.MinimumSize;
            Pump(200);
            Capture(dashboard.View, "work/glass-min.png");
            window.Close();
        }
        Console.WriteLine("Dashboard layout, rain, real PID filtering, simulated attach and party recovery passed.");
    }

    static void Assert(bool result, string reason) {
        if (!result) throw new Exception(reason);
    }

    static void Press(Button button) {
        Assert(button.IsEnabled, "Disabled button " + button.Name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    }

    static void Pump(int milliseconds) {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += delegate {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    static void PumpUntil(Func<bool> condition, int milliseconds) {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.ElapsedMilliseconds < milliseconds) Pump(20);
        Assert(condition(), "Timed out waiting for UI action");
    }

    static void Capture(FrameworkElement view, string path) {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) png.Save(file);
    }
}


