using System.ComponentModel;
using System.Runtime.CompilerServices;
using Companion.Models.Presets;

namespace Companion.ViewModels;

public class PresetDetailsViewModel : INotifyPropertyChanged
{
    private Preset? _preset;

    public Preset? Preset
    {
        get => _preset;
        set
        {
            if (_preset != value)
            {
                _preset = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public PresetDetailsViewModel()
    {

    }
}