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

        public GamePathForm()
        {
            InitializeComponent();
            MaximizeBox = false;
            MinimizeBox = false;
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
            if (!GameInstallLocator.TryResolveGameDirectory(path, out string gameDirectory)) return false;

            Properties.GameDir = gameDirectory;
            return true;
        }

        //Saves the directory if it is valid.
        private void okButton_Click(object sender, EventArgs e)
        {
            if (dirValid)
            {
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
                Properties.GameDir = null;
                errorLabel.Visible = true;
                okButton.Enabled = false;
            }
        }
    }
}
