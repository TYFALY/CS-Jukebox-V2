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

        // Attempts to auto-detect the Counter-Strike 2 installation by reading the
        // Steam install path from registry and parsing libraryfolders.vdf. Returns
        // the resolved game "game" directory (containing csgo\cfg and bin\win64\cs2.exe)
        // or null if not found.
        public static string AutoDetectCS2Path()
        {
            try
            {
                // Read Steam install path from registry (64-bit view key for Wow6432Node)
                string steamRoot = null;
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
                    {
                        if (key != null)
                        {
                            var installPath = key.GetValue("InstallPath") as string;
                            if (!string.IsNullOrWhiteSpace(installPath) && Directory.Exists(installPath)) steamRoot = installPath;
                        }
                    }
                }
                catch { steamRoot = null; }

                var candidates = new List<string>();

                if (!string.IsNullOrWhiteSpace(steamRoot))
                {
                    candidates.Add(steamRoot);

                    // Default library file
                    string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        try
                        {
                            string text = File.ReadAllText(vdf);

                            // Find quoted path entries. Handle both legacy and newer formats.
                            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            // pattern: "path" "D:\\\\SteamLibrary"
                            var regexPathKey = new System.Text.RegularExpressions.Regex("\"path\"\\s*\\\"([^\\\"]+)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            foreach (System.Text.RegularExpressions.Match m in regexPathKey.Matches(text))
                            {
                                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                                if (Directory.Exists(p)) paths.Add(p);
                            }

                            // pattern: "1"    "D:\\\\SteamLibrary"
                            var regexNumeric = new System.Text.RegularExpressions.Regex("\"\\d+\"\\s*\\\"([^\\\"]+)\\\"");
                            foreach (System.Text.RegularExpressions.Match m in regexNumeric.Matches(text))
                            {
                                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                                if (Directory.Exists(p)) paths.Add(p);
                            }

                            // fallback: any quoted absolute path like "C:\\Program Files (x86)\\Steam"
                            var regexAny = new System.Text.RegularExpressions.Regex("\"([A-Za-z]:\\\\[^\"]+)\"");
                            foreach (System.Text.RegularExpressions.Match m in regexAny.Matches(text))
                            {
                                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                                if (Directory.Exists(p)) paths.Add(p);
                            }

                            foreach (var p in paths) candidates.Add(p);
                        }
                        catch { /* ignore parsing errors */ }
                    }
                }

                // Also search common installed locations as a last resort
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrWhiteSpace(programFiles) && Directory.Exists(programFiles))
                    candidates.Add(programFiles);

                // For each candidate library, check steamapps/common/Counter-Strike Global Offensive
                foreach (var lib in candidates)
                {
                    try
                    {
                        string common = Path.Combine(lib, "steamapps", "common", "Counter-Strike Global Offensive");
                        if (Directory.Exists(common))
                        {
                            // Try resolving the game directory from the common path
                            string parent = Directory.GetParent(common).Parent?.FullName; // library root (steamapps's parent)
                            // The common folder is steamapps/common/CSGO; the game directory we want is likely the common\Counter-Strike Global Offensive\game folder's parent
                            // Some installs keep game folder inside the CSGO folder; try common\Counter-Strike Global Offensive\game
                            string candidateGame = Path.Combine(common, "game");
                            if (TryResolveGameDirectory(candidateGame, out var resolved) || TryResolveGameDirectory(common, out resolved) || TryResolveGameDirectory(lib, out resolved))
                            {
                                return resolved;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }
    }
}
