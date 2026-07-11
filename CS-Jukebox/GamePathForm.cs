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

        //Check if the directory is a valid CSGO install
        private bool CheckDir(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                // Direct match (selected folder is the game folder)
                if (Directory.Exists(Path.Combine(path, "core")))
                {
                    Properties.GameDir = path;
                    return true;
                }

                // Recursively search inner folders for a directory that contains a 'core' folder
                foreach (string dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (Directory.Exists(Path.Combine(dir, "core")))
                        {
                            Properties.GameDir = dir;
                            return true;
                        }

                        // Also accept folders that contain a csgo/cfg or cs2/cfg structure
                        string folderName = Path.GetFileName(dir).ToLowerInvariant();
                        if ((folderName == "csgo" || folderName == "cs2") && Directory.Exists(Path.Combine(dir, "cfg")))
                        {
                            Properties.GameDir = dir;
                            return true;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip folders we don't have access to
                        continue;
                    }
                    catch (PathTooLongException)
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore and return false
            }
            catch (PathTooLongException)
            {
                // Ignore and return false
            }

            return false;
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
                // CheckDir will set Properties.GameDir to the resolved game folder (could be a child folder)
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
