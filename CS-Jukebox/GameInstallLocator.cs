using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;

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
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string steamRoot in GetSteamRoots())
            {
                libraries.Add(steamRoot);
                string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFile)) continue;

                try
                {
                    string text = File.ReadAllText(libraryFile);
                    foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
                    {
                        AddExistingDirectory(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                    }

                    // Older Steam versions stored library paths under numeric keys.
                    foreach (Match match in Regex.Matches(text, "\"\\d+\"\\s*\"([A-Za-z]:\\\\[^\"]+)\""))
                    {
                        AddExistingDirectory(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            AddExistingDirectory(libraries, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            AddExistingDirectory(libraries, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            AddExistingDirectory(libraries, @"C:\Steam");

            foreach (string library in libraries)
            {
                string installRoot = Path.Combine(library, "steamapps", "common", "Counter-Strike Global Offensive");
                if (TryResolveGameDirectory(installRoot, out string resolved))
                    return resolved;
            }

            return null;
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadSteamRegistryKey(roots, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam");
            ReadSteamRegistryKey(roots, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam");
            ReadSteamRegistryKey(roots, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam");
            return roots;
        }

        private static void ReadSteamRegistryKey(HashSet<string> roots, RegistryHive hive, RegistryView view, string subKey)
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey key = baseKey.OpenSubKey(subKey);
                if (key == null) return;

                foreach (string valueName in new[] { "InstallPath", "SteamPath" })
                {
                    if (key.GetValue(valueName) is string path)
                        AddExistingDirectory(roots, path.Replace('/', Path.DirectorySeparatorChar));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
            catch (IOException) { }
        }

        private static void AddExistingDirectory(HashSet<string> paths, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                paths.Add(Path.GetFullPath(path));
        }
    }
}
