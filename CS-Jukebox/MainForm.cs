using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;

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

            // Use default system theme (no custom theming applied)

            Properties.Load();

            // If GameDir is missing or invalid, attempt auto-detection of CS2 install.
            bool gameDirValid = false;
            if (!string.IsNullOrWhiteSpace(Properties.GameDir))
            {
                gameDirValid = GameInstallLocator.TryResolveGameDirectory(Properties.GameDir, out string resolved) && !string.IsNullOrWhiteSpace(resolved);
                if (gameDirValid)
                {
                    // normalize to resolved path
                    Properties.GameDir = resolved;
                }
            }

            if (!gameDirValid)
            {
                try
                {
                    string detected = GameInstallLocator.AutoDetectCS2Path();
                    if (!string.IsNullOrWhiteSpace(detected) && GameInstallLocator.TryResolveGameDirectory(detected, out string resolvedDetected))
                    {
                        Properties.GameDir = resolvedDetected;
                        Properties.SaveProperties();
                        // create config silently
                        try { Properties.CreateConfig(); } catch { }
                        gameDirValid = true;
                    }
                }
                catch { }
            }

            //If still not valid, prompt user to select it
            if (!gameDirValid)
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
        public static void ExportMusicKit(MusicKit kit, string outputZipPath)
        {
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (string.IsNullOrWhiteSpace(outputZipPath)) throw new ArgumentNullException(nameof(outputZipPath));

            string fullOutputPath = Path.GetFullPath(outputZipPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException("The export directory is invalid.");

            Directory.CreateDirectory(outputDirectory);
            string temporaryZipPath = Path.Combine(outputDirectory,
                "." + Path.GetFileName(fullOutputPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var usedEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (FileStream output = new FileStream(temporaryZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
                {
                    foreach (SongProfile profile in GetAllSongProfiles(kit))
                    {
                        if (profile == null || string.IsNullOrWhiteSpace(profile.Path) ||
                            nameMap.ContainsKey(profile.Path) || !File.Exists(profile.Path))
                        {
                            continue;
                        }

                        string fileName = Path.GetFileName(profile.Path);
                        string uniqueName = fileName;
                        int counter = 1;
                        while (!usedEntryNames.Add(uniqueName))
                        {
                            uniqueName = Path.GetFileNameWithoutExtension(fileName) + $"({counter})" + Path.GetExtension(fileName);
                            counter++;
                        }

                        ZipArchiveEntry audioEntry = archive.CreateEntry("audio/" + uniqueName,
                            GetAudioCompressionLevel(Path.GetExtension(fileName)));
                        try
                        {
                            using Stream source = new FileStream(profile.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            using Stream destination = audioEntry.Open();
                            source.CopyTo(destination);
                            nameMap[profile.Path] = uniqueName;
                        }
                        catch
                        {
                            try { audioEntry.Delete(); } catch { }
                            usedEntryNames.Remove(uniqueName);
                        }
                    }

                    MusicKit kitCopy = kit.DeepClone();
                    foreach (SongProfile profile in GetAllSongProfiles(kitCopy))
                    {
                        if (profile == null || string.IsNullOrWhiteSpace(profile.Path)) continue;
                        profile.Path = nameMap.TryGetValue(profile.Path, out string mappedName)
                            ? "audio/" + mappedName
                            : "";
                    }

                    ZipArchiveEntry jsonEntry = archive.CreateEntry("kit.json", CompressionLevel.Optimal);
                    using Stream jsonStream = jsonEntry.Open();
                    using var writer = new StreamWriter(jsonStream);
                    writer.Write(JsonConvert.SerializeObject(kitCopy, Formatting.Indented));
                }

                File.Move(temporaryZipPath, fullOutputPath, true);
            }
            finally
            {
                try { File.Delete(temporaryZipPath); } catch { }
            }
        }

        // Import a MusicKit archive (created by ExportMusicKit). Extracts files into local kits folder
        // and registers the kit into Properties.MusicKits automatically.
        public static void ImportMusicKit(string zipPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) throw new FileNotFoundException("Zip file not found", zipPath);

            ValidateKitArchive(zipPath);

            string createdAudioDirectory = null;
            string createdKitFile = null;
            bool committed = false;

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(zipPath);
                Dictionary<string, ZipArchiveEntry> entries = CreateArchiveEntryMap(archive);
                ZipArchiveEntry kitJsonEntry = entries.TryGetValue("kit.json", out ZipArchiveEntry exactJson)
                    ? exactJson
                    : entries.Values.FirstOrDefault(entry =>
                        !entry.FullName.Contains('/') && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
                if (kitJsonEntry == null) throw new InvalidDataException("No kit JSON found in archive.");

                string kitJson;
                using (Stream stream = kitJsonEntry.Open())
                using (var reader = new StreamReader(stream))
                    kitJson = reader.ReadToEnd();

                var importedKit = JsonConvert.DeserializeObject<MusicKit>(kitJson);
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
                while (Properties.MusicKits.Any(k => string.Equals(k.Name, finalName, StringComparison.OrdinalIgnoreCase)) ||
                       File.Exists(Path.Combine(kitsDir, finalName + ".json")) ||
                       Directory.Exists(Path.Combine(kitsDir, finalName + "_files")))
                {
                    finalName = baseName + "_" + suffix++; 
                }
                importedKit.Name = finalName;

                // Create folder for audio files
                string audioDest = Path.Combine(kitsDir, finalName + "_files");
                Directory.CreateDirectory(audioDest);
                createdAudioDirectory = audioDest;
                var copiedAudio = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Move/copy audio files referenced in JSON into audioDest and update paths
                foreach (var p in GetAllSongProfiles(importedKit))
                {
                    if (p == null || string.IsNullOrWhiteSpace(p.Path)) continue;

                    if (TryResolveArchiveAudioEntry(entries, p.Path, out ZipArchiveEntry sourceEntry))
                    {
                        if (copiedAudio.TryGetValue(sourceEntry.FullName, out string existingDestination))
                        {
                            p.Path = existingDestination;
                            continue;
                        }

                        string destName = Path.GetFileName(sourceEntry.FullName);
                        string destPath = Path.Combine(audioDest, destName);
                        int cnt = 1;
                        while (File.Exists(destPath))
                        {
                            destName = Path.GetFileNameWithoutExtension(sourceEntry.FullName) + $"({cnt})" + Path.GetExtension(sourceEntry.FullName);
                            destPath = Path.Combine(audioDest, destName);
                            cnt++;
                        }

                        using (Stream source = sourceEntry.Open())
                        using (Stream destination = new FileStream(destPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            source.CopyTo(destination);

                        copiedAudio[sourceEntry.FullName] = destPath;
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
                createdKitFile = kitFilePath;
                File.WriteAllText(kitFilePath, JsonConvert.SerializeObject(importedKit, Formatting.Indented));

                // Register in memory
                Properties.MusicKits.Add(importedKit);
                Properties.SelectedKit = importedKit;

                // Persist kits
                Properties.SaveKits();
                committed = true;
            }
            finally
            {
                if (!committed)
                {
                    try { if (createdKitFile != null) File.Delete(createdKitFile); } catch { }
                    try { if (createdAudioDirectory != null) Directory.Delete(createdAudioDirectory, true); } catch { }
                }
            }
        }

        private static void ValidateKitArchive(string zipPath)
        {
            const int maximumEntries = 512;
            const long maximumEntryBytes = 512L * 1024 * 1024;
            const long maximumTotalBytes = 1024L * 1024 * 1024;

            using ZipArchive archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count > maximumEntries)
                throw new InvalidDataException("The archive contains too many files.");

            long totalBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.Length > maximumEntryBytes)
                    throw new InvalidDataException("An archived file is larger than 512 MB.");

                if (entry.Length > maximumTotalBytes - totalBytes)
                    throw new InvalidDataException("The extracted kit would be larger than 1 GB.");

                totalBytes += entry.Length;
            }
        }

        // Helper to enumerate all SongProfile objects in a MusicKit (primaries and extras)
        private static IEnumerable<SongProfile> GetAllSongProfiles(MusicKit kit)
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

        private static CompressionLevel GetAudioCompressionLevel(string extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".mp3" or ".aac" or ".m4a" or ".wma" or ".ogg" or ".flac" => CompressionLevel.NoCompression,
                _ => CompressionLevel.Fastest
            };
        }

        private static Dictionary<string, ZipArchiveEntry> CreateArchiveEntryMap(ZipArchive archive)
        {
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string normalizedName = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.EndsWith('/')) continue;
                if (!entries.TryAdd(normalizedName, entry))
                    throw new InvalidDataException("The archive contains duplicate file names.");
            }

            return entries;
        }

        private static bool TryResolveArchiveAudioEntry(
            IReadOnlyDictionary<string, ZipArchiveEntry> entries,
            string storedPath,
            out ZipArchiveEntry sourceEntry)
        {
            sourceEntry = null;
            if (string.IsNullOrWhiteSpace(storedPath) || Path.IsPathRooted(storedPath)) return false;

            string normalized = storedPath.Replace('\\', '/').TrimStart('/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2 || !string.Equals(segments[0], "audio", StringComparison.OrdinalIgnoreCase) ||
                segments.Any(segment => segment == "." || segment == ".."))
            {
                return false;
            }

            return entries.TryGetValue(string.Join('/', segments), out sourceEntry);
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

        private async void exportButton_Click(object sender, EventArgs e)
        {
            if (Properties.SelectedKit == null)
            {
                MessageBox.Show("No music kit selected to export.", "Export Kit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog();
            sfd.Filter = "Zip Archive|*.zip";
            sfd.FileName = ToSafeKitName(Properties.SelectedKit.Name) + ".zip";
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                MusicKit kitSnapshot = Properties.SelectedKit.DeepClone();
                try
                {
                    Enabled = false;
                    UseWaitCursor = true;
                    await Task.Run(() => ExportMusicKit(kitSnapshot, sfd.FileName));
                    MessageBox.Show("Export completed.", "Export Kit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export failed: " + ex.Message, "Export Kit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Enabled = true;
                    UseWaitCursor = false;
                }
            }
        }

        private async void importButton_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "Zip Archive|*.zip";
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    Enabled = false;
                    UseWaitCursor = true;
                    await Task.Run(() => ImportMusicKit(ofd.FileName));
                    MessageBox.Show("Import completed.", "Import Kit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshParameters();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Import failed: " + ex.Message, "Import Kit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Enabled = true;
                    UseWaitCursor = false;
                }
            }
        }
    }
}
