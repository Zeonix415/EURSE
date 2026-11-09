using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Eurse;

class RobRunCheck {
    static int Main(string[] args) {
        string root = Path.GetFullPath(args[0]);
        var paths = new string[2];
        Parallel.Invoke(() => paths[0] = RobRunPatch.Prepare(root), () => paths[1] = RobRunPatch.Prepare(root));
        string launch = paths[0];
        Assert(launch == paths[1] && File.Exists(launch), "concurrent extraction");
        string app = Path.GetDirectoryName(launch);
        foreach (var file in new[] { "wintun.dll", "runtime/python.exe", "patch.py", "transport.py", "LICENSE", "SHA256SUMS.txt" })
            Assert(File.Exists(Path.Combine(app, file)), "missing " + file);
        var start = RobRunPatch.LauncherInfo(launch);
        Assert(File.Exists(start.FileName) && start.Arguments.EndsWith("-File \"" + launch + "\"") &&
            start.WorkingDirectory == app && String.IsNullOrEmpty(start.Verb), "wrong launcher settings");
        string readme = Path.Combine(app, "README.txt");
        byte[] original = File.ReadAllBytes(readme);
        File.AppendAllText(readme, "tamper check");
        bool rejected = false;
        try { RobRunPatch.Prepare(root); }
        catch (InvalidDataException) { rejected = true; }
        finally { File.WriteAllBytes(readme, original); }
        Assert(rejected && RobRunPatch.Prepare(root) == launch, "integrity check did not reject tampering");

        using (var patch = new RobRunPatch()) {
            string status = Path.Combine(root, "status-test.json");
            Set(patch, "launcher", Process.GetCurrentProcess());
            Set(patch, "statusPath", status);
            Set(patch, "launchedUtc", DateTime.UtcNow.AddSeconds(-10));
            File.WriteAllText(status, "{\"status\":\"armed\",\"version\":\"0.3.2\"}");
            Assert(patch.Poll() == "Monitoring battle joins", "status not surfaced");
            Set(patch, "nextPollUtc", DateTime.MinValue);
            Assert(patch.Poll() == null, "duplicate status");
            File.WriteAllText(status, "{\"status\":\"connected\",\"version\":\"0.3.2\"}");
            Set(patch, "nextPollUtc", DateTime.MinValue);
            Assert(patch.Poll() == "Game reported a connection", "connection status");
        }
        Console.WriteLine("Embedded deployment, launcher settings, source integrity and status reporting passed.");
        return 0;
    }

    static void Set(object instance, string field, object value) {
        instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
    }

    static void Assert(bool ok, string message) {
        if (!ok) throw new Exception(message);
    }
}
