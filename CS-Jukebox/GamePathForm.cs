using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace CS_Jukebox
{
    public partial class GamePathForm : Form
    {
        private bool dirValid = false;
        private string resolvedGameDirectory;

        public GamePathForm()
        {
            InitializeComponent();
            MaximizeBox = false;
            MinimizeBox = false;
            if (!string.IsNullOrWhiteSpace(Properties.GameDir))
                dirTextBox.Text = Properties.GameDir;
            ThemeManager.Apply(this);
        }

        //Open folder browser dialog
        private void browseButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                dirTextBox.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        // Resolves the selected path to CS2's actual game directory. CS2 uses
        // game\\bin\\win64\\cs2.exe and game\\csgo\\cfg; it does not require
        // a legacy csgo.exe file.
        private bool CheckDir(string path)
        {
            return GameInstallLocator.TryResolveGameDirectory(path, out resolvedGameDirectory);
        }

        //Saves the directory if it is valid.
        private void okButton_Click(object sender, EventArgs e)
        {
            if (dirValid)
            {
                Properties.GameDir = resolvedGameDirectory;
                Properties.SaveProperties();
                Close();
            }
        }

        //Shows error label based on whether given directory is a valid CS:GO path
        private void dirTextBox_TextChanged(object sender, EventArgs e)
        {
            dirValid = CheckDir(dirTextBox.Text);
            if (dirValid)
            {
                // CheckDir stores the canonical ...\\Counter-Strike...\\game folder.
                errorLabel.Visible = false;
                okButton.Enabled = true;
            }
            else
            {
                resolvedGameDirectory = null;
                errorLabel.Visible = true;
                okButton.Enabled = false;
            }
        }
    }
}
