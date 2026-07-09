using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HNXOSOptimizer
{
    public partial class HomeView : UserControl
    {
        private readonly MainWindow _mainWindow;

        public HomeView(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            RefreshSystemInfo();
        }

        public void RefreshSystemInfo()
        {
            TxtOS.Text = "Algılanıyor...";
            TxtCPU.Text = "Algılanıyor...";
            TxtRAM.Text = "Algılanıyor...";
            TxtGPU.Text = "Algılanıyor...";
            TxtDisk.Text = "Algılanıyor...";

            Task.Run(() =>
            {
                string os = OptimizationEngine.GetOsInfo();
                string cpu = OptimizationEngine.GetCpuInfo();
                string ram = OptimizationEngine.GetRamInfo();
                string gpu = OptimizationEngine.GetGpuInfo();
                string disk = OptimizationEngine.GetDiskInfo();

                Dispatcher.Invoke(() =>
                {
                    TxtOS.Text = os;
                    TxtCPU.Text = cpu;
                    TxtRAM.Text = ram;
                    TxtGPU.Text = gpu;
                    TxtDisk.Text = disk;
                });
            });
        }

        private async void BtnQuickOpt_Click(object sender, RoutedEventArgs e)
        {
            SetButtonsState(false);
            ProgressPanel.Visibility = Visibility.Visible;
            OptProgressBar.Value = 0;
            TxtProgressPercent.Text = "0%";

            try
            {
                // 1. Restore Point
                UpdateStatus("1/6: Sistem Geri Yükleme Noktası Oluşturuluyor...", 10);
                await Task.Run(() => RestorePointCreator.CreateRestorePoint("HNX Quick Optimization"));

                // 2. Backup Settings
                UpdateStatus("2/6: Mevcut Sistem Ayarları Yedekleniyor...", 25);
                await Task.Run(() => BackupManager.CreateBackup());

                // 3. Game Mode & Priority
                UpdateStatus("3/6: Oyun Modu ve GPU Öncelikleri Aktifleştiriliyor...", 45);
                await OptimizationEngine.ToggleGameModeAsync(true);

                // 4. Visual Effects
                UpdateStatus("4/6: Görsel Efektler Optimize Ediliyor...", 65);
                await OptimizationEngine.ToggleVisualEffectsAsync(true);

                // 5. Network Optimization
                UpdateStatus("5/6: TCP/IP Ayarları ve DNS Önbelleği Temizleniyor...", 85);
                await OptimizationEngine.ToggleTcpWindowAutoTuningAsync(true);
                await OptimizationEngine.FlushDnsAsync();

                // 6. Clean Temp & Recycle Bin
                UpdateStatus("6/6: Geçici Dosyalar ve Geri Dönüşüm Kutusu Temizleniyor...", 95);
                await OptimizationEngine.CleanTempFilesAsync(true, false, false, true);

                UpdateStatus("Hızlı Optimizasyon Tamamlandı!", 100);
                MessageBox.Show("Hızlı Optimizasyon başarıyla tamamlandı!", "HNX OS Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Quick Optimization encountered an error", ex);
                MessageBox.Show("Optimizasyon sırasında bir hata oluştu. Log dosyasını inceleyin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(1500);
                ProgressPanel.Visibility = Visibility.Collapsed;
                SetButtonsState(true);
            }
        }

        private async void BtnFullOpt_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Full Optimizasyon, telemetri verilerini kapatır, arka plan hizmetlerini devre dışı bırakır, Cloudflare DNS ataması yapar ve sistem önbelleğini temizler. Devam etmek istiyor musunuz?", 
                "Full Optimizasyon", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            SetButtonsState(false);
            ProgressPanel.Visibility = Visibility.Visible;
            OptProgressBar.Value = 0;
            TxtProgressPercent.Text = "0%";

            try
            {
                // 1. Restore Point
                UpdateStatus("1/9: Sistem Geri Yükleme Noktası Oluşturuluyor...", 5);
                await Task.Run(() => RestorePointCreator.CreateRestorePoint("HNX Full Optimization"));

                // 2. Backup Settings
                UpdateStatus("2/9: Sistem Ayarları Yedekleniyor...", 15);
                await Task.Run(() => BackupManager.CreateBackup());

                // 3. Game Mode & Visual Effects
                UpdateStatus("3/9: Oyun Modu ve Görsel Efektler Optimize Ediliyor...", 30);
                await OptimizationEngine.ToggleGameModeAsync(true);
                await OptimizationEngine.ToggleVisualEffectsAsync(true);

                // 4. Services Toggling
                UpdateStatus("4/9: Gereksiz Hizmetler Kapatılıyor (SysMain, Telemetry, Update)...", 45);
                string[] services = { "SysMain", "DiagTrack", "dmwappushservice", "WSearch", "XblAuthManager", "XblGameSave" };
                foreach (var svc in services)
                {
                    await OptimizationEngine.ToggleServiceAsync(svc, true);
                }

                // 5. Privacy Tweaks
                UpdateStatus("5/9: Telemetri Seviyesi ve Gizlilik Ayarları Değiştiriliyor...", 60);
                await OptimizationEngine.ApplyTelemetryLevelAsync(0); // Security
                string[] privacyKeys = { "Cortana", "Copilot", "OneDrive", "EdgeTelemetry", "OfficeTelemetry", "AdId", "Location", "Feedback" };
                foreach (var key in privacyKeys)
                {
                    await OptimizationEngine.TogglePrivacySettingAsync(key, true);
                }

                // 6. Network Optimization
                UpdateStatus("6/9: Ağ Ayarları Yapılandırılıyor ve Cloudflare DNS Atanıyor...", 75);
                await OptimizationEngine.ApplyDnsAsync("1.1.1.1", "1.0.0.1");
                await OptimizationEngine.ToggleNaglesAlgorithmAsync(true);
                await OptimizationEngine.ToggleTcpWindowAutoTuningAsync(true);
                await OptimizationEngine.ToggleQosAsync(true);

                // 7. Flush DNS
                UpdateStatus("7/10: DNS Çözümleyici Önbelleği Sıfırlanıyor...", 75);
                await OptimizationEngine.FlushDnsAsync();

                // 8. Advanced Gaming Tweaks
                UpdateStatus("8/10: Gelişmiş FPS ve İşlemci Ayarları (HPET, Core Parking, MSI) Uygulanıyor...", 85);
                await OptimizationEngine.ToggleHpetAsync(true);
                await OptimizationEngine.ToggleCoreParkingAsync(true);
                await OptimizationEngine.ToggleMsiModeForGpuAsync(true);
                await OptimizationEngine.ApplySystemPrioritiesAsync(true);

                // 9. Temp & Cache Cleaning
                UpdateStatus("9/10: Sistem Önbelleği (Prefetch, Temp, Updates) Temizleniyor...", 95);
                await OptimizationEngine.CleanTempFilesAsync(true, true, true, true);

                UpdateStatus("Full Optimizasyon Tamamlandı!", 100);
                MessageBox.Show("Sisteminiz başarıyla tam kapasite optimize edildi!", "HNX OS Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError("Full Optimization encountered an error", ex);
                MessageBox.Show("Optimizasyon sırasında bir hata oluştu. Log dosyasını inceleyin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(1500);
                ProgressPanel.Visibility = Visibility.Collapsed;
                SetButtonsState(true);
            }
        }

        private async void BtnRollback_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Sisteminiz, en son alınan HNX yedeğine geri döndürülecektir. Devam etmek istiyor musunuz?", 
                "Geri Yükle", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            SetButtonsState(false);
            ProgressPanel.Visibility = Visibility.Visible;
            OptProgressBar.Value = 0;
            TxtProgressPercent.Text = "0%";

            try
            {
                UpdateStatus("Yedekler Geri Yükleniyor...", 40);
                bool success = await Task.Run(() => BackupManager.RestoreBackup());
                
                UpdateStatus("Geri Yükleme İşlemi Bitti.", 100);
                if (success)
                {
                    MessageBox.Show("Tüm ayarlar başarıyla geri yüklendi! Lütfen bilgisayarınızı yeniden başlatın.", "Geri Yükleme", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Yedek dosyası bulunamadı veya geri yüklenemedi. Lütfen log dosyasını kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Rollback failed", ex);
                MessageBox.Show("Geri yükleme hatası. Detaylar log dosyasında.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(1500);
                ProgressPanel.Visibility = Visibility.Collapsed;
                SetButtonsState(true);
                _mainWindow.SwitchView("Geri Alma Merkezi"); // Jump to rollback logs to verify
            }
        }

        private void SetButtonsState(bool enabled)
        {
            BtnQuickOpt.IsEnabled = enabled;
            BtnFullOpt.IsEnabled = enabled;
            BtnRollback.IsEnabled = enabled;
        }

        private void UpdateStatus(string text, double percent)
        {
            TxtProgressStatus.Text = text;
            OptProgressBar.Value = percent;
            TxtProgressPercent.Text = $"{percent}%";
        }
    }
}
