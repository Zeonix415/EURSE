using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Threading;

namespace Eurse {
    sealed class ConsoleWindow : Form {
        private readonly Dashboard dashboard;
        private readonly Icon windowIcon;

        public ConsoleWindow() {
            dashboard = new Dashboard();
            dashboard.ReadyToClose += Close;
            using (var source = typeof(ConsoleWindow).Assembly.GetManifestResourceStream("EURSE.WindowIcon"))
            using (var embedded = new Icon(source))
                windowIcon = (Icon)embedded.Clone();
            Text = "EURSE";
            Icon = windowIcon;
            ClientSize = new Size(1180, 720);
            MinimumSize = new Size(1010, 662);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(16, 23, 30);
            Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = dashboard.View });
        }

        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            int enabled = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && dashboard.StopBeforeClosing()) e.Cancel = true;
        }

        protected override void OnVisibleChanged(EventArgs e) {
            base.OnVisibleChanged(e);
            UpdateRain();
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            UpdateRain();
        }

        private void UpdateRain() {
            if (dashboard != null) dashboard.SetVisible(Visible && WindowState != FormWindowState.Minimized);
        }

        protected override void Dispose(bool disposing) {
            if (disposing && dashboard != null) dashboard.Close();
            base.Dispose(disposing);
            if (disposing && windowIcon != null) windowIcon.Dispose();
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    }

    static class Program {
        [STAThread]
        public static int Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool first;
            using (var instance = new Mutex(true, @"Local\EURSE.RobRun.Gui", out first)) {
                if (!first) {
                    MessageBox.Show("EURSE is already open.", "EURSE", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                var area = Screen.FromPoint(Cursor.Position).WorkingArea;
                using (var window = new ConsoleWindow()) {
                    window.StartPosition = FormStartPosition.Manual;
                    window.Location = new Point(
                        area.Left + Math.Max(0, (area.Width - window.Width) / 2),
                        area.Top + Math.Max(0, (area.Height - window.Height) / 2));
                    Application.Run(window);
                }
            }
            return 0;
        }
    }
}
