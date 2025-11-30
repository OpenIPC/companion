using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace Companion.ViewModels;

public class PresetsAddRepoViewModel : INotifyPropertyChanged
{
    private string? _repoUrl;

    public string? RepoUrl
    {
        get => _repoUrl;
        set
        {
            if (_repoUrl != value)
            {
                _repoUrl = value;
                OnPropertyChanged();
                // Raise CanExecuteChanged to re-evaluate the command's enabled state.
                AddRepositoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PresetsAddRepoViewModel()
    {
        AddRepositoryCommand = new RelayCommand(AddRepository, CanAddRepository);
    }

    private bool CanAddRepository()
    {
        // Implement your logic here to determine if the button should be enabled.
        // For example, check if RepoUrl is not null and not empty.
        return !string.IsNullOrEmpty(RepoUrl);
    }

    private void AddRepository()
    {
        throw new System.NotImplementedException();
    }

    #region Commands
    public RelayCommand AddRepositoryCommand { get; } // Changed ICommand to RelayCommand
    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}