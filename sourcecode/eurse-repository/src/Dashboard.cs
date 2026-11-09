using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Eurse {
    sealed class TraceRow {
        public string Time { get; private set; }
        public string Level { get; private set; }
        public string Component { get; private set; }
        public string Message { get; private set; }
        public Brush Tone { get; private set; }

        public TraceRow(string time, string level, string component, string message, Brush tone) {
            Time = time; Level = level; Component = component; Message = message; Tone = tone;
        }

        public override string ToString() {
            return Time + " " + Level.PadRight(5) + " " + Component.PadRight(10) + " " + Message;
        }
    }

    sealed class Dashboard {
        private static readonly Brush Text = ColorBrush(237, 245, 247);
        private static readonly Brush Accent = ColorBrush(164, 231, 217);
        private static readonly Brush Green = ColorBrush(156, 237, 193);
        private static readonly Brush Amber = ColorBrush(250, 206, 133);
        private static readonly Brush Red = ColorBrush(255, 157, 163);
        private static readonly Brush Muted = ColorBrush(170, 192, 199);

        internal Grid View { get; private set; }
        private readonly RobRunPatch robRun = new RobRunPatch();
        private readonly RainLayer rain = new RainLayer();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private readonly List<TraceRow> newTraceRows = new List<TraceRow>();
        private readonly List<TraceRow> transcript = new List<TraceRow>();
        private readonly Grid frost = new Grid { IsHitTestVisible = false };
        private readonly Border[] panels;
        private readonly ListBox events;
        private readonly Button start, stop;
        private readonly TextBlock gameState, gameDetail, patchState, patchDetail, connectionState, connectionDetail, status;
        private int? gamePid;
        private bool hasScannedGame;
        private bool scanningGame;
        private bool preparing;
        private string battleEvent;
        private bool closingAfterStop;
        private DateTime nextGameScan;
        private bool disposed;
        private bool traceScrollPending;
        internal event Action ReadyToClose = delegate {};

        internal Dashboard() {
            using (var stream = Resource("EURSE.Dashboard"))
                View = (Grid)XamlReader.Load(stream);

            var photo = CreatePhoto();
            Find<Grid>("PhotoLayer").Background = photo;
            frost.Children.Add(new Grid {
                Background = photo,
                Effect = new BlurEffect { Radius = 20, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance },
                CacheMode = new BitmapCache(.6)
            });
            frost.Opacity = .86;
            View.Children.Insert(2, frost);
            Find<Grid>("RainLayerHost").Children.Add(rain);
            panels = new[] { "SetupGlass", "ClientGlass", "GatewayGlass", "PartyGlass", "LogGlass", "StatusGlass" }
                .Select(Find<Border>).ToArray();
            View.Loaded += delegate { UpdateFrost(); };
            View.SizeChanged += delegate { View.Dispatcher.BeginInvoke(new Action(UpdateFrost), DispatcherPriority.Loaded); };

            start = Find<Button>("StartPatch");
            stop = Find<Button>("StopPatch");
            gameState = Find<TextBlock>("GameState");
            gameDetail = Find<TextBlock>("GameDetail");
            patchState = Find<TextBlock>("PatchState");
            patchDetail = Find<TextBlock>("PatchDetail");
            connectionState = Find<TextBlock>("ConnectionState");
            connectionDetail = Find<TextBlock>("ConnectionDetail");
            status = Find<TextBlock>("Status");
            events = Find<ListBox>("Events");
            events.Loaded += delegate { ScheduleTraceAnimation(); };

            start.Click += delegate { StartPatch(); };
            stop.Click += delegate { StopPatch(); };
            Find<Button>("Export").Click += delegate { Export(); };
            timer.Tick += delegate { Advance(); };
            timer.Start();
            Append("INFO", "EURSE", "Bundled ROB RUN backend v0.3.2 available.");
            UpdateControls();
            ScanGame();
        }

        internal T Find<T>(string name) where T : class {
            return (T)View.FindName(name);
        }

        private static Stream Resource(string name) {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        private static ImageBrush CreatePhoto() {
            var image = new BitmapImage();
            using (var stream = Resource("EURSE.Storm")) {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }
            image.Freeze();
            var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill, AlignmentY = AlignmentY.Center };
            brush.Freeze();
            return brush;
        }

        private void UpdateFrost() {
            if (disposed || !View.IsLoaded) return;
            var mask = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (var panel in panels) {
                var origin = panel.TranslatePoint(new Point(0, 0), View);
                var area = new Rect(origin, panel.RenderSize);
                double radius = panel.CornerRadius.TopLeft;
                mask.Children.Add(new RectangleGeometry(area, radius, radius));
            }
            mask.Freeze();
            frost.Clip = mask;
        }

        internal void SetVisible(bool visible) {
            if (!disposed) rain.SetActive(visible);
        }

        internal void Close() {
            disposed = true;
            timer.Stop();
            rain.SetActive(false);
            robRun.Dispose();
        }

        private void ScanGame() {
            if (scanningGame || DateTime.UtcNow < nextGameScan) return;
            nextGameScan = DateTime.UtcNow.AddSeconds(2);
            scanningGame = true;
            var dispatcher = View.Dispatcher;
            Task.Run(delegate {
                int? pid = null;
                bool available = true;
                try {
                    var processes = Process.GetProcessesByName("ROB RUN");
                    try { if (processes.Length > 0) pid = processes[0].Id; }
                    finally { foreach (var process in processes) process.Dispose(); }
                } catch (System.ComponentModel.Win32Exception) { available = false; }
                dispatcher.BeginInvoke(new Action(delegate {
                    if (disposed) return;
                    scanningGame = false;
                    if (available && (!hasScannedGame || pid != gamePid))
                        Append("INFO", "GAME", pid.HasValue ? "ROB RUN detected | PID " + pid.Value : "ROB RUN is not running.");
                    hasScannedGame = true;
                    gamePid = pid;
                    gameState.Text = !available ? "UNAVAILABLE" : pid.HasValue ? "RUNNING" : "NOT RUNNING";
                    gameState.Foreground = pid.HasValue ? Green : Muted;
                    gameDetail.Text = pid.HasValue ? "ROB RUN  /  PID " + pid.Value : "Launch ROB RUN through Steam";
                    UpdateControls();
                }), DispatcherPriority.Background);
            });
        }

        private void StartPatch() {
            if (preparing || robRun.IsRunning || !gamePid.HasValue) return;
            preparing = true;
            patchState.Text = "PREPARING";
            patchState.Foreground = Accent;
            patchDetail.Text = "Verifying bundled backend";
            battleEvent = null;
            connectionState.Text = "NO EVENT";
            connectionState.Foreground = Text;
            connectionDetail.Text = "Waiting for patch events";
            UpdateControls();
            var dispatcher = View.Dispatcher;
            Task.Run(delegate {
                string app = null;
                Exception failure = null;
                try { app = RobRunPatch.Prepare(RobRunPatch.DefaultLocation); }
                catch (Exception error) { failure = error; }
                dispatcher.BeginInvoke(new Action(delegate {
                    if (disposed) return;
                    preparing = false;
                    try {
                        if (failure != null) throw failure;
                        robRun.Launch(app);
                        patchState.Text = "STARTING";
                        patchDetail.Text = "Waiting for the elevated helper";
                        Append("INFO", "EURSE", "Starting the ROB RUN helper with administrator access.");
                    } catch (System.ComponentModel.Win32Exception error) {
                        if (error.NativeErrorCode == 1223) {
                            patchState.Text = "STANDBY";
                            patchDetail.Text = "Administrator approval was cancelled";
                            Append("WARN", "EURSE", "Administrator approval was cancelled.");
                        } else LaunchError(error);
                    } catch (Exception error) { LaunchError(error); }
                    UpdateControls();
                }), DispatcherPriority.Background);
            });
        }

        private void LaunchError(Exception error) {
            patchState.Text = "START FAILED";
            patchState.Foreground = Red;
            patchDetail.Text = "See diagnostics";
            Append("ERROR", "EURSE", error.Message);
        }

        private void StopPatch() {
            if (!robRun.CanStop) return;
            try {
                robRun.RequestStop();
                patchState.Text = "STOP REQUESTED";
                patchState.Foreground = Amber;
                patchDetail.Text = "Leave the battle to stop safely";
                Append("INFO", "EURSE", "Safe stop requested. Leave a relayed battle normally to release the route.");
            } catch (IOException error) { Append("ERROR", "EURSE", error.Message); }
            catch (UnauthorizedAccessException error) { Append("ERROR", "EURSE", error.Message); }
            UpdateControls();
        }

        internal bool StopBeforeClosing() {
            if (!robRun.IsRunning) return false;
            closingAfterStop = true;
            StopPatch();
            return true;
        }

        private void Advance() {
            ScanGame();
            string liveStatus = robRun.Poll();
            robRun.ReadOutput(ReadHelperOutput);
            if (liveStatus == null) return;
            patchDetail.Text = liveStatus;
            switch (robRun.LastEvent) {
                case "startup": patchState.Text = "STARTING"; break;
                case "armed":
                    patchState.Text = "MONITORING";
                    patchState.Foreground = Green;
                    break;
                case "trial": if (battleEvent == null || battleEvent == "joining") SetConnection("RELAY ACTIVE", liveStatus, Amber); break;
                case "reply": if (battleEvent == null || battleEvent == "joining") SetConnection("SERVER REPLIED", liveStatus, Accent); break;
                case "route_failed":
                case "retry_limit":
                case "budget_limit": if (battleEvent == null || battleEvent == "joining") SetConnection("RECOVERY LIMITED", liveStatus, Amber); break;
                case "stopping":
                    patchState.Text = "STOPPING";
                    patchState.Foreground = Amber;
                    break;
                case "error":
                case "exit_error":
                    patchState.Text = "ERROR";
                    patchState.Foreground = Red;
                    break;
                case "cleanup":
                case "exited":
                    patchState.Text = "STOPPED";
                    patchState.Foreground = Muted;
                    battleEvent = null;
                    SetConnection("NOT MONITORED", "Start the patch to watch joins", Muted);
                    break;
            }
            Append(robRun.LastEvent == "error" || robRun.LastEvent == "exit_error" ? "ERROR" : "INFO",
                "STATUS", liveStatus);
            UpdateControls();
            if (closingAfterStop && !robRun.IsRunning) ReadyToClose();
        }

        private void ReadHelperOutput(string level, string line) {
            const string prefix = "EURSE_BATTLE ";
            if (!line.StartsWith(prefix, StringComparison.Ordinal)) {
                Append(level, "HELPER", line);
                return;
            }

            string state = line.Substring(prefix.Length);
            battleEvent = state;
            switch (state) {
                case "joining":
                    SetConnection("CONNECTING", "Battle join started", Accent);
                    Append("INFO", "GAME", "Battle join started.");
                    break;
                case "joined":
                    SetConnection("CONNECTED", "No EURSE relay needed", Green);
                    Append("OK", "GAME", "Battle server connected. No EURSE relay was needed.");
                    break;
                case "joined_route":
                    SetConnection("CONNECTED", "EURSE route was active", Green);
                    Append("OK", "GAME", "Battle server connected while the EURSE route was active.");
                    break;
                case "left":
                    SetConnection("LEFT BATTLE", "Back outside the battle", Muted);
                    Append("INFO", "GAME", "Left the battle server.");
                    break;
                case "disconnected":
                    SetConnection("DISCONNECTED", "Game reported a disconnection", Amber);
                    Append("WARN", "GAME", "Game reported a battle server disconnection.");
                    break;
            }
        }

        private void SetConnection(string title, string detail, Brush tone) {
            connectionState.Text = title;
            connectionState.Foreground = tone;
            connectionDetail.Text = detail;
        }

        private void UpdateControls() {
            bool running = robRun.IsRunning;
            start.IsEnabled = gamePid.HasValue && !preparing && !running;
            stop.IsEnabled = robRun.CanStop;
            status.Text = preparing ? "PREPARING BACKEND" : running ? patchState.Text :
                gamePid.HasValue ? "READY TO START" : "WAITING FOR ROB RUN";
        }

        private void Append(string level, string component, string message) {
            var row = new TraceRow(DateTime.Now.ToString("HH:mm:ss.fff"), level, component, message,
                level == "ERROR" ? Red : level == "WARN" ? Amber : level == "OK" ? Green : level == "TRACE" ? Muted : Accent);
            transcript.Add(row);
            events.Items.Add(row);
            newTraceRows.Add(row);
            ScheduleTraceAnimation();
        }

        private void ScheduleTraceAnimation() {
            if (!events.IsLoaded || traceScrollPending || newTraceRows.Count == 0) return;
            traceScrollPending = true;
            View.Dispatcher.BeginInvoke(new Action(delegate {
                traceScrollPending = false;
                if (disposed) return;
                events.UpdateLayout();
                events.ScrollIntoView(events.Items[events.Items.Count - 1]);
                events.UpdateLayout();
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                ease.Freeze();
                foreach (var row in newTraceRows) {
                    var item = events.ItemContainerGenerator.ContainerFromItem(row) as ListBoxItem;
                    if (item == null) continue;
                    var slide = new TranslateTransform();
                    item.RenderTransform = slide;
                    var duration = TimeSpan.FromMilliseconds(330);
                    item.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration) {
                        EasingFunction = ease, FillBehavior = FillBehavior.Stop
                    });
                    slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(9, 0, duration) {
                        EasingFunction = ease, FillBehavior = FillBehavior.Stop
                    });
                }
                newTraceRows.Clear();
            }), DispatcherPriority.Render);
        }

        private void Export() {
            var dialog = new Microsoft.Win32.SaveFileDialog {
                Filter = "Log files (*.log)|*.log", FileName = "eurse-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log"
            };
            if (dialog.ShowDialog() != true) return;
            try {
                File.WriteAllLines(dialog.FileName, new[] { "EURSE | ROB RUN CONNECTION PATCH v0.3.2" }
                    .Concat(transcript.Select(row => row.ToString())), new UTF8Encoding(false));
                Append("OK", "ENGINE", "Diagnostic transcript exported");
            } catch (IOException e) {
                MessageBox.Show(e.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
            } catch (UnauthorizedAccessException e) {
                MessageBox.Show(e.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Brush ColorBrush(byte red, byte green, byte blue) {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
