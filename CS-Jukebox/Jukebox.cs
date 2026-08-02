using System;
using System.IO;
using System.Windows.Forms;
using WMPLib;

namespace CS_Jukebox
{
    public class Jukebox
    {
        private WindowsMediaPlayer player;
        private SongProfile currentSong;

        private Timer fadeTimer;
        private bool isFading = false;

        private bool isPlaying = false;
        private int timerCount = 0;
        private int timerGoal = 0;
        private float fadeVolume;
        private float volumeIncrement; //Incremental change in volume when fading out song.

        public Jukebox()
        {
            player = new WindowsMediaPlayer();

            SetupTimer();
        }

        public void PlaySong(string path)
        {
            CancelScheduledStop();
            CancelFade();
            player.URL = path;
            player.controls.play();
        }

        //Play song for length or loop indefinitely
        public void PlaySong(SongProfile song, bool loop)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path)) return;

            CancelScheduledStop();
            CancelFade();
            currentSong = song;

            // Calculate normalization gain on first play if not computed
            try
            {
                if (currentSong.NormalizationGain <= 0f)
                {
                    if (File.Exists(currentSong.Path))
                    {
                        currentSong.NormalizationGain = AudioUtils.CalculateNormalizationGain(currentSong.Path);
                    }
                    else
                    {
                        currentSong.NormalizationGain = 1f;
                    }
                }
            }
            catch
            {
                currentSong.NormalizationGain = 1f;
            }

            UpdateVolume();
            player.settings.setMode("loop", loop);
            player.URL = song.Path;
            player.controls.currentPosition = song.Start;
            player.controls.play();
        }

        //Play song with a determined amount of time in seconds
        public void PlaySong(SongProfile song, bool loop, int duration)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path)) return;
            PlaySong(song, loop);

            timerCount = 0;
            timerGoal = duration;
            isPlaying = true;
        }

        public void UpdateVolume()
        {
            if (currentSong == null || isFading) return;

            // Properties.MasterVolume = 0..100, currentSong.Volume = 0..100
            float master = (float)Properties.MasterVolume / 100f;
            float songVol = (float)currentSong.Volume / 100f;
            float focus = IsGameFocused() ? 1f : 0f;

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
            timerCount = 0;
            timerGoal = 0;
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
            Timer songTimer = new Timer();
            songTimer.Interval = 1000;
            songTimer.Tick += new EventHandler(TimerTick);
            songTimer.Start();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (isPlaying && ++timerCount >= timerGoal)
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
    }
}
