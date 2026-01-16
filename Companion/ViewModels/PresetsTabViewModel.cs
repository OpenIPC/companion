using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using Companion.Events;
using Companion.Models;
using Companion.Models.Presets;
using Companion.Services;
using Companion.Services.Presets;
using Companion.Views;
using Serilog;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing camera presets, including loading, filtering, and applying presets
/// </summary>
public partial class PresetsTabViewModel : ViewModelBase
{
    private readonly IGitHubPresetService _gitHubPresetService;
    private readonly IPresetService _presetService;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    #region Observable Properties

    [ObservableProperty] private bool _canConnect;

    [ObservableProperty] private Repository? _selectedRepository;

    [ObservableProperty] private Preset? _selectedPreset;

    [ObservableProperty] private string? _selectedCategory;

    [ObservableProperty] private string? _selectedTag;

    [ObservableProperty] private string? _selectedAuthor;

    [ObservableProperty] private string? _selectedStatus;

    [ObservableProperty] private string? _searchQuery;

    [ObservableProperty] private string? _newRepositoryUrl;

    [ObservableProperty] private string? _logMessage;

    [ObservableProperty] private bool _isApplyingPreset;

    [ObservableProperty] private bool _isLoading;

    #endregion

    #region Collections

    public ObservableCollection<Repository> Repositories { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> Tags { get; } = new();
    public ObservableCollection<string> Authors { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new();
    public ObservableCollection<Preset> AllPresets { get; } = new();

    #endregion

    #region Commands

    public ICommand AddRepositoryCommand { get; }
    public ICommand RemoveRepositoryCommand { get; }
    public ICommand FetchPresetsCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand FilterCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand ShowPresetDetailsCommand { get; }
    public ICommand SyncRepositoryCommand { get; }
    public ICommand CreatePresetCommand { get; }

    #endregion

    #region Constructor

    public PresetsTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IGitHubPresetService gitHubPresetService,
        IPresetService presetService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _gitHubPresetService = gitHubPresetService;
        _presetService = presetService;
        _logger = logger?.ForContext(GetType()) ?? 
                 throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();

        SubscribeToEvents();

        // Initialize commands
        // AddRepositoryCommand = new RelayCommand(AddRepository, 
        //     () => !string.IsNullOrWhiteSpace(NewRepositoryUrl));

        AddRepositoryCommand = new AsyncRelayCommand(AddRepository);

        RemoveRepositoryCommand = new RelayCommand<Repository>(RemoveRepository);
        FetchPresetsCommand = new AsyncRelayCommand(FetchPresetsAsync);
        ApplyPresetCommand = new AsyncRelayCommand<Preset>(ApplyPresetAsync, CanApplyPreset);
        FilterCommand = new RelayCommand(FilterPresets);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ShowPresetDetailsCommand = new RelayCommand<Preset>(ShowPresetDetails);
        SyncRepositoryCommand = new AsyncRelayCommand<Repository>(SyncRepositoryAsync);

        // CreatePresetCommand = new RelayCommand(async () => await CreatePresetAsync());

        LoadInitialRepositories();

        // Use async method to load presets
        _ = LoadPresetsAsync();
    }

    #endregion

    #region Initialization Methods

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
    }

    #endregion

    #region Event Handlers

    private void OnAppMessage(AppMessage message)
    {
        // Ensure update happens on UI thread
        Dispatcher.UIThread.Invoke(() =>
        {
            // Force update even if the value seems the same
            if (CanConnect != message.CanConnect)
            {
                CanConnect = message.CanConnect;
            }

            // Explicitly trigger property changed notification
            OnPropertyChanged(nameof(CanConnect));
        });
    }

    #endregion

    #region Property Changed Partial Methods

    partial void OnSelectedCategoryChanged(string? value)
    {
        FilterPresets();
    }

    partial void OnSelectedTagChanged(string? value)
    {
        FilterPresets();
    }

    partial void OnSelectedAuthorChanged(string? value)
    {
        FilterPresets();
    }

    partial void OnSelectedStatusChanged(string? value)
    {
        FilterPresets();
    }

    partial void OnSearchQueryChanged(string? value)
    {
        FilterPresets();
    }

