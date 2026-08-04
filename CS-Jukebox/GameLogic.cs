using System;
using System.Threading;
using System.Windows.Forms;
using CSGSI;
using CSGSI.Nodes;

namespace CS_Jukebox
{
    class GameLogic : IDisposable
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
            uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            jukebox = new Jukebox();
            try
            {
                StartGameListener();
            }
            catch
            {
                jukebox.Dispose();
                throw;
            }
        }

        void StartGameListener()
        {
            gsl = new GameStateListener(3010);
            gsl.NewGameState += new NewGameStateHandler(OnGameStateReceived);

            if (!gsl.Start())
            {
                Console.WriteLine("Game State Listener failed to start.");
                throw new InvalidOperationException(
                    "The Game State Integration listener could not start on port 3010. " +
                    "Close another CS Jukebox instance or any application using this port, then try again.");
            }
            else
            {
                Console.WriteLine("Listening...");
            }
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            Console.WriteLine("Stopping Game Listener");
            if (gsl != null)
            {
                gsl.NewGameState -= new NewGameStateHandler(OnGameStateReceived);
                gsl.Stop();
            }
            jukebox?.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        private void OnGameStateReceived(GameState gs)
        {
            if (stopped) return;
            uiContext.Post(_ =>
            {
                if (!stopped) OnNewGameState(gs);
            }, null);
        }

        void OnNewGameState(GameState gs)
        {
            if (Properties.SelectedKit == null) return;

            int currentMvpCount = gs.Player.MatchStats.MVPs;
            if (currentMvpCount >= 0 && (playerMVPs < 0 || currentMvpCount < playerMVPs))
                playerMVPs = currentMvpCount;

            if (gs.Map.JSON.Equals("{}") && musicState != MusicState.Menu)
            {
                musicState = MusicState.Menu;
                playerMVPs = -1;
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.mainMenuSong, Properties.SelectedKit.mainMenuSongs), true);
                Console.WriteLine("Main Menu");
            }

            if (gs.Round.Phase == RoundPhase.FreezeTime && musicState != MusicState.FreezeTime)
            {
                musicState = MusicState.FreezeTime;
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.freezeSong, Properties.SelectedKit.freezeSongs), false);
                roundTenSecondPlayed = false;
                bombTenSecondPlayed = false;
                Console.WriteLine("FreezeTime Begun");
            }

            if (musicState == MusicState.Menu)
            {
                if (gs.Round.Phase == RoundPhase.Live)
                {
                    //Fade out main menu song
                    Console.WriteLine("Stopping main menu song");
                    jukebox.Stop();
                }
                return;
            }

            if (gs.Round.Phase == RoundPhase.Live && musicState != MusicState.Live && musicState != MusicState.BombPlanted)
            {
                jukebox.Stop();
                musicState = MusicState.Live;
                // RoundPhase.Live is CS2's "round started" signal. Let the
                // selected intro play for five seconds, then fade it out.
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.startSong, Properties.SelectedKit.startSongs), false, 5);
                Console.WriteLine("Round Begun");

            }

            HandlePlayerDeath(gs);

            if (gs.Round.Phase == RoundPhase.Over && musicState != MusicState.Over)
            {
                musicState = MusicState.Over;

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
                    jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.loseSong, Properties.SelectedKit.loseSongs), false);
                }
            }

            if (gs.Round.Bomb == BombState.Planted && musicState != MusicState.BombPlanted)
            {
                musicState = MusicState.BombPlanted;
                bombTenSecondPlayed = false;
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.bombSong, Properties.SelectedKit.bombSongs), false);
                Console.WriteLine("Bomb Planted");
            }

            HandleTenSecondCues(gs);
        }

        private void HandlePlayerDeath(GameState gs)
        {
            int currentHealth = gs.Player.State.Health;
            bool diedNow = IsLocalPlayerDeath(gs, lastKnownPlayerHealth);

            if (currentHealth >= 0)
                lastKnownPlayerHealth = currentHealth;

            if (!diedNow) return;

            Console.WriteLine("Local player died");
            jukebox.PlaySong(
                Properties.SelectedKit.PickSong(Properties.SelectedKit.deathSong, Properties.SelectedKit.deathSongs),
                false,
                5);
        }

        internal static bool IsLocalPlayerDeath(GameState gs, int lastKnownHealth)
        {
            int currentHealth = gs.Player.State.Health;
            int previousHealth = gs.Previously.Player.State.Health;
            string providerSteamId = gs.Provider.SteamID;
            string playerSteamId = gs.Player.SteamID;

            bool isLocalPlayer = string.IsNullOrWhiteSpace(providerSteamId) ||
                string.IsNullOrWhiteSpace(playerSteamId) ||
                string.Equals(providerSteamId, playerSteamId, StringComparison.Ordinal);

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
                roundTenSecondPlayed = true;
                Console.WriteLine("Ten seconds left in round");
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.roundTenSecSong, Properties.SelectedKit.roundTenSecSongs), false);
            }
            else if (cue == PhaseCountdownsPhase.Bomb && !bombTenSecondPlayed)
            {
                bombTenSecondPlayed = true;
                Console.WriteLine("Ten seconds left on bomb");
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.bombTenSecSong, Properties.SelectedKit.bombTenSecSongs), false);
            }
        }

        internal static PhaseCountdownsPhase GetTenSecondCue(GameState gs)
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

        private void RoundWin(GameState gs)
        {
            //Check if player was MVP of the round
            if (gs.Player.MatchStats.MVPs > playerMVPs)
            {
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.MVPSong, Properties.SelectedKit.MVPSongs), false);
                playerMVPs = gs.Player.MatchStats.MVPs;
            }
            else
            {
                jukebox.PlaySong(Properties.SelectedKit.PickSong(Properties.SelectedKit.winSong, Properties.SelectedKit.winSongs), false);
            }
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
