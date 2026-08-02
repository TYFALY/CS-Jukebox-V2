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
        private readonly NumericUpDown startNumeric = new NumericUpDown();
        private int selectedIndex = -1;
        private bool refreshingSongList;

        public List<SongProfile> Songs => songs;

        public AdditionalSongsForm(string eventName, IEnumerable<SongProfile> currentSongs)
        {
            songs = currentSongs?
                .Where(song => song != null)
                .Select(song => new SongProfile(song.Path, song.Volume) { Start = song.Start })
                .ToList() ?? new List<SongProfile>();

            Text = eventName + " — extra tracks";
            Width = 580;
            Height = 330;
            MinimizeBox = false;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildControls();
            RefreshSongList();
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
            pathTextBox.SetBounds(240, 40, 230, 23);
            var browseButton = new Button { Text = "Browse…", Left = 478, Top = 39, Width = 78 };
            browseButton.Click += (sender, args) => BrowseForSong();

            var volumeLabel = new Label { Text = "Volume:", Left = 240, Top = 80, AutoSize = true };
            volumeTrackBar.SetBounds(240, 102, 316, 28);
            volumeTrackBar.Minimum = 0;
            volumeTrackBar.Maximum = 100;
            volumeTrackBar.TickStyle = TickStyle.None;
            volumeTrackBar.Value = 100;

            var startLabel = new Label { Text = "Start at (seconds):", Left = 240, Top = 145, AutoSize = true };
            startNumeric.SetBounds(240, 167, 100, 23);
            startNumeric.Minimum = 0;
            startNumeric.Maximum = 36000;

            var saveButton = new Button { Text = "Save", Left = 396, Top = 250, Width = 75, DialogResult = DialogResult.OK };
            saveButton.Click += (sender, args) => StoreCurrentSong();
            var cancelButton = new Button { Text = "Cancel", Left = 481, Top = 250, Width = 75, DialogResult = DialogResult.Cancel };

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            Controls.AddRange(new Control[] { songsList, addButton, removeButton, pathLabel, pathTextBox,
                browseButton, volumeLabel, volumeTrackBar, startLabel, startNumeric, saveButton, cancelButton });
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
            startNumeric.Value = Math.Clamp(song.Start, (int)startNumeric.Minimum, (int)startNumeric.Maximum);
        }

        private void StoreCurrentSong()
        {
            if (selectedIndex < 0 || selectedIndex >= songs.Count) return;
            songs[selectedIndex].Path = pathTextBox.Text;
            songs[selectedIndex].Volume = volumeTrackBar.Value;
            songs[selectedIndex].Start = (int)startNumeric.Value;
        }

        private void ClearEditor()
        {
            pathTextBox.Clear();
            volumeTrackBar.Value = 100;
            startNumeric.Value = 0;
        }

        private void BrowseForSong()
        {
            using var dialog = new OpenFileDialog { Filter = "Audio files|*.mp3;*.wav;*.wma;*.aac;*.m4a|All files|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) pathTextBox.Text = dialog.FileName;
        }
    }
}
