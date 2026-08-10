using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_Jukebox
{
    //Contains references to SongProfiles
    public class MusicKit
    {
        public string Name;

        public SongProfile freezeSong { get; set; }
        public SongProfile startSong { get; set; }
        public SongProfile bombSong { get; set; }
        public SongProfile winSong { get; set; }
        public SongProfile loseSong { get; set; }
        public SongProfile MVPSong { get; set; }
        public SongProfile bombTenSecSong { get; set; }
        public SongProfile roundTenSecSong { get; set; }
        public SongProfile mainMenuSong { get; set; }
        public SongProfile deathSong { get; set; }

        // Optional extra tracks. The singular properties above remain the
        // primary tracks, keeping existing kit JSON files compatible.
        public List<SongProfile> freezeSongs { get; set; }
        public List<SongProfile> startSongs { get; set; }
        public List<SongProfile> bombSongs { get; set; }
        public List<SongProfile> winSongs { get; set; }
        public List<SongProfile> loseSongs { get; set; }
        public List<SongProfile> MVPSongs { get; set; }
        public List<SongProfile> bombTenSecSongs { get; set; }
        public List<SongProfile> roundTenSecSongs { get; set; }
        public List<SongProfile> mainMenuSongs { get; set; }
        public List<SongProfile> deathSongs { get; set; }

        public MusicKit(string name)
        {
            Name = name;

            freezeSong = new SongProfile();
            startSong = new SongProfile();
            bombSong = new SongProfile();
            winSong = new SongProfile();
            loseSong = new SongProfile();
            MVPSong = new SongProfile();
            bombTenSecSong = new SongProfile();
            roundTenSecSong = new SongProfile();
            mainMenuSong = new SongProfile();
            deathSong = new SongProfile();

            freezeSongs = new List<SongProfile>();
            startSongs = new List<SongProfile>();
            bombSongs = new List<SongProfile>();
            winSongs = new List<SongProfile>();
            loseSongs = new List<SongProfile>();
            MVPSongs = new List<SongProfile>();
            bombTenSecSongs = new List<SongProfile>();
            roundTenSecSongs = new List<SongProfile>();
            mainMenuSongs = new List<SongProfile>();
            deathSongs = new List<SongProfile>();
        }

        public void EnsureInitialized()
        {
            Logger.LogEntry($"KitName={Name}");
            freezeSong ??= new SongProfile();
            startSong ??= new SongProfile();
            bombSong ??= new SongProfile();
            winSong ??= new SongProfile();
            loseSong ??= new SongProfile();
            MVPSong ??= new SongProfile();
            bombTenSecSong ??= new SongProfile();
            roundTenSecSong ??= new SongProfile();
            mainMenuSong ??= new SongProfile();
            deathSong ??= new SongProfile();

            freezeSongs ??= new List<SongProfile>();
            startSongs ??= new List<SongProfile>();
            bombSongs ??= new List<SongProfile>();
            winSongs ??= new List<SongProfile>();
            loseSongs ??= new List<SongProfile>();
            MVPSongs ??= new List<SongProfile>();
            bombTenSecSongs ??= new List<SongProfile>();
            roundTenSecSongs ??= new List<SongProfile>();
            mainMenuSongs ??= new List<SongProfile>();
            deathSongs ??= new List<SongProfile>();

            foreach (SongProfile song in GetAllSongs())
                song?.EnsureValid();
            Logger.LogExit();
        }

        public MusicKit DeepClone()
        {
            Logger.LogEntry($"KitName={Name}");
            EnsureInitialized();
            var clone = new MusicKit(Name)
            {
                freezeSong = freezeSong.Clone(),
                startSong = startSong.Clone(),
                bombSong = bombSong.Clone(),
                winSong = winSong.Clone(),
                loseSong = loseSong.Clone(),
                MVPSong = MVPSong.Clone(),
                bombTenSecSong = bombTenSecSong.Clone(),
                roundTenSecSong = roundTenSecSong.Clone(),
                mainMenuSong = mainMenuSong.Clone(),
                deathSong = deathSong.Clone(),
                freezeSongs = CloneSongs(freezeSongs),
                startSongs = CloneSongs(startSongs),
                bombSongs = CloneSongs(bombSongs),
                winSongs = CloneSongs(winSongs),
                loseSongs = CloneSongs(loseSongs),
                MVPSongs = CloneSongs(MVPSongs),
                bombTenSecSongs = CloneSongs(bombTenSecSongs),
                roundTenSecSongs = CloneSongs(roundTenSecSongs),
                mainMenuSongs = CloneSongs(mainMenuSongs),
                deathSongs = CloneSongs(deathSongs)
            };
            Logger.LogExit($"Clone created for kit {Name}");
            return clone;
        }

        private IEnumerable<SongProfile> GetAllSongs()
        {
            yield return freezeSong;
            yield return startSong;
            yield return bombSong;
            yield return winSong;
            yield return loseSong;
            yield return MVPSong;
            yield return bombTenSecSong;
            yield return roundTenSecSong;
            yield return mainMenuSong;
            yield return deathSong;

            foreach (SongProfile song in freezeSongs.Concat(startSongs).Concat(bombSongs).Concat(winSongs)
                .Concat(loseSongs).Concat(MVPSongs).Concat(bombTenSecSongs).Concat(roundTenSecSongs)
                .Concat(mainMenuSongs).Concat(deathSongs))
            {
                yield return song;
            }
        }

        private static List<SongProfile> CloneSongs(IEnumerable<SongProfile> songs)
        {
            return songs?.Where(song => song != null).Select(song => song.Clone()).ToList()
                ?? new List<SongProfile>();
        }

        public SongProfile PickSong(SongProfile primarySong, List<SongProfile> extraSongs)
        {
            Logger.LogEntry($"Primary={primarySong?.Path}, ExtraCount={extraSongs?.Count ?? 0}");
            var songs = new List<SongProfile>();
            if (IsUsable(primarySong)) songs.Add(primarySong);
            if (extraSongs != null) songs.AddRange(extraSongs.Where(IsUsable));

            SongProfile chosen = songs.Count == 0
                ? primarySong ?? new SongProfile()
                : songs[Random.Shared.Next(songs.Count)];

            Logger.LogExit($"ChosenPath={chosen?.Path}, AvailableCount={songs.Count}");
            return chosen;
        }

        private static bool IsUsable(SongProfile song)
        {
            return song != null && !string.IsNullOrWhiteSpace(song.Path);
        }
    }
}
