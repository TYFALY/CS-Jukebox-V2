using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace CS_Jukebox
{
    public class Jukebox : IDisposable
    {
        private IWavePlayer outputDevice;
        private WaveStream waveStream;
        private SongProfile currentSong;

        private System.Windows.Forms.Timer songTimer;
        private readonly object lockObj = new object();

        private CancellationTokenSource fadeCts;
        private bool previewMode;

        private bool isPlaying = false;
        private int timerCount = 0;
        private int timerGoal = 0;

        public Jukebox()
        {
            SetupTimer();
        }

        public void PlaySong(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            PlayPreviewSong(new SongProfile(path, 100));
        }

        // Play song for length or loop indefinitely
        public void PlaySong(SongProfile song, bool loop)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || !File.Exists(song.Path)) return;

            CancelScheduledStopInternal();
            CancelFadeInternal();
            previewMode = false;
            currentSong = song;

            BeginNormalization(currentSong);

            lock (lockObj)
            {
                CleanupOutput();

                try
                {
                    var afr = new AudioFileReader(currentSong.Path);
                    WaveStream ws = afr;
                    if (loop)
                    {
                        ws = new LoopStream(afr);
                    }

                    waveStream = ws;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);

                    // set start position if provided
                    if (currentSong.Start > 0 && waveStream is AudioFileReader afr2)
                    {
                        afr2.CurrentTime = TimeSpan.FromSeconds(currentSong.Start);
                    }

                    UpdateVolume();
                    outputDevice.Play();
                }
                catch
                {
                    CleanupOutput();
                }
            }
        }

        public void PlayPreviewSong(SongProfile song)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || !File.Exists(song.Path)) return;

            CancelScheduledStopInternal();
            CancelFadeInternal();
            previewMode = true;
            currentSong = song;
            BeginNormalization(currentSong);

            lock (lockObj)
            {
                CleanupOutput();
                try
                {
                    var afr = new AudioFileReader(currentSong.Path);
                    waveStream = afr;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);

                    if (currentSong.Start > 0)
                        afr.CurrentTime = TimeSpan.FromSeconds(currentSong.Start);

                    UpdateVolume();
                    outputDevice.Play();
                }
                catch
                {
                    CleanupOutput();
                }
            }
        }

        public bool IsPlaybackActive()
        {
            try
            {
                return outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing;
            }
            catch
            {
                return false;
            }
        }

        // Play song for a determined amount of time (seconds)
        public void PlaySong(SongProfile song, bool loop, int duration)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || duration <= 0) return;
            PlaySong(song, loop);

            timerCount = 0;
            timerGoal = duration;
            isPlaying = true;
        }

        public void UpdateVolume()
        {
            lock (lockObj)
            {
                if (currentSong == null || fadeCts != null) return;

                float master = (float)Properties.MasterVolume / 100f;
                float songVol = (float)currentSong.Volume / 100f;
                float focus = previewMode || IsGameFocused() ? 1f : 0f;
                float norm = (currentSong.NormalizationGain > 0f) ? currentSong.NormalizationGain : 1f;

                float effective = master * songVol * norm * focus;
                float vol = Math.Clamp(effective, 0f, 1f);

                if (waveStream is AudioFileReader afr)
                {
                    afr.Volume = vol;
                }
                else if (waveStream is LoopStream ls && ls.Source is AudioFileReader afr2)
                {
                    afr2.Volume = vol;
                }
            }
        }

        // Stops the current track with a smooth fade.
        public void Stop()
        {
            CancelScheduledStopInternal();
            // Start fade immediately
            _ = StopSongAsync();
        }

        private async Task StopSongAsync()
        {
            // If already fading, don't start another
            if (fadeCts != null) return;

            var cts = new CancellationTokenSource();
            fadeCts = cts;

            try
            {
                const int steps = 28;
                const int intervalMs = 50;

                for (int i = 0; i < steps; i++)
                {
                    if (cts.IsCancellationRequested) break;

                    lock (lockObj)
                    {
                        if (waveStream is AudioFileReader afr)
                        {
                            afr.Volume = afr.Volume * (1f - (1f / (steps - i + 1)));
                        }
                        else if (waveStream is LoopStream ls && ls.Source is AudioFileReader afr2)
                        {
                            afr2.Volume = afr2.Volume * (1f - (1f / (steps - i + 1)));
                        }
                    }

                    try { await Task.Delay(intervalMs, cts.Token); } catch { break; }
                }

                lock (lockObj)
                {
                    CleanupOutput();
                }
            }
            finally
            {
                fadeCts = null;
            }
        }

        private void CancelScheduledStopInternal()
        {
            isPlaying = false;
            timerCount = 0;
            timerGoal = 0;
        }

        private void CancelFadeInternal()
        {
            if (fadeCts != null)
            {
                try { fadeCts.Cancel(); } catch { }
                fadeCts = null;
            }
        }

        public void StopImmediately()
        {
            CancelScheduledStopInternal();
            CancelFadeInternal();
            lock (lockObj)
            {
                CleanupOutput();
            }
        }

        private static void BeginNormalization(SongProfile song)
        {
            if (song.NormalizationGain > 0f) return;

            song.NormalizationGain = 1f;
            string path = song.Path;
            _ = Task.Run(() => AudioUtils.CalculateNormalizationGain(path))
                .ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        song.NormalizationGain = task.Result;
                });
        }

        private void CleanupOutput()
        {
            try { outputDevice?.Stop(); } catch { }
            try { outputDevice?.Dispose(); } catch { }
            outputDevice = null;
            try { waveStream?.Dispose(); } catch { }
            waveStream = null;
        }

        private void SetupTimer()
        {
            songTimer = new System.Windows.Forms.Timer();
            songTimer.Interval = 1000; // 1s tick for timers
            songTimer.Tick += new EventHandler(TimerTick);
            songTimer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (isPlaying && ++timerCount >= timerGoal)
            {
                _ = StopSongAsync();
                CancelScheduledStopInternal();
            }

            UpdateVolume();
        }

        private static bool IsGameFocused()
        {
            try
            {
                return WinAPI.GetActiveProcess() == "cs2";
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            StopImmediately();
            songTimer?.Stop();
            songTimer?.Dispose();
            CleanupOutput();
        }

        // Simple looping wrapper that uses the supplied source stream
        private class LoopStream : WaveStream
        {
            public WaveStream Source { get; }

            public LoopStream(WaveStream source)
            {
                Source = source;
            }

            public override WaveFormat WaveFormat => Source.WaveFormat;

            public override long Length => long.MaxValue;

            public override long Position
            {
                get => Source.Position;
                set => Source.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int total = 0;
                while (total < count)
                {
                    int read = Source.Read(buffer, offset + total, count - total);
                    if (read == 0)
                    {
                        // loop
                        if (Source.CanSeek)
                            Source.Position = 0;
                        else
                            break;
                    }
                    else total += read;
                }
                return total;
            }

            protected override void Dispose(bool disposing)
            {
                try { Source.Dispose(); } catch { }
                base.Dispose(disposing);
            }
        }
    }
}
