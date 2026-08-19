using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using CSGSI;
using CSGSI.Nodes;

namespace CS_Jukebox
{
    public class GameLogic : IDisposable
    {
        public Jukebox jukebox;

        private GameStateListener gsl;
        private MusicState musicState = MusicState.None;
        private int playerMVPs = -1;
        private readonly SynchronizationContext uiContext;
        private bool roundTenSecondPlayed;
        private bool bombTenSecondPlayed;
        private bool stopped;
        private int lastKnownPlayerHealth = -1;

        public GameLogic()
        {
            Logger.LogEntry("Initializing GameLogic");
            uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            jukebox = new Jukebox();
            try
            {
                StartGameListener();
                Logger.LogExit("GameLogic initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("GameLogic constructor", ex);
                jukebox.Dispose();
                throw;
            }
        }

        void StartGameListener()
        {
            Logger.LogEntry();
            gsl = new GameStateListener(3010);
            gsl.NewGameState += new NewGameStateHandler(OnGameStateReceived);

            if (!gsl.Start())
            {
                var ex = new InvalidOperationException(
                    "The Game State Integration listener could not start on port 3010. " +
                    "Close another CS Jukebox instance or any application using this port, then try again.");
                Logger.LogError("StartGameListener failed", ex);
                Console.WriteLine("Game State Listener failed to start.");
                throw ex;
            }
            else
            {
                Console.WriteLine("Listening...");
                Logger.LogExit("GameStateListener started on port 3010");
            }
        }

        public void Stop()
        {
            Logger.LogEntry($"stopped={stopped}");
            if (stopped) return;
            stopped = true;
            Console.WriteLine("Stopping Game Listener");
            if (gsl != null)
            {
                gsl.NewGameState -= new NewGameStateHandler(OnGameStateReceived);
                gsl.Stop();
            }
            jukebox?.Dispose();
            Logger.LogExit();
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnGameStateReceived(GameState gs)
        {
            if (stopped) return;
            Logger.LogEntry($"RoundPhase={gs.Round.Phase}, MapPhase={gs.Map.Phase}");
            uiContext.Post(_ =>
            {
                if (!stopped) OnNewGameState(gs);
            }, null);
        }

        void OnNewGameState(GameState gs)
        {
            Logger.LogEntry($"Phase={gs.Round.Phase}, MusicState={musicState}");
            if (Properties.SelectedKit == null) return;

            bool isLocal = string.IsNullOrWhiteSpace(gs.Provider.SteamID) ||
                string.IsNullOrWhiteSpace(gs.Player.SteamID) ||
                string.Equals(gs.Provider.SteamID, gs.Player.SteamID, StringComparison.OrdinalIgnoreCase);

            int currentMvpCount = gs.Player.MatchStats.MVPs;
            if (isLocal && currentMvpCount >= 0 && (playerMVPs < 0 || currentMvpCount < playerMVPs))
                playerMVPs = currentMvpCount;

            if (gs.Map.JSON.Equals("{}") && musicState != MusicState.Menu)
            {
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.mainMenuSong, Properties.SelectedKit.mainMenuSongs), true))
                {
                    musicState = MusicState.Menu;
                    playerMVPs = -1;
                    Logger.LogEvent("TransitionToMenu", $"SelectedKit={Properties.SelectedKit.Name}");
                    Console.WriteLine("Main Menu");
                }
            }

