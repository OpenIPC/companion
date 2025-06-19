using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json.Linq;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Renci.SshNet.Messages;
using Serilog;
using SharpCompress.Archives;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing firmware updates and device configuration
/// </summary>
public partial class FirmwareTabViewModel : ViewModelBase
{
    #region Private Fields

    private readonly HttpClient _httpClient;
    private readonly SysUpgradeService _sysupgradeService;
    private CancellationTokenSource _cancellationTokenSource;
    private FirmwareData _firmwareData;
    private readonly IGitHubService _gitHubService;
    private bool _bInitializedCommands = false;
    private bool _bRecursionSelectGuard = false;
    private readonly IMessageBoxService _messageBoxService;
    #endregion

    #region Observable Properties

    [ObservableProperty] private bool _canConnect;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isLocalFirmwarePackageSelected;
    [ObservableProperty] private bool _isFirmwareBySocSelected;
    [ObservableProperty] private bool _isManufacturerDeviceFirmwareComboSelected;
    [ObservableProperty] private bool _canDownloadFirmware;
    [ObservableProperty] private bool _isManualUpdateEnabled;
    [ObservableProperty] private string _selectedDevice;
    [ObservableProperty] private string _selectedFirmware;
    [ObservableProperty] private string _selectedFirmwareBySoc;
    [ObservableProperty] private string _selectedManufacturer;
    [ObservableProperty] private string _manualLocalFirmwarePackageFile;
    [ObservableProperty] private int _progressValue;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets whether dropdowns should be enabled based on connection and firmware selection state
    /// </summary>
    public bool CanUseDropdowns => IsConnected;

    /// <summary>
    /// Gets whether soc dropdowns should be enabled based on connection and firmware selection state
    /// </summary>
    public bool CanUseDropdownsBySoc => IsConnected;

    
    /// <summary>
    /// Gets whether firmware selection is available based on connection and manufacturer selection state
    /// </summary>
    public bool CanUseSelectLocalFirmwarePackage => IsConnected;

    /// <summary>
    /// Collection of available manufacturers
    /// </summary>
    public ObservableCollection<string> Manufacturers { get; set; } = new();

    /// <summary>
    /// Collection of available devices for selected manufacturer
    /// </summary>
    public ObservableCollection<string> Devices { get; set; } = new();

    /// <summary>
    /// Collection of available firmware versions
    /// </summary>
    public ObservableCollection<string> Firmwares { get; set; } = new();

    /// <summary>
    /// Collection of available firmware versions
    /// </summary>
    public ObservableCollection<string> FirmwareBySoc { get; set; } = new();

    #endregion

    #region Commands

    public ICommand SelectLocalFirmwarePackageCommand { get; set; }
    public ICommand PerformFirmwareUpgradeAsyncCommand { get; set; }
    public ICommand ClearFormCommand { get; set; }
    public IRelayCommand DownloadFirmwareAsyncCommand { get; set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of FirmwareTabViewModel
    /// </summary>
    public FirmwareTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IGitHubService gitHubService,
        IMessageBoxService messageBoxService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _gitHubService = gitHubService;
        _messageBoxService = messageBoxService;
        _httpClient = new HttpClient();
        _sysupgradeService = new SysUpgradeService(sshClientService, logger);
        _bInitializedCommands = false;
        _bRecursionSelectGuard = false;
        InitializeProperties();
        InitializeCommands();
        SubscribeToEvents();
    }

    #endregion

    #region Initialization Methods

    private void InitializeProperties()
    {
        CanConnect = false;
        IsConnected = false;
        IsLocalFirmwarePackageSelected = false;
        IsManufacturerDeviceFirmwareComboSelected = false;
    }

