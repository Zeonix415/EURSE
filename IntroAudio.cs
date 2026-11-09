using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;

namespace Eurse {
    sealed class IntroAudio : IDisposable {
        private readonly MediaPlayer player = new MediaPlayer();
        private string path;
        private bool closed;
        private bool ready;
        private double targetVolume;

        internal IntroAudio() {
            player.MediaOpened += delegate {
                if (closed) return;
                ready = true;
                player.Volume = targetVolume;
                player.Play();
            };
            player.MediaFailed += delegate { Dispose(); };
        }

        internal void Open() {
            try {
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("EURSE.IntroAudio")) {
                    path = Path.Combine(Path.GetTempPath(), "eurse-intro-" + Guid.NewGuid().ToString("N") + ".mp3");
                    using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) resource.CopyTo(file);
                }
                player.Volume = 0;
                player.Open(new Uri(path, UriKind.Absolute));
            } catch (IOException) { Dispose(); }
            catch (UnauthorizedAccessException) { Dispose(); }
        }

        internal void SetVolume(double volume) {
            if (closed) return;
            targetVolume = volume;
            if (ready && (volume == 0 || Math.Abs(player.Volume - volume) >= .004)) player.Volume = volume;
        }

        public void Dispose() {
            if (closed) return;
            closed = true;
            player.Close();
            if (path == null) return;
            try { File.Delete(path); }
            catch (IOException) {}
            catch (UnauthorizedAccessException) {}
        }
    }
}

