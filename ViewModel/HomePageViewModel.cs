using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YBSeedrClient;
using static YBSeedrClient.SeedrModels;
namespace LiveQueryChatAppMAUI.ViewModel;
/// <summary>
/// The home page view model.
/// </summary>
public partial class HomePageViewModel : ObservableObject
{

    /// <summary>
    /// The seedr service.
    /// </summary>
    private readonly ISeedrApiService _seedrService;
    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<HomePageViewModel> _logger;
    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Home Page";
    /// <summary>
    /// Gets or sets the current folder.
    /// </summary>
    [ObservableProperty]
    public partial FolderContent CurrentFolder { get; set; }
    [ObservableProperty]
    public partial FolderContent RootFolder { get; set; }
    /// <summary>
    /// Gets or sets the current files.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<SeedrFileItem> CurrentFiles { get; set; }
    /// <summary>
    /// Gets or sets the current folders.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<SeedrFolderItem> CurrentFolders { get; set; }
    /// <summary>
    /// Gets the current seedr user.
    /// </summary>
    [ObservableProperty]
    public partial SeedrUser? CurrentSeedrUser { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HomePageViewModel"/> class.
    /// </summary>
    /// <param name="seedrService">The seedr service.</param>
    /// <param name="logger">The logger.</param>
    public HomePageViewModel(ISeedrApiService seedrService, ILogger<HomePageViewModel> logger)
    {
        _seedrService = seedrService;
        _logger = logger;
        // InitializeAsync(); // Call from constructor or OnAppearing
        CurrentFiles = new ObservableCollection<SeedrFileItem>();
        CurrentFolders = new ObservableCollection<SeedrFolderItem>();
        CurrentSeedrUser = null;
        CurrentFolder = new FolderContent();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        // IMPORTANT: Ensure HttpClient for _seedrService is authenticated BEFORE calling this.
        // This might happen after a login flow.
        _logger.LogInformation("Fetching user data...");
        CurrentSeedrUser = await _seedrService.GetUserDataAsync();
        if (CurrentSeedrUser != null)
        {
            _logger.LogInformation($"User: {CurrentSeedrUser.Username}");
            await LoadRootFolderContentsAsync();
        }
        else
        {
            _logger.LogWarning("Failed to get user data. Is the client authenticated?");
        }
    }

    [RelayCommand]
    public async Task LoadRootFolderContentsAsync()
    {
        if (CurrentSeedrUser == null)
        {
            _logger.LogWarning("Cannot load folder contents, user not loaded/authenticated.");
            return;
        }

        _logger.LogInformation("Fetching root folder contents...");
        RootFolder = await _seedrService.ListRootFolderAsync();
        if (RootFolder != null)
        {
            CurrentFolders.Clear();
            CurrentFiles.Clear();
            foreach (var folder in RootFolder.Folders)
                CurrentFolders.Add(folder);
            foreach (var file in RootFolder.Files)
                CurrentFiles.Add(file);
            _logger.LogInformation($"Loaded {CurrentFolders.Count} folders and {CurrentFiles.Count} files.");
        }
        else
        {
            _logger.LogWarning("Failed to load root folder contents.");
        }
    }

    public async Task LoadSpecificFolder(string folderId)
    {
        CurrentFolder = await _seedrService.ListFolderAsync((long.Parse(folderId)));
        CurrentFolders  = CurrentFolder.Folders.ToObservableCollection();
        CurrentFiles = CurrentFolder.Files.ToObservableCollection();
    }

    // Example: Adding a magnet link (you'd get the magnet from UI input)
    [RelayCommand]
    public async Task AddMagnetAsync(string magnetLink)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
            return;

        var result = await _seedrService.AddMagnetAsync(magnetLink);
        if (result != null && result.Result)
        {
            _logger.LogInformation($"Magnet added: {result.Title}, ID: {result.UserTorrentId}");
            // Optionally refresh transfers or folder content
        }
        else
        {
            _logger.LogError($"Failed to add magnet: {result?.Error}");
            // Display error to user
        }
    }

    // Example: Download a file using the browser
    [RelayCommand]
    public async Task DownloadFileInBrowserAsync(long fileId)
    {
        bool success = await _seedrService.DownloadFileOnPreferredBrowserAsync(fileId);
        if (success)
        {
            _logger.LogInformation($"Initiated browser download for file ID: {fileId}");
        }
        else
        {
            _logger.LogError($"Failed to initiate browser download for file ID: {fileId}");
        }
    }

    // Example: Download a file using the browser
    [RelayCommand]
    public async Task DownloadFolderInBrowserAsync(long FolderId)
    {
        bool success = await _seedrService.DownloadFolderOnPreferredBrowserAsync(FolderId);
        if (success)
        {
            _logger.LogInformation($"Initiated browser download for Folder ID: {FolderId}");
        }
        else
        {
            _logger.LogError($"Failed to initiate browser download for file ID: {FolderId}");
        }
    }
}
