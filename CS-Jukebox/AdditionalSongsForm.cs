using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CS_Jukebox
{
    /// <summary>Edits the optional tracks for one game event.</summary>
    public class AdditionalSongsForm : Form
    {
        private readonly List<SongProfile> songs;
        private readonly ListBox songsList = new ListBox();
        private readonly TextBox pathTextBox = new TextBox();
        private readonly TrackBar volumeTrackBar = new TrackBar();
        private readonly TextBox startMinutesTextBox = new TextBox();
        private readonly TextBox startSecondsTextBox = new TextBox();
        private readonly Jukebox previewJukebox = new Jukebox();
        private int selectedIndex = -1;
        private bool refreshingSongList;
        private SongProfile previewSong;

        public List<SongProfile> Songs => songs;

        public AdditionalSongsForm(string eventName, IEnumerable<SongProfile> currentSongs)
        {
            songs = currentSongs?
                .Where(song => song != null)
                .Select(song => new SongProfile(song.Path, song.Volume) { Start = song.Start, NormalizationGain = song.NormalizationGain })
                .ToList() ?? new List<SongProfile>();

            Text = eventName + " - Extra tracks";
            Width = 580;
            Height = 330;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildControls();
            RefreshSongList();
            FormClosed += (sender, args) => previewJukebox.Dispose();
        }

        private void BuildControls()
        {
            songsList.SetBounds(12, 12, 210, 230);
            songsList.SelectedIndexChanged += SongsList_SelectedIndexChanged;

            var addButton = new Button { Text = "Add", Left = 12, Top = 250, Width = 100 };
            addButton.Click += (sender, args) => AddSong();
            var removeButton = new Button { Text = "Remove", Left = 122, Top = 250, Width = 100 };
            removeButton.Click += (sender, args) => RemoveSong();

            var pathLabel = new Label { Text = "Audio file:", Left = 240, Top = 18, AutoSize = true };
            pathTextBox.SetBounds(240, 40, 180, 23);
            var previewButton = new Button { Text = "▶", Left = 428, Top = 39, Width = 30, Height = 28 };
            previewButton.Click += (sender, args) => TogglePreview(previewButton);
            var browseButton = new Button { Text = "Browse...", Left = 478, Top = 39, Width = 78 };
            browseButton.Click += (sender, args) => BrowseForSong();

            var volumeLabel = new Label { Text = "Volume:", Left = 240, Top = 80, AutoSize = true };
            volumeTrackBar.SetBounds(240, 102, 316, 28);
            volumeTrackBar.Minimum = 0;
            volumeTrackBar.Maximum = 100;
            volumeTrackBar.TickStyle = TickStyle.None;
            volumeTrackBar.Value = 100;
            volumeTrackBar.ValueChanged += VolumeTrackBar_ValueChanged;

            var startLabel = new Label { Text = "Start At:", Left = 240, Top = 145, AutoSize = true };
            startMinutesTextBox.SetBounds(240, 167, 55, 23);
            startMinutesTextBox.Text = "0";
            startMinutesTextBox.TextAlign = HorizontalAlignment.Center;
            startMinutesTextBox.KeyPress += IntegerTextBox_KeyPress;
            var minutesLabel = new Label { Text = "min", Left = 299, Top = 171, AutoSize = true };
            startSecondsTextBox.SetBounds(330, 167, 55, 23);
            startSecondsTextBox.Text = "0";
            startSecondsTextBox.TextAlign = HorizontalAlignment.Center;
            startSecondsTextBox.KeyPress += IntegerTextBox_KeyPress;
            var secondsLabel = new Label { Text = "sec", Left = 389, Top = 171, AutoSize = true };

            var saveButton = new Button { Text = "Save", Left = 396, Top = 250, Width = 75, DialogResult = DialogResult.OK };
            saveButton.Click += (sender, args) => StoreCurrentSong();
            var cancelButton = new Button { Text = "Cancel", Left = 481, Top = 250, Width = 75, DialogResult = DialogResult.Cancel };

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.AddRange(new Control[] { songsList, addButton, removeButton, pathLabel, pathTextBox, previewButton,
                browseButton, volumeLabel, volumeTrackBar, startLabel, startMinutesTextBox, minutesLabel,
                startSecondsTextBox, secondsLabel, saveButton, cancelButton });
        }

        private void RefreshSongList()
        {
            refreshingSongList = true;
            songsList.Items.Clear();
            foreach (var song in songs)
                songsList.Items.Add(string.IsNullOrWhiteSpace(song.Path) ? "(no file selected)" : Path.GetFileName(song.Path));
            refreshingSongList = false;
        }

        private void AddSong()
        {
            StoreCurrentSong();
            songs.Add(new SongProfile());
            RefreshSongList();
            songsList.SelectedIndex = songs.Count - 1;
        }

        private void RemoveSong()
        {
            if (selectedIndex < 0) return;
            songs.RemoveAt(selectedIndex);
            selectedIndex = -1;
            RefreshSongList();
            ClearEditor();
        }

        private void SongsList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (refreshingSongList) return;
            if (songsList.SelectedIndex == selectedIndex) return;
            StoreCurrentSong();
            selectedIndex = songsList.SelectedIndex;
            if (selectedIndex < 0) { ClearEditor(); return; }

            var song = songs[selectedIndex];
            pathTextBox.Text = song.Path;
            volumeTrackBar.Value = Math.Clamp(song.Volume, volumeTrackBar.Minimum, volumeTrackBar.Maximum);
            int start = Math.Max(song.Start, 0);
            startMinutesTextBox.Text = Math.Min(999, start / 60).ToString();
            startSecondsTextBox.Text = (start % 60).ToString();
        }

        private void StoreCurrentSong()
        {
            if (selectedIndex < 0 || selectedIndex >= songs.Count) return;
            SongProfile song = songs[selectedIndex];
            string newPath = pathTextBox.Text;
            if (!string.Equals(song.Path, newPath, StringComparison.OrdinalIgnoreCase))
            {
                song.NormalizationGain = AudioUtils.TryGetCachedNormalizationGain(newPath, out float cachedGain)
                    ? cachedGain
                    : -1f;
            }

            song.Path = newPath;
            song.Volume = volumeTrackBar.Value;
            song.Start = (GetTimeValue(startMinutesTextBox, 999) * 60) + GetTimeValue(startSecondsTextBox, 59);
        }

        private void ClearEditor()
        {
            pathTextBox.Clear();
            volumeTrackBar.Value = 100;
            startMinutesTextBox.Text = "0";
            startSecondsTextBox.Text = "0";
        }

        private void TogglePreview(Button previewButton)
        {
            if (string.IsNullOrWhiteSpace(pathTextBox.Text))
            {
                MessageBox.Show("No file selected to preview.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var song = new SongProfile(pathTextBox.Text, volumeTrackBar.Value)
                {
                    Start = (GetTimeValue(startMinutesTextBox, 999) * 60) + GetTimeValue(startSecondsTextBox, 59)
                };

                if (previewSong != null && previewSong.Path == song.Path && previewSong.Volume == song.Volume &&
                    previewSong.Start == song.Start && previewJukebox.IsPlaybackActive())
                {
                    previewJukebox.Stop();
                    previewSong = null;
                    previewButton.Text = "▶";
                    return;
                }

                previewJukebox.PlayPreviewSong(song);
                previewSong = song;
                previewButton.Text = "■";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to preview file: " + ex.Message, "Preview error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VolumeTrackBar_ValueChanged(object sender, EventArgs e)
        {
            if (previewSong == null || !previewJukebox.IsPlaybackActive() ||
                !string.Equals(previewSong.Path, pathTextBox.Text, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            previewJukebox.UpdatePreviewVolume(volumeTrackBar.Value);
        }

        private static int GetTimeValue(TextBox textBox, int maximum)
        {
            return int.TryParse(textBox.Text, out int value) ? Math.Clamp(value, 0, maximum) : 0;
        }

        private void IntegerTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }

        private void BrowseForSong()
        {
            using var dialog = new OpenFileDialog { Filter = "Audio files|*.mp3;*.wav;*.wma;*.aac;*.m4a|All files|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) pathTextBox.Text = dialog.FileName;
        }
    }
}
