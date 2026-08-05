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
        private float lastAppliedVolume = -1f;

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
            playbackNormalizationGain = PrepareNormalization(currentSong);

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
                    lastAppliedVolume = afr.Volume;

                    WaveStream ws = afr;
                    if (loop)
                    {
                        ws = new LoopStream(afr);
                    }

                    waveStream = ws;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
                    outputDevice.Play();
                    songTimer.Start();
                }
                catch (Exception ex)
                {
                    CleanupOutput();
                    Console.WriteLine("Audio playback failed: " + ex.Message);
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
            playbackNormalizationGain = PrepareNormalization(currentSong);

            lock (lockObj)
            {
                CleanupOutput();
                try
                {
                    var afr = new AudioFileReader(currentSong.Path);
                    if (currentSong.Start > 0)
                        afr.CurrentTime = TimeSpan.FromSeconds(Math.Min(currentSong.Start, afr.TotalTime.TotalSeconds));
                    afr.Volume = CalculateEffectiveVolume();
                    lastAppliedVolume = afr.Volume;

                    waveStream = afr;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
                    outputDevice.Play();
                    songTimer.Start();
                }
                catch (Exception ex)
                {
                    CleanupOutput();
                    throw new InvalidOperationException("The selected audio file could not be played.", ex);
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
                if (currentSong == null || waveStream == null || outputDevice == null || fadeCts != null) return;

                float master = (float)Properties.MasterVolume / 100f;
                float songVol = (float)currentSong.Volume / 100f;
                float focus = previewMode || IsGameFocused() ? 1f : 0f;
                float effectiveVolume = Math.Clamp(master * songVol * playbackNormalizationGain * focus, 0f, 1f);
                if (Math.Abs(effectiveVolume - lastAppliedVolume) > 0.0001f)
                    ApplyReaderVolume(effectiveVolume);
            }
        }

        public void UpdatePreviewVolume(int volume)
        {
            lock (lockObj)
            {
                if (!previewMode || currentSong == null || fadeCts != null) return;

                currentSong.Volume = Math.Clamp(volume, 0, 100);
                ApplyReaderVolume(CalculateEffectiveVolume());
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
            {
                reader.Volume = volume;
                lastAppliedVolume = volume;
            }
            else if (waveStream is LoopStream loop && loop.Source is AudioFileReader loopReader)
            {
                loopReader.Volume = volume;
                lastAppliedVolume = volume;
            }
        }

        private static float PrepareNormalization(SongProfile song)
        {
            if (song == null) return 1f;
            if (song.NormalizationGain > 0f) return song.NormalizationGain;

            if (AudioUtils.TryGetCachedNormalizationGain(song.Path, out float cachedGain))
            {
                song.NormalizationGain = cachedGain;
                return cachedGain;
            }

            BeginNormalization(song);
            return 1f;
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
            Task<float> calculation = AudioUtils.GetNormalizationGainAsync(path);
            if (calculation.IsCompletedSuccessfully)
            {
                song.NormalizationGain = calculation.Result;
                return;
            }

            _ = calculation.ContinueWith(task => song.NormalizationGain = task.Result,
                CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        private void CleanupOutput()
        {
            songTimer?.Stop();
            try { outputDevice?.Stop(); } catch { }
            try { outputDevice?.Dispose(); } catch { }
            outputDevice = null;
            try { waveStream?.Dispose(); } catch { }
            waveStream = null;
            lastAppliedVolume = -1f;
        }

        private void SetupTimer()
        {
            songTimer = new System.Windows.Forms.Timer();
            songTimer.Interval = 100;
            songTimer.Tick += new EventHandler(TimerTick);
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (waveStream == null || outputDevice == null)
            {
                songTimer.Stop();
                return;
            }

            if (outputDevice.PlaybackState == PlaybackState.Stopped && fadeCts == null)
            {
                CancelScheduledStopInternal();
                lock (lockObj)
                {
                    CleanupOutput();
                }
                return;
            }

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
            private readonly long loopStartPosition;

            public LoopStream(WaveStream source)
            {
                Source = source;
                loopStartPosition = source.Position;
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
                bool rewoundWithoutReading = false;
                while (total < count)
                {
                    int read = Source.Read(buffer, offset + total, count - total);
                    if (read == 0)
                    {
                        if (!Source.CanSeek || rewoundWithoutReading)
                            break;

                        Source.Position = loopStartPosition;
                        rewoundWithoutReading = true;
                    }
                    else
                    {
                        total += read;
                        rewoundWithoutReading = false;
                    }
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