            if (gs.Round.Phase == RoundPhase.FreezeTime && musicState != MusicState.FreezeTime)
            {
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.freezeSong, Properties.SelectedKit.freezeSongs), false))
                {
                    musicState = MusicState.FreezeTime;
                    Logger.LogEvent("TransitionToFreezeTime", $"SelectedKit={Properties.SelectedKit.Name}");
                    roundTenSecondPlayed = false;
                    bombTenSecondPlayed = false;
                    Console.WriteLine("FreezeTime Begun");
                }
            }

            if (musicState == MusicState.Menu)
            {
                if (gs.Round.Phase == RoundPhase.Live)
                {
                    //Fade out main menu song
                    Console.WriteLine("Stopping main menu song");
                    Logger.LogEvent("StopMainMenuSong");
                    jukebox.Stop();
                }
                return;
            }

            if (gs.Round.Phase == RoundPhase.Live && musicState != MusicState.Live && musicState != MusicState.BombPlanted)
            {
                jukebox.Stop();
                // Play the round-start track to completion, matching the behaviour
                // of FreezeTime and MVP which also use the two-arg overload.
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.startSong, Properties.SelectedKit.startSongs), false))
                {
                    musicState = MusicState.Live;
                    Logger.LogEvent("TransitionToLive", $"SelectedKit={Properties.SelectedKit.Name}");
                    Console.WriteLine("Round Begun");
                }
            }

            HandlePlayerDeath(gs);

            if (gs.Round.Phase == RoundPhase.Over && musicState != MusicState.Over)
            {
                if (gs.Round.WinTeam == RoundWinTeam.T && gs.Player.Team == PlayerTeam.T)
                {
                    RoundWin(gs);
                }
                else if (gs.Round.WinTeam == RoundWinTeam.CT && gs.Player.Team == PlayerTeam.CT)
                {
                    RoundWin(gs);
                }
                else
                {
                    //lose
                    if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.loseSong, Properties.SelectedKit.loseSongs), false))
                    {
                        musicState = MusicState.Over;
                        Logger.LogEvent("TransitionToRoundOver", $"WinTeam={gs.Round.WinTeam}, PlayerTeam={gs.Player.Team}");
                        Logger.LogEvent("RoundLose");
                    }
                }
            }

            if (gs.Round.Bomb == BombState.Planted && musicState != MusicState.BombPlanted)
            {
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.bombSong, Properties.SelectedKit.bombSongs), false))
                {
                    musicState = MusicState.BombPlanted;
                    bombTenSecondPlayed = false;
                    Logger.LogEvent("TransitionToBombPlanted");
                    Console.WriteLine("Bomb Planted");
                }
            }

            HandleTenSecondCues(gs);
            Logger.LogExit();
        }

        private bool PlaySongIfValid(SongProfile song, bool loop, int duration = 0)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Path) || !File.Exists(song.Path))
            {
                Logger.Log($"WARNING: Song file path is missing or invalid: '{song?.Path}'");
                return false;
            }

            if (duration > 0)
                jukebox.PlaySong(song, loop, duration);
            else
                jukebox.PlaySong(song, loop);

            return true;
        }

        private void HandlePlayerDeath(GameState gs)
        {
            int currentHealth = gs.Player.State.Health;
            bool diedNow = IsLocalPlayerDeath(gs, lastKnownPlayerHealth);

            if (currentHealth >= 0)
                lastKnownPlayerHealth = currentHealth;

            if (!diedNow) return;

            if (musicState == MusicState.BombPlanted || IsActiveBombCountdown(gs))
            {
                Logger.LogEvent("PlayerDeathSuppressed", "BombPlanted or active bomb countdown in progress");
                return;
            }

            if (PlaySongIfValid(
                Properties.SelectedKit.PickSong(Properties.SelectedKit.deathSong, Properties.SelectedKit.deathSongs),
                false,
                5))
            {
                Console.WriteLine("Local player died");
                Logger.LogEvent("PlayerDeathDetected");
            }
        }

        public static bool IsActiveBombCountdown(GameState gs)
        {
            if (gs.Round.Bomb == BombState.Planted)
                return true;

            PhaseCountdownsNode countdown = gs.PhaseCountdowns;
            if (countdown.Phase == PhaseCountdownsPhase.Bomb)
                return true;

            if (gs.Bomb.State == BombState.Planted)
                return true;

            return false;
        }

        public static bool IsLocalPlayerDeath(GameState gs, int lastKnownHealth)
        {
            int currentHealth = gs.Player.State.Health;
            int previousHealth = gs.Previously.Player.State.Health;
            string providerSteamId = gs.Provider.SteamID;
            string playerSteamId = gs.Player.SteamID;

            bool isLocalPlayer = string.IsNullOrWhiteSpace(providerSteamId) ||
                string.IsNullOrWhiteSpace(playerSteamId) ||
                string.Equals(providerSteamId, playerSteamId, StringComparison.OrdinalIgnoreCase);

            return isLocalPlayer &&
                currentHealth == 0 &&
                (previousHealth > 0 || lastKnownHealth > 0) &&
                gs.Map.Phase == MapPhase.Live &&
                gs.Round.Phase == RoundPhase.Live;
        }

        private void HandleTenSecondCues(GameState gs)
        {
            PhaseCountdownsPhase cue = GetTenSecondCue(gs);

            if (cue == PhaseCountdownsPhase.Live && !roundTenSecondPlayed)
            {
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.roundTenSecSong, Properties.SelectedKit.roundTenSecSongs), false))
                {
                    roundTenSecondPlayed = true;
                    Console.WriteLine("Ten seconds left in round");
                    Logger.LogEvent("RoundTenSecondCue");
                }
            }
            else if (cue == PhaseCountdownsPhase.Bomb && !bombTenSecondPlayed)
            {
                if (PlaySongIfValid(Properties.SelectedKit.PickSong(Properties.SelectedKit.bombTenSecSong, Properties.SelectedKit.bombTenSecSongs), false))
                {
                    bombTenSecondPlayed = true;
                    Console.WriteLine("Ten seconds left on bomb");
                    Logger.LogEvent("BombTenSecondCue");
                }
            }
        }

        public static PhaseCountdownsPhase GetTenSecondCue(GameState gs)
        {
            PhaseCountdownsNode countdown = gs.PhaseCountdowns;
            float remaining = countdown.PhaseEndsIn;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining <= 0f || remaining > 10f)
                return PhaseCountdownsPhase.Undefined;

            // phase_countdowns is the authoritative GSI clock. Do not gate
            // this cue on musicState: that is playback state and can lag or
            // differ after joining a round already in progress.
            if (countdown.Phase == PhaseCountdownsPhase.Live ||
                (countdown.Phase == PhaseCountdownsPhase.Undefined &&
                 gs.Round.Phase == RoundPhase.Live &&
                 gs.Round.Bomb != BombState.Planted))
            {
                return PhaseCountdownsPhase.Live;
            }

            if (countdown.Phase == PhaseCountdownsPhase.Bomb ||
                (countdown.Phase == PhaseCountdownsPhase.Undefined &&
                 gs.Round.Bomb == BombState.Planted))
            {
                return PhaseCountdownsPhase.Bomb;
            }

            return PhaseCountdownsPhase.Undefined;
        }

        public static bool IsLocalPlayerRoundMvp(GameState gs, int previousMVPs)
        {
            string localSteamId = !string.IsNullOrWhiteSpace(gs.Provider.SteamID) ? gs.Provider.SteamID : gs.Player.SteamID;

            if (!string.IsNullOrWhiteSpace(gs.Round.MVP))
            {
                return !string.IsNullOrWhiteSpace(localSteamId) && string.Equals(gs.Round.MVP, localSteamId, StringComparison.OrdinalIgnoreCase);
            }

            bool isLocalPlayer = string.IsNullOrWhiteSpace(gs.Provider.SteamID) ||
                string.IsNullOrWhiteSpace(gs.Player.SteamID) ||
                string.Equals(gs.Provider.SteamID, gs.Player.SteamID, StringComparison.OrdinalIgnoreCase);

            return isLocalPlayer && gs.Player.MatchStats.MVPs > previousMVPs;
        }

        private void RoundWin(GameState gs)
        {
            Logger.LogEntry($"CurrentMVPs={gs.Player.MatchStats.MVPs}, PreviousMVPs={playerMVPs}");
            bool isMvp = IsLocalPlayerRoundMvp(gs, playerMVPs);

            SongProfile songToPlay;
            string eventName;
            if (isMvp)
            {
                songToPlay = Properties.SelectedKit.PickSong(Properties.SelectedKit.MVPSong, Properties.SelectedKit.MVPSongs);
                eventName = "RoundWinMVP";
            }
            else
            {
                songToPlay = Properties.SelectedKit.PickSong(Properties.SelectedKit.winSong, Properties.SelectedKit.winSongs);
                eventName = "RoundWinRegular";
            }

            if (PlaySongIfValid(songToPlay, false))
            {
                musicState = MusicState.Over;
                Logger.LogEvent("TransitionToRoundOver", $"WinTeam={gs.Round.WinTeam}, PlayerTeam={gs.Player.Team}");
                Logger.LogEvent(eventName, isMvp ? $"RoundMVP={gs.Round.MVP}" : "");
            }

            if (gs.Player.MatchStats.MVPs >= 0)
            {
                bool isLocalPlayer = string.IsNullOrWhiteSpace(gs.Provider.SteamID) ||
                    string.IsNullOrWhiteSpace(gs.Player.SteamID) ||
                    string.Equals(gs.Provider.SteamID, gs.Player.SteamID, StringComparison.OrdinalIgnoreCase);

                if (isLocalPlayer)
                {
                    playerMVPs = gs.Player.MatchStats.MVPs;
                }
            }
            Logger.LogExit();
        }

    }

    public enum MusicState
    {
        None,
        Menu,
        FreezeTime,
        Live,
        BombPlanted,
        Over
    }
}
