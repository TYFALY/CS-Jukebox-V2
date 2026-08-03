using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

namespace CS_Jukebox
{
    public partial class MainForm : Form
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private GameLogic logic;

        public MainForm()
        {
            InitializeComponent();
            trackBar1.ValueChanged += trackBar1_ValueChanged;
            //AllocConsole(); //Enable console
            MaximizeBox = false;

            Properties.Load();
            
            //If game directory is not set, create
            //popup so that user can browse to it.
            if (Properties.GameDir == null)
            {
                Form dirPopup = new GamePathForm();
                dirPopup.Location = this.Location;
                dirPopup.ShowDialog(this);
            }

            CheckAutoStart();
            Start();
        }

        void Start()
        {
            RefreshParameters();
            SetupGameListener();
        }

        void SetupGameListener()
        {
            Properties.CreateConfig();
            logic = new GameLogic();
        }

        //Refreshes controls that contain mutable data
        void RefreshParameters()
        {
            CreateKitDropdown();

            trackBar1.Value = Properties.MasterVolume;
        }

        private void CreateKitDropdown()
        {
            musicComboBox.Items.Clear();

            foreach (MusicKit musicKit in Properties.MusicKits)
            {
                musicComboBox.Items.Add(musicKit.Name);
            }

            if (Properties.SelectedKit != null)
                musicComboBox.SelectedIndex = Properties.MusicKits.IndexOf(Properties.SelectedKit);
        }

        private void CheckAutoStart()
        {
            try
            {
                using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                autoCheckBox.Checked = registryKey?.GetValue("CS-Jukebox") != null;
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to read autostart settings: " + e.Message);
                autoCheckBox.Checked = false;
            }
        }

