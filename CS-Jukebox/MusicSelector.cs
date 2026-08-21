using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CS_Jukebox
{
    public partial class MusicSelector : Form
    {
        MusicKit currentKit = null; //Music kit currently being edited
        private readonly MusicKit originalKit;
        bool createMode = false;
        private Jukebox previewJukebox;
        private SongProfile previewSong;
        private TrackBar previewVolumeTrackBar;
        private MusicEventType? previewEventType;
        private readonly Dictionary<TextBox, (TextBox Minutes, TextBox Seconds)> startTimeControls = new();
        private readonly GroupBox deathGroup = new GroupBox();
        private readonly Label deathStartLabel = new Label();
        private readonly TextBox deathStartTextBox = new TextBox();
        private readonly TrackBar deathTrackBar = new TrackBar();
        private readonly Button deathButton = new Button();
        private readonly Button deathPreviewButton = new Button();
        private readonly TextBox deathTextBox = new TextBox();
        private readonly Button saveAsCopyButton = new Button();
        private readonly CheckBox previewDurationCheckBox = new CheckBox();

        public MusicSelector(MusicKit newKit, bool? createKit)
        {
            InitializeComponent();
            CreateDeathScenarioControls();
            CreateStartTimeControls();
            CreatePreviewDurationControl();
            ConfigureEditorLayout();
            MaximizeBox = false;

            if (createKit.HasValue) createMode = createKit.Value;
            originalKit = newKit;
            currentKit = createMode
                ? newKit ?? new MusicKit("")
                : (newKit ?? new MusicKit("")).DeepClone();
            currentKit.EnsureInitialized();

            LoadKitParameters();
            AddExtraSongsButtons();
            EnsurePreviewButtonsVisible();
            ConfigurePreviewVolumeSynchronization();

            saveAsCopyButton.Name = "saveAsCopyButton";
            saveAsCopyButton.Text = "Save as Copy";
            saveAsCopyButton.Size = new System.Drawing.Size(110, 27);
            saveAsCopyButton.Click += saveAsCopyButton_Click;
            Controls.Add(saveAsCopyButton);
            saveAsCopyButton.Visible = !createMode;

            // Jukebox used for previewing individual tracks inside the editor
            previewJukebox = new Jukebox();
            previewJukebox.PreviewCompleted += PreviewJukebox_PreviewCompleted;
            FormClosed += MusicSelector_FormClosed;
            ThemeManager.Apply(this);
        }

        private void MusicSelector_Load(object sender, EventArgs e)
        {

        }

        private void CreateStartTimeControls()
        {
            AddStartTimeControls(freezeGroup, label1, freezeStartTextBox);
            AddStartTimeControls(startGroup, label3, startStartTextBox);
            AddStartTimeControls(bombGroup, label4, bombStartTextBox);
            AddStartTimeControls(wonGroup, label5, wonStartTextBox);
            AddStartTimeControls(lostGroup, label6, lostStartTextBox);
            AddStartTimeControls(MVPGroup, label7, MVPStartTextBox);
            AddStartTimeControls(bombTenSecBox1, label8, bombTenSecStartBox);
            AddStartTimeControls(roundTenSecBox, label9, roundTenSecStartBox);
            AddStartTimeControls(mainMenuGroupBox, label10, menuStartTextBox);
            AddStartTimeControls(deathGroup, deathStartLabel, deathStartTextBox);
        }

        private void CreateDeathScenarioControls()
        {
            deathGroup.Name = "deathGroup";
            deathGroup.Text = "Player Death:";

            deathStartLabel.Name = "deathStartLabel";
            deathStartLabel.AutoSize = true;
            deathStartTextBox.Name = "deathStartTextBox";

            deathTrackBar.Name = "deathTrackBar";
            deathTrackBar.Minimum = 0;
            deathTrackBar.Maximum = 100;
            deathTrackBar.Value = 100;
            deathTrackBar.TickStyle = TickStyle.None;
            deathTrackBar.AutoSize = false;

            deathButton.Name = "deathButton";
            deathButton.Text = "Browse";
            deathButton.Click += (sender, args) => OpenSongFile(deathTextBox);

            deathPreviewButton.Name = "deathPreviewButton";
            deathPreviewButton.Text = "▶";
            deathPreviewButton.Click += (sender, args) =>
                TogglePreview(deathTextBox, deathTrackBar, deathStartTextBox, deathPreviewButton, MusicEventType.PlayerDeath);

            deathTextBox.Name = "deathTextBox";

            deathGroup.Controls.AddRange(new Control[]
            {
                deathStartLabel, deathStartTextBox, deathTrackBar,
                deathButton, deathPreviewButton, deathTextBox
            });
            Controls.Add(deathGroup);
        }

        private void CreatePreviewDurationControl()
        {
            previewDurationCheckBox.Name = "previewDurationCheckBox";
            previewDurationCheckBox.Text = "Limit preview to event duration";
            previewDurationCheckBox.AutoSize = true;
            previewDurationCheckBox.Checked = Properties.LimitPreviewToEventDuration;
            previewDurationCheckBox.CheckedChanged += PreviewDurationCheckBox_CheckedChanged;
            Controls.Add(previewDurationCheckBox);
        }

        private void AddStartTimeControls(GroupBox group, Label label, TextBox legacyTextBox)
        {
            legacyTextBox.Visible = false;
            legacyTextBox.TabStop = false;
            label.Text = "Start At:";

            var minutes = new TextBox { Location = new System.Drawing.Point(75, 19), Size = new System.Drawing.Size(38, 23), Text = "0", TextAlign = HorizontalAlignment.Center };
            var minutesLabel = new Label { Text = "min", Location = new System.Drawing.Point(117, 22), AutoSize = true };
            var seconds = new TextBox { Location = new System.Drawing.Point(145, 19), Size = new System.Drawing.Size(38, 23), Text = "0", TextAlign = HorizontalAlignment.Center };
            var secondsLabel = new Label { Text = "sec", Location = new System.Drawing.Point(187, 22), AutoSize = true };
            minutes.KeyPress += IntegerTextBox_KeyPress;
            seconds.KeyPress += IntegerTextBox_KeyPress;

            startTimeControls.Add(legacyTextBox, (minutes, seconds));
            group.Controls.AddRange(new Control[] { minutes, minutesLabel, seconds, secondsLabel });
        }

        private void ConfigureEditorLayout()
        {
            ClientSize = new System.Drawing.Size(700, 660);

            LayoutScenarioGroup(freezeGroup, freezeButton, freezePreviewButton, freezeTextBox, freezeTrackBar, label1, freezeStartTextBox, 12, 45);
            LayoutScenarioGroup(wonGroup, wonButton, wonPreviewButton, wonTextBox, wonTrackBar, label5, wonStartTextBox, 358, 45);
            LayoutScenarioGroup(startGroup, startButton, startPreviewButton, startTextBox, startTrackBar, label3, startStartTextBox, 12, 160);
            LayoutScenarioGroup(lostGroup, lostButton, lostPreviewButton, lostTextBox, lostTrackBar, label6, lostStartTextBox, 358, 160);
            LayoutScenarioGroup(bombGroup, bombButton, bombPreviewButton, bombTextBox, bombTrackBar, label4, bombStartTextBox, 12, 275);
            LayoutScenarioGroup(MVPGroup, MVPButton, MVPPreviewButton, MVPTextBox, MVPTrackBar, label7, MVPStartTextBox, 358, 275);
            LayoutScenarioGroup(bombTenSecBox1, bombTenSecButton, bombTenSecPreviewButton, bombTenSecTextBox, bombTenSecTrackBar, label8, bombTenSecStartBox, 12, 390);
            LayoutScenarioGroup(roundTenSecBox, roundTenSecButton, roundTenSecPreviewButton, roundTenSecTextBox, roundTenSecTrackBar, label9, roundTenSecStartBox, 358, 390);
            LayoutScenarioGroup(mainMenuGroupBox, menuButton, menuPreviewButton, menuTextBox, menuTrackBar, label10, menuStartTextBox, 12, 505);
            LayoutScenarioGroup(deathGroup, deathButton, deathPreviewButton, deathTextBox, deathTrackBar, deathStartLabel, deathStartTextBox, 358, 505);

            label2.Location = new System.Drawing.Point(12, 14);
            nameTextBox.Location = new System.Drawing.Point(62, 11);
            nameTextBox.Size = new System.Drawing.Size(200, 23);
            deleteButton.Location = new System.Drawing.Point(272, 10);
            previewDurationCheckBox.Location = new System.Drawing.Point(370, 13);
            saveButton.Text = "Apply Changes";
            saveButton.Size = new System.Drawing.Size(110, 27);
            saveButton.Location = new System.Drawing.Point(12, 625);

            saveAsCopyButton.Location = new System.Drawing.Point(130, 625);
            saveAsCopyButton.Visible = !createMode;

            cancelButton.Location = new System.Drawing.Point(613, 625);
        }

        private void LayoutScenarioGroup(GroupBox group, Button browseButton, Button previewButton, TextBox pathTextBox,
            TrackBar volumeTrackBar, Label timeLabel, TextBox legacyTimeTextBox, int x, int y)
        {
            group.Location = new System.Drawing.Point(x, y);
            group.Size = new System.Drawing.Size(330, 110);
            timeLabel.Location = new System.Drawing.Point(12, 22);
            volumeTrackBar.Location = new System.Drawing.Point(12, 47);
            volumeTrackBar.Size = new System.Drawing.Size(306, 22);
            browseButton.Location = new System.Drawing.Point(12, 73);
            browseButton.Size = new System.Drawing.Size(76, 28);
            previewButton.Location = new System.Drawing.Point(94, 73);
            previewButton.Size = new System.Drawing.Size(28, 28);
            pathTextBox.Location = new System.Drawing.Point(130, 76);
            pathTextBox.Size = new System.Drawing.Size(188, 23);

            if (startTimeControls.TryGetValue(legacyTimeTextBox, out var time))
            {
                time.Minutes.Location = new System.Drawing.Point(75, 19);
                time.Seconds.Location = new System.Drawing.Point(145, 19);
            }
        }

        private void IntegerTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        private void EnsurePreviewButtonsVisible()
        {
            var previewButtons = new[]
            {
                (freezeGroup, freezePreviewButton), (startGroup, startPreviewButton), (bombGroup, bombPreviewButton),
                (wonGroup, wonPreviewButton), (lostGroup, lostPreviewButton), (MVPGroup, MVPPreviewButton),
                (bombTenSecBox1, bombTenSecPreviewButton), (roundTenSecBox, roundTenSecPreviewButton),
                (mainMenuGroupBox, menuPreviewButton), (deathGroup, deathPreviewButton)
            };

            foreach (var (group, button) in previewButtons)
            {
                // Several generated groups were initialized before their
                // preview button. Controls.Add(null) is ignored by WinForms,
                // so explicitly attach every button after initialization.
                if (button.Parent != group) group.Controls.Add(button);
                button.Visible = true;
                button.Enabled = true;
                button.BringToFront();
            }
        }

        private void MusicSelector_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (previewJukebox != null)
                previewJukebox.PreviewCompleted -= PreviewJukebox_PreviewCompleted;
            previewJukebox?.Dispose();
        }

        private void PreviewDurationCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.LimitPreviewToEventDuration = previewDurationCheckBox.Checked;
            Properties.SaveProperties();

            previewJukebox?.StopImmediately();
            previewSong = null;
            previewVolumeTrackBar = null;
            previewEventType = null;
            ResetPreviewButtonTexts();
        }

        private void PreviewJukebox_PreviewCompleted(object sender, PreviewCompletedEventArgs e)
        {
            previewSong = null;
            previewVolumeTrackBar = null;
            previewEventType = null;
            ResetPreviewButtonTexts();

            if (IsDisposed || Disposing) return;
            string message = e.Reason == PreviewCompletionReason.EventEnded
                ? "Event ended."
                : "Music ended.";
            MessageBox.Show(this, message, "Preview complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ConfigurePreviewVolumeSynchronization()
        {
            foreach (TrackBar trackBar in new[]
            {
                freezeTrackBar, startTrackBar, bombTrackBar, wonTrackBar, lostTrackBar,
                MVPTrackBar, bombTenSecTrackBar, roundTenSecTrackBar, menuTrackBar, deathTrackBar
            })
            {
                trackBar.ValueChanged += PreviewVolumeTrackBar_ValueChanged;
            }
        }

        private void PreviewVolumeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            if (sender is not TrackBar trackBar || !ReferenceEquals(trackBar, previewVolumeTrackBar) ||
                previewJukebox?.IsPlaybackActive() != true)
            {
                return;
            }

            previewJukebox.UpdatePreviewVolume(trackBar.Value);
        }

        private void AddExtraSongsButtons()
        {
            AddExtraSongsButton(freezeGroup, MusicEventType.FreezeTime, () => currentKit.freezeSongs, songs => currentKit.freezeSongs = songs);
            AddExtraSongsButton(startGroup, MusicEventType.RoundStart, () => currentKit.startSongs, songs => currentKit.startSongs = songs);
            AddExtraSongsButton(bombGroup, MusicEventType.BombPlanted, () => currentKit.bombSongs, songs => currentKit.bombSongs = songs);
            AddExtraSongsButton(wonGroup, MusicEventType.RoundWon, () => currentKit.winSongs, songs => currentKit.winSongs = songs);
            AddExtraSongsButton(lostGroup, MusicEventType.RoundLost, () => currentKit.loseSongs, songs => currentKit.loseSongs = songs);
            AddExtraSongsButton(MVPGroup, MusicEventType.Mvp, () => currentKit.MVPSongs, songs => currentKit.MVPSongs = songs);
            AddExtraSongsButton(bombTenSecBox1, MusicEventType.BombTenSeconds, () => currentKit.bombTenSecSongs, songs => currentKit.bombTenSecSongs = songs);
            AddExtraSongsButton(roundTenSecBox, MusicEventType.RoundTenSeconds, () => currentKit.roundTenSecSongs, songs => currentKit.roundTenSecSongs = songs);
            AddExtraSongsButton(mainMenuGroupBox, MusicEventType.MainMenu, () => currentKit.mainMenuSongs, songs => currentKit.mainMenuSongs = songs);
            AddExtraSongsButton(deathGroup, MusicEventType.PlayerDeath, () => currentKit.deathSongs, songs => currentKit.deathSongs = songs);
        }

        private void AddExtraSongsButton(GroupBox group, MusicEventType eventType,
            Func<List<SongProfile>> getSongs, Action<List<SongProfile>> setSongs)
        {
            var button = new Button
            {
                Location = new System.Drawing.Point(244, 15),
                Size = new System.Drawing.Size(74, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            void SetButtonText() => button.Text = $"Extra ({getSongs()?.Count ?? 0})";

            SetButtonText();
            button.Click += (sender, args) =>
            {
                previewJukebox.StopImmediately();
                previewSong = null;
                previewVolumeTrackBar = null;
                previewEventType = null;
                ResetPreviewButtonTexts();
                using var editor = new AdditionalSongsForm(eventType, getSongs());
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    setSongs(editor.Songs);
                    SetButtonText();
                }
            };
            group.Controls.Add(button);
        }

        //Loads parameters into controls such as textboxes and trackbars
        private void LoadKitParameters()
        {
            nameTextBox.Text = currentKit.Name;

            SetParamsFromSong(currentKit.freezeSong, freezeTextBox, freezeTrackBar, freezeStartTextBox);
            SetParamsFromSong(currentKit.startSong, startTextBox, startTrackBar, startStartTextBox);
            SetParamsFromSong(currentKit.bombSong, bombTextBox, bombTrackBar, bombStartTextBox);
            SetParamsFromSong(currentKit.winSong, wonTextBox, wonTrackBar, wonStartTextBox);
            SetParamsFromSong(currentKit.loseSong, lostTextBox, lostTrackBar, lostStartTextBox);
            SetParamsFromSong(currentKit.MVPSong, MVPTextBox, MVPTrackBar, MVPStartTextBox);
            SetParamsFromSong(currentKit.bombTenSecSong, bombTenSecTextBox, bombTenSecTrackBar, bombTenSecStartBox);
            SetParamsFromSong(currentKit.roundTenSecSong, roundTenSecTextBox, roundTenSecTrackBar, roundTenSecStartBox);
            SetParamsFromSong(currentKit.mainMenuSong, menuTextBox, menuTrackBar, menuStartTextBox);
            SetParamsFromSong(currentKit.deathSong, deathTextBox, deathTrackBar, deathStartTextBox);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            string kitName = nameTextBox.Text.Trim();
            bool isNameUnchanged = !createMode && originalKit != null && string.Equals(kitName, originalKit.Name, StringComparison.OrdinalIgnoreCase);

            bool isValid = isNameUnchanged
                ? Properties.TryValidateKitFileName(kitName, out string nameError)
                : Properties.TryValidateKitName(kitName, createMode ? null : originalKit, out nameError);

            if (!isValid)
            {
                MessageBox.Show(nameError, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CommitChanges(kitName, createMode);
        }

        private void saveAsCopyButton_Click(object sender, EventArgs e)
        {
            string kitName = nameTextBox.Text.Trim();
            if (originalKit != null && string.Equals(kitName, originalKit.Name, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Please enter a new unique name to save as a copy.", "Save as Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Properties.TryValidateKitName(kitName, null, out string nameError))
            {
                MessageBox.Show(nameError, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CommitChanges(kitName, isCreate: true);
        }

        private void CommitChanges(string kitName, bool isCreate)
        {
            currentKit.freezeSong = GetSongFromParams(freezeTextBox, freezeTrackBar, freezeStartTextBox, currentKit.freezeSong);
            currentKit.startSong = GetSongFromParams(startTextBox, startTrackBar, startStartTextBox, currentKit.startSong);
            currentKit.bombSong = GetSongFromParams(bombTextBox, bombTrackBar, bombStartTextBox, currentKit.bombSong);
            currentKit.winSong = GetSongFromParams(wonTextBox, wonTrackBar, wonStartTextBox, currentKit.winSong);
            currentKit.loseSong = GetSongFromParams(lostTextBox, lostTrackBar, lostStartTextBox, currentKit.loseSong);
            currentKit.MVPSong = GetSongFromParams(MVPTextBox, MVPTrackBar, MVPStartTextBox, currentKit.MVPSong);
            currentKit.bombTenSecSong = GetSongFromParams(bombTenSecTextBox, bombTenSecTrackBar, bombTenSecStartBox, currentKit.bombTenSecSong);
            currentKit.roundTenSecSong = GetSongFromParams(roundTenSecTextBox, roundTenSecTrackBar, roundTenSecStartBox, currentKit.roundTenSecSong);
            currentKit.mainMenuSong = GetSongFromParams(menuTextBox, menuTrackBar, menuStartTextBox, currentKit.mainMenuSong);
            currentKit.deathSong = GetSongFromParams(deathTextBox, deathTrackBar, deathStartTextBox, currentKit.deathSong);

            if (isCreate)
            {
                currentKit.Name = kitName;
                Properties.MusicKits.Add(currentKit);
                Properties.SelectedKit = currentKit;
            }
            else
            {
                string originalName = originalKit?.Name;
                if (!string.Equals(kitName, originalName, StringComparison.Ordinal))
                    Properties.DeleteKitFile(originalName);

                currentKit.Name = kitName;
                int kitIndex = Properties.MusicKits.IndexOf(originalKit);
                if (kitIndex >= 0)
                    Properties.MusicKits[kitIndex] = currentKit;
                else
                {
                    // Fallback if originalKit instance reference was changed
                    int matchByNameIndex = Properties.MusicKits.FindIndex(k => k != null && string.Equals(k.Name, originalName, StringComparison.OrdinalIgnoreCase));
                    if (matchByNameIndex >= 0)
                        Properties.MusicKits[matchByNameIndex] = currentKit;
                    else
                        Properties.MusicKits.Add(currentKit);
                }

                if (Properties.SelectedKit == null || ReferenceEquals(Properties.SelectedKit, originalKit) ||
                    string.Equals(Properties.SelectedKit.Name, originalName, StringComparison.OrdinalIgnoreCase))
                {
                    Properties.SelectedKit = currentKit;
                }
            }

            Properties.Save();
            Close();
        }

        //Returns a new SongProfile based on values of given form controls
        private SongProfile GetSongFromParams(TextBox pathTextBox, TrackBar volumeTrackbar, TextBox startTextBox,
            SongProfile previousSong = null)
        {
            SongProfile newSong = new SongProfile(pathTextBox.Text, volumeTrackbar.Value);
            if (startTimeControls.TryGetValue(startTextBox, out var time))
                newSong.Start = (GetTimeValue(time.Minutes, 999) * 60) + GetTimeValue(time.Seconds, 59);

            if (previousSong != null && previousSong.NormalizationGain > 0f &&
                string.Equals(previousSong.Path, newSong.Path, StringComparison.OrdinalIgnoreCase))
            {
                newSong.NormalizationGain = previousSong.NormalizationGain;
            }
            else if (AudioUtils.TryGetCachedNormalizationGain(newSong.Path, out float cachedGain))
            {
                newSong.NormalizationGain = cachedGain;
            }

            return newSong;
        }

        //Sets parameters of controls from song
        private void SetParamsFromSong(SongProfile songProfile,
                                       TextBox pathTextBox,
                                       TrackBar volumeTrackbar,
                                       TextBox startTextBox)
        {
            pathTextBox.Text = songProfile.Path;
            volumeTrackbar.Value = Math.Clamp(songProfile.Volume, volumeTrackbar.Minimum, volumeTrackbar.Maximum);
            if (startTimeControls.TryGetValue(startTextBox, out var time))
            {
                int start = Math.Max(songProfile.Start, 0);
                time.Minutes.Text = Math.Min(999, start / 60).ToString();
                time.Seconds.Text = (start % 60).ToString();
            }
        }

        private static int GetTimeValue(TextBox textBox, int maximum)
        {
            return int.TryParse(textBox.Text, out int value) ? Math.Clamp(value, 0, maximum) : 0;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (createMode)
            {
                Close();
                return;
            }

            DialogResult confirmResult = MessageBox.Show("Are you sure you want to delete this kit?", "Delete Music Kit", MessageBoxButtons.YesNo);

            if (confirmResult == DialogResult.Yes)
            {
                string kitName = originalKit?.Name ?? currentKit.Name;
                Properties.DeleteKitFile(kitName);
                Properties.MusicKits.Remove(originalKit ?? currentKit);
                Properties.SelectedKit = Properties.MusicKits.FirstOrDefault();
                Properties.SaveKits();

                Close();
            }
        }

        //Click handlers for browse buttons

        private void OpenSongFile(TextBox textBox)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox.Text = openFileDialog1.FileName;
            }
        }

        private void freezeButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(freezeTextBox);
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(startTextBox);
        }

        private void bombButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(bombTextBox);
        }

        private void wonButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(wonTextBox);
        }

        private void lostButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(lostTextBox);
        }

        private void MVPButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(MVPTextBox);
        }

        private void bombTenSecButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(bombTenSecTextBox);
        }

        private void roundTenSecButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(roundTenSecTextBox);
        }

        private void menuButton_Click(object sender, EventArgs e)
        {
            OpenSongFile(menuTextBox);
        }

        // Preview button handlers
        private void TogglePreview(TextBox textBox, TrackBar volumeTrackBar, TextBox startTextBox,
            Button previewButton, MusicEventType eventType)
        {
            string path = textBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("No file selected to preview.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                SongProfile song = GetSongFromParams(textBox, volumeTrackBar, startTextBox);

                // If these exact parameters are already playing, stop preview.
                if (previewSong != null && previewSong.Path == song.Path && previewSong.Volume == song.Volume &&
                    previewSong.Start == song.Start && previewEventType == eventType && previewJukebox.IsPlaybackActive())
                {
                    previewJukebox.Stop();
                    previewSong = null;
                    previewVolumeTrackBar = null;
                    previewEventType = null;
                    previewButton.Text = "▶";
                    return;
                }

                // Preview exactly what will play in-game: volume and offset.
                if (Properties.LimitPreviewToEventDuration)
                    previewJukebox.PlayPreviewSong(song, MusicEventTiming.GetPreviewDurationSeconds(eventType));
                else
                    previewJukebox.PlayPreviewSong(song);
                previewSong = song;
                previewVolumeTrackBar = volumeTrackBar;
                previewEventType = eventType;

                ResetPreviewButtonTexts();

                previewButton.Text = "■";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to preview file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetPreviewButtonTexts()
        {
            foreach (Control ctrl in Controls)
            {
                if (ctrl is GroupBox gb)
                {
                    foreach (Control child in gb.Controls)
                    {
                        if (child is Button button && button.Name.EndsWith("PreviewButton", StringComparison.Ordinal))
                            button.Text = "▶";
                    }
                }
            }
        }

        private void freezePreviewButton_Click(object sender, EventArgs e) => TogglePreview(freezeTextBox, freezeTrackBar, freezeStartTextBox, freezePreviewButton, MusicEventType.FreezeTime);
        private void startPreviewButton_Click(object sender, EventArgs e) => TogglePreview(startTextBox, startTrackBar, startStartTextBox, startPreviewButton, MusicEventType.RoundStart);
        private void bombPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTextBox, bombTrackBar, bombStartTextBox, bombPreviewButton, MusicEventType.BombPlanted);
        private void wonPreviewButton_Click(object sender, EventArgs e) => TogglePreview(wonTextBox, wonTrackBar, wonStartTextBox, wonPreviewButton, MusicEventType.RoundWon);
        private void lostPreviewButton_Click(object sender, EventArgs e) => TogglePreview(lostTextBox, lostTrackBar, lostStartTextBox, lostPreviewButton, MusicEventType.RoundLost);
        private void MVPPreviewButton_Click(object sender, EventArgs e) => TogglePreview(MVPTextBox, MVPTrackBar, MVPStartTextBox, MVPPreviewButton, MusicEventType.Mvp);
        private void bombTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTenSecTextBox, bombTenSecTrackBar, bombTenSecStartBox, bombTenSecPreviewButton, MusicEventType.BombTenSeconds);
        private void roundTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(roundTenSecTextBox, roundTenSecTrackBar, roundTenSecStartBox, roundTenSecPreviewButton, MusicEventType.RoundTenSeconds);
        private void menuPreviewButton_Click(object sender, EventArgs e) => TogglePreview(menuTextBox, menuTrackBar, menuStartTextBox, menuPreviewButton, MusicEventType.MainMenu);

        private void freezeStartTextbox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void startStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void bombStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void wonStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void lostStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void MVPStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void bombTenSecStartBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void roundTenSecStartBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }

        private void menuStartTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) &&
        (e.KeyChar != '.'))
            {
                e.Handled = true;
            }
        }
    }
}
