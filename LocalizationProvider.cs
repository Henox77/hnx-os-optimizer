using System.ComponentModel;

namespace HNXOSOptimizer
{
    public class LocalizationProvider : INotifyPropertyChanged
    {
        private static readonly LocalizationProvider _instance = new LocalizationProvider();
        public static LocalizationProvider Instance => _instance;

        private LocalizationProvider() { }

        public string this[string key] => TranslationManager.Translate(key);

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Refresh()
        {
            // "Item[]" is the magic property name in WPF that signals indexer changes
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
}
