using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UserInterface.Models
{
    public class StudentColumnSetting : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _header = string.Empty;
        private bool _isVisible = true;
        private int _displayIndex;
        private double _width;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Key
        {
            get => _key;
            set => SetField(ref _key, value ?? string.Empty);
        }

        public string Header
        {
            get => _header;
            set => SetField(ref _header, value ?? string.Empty);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }

        public int DisplayIndex
        {
            get => _displayIndex;
            set => SetField(ref _displayIndex, value);
        }

        public double Width
        {
            get => _width;
            set => SetField(ref _width, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
