using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UserInterface.Models
{
    public class AppPropertyItem : INotifyPropertyChanged
    {
        private string _value = string.Empty;
        private bool _isSecretVisible;

        public string DisplayId { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        public string Category { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public bool IsSensitive { get; set; }
        public bool IsBoolean { get; set; }

        public string Value
        {
            get => _value;
            set
            {
                string newValue = value ?? string.Empty;

                if (_value == newValue)
                    return;

                _value = newValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(BooleanValue));
            }
        }

        public bool IsSecretVisible
        {
            get => _isSecretVisible;
            set
            {
                if (_isSecretVisible == value)
                    return;

                _isSecretVisible = value;
                OnPropertyChanged();
            }
        }

        public string DisplayValue
        {
            get => Value;
            set => Value = value;
        }

        public bool BooleanValue
        {
            get => bool.TryParse(Value, out bool parsed) && parsed;
            set => Value = value ? "true" : "false";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaiseValueChanged()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(BooleanValue));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}