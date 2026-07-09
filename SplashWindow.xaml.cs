using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace HNXOSOptimizer
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBackgroundOrFallback();

            // Run icon generation in the background so we don't delay the Splash screen
            _ = Task.Run(() => IconGenerator.GetIconPath());

            // Display splash for 2.5 seconds
            await Task.Delay(2500);

            // Open MainWindow with fade transition
            Dispatcher.Invoke(() =>
            {
                var mainWindow = new MainWindow();
                
                // Assign programmatic icon if generated
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath))
                {
                    try
                    {
                        mainWindow.Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
                    }
                    catch { }
                }

                mainWindow.Opacity = 0;
                mainWindow.Show();

                // Fade-in animation for MainWindow
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
                mainWindow.BeginAnimation(OpacityProperty, fadeIn);

                // Close splash screen
                this.Close();
            });
        }

        private void LoadBackgroundOrFallback()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string bgPath = Path.Combine(baseDir, "background.png");

                // Fallback check parent folders
                if (!File.Exists(bgPath))
                {
                    string parentDir = Directory.GetParent(baseDir)?.Parent?.Parent?.FullName ?? "";
                    string checkPath = Path.Combine(parentDir, "background.png");
                    if (File.Exists(checkPath))
                    {
                        bgPath = checkPath;
                    }
                }

                if (File.Exists(bgPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(bgPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    BgImageBrush.ImageSource = bitmap;
                }
                else
                {
                    // Fallback to dark color
                    var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A));
                    this.Background = brush;
                    Logger.LogInfo("Splash: background.png not found. Using solid #1A1A1A fallback.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Splash background loading failed", ex);
            }
        }
    }
}
