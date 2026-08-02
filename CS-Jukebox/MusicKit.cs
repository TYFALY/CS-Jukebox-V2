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

            freezeSongs = new List<SongProfile>();
            startSongs = new List<SongProfile>();
            bombSongs = new List<SongProfile>();
            winSongs = new List<SongProfile>();
            loseSongs = new List<SongProfile>();
            MVPSongs = new List<SongProfile>();
            bombTenSecSongs = new List<SongProfile>();
            roundTenSecSongs = new List<SongProfile>();
            mainMenuSongs = new List<SongProfile>();
        }

        public SongProfile PickSong(SongProfile primarySong, List<SongProfile> extraSongs)
        {
            var songs = new List<SongProfile>();
            if (IsUsable(primarySong)) songs.Add(primarySong);
            if (extraSongs != null) songs.AddRange(extraSongs.Where(IsUsable));

            return songs.Count == 0
                ? primarySong ?? new SongProfile()
                : songs[Random.Shared.Next(songs.Count)];
        }

        private static bool IsUsable(SongProfile song)
        {
            return song != null && !string.IsNullOrWhiteSpace(song.Path);
        }
    }
}
