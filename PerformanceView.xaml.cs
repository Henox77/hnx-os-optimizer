using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    public partial class PerformanceView : UserControl
    {
        private bool _isInitializing = true;

        public PerformanceView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            try
            {
                await QueryCurrentStateAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading Performance settings state", ex);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async System.Threading.Tasks.Task QueryCurrentStateAsync()
        {
            // Query Power Plan
            string activePowerScheme = GetActivePowerScheme();
            foreach (ComboBoxItem item in ComboPowerPlans.Items)
            {
                if (item.Tag?.ToString() == activePowerScheme)
                {
                    ComboPowerPlans.SelectedItem = item;
                    break;
                }
            }

            // Query Game Mode
            bool gameModeEnabled = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar"))
            {
                if (key != null)
                {
                    var val = key.GetValue("AutoGameModeEnabled");
                    if (val != null) gameModeEnabled = Convert.ToInt32(val) == 1;
                }
            }
            ToggleGameMode.IsChecked = gameModeEnabled;

            // Query Visual Effects (Check if disabled)
            bool visualEffectsDisabled = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
            {
                if (key != null)
                {
                    var val = key.GetValue("VisualEffectsMask");
                    if (val != null) visualEffectsDisabled = Convert.ToInt32(val) == 2;
                }
            }
            ToggleVisualEffects.IsChecked = visualEffectsDisabled;

            // Query Service States (Check if Disabled)
            ToggleSysMain.IsChecked = IsServiceDisabled("SysMain");
            ToggleWSearch.IsChecked = IsServiceDisabled("WSearch");
            ToggleUpdates.IsChecked = IsServiceDisabled("wuauserv");
            ToggleXbox.IsChecked = IsServiceDisabled("XblAuthManager");
            TogglePrintSpooler.IsChecked = IsServiceDisabled("Spooler");
            ToggleDiagTrack.IsChecked = IsServiceDisabled("DiagTrack");
        }

        #region State Check Helpers
        private bool IsServiceDisabled(string serviceName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("Start");
                        if (val != null)
                        {
                            return Convert.ToInt32(val) == 4; // 4 = Disabled
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private string GetActivePowerScheme()
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
                        if (output.Contains("GUID:"))
                        {
                            int index = output.IndexOf("GUID:") + 5;
                            return output.Substring(index, 38).Trim();
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
        }
        #endregion

        #region Event Handlers
        private async void ComboPowerPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ComboPowerPlans.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                string guid = selectedItem.Tag.ToString()!;
                await OptimizationEngine.ApplyPowerPlanAsync(guid);
            }
        }

        private async void ToggleGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleGameMode.IsChecked == true;
            await OptimizationEngine.ToggleGameModeAsync(isChecked);
        }

        private async void ToggleVisualEffects_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleVisualEffects.IsChecked == true;
            await OptimizationEngine.ToggleVisualEffectsAsync(isChecked);
        }

        private async void ToggleSysMain_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleSysMain.IsChecked == true;
            await OptimizationEngine.ToggleServiceAsync("SysMain", isChecked);
        }

        private async void ToggleWSearch_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleWSearch.IsChecked == true;
            await OptimizationEngine.ToggleServiceAsync("WSearch", isChecked);
        }

        private async void ToggleUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleUpdates.IsChecked == true;
            await OptimizationEngine.ToggleServiceAsync("wuauserv", isChecked);
            await OptimizationEngine.ToggleServiceAsync("BITS", isChecked);
        }

        private async void ToggleXbox_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleXbox.IsChecked == true;
            string[] xboxServices = { "XboxGipSvc", "XblAuthManager", "XblGameSave", "XboxNetApiSvc" };
            foreach (var svc in xboxServices)
            {
                await OptimizationEngine.ToggleServiceAsync(svc, isChecked);
            }
        }

        private async void TogglePrintSpooler_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = TogglePrintSpooler.IsChecked == true;
            await OptimizationEngine.ToggleServiceAsync("Spooler", isChecked);
        }

        private async void ToggleDiagTrack_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool isChecked = ToggleDiagTrack.IsChecked == true;
            await OptimizationEngine.ToggleServiceAsync("DiagTrack", isChecked);
            await OptimizationEngine.ToggleServiceAsync("dmwappushservice", isChecked);
        }
        #endregion
    }
}
