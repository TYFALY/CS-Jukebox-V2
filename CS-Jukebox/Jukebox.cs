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
        private float playbackNormalizationGain = 1f;

        private bool isPlaying = false;
        private long scheduledStopAtMilliseconds;

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
            playbackNormalizationGain = GetNormalizationSnapshot(currentSong);
            BeginNormalization(currentSong);

            lock (lockObj)
            {
                CleanupOutput();

                try
                {
                    var afr = new AudioFileReader(currentSong.Path);
                    if (currentSong.Start > 0)
                        afr.CurrentTime = TimeSpan.FromSeconds(Math.Min(currentSong.Start, afr.TotalTime.TotalSeconds));

                    // AudioFileReader may be read by WaveOutEvent.Init, so set
                    // the effective volume before initializing the output buffer.
                    afr.Volume = CalculateEffectiveVolume();

                    WaveStream ws = afr;
                    if (loop)
                    {
                        ws = new LoopStream(afr);
                    }

                    waveStream = ws;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
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
            playbackNormalizationGain = GetNormalizationSnapshot(currentSong);
            BeginNormalization(currentSong);

            lock (lockObj)
            {
                CleanupOutput();
                try
                {
                    var afr = new AudioFileReader(currentSong.Path);
                    if (currentSong.Start > 0)
                        afr.CurrentTime = TimeSpan.FromSeconds(Math.Min(currentSong.Start, afr.TotalTime.TotalSeconds));
                    afr.Volume = CalculateEffectiveVolume();

                    waveStream = afr;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
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

            if (!IsPlaybackActive()) return;

            scheduledStopAtMilliseconds = Environment.TickCount64 + duration * 1000L;
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
                ApplyReaderVolume(Math.Clamp(master * songVol * playbackNormalizationGain * focus, 0f, 1f));
            }
        }

        private float CalculateEffectiveVolume()
        {
            if (currentSong == null) return 0f;
            float master = Properties.MasterVolume / 100f;
            float songVolume = currentSong.Volume / 100f;
            float focus = previewMode || IsGameFocused() ? 1f : 0f;
            return Math.Clamp(master * songVolume * playbackNormalizationGain * focus, 0f, 1f);
        }

        private void ApplyReaderVolume(float volume)
        {
            if (waveStream is AudioFileReader reader)
                reader.Volume = volume;
            else if (waveStream is LoopStream loop && loop.Source is AudioFileReader loopReader)
                loopReader.Volume = volume;
        }

        private static float GetNormalizationSnapshot(SongProfile song)
        {
            return song?.NormalizationGain > 0f ? song.NormalizationGain : 1f;
        }

        // Stops the current track with a smooth fade.
        public void Stop()
        {
            CancelScheduledStopInternal();
            if (!IsPlaybackActive())
            {
                StopImmediately();
                return;
            }
            // Start fade immediately
            _ = StopSongAsync();
        }

        private async Task StopSongAsync()
        {
            var cts = new CancellationTokenSource();
            lock (lockObj)
            {
                if (fadeCts != null)
                {
                    cts.Dispose();
                    return;
                }
                fadeCts = cts;
            }

            try
            {
                const int steps = 28;
                const int intervalMs = 50;

                for (int i = 0; i < steps; i++)
                {
                    lock (lockObj)
                    {
                        if (cts.IsCancellationRequested || !ReferenceEquals(fadeCts, cts))
                            break;

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
                    if (!cts.IsCancellationRequested && ReferenceEquals(fadeCts, cts))
                        CleanupOutput();
                }
            }
            finally
            {
                lock (lockObj)
                {
                    if (ReferenceEquals(fadeCts, cts))
                        fadeCts = null;
                }
                cts.Dispose();
            }
        }

        private void CancelScheduledStopInternal()
        {
            isPlaying = false;
            scheduledStopAtMilliseconds = 0;
        }

        private void CancelFadeInternal()
        {
            CancellationTokenSource cts;
            lock (lockObj)
            {
                cts = fadeCts;
                fadeCts = null;
            }
            try { cts?.Cancel(); } catch { }
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
            songTimer.Interval = 100;
            songTimer.Tick += new EventHandler(TimerTick);
            songTimer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (isPlaying && Environment.TickCount64 >= scheduledStopAtMilliseconds)
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
