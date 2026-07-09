using System;
using System.IO;

namespace HNXOSOptimizer
{
    public static class Logger
    {
        private static string LogFilePath = @"C:\HNX_Log.txt";
        private static readonly object LockObj = new object();

        static Logger()
        {
            try
            {
                // Try to initialize file on C:\
                string dir = Path.GetDirectoryName(LogFilePath) ?? @"C:\";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                if (!File.Exists(LogFilePath))
                {
                    File.WriteAllText(LogFilePath, $"--- HNX OS Optimizer Log File Initialized at {DateTime.Now} ---\r\n");
                }
            }
            catch (Exception)
            {
                // Fallback to local app data folder if C:\ access fails
                try
                {
                    string altPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HNX_Log.txt");
                    LogFilePath = altPath;
                    if (!File.Exists(LogFilePath))
                    {
                        File.WriteAllText(LogFilePath, $"--- HNX OS Optimizer Log File (Fallback) Initialized at {DateTime.Now} ---\r\n");
                    }
                }
                catch { }
            }
        }

        public static void Log(string message, string level = "INFO")
        {
            try
            {
                lock (LockObj)
                {
                    string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}\r\n";
                    File.AppendAllText(LogFilePath, logLine);
                }
            }
            catch
            {
                // Fail silently in production
            }
        }

        public static void LogInfo(string message) => Log(message, "INFO");
        public static void LogWarning(string message) => Log(message, "WARNING");
        public static void LogError(string message, Exception? ex = null)
        {
            string details = ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message;
            Log(details, "ERROR");
        }
    }
}
