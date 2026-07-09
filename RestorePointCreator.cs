using System;
using System.Diagnostics;

namespace HNXOSOptimizer
{
    public static class RestorePointCreator
    {
        public static bool CreateRestorePoint(string description)
        {
            Logger.LogInfo($"Attempting to create System Restore Point: '{description}'");
            try
            {
                // Disable the 24 hour restriction first in Registry
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"))
                {
                    if (key != null)
                    {
                        key.SetValue("SystemRestorePointCreationFrequency", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }

                // Call PowerShell to create the checkpoint
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description '{description}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        process.WaitForExit(45000); // Restore points can take up to 45 seconds
                        string error = process.StandardError.ReadToEnd();
                        if (process.ExitCode == 0)
                        {
                            Logger.LogInfo("System Restore Point created successfully.");
                            return true;
                        }
                        else
                        {
                            Logger.LogWarning($"PowerShell failed to create Restore Point. Code: {process.ExitCode}, Error: {error}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception while creating System Restore Point", ex);
            }
            return false;
        }
    }
}
