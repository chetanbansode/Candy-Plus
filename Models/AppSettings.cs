using CommunityToolkit.Mvvm.ComponentModel;

namespace YtDlpGui.Models;

public partial class AppSettings : ObservableObject
{
    [ObservableProperty] private string _theme = "Dark"; // Dark, Light, System
    [ObservableProperty] private string _defaultAudioPath = string.Empty;
    [ObservableProperty] private string _defaultVideoPath = string.Empty;
    [ObservableProperty] private string _fileExistsAction = "Auto-Rename"; // Overwrite, Auto-Rename, Skip
    [ObservableProperty] private bool _openFolderAfterDownload = true;
    [ObservableProperty] private string _audioConversion = "Native"; // Native, MP3, FLAC
    [ObservableProperty] private bool _embedThumbnail = true;
    [ObservableProperty] private bool _embedMetadata = true;
    [ObservableProperty] private string _speedLimit = "No Limit"; // No Limit, 2MB/s, 5MB/s, 10MB/s

    // Candy Plus settings
    [ObservableProperty] private string _proxyUrl = string.Empty;
    [ObservableProperty] private string _cookiesFilePath = string.Empty;
    [ObservableProperty] private bool _enableArgumentInjection = false;
}
