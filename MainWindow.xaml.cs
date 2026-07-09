using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace HNXOSOptimizer
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, UserControl> _views = new();
        private string _activeCategory = "Ana Sayfa";
        private bool _isInitializingLang = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadBackgroundImage();
            PopulateLanguages();
            // Load default Home view
            SwitchView("Ana Sayfa");
        }

        private void PopulateLanguages()
        {
            _isInitializingLang = true;
            try
            {
                ComboLang.DisplayMemberPath = "Value";
                ComboLang.SelectedValuePath = "Key";
                ComboLang.ItemsSource = TranslationManager.GetLanguagesList();
                ComboLang.SelectedValue = TranslationManager.CurrentLanguage;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error populating languages", ex);
            }
            finally
            {
                _isInitializingLang = false;
            }
        }

        private void LoadBackgroundImage()
        {
            try
            {
                // Try reading background.png from the application's base directory
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string bgPath = Path.Combine(baseDir, "background.png");

                // Fallback check: try parent folder if running inside bin\Debug\net8.0-windows
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
                    Logger.LogInfo("Successfully loaded background image: " + bgPath);
                }
                else
                {
                    Logger.LogWarning("background.png was not found in: " + baseDir);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading background image", ex);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Application shutting down.");
            Application.Current.Shutdown();
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.IsChecked == true)
            {
                string category = btn.Name switch
                {
                    "BtnMenuHome" => "Ana Sayfa",
                    "BtnMenuPerf" => "Performans",
                    "BtnMenuPriv" => "Gizlilik",
                    "BtnMenuNet" => "Ağ",
                    "BtnMenuClean" => "Temizlik",
                    "BtnMenuTools" => "Araçlar",
                    "BtnMenuRollback" => "Geri Alma Merkezi",
                    "BtnMenuAbout" => "Hakkında",
                    _ => "Ana Sayfa"
                };
                SwitchView(category);
            }
        }

        private void ComboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingLang) return;

            string lang = null;
            if (ComboLang.SelectedValue is string s)
            {
                lang = s;
            }
            else if (ComboLang.SelectedItem is KeyValuePair<string, string> kvp)
            {
                lang = kvp.Key;
            }
            else if (ComboLang.SelectedItem != null)
            {
                try
                {
                    var prop = ComboLang.SelectedItem.GetType().GetProperty("Key");
                    lang = prop?.GetValue(ComboLang.SelectedItem)?.ToString();
                }
                catch { }

                if (string.IsNullOrEmpty(lang))
                {
                    string str = ComboLang.SelectedItem.ToString() ?? "";
                    if (str.StartsWith("[") && str.Contains(","))
                    {
                        lang = str.Substring(1, str.IndexOf(",") - 1).Trim();
                    }
                }
            }

            if (!string.IsNullOrEmpty(lang))
            {
                TranslationManager.CurrentLanguage = lang;
                Logger.LogInfo($"Language changed to: {lang}");

                // Trigger dynamic binding update for indexer-bound XAML controls
                LocalizationProvider.Instance.Refresh();

                // Re-translate active view by clearing cache for it and re-switching
                if (ContentArea != null)
                {
                    _views.Remove(_activeCategory);
                    SwitchView(_activeCategory);
                }
            }
            else
            {
                Logger.LogWarning($"ComboLang selection changed, but language code could not be determined.");
            }
        }

        public void SwitchView(string category)
        {
            if (ContentArea == null) return;
            _activeCategory = category;

            UserControl view = GetOrCreateView(category);

            // Smooth transition animation (Fade in)
            var fadeOutAnimation = new DoubleAnimation(0, TimeSpan.FromSeconds(0.1));
            fadeOutAnimation.Completed += (s, a) =>
            {
                ContentArea.Content = view;
                var fadeInAnimation = new DoubleAnimation(1, TimeSpan.FromSeconds(0.15));
                ContentArea.BeginAnimation(OpacityProperty, fadeInAnimation);
            };
            ContentArea.BeginAnimation(OpacityProperty, fadeOutAnimation);
        }

        private UserControl GetOrCreateView(string category)
        {
            if (_views.TryGetValue(category, out var cachedView))
            {
                // For dynamic views, reload data if necessary
                if (cachedView is HomeView hv) hv.RefreshSystemInfo();
                if (cachedView is RollbackView rv) rv.LoadHistory();
                return cachedView;
            }

            UserControl newView = category switch
            {
                "Ana Sayfa" => new HomeView(this),
                "Performans" => new PerformanceView(),
                "Gizlilik" => new PrivacyView(),
                "Ağ" => new NetworkView(),
                "Temizlik" => new CleaningView(),
                "Araçlar" => new ToolsView(),
                "Geri Alma Merkezi" => new RollbackView(),
                "Hakkında" => new AboutView(),
                _ => new HomeView(this)
            };

            _views[category] = newView;
            return newView;
        }
    }
}
