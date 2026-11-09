using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Eurse {
    sealed class RobRunPatch : IDisposable {
        private const string ResourceName = "EURSE.RobRunPatch";
        private const string Prefix = "ROB-RUN-Connection-Patch/";
        private static readonly Dictionary<string, string> StatusLabels = new Dictionary<string, string> {
            { "startup", "Initializing adapter" },
            { "armed", "Monitoring battle joins" },
            { "trial", "Relay active · checking stalled join" },
            { "reply", "Battle server replied" },
            { "connected", "Game reported a connection" },
            { "route_failed", "Could not add the temporary route" },
            { "retry_limit", "Handshake retry limit reached" },
            { "budget_limit", "Source-port limit reached" },
            { "ended", "Battle route removed" },
            { "stopping", "Waiting to stop safely" },
            { "cleanup", "Helper stopped" },
            { "error", "Helper reported an error" },
            { "game_exit", "ROB RUN closed" }
        };
        private Process launcher;
        private string statusPath;
        private string stopPath;
        private string runDirectory;
        private bool stopRequested;
        private long outputPosition, errorPosition;
        internal string LastEvent { get { return lastEvent; } }
        private string lastEvent;
        private DateTime launchedUtc;
        private DateTime nextPollUtc;

        [DataContract]
        private sealed class Status {
            [DataMember(Name = "status")] public string Event { get; set; }
            [DataMember(Name = "version")] public string Version { get; set; }
        }

        internal bool IsRunning { get { return launcher != null && !launcher.HasExited; } }
        internal bool CanStop { get { return IsRunning && !stopRequested && lastEvent != "cleanup"; } }

        internal static string DefaultLocation {
            get {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EURSE", "ROB-RUN", "0.3.2-" + BackendIntegrity.PackageHash.Substring(0, 8));
            }
        }

        internal static string Prepare(string directory) {
            string destination = Path.GetFullPath(directory);
            string parent = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(parent);
            using (var package = typeof(RobRunPatch).Assembly.GetManifestResourceStream(ResourceName)) {
                if (package == null) throw new InvalidDataException("The embedded ROB RUN package is missing.");
                using (var hash = SHA256.Create()) {
                    var digest = BitConverter.ToString(hash.ComputeHash(package)).Replace("-", "");
                    if (!digest.Equals(BackendIntegrity.PackageHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The embedded ROB RUN package failed verification.");
                }
                package.Position = 0;
                using (var archive = new ZipArchive(package, ZipArchiveMode.Read)) {
                    if (Directory.Exists(destination)) VerifyFiles(archive, destination);
                    else {
                        string stage = Path.Combine(parent, ".stage-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(stage);
                        try {
                            foreach (var entry in archive.Entries) {
                                string file = EntryPath(stage, entry);
                                Directory.CreateDirectory(Path.GetDirectoryName(file));
                                using (var input = entry.Open())
                                using (var output = File.Open(file, FileMode.CreateNew, FileAccess.Write))
                                    input.CopyTo(output);
                            }
                            try { Directory.Move(stage, destination); }
                            catch (IOException) {
                                if (!Directory.Exists(destination)) throw;
                                VerifyFiles(archive, destination);
                            }
                        } finally {
                            if (Directory.Exists(stage)) Directory.Delete(stage, true);
                        }
                    }
                }
            }
            return Path.Combine(destination, "app");
        }

        private static string EntryPath(string root, ZipArchiveEntry entry) {
            if (!entry.FullName.StartsWith(Prefix, StringComparison.Ordinal) || String.IsNullOrEmpty(entry.Name))
                throw new InvalidDataException("Unexpected ROB RUN archive entry.");
            string relative = entry.FullName.Substring(Prefix.Length);
            if (relative.Split('/').Any(part => part.Length == 0 || part == "." || part == ".." || part.IndexOfAny(new[] { '\\', ':' }) >= 0))
                throw new InvalidDataException("Unexpected ROB RUN archive path.");
            string target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The ROB RUN archive path leaves the install folder.");
            return target;
        }

        private static void VerifyFiles(ZipArchive archive, string destination) {
            using (var hash = SHA256.Create()) {
                foreach (var entry in archive.Entries) {
                    string file = EntryPath(destination, entry);
                    if (!File.Exists(file))
                        throw new InvalidDataException("Missing file in the local ROB RUN package: " + entry.Name);
                    using (var expected = entry.Open())
                    using (var actual = File.OpenRead(file)) {
                        if (actual.Length != entry.Length || !hash.ComputeHash(expected).SequenceEqual(hash.ComputeHash(actual)))
                            throw new InvalidDataException("Modified file in the local ROB RUN package: " + entry.Name);
                    }
                }
            }
        }

        internal static ProcessStartInfo LauncherInfo(string app, string run) {
            string runner;
            using (var stream = typeof(RobRunPatch).Assembly.GetManifestResourceStream("EURSE.RobRunRunner"))
            using (var reader = new StreamReader(stream))
                runner = reader.ReadToEnd();
            int owner;
            using (var current = Process.GetCurrentProcess()) owner = current.Id;
            string command = "$app = " + Quote(app) + "\n$run = " + Quote(run) +
                "\n$guiPid = " + owner + "\n$manifestHash = " + Quote(BackendIntegrity.ManifestHash) + "\n" + runner;
            return new ProcessStartInfo {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " +
                    Convert.ToBase64String(Encoding.Unicode.GetBytes(command)),
                WorkingDirectory = app,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
        }

        private static string Quote(string path) {
            return "'" + path.Replace("'", "''") + "'";
        }

        internal void Launch(string app) {
            if (IsRunning) return;
            if (HelperAlreadyActive())
                throw new InvalidOperationException("A ROB RUN helper is already active. Let it stop before starting another.");
            Dispose();
            stopPath = Path.Combine(app, "stop.request");
            if (File.Exists(stopPath)) File.Delete(stopPath);
            runDirectory = Path.Combine(Path.GetDirectoryName(app), "runs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            launchedUtc = DateTime.UtcNow;
            statusPath = Path.Combine(app, "status.json");
            lastEvent = null;
            nextPollUtc = launchedUtc;
            stopRequested = false;
            outputPosition = errorPosition = 0;
            launcher = Process.Start(LauncherInfo(app, runDirectory));
            if (launcher == null) throw new InvalidOperationException("Windows did not start the ROB RUN helper.");
        }

        private static bool HelperAlreadyActive() {
            try {
                using (Mutex.OpenExisting(@"Local\RobRunCommunityPatch")) return true;
            } catch (WaitHandleCannotBeOpenedException) { return false; }
            catch (UnauthorizedAccessException) { return true; }
        }

        internal void RequestStop() {
            if (!CanStop) return;
            File.WriteAllText(stopPath, "");
            stopRequested = true;
        }

        internal void ReadOutput(Action<string, string> consume) {
            if (runDirectory == null) return;
            ReadLog("output.log", "INFO", ref outputPosition, consume);
            ReadLog("error.log", "ERROR", ref errorPosition, consume);
            if (launcher == null) runDirectory = null;
        }

        private void ReadLog(string name, string level, ref long offset, Action<string, string> consume) {
            try {
                string path = Path.Combine(runDirectory, name);
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    if (offset >= stream.Length) return;
                    stream.Position = offset;
                    byte[] bytes = new byte[(int)Math.Min(16384, stream.Length - offset)];
                    int length = stream.Read(bytes, 0, bytes.Length);
                    if (length == 0) return;
                    int last = Array.LastIndexOf(bytes, (byte)'\n', length - 1);
                    if (last < 0) {
                        if (length == 16384) {
                            offset += length;
                            consume("WARN", "Oversized helper output was skipped.");
                        }
                        return;
                    }
                    offset += last + 1;
                    foreach (string line in Encoding.UTF8.GetString(bytes, 0, last + 1).Split('\n')) {
                        string text = line.Trim();
                        if (text.Length > 0) consume(level, text);
                    }
                }
            } catch (IOException) {}
            catch (UnauthorizedAccessException) {}
        }

        internal string Poll() {
            if (launcher == null) return null;
            if (launcher.HasExited) {
                int code = launcher.ExitCode;
                Dispose();
                lastEvent = code == 0 ? "exited" : "exit_error";
                return code == 0 ? "Helper exited" : "Helper exited with code " + code;
            }
            var now = DateTime.UtcNow;
            if (now < nextPollUtc) return null;
            nextPollUtc = now.AddMilliseconds(850);
            try {
                var file = new FileInfo(statusPath);
                if (!file.Exists || file.LastWriteTimeUtc < launchedUtc || file.Length > 2048) return null;
                using (var stream = File.Open(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                    var status = (Status)new DataContractJsonSerializer(typeof(Status)).ReadObject(stream);
                    if (status == null || status.Version != "0.3.2" || status.Event == null || status.Event == lastEvent)
                        return null;
                    lastEvent = status.Event;
                    string label;
                    return StatusLabels.TryGetValue(lastEvent, out label) ? label : null;
                }
            } catch (IOException) { return null; }
            catch (SerializationException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void Dispose() {
            if (launcher == null) return;
            launcher.Dispose();
            launcher = null;
        }
    }
}

