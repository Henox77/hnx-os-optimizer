using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    public partial class NetworkView : UserControl
    {
        private bool _isInitializing = true;

        public NetworkView()
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
                Logger.LogError("Error loading Network settings state", ex);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void QueryCurrentState()
        {
            // 1. Query active DNS
            string activeDns = GetActiveDnsFromRegistry();
            SelectDnsInCombo(activeDns);

            // 2. Query Toggles
            ToggleNagles.IsChecked = IsNaglesDisabled();
            ToggleQos.IsChecked = IsQosDisabled();
            ToggleTcpAutoTuning.IsChecked = IsTcpAutoTuningOptimized();

            // 3. Load Hosts File
            TxtHostsContent.Text = OptimizationEngine.ReadHostsFile();
        }

        #region Helpers
        private string GetActiveDnsFromRegistry()
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
                                    var ns = subKey.GetValue("NameServer");
                                    if (ns != null && !string.IsNullOrEmpty(ns.ToString()))
                                    {
                                        return ns.ToString()!; // Returns comma-separated DNS
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private void SelectDnsInCombo(string dns)
        {
            if (string.IsNullOrEmpty(dns))
            {
                ComboDns.SelectedIndex = 0;
                return;
            }

            dns = dns.Replace(" ", ""); // Remove spaces
            foreach (ComboBoxItem item in ComboDns.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (tag.Replace(" ", "") == dns)
                {
                    ComboDns.SelectedItem = item;
                    return;
                }
            }

            // If not found in defaults, set to Custom and fill fields
            ComboDns.SelectedIndex = ComboDns.Items.Count - 1; // Custom index
            string[] parts = dns.Split(',');
            if (parts.Length > 0) TxtCustomDns1.Text = parts[0];
            if (parts.Length > 1) TxtCustomDns2.Text = parts[1];
        }

        private bool IsNaglesDisabled()
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
                                    var ack = subKey.GetValue("TcpAckFrequency");
                                    var delay = subKey.GetValue("TCPNoDelay");
                                    if (ack != null && delay != null && Convert.ToInt32(ack) == 1 && Convert.ToInt32(delay) == 1)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        private bool IsQosDisabled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Psched"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("NonBestEffortLimit");
                        if (val != null) return Convert.ToInt32(val) == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool IsTcpAutoTuningOptimized()
        {
            // By default we check if auto tuning is set (we can assume true if optimized or query via netsh)
            // To keep it simple, we check if it is active. Since standard Windows has it at "normal",
            // we will default this to Checked.
            return true;
        }
        #endregion

        #region Actions
        private void ComboDns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridCustomDns == null) return;

            if (ComboDns.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "custom")
            {
                GridCustomDns.Visibility = Visibility.Visible;
            }
            else
            {
                GridCustomDns.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnApplyDns_Click(object sender, RoutedEventArgs e)
        {
            if (ComboDns.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (tag == "none") return;

                string primary = "";
                string secondary = "";

                if (tag == "custom")
                {
                    primary = TxtCustomDns1.Text.Trim();
                    secondary = TxtCustomDns2.Text.Trim();
                    if (string.IsNullOrEmpty(primary))
                    {
                        MessageBox.Show("Lütfen geçerli bir birincil DNS adresi girin.", "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    string[] parts = tag.Split(',');
                    if (parts.Length > 0) primary = parts[0];
                    if (parts.Length > 1) secondary = parts[1];
                }

                BtnApplyDns.IsEnabled = false;
                await OptimizationEngine.ApplyDnsAsync(primary, secondary);
                BtnApplyDns.IsEnabled = true;
                MessageBox.Show("DNS ayarları başarıyla güncellendi!", "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void ToggleNagles_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool disable = ToggleNagles.IsChecked == true;
            await OptimizationEngine.ToggleNaglesAlgorithmAsync(disable);
        }

        private async void ToggleTcpAutoTuning_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool optimize = ToggleTcpAutoTuning.IsChecked == true;
            await OptimizationEngine.ToggleTcpWindowAutoTuningAsync(optimize);
        }

        private async void ToggleQos_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            bool disable = ToggleQos.IsChecked == true;
            await OptimizationEngine.ToggleQosAsync(disable);
        }

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            BtnFlushDns.IsEnabled = false;
            await OptimizationEngine.FlushDnsAsync();
            BtnFlushDns.IsEnabled = true;
            MessageBox.Show("DNS önbelleği başarıyla temizlendi!", "Ağ Ayarları", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnHostsSave_Click(object sender, RoutedEventArgs e)
        {
            bool success = OptimizationEngine.SaveHostsFile(TxtHostsContent.Text);
            if (success)
            {
                MessageBox.Show("Hosts dosyası başarıyla kaydedildi!", "Hosts Düzenleyici", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Dosya kaydedilemedi. Yönetici haklarınızı kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnHostsUndo_Click(object sender, RoutedEventArgs e)
        {
            string backupHosts = @"C:\HNX_Backup\hosts.bak";
            if (File.Exists(backupHosts))
            {
                try
                {
                    string content = File.ReadAllText(backupHosts);
                    TxtHostsContent.Text = content;
                    OptimizationEngine.SaveHostsFile(content);
                    MessageBox.Show("Hosts dosyası yedekten geri yüklendi ve kaydedildi!", "Hosts Düzenleyici", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Yedek yükleme başarısız oldu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Alınmış bir Hosts yedeği bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            string host = TxtNetQuery.Text.Trim();
            if (string.IsNullOrEmpty(host)) return;

            TxtNetResult.Text = "Bağlantı sorgulanıyor (Ping)...";
            BtnPing.IsEnabled = false;
            string result = await OptimizationEngine.PingHostAsync(host);
            TxtNetResult.Text = result;
            BtnPing.IsEnabled = true;
        }

        private async void BtnShodan_Click(object sender, RoutedEventArgs e)
        {
            string ip = TxtNetQuery.Text.Trim();
            if (string.IsNullOrEmpty(ip)) return;

            TxtNetResult.Text = "Shodan InternetDB üzerinden sorgulanıyor...";
            BtnShodan.IsEnabled = false;
            string result = await OptimizationEngine.QueryShodanIpAsync(ip);
            TxtNetResult.Text = result;
            BtnShodan.IsEnabled = true;
        }
        #endregion
    }
}
