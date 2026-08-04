using System;
using System.Windows.Forms;

namespace CS_Jukebox
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!MediaPlayerAvailability.IsAvailable(out string mediaPlayerError))
            {
                MessageBox.Show(
                    "CS Jukebox requires the Windows Media Player system component.\n\n" +
                    "Enable it in Windows Features: Media Features -> Windows Media Player. " +
                    "On Windows N editions, install the Media Feature Pack first.\n\n" +
                    "Technical details: " + mediaPlayerError,
                    "Windows Media Player is unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "CS Jukebox could not start:\n\n" + ex.Message,
                    "Startup error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
