using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CS_Jukebox
{
    public static class Logger
    {
        private static readonly object lockObj = new object();
        private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "debug_log.txt");

        static Logger()
        {
            try
            {
                File.WriteAllText(LogFilePath, $"=== CS-Jukebox Trace Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ==={Environment.NewLine}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to initialize Logger: " + ex.Message);
            }
        }

        public static void Log(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string className = Path.GetFileNameWithoutExtension(filePath);
            string threadId = Thread.CurrentThread.ManagedThreadId.ToString("D2");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string formattedMessage = $"[{timestamp}] [T{threadId}] [{className}.{memberName}:{lineNumber}] {message}";

            lock (lockObj)
            {
                try
                {
                    File.AppendAllText(LogFilePath, formattedMessage + Environment.NewLine);
                }
                catch
                {
                    // Fail silently if unable to write log
                }
            }
        }

        public static void LogEntry(string details = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string msg = string.IsNullOrWhiteSpace(details) ? "ENTER" : $"ENTER | {details}";
            Log(msg, memberName, filePath, lineNumber);
        }

        public static void LogExit(string details = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string msg = string.IsNullOrWhiteSpace(details) ? "EXIT" : $"EXIT | {details}";
            Log(msg, memberName, filePath, lineNumber);
        }

        public static void LogEvent(string eventName, string payload = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string msg = string.IsNullOrWhiteSpace(payload) ? $"EVENT [{eventName}]" : $"EVENT [{eventName}] {payload}";
            Log(msg, memberName, filePath, lineNumber);
        }

        public static void LogError(string context, Exception ex,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            string msg = $"CATCH [{context}] {ex.GetType().Name}: {ex.Message}";
            Log(msg, memberName, filePath, lineNumber);
        }
    }
}
