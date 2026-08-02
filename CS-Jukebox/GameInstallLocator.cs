using System;
using System.Collections.Generic;
using System.IO;

namespace CS_Jukebox
{
    /// <summary>Finds the canonical CS2 <c>game</c> directory from a user selection.</summary>
    public static class GameInstallLocator
    {
        public static bool TryResolveGameDirectory(string selectedPath, out string gameDirectory)
        {
            gameDirectory = null;
            if (string.IsNullOrWhiteSpace(selectedPath)) return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(selectedPath);
            }
            catch (Exception)
            {
                return false;
            }

            var candidates = new List<string>
            {
                fullPath,
                Path.Combine(fullPath, "game")
            };

            // Also allow choosing the csgo, bin, or win64 folder by mistake.
            var parent = Directory.GetParent(fullPath);
            for (var level = 0; parent != null && level < 2; level++, parent = parent.Parent)
                candidates.Add(parent.FullName);

            var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (!checkedDirectories.Add(candidate)) continue;

                string csgoDirectory = Path.Combine(candidate, "csgo");
                string cfgDirectory = Path.Combine(csgoDirectory, "cfg");
                string executable = Path.Combine(candidate, "bin", "win64", "cs2.exe");

                // cfg is the directory required by this application. cs2.exe
                // proves this is a current CS2 install rather than an unrelated
                // folder that happens to be named "csgo".
                if (Directory.Exists(cfgDirectory) && File.Exists(executable))
                {
                    gameDirectory = candidate;
                    return true;
                }
            }

            return false;
        }

        public static string GetConfigDirectory(string gameDirectory)
        {
            return Path.Combine(gameDirectory, "csgo", "cfg");
        }
    }
}
