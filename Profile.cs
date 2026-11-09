using System;
using System.Windows.Forms;
using Eurse;
class Profile {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        using (var splash = new IntroWindow(Screen.PrimaryScreen.Bounds))
        using (var close = new Timer { Interval = 6200 }) {
            close.Tick += delegate { close.Stop(); splash.Close(); };
            close.Start();
            splash.ShowDialog();
        }
    }
}
