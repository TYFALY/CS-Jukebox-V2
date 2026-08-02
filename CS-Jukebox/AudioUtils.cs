using System;
using System.IO;
using NAudio.Wave;

namespace CS_Jukebox
{
    public static class AudioUtils
    {
        // Analyze peak absolute amplitude of an audio file and return a multiplier
        // to normalize peak to targetPeak (0..1). Caps multiplier to avoid extreme boosts.
        public static float CalculateNormalizationGain(string path, float targetPeak = 0.6f, int maxSecondsToScan = 30)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 1f;

            try
            {
                using var reader = new AudioFileReader(path);

                float max = 0f;
                int sampleRate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                float[] buffer = new float[sampleRate * channels];

                int samplesRead;
                int secondsScanned = 0;
                // Read in 1-second chunks
                do
                {
                    samplesRead = reader.Read(buffer, 0, buffer.Length);
                    if (samplesRead > 0)
                    {
                        for (int n = 0; n < samplesRead; n++)
                        {
                            float abs = Math.Abs(buffer[n]);
                            if (abs > max) max = abs;
                        }
                    }

                    secondsScanned++;
                    if (secondsScanned >= maxSecondsToScan) break;
                }
                while (samplesRead > 0);

                if (max <= 0f) return 1f;

                float multiplier = targetPeak / max;
                // Cap multiplier to a reasonable value to avoid extreme amplification
                if (multiplier > 3.0f) multiplier = 3.0f;
                if (multiplier < 0.1f) multiplier = 0.1f;

                return multiplier;
            }
            catch
            {
                return 1f;
            }
        }
    }
}
