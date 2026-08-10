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
        private float lastAppliedVolume = -1f;

        private bool isPlaying = false;
        private long scheduledStopAtMilliseconds;

        public Jukebox()
        {
            Logger.LogEntry();
            SetupTimer();
        }

        public void PlaySong(string path)
        {
            Logger.LogEntry($"path={path}");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            PlayPreviewSong(new SongProfile(path, 100));
        }

        private static WaveStream CreateAudioStream(string path)
        {
            try
            {
                return new AudioFileReader(path);
            }
            catch (Exception ex)
            {
                Logger.LogError("AudioFileReader failed, falling back to MediaFoundationReader", ex);
                return new MediaFoundationReader(path);
            }
        }

        // Play song for length or loop indefinitely
        public void PlaySong(SongProfile song, bool loop)
        {
            Logger.LogEntry($"song={song?.Path}, loop={loop}");
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || !File.Exists(song.Path))
            {
                Logger.LogExit("Invalid song or missing file");
                return;
            }

            lock (lockObj)
            {
                CancelScheduledStopInternal();
                CancelFadeInternal();
                previewMode = false;
                currentSong = song;
                PrepareNormalization(currentSong);
                CleanupOutput();

                try
                {
                    WaveStream readerStream = CreateAudioStream(currentSong.Path);
                    if (currentSong.Start > 0)
                        readerStream.CurrentTime = TimeSpan.FromSeconds(Math.Min(currentSong.Start, readerStream.TotalTime.TotalSeconds));

                    float effectiveVolume = CalculateEffectiveVolume();
                    if (readerStream is AudioFileReader reader)
                    {
                        reader.Volume = effectiveVolume;
                        lastAppliedVolume = effectiveVolume;
                    }

                    WaveStream ws = readerStream;
                    if (loop)
                    {
                        ws = new LoopStream(readerStream);
                    }

                    waveStream = ws;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
                    outputDevice.Play();
                    songTimer.Start();
                    Logger.LogExit("Playback started successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("PlaySong failed", ex);
                    CleanupOutput();
                    Console.WriteLine("Audio playback failed: " + ex.Message);
                }
            }
        }

        public void PlayPreviewSong(SongProfile song)
        {
            Logger.LogEntry($"song={song?.Path}");
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || !File.Exists(song.Path))
            {
                Logger.LogExit("Invalid preview song or missing file");
                return;
            }

            lock (lockObj)
            {
                CancelScheduledStopInternal();
                CancelFadeInternal();
                previewMode = true;
                currentSong = song;
                PrepareNormalization(currentSong);
                CleanupOutput();

                try
                {
                    WaveStream readerStream = CreateAudioStream(currentSong.Path);
                    if (currentSong.Start > 0)
                        readerStream.CurrentTime = TimeSpan.FromSeconds(Math.Min(currentSong.Start, readerStream.TotalTime.TotalSeconds));

                    float effectiveVolume = CalculateEffectiveVolume();
                    if (readerStream is AudioFileReader reader)
                    {
                        reader.Volume = effectiveVolume;
                        lastAppliedVolume = effectiveVolume;
                    }

                    waveStream = readerStream;
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(waveStream);
                    outputDevice.Play();
                    songTimer.Start();
                    Logger.LogExit("Preview playback started successfully");
                }
                catch (Exception ex)
                {
                    Logger.LogError("PlayPreviewSong failed", ex);
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
            Logger.LogEntry($"song={song?.Path}, loop={loop}, duration={duration}");
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || duration <= 0) return;
            PlaySong(song, loop);

            if (!IsPlaybackActive()) return;

            scheduledStopAtMilliseconds = Environment.TickCount64 + duration * 1000L;
            isPlaying = true;
            Logger.LogExit($"Scheduled stop at {duration}s");
        }

        public void UpdateVolume()
        {
            lock (lockObj)
            {
                if (currentSong == null || waveStream == null || outputDevice == null || fadeCts != null) return;

                float master = (float)Properties.MasterVolume / 100f;
                float songVol = (float)currentSong.Volume / 100f;
                // Read the live normalization gain from the song profile.
                // Negative means analysis has not finished yet; treat as 1.
                float normGain = currentSong.NormalizationGain > 0f ? currentSong.NormalizationGain : 1f;
                float focus = previewMode || IsGameFocused() ? 1f : 0f;
                // Final volume = GlobalVolume * SectionVolume * normGain * focus
                float effectiveVolume = Math.Clamp(master * songVol * normGain * focus, 0f, 1f);
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
            // Read the live normalization gain from the song profile.
            // Negative means analysis has not finished yet; treat as 1.
            float normGain = currentSong.NormalizationGain > 0f ? currentSong.NormalizationGain : 1f;
            float focus = previewMode || IsGameFocused() ? 1f : 0f;
            // Final volume = GlobalVolume * SectionVolume * normGain * focus
            return Math.Clamp(master * songVolume * normGain * focus, 0f, 1f);
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

        private float PrepareNormalization(SongProfile song)
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

        // Stops the current track with a smooth 150ms fade.
        public void Stop()
        {
            Logger.LogEntry();
            lock (lockObj)
            {
                CancelScheduledStopInternal();
                if (!IsPlaybackActive())
                {
                    CleanupOutput();
                    Logger.LogExit("Not active; stopped immediately");
                    return;
                }
                // Start quick 150ms fade immediately
                _ = StopSongAsync();
                Logger.LogExit("Initiated fade out");
            }
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
                const int steps = 15;
                const int intervalMs = 10; // 15 steps * 10ms = 150ms

                for (int i = 0; i < steps; i++)
                {
                    lock (lockObj)
                    {
                        if (cts.IsCancellationRequested || !ReferenceEquals(fadeCts, cts))
                            break;

                        float factor = 1f - ((float)(i + 1) / steps);

                        if (waveStream is AudioFileReader afr)
                        {
                            afr.Volume = afr.Volume * factor;
                        }
                        else if (waveStream is LoopStream ls && ls.Source is AudioFileReader afr2)
                        {
                            afr2.Volume = afr2.Volume * factor;
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
            Logger.LogEntry();
            lock (lockObj)
            {
                CancelScheduledStopInternal();
                CancelFadeInternal();
                CleanupOutput();
            }
            Logger.LogExit();
        }

        private void BeginNormalization(SongProfile song)
        {
            Logger.LogEntry($"path={song?.Path}");
            if (song.NormalizationGain > 0f)
            {
                Logger.LogExit($"Already normalized: gain={song.NormalizationGain}");
                return;
            }

            song.NormalizationGain = 1f;
            string path = song.Path;
            Task<float> calculation = AudioUtils.GetNormalizationGainAsync(path);
            if (calculation.IsCompletedSuccessfully)
            {
                song.NormalizationGain = calculation.Result;
                Logger.LogExit($"Sync normalized: gain={song.NormalizationGain}");
                return;
            }

            // When async analysis completes, write the real gain back and
            // immediately refresh the output volume so the corrected level
            // takes effect while the track is still playing.
            _ = calculation.ContinueWith(
                task =>
                {
                    if (task.IsFaulted && task.Exception != null)
                    {
                        Logger.LogError("Async normalization failed", task.Exception);
                    }
                    else
                    {
                        song.NormalizationGain = task.Result;
                        Logger.LogEvent("AsyncNormalizationComplete", $"path={path}, gain={task.Result}");
                        // UpdateVolume reads currentSong.NormalizationGain directly,
                        // so posting it here picks up the freshly computed gain.
                        UpdateVolume();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
            Logger.LogExit("Started async normalization task");
        }

        private void CleanupOutput()
        {
            lock (lockObj)
            {
                Logger.LogEntry();
                songTimer?.Stop();
                try { outputDevice?.Stop(); } catch (Exception ex) { Logger.LogError("CleanupOutput outputDevice.Stop", ex); }
                try { outputDevice?.Dispose(); } catch (Exception ex) { Logger.LogError("CleanupOutput outputDevice.Dispose", ex); }
                outputDevice = null;
                try { waveStream?.Dispose(); } catch (Exception ex) { Logger.LogError("CleanupOutput waveStream.Dispose", ex); }
                waveStream = null;
                lastAppliedVolume = -1f;
                Logger.LogExit();
            }
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
