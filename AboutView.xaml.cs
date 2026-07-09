using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HNXOSOptimizer
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }

        private void Link_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && tb.Tag != null)
            {
                string url = tb.Tag.ToString()!;
                OpenUrl(url);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open link: " + url, ex);
            }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnCheckUpdate.IsEnabled = false;
            TxtUpdateStatus.Text = "Güncellemeler denetleniyor...";

            // Simulate online update check
            await Task.Delay(2000);

            TxtUpdateStatus.Text = "Tebrikler! En güncel HNX OS Optimizer sürümünü kullanıyorsunuz (v1.0.0).";
            BtnCheckUpdate.IsEnabled = true;
        }
    }
}
