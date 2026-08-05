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
