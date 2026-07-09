using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    public class BackupModel
    {
        public string BackupTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string ActivePowerScheme { get; set; } = string.Empty;
        public Dictionary<string, int> ServiceStartupTypes { get; set; } = new();
        public Dictionary<string, string> RegistryStringSettings { get; set; } = new();
        public Dictionary<string, int> RegistryIntSettings { get; set; } = new();
        public Dictionary<string, string> AdapterDnsSettings { get; set; } = new(); // GUID -> NameServer
        public string HostsContent { get; set; } = string.Empty;
    }

    public static class BackupManager
    {
        private static readonly string BackupDir = @"C:\HNX_Backup";
        private static readonly string BackupJsonPath = Path.Combine(BackupDir, "hnx_backup.json");
        private static readonly string BackupHostsPath = Path.Combine(BackupDir, "hosts.bak");

        static BackupManager()
        {
            try
            {
                if (!Directory.Exists(BackupDir))
                {
                    Directory.CreateDirectory(BackupDir);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create backup directory", ex);
            }
        }

        public static bool CreateBackup()
        {
            Logger.LogInfo("Starting System Backup...");
            try
            {
                var model = new BackupModel();

                // 1. Backup Power Scheme
                model.ActivePowerScheme = GetActivePowerScheme();
                Logger.LogInfo($"Backed up active power scheme GUID: {model.ActivePowerScheme}");

                // 2. Backup Services Startup Types
                string[] services = { "SysMain", "DiagTrack", "dmwappushservice", "WSearch", 
                                      "XboxGipSvc", "XblAuthManager", "XblGameSave", "XboxNetApiSvc", 
                                      "Spooler", "BITS", "wuauserv" };
                foreach (var svc in services)
                {
                    int startupType = GetServiceStartupType(svc);
                    model.ServiceStartupTypes[svc] = startupType;
                }
                Logger.LogInfo("Backed up service startup configurations.");

                // 3. Backup Registry Settings (Performance, Privacy, Network)
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate"); // Wait, MinAnimate can be string
                BackupRegistryString(model, RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualEffectsMask");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisallowShaking");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation");

                // Privacy
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "MetricsReportingEnabled");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Policies\Microsoft\office\common\clienttelemetry", "sendtelemetry");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessLocation");
                BackupRegistryInt(model, RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfHeartbeatsAllowed");

                // Network QoS
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit");

                // Core Parking, IO & Page priority
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HackFlags");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "IoPriority");
                BackupRegistryInt(model, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "PagePriority");
                BackupGpuMsiSettings(model);

                // 4. Backup Network Adapters DNS Settings
                BackupNetworkDns(model);

                // 5. Backup Hosts File
                string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    model.HostsContent = File.ReadAllText(hostsPath);
                    File.Copy(hostsPath, BackupHostsPath, true);
                }
                Logger.LogInfo("Backed up Hosts file.");

                // Serialize and save to disk
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(model, options);
                File.WriteAllText(BackupJsonPath, json);

                Logger.LogInfo("System Backup completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Backup failed with exception", ex);
                return false;
            }
        }

        public static bool RestoreBackup()
        {
            Logger.LogInfo("Starting System Restore from Backup...");
            if (!File.Exists(BackupJsonPath))
            {
                Logger.LogWarning("No backup file found at: " + BackupJsonPath);
                return false;
            }

            try
            {
                string json = File.ReadAllText(BackupJsonPath);
                var model = JsonSerializer.Deserialize<BackupModel>(json);
                if (model == null)
                {
                    Logger.LogWarning("Failed to deserialize backup file.");
                    return false;
                }

                // 1. Restore Power Plan
                if (!string.IsNullOrEmpty(model.ActivePowerScheme))
                {
                    RunCommand("powercfg", $"-setactive {model.ActivePowerScheme}");
                    Logger.LogInfo($"Restored active power scheme GUID to {model.ActivePowerScheme}");
                }

                // 2. Restore Services
                foreach (var kvp in model.ServiceStartupTypes)
                {
                    RestoreServiceStartup(kvp.Key, kvp.Value);
                }
                Logger.LogInfo("Restored service startup configurations.");

                // 3. Restore Registry Settings
                foreach (var kvp in model.RegistryIntSettings)
                {
                    RestoreRegistryValue(kvp.Key, kvp.Value);
                }
                foreach (var kvp in model.RegistryStringSettings)
                {
                    RestoreRegistryValue(kvp.Key, kvp.Value);
                }
                // Restore HPET override
                RunCommand("bcdedit", "/set useplatformclock true");
                Logger.LogInfo("Restored registry settings and HPET.");

                // 4. Restore DNS Settings
                RestoreNetworkDns(model);

                // 5. Restore Hosts File
                string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(BackupHostsPath))
                {
                    File.Copy(BackupHostsPath, hostsPath, true);
                    Logger.LogInfo("Restored Hosts file from hosts.bak.");
                }
                else if (!string.IsNullOrEmpty(model.HostsContent))
                {
                    File.WriteAllText(hostsPath, model.HostsContent);
                    Logger.LogInfo("Restored Hosts file from memory content.");
                }

                Logger.LogInfo("System Restore completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Restore failed with exception", ex);
                return false;
            }
        }

        #region Helpers

        private static string GetActivePowerScheme()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        proc.WaitForExit();
                        // Format: Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e (Balanced)
                        if (output.Contains("GUID:"))
                        {
                            int index = output.IndexOf("GUID:") + 5;
                            string guid = output.Substring(index, 38).Trim();
                            return guid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error getting active power scheme", ex);
            }
            return string.Empty;
        }

        private static int GetServiceStartupType(string serviceName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("Start");
                        if (val != null) return (int)val;
                    }
                }
            }
            catch { }
            return 3; // Default manual
        }

        private static void RestoreServiceStartup(string serviceName, int startupType)
        {
            try
            {
                // Convert start type registry val to sc config params
                // 2 = auto, 3 = demand (manual), 4 = disabled
                string startParam = startupType switch
                {
                    2 => "auto",
                    3 => "demand",
                    4 => "disabled",
                    _ => "demand"
                };

                RunCommand("sc", $"config \"{serviceName}\" start= {startParam}");
                
                // Start or stop service based on type (if automatic, try start; if disabled, stop)
                if (startupType == 4)
                {
                    RunCommand("sc", $"stop \"{serviceName}\"");
                }
                else if (startupType == 2)
                {
                    RunCommand("sc", $"start \"{serviceName}\"");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error restoring service {serviceName} to {startupType}", ex);
            }
        }

        private static void BackupRegistryInt(BackupModel model, RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(valueName);
                        if (val != null && (key.GetValueKind(valueName) == RegistryValueKind.DWord || key.GetValueKind(valueName) == RegistryValueKind.QWord))
                        {
                            string fullKey = $"{hive}\\{subKey}\\{valueName}";
                            model.RegistryIntSettings[fullKey] = Convert.ToInt32(val);
                        }
                    }
                }
            }
            catch { }
        }

        private static void BackupRegistryString(BackupModel model, RegistryHive hive, string subKey, string valueName)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(subKey))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(valueName);
                        if (val != null && key.GetValueKind(valueName) == RegistryValueKind.String)
                        {
                            string fullKey = $"{hive}\\{subKey}\\{valueName}";
                            model.RegistryStringSettings[fullKey] = val.ToString() ?? "";
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreRegistryValue(string fullKey, object value)
        {
            try
            {
                string[] parts = fullKey.Split('\\');
                if (parts.Length < 3) return;

                RegistryHive hive = Enum.Parse<RegistryHive>(parts[0]);
                string valueName = parts[parts.Length - 1];
                
                // Reconstruct subkey
                List<string> subKeyParts = new List<string>();
                for (int i = 1; i < parts.Length - 1; i++)
                {
                    subKeyParts.Add(parts[i]);
                }
                string subKey = string.Join('\\', subKeyParts);

                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.CreateSubKey(subKey))
                {
                    if (key != null)
                    {
                        if (value is int intVal)
                        {
                            key.SetValue(valueName, intVal, RegistryValueKind.DWord);
                        }
                        else
                        {
                            key.SetValue(valueName, value.ToString() ?? "", RegistryValueKind.String);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error restoring registry key {fullKey}", ex);
            }
        }

        private static void BackupNetworkDns(BackupModel model)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                if (subKey != null)
                                {
                                    var nameServer = subKey.GetValue("NameServer");
                                    if (nameServer != null && !string.IsNullOrEmpty(nameServer.ToString()))
                                    {
                                        model.AdapterDnsSettings[subKeyName] = nameServer.ToString()!;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error backing up DNS parameters", ex);
            }
        }

        private static void BackupGpuMsiSettings(BackupModel model)
        {
            try
            {
                using (var pciKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI"))
                {
                    if (pciKey == null) return;
                    foreach (var devId in pciKey.GetSubKeyNames())
                    {
                        using (var devKey = pciKey.OpenSubKey(devId))
                        {
                            if (devKey == null) continue;
                            foreach (var instId in devKey.GetSubKeyNames())
                            {
                                using (var instKey = devKey.OpenSubKey(instId))
                                {
                                    if (instKey == null) continue;
                                    var classGuid = instKey.GetValue("ClassGUID")?.ToString();
                                    if (classGuid != null && classGuid.Equals("{4d36e968-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string subKeyPath = $@"SYSTEM\CurrentControlSet\Enum\PCI\{devId}\{instId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                                        using (var msiKey = instKey.OpenSubKey(@"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties"))
                                        {
                                            if (msiKey != null)
                                            {
                                                var val = msiKey.GetValue("MSISupported");
                                                if (val != null)
                                                {
                                                    string fullKey = $"LocalMachine\\{subKeyPath}\\MSISupported";
                                                    model.RegistryIntSettings[fullKey] = Convert.ToInt32(val);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreNetworkDns(BackupModel model)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                {
                    if (key != null)
                    {
                        foreach (var kvp in model.AdapterDnsSettings)
                        {
                            using (var subKey = key.OpenSubKey(kvp.Key, true))
                            {
                                if (subKey != null)
                                {
                                    subKey.SetValue("NameServer", kvp.Value, RegistryValueKind.String);
                                }
                            }
                        }
                    }
                }
                // Flush DNS resolver cache after restoring
                RunCommand("ipconfig", "/flushdns");
                Logger.LogInfo("Network DNS restored.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error restoring network DNS", ex);
            }
        }

        private static void RunCommand(string filename, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filename,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var proc = Process.Start(psi))
                {
                    proc?.WaitForExit(10000);
                }
            }
            catch { }
        }

        #endregion
    }
}
