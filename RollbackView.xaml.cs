using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HNXOSOptimizer
{
    public partial class RollbackView : UserControl
    {
        public RollbackView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHistory();
        }

        public void LoadHistory()
        {
            try
            {
                ListHistory.ItemsSource = null;
                ListHistory.ItemsSource = OptimizationEngine.ActionHistory;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading optimization history", ex);
            }
        }

        private async void BtnCreateRestorePoint_Click(object sender, RoutedEventArgs e)
        {
            BtnCreateRestorePoint.IsEnabled = false;
            
            // Running restore point creation asynchronously
            bool success = await Task.Run(() => RestorePointCreator.CreateRestorePoint("HNX Manual Restore Point"));

            if (success)
            {
                MessageBox.Show("Sistem Geri Yükleme Noktası başarıyla oluşturuldu!", "Geri Yükleme", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Geri yükleme noktası oluşturulamadı. Lütfen Windows Sistem Geri Yükleme özelliğinin açık olduğundan emin olun.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            BtnCreateRestorePoint.IsEnabled = true;
            LoadHistory();
        }

        private async void BtnRestoreFull_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Tüm kayıt defteri, hizmetler, güç planı ve ağ ayarlarınız en son alınan HNX yedeğine geri yüklenecektir. Bilgisayarınızın yeniden başlatılması gerekebilir. Devam etmek istiyor musunuz?",
                "Sistem Geri Yükleme",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            BtnRestoreFull.IsEnabled = false;

            bool success = await Task.Run(() => BackupManager.RestoreBackup());

            if (success)
            {
                MessageBox.Show("Tüm yedekler başarıyla geri yüklendi! Değişikliklerin etkili olması için bilgisayarınızı yeniden başlatmanızı öneririz.", "Geri Yükleme", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Yedekler yüklenemedi. Lütfen HNX_Backup klasöründeki yedek dosyalarınızı kontrol edin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            BtnRestoreFull.IsEnabled = true;
            LoadHistory();
        }
    }
}
