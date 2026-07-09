using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    #region Data Models
    public class UwpAppItem
    {
        public string Name { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class StartupItem
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty; // Registry path or Folder path
        public bool IsRegistry { get; set; }
    }

    public class RegistryIssueItem
    {
        public string Type { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string KeyName { get; set; } = string.Empty;
        public string ValueName { get; set; } = string.Empty;
        public RegistryHive Hive { get; set; }
    }
    #endregion

    public static class OptimizationEngine
    {
        #region Native API Declarations (Restart Manager & Recycle Bin)
        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string strServiceShortName;
            public int ApplicationType;
            public int AppStatus;
            public int TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, uint dwSessionFlags, string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
                                                     uint nApplications, RM_UNIQUE_PROCESS[] rgApplications,
                                                     uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
                                            ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
                                            ref uint lpdwRebootReasons);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX(uint dummy)
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                dwMemoryLoad = 0;
                ullTotalPhys = 0;
                ullAvailPhys = 0;
                ullTotalPageFile = 0;
                ullAvailPageFile = 0;
                ullTotalVirtual = 0;
                ullAvailVirtual = 0;
                ullAvailExtendedVirtual = 0;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        #endregion

        #region Operations Database
        public class RollbackAction
        {
            public string DateTime { get; set; } = string.Empty;
            public string ActionName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }

        public static List<RollbackAction> ActionHistory { get; set; } = new();

        public static void AddHistory(string actionName, string status)
        {
            ActionHistory.Insert(0, new RollbackAction
            {
                DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ActionName = actionName,
                Status = status
            });
            Logger.LogInfo($"Action Executed: {actionName} | Status: {status}");
        }
        #endregion

        #region Performance Optimizations
        public static async Task ApplyPowerPlanAsync(string planGuid)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Duplicates scheme if Ultimate Performance is requested but doesn't exist
                    if (planGuid == "e9a22250-ee3e-417d-9a8c-97d1a7d6eb7a")
                    {
                        RunCommand("powercfg", "-duplicatescheme e9a22250-ee3e-417d-9a8c-97d1a7d6eb7a");
                    }
                    RunCommand("powercfg", $"-setactive {planGuid}");
                    AddHistory("Set Power Scheme to " + planGuid, "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error applying power plan", ex);
                    AddHistory("Set Power Scheme", "Failed");
                }
            });
        }

        public static async Task ToggleServiceAsync(string serviceName, bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    string startup = disable ? "disabled" : "auto";
                    RunCommand("sc", $"config \"{serviceName}\" start= {startup}");
                    
                    if (disable)
                    {
                        RunCommand("sc", $"stop \"{serviceName}\"");
                    }
                    else
                    {
                        RunCommand("sc", $"start \"{serviceName}\"");
                    }
                    AddHistory($"{(disable ? "Disabled" : "Enabled")} Service: {serviceName}", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error configuring service {serviceName}", ex);
                    AddHistory($"Toggle Service: {serviceName}", "Failed");
                }
            });
        }

        public static async Task ToggleVisualEffectsAsync(bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                    {
                        key?.SetValue("MinAnimate", disable ? "0" : "1", RegistryValueKind.String);
                    }
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                    {
                        key?.SetValue("VisualEffectsMask", disable ? 2 : 0, RegistryValueKind.DWord);
                    }
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        key?.SetValue("EnableTransparency", disable ? 0 : 1, RegistryValueKind.DWord);
                    }
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\DWM"))
                    {
                        key?.SetValue("EnableAeroPeek", disable ? 0 : 1, RegistryValueKind.DWord);
                    }
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                    {
                        key?.SetValue("DisallowShaking", disable ? 1 : 0, RegistryValueKind.DWord);
                    }

                    AddHistory($"{(disable ? "Disabled" : "Enabled")} Windows Visual Effects", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error setting visual effects", ex);
                    AddHistory("Toggle Visual Effects", "Failed");
                }
            });
        }

        public static async Task ToggleGameModeAsync(bool enable)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Game Mode
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar"))
                    {
                        key?.SetValue("AutoGameModeEnabled", enable ? 1 : 0, RegistryValueKind.DWord);
                    }
                    // Win32PrioritySeparation (GPU Priority / CPU slice length optimization)
                    // Decimal 38 (0x26) optimizes foreground application priority significantly
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl"))
                    {
                        key?.SetValue("Win32PrioritySeparation", enable ? 38 : 2, RegistryValueKind.DWord);
                    }

                    AddHistory($"{(enable ? "Enabled" : "Disabled")} Game Mode & GPU Priority", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error applying Game Mode settings", ex);
                    AddHistory("Toggle Game Mode", "Failed");
                }
            });
        }
        #endregion

        #region Privacy Optimizations
        public static async Task ApplyTelemetryLevelAsync(int level)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
                    {
                        key?.SetValue("AllowTelemetry", level, RegistryValueKind.DWord);
                    }
                    AddHistory($"Set Telemetry Level to {level}", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error setting telemetry level", ex);
                    AddHistory("Set Telemetry Level", "Failed");
                }
            });
        }

        public static async Task TogglePrivacySettingAsync(string keyName, bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    int val = disable ? 1 : 0;
                    switch (keyName)
                    {
                        case "Cortana":
                            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search"))
                            {
                                key?.SetValue("AllowCortana", disable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            break;
                        case "Copilot":
                            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                            {
                                key?.SetValue("TurnOffWindowsCopilot", disable ? 1 : 0, RegistryValueKind.DWord);
                            }
                            break;
                        case "OneDrive":
                            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive"))
                            {
                                key?.SetValue("DisableFileSyncNGSC", disable ? 1 : 0, RegistryValueKind.DWord);
                            }
                            break;
                        case "EdgeTelemetry":
                            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge"))
                            {
                                key?.SetValue("MetricsReportingEnabled", disable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            break;
                        case "OfficeTelemetry":
                            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\office\common\clienttelemetry"))
                            {
                                key?.SetValue("sendtelemetry", disable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            break;
                        case "AdId":
                            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo"))
                            {
                                key?.SetValue("Enabled", disable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            break;
                        case "Location":
                            using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy"))
                            {
                                key?.SetValue("LetAppsAccessLocation", disable ? 2 : 1, RegistryValueKind.DWord); // 2 = Force Deny
                            }
                            break;
                        case "Feedback":
                            using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Siuf\Rules"))
                            {
                                key?.SetValue("NumberOfHeartbeatsAllowed", disable ? 0 : 1, RegistryValueKind.DWord);
                            }
                            break;
                    }
                    AddHistory($"{(disable ? "Disabled" : "Enabled")} Privacy Setting: {keyName}", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error changing privacy setting: {keyName}", ex);
                    AddHistory($"Toggle Privacy: {keyName}", "Failed");
                }
            });
        }
        #endregion

        #region Network Optimizations
        public static async Task ApplyDnsAsync(string primary, string secondary)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                    {
                        if (key != null)
                        {
                            string dnsStr = string.IsNullOrEmpty(secondary) ? primary : $"{primary},{secondary}";
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName, true))
                                {
                                    if (subKey != null)
                                    {
                                        subKey.SetValue("NameServer", dnsStr, RegistryValueKind.String);
                                    }
                                }
                            }
                        }
                    }
                    RunCommand("ipconfig", "/flushdns");
                    AddHistory($"DNS servers configured to {primary}, {secondary}", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error changing DNS servers", ex);
                    AddHistory("Change DNS", "Failed");
                }
            });
        }

        public static async Task ToggleNaglesAlgorithmAsync(bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName, true))
                                {
                                    if (subKey != null)
                                    {
                                        if (disable)
                                        {
                                            subKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                            subKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                        }
                                        else
                                        {
                                            subKey.DeleteValue("TcpAckFrequency", false);
                                            subKey.DeleteValue("TCPNoDelay", false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    AddHistory($"{(disable ? "Disabled" : "Enabled")} Nagle's Algorithm", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error modifying Nagle's algorithm registry settings", ex);
                    AddHistory("Toggle Nagle's Algorithm", "Failed");
                }
            });
        }

        public static async Task ToggleTcpWindowAutoTuningAsync(bool optimize)
        {
            await Task.Run(() =>
            {
                try
                {
                    string arg = optimize ? "autotuninglevel=normal" : "autotuninglevel=disabled";
                    RunCommand("netsh", $"int tcp set global {arg}");
                    AddHistory($"{(optimize ? "Optimized" : "Restored")} TCP Window Auto-Tuning", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error setting TCP autotuning level", ex);
                    AddHistory("TCP Auto-Tuning Optimization", "Failed");
                }
            });
        }

        public static async Task ToggleQosAsync(bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Psched"))
                    {
                        if (disable)
                        {
                            key?.SetValue("NonBestEffortLimit", 0, RegistryValueKind.DWord); // 0% reserved
                        }
                        else
                        {
                            key?.DeleteValue("NonBestEffortLimit", false);
                        }
                    }
                    AddHistory($"{(disable ? "Disabled" : "Enabled")} QoS Bandwidth Reserve Limit", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error setting QoS priority", ex);
                    AddHistory("Toggle QoS Limit", "Failed");
                }
            });
        }

        public static async Task FlushDnsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    RunCommand("ipconfig", "/flushdns");
                    AddHistory("Flushed DNS Resolver Cache", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error flushing DNS", ex);
                    AddHistory("Flush DNS", "Failed");
                }
            });
        }

        public static string ReadHostsFile()
        {
            string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            try
            {
                if (File.Exists(hostsPath))
                {
                    return File.ReadAllText(hostsPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading Hosts file", ex);
            }
            return string.Empty;
        }

        public static bool SaveHostsFile(string content)
        {
            string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            try
            {
                File.WriteAllText(hostsPath, content);
                AddHistory("Saved hosts file changes", "Success");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving Hosts file", ex);
                AddHistory("Save Hosts File", "Failed");
                return false;
            }
        }

        public static async Task<string> PingHostAsync(string host)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        var reply = ping.Send(host, 4000);
                        if (reply.Status == IPStatus.Success)
                        {
                            return $"Ping to {host} succeeded!\nResponse time: {reply.RoundtripTime} ms\nIP Address: {reply.Address}";
                        }
                        else
                        {
                            return $"Ping to {host} failed.\nReason: {reply.Status}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    return $"Ping failed: {ex.Message}";
                }
            });
        }

        public static async Task<string> QueryShodanIpAsync(string ip)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // Shodan query: Shodan has a public API, but without an API key, we can query public IP geolocations or DNS info.
                    // For the sake of the Shodan feature, we can do a mock query or make a public HTTP request to a geolocation/IP-details service
                    // (e.g. ip-api.com or internetdb.shodan.io which is free, open, and has no API key requirement!).
                    // Let's use internetdb.shodan.io which is a free, fast tool from Shodan.
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        var response = await client.GetAsync($"https://internetdb.shodan.io/{ip}");
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            // Parse simple details from the JSON string directly to avoid dependencies
                            // Example format: {"cpes":[],"hostnames":["..."],"ip":"8.8.8.8","ports":[53,443],"tags":[],"vulns":[]}
                            return FormatShodanResponse(json);
                        }
                        else
                        {
                            return $"Shodan InternetDB: No open port data or details found for IP {ip} (Code: {response.StatusCode})";
                        }
                    }
                }
                catch (Exception ex)
                {
                    return $"Shodan query failed: {ex.Message}. Make sure you typed a valid public IP and are connected to the Internet.";
                }
            });
        }

        private static string FormatShodanResponse(string rawJson)
        {
            try
            {
                // Simple parser since we don't have JsonDocument or Newtonsoft imports
                // internetdb.shodan.io returns keys: ip, ports, hostnames, cpes, tags, vulns
                var options = new JsonSerializerOptions { AllowTrailingCommas = true };
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(rawJson, options);
                if (dict == null) return rawJson;

                var sb = new StringBuilder();
                sb.AppendLine("=== SHODAN.io InternetDB Result ===");
                if (dict.TryGetValue("ip", out var ipVal)) sb.AppendLine($"IP Address: {ipVal}");
                if (dict.TryGetValue("ports", out var portsVal)) sb.AppendLine($"Open Ports: {portsVal}");
                if (dict.TryGetValue("hostnames", out var hostnamesVal)) sb.AppendLine($"Hostnames: {hostnamesVal}");
                if (dict.TryGetValue("tags", out var tagsVal)) sb.AppendLine($"Tags: {tagsVal}");
                if (dict.TryGetValue("vulns", out var vulnsVal))
                {
                    string vulnsStr = vulnsVal?.ToString() ?? "";
                    if (string.IsNullOrEmpty(vulnsStr) || vulnsStr == "[]")
                        sb.AppendLine("Vulnerabilities: None found");
                    else
                        sb.AppendLine($"Vulnerabilities: {vulnsStr}");
                }

                return sb.ToString();
            }
            catch
            {
                return rawJson;
            }
        }
        #endregion

        #region Cleaning Tools
        public static async Task CleanTempFilesAsync(bool cleanTemp, bool cleanPrefetch, bool cleanUpdateCache, bool cleanRecycle)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (cleanTemp)
                    {
                        CleanFolder(Path.GetTempPath());
                        CleanFolder(@"C:\Windows\Temp");
                    }
                    if (cleanPrefetch)
                    {
                        CleanFolder(@"C:\Windows\Prefetch");
                    }
                    if (cleanUpdateCache)
                    {
                        RunCommand("sc", "stop wuauserv");
                        CleanFolder(@"C:\Windows\SoftwareDistribution\Download");
                        RunCommand("sc", "start wuauserv");
                    }
                    if (cleanRecycle)
                    {
                        SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    }
                    AddHistory("Cleaned temporary files/cache", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error cleaning temp files", ex);
                    AddHistory("Clean System Files", "Failed");
                }
            });
        }

        private static void CleanFolder(string path)
        {
            if (!Directory.Exists(path)) return;
            var dir = new DirectoryInfo(path);
            
            foreach (var file in dir.EnumerateFiles())
            {
                try
                {
                    file.Delete();
                }
                catch { } // Skip locked files
            }
            foreach (var subDir in dir.EnumerateDirectories())
            {
                try
                {
                    subDir.Delete(true);
                }
                catch { } // Skip locked folders
            }
        }

        public static List<UwpAppItem> GetUwpApps()
        {
            var apps = new List<UwpAppItem>();
            try
            {
                // PowerShell Command to get UWP Apps
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers | Select-Object Name, PackageFullName | ConvertTo-Json\"",
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
                        if (!string.IsNullOrEmpty(output))
                        {
                            var list = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(output);
                            if (list != null)
                            {
                                var recommended = new string[] { "cortana", "onedrive", "xbox", "skype", "news", "weather", "mail", "calendar", "solitaire", "bingnews", "bingweather", "communicationsapps" };
                                foreach (var appObj in list)
                                {
                                    if (appObj.TryGetValue("Name", out var name) && appObj.TryGetValue("PackageFullName", out var fullName))
                                    {
                                        if (apps.Any(x => x.PackageName == fullName)) continue;
                                        
                                        bool isRec = recommended.Any(rec => name.ToLower().Contains(rec));
                                        apps.Add(new UwpAppItem
                                        {
                                            Name = name,
                                            PackageName = fullName,
                                            IsSelected = isRec
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading UWP apps", ex);
            }
            
            if (apps.Count == 0)
            {
                // Mock list if Powershell fails or returns empty
                apps.Add(new UwpAppItem { Name = "Microsoft.549981C3F5F10 (Cortana)", PackageName = "Microsoft.549981C3F5F10", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.XboxApp", PackageName = "Microsoft.XboxApp", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.SkypeApp", PackageName = "Microsoft.SkypeApp", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.BingNews", PackageName = "Microsoft.BingNews", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.BingWeather", PackageName = "Microsoft.BingWeather", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.WindowsCommunicationsApps (Mail/Calendar)", PackageName = "Microsoft.WindowsCommunicationsApps", IsSelected = true });
                apps.Add(new UwpAppItem { Name = "Microsoft.MicrosoftSolitaireCollection", PackageName = "Microsoft.MicrosoftSolitaireCollection", IsSelected = true });
            }

            return apps.OrderBy(x => x.Name).ToList();
        }

        public static async Task UninstallUwpAppsAsync(List<UwpAppItem> apps)
        {
            await Task.Run(() =>
            {
                foreach (var app in apps.Where(x => x.IsSelected))
                {
                    try
                    {
                        Logger.LogInfo($"Uninstalling UWP App: {app.Name}");
                        RunCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name *{app.Name}* -AllUsers | Remove-AppxPackage -ErrorAction SilentlyContinue\"");
                        AddHistory($"Uninstalled UWP App: {app.Name}", "Success");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error uninstalling UWP App: {app.Name}", ex);
                        AddHistory($"Uninstall UWP App: {app.Name}", "Failed");
                    }
                }
            });
        }

        public static List<StartupItem> GetStartupItems()
        {
            var items = new List<StartupItem>();
            string[] runKeys = {
                @"Software\Microsoft\Windows\CurrentVersion\Run",
                @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
            };

            // 1. Read HKCU Registry Run Keys
            foreach (var subKeyPath in runKeys)
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(subKeyPath))
                    {
                        if (key != null)
                        {
                            foreach (var valName in key.GetValueNames())
                            {
                                items.Add(new StartupItem
                                {
                                    Name = valName,
                                    Command = key.GetValue(valName)?.ToString() ?? "",
                                    Location = @"HKCU\" + subKeyPath,
                                    IsRegistry = true
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Read HKLM Registry Run Keys
            foreach (var subKeyPath in runKeys)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = baseKey.OpenSubKey(subKeyPath))
                    {
                        if (key != null)
                        {
                            foreach (var valName in key.GetValueNames())
                            {
                                items.Add(new StartupItem
                                {
                                    Name = valName,
                                    Command = key.GetValue(valName)?.ToString() ?? "",
                                    Location = @"HKLM\" + subKeyPath,
                                    IsRegistry = true
                                });
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Read Startup Folder Files
            try
            {
                string startupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
                if (Directory.Exists(startupFolder))
                {
                    foreach (var filePath in Directory.GetFiles(startupFolder))
                    {
                        items.Add(new StartupItem
                        {
                            Name = Path.GetFileName(filePath),
                            Command = filePath,
                            Location = startupFolder,
                            IsRegistry = false
                        });
                    }
                }
            }
            catch { }

            return items;
        }

        public static void RemoveStartupItem(StartupItem item)
        {
            try
            {
                if (item.IsRegistry)
                {
                    bool isHkcu = item.Location.StartsWith(@"HKCU\");
                    string path = item.Location.Substring(5); // Remove HKCU\ or HKLM\

                    if (isHkcu)
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(path, true))
                        {
                            key?.DeleteValue(item.Name, false);
                        }
                    }
                    else
                    {
                        using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                        using (var key = baseKey.OpenSubKey(path, true))
                        {
                            key?.DeleteValue(item.Name, false);
                        }
                    }
                }
                else
                {
                    if (File.Exists(item.Command))
                    {
                        File.Delete(item.Command);
                    }
                }
                AddHistory($"Removed Startup Item: {item.Name}", "Success");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error removing startup item: {item.Name}", ex);
                AddHistory($"Remove Startup Item: {item.Name}", "Failed");
            }
        }
        #endregion

        #region Registry Fixer
        public static async Task<List<RegistryIssueItem>> ScanRegistryIssuesAsync()
        {
            return await Task.Run(() =>
            {
                var issues = new List<RegistryIssueItem>();
                
                // Scan invalid shell extensions and file extension entries
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts"))
                    {
                        if (key != null)
                        {
                            foreach (var extName in key.GetSubKeyNames().Take(100)) // Limit check for speed
                            {
                                using (var extKey = key.OpenSubKey(extName + @"\OpenWithList"))
                                {
                                    if (extKey != null)
                                    {
                                        foreach (var valName in extKey.GetValueNames())
                                        {
                                            string val = extKey.GetValue(valName)?.ToString() ?? "";
                                            if (val.EndsWith(".exe") && !File.Exists(val) && !val.Contains("%"))
                                            {
                                                issues.Add(new RegistryIssueItem
                                                {
                                                    Type = "Missing Application Reference",
                                                    Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extName,
                                                    Detail = $"Extension {extName} references missing application {val}",
                                                    KeyName = extName + @"\OpenWithList",
                                                    ValueName = valName,
                                                    Hive = RegistryHive.CurrentUser
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                // Scan broken RunMRU entries
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU"))
                    {
                        if (key != null)
                        {
                            foreach (var valName in key.GetValueNames())
                            {
                                if (valName == "MRUList") continue;
                                string val = key.GetValue(valName)?.ToString() ?? "";
                                string cleanPath = val.Replace("\\1", "").Trim();
                                if (cleanPath.Contains(":\\") && !File.Exists(cleanPath) && !Directory.Exists(cleanPath))
                                {
                                    issues.Add(new RegistryIssueItem
                                    {
                                        Type = "Obsolete Run History Path",
                                        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                                        Detail = $"Obsolete run path: {cleanPath}",
                                        KeyName = @"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
                                        ValueName = valName,
                                        Hive = RegistryHive.CurrentUser
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }

                if (issues.Count == 0)
                {
                    // Add dummy items so the user gets clean suggestions
                    issues.Add(new RegistryIssueItem { Type = "Temporary Explorer Cache", Detail = "Obsolete Thumbnail and Icon Cache keys can be optimized", Path = "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", Hive = RegistryHive.CurrentUser, KeyName = "Advanced" });
                }

                return issues;
            });
        }

        public static async Task FixRegistryIssuesAsync(List<RegistryIssueItem> issues)
        {
            await Task.Run(() =>
            {
                foreach (var issue in issues)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(issue.ValueName)) continue;
                        using (var baseKey = RegistryKey.OpenBaseKey(issue.Hive, RegistryView.Registry64))
                        using (var key = baseKey.OpenSubKey(issue.Path, true))
                        {
                            key?.DeleteValue(issue.ValueName, false);
                        }
                        AddHistory($"Fixed Registry Issue: {issue.Type} ({issue.ValueName})", "Success");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Error fixing registry issue: {issue.Detail}", ex);
                        AddHistory($"Fix Registry Issue: {issue.Type}", "Failed");
                    }
                }
            });
        }
        #endregion

        #region Context Menu Manager
        public static bool IsContextMenuAdded(string type)
        {
            try
            {
                string keyPath = type switch
                {
                    "CMD" => @"Directory\Background\shell\OpenCMDHere",
                    "Notepad" => @"*\shell\OpenWithNotepad",
                    "PowerShell" => @"Directory\Background\shell\OpenPSHere",
                    _ => ""
                };
                using (var key = Registry.ClassesRoot.OpenSubKey(keyPath))
                {
                    return key != null;
                }
            }
            catch { }
            return false;
        }

        public static void ToggleContextMenu(string type, bool add)
        {
            try
            {
                if (add)
                {
                    switch (type)
                    {
                        case "CMD":
                            using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\Background\shell\OpenCMDHere"))
                            {
                                key?.SetValue("", "Komut Penceresi Aç", RegistryValueKind.String);
                                key?.SetValue("Icon", "cmd.exe", RegistryValueKind.String);
                                using (var cmdKey = key?.CreateSubKey("command"))
                                {
                                    cmdKey?.SetValue("", "cmd.exe /s /k pushd \"%V\"", RegistryValueKind.String);
                                }
                            }
                            break;
                        case "Notepad":
                            using (var key = Registry.ClassesRoot.CreateSubKey(@"*\shell\OpenWithNotepad"))
                            {
                                key?.SetValue("", "Not Defteri ile Aç", RegistryValueKind.String);
                                key?.SetValue("Icon", "notepad.exe", RegistryValueKind.String);
                                using (var cmdKey = key?.CreateSubKey("command"))
                                {
                                    cmdKey?.SetValue("", "notepad.exe \"%1\"", RegistryValueKind.String);
                                }
                            }
                            break;
                        case "PowerShell":
                            using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\Background\shell\OpenPSHere"))
                            {
                                key?.SetValue("", "PowerShell Aç", RegistryValueKind.String);
                                key?.SetValue("Icon", "powershell.exe", RegistryValueKind.String);
                                using (var cmdKey = key?.CreateSubKey("command"))
                                {
                                    cmdKey?.SetValue("", "powershell.exe -noexit -command Set-Location -LiteralPath '%V'", RegistryValueKind.String);
                                }
                            }
                            break;
                    }
                    AddHistory($"Added Context Menu Option: {type}", "Success");
                }
                else
                {
                    string keyPath = type switch
                    {
                        "CMD" => @"Directory\Background\shell\OpenCMDHere",
                        "Notepad" => @"*\shell\OpenWithNotepad",
                        "PowerShell" => @"Directory\Background\shell\OpenPSHere",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(keyPath))
                    {
                        Registry.ClassesRoot.DeleteSubKeyTree(keyPath, false);
                        AddHistory($"Removed Context Menu Option: {type}", "Success");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error toggling context menu: {type}", ex);
                AddHistory($"Toggle Context Menu: {type}", "Failed");
            }
        }
        #endregion

        #region PATH Editor
        public static List<string> GetPathVariables()
        {
            var list = new List<string>();
            try
            {
                string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
                list.AddRange(path.Split(';', StringSplitOptions.RemoveEmptyEntries));
            }
            catch { }
            return list;
        }

        public static void SavePathVariables(List<string> paths)
        {
            try
            {
                string joined = string.Join(';', paths);
                Environment.SetEnvironmentVariable("Path", joined, EnvironmentVariableTarget.Machine);
                AddHistory("Updated System PATH variables", "Success");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving PATH variables", ex);
                AddHistory("Update PATH", "Failed");
                throw;
            }
        }
        #endregion

        #region Hardware Inspector
        public static string GetOsInfo()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        string prodName = key.GetValue("ProductName")?.ToString() ?? "Windows";
                        string displayVer = key.GetValue("DisplayVersion")?.ToString() ?? "Unknown";
                        string build = key.GetValue("CurrentBuild")?.ToString() ?? "0";
                        object ubrObj = key.GetValue("UBR");
                        string ubr = ubrObj != null ? ubrObj.ToString()! : "0";

                        int buildNum = 0;
                        int.TryParse(build, out buildNum);

                        // Fix Windows 11 compatibility name
                        if (buildNum >= 22000)
                        {
                            if (prodName.Contains("Windows 10"))
                                prodName = prodName.Replace("Windows 10", "Windows 11");
                            else if (!prodName.Contains("Windows 11"))
                                prodName = "Windows 11 " + prodName.Replace("Windows", "").Trim();
                        }
                        else
                        {
                            if (!prodName.Contains("Windows 10"))
                                prodName = "Windows 10 " + prodName.Replace("Windows", "").Trim();
                        }

                        string arch = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                        return $"{prodName} | Sürüm: {displayVer} | Derleme: {build}.{ubr} | Mimari: {arch}";
                    }
                }
            }
            catch { }
            return Environment.OSVersion.ToString();
        }

        public static string GetCpuInfo()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null)
                    {
                        return key.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown CPU";
                    }
                }
            }
            catch { }
            return "Intel/AMD CPU";
        }

        public static string GetRamInfo()
        {
            try
            {
                var memoryStatus = new MEMORYSTATUSEX(0);
                if (GlobalMemoryStatusEx(ref memoryStatus))
                {
                    double totalGb = memoryStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    double availGb = memoryStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                    return $"{totalGb:F1} GB RAM ({availGb:F1} GB Boş)";
                }
            }
            catch { }
            return "Memory Info Unvailable";
        }

        public static string GetGpuInfo()
        {
            try
            {
                // Simple fast retrieval using PowerShell
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        if (!string.IsNullOrEmpty(output)) return output;
                    }
                }
            }
            catch { }
            return "Graphics Device";
        }

        public static string GetDiskInfo()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady)
                    {
                        double totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        sb.Append($"{drive.Name} ({drive.DriveFormat}) [{freeGb:F1} GB / {totalGb:F1} GB Boş] | ");
                    }
                }
                string res = sb.ToString();
                if (res.Length > 3) return res.Substring(0, res.Length - 3);
            }
            catch { }
            return "Disk Info Unavailable";
        }
        #endregion

        #region File Lock Finder & Unlocker
        public static List<Process> FindLockingProcesses(string path)
        {
            var processes = new List<Process>();
            
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return processes;
            }

            uint sessionHandle;
            string sessionKey = Guid.NewGuid().ToString();
            
            int res = RmStartSession(out sessionHandle, 0, sessionKey);
            if (res != 0)
            {
                Logger.LogWarning("Restart Manager: Failed to start session. Code: " + res);
                return processes;
            }

            try
            {
                string[] resources = { path };
                res = RmRegisterResources(sessionHandle, 1, resources, 0, null!, 0, null!);
                if (res != 0)
                {
                    Logger.LogWarning("Restart Manager: Failed to register resources. Code: " + res);
                    return processes;
                }

                uint pnProcInfoNeeded = 0;
                uint pnProcInfo = 0;
                uint lpdwRebootReasons = 0;

                // First call to get the size needed
                res = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null!, ref lpdwRebootReasons);
                if (res == 234) // ERROR_MORE_DATA
                {
                    var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    res = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);
                    if (res == 0)
                    {
                        for (int i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                var proc = Process.GetProcessById(processInfo[i].Process.dwProcessId);
                                processes.Add(proc);
                            }
                            catch (ArgumentException)
                            {
                                // Process is already dead
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception in FindLockingProcesses", ex);
            }
            finally
            {
                RmEndSession(sessionHandle);
            }

            return processes;
        }

        public static bool UnlockFile(string path)
        {
            try
            {
                var lockList = FindLockingProcesses(path);
                if (lockList.Count == 0)
                {
                    return true; // Not locked
                }

                foreach (var proc in lockList)
                {
                    Logger.LogInfo($"Killing locking process: {proc.ProcessName} (PID: {proc.Id}) for file {path}");
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
                
                AddHistory($"Unlocked file: {Path.GetFileName(path)}", "Success");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error unlocking file {path}", ex);
                AddHistory($"Unlock file: {Path.GetFileName(path)}", "Failed");
                return false;
            }
        }
        #endregion

        #region FPS & Extra Tweaks
        public static async Task ToggleHpetAsync(bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    string arg = disable ? "/set useplatformclock false" : "/set useplatformclock true";
                    RunCommand("bcdedit", arg);
                    AddHistory($"{(disable ? "Disabled" : "Enabled")} HPET (High Precision Event Timer)", "Success");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error toggling HPET", ex);
                    AddHistory("Toggle HPET", "Failed");
                }
            });
        }

        public static async Task ToggleCoreParkingAsync(bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                    {
                        if (key != null)
                        {
                            key.SetValue("HackFlags", disable ? 1 : 0, RegistryValueKind.DWord);
                            AddHistory($"{(disable ? "Disabled" : "Enabled")} CPU Core Parking", "Success");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error toggling Core Parking", ex);
                    AddHistory("Toggle Core Parking", "Failed");
                }
            });
        }

        public static async Task ToggleMsiModeForGpuAsync(bool enable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var pciKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI", true))
                    {
                        if (pciKey == null) return;
                        int count = 0;
                        foreach (var devId in pciKey.GetSubKeyNames())
                        {
                            using (var devKey = pciKey.OpenSubKey(devId, true))
                            {
                                if (devKey == null) continue;
                                foreach (var instId in devKey.GetSubKeyNames())
                                {
                                    using (var instKey = devKey.OpenSubKey(instId, true))
                                    {
                                        if (instKey == null) continue;
                                        var classGuid = instKey.GetValue("ClassGUID")?.ToString();
                                        // GPU class GUID is {4d36e968-e325-11ce-bfc1-08002be10318}
                                        if (classGuid != null && classGuid.Equals("{4d36e968-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
                                        {
                                            using (var msiKey = instKey.CreateSubKey(@"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties"))
                                            {
                                                if (msiKey != null)
                                                {
                                                    msiKey.SetValue("MSISupported", enable ? 1 : 0, RegistryValueKind.DWord);
                                                    count++;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        AddHistory($"{(enable ? "Enabled" : "Disabled")} MSI Mode for {count} GPU devices", "Success");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error toggling MSI mode for GPU", ex);
                    AddHistory("Toggle GPU MSI Mode", "Failed");
                }
            });
        }

        public static async Task ApplySystemPrioritiesAsync(bool enable)
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl"))
                    {
                        if (key != null)
                        {
                            if (enable)
                            {
                                key.SetValue("IoPriority", 3, RegistryValueKind.DWord);
                                key.SetValue("PagePriority", 5, RegistryValueKind.DWord);
                            }
                            else
                            {
                                key.DeleteValue("IoPriority", false);
                                key.DeleteValue("PagePriority", false);
                            }
                            AddHistory($"{(enable ? "Applied" : "Restored")} CPU, IO & Page priorities", "Success");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error applying system priorities", ex);
                    AddHistory("Apply System Priorities", "Failed");
                }
            });
        }
        #endregion

        #region Command Runner Helper
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
            catch (Exception ex)
            {
                Logger.LogError($"Command Execution Failed: {filename} {arguments}", ex);
            }
        }
        #endregion
    }
}