    partial void OnSelectedPresetChanged(Preset? value)
    {
        if (value != null)
        {
            // Load the preset files to ensure we have the latest content
            _ = LoadPresetFilesAsync(value);
        }
    }

    #endregion

    #region Data Loading Methods

    /// <summary>
    /// Gets a platform-independent temporary directory path WITHOUT deleting it
    /// </summary>
    private string GetTempPresetsDirectory(string repositoryName)
    {
        // Use the system's temp directory as a base
        string baseDir = Path.Combine(Path.GetTempPath(), "OpenIPC Companion", "Presets", repositoryName);

        // Just ensure the directory exists but don't delete it - let the GitHubPresetService handle cleaning
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }

        return baseDir;
    }

    private async Task LoadPresetsAsync()
    {
        try
        {
            IsLoading = true;
            UpdateUIMessage("Loading presets...");

            // Clear existing presets
            AllPresets.Clear();
            Presets.Clear();

            // First, load local presets
            // TODO: maybe do this later
            //await LoadLocalPresetsAsync();

            // Then, load presets from active repositories
            await LoadRemotePresetsAsync();

            _logger.Information($"Loaded {AllPresets.Count} presets in total.");
            LoadDropdownValues();
            FilterPresets();

            UpdateUIMessage($"Loaded {AllPresets.Count} presets.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading presets: {ex.Message}");
            UpdateUIMessage($"Error loading presets: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPresetFilesAsync(Preset preset)
    {
        try
        {
            // Ensure the preset files are loaded
            await _presetService.LoadPresetFilesAsync(preset);

            // Force UI update by raising property changed for the preset
            OnPropertyChanged(nameof(SelectedPreset));
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading preset files: {ex.Message}");
        }
    }

    private async Task LoadLocalPresetsAsync()
    {
        var presetDirectory = Path.Join(OpenIPC.GetBinariesPath(), "presets");
        if (!Directory.Exists(presetDirectory))
        {
            _logger.Warning("Local preset directory not found.");
            return;
        }

        foreach (var presetFolder in Directory.GetDirectories(presetDirectory))
        {
            var presetConfigPath = Path.Combine(presetFolder, "preset-config.yaml");
            if (!File.Exists(presetConfigPath))
            {
                _logger.Warning($"Skipping preset folder {presetFolder}: preset-config.yaml missing.");
                continue;
            }

            try
            {
                var preset = Preset.LoadFromFile(presetConfigPath);

                // Ensure the preset files are loaded
                await _presetService.LoadPresetFilesAsync(preset);

                // Avoid duplicates
                if (!AllPresets.Any(p => p.Name == preset.Name && p.FolderPath == preset.FolderPath))
                {
                    AllPresets.Add(preset);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading local preset from {presetConfigPath}: {ex.Message}");
            }
        }

        _logger.Information($"Loaded {AllPresets.Count} local presets.");
    }

    private async Task LoadRemotePresetsAsync()
    {
        foreach (var repository in Repositories.Where(r => r.IsActive))
        {
            try
            {
                _logger.Information($"Processing repository: {repository.Name}");

                // Don't create any directory here, just pass null to let the service handle it
                var downloadedPresets = await _gitHubPresetService.SyncRepositoryPresetsAsync(
                    repository,
                    null // Pass null to let GitHubPresetService handle directory creation
                );

                // Process downloaded presets as before
                foreach (var presetPath in downloadedPresets)
                {
                    try
                    {
                        var preset = Preset.LoadFromFile(presetPath);
                        await _presetService.LoadPresetFilesAsync(preset);

                        if (!AllPresets.Any(p => p.Name == preset.Name && p.FolderPath == preset.FolderPath))
                        {
                            AllPresets.Add(preset);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error loading remote preset from {presetPath}: {ex.Message}");
                    }
                }

                _logger.Information($"Loaded {downloadedPresets.Count} presets from {repository.Name}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error syncing repository {repository.Name}: {ex.Message}");
            }
        }
    }

    private void LoadDropdownValues()
    {
        Categories.Clear();
        Tags.Clear();
        Authors.Clear();
        StatusOptions.Clear();

        // Add "Empty" option
        Categories.Add("");
        Tags.Add("");
        Authors.Add("");
        StatusOptions.Add("");

        foreach (var preset in AllPresets)
        {
            if (!string.IsNullOrEmpty(preset.Category) && !Categories.Contains(preset.Category))
                Categories.Add(preset.Category);

            foreach (var tag in preset.Tags)
            {
                if (!Tags.Contains(tag))
                    Tags.Add(tag);
            }

            if (!string.IsNullOrEmpty(preset.Author) && !Authors.Contains(preset.Author))
                Authors.Add(preset.Author);

            if (!string.IsNullOrEmpty(preset.Status) && !StatusOptions.Contains(preset.Status))
                StatusOptions.Add(preset.Status);
        }
    }

    private void LoadInitialRepositories()
    {
        try
        {
            // Clear existing repositories
            Repositories.Clear();

            // Get the configuration
            var configuration = App.ServiceProvider.GetService<IConfiguration>();
            if (configuration == null)
            {
                _logger.Warning("Configuration not available, using default repository");
                AddDefaultRepository();
                return;
            }

            // Get the preset settings section
            var repositories = configuration.GetSection("Presets:Repositories").Get<List<RepositorySettings>>();
            if (repositories == null || !repositories.Any())
            {
                _logger.Warning("No repositories configured in settings, using default repository");
                AddDefaultRepository();
                return;
            }

            // Add each repository from settings
            foreach (var repoSettings in repositories)
            {
                try
                {
                    var repo = Repository.FromUrl(repoSettings.Url);
                    repo.Branch = repoSettings.Branch;
                    repo.Description = repoSettings.Description;
                    repo.IsActive = repoSettings.IsActive;

                    Repositories.Add(repo);
                    _logger.Information($"Added repository from settings: {repo.Name}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error adding repository {repoSettings.Url}: {ex.Message}");
                }
            }

            // If no repositories were added, use the default
            if (!Repositories.Any())
            {
                AddDefaultRepository();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading repositories from settings: {ex.Message}");
            AddDefaultRepository();
        }
    }

    private void AddDefaultRepository()
    {
        var repo = Repository.FromUrl("https://github.com/mikecarr/fpv-presets");
        repo.Branch = "master";
        repo.Description = "Official OpenIPC presets repository";
        repo.IsActive = true;
        Repositories.Add(repo);
        _logger.Information("Added default repository");
    }

    #endregion

    #region Repository Management Methods

    private async Task AddRepository()
    {
        // Show a dialog to get preset details

        var presetAddRepoViewModel = new PresetsAddRepoViewModel();
        {
            presetAddRepoViewModel.RepoUrl = NewRepositoryUrl;
        }
        ;

        var presetDetailsView = new PresetsAddRepoView();
        {
            //DataContext = presetAddRepoViewModel 
        }
        ;

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window
            {
                Title = "Add Repo",
                Content = presetDetailsView,
                Width = 600,
                Height = 300
            };

            window.Show(desktop.MainWindow);
        }

        if (string.IsNullOrWhiteSpace(NewRepositoryUrl))
            return;

        try
        {
            var newRepository = Repository.FromUrl(NewRepositoryUrl);
            Repositories.Add(newRepository);
            NewRepositoryUrl = string.Empty;

            _logger.Information($"Added repository: {newRepository.Name}");
            UpdateUIMessage($"Added repository: {newRepository.Name}");
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error adding repository: {ex.Message}");
            UpdateUIMessage($"Failed to add repository: {ex.Message}");
        }
    }

    private void RemoveRepository(Repository? repository)
    {
        if (repository == null)
            return;

        Repositories.Remove(repository);
        _logger.Information($"Removed repository: {repository.Name}");
        UpdateUIMessage($"Removed repository: {repository.Name}");
    }

    private async Task SyncRepositoryAsync(Repository? repository)
    {
        if (repository == null || !repository.IsActive)
            return;

        try
        {
            IsLoading = true;
            UpdateUIMessage($"Syncing repository {repository.Name}...");

            var localPresetsDir = GetTempPresetsDirectory(repository.RepositoryName);
            _logger.Information($"Using temp directory for {repository.Name}: {localPresetsDir}");

            var downloadedPresets = await _gitHubPresetService.SyncRepositoryPresetsAsync(
                repository,
                localPresetsDir
            );

            await LoadPresetsAsync();

            var message = $"Synced {downloadedPresets.Count} presets from {repository.Name}";
            _logger.Information(message);
            UpdateUIMessage(message);
        }
        catch (Exception ex)
        {
            var message = $"Error syncing repository {repository.Name}: {ex.Message}";
            _logger.Error(message);
            UpdateUIMessage(message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task FetchPresetsAsync()
    {
        try
        {
            IsLoading = true;
            UpdateUIMessage("Fetching presets...");

            foreach (var repository in Repositories.Where(r => r.IsActive))
            {
                // Use the repository's built-in methods for fetching presets
                var presets = await _gitHubPresetService.FetchPresetFilesAsync(repository);

                // Process fetched presets
                foreach (var preset in presets)
                {
                    _logger.Information($"Found preset: {preset.Name} at {preset.Path}");
                }
            }

            await LoadPresetsAsync();
            var message = "Presets fetched successfully.";
            UpdateUIMessage(message);
        }
        catch (Exception ex)
        {
            var message = $"Error fetching presets: {ex.Message}";
            _logger.Error(message);
            UpdateUIMessage(message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Preset Management Methods

    private bool CanApplyPreset(Preset? preset)
    {
        return preset != null && !IsApplyingPreset && !IsLoading;
    }

    public async Task ApplyPresetAsync(Preset? preset)
    {
        if (preset == null) return;

        try
        {
            IsApplyingPreset = true;
            UpdateUIMessage($"Applying preset '{preset.Name}'...");

            // Use the PresetService to apply the preset
            var result = await _presetService.ApplyPresetAsync(preset);

            if (result)
            {
                var message = $"Successfully applied preset '{preset.Name}'";
                _logger.Information(message);
                UpdateUIMessage(message);

                // Show a success message to the user
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await MessageBoxManager.GetMessageBoxStandard(
                        "Success",
                        $"Successfully applied preset '{preset.Name}'").ShowAsync();
                });
            }
            else
            {
                var message = $"Failed to apply preset '{preset.Name}'";
                _logger.Error(message);
                UpdateUIMessage(message);

                // Show an error message to the user
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        $"Failed to apply preset '{preset.Name}'. Check the log for details.").ShowAsync();
                });
            }
        }
        catch (Exception ex)
        {
            var message = $"Error applying preset: {ex.Message}";
            _logger.Error(message);
            UpdateUIMessage(message);

            // Show an error message to the user
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Error applying preset: {ex.Message}").ShowAsync();
            });
        }
        finally
        {
            IsApplyingPreset = false;
        }
    }

    private async Task CreatePresetAsync()
    {
        try
        {
            // Show a dialog to get preset details
            var dialog = new PresetDetailsDialog();
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var result = await dialog.ShowDialog<PresetDetailsResult>(desktop.MainWindow);

                if (result != null && !string.IsNullOrEmpty(result.Name))
                {
                    UpdateUIMessage($"Creating preset '{result.Name}'...");

                    // Use the PresetService to create a preset from current settings
                    var preset = await _presetService.CreatePresetFromCurrentConfigAsync(
                        result.Name,
                        result.Category,
                        result.Description);

                    if (preset != null)
                    {
                        // Save the preset to a local file
                        var presetDirectory = Path.Combine(
                            OpenIPC.GetBinariesPath(),
                            "presets",
                            "custom");

                        if (!Directory.Exists(presetDirectory))
                        {
                            Directory.CreateDirectory(presetDirectory);
                        }

                        var presetPath = Path.Combine(
                            presetDirectory,
                            $"{result.Name.Replace(' ', '_')}.yaml");

                        // Save the preset
                        preset.SaveToFile(presetPath);

                        UpdateUIMessage($"Created preset '{result.Name}'");

                        // Refresh the preset list
                        await LoadPresetsAsync();
                    }
                    else
                    {
                        UpdateUIMessage("Failed to create preset");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            var message = $"Error creating preset: {ex.Message}";
            _logger.Error(message);
            UpdateUIMessage(message);
        }
    }

    public void ShowPresetDetails(Preset? preset)
    {
        if (preset is null)
            return;

        var presetDetailsViewModel = new PresetDetailsViewModel
        {
            Preset = preset
        };

        var presetDetailsView = new PresetDetailsView
        {
            DataContext = presetDetailsViewModel
        };

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window
            {
                Title = "Preset Details",
                Content = presetDetailsView,
                Width = 600,
                Height = 600
            };

            window.Show(desktop.MainWindow);
        }
    }

    private void FilterPresets()
    {
        var filteredPresets = AllPresets.AsEnumerable();

        if (!string.IsNullOrEmpty(SearchQuery))
            filteredPresets = filteredPresets.Where(p =>
                p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(SelectedCategory))
            filteredPresets = filteredPresets.Where(p => p.Category == SelectedCategory);

        if (!string.IsNullOrEmpty(SelectedTag))
            filteredPresets = filteredPresets.Where(p => p.Tags.Contains(SelectedTag));

        if (!string.IsNullOrEmpty(SelectedAuthor))
            filteredPresets = filteredPresets.Where(p => p.Author == SelectedAuthor);

        if (!string.IsNullOrEmpty(SelectedStatus))
            filteredPresets = filteredPresets.Where(p => p.Status == SelectedStatus);

        Presets.Clear();
        foreach (var preset in filteredPresets)
            Presets.Add(preset);
    }

    // Add this method to ensure consistent repository name handling
    private string SanitizeRepositoryName(Repository repository)
    {
        // Get the repository URL and extract owner and repo name
        string repoUrl = repository.Url.TrimEnd('/');

        // Extract the owner/repo part from the URL
        string ownerRepo = repoUrl.Split('/').Skip(3).Take(2).Aggregate((a, b) => a + "/" + b);

        // If we successfully extracted owner/repo, format it as owner-repo
        if (ownerRepo.Contains("/"))
        {
            string[] parts = ownerRepo.Split('/');
            string owner = parts[0];
            string repo = parts[1];

            // Replace owner/repo with owner-repo format
            repoUrl = $"{owner}-{repo}";
        }
        else
        {
            // Fallback to repository name if we couldn't extract owner/repo
            repoUrl = repository.RepositoryName;
        }

        // Remove any characters that aren't suitable for directory names
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            repoUrl = repoUrl.Replace(c, '-');
        }

        return repoUrl;
    }

    private void ClearFilters()
    {
        SelectedCategory = null;
        SelectedTag = null;
        SelectedAuthor = null;
        SelectedStatus = null;
        SearchQuery = null;

        FilterPresets();
    }

    #endregion

    #region Utility Methods

    private void UpdateUIMessage(string message)
    {
        LogMessage = message;
    }

    #endregion
}

/// <summary>
/// Dialog for creating a new preset
/// </summary>
public class PresetDetailsDialog : Window
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _categoryTextBox;
    private readonly TextBox _descriptionTextBox;

    public PresetDetailsDialog()
    {
        Title = "Create New Preset";
        Width = 400;
        Height = 300;

        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(10)
        };

        panel.Children.Add(new TextBlock { Text = "Name:" });
        _nameTextBox = new TextBox { Margin = new Avalonia.Thickness(0, 0, 0, 10) };
        panel.Children.Add(_nameTextBox);

        panel.Children.Add(new TextBlock { Text = "Category:" });
        _categoryTextBox = new TextBox { Margin = new Avalonia.Thickness(0, 0, 0, 10) };
        panel.Children.Add(_categoryTextBox);

        panel.Children.Add(new TextBlock { Text = "Description:" });
        _descriptionTextBox = new TextBox
        {
            Margin = new Avalonia.Thickness(0, 0, 0, 10),
            Height = 60,
            AcceptsReturn = true
        };
        panel.Children.Add(_descriptionTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80,
            Margin = new Avalonia.Thickness(0, 0, 10, 0)
        };
        saveButton.Click += (s, e) => Close(new PresetDetailsResult
        {
            Name = _nameTextBox.Text,
            Category = _categoryTextBox.Text,
            Description = _descriptionTextBox.Text
        });

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80
        };
        cancelButton.Click += (s, e) => Close(null);

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        Content = panel;
    }
}

/// <summary>
/// Result from the preset details dialog
/// </summary>
public class PresetDetailsResult
{
    public string Name { get; set; }
    public string Category { get; set; }
    public string Description { get; set; }
}
