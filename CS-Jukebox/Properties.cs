using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace CS_Jukebox
{
    static class Properties
    {
        //Paths
        public static readonly string ConfigPath = @"csgo\cfg\gamestate_integration_jukebox.cfg";
        public static readonly string ConfigName = "gamestate_integration_jukebox.cfg";
        public static readonly string PropertiesFilePath = "properties.json";
        public static readonly string MusicKitsPath = "kits";

        public static string GameDir = null;
        public static int MasterVolume = 70;
        public static bool LimitPreviewToEventDuration = false;
        public static bool DarkTheme = false;
        public static MusicKit SelectedKit
        {
            get { return selectedKit; }
            set { SetKit(value); }
        }

        public static List<MusicKit> MusicKits = null;

        private static string startDir;
        private static bool dataDirectoryWritable = true;
        private const string ConfigResourceName = "CS_Jukebox.gamestate_integration_jukebox.cfg";

        public static string KitsDirectory => Path.Combine(startDir ?? GetAppDirectory(), MusicKitsPath);

        private static MusicKit selectedKit = null;
        private static string SelectedKitName = null;

        //Calls all load methods
        public static void Load()
        {
            LoadProperties();
            LoadKits();
        }

        //Calls all save methods
        public static void Save()
        {
            SaveProperties();
            SaveKits();
        }

        //Converts settings to json file then saves it
        public static void SaveProperties()
        {
            if (!dataDirectoryWritable) return;

            string dir = Path.Combine(startDir, PropertiesFilePath);

            PropertiesFile propFile = new PropertiesFile();
            propFile.GameDir = GameDir;
            propFile.SelectedKitName = SelectedKitName;
            propFile.MasterVolume = MasterVolume;
            propFile.LimitPreviewToEventDuration = LimitPreviewToEventDuration;
            propFile.DarkTheme = DarkTheme;

            string jsonFile = JsonConvert.SerializeObject(propFile);
            try
            {
                WriteAllTextAtomically(dir, jsonFile);
            }
            catch (Exception e)
            {
                ReportError("Settings could not be saved", e.Message);
            }
        }

        //Reads properties file then deserializes it
        public static void LoadProperties()
        {
            startDir = GetDataDirectory(GetAppDirectory());
            Console.WriteLine("App Directory: " + startDir);
            string dir = Path.Combine(startDir, PropertiesFilePath);

            try
            {
                string jsonFile = File.ReadAllText(dir);
                PropertiesFile propFile = JsonConvert.DeserializeObject<PropertiesFile>(jsonFile);
                if (propFile == null) throw new JsonSerializationException("Properties file is empty.");
                GameDir = propFile.GameDir;
                SelectedKitName = propFile.SelectedKitName;
                MasterVolume = Math.Clamp(propFile.MasterVolume, 0, 100);
                LimitPreviewToEventDuration = propFile.LimitPreviewToEventDuration;
                DarkTheme = propFile.DarkTheme;
            }
            catch (FileNotFoundException)
            {
                MasterVolume = 70;
                LimitPreviewToEventDuration = false;
                DarkTheme = false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                ReportError("Settings could not be loaded", "Defaults will be used.\n\n" + e.Message);
                GameDir = null;
                SelectedKitName = null;
                MasterVolume = 70;
                LimitPreviewToEventDuration = false;
                DarkTheme = false;
            }
        }

        private static string GetAppDirectory()
        {
            return AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string GetDataDirectory(string applicationDirectory)
        {
            dataDirectoryWritable = CanWriteToDirectory(applicationDirectory);
            if (!dataDirectoryWritable)
            {
                ReportError(
                    "Application folder is read-only",
                    "CS Jukebox stores properties.json and the kits folder beside CS-Jukebox.exe.\n\n" +
                    "Move CS-Jukebox.exe to a writable folder, such as Documents, then restart the application.");
            }
            else
            {
                try
                {
                    ImportLegacyUserData(applicationDirectory);
                }
                catch (Exception e)
                {
                    ReportError(
                        "Existing settings could not be imported",
                        "CS Jukebox will continue with portable storage beside the executable.\n\n" + e.Message);
                }
            }

            return applicationDirectory;
        }

        private static bool CanWriteToDirectory(string directory)
        {
            string probePath = Path.Combine(directory, ".cs-jukebox-write-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (FileStream stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.WriteByte(0);
                File.Delete(probePath);
                return true;
            }
            catch
            {
                try { File.Delete(probePath); } catch { }
                return false;
            }
        }

        private static void ImportLegacyUserData(string destinationDirectory)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData)) return;

            string legacyDirectory = Path.Combine(localAppData, "CS-Jukebox");
            if (!Directory.Exists(legacyDirectory) ||
                string.Equals(
                    Path.GetFullPath(legacyDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string legacyProperties = Path.Combine(legacyDirectory, PropertiesFilePath);
            string portableProperties = Path.Combine(destinationDirectory, PropertiesFilePath);
            string portableKitsDirectory = Path.Combine(destinationDirectory, MusicKitsPath);

            // Import only into a completely new portable data directory. This
            // prevents deleted kits from reappearing from legacy storage later.
            if (File.Exists(portableProperties) || Directory.Exists(portableKitsDirectory)) return;

            if (File.Exists(legacyProperties) && !File.Exists(portableProperties))
                File.Copy(legacyProperties, portableProperties);

            string legacyKitsDirectory = Path.Combine(legacyDirectory, MusicKitsPath);
            if (!Directory.Exists(legacyKitsDirectory)) return;

            foreach (string legacyKit in Directory.GetFiles(legacyKitsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                Directory.CreateDirectory(portableKitsDirectory);
                string portableKit = Path.Combine(portableKitsDirectory, Path.GetFileName(legacyKit));
                if (!File.Exists(portableKit)) File.Copy(legacyKit, portableKit);
            }
        }

        // Copies the config to the exact CS2 GSI directory.
        public static void CreateConfig()
        {
            if (string.IsNullOrWhiteSpace(Properties.GameDir)) return;

            string configFileName = Properties.ConfigName;
            string cfgDir = GameInstallLocator.GetConfigDirectory(Properties.GameDir);
            string configPath = Path.Combine(cfgDir, configFileName);

            try
            {
                if (!Directory.Exists(cfgDir))
                {
                    ReportError("CS2 configuration directory was not found", cfgDir);
                    return;
                }

                using Stream configStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ConfigResourceName);
                if (configStream == null)
                    throw new InvalidOperationException("The embedded GSI configuration resource is missing.");

                WriteStreamAtomically(configPath, configStream);
            }
            catch (Exception e)
            {
                ReportError("GSI configuration could not be installed", e.Message);
            }
        }

        public static void SaveKits()
        {
            if (!dataDirectoryWritable) return;

            string dir = KitsDirectory;
            var saveErrors = new List<string>();
            try
            {
                Directory.CreateDirectory(dir);

                foreach (MusicKit musicKit in (MusicKits ?? new List<MusicKit>()).Where(kit => kit != null))
                {
                    if (!TryValidateKitName(musicKit.Name, musicKit, out _)) continue;
                    try
                    {
                        string kitDir = Path.Combine(dir, musicKit.Name + ".json");
                        string jsonFile = JsonConvert.SerializeObject(musicKit);
                        WriteAllTextAtomically(kitDir, jsonFile);
                    }
                    catch (Exception e)
                    {
                        saveErrors.Add(musicKit.Name + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                saveErrors.Add(e.Message);
            }

            if (saveErrors.Count > 0)
                ReportError("Some music kits could not be saved", string.Join("\n", saveErrors));
        }

        public static void LoadKits()
        {
            string dir = KitsDirectory;
            MusicKits = new List<MusicKit>();
            if (!dataDirectoryWritable) return;

            string[] kitFiles;

            try
            {
                Directory.CreateDirectory(dir);
                kitFiles = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch (Exception e)
            {
                ReportError("Music kits could not be loaded", e.Message);
                kitFiles = Array.Empty<string>();
            }

            var loadErrors = new List<string>();
            foreach (string filePath in kitFiles)
            {
                try
                {
                    string jsonFile = File.ReadAllText(filePath);
                    MusicKit musicKit = JsonConvert.DeserializeObject<MusicKit>(jsonFile);
                    if (musicKit == null) throw new JsonSerializationException("Kit file is empty.");
                    musicKit.EnsureInitialized();
                    if (!TryValidateKitName(musicKit.Name, null, out string validationError))
                        throw new InvalidDataException(validationError);
                    MusicKits.Add(musicKit);
                }
                catch (Exception e)
                {
                    loadErrors.Add(Path.GetFileName(filePath) + ": " + e.Message);
                }
            }

            if (loadErrors.Count > 0)
                ReportError("Some music kits were skipped", string.Join("\n", loadErrors));

            //Find a value for SelectedKit if applicable
            if (MusicKits.Count > 0)
            {
                foreach (MusicKit musicKit in MusicKits)
                {
                    if (string.Equals(musicKit.Name, SelectedKitName, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedKit = musicKit;
                    }
                }

                if (SelectedKit == null)
                {
                    SelectedKit = MusicKits[0];
                }
            }
        }

        //Deletes the json file for a kit but not the kit itself
        public static void DeleteKitFile(string kitName)
        {
            if (!dataDirectoryWritable) return;
            if (!TryValidateKitFileName(kitName, out _)) return;
            string dir = KitsDirectory;
            string kitDir = Path.Combine(dir, kitName + ".json");
            try { File.Delete(kitDir); }
            catch (Exception e) { Console.WriteLine("Failed to delete kit file: " + e.Message); }
        }

        private static void SetKit(MusicKit newKit)
        {
            selectedKit = newKit;
            SelectedKitName = selectedKit?.Name;
        }

        public static bool TryValidateKitName(string name, MusicKit currentKit, out string error)
        {
            if (!TryValidateKitFileName(name, out error)) return false;

            if (MusicKits != null && MusicKits.Any(kit => kit != null && !ReferenceEquals(kit, currentKit) &&
                string.Equals(kit.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A music kit with this name already exists.";
                return false;
            }

            return true;
        }

        public static bool TryValidateKitFileName(string name, out string error)
        {
            error = null;
            string trimmedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                error = "Please enter a name.";
                return false;
            }

            if (!string.Equals(name, trimmedName, StringComparison.Ordinal))
            {
                error = "The kit name cannot begin or end with spaces.";
                return false;
            }

            name = trimmedName;
            if (name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar))
            {
                error = "The kit name contains characters that cannot be used in a file name.";
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(name);
            string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (reservedNames.Contains(baseName, StringComparer.OrdinalIgnoreCase) || name.EndsWith("."))
            {
                error = "This name is reserved by Windows or ends with an unsupported character.";
                return false;
            }

            return true;
        }

        private static void ReportError(string title, string message)
        {
            Console.WriteLine(title + ": " + message);
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void WriteAllTextAtomically(string path, string contents)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory,
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(temporaryPath, contents);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }

        private static void WriteStreamAtomically(string path, Stream source)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory,
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    source.CopyTo(destination);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                try { File.Delete(temporaryPath); } catch { }
            }
        }

        //Inner class for properties parameters
        private class PropertiesFile
        {
            public string GameDir;
            public string SelectedKitName;
            public int MasterVolume;
            public bool LimitPreviewToEventDuration;
            public bool DarkTheme;
        }
    }
}
