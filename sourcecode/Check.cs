using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Eurse;

class Check {
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Tick(Session session, int count) { for (int i = 0; i < count; i++) session.Tick(); }
    static T Field<T>(object obj, string name) {
        return (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    }
    static void Invoke(object obj, string name, params object[] args) {
        obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    }
    static void Capture(Form form, string path) {
        using (var bmp = new Bitmap(form.Width, form.Height)) {
            form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(path);
        }
    }
    [STAThread]
    public static void Main() {
        var log = new List<string>();
        var model = new Session();
        model.Log += (level, category, message) => log.Add(message);
        model.Auto = true;
        foreach (var mode in new[] { Scenario.Party, Scenario.Solo, Scenario.Transient }) {
            model.Start(mode);
            string seat = model.Reservation;
            Tick(model, 100);
            Assert(model.State == Stage.Joined && model.Reservation == seat, "Join lost original reservation: " + mode);
            Assert(model.Generation == (mode == Scenario.Solo ? 1 : 42), "Generation not reconciled");
            Assert(model.Attempt == (mode == Scenario.Transient ? 2 : 1), "Unexpected retry count");
        }
        model.Start(Scenario.Expired); Tick(model, 100);
        Assert(model.State == Stage.Returned && log.Last().Contains("410 RESERVATION_EXPIRED"), "Expired");
        model.Auto = false;
        model.Start(Scenario.Party); Tick(model, 26); model.RestartClient();
        Assert(model.State == Stage.Returned && log.Last().Contains("409 PARTY_GENERATION_MISMATCH"), "Party restart");
        model.Start(Scenario.Solo); Tick(model, 26); model.RestartClient();
        Assert(model.State == Stage.Joined, "Solo restart");
        model.Start(Scenario.Party); Tick(model, 120);
        Assert(model.State == Stage.Returned && log.Last().Contains("MM_STATUS_TIMEOUT"), "Deadline");
        model.Start(Scenario.Party); Tick(model, 26); model.Recover(); Tick(model, 40);
        Assert(model.State == Stage.Joined, "Manual");
        Console.WriteLine("8 session paths passed");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (var picker = new ProcessPicker()) {
            picker.Show(); Application.DoEvents();
            var list = Field<ListView>(picker, "list");
            Assert(list.Items.Count > 0, "Missing process list");
            var search = Field<TextBox>(picker, "search");
            int pid = Process.GetCurrentProcess().Id;
            search.Text = pid.ToString();
            var own = list.Items.Cast<ListViewItem>().First(row => ((ProcessEntry)row.Tag).Id == pid);
            Assert(own.Text == Process.GetCurrentProcess().ProcessName, "Wrong image");
            search.Clear();
            Capture(picker, @"work\picker.png");
            search.Text = pid.ToString();
            list.Items.Cast<ListViewItem>().First(row => ((ProcessEntry)row.Tag).Id == pid).Selected = true;
            Invoke(picker, "Choose");
            Assert(picker.Selected.Id == pid, "Selection did not return the chosen PID");
            picker.Hide();
        }
        using (var window = new ConsoleWindow()) {
            window.Show(); Application.DoEvents();
            Field<Timer>(window, "timer").Stop();
            var start = Field<Button>(window, "start");
            var restart = Field<Button>(window, "restart");
            Assert(!start.Enabled, "Start available before simulated attach");
            int id = Process.GetCurrentProcess().Id;
            Invoke(window, "BeginAttach", new ProcessEntry(Process.GetCurrentProcess().ProcessName, id));
            for (int i = 0; i < 9; i++) Invoke(window, "Advance");
            Assert(start.Enabled, "Simulated attach failed");
            start.PerformClick();
            for (int i = 0; i < 60; i++) Invoke(window, "Advance");
            Application.DoEvents();
            Assert(Field<Label>(window, "client").Text == "CONNECTED", "Dashboard never reported success");
            Capture(window, @"work\console.png");
            Field<CheckBox>(window, "auto").Checked = false;
            start.PerformClick();
            for (int i = 0; i < 26; i++) Invoke(window, "Advance");
            Application.DoEvents();
            Assert(restart.Enabled, "Restart unavailable while stalled");
            Capture(window, @"work\stalled.png");
            window.Close();
        }
        Console.WriteLine("Process list, PID filter, selection and dashboard passed");
    }
}
