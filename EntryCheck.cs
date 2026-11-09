using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Eurse;

class EntryCheck {
    [STAThread]
    static int Main() {
        bool opened = false;
        var time = Stopwatch.StartNew();
        EventHandler check = null;
        check = delegate {
            var window = Application.OpenForms.OfType<ConsoleWindow>().FirstOrDefault();
            if (window == null || !window.Visible) return;
            opened = Application.OpenForms.Count == 1 && window.Text == "EURSE" &&
                window.Icon != null && time.ElapsedMilliseconds < 5000;
            Application.Idle -= check;
            window.Close();
        };
        Application.Idle += check;
        Program.Main();
        if (!opened) throw new Exception("Console did not open directly");
        Console.WriteLine("Console opened directly after {0}ms.", time.ElapsedMilliseconds);
        return 0;
    }
}
