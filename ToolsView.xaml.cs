using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace HNXOSOptimizer
{
    public partial class ToolsView : UserControl
    {
        private List<RegistryIssueItem> _scannedIssues = new();
        private List<string> _pathVariables = new();

        public ToolsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadHardwareDetails();
            LoadContextMenuStates();
            LoadPathVariables();
        }

        #region Hardware Details
        private void LoadHardwareDetails()
        {
            TxtInfoCpu.Text = OptimizationEngine.GetCpuInfo();
            TxtInfoGpu.Text = OptimizationEngine.GetGpuInfo();
            TxtInfoRam.Text = OptimizationEngine.GetRamInfo();
            TxtInfoDisk.Text = OptimizationEngine.GetDiskInfo();
            TxtInfoOs.Text = OptimizationEngine.GetOsInfo();
            TxtInfoArch.Text = Environment.Is64BitOperatingSystem ? "64-bit İşletim Sistemi" : "32-bit İşletim Sistemi";
        }
        #endregion

        #region Context Menu Manager
        private void LoadContextMenuStates()
        {
            try
            {
                ToggleCmdMenu.IsChecked = OptimizationEngine.IsContextMenuAdded("CMD");
                ToggleNotepadMenu.IsChecked = OptimizationEngine.IsContextMenuAdded("Notepad");
                TogglePsMenu.IsChecked = OptimizationEngine.IsContextMenuAdded("PowerShell");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading context menu states", ex);
            }
        }

        private void ToggleCmdMenu_Click(object sender, RoutedEventArgs e)
        {
            bool add = ToggleCmdMenu.IsChecked == true;
            OptimizationEngine.ToggleContextMenu("CMD", add);
        }

        private void ToggleNotepadMenu_Click(object sender, RoutedEventArgs e)
        {
            bool add = ToggleNotepadMenu.IsChecked == true;
            OptimizationEngine.ToggleContextMenu("Notepad", add);
        }

        private void TogglePsMenu_Click(object sender, RoutedEventArgs e)
        {
            bool add = TogglePsMenu.IsChecked == true;
            OptimizationEngine.ToggleContextMenu("PowerShell", add);
        }
        #endregion

        #region Registry Cleaner
        private async void BtnScanReg_Click(object sender, RoutedEventArgs e)
        {
            BtnScanReg.IsEnabled = false;
            BtnFixReg.IsEnabled = false;
            ListRegIssues.ItemsSource = null;

            _scannedIssues = await OptimizationEngine.ScanRegistryIssuesAsync();
            ListRegIssues.ItemsSource = _scannedIssues;

            BtnScanReg.IsEnabled = true;
            BtnFixReg.IsEnabled = _scannedIssues.Count > 0;
            
            MessageBox.Show($"Kayıt defteri taraması bitti! {_scannedIssues.Count} adet düzeltilebilir kayıt bulundu.", "Kayıt Defteri", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnFixReg_Click(object sender, RoutedEventArgs e)
        {
            if (_scannedIssues.Count == 0) return;

            BtnFixReg.IsEnabled = false;
            await OptimizationEngine.FixRegistryIssuesAsync(_scannedIssues);
            
            _scannedIssues.Clear();
            ListRegIssues.ItemsSource = null;
            
            MessageBox.Show("Kayıt defteri sorunları başarıyla düzeltildi!", "Kayıt Defteri", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        #endregion

        #region PATH Editor
        private void LoadPathVariables()
        {
            try
            {
                _pathVariables = OptimizationEngine.GetPathVariables();
                ListPaths.ItemsSource = _pathVariables;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error loading PATH variables", ex);
            }
        }

        private void BtnAddPath_Click(object sender, RoutedEventArgs e)
        {
            string newPath = TxtNewPath.Text.Trim();
            if (string.IsNullOrEmpty(newPath)) return;

            if (!Directory.Exists(newPath))
            {
                var confirm = MessageBox.Show(
                    "Girdiğiniz dizin şu anda sistemde mevcut değil. Yine de PATH değişkenine eklemek istiyor musunuz?", 
                    "Dizin Bulunamadı", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            if (_pathVariables.Contains(newPath))
            {
                MessageBox.Show("Bu dizin zaten PATH değişkeninde mevcut.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _pathVariables.Add(newPath);
                OptimizationEngine.SavePathVariables(_pathVariables);
                LoadPathVariables();
                TxtNewPath.Clear();
                MessageBox.Show("Dizin başarıyla PATH değişkenine eklendi!", "PATH Düzenleyici", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ekleme başarısız: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pathToRemove)
            {
                var confirm = MessageBox.Show(
                    $"Aşağıdaki dizini PATH değişkeninden silmek istiyor musunuz?\n\n{pathToRemove}",
                    "PATH Değişkeni Silme",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Yes)
                {
                    try
                    {
                        _pathVariables.Remove(pathToRemove);
                        OptimizationEngine.SavePathVariables(_pathVariables);
                        LoadPathVariables();
                        MessageBox.Show("Dizin başarıyla PATH değişkeninden kaldırıldı!", "PATH Düzenleyici", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Silme başarısız: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        #endregion

        #region File Lock Finder & Unlocker
        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Title = "Kilitli Dosyayı Seçin";
            ofd.Filter = "Tüm Dosyalar (*.*)|*.*";
            if (ofd.ShowDialog() == true)
            {
                TxtFilePath.Text = ofd.FileName;
            }
        }

        private async void BtnScanLock_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtFilePath.Text.Trim();
            if (string.IsNullOrEmpty(path)) return;

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show("Belirtilen dosya veya klasör bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnScanLock.IsEnabled = false;
            ListLockingProcesses.ItemsSource = null;

            var lockers = await Task.Run(() => OptimizationEngine.FindLockingProcesses(path));

            ListLockingProcesses.ItemsSource = lockers;
            BtnScanLock.IsEnabled = true;
            BtnUnlock.IsEnabled = lockers.Count > 0;

            if (lockers.Count == 0)
            {
                MessageBox.Show("Bu dosyayı kilitleyen herhangi bir arka plan işlemi bulunamadı.", "Dosya Kilidi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Dosyayı kilitleyen {lockers.Count} adet işlem bulundu!", "Dosya Kilidi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtFilePath.Text.Trim();
            if (string.IsNullOrEmpty(path)) return;

            BtnUnlock.IsEnabled = false;

            bool success = await Task.Run(() => OptimizationEngine.UnlockFile(path));

            if (success)
            {
                ListLockingProcesses.ItemsSource = null;
                MessageBox.Show("Dosya kilidi başarıyla kaldırıldı! Dosyayı artık silebilir veya taşıyabilirsiniz.", "Dosya Kilidi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Dosya kilidi kaldırılamadı. Detaylar log dosyasında.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}
