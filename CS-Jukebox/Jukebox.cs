using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WMPLib;

namespace CS_Jukebox
{
    public class Jukebox : IDisposable
    {
        private WindowsMediaPlayer player;
        private SongProfile currentSong;

        private Timer fadeTimer;
        private Timer songTimer;
        private bool isFading = false;
        private bool previewMode;

        private bool isPlaying = false;
        private long scheduledStopAtMilliseconds;
        private float fadeVolume;
        private float volumeIncrement; //Incremental change in volume when fading out song.

        public Jukebox()
        {
            player = new WindowsMediaPlayer();

            SetupTimer();
        }

        public void PlaySong(string path)
        {
            PlayPreviewSong(new SongProfile(path, 100));
        }

        //Play song for length or loop indefinitely
        public void PlaySong(SongProfile song, bool loop)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path)) return;

            CancelScheduledStop();
            CancelFade();
            previewMode = false;
            currentSong = song;

            BeginNormalization(currentSong);

            UpdateVolume();
            player.settings.setMode("loop", loop);
            player.URL = song.Path;
            player.controls.currentPosition = song.Start;
            player.controls.play();
        }

        public void PlayPreviewSong(SongProfile song)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path)) return;

            CancelScheduledStop();
            CancelFade();
            previewMode = true;
            currentSong = song;
            BeginNormalization(currentSong);
            player.settings.setMode("loop", false);
            UpdateVolume();
            player.URL = song.Path;
            player.controls.currentPosition = song.Start;
            player.controls.play();
        }

        public bool IsPlaybackActive()
        {
            try
            {
                return player.playState == WMPPlayState.wmppsPlaying ||
                       player.playState == WMPPlayState.wmppsBuffering ||
                       player.playState == WMPPlayState.wmppsTransitioning;
            }
            catch
            {
                return false;
            }
        }

        //Play song with a determined amount of time in seconds
        public void PlaySong(SongProfile song, bool loop, int duration)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path)) return;
            PlaySong(song, loop);

            scheduledStopAtMilliseconds = Environment.TickCount64 + Math.Max(duration, 0) * 1000L;
            isPlaying = true;
        }

        public void UpdateVolume()
        {
            if (currentSong == null || isFading) return;

            // Properties.MasterVolume = 0..100, currentSong.Volume = 0..100
            float master = (float)Properties.MasterVolume / 100f;
            float songVol = (float)currentSong.Volume / 100f;
            float focus = previewMode || IsGameFocused() ? 1f : 0f;

            float norm = (currentSong.NormalizationGain > 0f) ? currentSong.NormalizationGain : 1f;

            float effective = master * songVol * norm * focus;
            int winVol = (int)Math.Clamp(effective * 100f, 0f, 100f);
            player.settings.volume = winVol;
        }

        //Stops the current track with a smooth fade.
        public void Stop()
        {
            CancelScheduledStop();
            // Start fade immediately instead of deferring to the next tick
            if (!isFading)
            {
                StopSong();
            }
        }

        private void CancelScheduledStop()
        {
            isPlaying = false;
            scheduledStopAtMilliseconds = 0;
        }

        private void CancelFade()
        {
            if (fadeTimer != null)
            {
                fadeTimer.Stop();
                fadeTimer.Tick -= new EventHandler(FadeTimerTick);
                fadeTimer = null;
            }
            isFading = false;
        }

        private void StopSong()
        {
            // If already fading, don't start another fade
            if (isFading) return;

            float fadeDurationSeconds = 1.4f; // safer, smoother fade
            const int intervalMs = 25; // smooth timer interval

            float startVolume = 0f;
            try { startVolume = player.settings.volume; } catch { startVolume = 0f; }

            // If volume is already essentially zero, stop immediately
            if (startVolume <= 1f)
            {
                try { player.controls.stop(); } catch { }
                try { player.settings.volume = 0; } catch { }
                isPlaying = false;
                isFading = false;
                return;
            }

            isFading = true;

            fadeVolume = startVolume;
            float steps = (fadeDurationSeconds * 1000f) / intervalMs;
            volumeIncrement = (steps > 0) ? (startVolume / steps) : startVolume;

            // Ensure any existing fade timer is stopped
            if (fadeTimer != null)
            {
                fadeTimer.Stop();
                fadeTimer.Tick -= new EventHandler(FadeTimerTick);
                fadeTimer = null;
            }

            fadeTimer = new Timer();
            fadeTimer.Interval = intervalMs;
            fadeTimer.Tick += new EventHandler(FadeTimerTick);
            fadeTimer.Start();
        }

        private void FadeTimerTick(object sender, EventArgs e)
        {
            try
            {
                fadeVolume -= volumeIncrement;

                if (fadeVolume > 1f)
                {
                    player.settings.volume = (int)fadeVolume;
                }
                else
                {
                    try { player.controls.stop(); } catch { }
                    try { player.settings.volume = 0; } catch { }

                    if (fadeTimer != null)
                    {
                        fadeTimer.Stop();
                        fadeTimer.Tick -= new EventHandler(FadeTimerTick);
                        fadeTimer = null;
                    }

                    isPlaying = false;
                    isFading = false;
                }
            }
            catch
            {
                if (fadeTimer != null)
                {
                    fadeTimer.Stop();
                    fadeTimer.Tick -= new EventHandler(FadeTimerTick);
                    fadeTimer = null;
                }
                try { player.controls.stop(); } catch { }
                try { player.settings.volume = 0; } catch { }
                isPlaying = false;
                isFading = false;
            }
        }

        private void SetupTimer()
        {
            songTimer = new Timer();
            songTimer.Interval = 100;
            songTimer.Tick += new EventHandler(TimerTick);
            songTimer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (isPlaying && Environment.TickCount64 >= scheduledStopAtMilliseconds)
            {
                StopSong();
                CancelScheduledStop();
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

        public void StopImmediately()
        {
            CancelScheduledStop();
            CancelFade();
            try { player.controls.stop(); } catch { }
            try { player.settings.volume = 0; } catch { }
        }

        private static void BeginNormalization(SongProfile song)
        {
            if (song.NormalizationGain > 0f) return;

            // Decoding audio must not block a game-state callback. Playback
            // starts at its original level, then uses the cached result.
            song.NormalizationGain = 1f;
            string path = song.Path;
            _ = Task.Run(() => AudioUtils.CalculateNormalizationGain(path))
                .ContinueWith(task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        song.NormalizationGain = task.Result;
                });
        }

        public void Dispose()
        {
            StopImmediately();
            songTimer?.Stop();
            songTimer?.Dispose();
            fadeTimer?.Dispose();
            try { player.close(); } catch { }
            if (player != null && Marshal.IsComObject(player))
            {
                try { Marshal.FinalReleaseComObject(player); } catch { }
            }
            player = null;
        }
    }
}
