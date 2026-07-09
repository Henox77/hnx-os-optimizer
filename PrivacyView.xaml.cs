using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    public partial class PrivacyView : UserControl
    {
        private bool _isInitializing = true;

        public PrivacyView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            try
            {
                QueryCurrentState();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading Privacy settings state", ex);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void QueryCurrentState()
        {
            // 1. Query Telemetry Level
            int telemetryLevel = 3; // Default is usually Full (3)
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection"))
            {
                if (key != null)
                {
                    var val = key.GetValue("AllowTelemetry");
                    if (val != null) telemetryLevel = Convert.ToInt32(val);
                }
            }
            SliderTelemetry.Value = telemetryLevel;
            UpdateTelemetryDescription(telemetryLevel);

            // 2. Query Toggles (IsChecked means Disabled)
            ToggleCortana.IsChecked = IsSettingDisabled(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0);
            ToggleCopilot.IsChecked = IsSettingEnabled(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            ToggleOneDrive.IsChecked = IsSettingEnabled(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1);
            ToggleEdgeTelemetry.IsChecked = IsSettingDisabled(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "MetricsReportingEnabled", 0);
            ToggleOfficeTelemetry.IsChecked = IsSettingDisabled(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\office\common\clienttelemetry", "sendtelemetry", 0);
            ToggleAdId.IsChecked = IsSettingDisabled(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
            ToggleLocation.IsChecked = IsSettingEnabled(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessLocation", 2);
            ToggleFeedback.IsChecked = IsSettingDisabled(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfHeartbeatsAllowed", 0);
        }

        #region Helpers
        private bool IsSettingDisabled(RegistryHive hive, string path, string name, int disabledValue)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(name);
                        if (val != null)
                        {
                            return Convert.ToInt32(val) == disabledValue;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private bool IsSettingEnabled(RegistryHive hive, string path, string name, int enabledValue)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (var key = baseKey.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        var val = key.GetValue(name);
                        if (val != null)
                        {
                            return Convert.ToInt32(val) == enabledValue;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private void UpdateTelemetryDescription(int level)
        {
            TxtTelemetryLevelDesc.Text = level switch
            {
                0 => TranslationManager.Translate("TelemetryLvl0"),
                1 => TranslationManager.Translate("TelemetryLvl1"),
                2 => TranslationManager.Translate("TelemetryLvl2"),
                3 => TranslationManager.Translate("TelemetryLvl3"),
                _ => TranslationManager.Translate("TelemetryLvlUnknown")
            };
        }
        #endregion

        #region Events
        private async void SliderTelemetry_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing) return;
            int level = (int)e.NewValue;
            UpdateTelemetryDescription(level);
            await OptimizationEngine.ApplyTelemetryLevelAsync(level);
        }

        private async void TogglePrivacy_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (sender is ToggleButton btn && btn.Tag != null)
            {
                string key = btn.Tag.ToString()!;
                bool disable = btn.IsChecked == true;
                await OptimizationEngine.TogglePrivacySettingAsync(key, disable);
            }
        }
        #endregion
    }
}
