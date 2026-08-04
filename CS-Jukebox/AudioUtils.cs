using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace CS_Jukebox
{
    public static class AudioUtils
    {
        private static readonly ConcurrentDictionary<NormalizationCacheKey, Lazy<Task<float>>> NormalizationCache = new();
        private static readonly SemaphoreSlim NormalizationSlot = new(1, 1);

        public static Task<float> GetNormalizationGainAsync(string path)
        {
            if (!TryCreateCacheKey(path, out NormalizationCacheKey key))
                return Task.FromResult(1f);

            foreach (NormalizationCacheKey cachedKey in NormalizationCache.Keys)
            {
                if (!cachedKey.Equals(key) && string.Equals(cachedKey.Path, key.Path, StringComparison.OrdinalIgnoreCase))
                    NormalizationCache.TryRemove(cachedKey, out _);
            }

            Lazy<Task<float>> calculation = NormalizationCache.GetOrAdd(key, cacheKey =>
                new Lazy<Task<float>>(
                    () => CalculateNormalizationGainThrottledAsync(cacheKey.Path),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            return calculation.Value;
        }

        private static async Task<float> CalculateNormalizationGainThrottledAsync(string path)
        {
            await NormalizationSlot.WaitAsync().ConfigureAwait(false);
            try
            {
                return await Task.Run(() => CalculateNormalizationGain(path)).ConfigureAwait(false);
            }
            finally
            {
                NormalizationSlot.Release();
            }
        }

        public static bool TryGetCachedNormalizationGain(string path, out float gain)
        {
            gain = 1f;
            if (!TryCreateCacheKey(path, out NormalizationCacheKey key) ||
                !NormalizationCache.TryGetValue(key, out Lazy<Task<float>> calculation) ||
                !calculation.IsValueCreated || !calculation.Value.IsCompletedSuccessfully)
            {
                return false;
            }

            gain = calculation.Value.Result;
            return true;
        }

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

        private static bool TryCreateCacheKey(string path, out NormalizationCacheKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                var file = new FileInfo(path);
                if (!file.Exists) return false;
                key = new NormalizationCacheKey(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private readonly record struct NormalizationCacheKey(string Path, long Length, long LastWriteTimeTicks);
    }
}
