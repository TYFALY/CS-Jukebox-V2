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
        private readonly Dictionary<TextBox, (TextBox Minutes, TextBox Seconds)> startTimeControls = new();
        private readonly GroupBox deathGroup = new GroupBox();
        private readonly Label deathStartLabel = new Label();
        private readonly TextBox deathStartTextBox = new TextBox();
        private readonly TrackBar deathTrackBar = new TrackBar();
        private readonly Button deathButton = new Button();
        private readonly Button deathPreviewButton = new Button();
        private readonly TextBox deathTextBox = new TextBox();

        public MusicSelector(MusicKit newKit, bool? createKit)
        {
            InitializeComponent();
            CreateDeathScenarioControls();
            CreateStartTimeControls();
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

            // Jukebox used for previewing individual tracks inside the editor
            previewJukebox = new Jukebox();
            FormClosed += MusicSelector_FormClosed;
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
                TogglePreview(deathTextBox, deathTrackBar, deathStartTextBox, deathPreviewButton);

            deathTextBox.Name = "deathTextBox";

            deathGroup.Controls.AddRange(new Control[]
            {
                deathStartLabel, deathStartTextBox, deathTrackBar,
                deathButton, deathPreviewButton, deathTextBox
            });
            Controls.Add(deathGroup);
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
            saveButton.Location = new System.Drawing.Point(12, 625);
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
            previewJukebox?.Dispose();
        }

        private void AddExtraSongsButtons()
        {
            AddExtraSongsButton(freezeGroup, "Freeze Time", () => currentKit.freezeSongs, songs => currentKit.freezeSongs = songs);
            AddExtraSongsButton(startGroup, "Round Start", () => currentKit.startSongs, songs => currentKit.startSongs = songs);
            AddExtraSongsButton(bombGroup, "Bomb Planted", () => currentKit.bombSongs, songs => currentKit.bombSongs = songs);
            AddExtraSongsButton(wonGroup, "Round Won", () => currentKit.winSongs, songs => currentKit.winSongs = songs);
            AddExtraSongsButton(lostGroup, "Round Lost", () => currentKit.loseSongs, songs => currentKit.loseSongs = songs);
            AddExtraSongsButton(MVPGroup, "MVP", () => currentKit.MVPSongs, songs => currentKit.MVPSongs = songs);
            AddExtraSongsButton(bombTenSecBox1, "Bomb: 10 seconds", () => currentKit.bombTenSecSongs, songs => currentKit.bombTenSecSongs = songs);
            AddExtraSongsButton(roundTenSecBox, "Round: 10 seconds", () => currentKit.roundTenSecSongs, songs => currentKit.roundTenSecSongs = songs);
            AddExtraSongsButton(mainMenuGroupBox, "Main Menu", () => currentKit.mainMenuSongs, songs => currentKit.mainMenuSongs = songs);
            AddExtraSongsButton(deathGroup, "Player Death", () => currentKit.deathSongs, songs => currentKit.deathSongs = songs);
        }

        private void AddExtraSongsButton(GroupBox group, string eventName,
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
                ResetPreviewButtonTexts();
                using var editor = new AdditionalSongsForm(eventName, getSongs());
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
            if (!Properties.TryValidateKitName(kitName, createMode ? null : originalKit, out string nameError))
            {
                MessageBox.Show(nameError, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                currentKit.freezeSong = GetSongFromParams(freezeTextBox, freezeTrackBar, freezeStartTextBox);
                currentKit.startSong = GetSongFromParams(startTextBox, startTrackBar, startStartTextBox);
                currentKit.bombSong = GetSongFromParams(bombTextBox, bombTrackBar, bombStartTextBox);
                currentKit.winSong = GetSongFromParams(wonTextBox, wonTrackBar, wonStartTextBox);
                currentKit.loseSong = GetSongFromParams(lostTextBox, lostTrackBar, lostStartTextBox);
                currentKit.MVPSong = GetSongFromParams(MVPTextBox, MVPTrackBar, MVPStartTextBox);
                currentKit.bombTenSecSong = GetSongFromParams(bombTenSecTextBox, bombTenSecTrackBar, bombTenSecStartBox);
                currentKit.roundTenSecSong = GetSongFromParams(roundTenSecTextBox, roundTenSecTrackBar, roundTenSecStartBox);
                currentKit.mainMenuSong = GetSongFromParams(menuTextBox, menuTrackBar, menuStartTextBox);
                currentKit.deathSong = GetSongFromParams(deathTextBox, deathTrackBar, deathStartTextBox);

                if (createMode)
                {
                    //Add kit to list if it is a new kit
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

                    if (ReferenceEquals(Properties.SelectedKit, originalKit))
                        Properties.SelectedKit = currentKit;
                }

                Properties.Save();

                //Add some form of delegate method to invoke in MainForm.cs
                Close();
            }
        }

        //Returns a new SongProfile based on values of given form controls
        private SongProfile GetSongFromParams(TextBox pathTextBox, TrackBar volumeTrackbar, TextBox startTextBox)
        {
            SongProfile newSong = new SongProfile(pathTextBox.Text, volumeTrackbar.Value);
            if (startTimeControls.TryGetValue(startTextBox, out var time))
                newSong.Start = (GetTimeValue(time.Minutes, 999) * 60) + GetTimeValue(time.Seconds, 59);
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
        private void TogglePreview(TextBox textBox, TrackBar volumeTrackBar, TextBox startTextBox, Button previewButton)
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
                    previewSong.Start == song.Start && previewJukebox.IsPlaybackActive())
                {
                    previewJukebox.Stop();
                    previewSong = null;
                    previewButton.Text = "▶";
                    return;
                }

                // Preview exactly what will play in-game: volume and offset.
                previewJukebox.PlayPreviewSong(song);
                previewSong = song;

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

        private void freezePreviewButton_Click(object sender, EventArgs e) => TogglePreview(freezeTextBox, freezeTrackBar, freezeStartTextBox, freezePreviewButton);
        private void startPreviewButton_Click(object sender, EventArgs e) => TogglePreview(startTextBox, startTrackBar, startStartTextBox, startPreviewButton);
        private void bombPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTextBox, bombTrackBar, bombStartTextBox, bombPreviewButton);
        private void wonPreviewButton_Click(object sender, EventArgs e) => TogglePreview(wonTextBox, wonTrackBar, wonStartTextBox, wonPreviewButton);
        private void lostPreviewButton_Click(object sender, EventArgs e) => TogglePreview(lostTextBox, lostTrackBar, lostStartTextBox, lostPreviewButton);
        private void MVPPreviewButton_Click(object sender, EventArgs e) => TogglePreview(MVPTextBox, MVPTrackBar, MVPStartTextBox, MVPPreviewButton);
        private void bombTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTenSecTextBox, bombTenSecTrackBar, bombTenSecStartBox, bombTenSecPreviewButton);
        private void roundTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(roundTenSecTextBox, roundTenSecTrackBar, roundTenSecStartBox, roundTenSecPreviewButton);
        private void menuPreviewButton_Click(object sender, EventArgs e) => TogglePreview(menuTextBox, menuTrackBar, menuStartTextBox, menuPreviewButton);

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
