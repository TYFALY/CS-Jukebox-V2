using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CS_Jukebox
{
    public partial class MusicSelector : Form
    {
        MusicKit currentKit = null; //Music kit currently being edited
        bool createMode = false;
        private Jukebox previewJukebox;
        private string playingPreviewPath = null;

        public MusicSelector(MusicKit newKit, bool? createKit)
        {
            InitializeComponent();
            MaximizeBox = false;

            if (createKit.HasValue) createMode = createKit.Value;
            currentKit = newKit;

            LoadKitParameters();
            AddExtraSongsButtons();

            // Jukebox used for previewing individual tracks inside the editor
            previewJukebox = new Jukebox();
        }

        private void MusicSelector_Load(object sender, EventArgs e)
        {

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
        }

        private void AddExtraSongsButton(GroupBox group, string eventName,
            Func<List<SongProfile>> getSongs, Action<List<SongProfile>> setSongs)
        {
            var button = new Button
            {
                Location = new System.Drawing.Point(122, 14),
                Size = new System.Drawing.Size(107, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            void SetButtonText() => button.Text = $"Extra tracks ({getSongs()?.Count ?? 0})";

            SetButtonText();
            button.Click += (sender, args) =>
            {
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
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (nameTextBox.Text == "")
            {
                //Show warning prompt
                MessageBox.Show("Please enter a name.", "Warning", MessageBoxButtons.OK);
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

                if (createMode)
                {
                    //Add kit to list if it is a new kit
                    currentKit.Name = nameTextBox.Text;
                    Properties.MusicKits.Add(currentKit);
                    Properties.SelectedKit = currentKit;
                }
                else if (nameTextBox.Text != currentKit.Name)
                {
                    //Detect if a music kit was renamed
                    Properties.DeleteKitFile(currentKit.Name);
                    currentKit.Name = nameTextBox.Text;
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
            newSong.Start = startTextBox.Enabled ? int.Parse(startTextBox.Text) : 0;
            return newSong;
        }

        //Sets parameters of controls from song
        private void SetParamsFromSong(SongProfile songProfile,
                                       TextBox pathTextBox,
                                       TrackBar volumeTrackbar,
                                       TextBox startTextBox)
        {
            pathTextBox.Text = songProfile.Path;
            volumeTrackbar.Value = songProfile.Volume;
            startTextBox.Text = songProfile.Start.ToString();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            DialogResult confirmResult = MessageBox.Show("Are you sure you want to delete this kit?", "Delete Music Kit", MessageBoxButtons.YesNo);

            if (createMode)
            {
                Close();
            }

            if (confirmResult == DialogResult.Yes)
            {
                Properties.DeleteKitFile(currentKit.Name);
                Properties.MusicKits.Remove(currentKit);
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
        private void TogglePreview(TextBox textBox, Button previewButton)
        {
            string path = textBox.Text;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("No file selected to preview.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // If the same track is playing, stop it
                if (playingPreviewPath != null && playingPreviewPath == path)
                {
                    previewJukebox.Stop();
                    playingPreviewPath = null;
                    previewButton.Text = "▶";
                    return;
                }

                // Otherwise play this track and update button texts
                previewJukebox.PlaySong(path);
                playingPreviewPath = path;

                // Reset all preview buttons' text to play
                foreach (var ctrl in this.Controls)
                {
                    if (ctrl is GroupBox gb)
                    {
                        foreach (Control c in gb.Controls)
                        {
                            if (c is Button b && b.Name != previewButton.Name && b.Name.EndsWith("PreviewButton"))
                            {
                                b.Text = "▶";
                            }
                        }
                    }
                }

                previewButton.Text = "■";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to preview file: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void freezePreviewButton_Click(object sender, EventArgs e) => TogglePreview(freezeTextBox, freezePreviewButton);
        private void startPreviewButton_Click(object sender, EventArgs e) => TogglePreview(startTextBox, startPreviewButton);
        private void bombPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTextBox, bombPreviewButton);
        private void wonPreviewButton_Click(object sender, EventArgs e) => TogglePreview(wonTextBox, wonPreviewButton);
        private void lostPreviewButton_Click(object sender, EventArgs e) => TogglePreview(lostTextBox, lostPreviewButton);
        private void MVPPreviewButton_Click(object sender, EventArgs e) => TogglePreview(MVPTextBox, MVPPreviewButton);
        private void bombTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(bombTenSecTextBox, bombTenSecPreviewButton);
        private void roundTenSecPreviewButton_Click(object sender, EventArgs e) => TogglePreview(roundTenSecTextBox, roundTenSecPreviewButton);
        private void menuPreviewButton_Click(object sender, EventArgs e) => TogglePreview(menuTextBox, menuPreviewButton);

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