        private void RegisterInStartup(bool isChecked)
        {
            try
            {
                using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(
                    "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                if (registryKey == null) return;

                if (isChecked)
                    registryKey.SetValue("CS-Jukebox", "\"" + Application.ExecutablePath + "\"");
                else
                    registryKey.DeleteValue("CS-Jukebox", false);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to update autostart settings: " + e.Message);
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            SetMasterVolume();
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            SetMasterVolume();
        }

        private void SetMasterVolume()
        {
            Properties.MasterVolume = trackBar1.Value;
            logic?.jukebox.UpdateVolume();
        }

        // Export a MusicKit and its referenced audio files into a single ZIP archive.
        // outputZipPath: full path to the .zip file to create (will be overwritten if exists)
        public void ExportMusicKit(MusicKit kit, string outputZipPath)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (string.IsNullOrWhiteSpace(outputZipPath)) throw new ArgumentNullException(nameof(outputZipPath));

            string tempDir = Path.Combine(Path.GetTempPath(), "CSJukeboxExport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string audioDir = Path.Combine(tempDir, "audio");
            Directory.CreateDirectory(audioDir);

            try
            {
                // Gather all song profiles referenced by the kit
                var profiles = GetAllSongProfiles(kit).Where(p => p != null && !string.IsNullOrWhiteSpace(p.Path)).ToList();

                // Map original absolute paths to unique filenames inside archive
                var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in profiles)
                {
                    try
                    {
                        if (!File.Exists(p.Path)) continue;

                        string original = p.Path;
                        string fileName = Path.GetFileName(original);
                        string uniqueName = fileName;
                        int counter = 1;
                        while (File.Exists(Path.Combine(audioDir, uniqueName)))
                        {
                            uniqueName = Path.GetFileNameWithoutExtension(fileName) + $"({counter})" + Path.GetExtension(fileName);
                            counter++;
                        }

                        File.Copy(original, Path.Combine(audioDir, uniqueName));
                        nameMap[original] = uniqueName;
                    }
                    catch
                    {
                        // ignore individual file copy errors
                    }
                }

                // Create a copy of the kit where paths point to audio/<filename>
                var kitCopy = JsonConvert.DeserializeObject<MusicKit>(JsonConvert.SerializeObject(kit));
                foreach (var p in GetAllSongProfiles(kitCopy))
                {
                    if (p == null || string.IsNullOrWhiteSpace(p.Path)) continue;
                    if (nameMap.TryGetValue(p.Path, out var mappedName))
                    {
                        p.Path = Path.Combine("audio", mappedName).Replace('\\', '/');
                    }
                    else
                    {
                        // If we didn't include the file (missing), clear the path to avoid broken references
                        p.Path = "";
                    }
                }

                // Write kit json
                string kitJsonPath = Path.Combine(tempDir, "kit.json");
                File.WriteAllText(kitJsonPath, JsonConvert.SerializeObject(kitCopy, Formatting.Indented));

                // Create zip from tempDir
                if (File.Exists(outputZipPath)) File.Delete(outputZipPath);
                ZipFile.CreateFromDirectory(tempDir, outputZipPath, CompressionLevel.Optimal, false);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        // Import a MusicKit archive (created by ExportMusicKit). Extracts files into local kits folder
        // and registers the kit into Properties.MusicKits automatically.
        public void ImportMusicKit(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) throw new FileNotFoundException("Zip file not found", zipPath);

            string tempDir = Path.Combine(Path.GetTempPath(), "CSJukeboxImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                // Find kit json
                string kitJson = Directory.GetFiles(tempDir, "kit.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                                 ?? Directory.GetFiles(tempDir, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();

                if (kitJson == null) throw new InvalidDataException("No kit JSON found in archive.");

                var importedKit = JsonConvert.DeserializeObject<MusicKit>(File.ReadAllText(kitJson));
                if (importedKit == null) throw new InvalidDataException("Failed to deserialize kit JSON.");
                importedKit.EnsureInitialized();

                // Determine app kits directory
                string kitsDir = Properties.KitsDirectory;
                Directory.CreateDirectory(kitsDir);

                // Ensure unique kit name if collision
                if (Properties.MusicKits == null) Properties.MusicKits = new List<MusicKit>();

                string baseName = ToSafeKitName(importedKit.Name);
                if (!Properties.TryValidateKitName(baseName, null, out _))
                    baseName = "ImportedKit";
                string finalName = baseName;
                int suffix = 1;
                while (Properties.MusicKits.Any(k => string.Equals(k.Name, finalName, StringComparison.OrdinalIgnoreCase)))
                {
                    finalName = baseName + "_" + suffix++; 
                }
                importedKit.Name = finalName;

                // Create folder for audio files
                string audioDest = Path.Combine(kitsDir, finalName + "_files");
                Directory.CreateDirectory(audioDest);

                // Move/copy audio files referenced in JSON into audioDest and update paths
                foreach (var p in GetAllSongProfiles(importedKit))
                {
                    if (p == null || string.IsNullOrWhiteSpace(p.Path)) continue;

                    if (TryResolveArchiveAudioPath(tempDir, p.Path, out string srcPath) && File.Exists(srcPath))
                    {
                        string destName = Path.GetFileName(srcPath);
                        string destPath = Path.Combine(audioDest, destName);
                        int cnt = 1;
                        while (File.Exists(destPath))
                        {
                            destName = Path.GetFileNameWithoutExtension(srcPath) + $"({cnt})" + Path.GetExtension(srcPath);
                            destPath = Path.Combine(audioDest, destName);
                            cnt++;
                        }
                        File.Copy(srcPath, destPath);
                        p.Path = destPath;
                    }
                    else
                    {
                        // Missing file: clear path
                        p.Path = "";
                    }
                }

                // Save kit JSON in kits directory
                string kitFilePath = Path.Combine(kitsDir, importedKit.Name + ".json");
                File.WriteAllText(kitFilePath, JsonConvert.SerializeObject(importedKit, Formatting.Indented));

                // Register in memory
                Properties.MusicKits.Add(importedKit);
                Properties.SelectedKit = importedKit;

                // Persist kits
                Properties.SaveKits();
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        // Helper to enumerate all SongProfile objects in a MusicKit (primaries and extras)
        private IEnumerable<SongProfile> GetAllSongProfiles(MusicKit kit)
        {
            if (kit == null) yield break;

            yield return kit.freezeSong;
            yield return kit.startSong;
            yield return kit.bombSong;
            yield return kit.winSong;
            yield return kit.loseSong;
            yield return kit.MVPSong;
            yield return kit.bombTenSecSong;
            yield return kit.roundTenSecSong;
            yield return kit.mainMenuSong;
            yield return kit.deathSong;

            if (kit.freezeSongs != null) foreach (var s in kit.freezeSongs) yield return s;
            if (kit.startSongs != null) foreach (var s in kit.startSongs) yield return s;
            if (kit.bombSongs != null) foreach (var s in kit.bombSongs) yield return s;
            if (kit.winSongs != null) foreach (var s in kit.winSongs) yield return s;
            if (kit.loseSongs != null) foreach (var s in kit.loseSongs) yield return s;
            if (kit.MVPSongs != null) foreach (var s in kit.MVPSongs) yield return s;
            if (kit.bombTenSecSongs != null) foreach (var s in kit.bombTenSecSongs) yield return s;
            if (kit.roundTenSecSongs != null) foreach (var s in kit.roundTenSecSongs) yield return s;
            if (kit.mainMenuSongs != null) foreach (var s in kit.mainMenuSongs) yield return s;
            if (kit.deathSongs != null) foreach (var s in kit.deathSongs) yield return s;
        }

        private static string ToSafeKitName(string name)
        {
            string safeName = Path.GetFileName(name?.Trim() ?? "");
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            safeName = safeName.Trim().Trim('.');
            return string.IsNullOrWhiteSpace(safeName) ? "ImportedKit" : safeName;
        }

        private static bool TryResolveArchiveAudioPath(string archiveRoot, string storedPath, out string sourcePath)
        {
            sourcePath = null;
            if (string.IsNullOrWhiteSpace(storedPath) || Path.IsPathRooted(storedPath)) return false;

            string relative = storedPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            string audioRoot = Path.GetFullPath(Path.Combine(archiveRoot, "audio")) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(archiveRoot, relative));
            if (!candidate.StartsWith(audioRoot, StringComparison.OrdinalIgnoreCase)) return false;

            sourcePath = candidate;
            return true;
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            Form musicSelector = new MusicSelector(new MusicKit(""), true);
            musicSelector.Location = this.Location;
            musicSelector.ShowDialog(this);
            RefreshParameters();
        }

        private void editButton_Click(object sender, EventArgs e)
        {
            if (Properties.SelectedKit != null)
            {
                Form musicSelector = new MusicSelector(Properties.SelectedKit, false);
                musicSelector.Location = this.Location;
                musicSelector.ShowDialog(this);
                RefreshParameters();
            }
        }

        private void musicComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = musicComboBox.SelectedIndex;
            if (index >= 0 && index < Properties.MusicKits.Count)
                Properties.SelectedKit = Properties.MusicKits[index];
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            logic?.Dispose();
            Properties.Save();
        }

        private void directoryButton_Click(object sender, EventArgs e)
        {
            Form dirPopup = new GamePathForm();
            dirPopup.Location = this.Location;
            dirPopup.ShowDialog(this);
            Properties.CreateConfig();
            RefreshParameters();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void autoCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            RegisterInStartup(autoCheckBox.Checked);
        }
    }
}
