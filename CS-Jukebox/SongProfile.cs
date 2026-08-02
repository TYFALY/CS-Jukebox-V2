using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_Jukebox
{
    //Contains reference to the path of a song and saves volume
    public class SongProfile
    {
        public string Path;
        public int Volume;
        public int Start;
        // Normalization gain factor (multiplier) computed from audio analysis. If negative, not computed yet.
        public float NormalizationGain;

        public SongProfile()
        {
            Path = "";
            Volume = 100;
            NormalizationGain = -1f; // indicates not yet analyzed
        }

        public SongProfile(string path, int volume)
        {
            Path = path;
            Volume = volume;
            NormalizationGain = -1f;
        }
    }
}