    private void InitializeCommands()
    {
        if (_bInitializedCommands)
            return;
        _bInitializedCommands = true;
        DownloadFirmwareAsyncCommand = new RelayCommand(
            async () => await DownloadAndPerformFirmwareUpgradeAsync(), 
            CanExecuteDownloadFirmware);

        PerformFirmwareUpgradeAsyncCommand = new RelayCommand(
            async () => await DownloadAndPerformFirmwareUpgradeAsync()); 

        SelectLocalFirmwarePackageCommand = new RelayCommand<Window>(async window =>
            await SelectLocalFirmwarePackage(window));

        ClearFormCommand = new RelayCommand(ClearForm);
    }

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
    }

    #endregion

    #region Event Handlers

    private void OnAppMessage(AppMessage message)
    {
        CanConnect = message.CanConnect;
        IsConnected = message.CanConnect;

        LoadManufacturers();

        if (!IsConnected)
        {
            IsLocalFirmwarePackageSelected = false;
            IsManufacturerDeviceFirmwareComboSelected = false;
        }

        UpdateCanExecuteCommands();
    }

    partial void OnSelectedFirmwareBySocChanged(string value)
    {
        IsFirmwareBySocSelected = !string.IsNullOrEmpty(value);
        if (_bRecursionSelectGuard)
            return;
        _bRecursionSelectGuard = true;
        SelectedManufacturer = string.Empty;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        IsManufacturerDeviceFirmwareComboSelected = false;
        ManualLocalFirmwarePackageFile = string.Empty;
        IsLocalFirmwarePackageSelected = false;
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }
    
    partial void OnSelectedManufacturerChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        _bRecursionSelectGuard = true;
        LoadDevices(value);
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedDeviceChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        _bRecursionSelectGuard = true;
        LoadFirmwares(value);
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedFirmwareChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        _bRecursionSelectGuard = true;
        var manufacturer = _firmwareData?.Manufacturers
                .FirstOrDefault(m => ((m.Name == SelectedManufacturer) || (m.FriendlyName == SelectedManufacturer)));

        var device = manufacturer?.Devices
            .FirstOrDefault(d => ((d.Name == SelectedDevice) || (d.FriendlyName == SelectedDevice)));

        var firmware = device?.FirmwarePackages
            .FirstOrDefault(f => ((f.Name == SelectedFirmware) || (f.FriendlyName == SelectedFirmware)));

        var filename = string.Empty;


        if (!string.IsNullOrEmpty(manufacturer?.Name) &&
            !string.IsNullOrEmpty(device?.Name) &&
            !string.IsNullOrEmpty(firmware?.Name))
        {
            filename = firmware.PackageFile;
            IsManufacturerDeviceFirmwareComboSelected = true;
            IsLocalFirmwarePackageSelected = false;
            ManualLocalFirmwarePackageFile = string.Empty;
        }
        else
        {
            IsManufacturerDeviceFirmwareComboSelected = false;
        }

        if ((!string.IsNullOrEmpty(filename)) && FirmwareBySoc.Contains(filename))
        {
            SelectedFirmwareBySoc = filename;
            IsFirmwareBySocSelected = true;
        }
        else
        {
            SelectedFirmwareBySoc = "";
            IsFirmwareBySocSelected = false;
        }
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnManualLocalFirmwarePackageFileChanged(string value)
    {
        UpdateCanExecuteCommands();
    }

    #endregion

    #region Public Methods

    public async void LoadManufacturers()
    {
        try
        {
            Logger.Information("Loading firmware list...");
            Manufacturers.Clear();
            var data = await FetchFirmwareListAsync();

            if (data?.Manufacturers != null && data.Manufacturers.Any())
            {
                Manufacturers.Clear();
                foreach (var manufacturer in data.Manufacturers)
                {
                    var hasValidFirmwareType = manufacturer.Devices.Any(device =>
                        device.FirmwarePackages.Any(firmware =>
                            DevicesFriendlyNames.FirmwareIsSupported(firmware.Name)));

                    if (hasValidFirmwareType)
                        Manufacturers.Add(manufacturer.FriendlyName);
                }

                if (!Manufacturers.Any())
                    Logger.Warning("No manufacturers with valid firmware types found.");
            }
            else
            {
                Logger.Warning("No manufacturers found in the fetched firmware data.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading manufacturers.");
        }
    }

    public void LoadDevices(string manufacturer)
    {
        Devices.Clear();

        if (string.IsNullOrEmpty(manufacturer))
        {
            Logger.Warning("Manufacturer is null or empty. Devices cannot be loaded.");
            return;
        }

        manufacturer = DevicesFriendlyNames.ManufacturerByFriendlyName(manufacturer);

        var manufacturerData = _firmwareData?.Manufacturers
            .FirstOrDefault(m => m.Name == manufacturer);

        if (manufacturerData == null || !manufacturerData.Devices.Any())
        {
            Logger.Warning($"No devices found for manufacturer: {manufacturer}");
            return;
        }

        foreach (var device in manufacturerData.Devices)
            Devices.Add(device.FriendlyName);

        Logger.Information($"Loaded {Devices.Count} devices for manufacturer: {manufacturer}");
    }

    
    public void LoadFirmwares(string device)
    {
        Firmwares.Clear();

        if (string.IsNullOrEmpty(device))
        {
            Logger.Warning("Device is null or empty. Firmwares cannot be loaded.");
            return;
        }

        device = DevicesFriendlyNames.DeviceByFriendlyName(device);
       
        var deviceData = _firmwareData?.Manufacturers
            .FirstOrDefault(m => ((m.Name == SelectedManufacturer)||(m.FriendlyName == SelectedManufacturer)))?.Devices
            .FirstOrDefault(d => d.Name == device);

        if (deviceData == null || !deviceData.FirmwarePackages.Any())
        {
            Logger.Warning($"No firmware found for device: {device}");
            return;
        }

        foreach (var firmware in deviceData.FirmwarePackages)
        {
           var firmwareName = DevicesFriendlyNames.FirmwareFriendlyNameById(firmware.Name);
           if (!Firmwares.Contains(firmwareName))
                Firmwares.Add(firmwareName);
        }

        Logger.Information($"Loaded {Firmwares.Count} firmware types for device: {device}");
    }

    #endregion

    #region Private Methods

    private void ClearForm()
    {
        SelectedManufacturer = string.Empty;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        SelectedFirmwareBySoc = string.Empty;
        ManualLocalFirmwarePackageFile = string.Empty;

        IsLocalFirmwarePackageSelected = false;
        IsManufacturerDeviceFirmwareComboSelected = false;
        IsManualUpdateEnabled = true;
        
        UpdateCanExecuteCommands();
    }

    private bool CanExecuteDownloadFirmware()
    {
        return CanConnect &&
               (!string.IsNullOrEmpty(SelectedManufacturer) &&
                !string.IsNullOrEmpty(SelectedDevice) &&
                !string.IsNullOrEmpty(SelectedFirmware)) ||
               !string.IsNullOrEmpty(ManualLocalFirmwarePackageFile);
    }

    private void UpdateCanExecuteCommands()
    {
        CanDownloadFirmware = CanExecuteDownloadFirmware();

        OnPropertyChanged(nameof(CanUseDropdowns));
        OnPropertyChanged(nameof(CanUseDropdownsBySoc));
        OnPropertyChanged(nameof(CanUseSelectLocalFirmwarePackage));

        if (IsConnected)
        {
            InitializeCommands();
            (DownloadFirmwareAsyncCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (PerformFirmwareUpgradeAsyncCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (SelectLocalFirmwarePackageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }

    private async Task<FirmwareData> FetchFirmwareListAsync()
    {
        try
        {
            Logger.Information("Fetching firmware list...");

            IEnumerable<string> filenames = await GetFilenamesAsync();

            Logger.Information($"Fetched {filenames.Count()} firmware files.");

            FirmwareData firmwareData = ProcessFilenames(filenames);
            
            // Populate FirmwareBySoc
            PopulateFirmwareBySoc(filenames); // Calling populate method here

            _firmwareData = firmwareData;
            return firmwareData;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error fetching firmware list.");
            return null;
        }
    }
    
    private void PopulateFirmwareBySoc(IEnumerable<string> filenames)
    {
        FirmwareBySoc.Clear(); // Clear existing list

        var chipType = DeviceConfig.Instance.ChipType;

        if (string.IsNullOrEmpty(chipType))
            return;
        
        foreach (var filename in filenames)
        {
            //string pattern = $@"^(?=.*{Regex.Escape(chipType)})(?=.*fpv).*?(?<memoryType>nand|nor)\.tgz$";  //Dynamically create regex with escaped chipType
            string simplePattern = $@".*{chipType}.*fpv.*";
            var match = Regex.Match(filename, simplePattern, RegexOptions.IgnoreCase); //Added RegexOptions.IgnoreCase to compare 
            if (match.Success)
            {
                if (!FirmwareBySoc.Contains(filename))
                {
                    FirmwareBySoc.Add(filename);
                    Logger.Information($"Added FirmwareBySoc: {filename}");
                }
            }
        }

        Logger.Information($"Populated FirmwareBySoc with {FirmwareBySoc.Count} entries.");
    }

    private async Task<IEnumerable<string>> GetFilenamesAsync()
    {
        var response = await _gitHubService.GetGitHubDataAsync(OpenIPC.OpenIPCBuilderGitHubApiUrl);
        var releaseData = JObject.Parse(response.ToString());
        var assets = releaseData["assets"];
        return assets?.Select(asset => asset["name"]?.ToString()).Where(name => !string.IsNullOrEmpty(name)) ??
               Enumerable.Empty<string>();
    }

    private FirmwareData ProcessFilenames(IEnumerable<string> filenames)
    {
        var firmwareData = new FirmwareData { Manufacturers = new ObservableCollection<Manufacturer>() };

        // First, parse and add canonical firmware files;
        // Second, parse add generic firmwares (as the can be added to multiple manufacturers as wildcards)

        foreach (var filename in filenames)
        {
            ProcessIndividualFirmwareFile(filename, firmwareData, true);
        }

        foreach (var filename in filenames)
        {
            ProcessIndividualFirmwareFile(filename, firmwareData, false);
        }

        if (firmwareData.Manufacturers.Count > 0)
        {
            // Add OpenIPC generic SSC338 FPV firmware to the generic entry too
            AddFirmwareData(firmwareData, "ssc338q_fpv_openipc-urllc-aio-nor.tgz", "*", "ssc338q", "urllc-aio", "ssc338q", "nor");

            // Add OpenIPC Thinker built in WiFi firmware as it's not part of the canonical list
            AddFirmwareData(firmwareData, "ssc338q_fpv_openipc-thinker-aio-nor.tgz", "openipc", "thinker-aio-wifi", "fpv", "ssc338q", "nor");
            firmwareData.SortCollections();
        }
        return firmwareData;
    }
    
    private void ProcessIndividualFirmwareFile(string firmwarePackageFilename, FirmwareData firmwareData, bool bCanonicalFilesOnly)
    {
        // A firmware file can be:
        //  * For a particular SOC and a particular manufacturer
        //  * For a particular SOC but any manufacturer
        //  * For a particular SOC and a particular HW configuration (ie.Thinker Wifi)
        // The common firmware filename convention is for first case above, with the filename format: SOC_firmwaretype_manufacturer-device-memorytype
        // For the other cases, different filename conventions could be used (ie: for a firmware generic for all manufacturers: firmwaretype_SOC)
        // Filenames must be canonized after a match is found


        var match = Regex.Match(firmwarePackageFilename,
            @"^(?<sensor>[^_]+)_(?<firmwareType>[^_]+)_(?<manufacturer>[^-]+)-(?<device>.+?)-(?<memoryType>nand|nor)");
        
        if (firmwarePackageFilename.StartsWith("ssc338q_rubyfpv") && (!bCanonicalFilesOnly) )
        {
            ProcessFirmwareMatchGenericRuby(firmwarePackageFilename, firmwareData);
        }
        else if (match.Success && (!firmwarePackageFilename.Contains("rubyfpv")) && bCanonicalFilesOnly )
        {
            ProcessFirmwareMatchForManufacturer(firmwarePackageFilename, match, firmwareData);
        }
        else
        {
            Debug.WriteLine($"Filename '{firmwarePackageFilename}' does not match the expected pattern.");
        }
    }

    private void ProcessFirmwareMatchGenericRuby(string firmwarePackageFilename, FirmwareData firmwareData)
    {
        string socType = "ssc338q";
        string firmwareType = "rubyfpv";
        string manufacturerName = "*";
        string deviceName = "ssc338q";
        string memoryType = "nor";

        if (firmwarePackageFilename.Contains("thinker_internal") )
        {
            manufacturerName = "openipc";
            deviceName = "thinker-aio-wifi";
        }

        if (DeviceConfig.Instance.ChipType != socType)
            return; // using `return` to exit the method. continue is no longer relevant here

        Debug.WriteLine(
            $"Parsed file: SOCType={socType}, FirmwareType={firmwareType}, Manufacturer={manufacturerName}, Device={deviceName}, MemoryType={memoryType}");

        AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturerName, deviceName, firmwareType, socType, memoryType);

        // Add this generic firmware to all supported manufacturers and devices, including generic OpenIPC device
        if (manufacturerName.Equals("*"))
        {
            foreach (var manufacturer in firmwareData.Manufacturers)
            {
                foreach (var device in manufacturer.Devices)
                {
                    AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturer.Name, device.Name, firmwareType, socType, memoryType);
                }
            }
        }
    }

    private void ProcessFirmwareMatchForManufacturer(string firmwarePackageFilename, Match match, FirmwareData firmwareData)
    {
        var socType = match.Groups["sensor"].Value;

        // only show firmware that matches the selected sensor/soc
        if (DeviceConfig.Instance.ChipType != socType)
            return; // using `return` to exit the method. continue is no longer relevant here

        var firmwareType = match.Groups["firmwareType"].Value;
        var manufacturerName = match.Groups["manufacturer"].Value;
        var deviceName = match.Groups["device"].Value;
        var memoryType = match.Groups["memoryType"].Value;

        Debug.WriteLine(
            $"Parsed file: SOCType={socType}, FirmwareType={firmwareType}, Manufacturer={manufacturerName}, Device={deviceName}, MemoryType={memoryType}");

        AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturerName, deviceName, firmwareType, socType, memoryType);
    }

    private void AddFirmwareData(FirmwareData firmwareData, string firmwarePackageFilename, string manufacturerName, string deviceName,
        string firmwareType, string socType, string memoryType)
    {
        var manufacturer = firmwareData.Manufacturers.FirstOrDefault(m => m.Name == manufacturerName);
        if (manufacturer == null)
        {
            manufacturer = CreateAndAddManufacturer(firmwareData, manufacturerName);
        }

        var device = manufacturer.Devices.FirstOrDefault(d => d.Name == deviceName);
        if ( device == null )
        {
            device = CreateAndAddDevice(manufacturer, deviceName);
        }


        var firmwarePackage = device.FirmwarePackages.FirstOrDefault(f => f.PackageFile == firmwarePackageFilename);
        if ( firmwarePackage == null )
        {
            firmwarePackage = CreateAndAddFirmwarePackage(manufacturer, device, firmwarePackageFilename, firmwareType);
        }
    
    }
    
    private Manufacturer CreateAndAddManufacturer(FirmwareData firmwareData, string manufacturerName)
    {
        var manufacturer = new Manufacturer
        {
            Name = manufacturerName,
            FriendlyName = DevicesFriendlyNames.ManufacturerFriendlyNameById(manufacturerName),
            Devices = new ObservableCollection<Device>()
        };
        firmwareData.Manufacturers.Add(manufacturer);
        return manufacturer;
    }

    private Device CreateAndAddDevice(Manufacturer manufacturer, string deviceName)
    {
        var device = new Device
        {
            Name = deviceName,
            FriendlyName = DevicesFriendlyNames.DeviceFriendlyNameById(deviceName),
            FirmwarePackages = new ObservableCollection<FirmwarePackage>()
        };
        manufacturer.Devices.Add(device);
        return device;
    }

    private FirmwarePackage CreateAndAddFirmwarePackage(Manufacturer manufacturer, Device device, string firmwarePackageFilename, string firmwareType)
    {
        var firmwarePackage = new FirmwarePackage
        {
            Name = firmwareType,
            FriendlyName = DevicesFriendlyNames.FirmwareFriendlyNameById(firmwareType),
            PackageFile = firmwarePackageFilename
        };
        
        device.FirmwarePackages.Add(firmwarePackage);
        return firmwarePackage;
    }

    private async Task<string> DownloadFirmwareAsync(string url = null, string filename = null)
    {
        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), filename);
            Logger.Information($"Downloading firmware from {url} to {filePath}");

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                Logger.Information($"Firmware successfully downloaded to: {filePath}");
                return filePath;
            }
            else
            {
                Logger.Warning($"Failed to download firmware. Status code: {response.StatusCode}");
                ProgressValue = 100;
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error downloading firmware");
            return null;
        }
    }

    private string DecompressTgzToTar(string tgzFilePath)
    {
        try
        {
            var tarFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(tgzFilePath)}.tar");

            using (var fileStream = File.OpenRead(tgzFilePath))
            using (var gzipStream =
                   new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress))
            using (var tarFileStream = File.Create(tarFilePath))
            {
                gzipStream.CopyTo(tarFileStream);
            }

            Logger.Information($"Decompressed .tgz to .tar: {tarFilePath}");
            return tarFilePath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error decompressing .tgz file");
            throw;
        }
    }

    private string UncompressFirmware(string tgzFilePath)
    {
        try
        {
            var tarFilePath = DecompressTgzToTar(tgzFilePath);

            ProgressValue = 4;
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(tgzFilePath));
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            Directory.CreateDirectory(tempDir);

            ProgressValue = 8;
            using (var archive = SharpCompress.Archives.Tar.TarArchive.Open(tarFilePath))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    var destinationPath = Path.Combine(tempDir, entry.Key);
                    var directoryPath = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(directoryPath))
                        Directory.CreateDirectory(directoryPath);

                    using (var entryStream = entry.OpenEntryStream())
                    using (var fileStream = File.Create(destinationPath))
                    {
                        entryStream.CopyTo(fileStream);
                    }
                }
            }

            Logger.Information($"Firmware extracted to {tempDir}");
            return tempDir;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error uncompressing firmware file");
            throw;
        }
    }

    private void ValidateFirmwareFiles(string extractedDir)
    {
        try
        {
            var allFiles = Directory.GetFiles(extractedDir);

            var md5Files = allFiles.Where(file => file.EndsWith(".md5sum")).ToList();

            if (!md5Files.Any())
                throw new InvalidOperationException("No MD5 checksum files found in the extracted directory.");

            foreach (var md5File in md5Files)
            {
                var baseFileName = Path.GetFileNameWithoutExtension(md5File);

                var dataFile = allFiles.FirstOrDefault(file => Path.GetFileName(file) == baseFileName);
                if (dataFile == null)
                    throw new FileNotFoundException(
                        $"Data file '{baseFileName}' referenced by '{md5File}' is missing.");

                ValidateMd5Checksum(md5File, dataFile);
            }

            Logger.Information("Firmware files validated successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating firmware files");
            throw;
        }
    }

    private void ValidateMd5Checksum(string md5FilePath, string dataFilePath)
    {
        try
        {
            var md5Line = File.ReadAllText(md5FilePath).Trim();

            var parts = md5Line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"Invalid format in MD5 file: {md5FilePath}");
            }

            var expectedMd5 = parts[0];
            var expectedFilename = parts[1];

            if (Path.GetFileName(dataFilePath) != expectedFilename)
            {
                throw new InvalidOperationException(
                    $"Filename mismatch: expected '{expectedFilename}', found '{Path.GetFileName(dataFilePath)}'");
            }

            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(dataFilePath);
            var actualMd5 = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

            if (expectedMd5 != actualMd5)
            {
                throw new InvalidOperationException(
                    $"MD5 mismatch for file: {Path.GetFileName(dataFilePath)}. Expected: {expectedMd5}, Actual: {actualMd5}");
            }

            Logger.Information($"File '{Path.GetFileName(dataFilePath)}' passed MD5 validation.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error validating MD5 checksum for file: {dataFilePath}");
            throw;
        }
    }

    private async Task DownloadAndPerformFirmwareUpgradeAsync()
    {
        try
        {
            ProgressValue = 0;

            if (!string.IsNullOrEmpty(ManualLocalFirmwarePackageFile))
            {
                Logger.Information("Performing firmware upgrade using manual file.");
                await UpgradeFirmwareFromFileAsync(ManualLocalFirmwarePackageFile);
            }
            else if (!string.IsNullOrEmpty(SelectedFirmwareBySoc))
            {
                Logger.Information("Performing firmware upgrade using firmware by soc.");
                await PerformFirmwareUpgradeFromSocAsync();
            }
            else
            {
                if (string.IsNullOrEmpty(SelectedManufacturer) ||
                    string.IsNullOrEmpty(SelectedDevice) ||
                    string.IsNullOrEmpty(SelectedFirmware))
                {
                    Logger.Warning("Cannot perform firmware upgrade. Missing dropdown selections.");
                    return;
                }

                Logger.Information("Performing firmware upgrade using selected dropdown options.");
                await PerformFirmwareUpgradeFromDropdownAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade");
        }
    }

    private async Task PerformFirmwareUpgradeFromSocAsync()
    {
        try
        {
            ProgressValue = 0;
            
            Logger.Information("Performing firmware upgrade using selected dropdown options.");
            
            var firmwwareFile = SelectedFirmwareBySoc;
            
            var downloadUrl = string.Empty;
            var filename = string.Empty;
            
            if (!string.IsNullOrEmpty(firmwwareFile))
            {
                filename = firmwwareFile;
                downloadUrl = $"https://github.com/OpenIPC/builder/releases/download/latest/{firmwwareFile}";
            }
            
            else
            {
                Logger.Warning("Failed to construct firmware URL. Missing or invalid data.");
                return;
            }

            string firmwareFilePath = await DownloadFirmwareAsync(downloadUrl, filename);
            if (!string.IsNullOrEmpty(firmwareFilePath))
            {
                await UpgradeFirmwareFromFileAsync(firmwareFilePath);
            }
            
            
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade from dropdown");
        }
    }
    private async Task PerformFirmwareUpgradeFromDropdownAsync()
    {
        try
        {
            ProgressValue = 0;

            if (string.IsNullOrEmpty(SelectedManufacturer) ||
                string.IsNullOrEmpty(SelectedDevice) ||
                string.IsNullOrEmpty(SelectedFirmware))
            {
                Logger.Warning("Cannot perform firmware upgrade. Missing dropdown selections.");
                return;
            }

            Logger.Information("Performing firmware upgrade using selected dropdown options.");

            var manufacturer = _firmwareData?.Manufacturers
                .FirstOrDefault(m => ((m.Name == SelectedManufacturer) || (m.FriendlyName == SelectedManufacturer)));

            var device = manufacturer?.Devices
                .FirstOrDefault(d => ((d.Name == SelectedDevice) || (d.FriendlyName == SelectedDevice)));

            var firmware = device?.FirmwarePackages
                .FirstOrDefault(f => ((f.Name == SelectedFirmware) || (f.FriendlyName == SelectedFirmware)));

            var downloadUrl = string.Empty;
            var filename = string.Empty;
            

            if (!string.IsNullOrEmpty(manufacturer?.Name) &&
                !string.IsNullOrEmpty(device?.Name) &&
                !string.IsNullOrEmpty(firmware?.Name))
            {
                filename = firmware.PackageFile;
                downloadUrl = $"https://github.com/OpenIPC/builder/releases/download/latest/{filename}";
            }
            
            else
            {
                Logger.Warning("Failed to construct firmware URL. Missing or invalid data.");
                return;
            }

            string firmwareFilePath = await DownloadFirmwareAsync(downloadUrl, filename);
            if (!string.IsNullOrEmpty(firmwareFilePath))
            {
                await UpgradeFirmwareFromFileAsync(firmwareFilePath);
            }
            
            
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade from dropdown");
        }
    }


    public void CancelSysupgrade()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task UpgradeFirmwareFromFileAsync(string firmwareFilePath)
    {
        try
        {
            Logger.Information($"Upgrading firmware from file: {firmwareFilePath}");

            ProgressValue = 5;
            Logger.Debug("UncompressFirmware ProgressValue: " + ProgressValue);
            var extractedDir = UncompressFirmware(firmwareFilePath);

            ProgressValue = 10;
            Logger.Debug("ValidateFirmwareFiles ProgressValue: " + ProgressValue);
            ValidateFirmwareFiles(extractedDir);

            ProgressValue = 20;
            Logger.Debug("Before PerformSysupgradeAsync ProgressValue: " + ProgressValue);
            var kernelFile = Directory.GetFiles(extractedDir)
                .FirstOrDefault(f => f.Contains("uImage") && !f.EndsWith(".md5sum"));

            var rootfsFile = Directory.GetFiles(extractedDir)
                .FirstOrDefault(f => f.Contains("rootfs") && !f.EndsWith(".md5sum"));

            if (kernelFile == null || rootfsFile == null)
                throw new InvalidOperationException("Kernel or RootFS file is missing after validation.");

            var sysupgradeService = new SysUpgradeService(SshClientService, Logger);

            await Task.Run(async () =>
            {
                await sysupgradeService.PerformSysupgradeAsync(DeviceConfig.Instance, kernelFile, rootfsFile,
                    progress =>
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            switch (progress)
                            {
                                case var s when s.Contains("Update kernel from"):
                                    ProgressValue = 30;
                                    Logger.Debug("Update kernel from ProgressValue: " + ProgressValue);
                                    break;
                                case var s when s.Contains("Kernel updated to"):
                                    ProgressValue = 50;
                                    Logger.Debug("Kernel updated to ProgressValue: " + ProgressValue);
                                    break;
                                case var s when s.Contains("Update rootfs from"):
                                    ProgressValue = 60;
                                    Logger.Debug("Update rootfs from ProgressValue: " + ProgressValue);
                                    break;
                                case var s when s.Contains("Root filesystem uploaded successfully"):
                                    ProgressValue = 70;
                                    Logger.Debug(
                                        "Root filesystem uploaded successfully ProgressValue: " + ProgressValue);
                                    break;
                                case var s when s.Contains("Erase overlay partition"):
                                    ProgressValue = 90;
                                    Logger.Debug("Erase overlay partition ProgressValue: " + ProgressValue);
                                    break;
                            }

                            Logger.Debug(progress);
                        });
                    },
                    CancellationToken.None);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    //ProgressValue = 100;
                    Logger.Information("Firmware upgrade completed successfully.");
                });
            });

            ProgressValue = 100;
            await _messageBoxService.ShowCustomMessageBox("Upgrade Complete!!", "Device has been flashed!", ButtonEnum.Ok, Icon.Success);
            Logger.Information("Firmware upgrade completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error upgrading firmware from file");
        }
    }

    public async Task SelectLocalFirmwarePackage(Window window)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a File",
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Compressed", Extensions = { "tgz" } },
                new() { Name = "Bin Files", Extensions = { "bin" } },
                new() { Name = "All Files", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(window);

        if (result != null && result.Length > 0)
        {
            var selectedFile = result[0];
            var fileName = Path.GetFileName(selectedFile);
            Console.WriteLine($"Selected File: {selectedFile}");
            ManualLocalFirmwarePackageFile = selectedFile;

            IsLocalFirmwarePackageSelected = true;
            IsManufacturerDeviceFirmwareComboSelected = false;
            SelectedManufacturer = string.Empty;
            SelectedDevice = string.Empty;
            SelectedFirmware = string.Empty;
            SelectedFirmwareBySoc = string.Empty;


            UpdateCanExecuteCommands();
        }
    }

    #endregion
}

