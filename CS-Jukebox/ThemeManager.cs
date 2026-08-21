using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CS_Jukebox
{
    internal static class ThemeManager
    {
        private static readonly Color DarkBackground = Color.FromArgb(31, 35, 42);
        private static readonly Color DarkSurface = Color.FromArgb(42, 47, 56);
        private static readonly Color DarkControl = Color.FromArgb(52, 58, 69);
        private static readonly Color DarkInput = Color.FromArgb(36, 41, 49);
        private static readonly Color DarkText = Color.FromArgb(232, 235, 241);
        private static readonly Color DarkMutedText = Color.FromArgb(184, 192, 204);
        private static readonly Color DarkBorder = Color.FromArgb(82, 91, 106);
        private static readonly Color DarkHover = Color.FromArgb(63, 71, 84);
        private static readonly Color DarkPressed = Color.FromArgb(72, 99, 142);
        private static readonly Color DarkAccent = Color.FromArgb(122, 162, 247);
        private static readonly Color DarkError = Color.FromArgb(255, 125, 135);

        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr windowHandle,
            int attribute,
            ref int attributeValue,
            int attributeSize);

        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed) return;

            ApplyControl(form, Properties.DarkTheme);
            form.HandleCreated -= Form_HandleCreated;
            form.HandleCreated += Form_HandleCreated;
            if (form.IsHandleCreated) ApplyTitleBar(form);
            form.Invalidate(true);
        }

        public static void ApplyToOpenForms()
        {
            var forms = new Form[Application.OpenForms.Count];
            for (int index = 0; index < forms.Length; index++)
                forms[index] = Application.OpenForms[index];
            foreach (Form form in forms) Apply(form);
        }

        private static void Form_HandleCreated(object sender, EventArgs e)
        {
            if (sender is Form form) ApplyTitleBar(form);
        }

        private static void ApplyControl(Control control, bool dark)
        {
            switch (control)
            {
                case Form form:
                    form.BackColor = dark ? DarkBackground : SystemColors.Control;
                    form.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                case GroupBox groupBox:
                    groupBox.BackColor = dark ? DarkSurface : SystemColors.Control;
                    groupBox.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    break;

                case Button button:
                    ApplyButton(button, dark);
                    break;

                case TextBoxBase textBox:
                    textBox.BackColor = dark ? DarkInput : SystemColors.Window;
                    textBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    textBox.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case ComboBox comboBox:
                    comboBox.BackColor = dark ? DarkInput : SystemColors.Window;
                    comboBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    comboBox.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    break;

                case ListBox listBox:
                    listBox.BackColor = dark ? DarkInput : SystemColors.Window;
                    listBox.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    listBox.BorderStyle = dark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    break;

                case TrackBar trackBar:
                    trackBar.BackColor = dark
                        ? trackBar.Parent?.BackColor ?? DarkBackground
                        : SystemColors.Control;
                    trackBar.ForeColor = dark ? DarkAccent : SystemColors.ControlText;
                    break;

                case CheckBox checkBox:
                    checkBox.BackColor = dark
                        ? checkBox.Parent?.BackColor ?? DarkBackground
                        : SystemColors.Control;
                    checkBox.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    checkBox.UseVisualStyleBackColor = !dark;
                    break;

                case LinkLabel linkLabel:
                    linkLabel.BackColor = dark
                        ? linkLabel.Parent?.BackColor ?? DarkBackground
                        : SystemColors.Control;
                    linkLabel.ForeColor = dark ? DarkAccent : SystemColors.ControlText;
                    linkLabel.LinkColor = dark ? DarkAccent : SystemColors.HotTrack;
                    linkLabel.ActiveLinkColor = dark ? DarkText : Color.Red;
                    break;

                case Label label:
                    label.BackColor = dark
                        ? label.Parent?.BackColor ?? DarkBackground
                        : SystemColors.Control;
                    label.ForeColor = string.Equals(label.Name, "errorLabel", StringComparison.OrdinalIgnoreCase)
                        ? dark ? DarkError : Color.Red
                        : dark ? DarkMutedText : SystemColors.ControlText;
                    break;

                default:
                    if (control is Panel or UserControl)
                    {
                        control.BackColor = dark ? DarkSurface : SystemColors.Control;
                        control.ForeColor = dark ? DarkText : SystemColors.ControlText;
                    }
                    break;
            }

            foreach (Control child in control.Controls)
                ApplyControl(child, dark);
        }

        private static void ApplyButton(Button button, bool dark)
        {
            button.UseVisualStyleBackColor = !dark;
            button.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
            button.BackColor = dark ? DarkControl : SystemColors.Control;
            button.ForeColor = dark ? DarkText : SystemColors.ControlText;

            if (dark)
            {
                button.FlatAppearance.BorderColor = DarkBorder;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = DarkHover;
                button.FlatAppearance.MouseDownBackColor = DarkPressed;
            }
            else
            {
                button.FlatAppearance.BorderColor = Color.Empty;
                button.FlatAppearance.MouseOverBackColor = Color.Empty;
                button.FlatAppearance.MouseDownBackColor = Color.Empty;
            }
        }

        private static void ApplyTitleBar(Form form)
        {
            if (!OperatingSystem.IsWindows() || !form.IsHandleCreated) return;

            int enabled = Properties.DarkTheme ? 1 : 0;
            try
            {
                int result = DwmSetWindowAttribute(
                    form.Handle,
                    DwmUseImmersiveDarkMode,
                    ref enabled,
                    sizeof(int));

                if (result != 0)
                {
                    DwmSetWindowAttribute(
                        form.Handle,
                        DwmUseImmersiveDarkModeBefore20H1,
                        ref enabled,
                        sizeof(int));
                }
            }
            catch (DllNotFoundException)
            {
                // Older Windows versions do not expose DWM dark title bars.
            }
            catch (EntryPointNotFoundException)
            {
                // Keep the themed client area even when the title-bar API is unavailable.
            }
        }
    }
}
