namespace YtDlpGui.ViewModels;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDlpGui.Models;
using YtDlpGui.Services;

public enum DownloadMode { Audio, Video, Manual }
public enum AppPage { Home, UrlInput, Processing, AudioOptions, VideoOptions, ManualOptions, Downloading, Complete, Settings }

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomePage))]
    [NotifyPropertyChangedFor(nameof(IsUrlInputPage))]
    [NotifyPropertyChangedFor(nameof(IsProcessingPage))]
    [NotifyPropertyChangedFor(nameof(IsAudioOptionsPage))]
    [NotifyPropertyChangedFor(nameof(IsDownloadingPage))]
    [NotifyPropertyChangedFor(nameof(IsCompletePage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    [NotifyPropertyChangedFor(nameof(IsVideoOptionsPage))]
    [NotifyPropertyChangedFor(nameof(IsManualOptionsPage))]
    private AppPage _currentPage = AppPage.Home;

    public bool IsHomePage => CurrentPage == AppPage.Home;
    public bool IsUrlInputPage => CurrentPage == AppPage.UrlInput;
    public bool IsProcessingPage => CurrentPage == AppPage.Processing;
    public bool IsAudioOptionsPage => CurrentPage == AppPage.AudioOptions;
    public bool IsDownloadingPage => CurrentPage == AppPage.Downloading;
    public bool IsCompletePage => CurrentPage == AppPage.Complete;
    public bool IsSettingsPage => CurrentPage == AppPage.Settings;
    public bool IsVideoOptionsPage => CurrentPage == AppPage.VideoOptions;
    public bool IsManualOptionsPage => CurrentPage == AppPage.ManualOptions;

    public string UrlInputSubtitle => DownloadMode == DownloadMode.Manual
        ? "Paste the link to fetch all available formats"
        : IsVideoMode 
            ? "Paste the link to the video you want to download" 
            : "Paste the link to the audio you want to download";
    public string ProcessingText => App.IsPlusVersion
        ? "Fetching all formats..."
        : DownloadMode == DownloadMode.Manual
            ? "Fetching all available formats..."
            : IsVideoMode 
                ? "Fetching video formats..." 
                : "Fetching audio formats...";

    [ObservableProperty] private AppSettings _appSettings = new();
    
    partial void OnAppSettingsChanged(AppSettings? oldValue, AppSettings newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= AppSettings_PropertyChanged;
        if (newValue != null) newValue.PropertyChanged += AppSettings_PropertyChanged;
    }
    

    
    public List<string> ThemeOptions { get; } = new() { "Dark", "Light", "System" };
    public List<string> FileExistsOptions { get; } = new() { "Overwrite", "Auto-Rename" };
    public List<string> AudioConversionOptions { get; } = new() { "Native", "MP3", "FLAC" };
    public List<string> SpeedLimitOptions { get; } = new() { "No Limit", "2MB/s", "5MB/s", "10MB/s" };
    public List<string> ContainerOptions { get; } = new() { "MP4", "MKV" };

    public List<string> SettingsCategories { get; } = App.IsPlusVersion
        ? new() { "Appearance", "Downloads", "Audio", "Network", "Advanced", "About" }
        : new() { "Appearance", "Downloads", "Audio", "Network", "About" };
    
    [ObservableProperty] private string _selectedSettingsCategory = "Appearance";

    [ObservableProperty] private string _videoUrl = string.Empty;
    
    partial void OnVideoUrlChanged(string value)
    {
        if (_isAutoPasting) return;
        ErrorMessage = string.Empty;
        IsUrlFromClipboard = false;
    }

    [ObservableProperty] private bool _isUrlFromClipboard;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private double _progressPercentage;
    [ObservableProperty] private string _progressSpeed = string.Empty;
    [ObservableProperty] private string _progressEta = string.Empty;
    [ObservableProperty] private string _elapsedTime = "00:00";
    [ObservableProperty] private string _savePath = string.Empty;
    [ObservableProperty] private bool _openFolderAfterDownload = true;
    [ObservableProperty] private List<string> _availableCodecs = new();
    [ObservableProperty] private string _selectedCodec = string.Empty;
    [ObservableProperty] private List<AudioFormatOption> _availableQualities = new();
    [ObservableProperty] private AudioFormatOption? _selectedFormat;
    [ObservableProperty] private string _recommendationText = string.Empty;
    [ObservableProperty] private string _expectedFileSizeLabel = string.Empty;
    [ObservableProperty] private long _expectedFileSizeBytes;

    // Video mode
    [ObservableProperty] private DownloadMode _downloadMode = DownloadMode.Audio;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UrlInputSubtitle))]
    [NotifyPropertyChangedFor(nameof(ProcessingText))]
    private bool _isVideoMode;
    [ObservableProperty] private List<string> _availableResolutions = new();
    [ObservableProperty] private string _selectedResolution = string.Empty;
    [ObservableProperty] private List<string> _availableFrameRates = new();
    [ObservableProperty] private string _selectedFrameRate = string.Empty;
    [ObservableProperty] private string _selectedContainer = "MP4";
    [ObservableProperty] private string _audioQualityDisplay = "Medium (AAC)";

    // Manual mode properties
    public bool IsPlusVersion => App.IsPlusVersion;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(VideoFormats))]
    [NotifyPropertyChangedFor(nameof(AudioFormats))]
    private List<ManualFormatOption> _manualFormats = new();
    
    public IEnumerable<ManualFormatOption> VideoFormats => ManualFormats.Where(f => f.IsVideoFormat);
    public IEnumerable<ManualFormatOption> AudioFormats => ManualFormats.Where(f => f.IsAudioFormat);

    [ObservableProperty] private ManualFormatOption? _selectedManualFormat;
    [ObservableProperty] private ManualFormatOption? _selectedVideoFormat;
    [ObservableProperty] private ManualFormatOption? _selectedAudioFormat;
    [ObservableProperty] private string _manualFormatFilter = "All";
    [ObservableProperty] private bool _useCookies;
    [ObservableProperty] private bool _embedSubtitles;
    [ObservableProperty] private string _manualArguments = string.Empty;
    [ObservableProperty] private bool _isManualMode;
    public List<string> ManualFilterOptions { get; } = new() { "All", "Video", "Audio" };

    // Remuxer properties
    public string AppName => App.IsPlusVersion ? "Candy Plus" : "Candy";
    public string AppVersion => App.IsPlusVersion ? "1.0.0 Beta" : "1.0.1";
    public string AppDescription => App.IsPlusVersion 
        ? "An advanced interface for downloading audio and video with manual format selection, remuxing, and advanced options powered by yt-dlp."
        : "A simple and clean interface for downloading audio and video from YouTube.";
        
    [ObservableProperty] private bool _isRemuxerEnabled;
    public List<string> RemuxContainers { get; } = new() { "mp4", "mkv", "webm" };
    [ObservableProperty] private string _selectedRemuxContainer = "mp4";
    
    // Will hold the items selected in the view
    public System.Collections.ObjectModel.ObservableCollection<ManualFormatOption> SelectedManualFormats { get; } = new();

    private List<ManualFormatOption> _allManualFormats = new();
    private List<AudioFormatOption> _allFormats = new();
    private List<YtDlpGui.Models.VideoFormatOption> _allVideoFormats = new();
    private string _bestAudioFormatId = string.Empty;
    private YtDlpService _ytDlpService = new();
    private SettingsService _settingsService = new();
    private CancellationTokenSource? _downloadCts;
    private Stopwatch _stopwatch = new();
    private DispatcherTimer _elapsedTimer;
    private DateTime _lastProgressUpdateTime = DateTime.Now;
    private bool _isAutoPasting;

    public MainViewModel()
    {
        AppSettings = _settingsService.Load();
        
        SavePath = string.IsNullOrWhiteSpace(AppSettings.DefaultAudioPath) 
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) 
            : AppSettings.DefaultAudioPath;
            
        OpenFolderAfterDownload = AppSettings.OpenFolderAfterDownload;

        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (s, e) => 
        {
            ElapsedTime = $"{_stopwatch.Elapsed.Minutes:D2}:{_stopwatch.Elapsed.Seconds:D2}";
            if (IsDownloading && (DateTime.Now - _lastProgressUpdateTime).TotalSeconds > 2)
            {
                ProgressSpeed = "0 B/s";
            }
        };

        CheckClipboardForUrl();
        _ytDlpService.CleanupOrphanedMeiFolders();
    }

    private void CheckClipboardForUrl()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"https://(www\.)?(youtube\.com|youtu\.be)"))
                {
                    _isAutoPasting = true;
                    VideoUrl = text;
                    IsUrlFromClipboard = true;
                    _isAutoPasting = false;
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    private void NavigateToAudio()
    {
        DownloadMode = DownloadMode.Audio;
        IsVideoMode = false;
        SavePath = string.IsNullOrWhiteSpace(AppSettings.DefaultAudioPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            : AppSettings.DefaultAudioPath;
        CurrentPage = AppPage.UrlInput;
        CheckClipboardForUrl();
    }

    [RelayCommand]
    private void NavigateToVideo()
    {
        DownloadMode = DownloadMode.Video;
        IsVideoMode = true;
        IsManualMode = false;
        SavePath = string.IsNullOrWhiteSpace(AppSettings.DefaultVideoPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : AppSettings.DefaultVideoPath;
        CurrentPage = AppPage.UrlInput;
        CheckClipboardForUrl();
    }

    [RelayCommand]
    private void NavigateToManual()
    {
        DownloadMode = DownloadMode.Manual;
        IsVideoMode = false;
        IsManualMode = true;
        SavePath = string.IsNullOrWhiteSpace(AppSettings.DefaultVideoPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            : AppSettings.DefaultVideoPath;
        CurrentPage = AppPage.UrlInput;
        CheckClipboardForUrl();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentPage == AppPage.UrlInput)
        {
            CurrentPage = AppPage.Home;
        }
        else if (CurrentPage == AppPage.AudioOptions)
        {
            CurrentPage = AppPage.UrlInput;
        }
        else if (CurrentPage == AppPage.VideoOptions)
        {
            CurrentPage = AppPage.UrlInput;
        }
        else if (CurrentPage == AppPage.ManualOptions)
        {
            CurrentPage = AppPage.UrlInput;
        }
        else if (CurrentPage == AppPage.Downloading)
        {
            CancelDownload();
            if (DownloadMode == DownloadMode.Manual)
                CurrentPage = AppPage.ManualOptions;
            else
                CurrentPage = DownloadMode == DownloadMode.Video ? AppPage.VideoOptions : AppPage.AudioOptions;
        }
        else if (CurrentPage == AppPage.Settings)
        {
            CurrentPage = AppPage.Home;
        }
    }

    [RelayCommand]
    private async Task FetchFormats()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl) || !VideoUrl.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Invalid video URL. Please check the link and try again.";
            return;
        }

        ErrorMessage = string.Empty;

        CurrentPage = AppPage.Processing;
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            if (DownloadMode == DownloadMode.Video)
            {
                var result = await _ytDlpService.GetVideoFormatsAsync(VideoUrl);
                _allVideoFormats = result.VideoFormats;
                _bestAudioFormatId = result.BestAudioFormatId;

                if (_allVideoFormats.Count == 0)
                    throw new Exception("No video formats found for this URL.");

                // Extract unique resolution labels, ordered by height descending
                AvailableResolutions = _allVideoFormats
                    .Select(f => f.ResolutionLabel)
                    .Distinct()
                    .ToList();

                // Auto-select first (highest) resolution
                if (AvailableResolutions.Count > 0)
                    SelectedResolution = AvailableResolutions[0];

                SelectedContainer = "MP4";
                AudioQualityDisplay = "Medium (AAC)";
                UpdateVideoExpectedSize();
                CurrentPage = AppPage.VideoOptions;
            }
            else if (DownloadMode == DownloadMode.Manual)
            {
                _allManualFormats = await _ytDlpService.GetAllFormatsAsync(VideoUrl);

                if (_allManualFormats.Count == 0)
                    throw new Exception("No formats found for this URL.");

                ManualFormatFilter = "All";
                ApplyManualFilter();
                CurrentPage = AppPage.ManualOptions;
            }
            else
            {
                _allFormats = await _ytDlpService.GetAudioFormatsAsync(VideoUrl);

                if (_allFormats.Count == 0)
                    throw new Exception("No audio formats found for this URL.");

                AvailableCodecs = _allFormats.Select(f => f.CodecDisplay).Distinct().ToList();

                var recommendedFormat = _allFormats.FirstOrDefault(f => f.IsRecommended);
                if (recommendedFormat != null)
                    SelectedCodec = recommendedFormat.CodecDisplay;
                else if (AvailableCodecs.Count > 0)
                    SelectedCodec = AvailableCodecs[0];

                CurrentPage = AppPage.AudioOptions;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentPage = AppPage.UrlInput;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCodecChanged(string value)
    {
        UpdateQualitiesForCodec(value);
    }



    private void UpdateQualitiesForCodec(string codecDisplay)
    {
        if (string.IsNullOrEmpty(codecDisplay)) return;
        
        AvailableQualities = _allFormats.Where(f => f.CodecDisplay == codecDisplay).ToList();
        
        var recommended = AvailableQualities.FirstOrDefault(f => f.IsRecommended);
        if (recommended != null)
        {
            SelectedFormat = recommended;
            RecommendationText = "★ = Recommended";
        }
        else if (AvailableQualities.Count > 0)
        {
            var best = AvailableQualities.OrderByDescending(f => f.Bitrate).First();
            SelectedFormat = best;
        }
    }

    partial void OnSelectedResolutionChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        
        AvailableFrameRates = _allVideoFormats
            .Where(f => f.ResolutionLabel == value)
            .Select(f => f.Fps + "fps")
            .Distinct()
            .OrderByDescending(f => f)
            .ToList();
            
        if (AvailableFrameRates.Count > 0)
            SelectedFrameRate = AvailableFrameRates[0];
            
        UpdateVideoExpectedSize();
    }
    
    partial void OnSelectedFrameRateChanged(string value)
    {
        UpdateVideoExpectedSize();
    }
    
    partial void OnSelectedContainerChanged(string value)
    {
        UpdateVideoExpectedSize();
    }
    
    private void UpdateVideoExpectedSize()
    {
        if (string.IsNullOrEmpty(SelectedResolution) || string.IsNullOrEmpty(SelectedFrameRate)) return;
        
        int selectedHeight = _allVideoFormats
            .Where(f => f.ResolutionLabel == SelectedResolution)
            .Select(f => f.Height)
            .FirstOrDefault();
        int selectedFps = 30;
        int.TryParse(SelectedFrameRate.Replace("fps", ""), out selectedFps);

        var candidates = _allVideoFormats
            .Where(f => f.Height == selectedHeight && f.Fps == selectedFps)
            .ToList();

        string preferredCodecPrefix = SelectedContainer == "MP4" ? "avc" : "vp";
        var best = candidates
            .Where(f => f.Codec.StartsWith(preferredCodecPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Bitrate)
            .FirstOrDefault();
        best ??= candidates.OrderByDescending(f => f.Bitrate).FirstOrDefault();

        if (best != null)
        {
            ExpectedFileSizeBytes = best.FileSize;
            ExpectedFileSizeLabel = best.FileSize > 0 ? $"Approximate Size: {best.FileSizeStr}" : "Approximate Size: Unknown";
        }
    }

    partial void OnManualFormatFilterChanged(string value)
    {
        ApplyManualFilter();
    }

    private void ApplyManualFilter()
    {
        if (ManualFormatFilter == "All")
            ManualFormats = _allManualFormats.ToList();
        else if (ManualFormatFilter == "Video")
            ManualFormats = _allManualFormats.Where(f => f.Type == "video").ToList();
        else if (ManualFormatFilter == "Audio")
            ManualFormats = _allManualFormats.Where(f => f.Type == "audio").ToList();
        else if (ManualFormatFilter == "Video+Audio")
            ManualFormats = _allManualFormats.Where(f => f.Type == "video+audio").ToList();
    }

    partial void OnSelectedFormatChanged(AudioFormatOption? value)
    {
        if (value != null)
        {
            if (value.IsRecommended)
            {
                RecommendationText = "★ = Recommended";
            }
            else
            {
                RecommendationText = $"{value.CodecDisplay} {value.QualityLabel} selected.";
            }
            
            ExpectedFileSizeBytes = value.FileSize;
            ExpectedFileSizeLabel = value.FileSize > 0 ? $"Approximate Size: {value.FileSizeStr}" : "Approximate Size: Unknown";
        }
    }

    [RelayCommand]
    private void BrowseSavePath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Save Folder"
        };
        
        if (dialog.ShowDialog() == true)
        {
            SavePath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseDefaultAudioPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Default Audio Folder" };
        if (dialog.ShowDialog() == true) AppSettings.DefaultAudioPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseDefaultVideoPath()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select Default Video Folder" };
        if (dialog.ShowDialog() == true) AppSettings.DefaultVideoPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseCookiesFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Cookies File",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            AppSettings.CookiesFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        AppSettings = _settingsService.Load();
        SelectedSettingsCategory = "Appearance"; // Reset to default when opening
        CurrentPage = AppPage.Settings;
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void AppSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _settingsService.Save(AppSettings);
        if (e.PropertyName == nameof(Models.AppSettings.Theme))
        {
            App.ApplyTheme(AppSettings.Theme);
            
            // Workaround for Windows 11 DWM theme switching bug with WPF UI
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                var processModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
                if (processModule != null)
                {
                    System.Diagnostics.Process.Start(processModule.FileName);
                    System.Windows.Application.Current.Shutdown();
                }
            }
        }
    }

    [RelayCommand]
    private async Task StartDownload()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl) || !VideoUrl.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Invalid video URL. Please check the link and try again.";
            return;
        }

        if (DownloadMode == DownloadMode.Audio && SelectedFormat == null) return;
        if (DownloadMode == DownloadMode.Video && string.IsNullOrEmpty(SelectedResolution)) return;
        if (DownloadMode == DownloadMode.Manual)
        {
            if (IsRemuxerEnabled)
            {
                if (SelectedVideoFormat == null || SelectedAudioFormat == null) return;
                ExpectedFileSizeBytes = (SelectedVideoFormat?.FileSizeRaw ?? 0) + (SelectedAudioFormat?.FileSizeRaw ?? 0);
            }
            else
            {
                if (SelectedManualFormat == null) return;
                ExpectedFileSizeBytes = SelectedManualFormat?.FileSizeRaw ?? 0;
            }
            
            if (ExpectedFileSizeBytes > 0)
            {
                ExpectedFileSizeLabel = ExpectedFileSizeBytes >= 1_073_741_824 ? $"Approximate Size: ~{ExpectedFileSizeBytes / 1_073_741_824.0:F1}GB" :
                                        ExpectedFileSizeBytes >= 1_048_576 ? $"Approximate Size: ~{ExpectedFileSizeBytes / 1_048_576.0:F1}MB" :
                                        $"Approximate Size: ~{ExpectedFileSizeBytes / 1024.0:F0}KB";
            }
            else
            {
                ExpectedFileSizeLabel = "Approximate Size: Unknown";
            }
        }
        
        if (ExpectedFileSizeBytes > 0)
        {
            try
            {
                var driveRoot = Path.GetPathRoot(Path.GetFullPath(SavePath));
                if (!string.IsNullOrEmpty(driveRoot))
                {
                    var driveInfo = new DriveInfo(driveRoot);
                    if (driveInfo.IsReady)
                    {
                        // Add 10% buffer
                        long requiredSpace = (long)(ExpectedFileSizeBytes * 1.1);
                        if (driveInfo.AvailableFreeSpace < requiredSpace)
                        {
                            System.Windows.MessageBox.Show($"Not enough disk space on {driveRoot}. Required: ~{requiredSpace / 1048576} MB, Available: {driveInfo.AvailableFreeSpace / 1048576} MB.", "Disk Full", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            return;
                        }
                    }
                }
            }
            catch { }
        }
        
        CurrentPage = AppPage.Downloading;
        IsDownloading = true;
        ProgressPercentage = 0;
        ProgressSpeed = string.Empty;
        ProgressEta = string.Empty;
        ElapsedTime = "00:00";
        StatusMessage = DownloadMode == DownloadMode.Video ? "Downloading Video & Audio Tracks..." : 
                        DownloadMode == DownloadMode.Audio ? "Downloading Audio..." : 
                        "Downloading...";
        
        _lastProgressUpdateTime = DateTime.Now;
        _stopwatch.Restart();
        _elapsedTimer.Start();
        
        _downloadCts = new CancellationTokenSource();
        
        var progress = new Progress<DownloadProgress>(p => 
        {
            ProgressPercentage = p.Percentage;
            ProgressSpeed = p.SpeedDisplay;
            ProgressEta = p.Eta;
            _lastProgressUpdateTime = DateTime.Now;
        });

        try
        {
            if (DownloadMode == DownloadMode.Video)
            {
                // Find best video format for selected resolution + fps + container
                int selectedHeight = _allVideoFormats
                    .Where(f => f.ResolutionLabel == SelectedResolution)
                    .Select(f => f.Height)
                    .FirstOrDefault();
                int selectedFps = 30;
                if (!string.IsNullOrEmpty(SelectedFrameRate))
                    int.TryParse(SelectedFrameRate.Replace("fps", ""), out selectedFps);

                var candidates = _allVideoFormats
                    .Where(f => f.Height == selectedHeight && f.Fps == selectedFps)
                    .ToList();

                // Prefer codec based on container
                string preferredCodecPrefix = SelectedContainer == "MP4" ? "avc" : "vp";
                var best = candidates
                    .Where(f => f.Codec.StartsWith(preferredCodecPrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.Bitrate)
                    .FirstOrDefault();

                // Fall back to any codec
                best ??= candidates.OrderByDescending(f => f.Bitrate).FirstOrDefault();

                if (best == null)
                    throw new Exception("No matching video format found.");

                await _ytDlpService.DownloadVideoAsync(VideoUrl, best.FormatId, _bestAudioFormatId, SavePath, SelectedContainer, progress, _downloadCts.Token);
            }
            else if (DownloadMode == DownloadMode.Manual)
            {
                string formatId;
                string? extraArgs = AppSettings.EnableArgumentInjection && !string.IsNullOrWhiteSpace(ManualArguments) ? ManualArguments : null;

                if (IsRemuxerEnabled)
                {
                    if (!YtDlpGui.Services.YtDlpService.IsFFmpegAvailable())
                    {
                        System.Windows.MessageBox.Show("FFmpeg is required to combine video and audio tracks. Please place ffmpeg.exe in the application folder.", "FFmpeg Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return;
                    }

                    var videoFormat = SelectedVideoFormat;
                    var audioFormat = SelectedAudioFormat;

                    if (videoFormat == null || audioFormat == null)
                    {
                        System.Windows.MessageBox.Show("Please select exactly one video track and one audio track.", "Invalid Selection", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    
                    StatusMessage = "Downloading Video & Audio Tracks...";
                    string vCodec = videoFormat.Codec.ToLower();
                    string aCodec = audioFormat.Codec.ToLower();
                    
                    if (SelectedRemuxContainer == "mp4")
                    {
                        if (!aCodec.Contains("mp4a") && !aCodec.Contains("m4a") && !aCodec.Contains("aac") && !aCodec.Contains("mp3") && !aCodec.Contains("alac"))
                        {
                            System.Windows.MessageBox.Show($"MP4 container does not support audio codec: {aCodec}.", "Codec Mismatch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            return;
                        }
                        if (vCodec.Contains("vp8") || vCodec.Contains("vp9"))
                        {
                            System.Windows.MessageBox.Show($"MP4 container does not support video codec: {vCodec}. Try WebM or MKV.", "Codec Mismatch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            return;
                        }
                    }
                    else if (SelectedRemuxContainer == "webm")
                    {
                        if (!aCodec.Contains("opus") && !aCodec.Contains("vorbis"))
                        {
                            System.Windows.MessageBox.Show($"WebM container does not support audio codec: {aCodec}. Try MKV.", "Codec Mismatch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            return;
                        }
                        if (vCodec.Contains("avc") || vCodec.Contains("hevc"))
                        {
                            System.Windows.MessageBox.Show($"WebM container does not support video codec: {vCodec}. Try MP4 or MKV.", "Codec Mismatch", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                            return;
                        }
                    }
                    
                    formatId = $"{videoFormat.FormatId}+{audioFormat.FormatId}";
                    extraArgs = (extraArgs ?? "") + $" --merge-output-format {SelectedRemuxContainer}";
                }
                else
                {
                    if (SelectedManualFormat == null) return;
                    formatId = SelectedManualFormat.FormatId;
                }

                var proxyUrl = !string.IsNullOrWhiteSpace(AppSettings.ProxyUrl) ? AppSettings.ProxyUrl : null;
                var cookiesPath = !string.IsNullOrWhiteSpace(AppSettings.CookiesFilePath) ? AppSettings.CookiesFilePath : null;

                await _ytDlpService.DownloadManualAsync(
                    VideoUrl, formatId, SavePath,
                    proxyUrl, cookiesPath, UseCookies, EmbedSubtitles,
                    extraArgs, progress, _downloadCts.Token);
            }
            else
            {
                await _ytDlpService.DownloadAudioAsync(VideoUrl, SelectedFormat.FormatId, SavePath, progress, _downloadCts.Token);
            }

            CurrentPage = AppPage.Complete;
            StatusMessage = "Your file has been saved successfully.";
            
            if (OpenFolderAfterDownload)
            {
                Process.Start("explorer.exe", SavePath);
            }
        }
        catch (OperationCanceledException)
        {
            if (DownloadMode == DownloadMode.Manual)
                CurrentPage = AppPage.ManualOptions;
            else
                CurrentPage = DownloadMode == DownloadMode.Video ? AppPage.VideoOptions : AppPage.AudioOptions;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentPage = AppPage.UrlInput;

            if (ex.Message.Contains("(Skipped)"))
            {
                System.Windows.MessageBox.Show(
                    "The file already exists in the destination folder.", 
                    "Download Skipped", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Information);
            }
        }
        finally
        {
            IsDownloading = false;
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            
            var settings = _settingsService.Load();
            // Don't auto-save SavePath as DefaultAudioPath to avoid overwriting settings
            // But we can remember OpenFolderAfterDownload state since it's quick toggle
            settings.OpenFolderAfterDownload = OpenFolderAfterDownload;
            _settingsService.Save(settings);
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        Task.Run(() => 
        {
            try { _downloadCts?.Cancel(); } catch { }
            try { _ytDlpService.Cancel(); } catch { }
        });
    }

    [RelayCommand]
    private void GoHome()
    {
        VideoUrl = string.Empty;
        IsUrlFromClipboard = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        CurrentPage = AppPage.Home;
    }

    [RelayCommand]
    private void DownloadAnother()
    {
        ProgressPercentage = 0;
        ProgressSpeed = string.Empty;
        ProgressEta = string.Empty;
        ElapsedTime = "00:00";
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        
        CurrentPage = AppPage.UrlInput;
        CheckClipboardForUrl();
    }
}
