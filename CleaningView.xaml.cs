using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace HNXOSOptimizer
{
    public partial class CleaningView : UserControl
    {
        public CleaningView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            LoadStartupItems();
            LoadUwpApps();
        }

        private void LoadStartupItems()
        {
            try
            {
                var items = OptimizationEngine.GetStartupItems();
                ListStartup.ItemsSource = items;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading startup items", ex);
            }
        }

        private void LoadUwpApps()
        {
            ListUwpApps.ItemsSource = null;
            
            // Querying UWP Apps might take 1-2 seconds, run on separate thread to keep UI interactive
            Task.Run(() =>
            {
                var apps = OptimizationEngine.GetUwpApps();
                Dispatcher.Invoke(() =>
                {
                    ListUwpApps.ItemsSource = apps;
                });
            });
        }

        private async void BtnClean_Click(object sender, RoutedEventArgs e)
        {
            bool temp = ChkTemp.IsChecked == true;
            bool prefetch = ChkPrefetch.IsChecked == true;
            bool updateCache = ChkUpdateCache.IsChecked == true;
            bool recycle = ChkRecycle.IsChecked == true;

            if (!temp && !prefetch && !updateCache && !recycle)
            {
                MessageBox.Show("Lütfen temizlemek için en az bir öğe seçin.", "Sistem Temizliği", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnClean.IsEnabled = false;
            await OptimizationEngine.CleanTempFilesAsync(temp, prefetch, updateCache, recycle);
            BtnClean.IsEnabled = true;

            MessageBox.Show("Seçilen önbellek ve geçici dosyalar başarıyla temizlendi!", "Sistem Temizliği", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnUninstallUwp_Click(object sender, RoutedEventArgs e)
        {
            if (ListUwpApps.ItemsSource is not List<UwpAppItem> apps) return;

            var selectedApps = apps.Where(x => x.IsSelected).ToList();
            if (selectedApps.Count == 0)
            {
                MessageBox.Show("Lütfen kaldırılacak en az bir metro uygulaması seçin.", "Uygulama Kaldırıcı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"{selectedApps.Count} adet UWP uygulamasını kaldırmak istediğinizden emin misiniz? Bu işlem geri alınamaz.",
                "Uygulama Kaldırıcı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            BtnUninstallUwp.IsEnabled = false;
            
            await OptimizationEngine.UninstallUwpAppsAsync(selectedApps);
            
            BtnUninstallUwp.IsEnabled = true;
            MessageBox.Show("Seçilen metro uygulamaları başarıyla kaldırıldı!", "Uygulama Kaldırıcı", MessageBoxButton.OK, MessageBoxImage.Information);
            
            LoadUwpApps(); // Refresh
        }

        private void BtnRemoveStartup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StartupItem item)
            {
                var confirm = MessageBox.Show(
                    $"'{item.Name}' ögesini başlangıçtan kaldırmak istiyor musunuz?",
                    "Başlangıç Programları",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    OptimizationEngine.RemoveStartupItem(item);
                    LoadStartupItems(); // Refresh list
                }
            }
        }
    }
}
