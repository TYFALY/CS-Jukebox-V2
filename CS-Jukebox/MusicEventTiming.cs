namespace CS_Jukebox
{
    public enum MusicEventType
    {
        MainMenu,
        FreezeTime,
        RoundStart,
        BombPlanted,
        BombTenSeconds,
        RoundTenSeconds,
        Mvp,
        RoundWon,
        RoundLost,
        PlayerDeath
    }

    /// <summary>
    /// Fixed preview windows documented by the project. These values describe
    /// the length of the event preview after the configured Start At offset.
    /// </summary>
    public static class MusicEventTiming
    {
        public const int FreezeTimeSeconds = 15;
        public const int RoundStartSeconds = 5;
        public const int BombPlantedSeconds = 30;
        public const int TenSecondWarningSeconds = 10;
        public const int RoundResultSeconds = 7;
        public const int PlayerDeathSeconds = 5;

        public static int? GetPreviewDurationSeconds(MusicEventType eventType)
        {
            return eventType switch
            {
                MusicEventType.FreezeTime => FreezeTimeSeconds,
                MusicEventType.RoundStart => RoundStartSeconds,
                MusicEventType.BombPlanted => BombPlantedSeconds,
                MusicEventType.BombTenSeconds => TenSecondWarningSeconds,
                MusicEventType.RoundTenSeconds => TenSecondWarningSeconds,
                MusicEventType.Mvp => RoundResultSeconds,
                MusicEventType.RoundWon => RoundResultSeconds,
                MusicEventType.RoundLost => RoundResultSeconds,
                MusicEventType.PlayerDeath => PlayerDeathSeconds,
                MusicEventType.MainMenu => null,
                _ => null
            };
        }

        public static string GetDisplayName(MusicEventType eventType)
        {
            return eventType switch
            {
                MusicEventType.MainMenu => "Main Menu",
                MusicEventType.FreezeTime => "Freeze Time",
                MusicEventType.RoundStart => "Round Start",
                MusicEventType.BombPlanted => "Bomb Planted",
                MusicEventType.BombTenSeconds => "Bomb: 10 seconds",
                MusicEventType.RoundTenSeconds => "Round: 10 seconds",
                MusicEventType.Mvp => "MVP",
                MusicEventType.RoundWon => "Round Won",
                MusicEventType.RoundLost => "Round Lost",
                MusicEventType.PlayerDeath => "Player Death",
                _ => eventType.ToString()
            };
        }
    }
}