#region Support Classes

// Firmwares are organised as follow:
// Manufacturers -> Devices -> Specific Firmware Package
// Ex:
//    Manufacturer A:
//        Device 1:
//             Firmware package a (ie. OpenIPC-FPV for SSC338Q);
//             Firmware package b (ie. RubyFPV);
//        Device 2:
//             Firmware package a;
//             Firmware package b;
//             Firmware package c;
//             Firmware package d;
//        ...
//    Manufacturer B:
//        Device 1:
//             Firmware package a;
//        ...
// To allow for:
//    * generic firmwares that can either be used on multiple devices from same manufacturer,
//      either can be used for multiple manufacturers,
//    * generic manufacturers (ie. Aliexpress SSC338Q generic boards)
//
// a wildcard Manufacturer and a wildcard Device is present in the tree at each mode, where appropiate, denoted with id "*" and friendly name "Generic"
//
public class FirmwareData
{
    public ObservableCollection<Manufacturer> Manufacturers { get; set; }

    public void SortCollections()
    {
        Manufacturers = new ObservableCollection<Manufacturer>(Manufacturers.OrderBy(p => p.FriendlyName));
        var genericMan = Manufacturers.Single(p => p.Name.Equals("*"));
        Manufacturers.Remove(Manufacturers.Where(p => p.Name.Equals("*")).Single());
        Manufacturers.Insert(0, genericMan);
        foreach (var manufacturer in Manufacturers)
        {
            manufacturer.SortCollections();
        }
    }
}

public class Manufacturer
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public ObservableCollection<Device> Devices { get; set; }

    public void SortCollections()
    {
        Devices = new ObservableCollection<Device>(Devices.OrderBy(p => p.FriendlyName));
        foreach (var device in Devices)
        {
            device.SortCollections();
        }
    }
}

public class Device
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public ObservableCollection<FirmwarePackage> FirmwarePackages { get; set; }

    public void SortCollections()
    {
        FirmwarePackages = new ObservableCollection<FirmwarePackage>(FirmwarePackages.OrderBy(p => p.FriendlyName));
    }
}

public class FirmwarePackage
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string PackageFile { get; set; }
}
#endregion