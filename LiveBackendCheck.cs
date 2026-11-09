using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Eurse;

class LiveBackendCheck {
    static int Main(string[] args) {
        string root = Path.GetFullPath(args[0]);
        var paths = new string[2];
        Parallel.Invoke(() => paths[0] = RobRunPatch.Prepare(root), () => paths[1] = RobRunPatch.Prepare(root));
        string app = paths[0];
        Assert(app == paths[1] && File.Exists(Path.Combine(app, "patch.py")), "concurrent extraction");
        Assert(!File.Exists(Path.Combine(app, "Launch.ps1")) && !File.Exists(Path.Combine(app, "Update.ps1")) &&
            !File.Exists(Path.Combine(app, "demo.py")), "stock launcher, updater or demo found");
        string run = Path.Combine(root, "test run");
        Directory.CreateDirectory(run);
        var start = RobRunPatch.LauncherInfo(app, run);
        Assert(File.Exists(start.FileName) && start.WorkingDirectory == app && start.Verb == "runas" &&
            start.WindowStyle == ProcessWindowStyle.Hidden, "wrong launcher settings");
        string payload = start.Arguments.Substring(start.Arguments.LastIndexOf(' ') + 1);
        string runner = Encoding.Unicode.GetString(Convert.FromBase64String(payload));
        Assert(!runner.Contains("github") && runner.Contains("--eurse-headless") &&
            runner.Contains(BackendIntegrity.ManifestHash), "runner config");
        runner = runner.Replace("$guiPid = " + Process.GetCurrentProcess().Id, "$guiPid = $PID");
        File.WriteAllText(Path.Combine(root, "check-runner.ps1"), @"
function Start-Process {
    param($FilePath, $WorkingDirectory, $WindowStyle, [switch]$PassThru, $ArgumentList,
          $RedirectStandardOutput, $RedirectStandardError)
    if (-not (Test-Path -LiteralPath $FilePath) -or $ArgumentList -notcontains '--accept-risk' -or
        $ArgumentList -notcontains '--eurse-headless' -or $WindowStyle -ne 'Hidden') { throw 'Wrong launch arguments' }
    Set-Content -LiteralPath $RedirectStandardOutput -Value 'Launcher verified without opening a network connection.'
    $mock = [pscustomobject]@{ ExitCode = 0 }
    $mock | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param($delay) return $true }
    return $mock
}
".Replace("\\$", "$") + runner, new UTF8Encoding(true));
        string file = Path.Combine(app, "transport.py");
        byte[] original = File.ReadAllBytes(file);
        File.AppendAllText(file, "tamper check");
        bool rejected = false;
        try { RobRunPatch.Prepare(root); }
        catch (InvalidDataException) { rejected = true; }
        finally { File.WriteAllBytes(file, original); }
        Assert(rejected && RobRunPatch.Prepare(root) == app, "integrity rejection");
        using (var patch = new RobRunPatch()) {
            string status = Path.Combine(root, "status-test.json");
            string stop = Path.Combine(root, "stop-test.request");
            Set(patch, "launcher", Process.GetCurrentProcess());
            Set(patch, "statusPath", status);
            Set(patch, "stopPath", stop);
            Set(patch, "runDirectory", run);
            Set(patch, "launchedUtc", DateTime.UtcNow.AddSeconds(-10));
            File.WriteAllText(status, "{\"status\":\"armed\",\"version\":\"0.3.2\"}");
            Assert(patch.Poll() == "Monitoring battle joins", "status not surfaced");
            patch.RequestStop();
            Assert(File.Exists(stop) && !patch.CanStop, "safe stop");
            File.WriteAllText(Path.Combine(run, "output.log"), "real helper output\r\n");
            string line = null;
            patch.ReadOutput((level, message) => line = message);
            Assert(line == "real helper output", "stdout capture");
            line = null;
            patch.ReadOutput((level, message) => line = message);
            Assert(line == null, "stdout repeated");
        }
        Console.WriteLine("Embedded install, elevation arguments, safe stop, integrity rejection and output polling passed.");
        return 0;
    }
    static void Set(object instance, string field, object value) {
        instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
    }
    static void Assert(bool ok, string message) {
        if (!ok) throw new Exception(message);
    }
}

