using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CS_Jukebox
{
    public static class WinAPI
    {
        //Used to get Handle for Foreground Window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        //Used to get ID of any Window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        public delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);
        private static readonly object ForegroundCacheLock = new object();
        private static IntPtr cachedForegroundWindow;
        private static int cachedForegroundProcessId;
        private static string cachedForegroundProcess = "";

        public static string GetActiveProcess()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            int processId = GetWindowProcessId(foregroundWindow);
            lock (ForegroundCacheLock)
            {
                if (foregroundWindow == cachedForegroundWindow && processId == cachedForegroundProcessId)
                    return cachedForegroundProcess;

                cachedForegroundWindow = foregroundWindow;
                cachedForegroundProcessId = processId;
                try
                {
                    cachedForegroundProcess = GetProcessName(foregroundWindow, processId);
                }
                catch (ArgumentException) { cachedForegroundProcess = ""; }
                catch (InvalidOperationException) { cachedForegroundProcess = ""; }
                catch (Win32Exception) { cachedForegroundProcess = ""; }
                return cachedForegroundProcess;
            }
        }

        private static string GetProcessName(IntPtr window, int processId)
        {
            if (processId <= 0) return "";

            using Process foregroundProcess = Process.GetProcessById(processId);
            string processName = foregroundProcess.ProcessName;
            if (!string.Equals(processName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                return processName;

            string childProcessName = null;
            EnumChildWindows(foregroundProcess.MainWindowHandle, (childWindow, _) =>
            {
                int childProcessId = GetWindowProcessId(childWindow);
                if (childProcessId <= 0) return true;

                try
                {
                    using Process childProcess = Process.GetProcessById(childProcessId);
                    if (!string.Equals(childProcess.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                        childProcessName = childProcess.ProcessName;
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }

                return true;
            }, IntPtr.Zero);

            return childProcessName ?? processName;
        }

        private static int GetWindowProcessId(IntPtr hwnd)
        {
            int pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

    }
}
