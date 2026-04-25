using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using TORQUE.Models;
using TORQUE.Services;

namespace TORQUE;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double DesignWidth = 1365d;
    private const double DesignHeight = 805d;
    private const int MinWindowScalePercent = 66;
    private const int MaxWindowScalePercent = 120;
    private const int DefaultWindowScalePercent = 90;
    private const int WmSizing = 0x0214;
    private const int WmszLeft = 1;
    private const int WmszRight = 2;
    private const int WmszTop = 3;
    private const int WmszTopLeft = 4;
    private const int WmszTopRight = 5;
    private const int WmszBottom = 6;
    private const int WmszBottomLeft = 7;
    private const int WmszBottomRight = 8;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".gif",
        ".tif",
        ".tiff",
        ".webp",
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".wav",
        ".flac",
        ".aac",
        ".ogg",
        ".m4a",
        ".wma",
        ".aif",
        ".aiff",
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".mkv",
        ".avi",
        ".webm",
        ".m4v",
    };

    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    private static readonly string[] ImageOutputFormats = ["jpg", "png", "webp", "bmp", "tiff"];
    private static readonly string[] AudioOutputFormats = ["wav", "mp3", "flac", "aac", "m4a", "ogg", "aiff"];
    private static readonly string[] VideoOutputFormats = ["mp4", "mov", "mkv", "webm", "avi", "gif", "mp3", "wav"];

    private static readonly PdfActionOption[] PdfActionOptionsSource =
    [
        new("merge", "Merge multiple PDFs", "modify", "pdf"),
        new("invert-order", "Invert page order", "modify", "pdf"),
        new("split-pages", "Export all pages as separate PDFs", "modify", "pdf"),
        new("pages-jpg", "Export all pages as JPG files", "convert", "jpg"),
        new("pages-png", "Export all pages as PNG files", "convert", "png"),
        new("pages-webp", "Export all pages as WEBP files", "convert", "webp"),
    ];

    private readonly SettingsService _settingsService = new();
    private readonly ExportPathService _exportPathService = new();
    private readonly FfmpegService _ffmpegService;
    private readonly PdfService _pdfService = new();
    private readonly string _cacheRoot;
    private readonly string _brandingRoot;
    private readonly DispatcherTimer _previewStopTimer;
    private readonly DispatcherTimer _audioPreviewStopTimer;
    private readonly DispatcherTimer _toastDismissTimer;

    private AppSettings _settings = new();
    private MediaKind _currentMediaKind = MediaKind.None;
    private MediaFileItem? _selectedMediaItem;
    private string _selectedOutputFormat = string.Empty;
    private OutputTargetOption? _selectedOutputTarget;
    private bool _isBusy;
    private string _busyMessage = "Preparing files...";
    private string _busyDetailMessage = string.Empty;
    private double _busyProgressValue;
    private double _busyProgressMaximum = 1d;
    private bool _isBusyProgressIndeterminate = true;
    private bool _isOptionsOpen;
    private int _imageCompressionPercent;
    private int _videoResolutionDivisor = 1;
    private string _defaultExportPathDraft = string.Empty;
    private bool _createDateFoldersDraft = true;
    private bool _addCustomSuffixDraft;
    private string _customSuffixDraft = string.Empty;
    private bool _openExportFolderAfterExportDraft = true;
    private bool _returnHomeAfterExportDraft;
    private bool _removeMetadataDraft = true;
    private int _defaultImageCompressionDraft;
    private int _defaultVideoResolutionDraft = 1;
    private bool _defaultVideoPreviewLoopDraft = true;
    private int _defaultWindowScaleDraft = DefaultWindowScalePercent;
    private string _defaultImageFormatDraft = "jpg";
    private string _defaultAudioFormatDraft = "wav";
    private string _defaultVideoFormatDraft = "mp4";
    private PdfActionOption? _selectedPdfAction;
    private PdfActionOption? _defaultPdfActionDraft;
    private string _themeTextColorHexDraft = AppInfo.DefaultThemeTextColorHex;
    private string _themeBackgroundColorHexDraft = AppInfo.DefaultThemeBackgroundColorHex;
    private string _landingExportSummary = string.Empty;
    private string _optionsPreviewPath = string.Empty;
    private string _statusMessage = string.Empty;
    private string _toastMessage = string.Empty;
    private bool _isVideoPreviewFallbackVisible = true;
    private bool _isPreviewPlaying;
    private string? _customOutputFolder;
    private bool _suppressOutputSelectionChanged;
    private double _editingTrimStartBackup;
    private double _editingTrimEndBackup;
    private double _editingPreviewStartBackup;
    private double _editingPlaybackSpeedBackup = 1d;
    private double _editingCropLeftBackup;
    private double _editingCropTopBackup;
    private double _editingCropRightBackup = 1d;
    private double _editingCropBottomBackup = 1d;
    private bool _isInitialized;
    private string? _loadedVideoSourcePath;
    private string? _loadedAudioSourcePath;
    private TimeSpan _pendingPreviewSeek = TimeSpan.Zero;
    private TimeSpan _pendingAudioPreviewSeek = TimeSpan.Zero;
    private bool _playPreviewWhenReady;
    private bool _playAudioWhenReady;
    private bool _isAudioPreviewPlaying;
    private bool _isVideoPreviewLoopEnabled = true;
    private bool _isUpdatingPreviewStartFromPlayback;

    public MainWindow()
    {
        InitializeComponent();

        _ffmpegService = new FfmpegService(AppContext.BaseDirectory);
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppInfo.DataDirectoryName,
            "cache");
        _brandingRoot = Path.Combine(AppContext.BaseDirectory, AppInfo.BrandingFolderName);

        Directory.CreateDirectory(_cacheRoot);
        Directory.CreateDirectory(_brandingRoot);

        MediaItems.CollectionChanged += (_, _) => NotifyEditorStateChanged();

        _previewStopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _previewStopTimer.Tick += PreviewStopTimer_Tick;

        _audioPreviewStopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _audioPreviewStopTimer.Tick += AudioPreviewStopTimer_Tick;

        _toastDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };
        _toastDismissTimer.Tick += ToastDismissTimer_Tick;

        RefreshPdfActionOptions(useDraftOptions: false);
        RefreshPdfActionOptions(useDraftOptions: true);

        DataContext = this;

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MediaFileItem> MediaItems { get; } = new();

    public ObservableCollection<string> AvailableFormats { get; } = new();

    public ObservableCollection<OutputTargetOption> OutputTargetOptions { get; } = new();

    public ObservableCollection<string> ImageFormatOptions { get; } = new(ImageOutputFormats);

    public ObservableCollection<string> AudioFormatOptions { get; } = new(AudioOutputFormats);

    public ObservableCollection<string> VideoFormatOptions { get; } = new(VideoOutputFormats);

    public ObservableCollection<PdfActionOption> PdfActionOptions { get; } = new();

    public ObservableCollection<PdfActionOption> DefaultPdfActionOptions { get; } = new();

    public bool IsLandingView => _currentMediaKind == MediaKind.None;

    public bool IsEditorView => _currentMediaKind != MediaKind.None;

    public bool IsStandardFormatEditor => IsImageEditor || IsAudioEditor || IsVideoEditor;

    public bool IsImageEditor => _currentMediaKind == MediaKind.Image;

    public bool IsImageOverviewVisible => IsImageEditor && SelectedMediaItem is null;

    public bool IsImageCropVisible => IsImageEditor && SelectedMediaItem is not null;

    public bool IsAudioEditor => _currentMediaKind == MediaKind.Audio;

    public bool IsVideoEditor => _currentMediaKind == MediaKind.Video;

    public bool IsPdfEditor => _currentMediaKind == MediaKind.Pdf;

    public bool IsAudioOverviewVisible => IsAudioEditor && SelectedMediaItem is null;

    public bool IsAudioTrimVisible => IsAudioEditor && SelectedMediaItem is not null;

    public bool IsVideoOverviewVisible => IsVideoEditor && SelectedMediaItem is null;

    public bool IsVideoTrimVisible => IsVideoEditor && SelectedMediaItem is not null;

    public bool IsPdfOverviewVisible => IsPdfEditor;

    public string EditorTitle => _currentMediaKind switch
    {
        MediaKind.Image => "IMAGE\nCONVERTER",
        MediaKind.Audio => "AUDIO\nCONVERTER",
        MediaKind.Video => "VIDEO\nCONVERTER",
        MediaKind.Pdf => "PDF\nCONVERTER",
        _ => string.Empty,
    };

    public string LoadedCountLabel => _currentMediaKind switch
    {
        MediaKind.Image => $"IMAGES LOADED: {MediaItems.Count}",
        MediaKind.Audio => $"FILES LOADED: {MediaItems.Count}",
        MediaKind.Video when MediaItems.Count == 1 => "VIDEO LOADED: 1",
        MediaKind.Video => $"VIDEOS LOADED: {MediaItems.Count}",
        MediaKind.Pdf when MediaItems.Count == 1 => "PDF LOADED: 1",
        MediaKind.Pdf => $"PDFS LOADED: {MediaItems.Count}",
        _ => string.Empty,
    };

    public string SelectedOutputFormat
    {
        get => _selectedOutputFormat;
        set => SetProperty(ref _selectedOutputFormat, value);
    }

    public OutputTargetOption? SelectedOutputTarget
    {
        get => _selectedOutputTarget;
        set => SetProperty(ref _selectedOutputTarget, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    public string BusyDetailMessage
    {
        get => _busyDetailMessage;
        set
        {
            if (SetProperty(ref _busyDetailMessage, value))
            {
                OnPropertyChanged(nameof(HasBusyDetailMessage));
            }
        }
    }

    public bool HasBusyDetailMessage => !string.IsNullOrWhiteSpace(BusyDetailMessage);

    public double BusyProgressValue
    {
        get => _busyProgressValue;
        set => SetProperty(ref _busyProgressValue, value);
    }

    public double BusyProgressMaximum
    {
        get => _busyProgressMaximum;
        set => SetProperty(ref _busyProgressMaximum, value);
    }

    public bool IsBusyProgressIndeterminate
    {
        get => _isBusyProgressIndeterminate;
        set => SetProperty(ref _isBusyProgressIndeterminate, value);
    }

    public bool IsOptionsOpen
    {
        get => _isOptionsOpen;
        set => SetProperty(ref _isOptionsOpen, value);
    }

    public int ImageCompressionPercent
    {
        get => _imageCompressionPercent;
        set
        {
            if (SetProperty(ref _imageCompressionPercent, value))
            {
                OnPropertyChanged(nameof(CompressionLabel));
            }
        }
    }

    public int VideoResolutionDivisor
    {
        get => _videoResolutionDivisor;
        set
        {
            if (SetProperty(ref _videoResolutionDivisor, value))
            {
                OnPropertyChanged(nameof(ResolutionLabel));
            }
        }
    }

    public string CompressionLabel => $"{ImageCompressionPercent}%";

    public string ResolutionLabel => $"x{VideoResolutionDivisor}";

    public string DefaultExportPathDraft
    {
        get => _defaultExportPathDraft;
        set
        {
            if (SetProperty(ref _defaultExportPathDraft, value))
            {
                RefreshPreviewTexts();
            }
        }
    }

    public bool CreateDateFoldersDraft
    {
        get => _createDateFoldersDraft;
        set
        {
            if (SetProperty(ref _createDateFoldersDraft, value))
            {
                RefreshPreviewTexts();
            }
        }
    }

    public bool AddCustomSuffixDraft
    {
        get => _addCustomSuffixDraft;
        set => SetProperty(ref _addCustomSuffixDraft, value);
    }

    public string CustomSuffixDraft
    {
        get => _customSuffixDraft;
        set => SetProperty(ref _customSuffixDraft, value);
    }

    public bool OpenExportFolderAfterExportDraft
    {
        get => _openExportFolderAfterExportDraft;
        set => SetProperty(ref _openExportFolderAfterExportDraft, value);
    }

    public bool ReturnHomeAfterExportDraft
    {
        get => _returnHomeAfterExportDraft;
        set => SetProperty(ref _returnHomeAfterExportDraft, value);
    }

    public bool RemoveMetadataDraft
    {
        get => _removeMetadataDraft;
        set => SetProperty(ref _removeMetadataDraft, value);
    }

    public string LandingExportSummary
    {
        get => _landingExportSummary;
        private set => SetProperty(ref _landingExportSummary, value);
    }

    public string OptionsPreviewPath
    {
        get => _optionsPreviewPath;
        private set => SetProperty(ref _optionsPreviewPath, value);
    }

    public int DefaultImageCompressionDraft
    {
        get => _defaultImageCompressionDraft;
        set
        {
            if (SetProperty(ref _defaultImageCompressionDraft, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(DefaultImageCompressionDraftLabel));
            }
        }
    }

    public int DefaultVideoResolutionDraft
    {
        get => _defaultVideoResolutionDraft;
        set
        {
            if (SetProperty(ref _defaultVideoResolutionDraft, Math.Clamp(value, 1, 4)))
            {
                OnPropertyChanged(nameof(DefaultVideoResolutionDraftLabel));
            }
        }
    }

    public string DefaultImageCompressionDraftLabel => $"{DefaultImageCompressionDraft}%";

    public string DefaultVideoResolutionDraftLabel => $"x{DefaultVideoResolutionDraft}";

    public bool DefaultVideoPreviewLoopDraft
    {
        get => _defaultVideoPreviewLoopDraft;
        set => SetProperty(ref _defaultVideoPreviewLoopDraft, value);
    }

    public int DefaultWindowScaleDraft
    {
        get => _defaultWindowScaleDraft;
        set
        {
            if (SetProperty(ref _defaultWindowScaleDraft, Math.Clamp(value, MinWindowScalePercent, MaxWindowScalePercent)))
            {
                OnPropertyChanged(nameof(DefaultWindowScaleDraftLabel));
            }
        }
    }

    public string DefaultWindowScaleDraftLabel => $"{DefaultWindowScaleDraft}%";

    public string DefaultImageFormatDraft
    {
        get => _defaultImageFormatDraft;
        set => SetProperty(ref _defaultImageFormatDraft, value);
    }

    public string DefaultAudioFormatDraft
    {
        get => _defaultAudioFormatDraft;
        set => SetProperty(ref _defaultAudioFormatDraft, value);
    }

    public string DefaultVideoFormatDraft
    {
        get => _defaultVideoFormatDraft;
        set => SetProperty(ref _defaultVideoFormatDraft, value);
    }

    public PdfActionOption? SelectedPdfAction
    {
        get => _selectedPdfAction;
        set => SetProperty(ref _selectedPdfAction, value);
    }

    public PdfActionOption? DefaultPdfActionDraft
    {
        get => _defaultPdfActionDraft;
        set => SetProperty(ref _defaultPdfActionDraft, value);
    }

    public string ThemeTextColorHexDraft
    {
        get => _themeTextColorHexDraft;
        set
        {
            if (SetProperty(ref _themeTextColorHexDraft, value))
            {
                OnPropertyChanged(nameof(ThemeColorPreviewBrush));
            }
        }
    }

    public Brush ThemeColorPreviewBrush => CreateThemeBrush(ThemeTextColorHexDraft);

    public string ThemeBackgroundColorHexDraft
    {
        get => _themeBackgroundColorHexDraft;
        set
        {
            if (SetProperty(ref _themeBackgroundColorHexDraft, value))
            {
                OnPropertyChanged(nameof(ThemeBackgroundPreviewBrush));
            }
        }
    }

    public Brush ThemeBackgroundPreviewBrush => CreateThemeBrush(ThemeBackgroundColorHexDraft, AppInfo.DefaultThemeBackgroundColorHex);

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string ToastMessage
    {
        get => _toastMessage;
        private set
        {
            if (SetProperty(ref _toastMessage, value))
            {
                OnPropertyChanged(nameof(HasToastMessage));
            }
        }
    }

    public bool HasToastMessage => !string.IsNullOrWhiteSpace(ToastMessage);

    public MediaFileItem? SelectedMediaItem
    {
        get => _selectedMediaItem;
        private set
        {
            if (ReferenceEquals(_selectedMediaItem, value))
            {
                return;
            }

            if (_selectedMediaItem is not null)
            {
                _selectedMediaItem.PropertyChanged -= SelectedMediaItem_PropertyChanged;
            }

            _selectedMediaItem = value;

            if (_selectedMediaItem is not null)
            {
                _selectedMediaItem.PropertyChanged += SelectedMediaItem_PropertyChanged;
            }

            OnPropertyChanged();
            NotifyEditorStateChanged();
        }
    }

    public bool IsVideoPreviewFallbackVisible
    {
        get => _isVideoPreviewFallbackVisible;
        set => SetProperty(ref _isVideoPreviewFallbackVisible, value);
    }

    public string VideoPlayButtonText => _isPreviewPlaying ? "\u25A0" : "\u25B6";

    public string AudioPlayButtonText => _isAudioPreviewPlaying ? "\u25A0" : "\u25B6";

    public bool IsVideoPreviewLoopEnabled
    {
        get => _isVideoPreviewLoopEnabled;
        set
        {
            if (SetProperty(ref _isVideoPreviewLoopEnabled, value))
            {
                OnPropertyChanged(nameof(VideoLoopButtonText));
            }
        }
    }

    public string VideoLoopButtonText => IsVideoPreviewLoopEnabled ? "LOOP ON" : "LOOP OFF";

    public string AppDisplayName => AppInfo.DisplayName;

    public string AppVersion => AppInfo.Version;

    public string AppDisplayCreditLine => AppInfo.DisplayCreditLine;

    public string? HeaderLogoPath => FindBrandingAsset("logo", ".png", ".jpg", ".jpeg", ".bmp");

    public bool HasHeaderLogo => !string.IsNullOrWhiteSpace(HeaderLogoPath);

    public bool HasNoHeaderLogo => !HasHeaderLogo;

    public string? AppWindowIconPath => FindBrandingAsset("icon", ".ico", ".png") ?? FindBrandingAsset("ico", ".ico", ".png");

    public ImageSource? AppWindowIconSource => LoadBrandingImage(AppWindowIconPath);

    private static double WindowAspectRatio => DesignWidth / DesignHeight;

    private void LoadDraftsFromSettings()
    {
        DefaultExportPathDraft = _settings.DefaultExportPath;
        CreateDateFoldersDraft = _settings.CreateDateFolders;
        DefaultImageCompressionDraft = _settings.DefaultImageCompressionPercent;
        DefaultVideoResolutionDraft = _settings.DefaultVideoResolutionDivisor;
        DefaultVideoPreviewLoopDraft = _settings.DefaultVideoPreviewLoopEnabled;
        DefaultWindowScaleDraft = _settings.DefaultWindowScalePercent;
        DefaultImageFormatDraft = _settings.DefaultImageFormat;
        DefaultAudioFormatDraft = _settings.DefaultAudioFormat;
        DefaultVideoFormatDraft = _settings.DefaultVideoFormat;
        DefaultPdfActionDraft = FindPdfActionOption(_settings.DefaultPdfAction);
        ThemeTextColorHexDraft = _settings.ThemeTextColorHex;
        ThemeBackgroundColorHexDraft = _settings.ThemeBackgroundColorHex;
        AddCustomSuffixDraft = _settings.AddCustomSuffixToFiles;
        CustomSuffixDraft = _settings.CustomSuffix;
        OpenExportFolderAfterExportDraft = _settings.OpenExportFolderAfterExport;
        ReturnHomeAfterExportDraft = _settings.ReturnHomeAfterExport;
        RemoveMetadataDraft = _settings.RemoveMetadata;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _settings = await _settingsService.LoadAsync();
        LoadDraftsFromSettings();
        ApplyDefaultWindowSize(_settings.DefaultWindowScalePercent, centerOnWorkArea: true);
        ApplyTheme(_settings.ThemeTextColorHex, _settings.ThemeBackgroundColorHex);
        RefreshPreviewTexts();
        RebuildOutputTargetOptions(selectCustom: false);
        NotifyEditorStateChanged();

        if (!_ffmpegService.IsAvailable)
        {
            StatusMessage = "Bundled FFmpeg tools are missing. Add ffmpeg.exe and ffprobe.exe under tools\\ffmpeg\\bin so previews and exports can run.";
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
    }

    private void ApplyDefaultWindowSize(int scalePercent, bool centerOnWorkArea)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var normalizedScale = Math.Clamp(scalePercent, MinWindowScalePercent, MaxWindowScalePercent);
        var targetWidth = Math.Max(MinWidth, Math.Round((DesignWidth * normalizedScale) / 100d));
        var targetHeight = Math.Max(MinHeight, Math.Round((DesignHeight * normalizedScale) / 100d));
        var workArea = SystemParameters.WorkArea;
        var currentWidth = ActualWidth > 0 ? ActualWidth : Width;
        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
        var currentLeft = Left;
        var currentTop = Top;

        Width = targetWidth;
        Height = targetHeight;

        if (centerOnWorkArea)
        {
            Left = workArea.Left + ((workArea.Width - targetWidth) / 2d);
            Top = workArea.Top + ((workArea.Height - targetHeight) / 2d);
            return;
        }

        var desiredLeft = currentLeft + ((currentWidth - targetWidth) / 2d);
        var desiredTop = currentTop + ((currentHeight - targetHeight) / 2d);
        var maxLeft = workArea.Right - targetWidth;
        var maxTop = workArea.Bottom - targetHeight;

        Left = Math.Min(Math.Max(workArea.Left, desiredLeft), Math.Max(workArea.Left, maxLeft));
        Top = Math.Min(Math.Max(workArea.Top, desiredTop), Math.Max(workArea.Top, maxTop));
    }

    private async void ImportFilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Supported media|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a;*.wma;*.aif;*.aiff;*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.pdf|All files|*.*",
        };

        if (dialog.ShowDialog() == true)
        {
            await ImportFilesAsync(dialog.FileNames);
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (IsBusy)
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            await ImportFilesAsync(files);
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async Task ImportFilesAsync(IEnumerable<string> incomingPaths)
    {
        try
        {
            var fileInfos = incomingPaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new
                {
                    Path = path,
                    Kind = DetectMediaKind(path),
                })
                .Where(item => item.Kind != MediaKind.None)
                .ToList();

            if (fileInfos.Count == 0)
            {
                MessageBox.Show(this, "Please drop supported image, audio, video, or PDF files.", "Nothing To Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var detectedKinds = fileInfos
                .Select(item => item.Kind)
                .Distinct()
                .ToList();

            if (detectedKinds.Count > 1)
            {
                MessageBox.Show(this, "Import one media type at a time so the app can open the right editor.", "Mixed File Types", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var importedKind = detectedKinds[0];

            if (_currentMediaKind != MediaKind.None &&
                importedKind != _currentMediaKind &&
                MediaItems.Count > 0)
            {
                var replace = MessageBox.Show(
                    this,
                    "These files are a different media type and will replace the current batch. Continue?",
                    "Replace Current Batch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (replace != MessageBoxResult.Yes)
                {
                    return;
                }

                ResetCurrentBatch();
            }

            SetCurrentMediaKind(importedKind);

            var existingPaths = MediaItems
                .Select(item => item.SourcePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var newItems = fileInfos
                .Where(item => !existingPaths.Contains(item.Path))
                .Select(item => new MediaFileItem(item.Path, item.Kind))
                .ToList();

            if (newItems.Count == 0)
            {
                StatusMessage = "Those files are already in the current batch.";
                return;
            }

            IsBusy = true;
            SetBusyState("Preparing files...", true, detailMessage: "Importing your media.");

            for (var index = 0; index < newItems.Count; index++)
            {
                BusyMessage = $"Preparing {index + 1} of {newItems.Count}";
                BusyDetailMessage = newItems[index].DisplayName;
                var item = newItems[index];
                MediaItems.Add(item);
                await PrepareImportedItemAsync(item);
            }

            if ((importedKind == MediaKind.Audio || importedKind == MediaKind.Video) &&
                MediaItems.Count == 1)
            {
                await OpenTrimEditorAsync(MediaItems[0]);
            }

            StatusMessage = importedKind == MediaKind.Image
                ? $"Loaded {newItems.Count} new {(newItems.Count == 1 ? "image" : "images")} into the image editor. Click an image to crop it."
                : importedKind == MediaKind.Video
                    ? $"Loaded {newItems.Count} new {(newItems.Count == 1 ? "video" : "videos")} into the video editor. Click a video to trim and crop it."
                    : importedKind == MediaKind.Pdf
                        ? $"Loaded {newItems.Count} new {(newItems.Count == 1 ? "PDF" : "PDFs")} into the PDF converter."
                    : $"Loaded {newItems.Count} new {(newItems.Count == 1 ? "file" : "files")} into the {_currentMediaKind.ToString().ToLowerInvariant()} editor.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Something went wrong while importing your files.";
        }
        finally
        {
            IsBusy = false;
            ResetBusyState();
        }
    }

    private async Task PrepareImportedItemAsync(MediaFileItem item)
    {
        switch (item.Kind)
        {
            case MediaKind.Image:
                LoadImageMetadata(item);
                item.ThumbnailPath = await EnsureImageThumbnailAsync(item);
                break;

            case MediaKind.Audio:
                item.Duration = await _ffmpegService.GetDurationAsync(item.SourcePath);
                break;

            case MediaKind.Video:
                item.Duration = await _ffmpegService.GetDurationAsync(item.SourcePath);
                var (videoWidth, videoHeight) = await _ffmpegService.GetVideoDimensionsAsync(item.SourcePath);
                item.PixelWidth = videoWidth;
                item.PixelHeight = videoHeight;
                item.PreviewPath = await EnsureVideoPreviewAsync(item);
                item.ThumbnailPath = await EnsureVideoThumbnailAsync(item);
                break;

            case MediaKind.Pdf:
                item.PageCount = await _pdfService.GetPageCountAsync(item.SourcePath);
                var (pdfWidth, pdfHeight) = await _pdfService.GetFirstPageDimensionsAsync(item.SourcePath);
                item.PixelWidth = pdfWidth;
                item.PixelHeight = pdfHeight;
                item.ThumbnailPath = await EnsurePdfThumbnailAsync(item);
                break;
        }
    }

    private async Task<string> EnsureImageThumbnailAsync(MediaFileItem item)
    {
        var outputPath = BuildCachePath(item.SourcePath, $"image-thumb-{BuildCropCacheToken(item)}", "jpg");

        if (!File.Exists(outputPath))
        {
            await _ffmpegService.CreateImageThumbnailAsync(item, outputPath);
        }

        return outputPath;
    }

    private async Task RefreshImageThumbnailAsync(MediaFileItem item)
    {
        var thumbnailPath = await EnsureImageThumbnailAsync(item);
        item.ThumbnailPath = null;
        item.ThumbnailPath = thumbnailPath;
    }

    private async Task<string> EnsurePdfThumbnailAsync(MediaFileItem item)
    {
        var outputPath = BuildCachePath(item.SourcePath, "pdf-thumb", "png");

        if (!File.Exists(outputPath))
        {
            await _pdfService.CreateThumbnailAsync(item.SourcePath, outputPath);
        }

        return outputPath;
    }

    private async Task<string> EnsureAudioWaveformAsync(MediaFileItem item)
    {
        var waveformColorToken = (_settings.ThemeTextColorHex ?? AppInfo.DefaultThemeTextColorHex)
            .Trim()
            .TrimStart('#')
            .ToUpperInvariant();
        var outputPath = BuildCachePath(item.SourcePath, $"waveform-{waveformColorToken}", "png");

        if (!File.Exists(outputPath))
        {
            await _ffmpegService.CreateAudioWaveformAsync(item.SourcePath, outputPath, _settings.ThemeTextColorHex);
        }

        return outputPath;
    }

    private async Task<string> EnsureVideoPreviewAsync(MediaFileItem item)
    {
        var outputPath = BuildCachePath(item.SourcePath, "video-preview-v2", "png");

        if (!File.Exists(outputPath))
        {
            var seekTime = TimeSpan.Zero;
            await _ffmpegService.CreateVideoPreviewAsync(item, outputPath, seekTime, includeCrop: false);
        }

        return outputPath;
    }

    private async Task<string> EnsureVideoThumbnailAsync(MediaFileItem item)
    {
        var outputPath = BuildCachePath(item.SourcePath, $"video-thumb-{BuildCropCacheToken(item)}", "png");

        if (!File.Exists(outputPath))
        {
            var seekTime = item.Duration <= TimeSpan.Zero
                ? TimeSpan.Zero
                : TimeSpan.FromMilliseconds(item.Duration.TotalMilliseconds * 0.15d);
            await _ffmpegService.CreateVideoPreviewAsync(item, outputPath, seekTime, includeCrop: true);
        }

        return outputPath;
    }

    private async Task RefreshVideoPreviewAssetsAsync(MediaFileItem item)
    {
        var previewPath = await EnsureVideoPreviewAsync(item);
        var thumbnailPath = await EnsureVideoThumbnailAsync(item);
        item.PreviewPath = null;
        item.PreviewPath = previewPath;
        item.ThumbnailPath = null;
        item.ThumbnailPath = thumbnailPath;
        await EnsureVideoTimelineAsync(item);
    }

    private async Task EnsureVideoTimelineAsync(MediaFileItem item)
    {
        var timelineFolder = BuildCacheFolder(item.SourcePath, "timeline");
        if (item.TimelineFrames.Count > 0 && item.TimelineFrames.All(File.Exists))
        {
            return;
        }

        var frames = await _ffmpegService.CreateVideoTimelineAsync(item, timelineFolder, 6);
        item.ReplaceTimelineFrames(frames);
    }

    private async Task OpenTrimEditorAsync(MediaFileItem item)
    {
        if (!EnsureSourceFileExists(item, "This file is no longer available on disk. Re-import it before editing."))
        {
            return;
        }

        RememberEditingBackup(item);
        StopAudioPreview();
        StopVideoPreview();

        IsBusy = true;
        SetBusyState(
            item.Kind == MediaKind.Audio ? "Preparing audio trim..." : "Preparing video editor...",
            true,
            detailMessage: item.DisplayName);

        try
        {
            if (item.Kind == MediaKind.Audio)
            {
                item.PreviewPath = await EnsureAudioWaveformAsync(item);
            }
            else if (item.Kind == MediaKind.Video)
            {
                item.PreviewPath = await EnsureVideoPreviewAsync(item);
                await EnsureVideoTimelineAsync(item);
            }

            SelectedMediaItem = item;

            if (item.Kind == MediaKind.Audio)
            {
                PrepareAudioPreview(item, autoPlay: false);
            }
            else if (item.Kind == MediaKind.Video)
            {
                PrepareVideoPreview(item, autoPlay: false);
            }
        }
        finally
        {
            IsBusy = false;
            ResetBusyState();
        }
    }

    private void ResetCurrentBatch()
    {
        StopAudioPreview();
        StopVideoPreview();
        AudioPreviewElement.Source = null;
        _loadedAudioSourcePath = null;
        PreviewMediaElement.Source = null;
        _loadedVideoSourcePath = null;
        SelectedMediaItem = null;
        MediaItems.Clear();
        StatusMessage = string.Empty;
        _currentMediaKind = MediaKind.None;
        NotifyEditorStateChanged();
        RefreshPreviewTexts();
    }

    private void SetCurrentMediaKind(MediaKind mediaKind)
    {
        var applySavedDefaults = _currentMediaKind != mediaKind || MediaItems.Count == 0;
        _currentMediaKind = mediaKind;
        ConfigureFormatOptions(mediaKind, applySavedDefaults);
        ApplyEditorDefaults(mediaKind, applySavedDefaults);
        NotifyEditorStateChanged();
        RefreshPreviewTexts();
    }

    private void ConfigureFormatOptions(MediaKind mediaKind, bool applySavedDefaults)
    {
        AvailableFormats.Clear();

        var formats = GetFormatsForMediaKind(mediaKind);
        var defaultSelection = GetDefaultOutputFormat(mediaKind);

        foreach (var format in formats)
        {
            AvailableFormats.Add(format);
        }

        if (!string.IsNullOrWhiteSpace(defaultSelection))
        {
            SelectedOutputFormat = !applySavedDefaults && formats.Contains(SelectedOutputFormat, StringComparer.OrdinalIgnoreCase)
                ? SelectedOutputFormat
                : defaultSelection;
        }
        else
        {
            SelectedOutputFormat = string.Empty;
        }
    }

    private void ApplyEditorDefaults(MediaKind mediaKind, bool applySavedDefaults)
    {
        if (!applySavedDefaults)
        {
            return;
        }

        if (mediaKind == MediaKind.Image)
        {
            ImageCompressionPercent = _settings.DefaultImageCompressionPercent;
        }
        else if (mediaKind == MediaKind.Video)
        {
            VideoResolutionDivisor = _settings.DefaultVideoResolutionDivisor;
            IsVideoPreviewLoopEnabled = _settings.DefaultVideoPreviewLoopEnabled;
        }
        else if (mediaKind == MediaKind.Pdf)
        {
            SelectedPdfAction = FindPdfActionOption(_settings.DefaultPdfAction)
                ?? PdfActionOptions.FirstOrDefault();
        }
    }

    private string[] GetFormatsForMediaKind(MediaKind mediaKind)
    {
        return mediaKind switch
        {
            MediaKind.Image => ImageOutputFormats,
            MediaKind.Audio => AudioOutputFormats,
            MediaKind.Video => VideoOutputFormats,
            MediaKind.Pdf => [],
            _ => [],
        };
    }

    private string GetDefaultOutputFormat(MediaKind mediaKind)
    {
        var configuredFormat = mediaKind switch
        {
            MediaKind.Image => _settings.DefaultImageFormat,
            MediaKind.Audio => _settings.DefaultAudioFormat,
            MediaKind.Video => _settings.DefaultVideoFormat,
            _ => string.Empty,
        };

        var formats = GetFormatsForMediaKind(mediaKind);
        if (formats.Length == 0)
        {
            return string.Empty;
        }

        return formats.Contains(configuredFormat, StringComparer.OrdinalIgnoreCase)
            ? configuredFormat
            : formats[0];
    }

    private void RefreshPdfActionOptions(bool useDraftOptions)
    {
        var optionCollection = useDraftOptions
            ? DefaultPdfActionOptions
            : PdfActionOptions;

        var currentKey = useDraftOptions
            ? DefaultPdfActionDraft?.Key
            : SelectedPdfAction?.Key;

        optionCollection.Clear();

        foreach (var actionOption in PdfActionOptionsSource)
        {
            optionCollection.Add(actionOption);
        }

        var nextOption = optionCollection.FirstOrDefault(option =>
                string.Equals(option.Key, currentKey, StringComparison.OrdinalIgnoreCase))
            ?? optionCollection.FirstOrDefault();

        if (useDraftOptions)
        {
            _defaultPdfActionDraft = nextOption;
            OnPropertyChanged(nameof(DefaultPdfActionDraft));
        }
        else
        {
            _selectedPdfAction = nextOption;
            OnPropertyChanged(nameof(SelectedPdfAction));
        }
    }

    private static PdfActionOption? FindPdfActionOption(string? actionKey)
    {
        if (string.IsNullOrWhiteSpace(actionKey))
        {
            return PdfActionOptionsSource.FirstOrDefault();
        }

        var normalizedActionKey = actionKey.Trim().ToLowerInvariant();

        return PdfActionOptionsSource.FirstOrDefault(option =>
                   string.Equals(option.Key, normalizedActionKey, StringComparison.OrdinalIgnoreCase))
               ?? PdfActionOptionsSource.FirstOrDefault();
    }

    private void NotifyEditorStateChanged()
    {
        OnPropertyChanged(nameof(IsLandingView));
        OnPropertyChanged(nameof(IsEditorView));
        OnPropertyChanged(nameof(IsStandardFormatEditor));
        OnPropertyChanged(nameof(IsImageEditor));
        OnPropertyChanged(nameof(IsImageOverviewVisible));
        OnPropertyChanged(nameof(IsImageCropVisible));
        OnPropertyChanged(nameof(IsAudioEditor));
        OnPropertyChanged(nameof(IsVideoEditor));
        OnPropertyChanged(nameof(IsPdfEditor));
        OnPropertyChanged(nameof(IsAudioOverviewVisible));
        OnPropertyChanged(nameof(IsAudioTrimVisible));
        OnPropertyChanged(nameof(IsVideoOverviewVisible));
        OnPropertyChanged(nameof(IsVideoTrimVisible));
        OnPropertyChanged(nameof(IsPdfOverviewVisible));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(LoadedCountLabel));
    }

    private void ImageTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MediaFileItem item })
        {
            return;
        }

        RememberEditingBackup(item);
        StopAudioPreview();
        StopVideoPreview();
        SelectedMediaItem = item;
    }

    private async void AudioTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MediaFileItem item })
        {
            await OpenTrimEditorAsync(item);
        }
    }

    private async void VideoTileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MediaFileItem item })
        {
            await OpenTrimEditorAsync(item);
        }
    }

    private void RememberEditingBackup(MediaFileItem item)
    {
        _editingTrimStartBackup = item.TrimStartRatio;
        _editingTrimEndBackup = item.TrimEndRatio;
        _editingPreviewStartBackup = item.PreviewStartRatio;
        _editingPlaybackSpeedBackup = item.PlaybackSpeedMultiplier;
        _editingCropLeftBackup = item.CropLeftRatio;
        _editingCropTopBackup = item.CropTopRatio;
        _editingCropRightBackup = item.CropRightRatio;
        _editingCropBottomBackup = item.CropBottomRatio;
    }

    private void RestoreEditingBackup(MediaFileItem item)
    {
        item.TrimStartRatio = _editingTrimStartBackup;
        item.TrimEndRatio = _editingTrimEndBackup;
        item.PreviewStartRatio = _editingPreviewStartBackup;
        item.PlaybackSpeedMultiplier = _editingPlaybackSpeedBackup;
        item.CropLeftRatio = _editingCropLeftBackup;
        item.CropTopRatio = _editingCropTopBackup;
        item.CropRightRatio = _editingCropRightBackup;
        item.CropBottomRatio = _editingCropBottomBackup;
    }

    private void VideoSpeedButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null || SelectedMediaItem.Kind != MediaKind.Video)
        {
            return;
        }

        var selectedSpeed = sender is FrameworkElement { Tag: var tagValue }
            ? tagValue?.ToString() switch
            {
                "0.5" => 0.5d,
                "2" => 2d,
                _ => 1d,
            }
            : 1d;

        SelectedMediaItem.PlaybackSpeedMultiplier = selectedSpeed;
        ApplyVideoPreviewSpeed(SelectedMediaItem);
    }

    private void VideoLoopButton_Click(object sender, RoutedEventArgs e)
    {
        IsVideoPreviewLoopEnabled = !IsVideoPreviewLoopEnabled;
    }

    private void CancelTrimButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        RestoreEditingBackup(SelectedMediaItem);
        StopAudioPreview();
        StopVideoPreview();

        if (MediaItems.Count > 1)
        {
            SelectedMediaItem = null;
        }

        StatusMessage = _currentMediaKind == MediaKind.Image
            ? "Crop changes were discarded."
            : _currentMediaKind == MediaKind.Video
                ? "Video edit changes were discarded."
                : "Trim changes were discarded.";
    }

    private async void DoneTrimButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        var editingItem = SelectedMediaItem;
        var savedDisplayName = editingItem.DisplayName;
        StopAudioPreview();
        StopVideoPreview();

        if (_currentMediaKind == MediaKind.Image)
        {
            await RefreshImageThumbnailAsync(editingItem);
        }
        else if (_currentMediaKind == MediaKind.Video)
        {
            await RefreshVideoPreviewAssetsAsync(editingItem);
        }

        if (MediaItems.Count > 1)
        {
            SelectedMediaItem = null;
        }
        else if (_currentMediaKind == MediaKind.Image)
        {
            // Keep the crop editor open for single-image batches.
        }
        else if (_currentMediaKind == MediaKind.Audio)
        {
            PrepareAudioPreview(SelectedMediaItem, autoPlay: false);
        }
        else if (_currentMediaKind == MediaKind.Video)
        {
            PrepareVideoPreview(SelectedMediaItem, autoPlay: false);
        }

        StatusMessage = _currentMediaKind == MediaKind.Image
            ? $"Saved crop for {savedDisplayName}."
            : _currentMediaKind == MediaKind.Video
                ? $"Saved trim and crop for {savedDisplayName}."
                : $"Saved trim for {savedDisplayName}.";
    }

    private async void LetsGoButton_Click(object sender, RoutedEventArgs e)
    {
        if (MediaItems.Count == 0)
        {
            return;
        }

        var requiresFfmpeg = _currentMediaKind != MediaKind.Pdf ||
            string.Equals(SelectedPdfAction?.OutputExtension, "webp", StringComparison.OrdinalIgnoreCase);

        if (requiresFfmpeg && !_ffmpegService.IsAvailable)
        {
            MessageBox.Show(this, "Bundled FFmpeg tools are missing, so export cannot run yet.", "FFmpeg Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string? completionToastText = null;
        string? exportedFolderToOpen = null;
        var shouldOpenExportFolder = _settings.OpenExportFolderAfterExport;
        var shouldReturnHomeAfterExport = _settings.ReturnHomeAfterExport;
        var removeMetadata = _settings.RemoveMetadata;

        try
        {
            var itemsToExport = MediaItems.ToList();
            if (!EnsureSourceFilesExist(itemsToExport, "One or more imported files are no longer available on disk. Re-import them before exporting."))
            {
                return;
            }

            var exportCount = itemsToExport.Count;
            var exportFolder = _exportPathService.ResolveSessionFolder(_settings, _currentMediaKind, _customOutputFolder);

            IsBusy = true;
            if (_currentMediaKind == MediaKind.Pdf)
            {
                var pdfAction = SelectedPdfAction ?? throw new InvalidOperationException("Choose a PDF action first.");
                var exportedFiles = await ExportPdfBatchAsync(itemsToExport, exportFolder, pdfAction, removeMetadata);
                completionToastText = $"Finished exporting {exportedFiles} {(exportedFiles == 1 ? "file" : "files")} to {ShortenPath(exportFolder)}.";
            }
            else
            {
                SetBusyState("Converting files...", false, 0, exportCount, $"0 / {exportCount}");

                for (var index = 0; index < itemsToExport.Count; index++)
                {
                    var item = itemsToExport[index];
                    BusyMessage = $"Exporting {index + 1} of {exportCount}";
                    BusyDetailMessage = $"{index + 1} / {exportCount}: {item.DisplayName}";

                    var targetPath = _exportPathService.GetUniqueOutputPath(
                        exportFolder,
                        GetExportBaseName(item),
                        SelectedOutputFormat);

                    switch (_currentMediaKind)
                    {
                        case MediaKind.Image:
                            await _ffmpegService.ConvertImageAsync(item, targetPath, SelectedOutputFormat, ImageCompressionPercent, removeMetadata);
                            break;

                        case MediaKind.Audio:
                            await _ffmpegService.ConvertAudioAsync(item, targetPath, SelectedOutputFormat, removeMetadata);
                            break;

                        case MediaKind.Video:
                            await _ffmpegService.ConvertVideoAsync(item, targetPath, SelectedOutputFormat, VideoResolutionDivisor, item.PlaybackSpeedMultiplier, removeMetadata);
                            break;
                    }

                    BusyProgressValue = index + 1;
                }

                completionToastText = $"Finished exporting {exportCount} {(exportCount == 1 ? "file" : "files")} to {ShortenPath(exportFolder)}.";
            }

            exportedFolderToOpen = exportFolder;

            if (shouldReturnHomeAfterExport)
            {
                ResetCurrentBatch();
            }
            else if (SelectedMediaItem is not null)
            {
                if (_currentMediaKind == MediaKind.Audio)
                {
                    PrepareAudioPreview(SelectedMediaItem, autoPlay: false);
                }
                else if (_currentMediaKind == MediaKind.Video)
                {
                    PrepareVideoPreview(SelectedMediaItem, autoPlay: false);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Export failed before all files could be written.";
        }
        finally
        {
            IsBusy = false;
            ResetBusyState();
        }

        if (!string.IsNullOrWhiteSpace(completionToastText))
        {
            var openedFolder = false;
            if (shouldOpenExportFolder && !string.IsNullOrWhiteSpace(exportedFolderToOpen))
            {
                openedFolder = OpenFolderInExplorer(exportedFolderToOpen);
            }

            ShowToast(openedFolder || !shouldOpenExportFolder
                ? completionToastText
                : $"{completionToastText} The output folder could not be opened automatically.");
        }
    }

    private async Task<int> ExportPdfBatchAsync(IReadOnlyList<MediaFileItem> itemsToExport, string exportFolder, PdfActionOption pdfAction, bool removeMetadata)
    {
        return pdfAction.Key switch
        {
            "merge" => await ExportMergedPdfAsync(itemsToExport, exportFolder, removeMetadata),
            "invert-order" => await ExportReversedPdfAsync(itemsToExport, exportFolder, removeMetadata),
            "split-pages" => await ExportSeparatedPdfPagesAsync(itemsToExport, exportFolder, removeMetadata),
            "pages-jpg" or "pages-png" or "pages-webp" => await ExportPdfPagesAsImagesAsync(itemsToExport, exportFolder, pdfAction.OutputExtension, removeMetadata),
            _ => throw new InvalidOperationException($"Unsupported PDF action '{pdfAction.Label}'."),
        };
    }

    private async Task<int> ExportMergedPdfAsync(IReadOnlyList<MediaFileItem> itemsToExport, string exportFolder, bool removeMetadata)
    {
        SetBusyState("Preparing merged PDF...", false, 0, 1, "0 / 1");
        BusyMessage = itemsToExport.Count > 1 ? "Merging PDFs..." : "Writing PDF...";
        BusyDetailMessage = itemsToExport.Count > 1
            ? $"{itemsToExport.Count} PDFs in batch"
            : itemsToExport[0].DisplayName;

        var mergedPdfBytes = await _pdfService.MergeDocumentsAsync(
            itemsToExport.Select(item => item.SourcePath).ToArray(),
            0,
            removeMetadata);

        var mergedBaseName = ApplyCustomSuffix(itemsToExport.Count == 1
            ? itemsToExport[0].BaseName
            : "merged-pdfs");
        var mergedOutputPath = _exportPathService.GetUniqueOutputPath(exportFolder, mergedBaseName, "pdf");
        await File.WriteAllBytesAsync(mergedOutputPath, mergedPdfBytes);
        BusyProgressValue = 1;
        return 1;
    }

    private async Task<int> ExportReversedPdfAsync(IReadOnlyList<MediaFileItem> itemsToExport, string exportFolder, bool removeMetadata)
    {
        SetBusyState("Reordering PDF pages...", false, 0, itemsToExport.Count, $"0 / {itemsToExport.Count}");

        for (var index = 0; index < itemsToExport.Count; index++)
        {
            var item = itemsToExport[index];
            BusyMessage = $"Reversing PDF {index + 1} of {itemsToExport.Count}";
            BusyDetailMessage = item.DisplayName;

            var reversedPdfBytes = await _pdfService.ReverseDocumentAsync(item.SourcePath, 0, removeMetadata);
            var outputBaseName = ApplyCustomSuffix($"{item.BaseName}-reversed");
            var outputPath = _exportPathService.GetUniqueOutputPath(exportFolder, outputBaseName, "pdf");
            await File.WriteAllBytesAsync(outputPath, reversedPdfBytes);

            BusyProgressValue = index + 1;
        }

        return itemsToExport.Count;
    }

    private async Task<int> ExportSeparatedPdfPagesAsync(IReadOnlyList<MediaFileItem> itemsToExport, string exportFolder, bool removeMetadata)
    {
        var totalPages = itemsToExport.Sum(item => item.PageCount);
        var writtenFiles = 0;

        SetBusyState("Exporting PDF pages...", false, 0, totalPages, $"0 / {totalPages}");

        foreach (var item in itemsToExport)
        {
            for (var pageIndex = 0; pageIndex < item.PageCount; pageIndex++)
            {
                BusyMessage = $"Exporting page {writtenFiles + 1} of {totalPages}";
                BusyDetailMessage = $"{item.DisplayName} - page {pageIndex + 1}";

                var pagePdfBytes = await _pdfService.ExportSinglePagePdfAsync(item.SourcePath, pageIndex, 0, removeMetadata);
                var baseName = BuildPdfPageBaseName(item, pageIndex + 1);
                var outputPath = _exportPathService.GetUniqueOutputPath(exportFolder, baseName, "pdf");
                await File.WriteAllBytesAsync(outputPath, pagePdfBytes);

                writtenFiles++;
                BusyProgressValue = writtenFiles;
            }
        }

        return writtenFiles;
    }

    private async Task<int> ExportPdfPagesAsImagesAsync(IReadOnlyList<MediaFileItem> itemsToExport, string exportFolder, string outputExtension, bool removeMetadata)
    {
        var totalPages = itemsToExport.Sum(item => item.PageCount);
        var writtenFiles = 0;

        SetBusyState("Rendering PDF pages...", false, 0, totalPages, $"0 / {totalPages}");

        foreach (var item in itemsToExport)
        {
            for (var pageIndex = 0; pageIndex < item.PageCount; pageIndex++)
            {
                BusyMessage = $"Exporting page {writtenFiles + 1} of {totalPages}";
                BusyDetailMessage = $"{item.DisplayName} - page {pageIndex + 1}";

                var baseName = BuildPdfPageBaseName(item, pageIndex + 1);
                var outputPath = _exportPathService.GetUniqueOutputPath(exportFolder, baseName, outputExtension);

                if (string.Equals(outputExtension, "webp", StringComparison.OrdinalIgnoreCase))
                {
                    var tempPngPath = BuildCachePath(item.SourcePath, $"pdf-page-{pageIndex:D4}", "png");

                    await _pdfService.RenderPageAsImageAsync(item.SourcePath, pageIndex, tempPngPath, "png", 0);
                    await _ffmpegService.ConvertStillImageFileAsync(
                        tempPngPath,
                        outputPath,
                        "webp",
                        0,
                        removeMetadata);
                }
                else
                {
                    await _pdfService.RenderPageAsImageAsync(item.SourcePath, pageIndex, outputPath, outputExtension, 0);
                }

                writtenFiles++;
                BusyProgressValue = writtenFiles;
            }
        }

        return writtenFiles;
    }

    private void SetBusyState(string message, bool isIndeterminate, double value = 0d, double maximum = 1d, string detailMessage = "")
    {
        BusyMessage = message;
        BusyDetailMessage = detailMessage;
        IsBusyProgressIndeterminate = isIndeterminate;
        BusyProgressMaximum = Math.Max(1d, maximum);
        BusyProgressValue = Math.Clamp(value, 0d, BusyProgressMaximum);
    }

    private void ResetBusyState()
    {
        BusyMessage = "Preparing files...";
        BusyDetailMessage = string.Empty;
        BusyProgressMaximum = 1d;
        BusyProgressValue = 0d;
        IsBusyProgressIndeterminate = true;
    }

    private void OutputTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressOutputSelectionChanged || SelectedOutputTarget is null)
        {
            return;
        }

        switch (SelectedOutputTarget.Key)
        {
            case "picker":
                var chosenFolder = PickFolder(_customOutputFolder ?? _settings.DefaultExportPath);
                if (!string.IsNullOrWhiteSpace(chosenFolder))
                {
                    _customOutputFolder = chosenFolder;
                    RebuildOutputTargetOptions(selectCustom: true);
                    StatusMessage = $"Using custom export folder: {_customOutputFolder}";
                }
                else
                {
                    RebuildOutputTargetOptions(selectCustom: !string.IsNullOrWhiteSpace(_customOutputFolder));
                }

                break;

            case "default":
                _customOutputFolder = null;
                RebuildOutputTargetOptions(selectCustom: false);
                StatusMessage = "Using the default export folder from Options.";
                break;

            case "custom":
                _customOutputFolder = SelectedOutputTarget.FolderPath;
                StatusMessage = $"Using custom export folder: {_customOutputFolder}";
                break;
        }
    }

    private void RebuildOutputTargetOptions(bool selectCustom)
    {
        _suppressOutputSelectionChanged = true;

        OutputTargetOptions.Clear();

        var defaultOption = new OutputTargetOption("default", "DEFAULT", null, true);
        OutputTargetOptions.Add(defaultOption);

        OutputTargetOption? customOption = null;
        if (!string.IsNullOrWhiteSpace(_customOutputFolder))
        {
            customOption = new OutputTargetOption("custom", $"CUSTOM: {ShortenPath(_customOutputFolder)}", _customOutputFolder, false);
            OutputTargetOptions.Add(customOption);
        }

        var pickerOption = new OutputTargetOption("picker", "CHOOSE FOLDER...", null, false);
        OutputTargetOptions.Add(pickerOption);

        SelectedOutputTarget = selectCustom && customOption is not null ? customOption : defaultOption;

        _suppressOutputSelectionChanged = false;
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        LoadDraftsFromSettings();
        IsOptionsOpen = true;
    }

    private void BackToLandingButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy || _currentMediaKind == MediaKind.None)
        {
            return;
        }

        IsOptionsOpen = false;
        ResetCurrentBatch();
    }

    private void CancelOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        IsOptionsOpen = false;
        LoadDraftsFromSettings();
    }

    private async void SaveOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DefaultExportPathDraft))
        {
            MessageBox.Show(this, "Choose a default export path first.", "Missing Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (AddCustomSuffixDraft)
        {
            if (string.IsNullOrWhiteSpace(CustomSuffixDraft))
            {
                MessageBox.Show(this, "Enter a custom suffix or turn the suffix option off.", "Missing Suffix", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CustomSuffixDraft.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "The custom suffix contains characters that cannot be used in file names.", "Invalid Suffix", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!TryNormalizeThemeColorHex(ThemeTextColorHexDraft, out var normalizedThemeColorHex))
        {
            MessageBox.Show(this, "Enter a valid hex color for the text theme, for example #EC5E27.", "Invalid Theme Color", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryNormalizeThemeColorHex(ThemeBackgroundColorHexDraft, out var normalizedThemeBackgroundColorHex))
        {
            MessageBox.Show(this, "Enter a valid hex color for the background theme, for example #202020.", "Invalid Background Color", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings = new AppSettings
        {
            DefaultExportPath = DefaultExportPathDraft,
            CreateDateFolders = CreateDateFoldersDraft,
            DefaultImageCompressionPercent = DefaultImageCompressionDraft,
            DefaultVideoResolutionDivisor = DefaultVideoResolutionDraft,
            DefaultVideoPreviewLoopEnabled = DefaultVideoPreviewLoopDraft,
            DefaultPdfCompressionLevel = _settings.DefaultPdfCompressionLevel,
            DefaultWindowScalePercent = DefaultWindowScaleDraft,
            DefaultImageFormat = DefaultImageFormatDraft,
            DefaultAudioFormat = DefaultAudioFormatDraft,
            DefaultVideoFormat = DefaultVideoFormatDraft,
            DefaultPdfMode = DefaultPdfActionDraft?.ModeKey ?? "convert",
            DefaultPdfAction = DefaultPdfActionDraft?.Key ?? "pages-jpg",
            ThemeTextColorHex = normalizedThemeColorHex,
            ThemeBackgroundColorHex = normalizedThemeBackgroundColorHex,
            AddCustomSuffixToFiles = AddCustomSuffixDraft,
            CustomSuffix = CustomSuffixDraft,
            OpenExportFolderAfterExport = OpenExportFolderAfterExportDraft,
            ReturnHomeAfterExport = ReturnHomeAfterExportDraft,
            RemoveMetadata = RemoveMetadataDraft,
        };

        await _settingsService.SaveAsync(_settings);
        LoadDraftsFromSettings();
        ThemeTextColorHexDraft = _settings.ThemeTextColorHex;
        ThemeBackgroundColorHexDraft = _settings.ThemeBackgroundColorHex;
        ApplyTheme(_settings.ThemeTextColorHex, _settings.ThemeBackgroundColorHex);
        ApplyDefaultWindowSize(_settings.DefaultWindowScalePercent, centerOnWorkArea: false);
        IsOptionsOpen = false;
        RefreshPreviewTexts();
        ShowToast("Options saved.");
    }

    private void BrowseDefaultExportPathButton_Click(object sender, RoutedEventArgs e)
    {
        var chosenFolder = PickFolder(DefaultExportPathDraft);
        if (!string.IsNullOrWhiteSpace(chosenFolder))
        {
            DefaultExportPathDraft = chosenFolder;
        }
    }

    private void ResetThemeColorButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeTextColorHexDraft = AppInfo.DefaultThemeTextColorHex;
    }

    private void ResetThemeBackgroundColorButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeBackgroundColorHexDraft = AppInfo.DefaultThemeBackgroundColorHex;
    }

    private void RefreshPreviewTexts()
    {
        var baseSettings = new AppSettings
        {
            DefaultExportPath = string.IsNullOrWhiteSpace(_settings.DefaultExportPath)
                ? _settingsService.CreateDefaultSettings().DefaultExportPath
                : _settings.DefaultExportPath,
            CreateDateFolders = _settings.CreateDateFolders,
            DefaultImageCompressionPercent = _settings.DefaultImageCompressionPercent,
            DefaultVideoResolutionDivisor = _settings.DefaultVideoResolutionDivisor,
            DefaultVideoPreviewLoopEnabled = _settings.DefaultVideoPreviewLoopEnabled,
            DefaultPdfCompressionLevel = _settings.DefaultPdfCompressionLevel,
            DefaultWindowScalePercent = _settings.DefaultWindowScalePercent,
            DefaultImageFormat = _settings.DefaultImageFormat,
            DefaultAudioFormat = _settings.DefaultAudioFormat,
            DefaultVideoFormat = _settings.DefaultVideoFormat,
            DefaultPdfMode = FindPdfActionOption(_settings.DefaultPdfAction)?.ModeKey ?? _settings.DefaultPdfMode,
            DefaultPdfAction = _settings.DefaultPdfAction,
            ThemeTextColorHex = _settings.ThemeTextColorHex,
            ThemeBackgroundColorHex = _settings.ThemeBackgroundColorHex,
            AddCustomSuffixToFiles = _settings.AddCustomSuffixToFiles,
            CustomSuffix = _settings.CustomSuffix,
            OpenExportFolderAfterExport = _settings.OpenExportFolderAfterExport,
            ReturnHomeAfterExport = _settings.ReturnHomeAfterExport,
            RemoveMetadata = _settings.RemoveMetadata,
        };

        LandingExportSummary = $"DEFAULT EXPORT FOLDER SET TO: {_exportPathService.GetDefaultPreview(baseSettings, _currentMediaKind)}".ToUpperInvariant();

        var draftSettings = new AppSettings
        {
            DefaultExportPath = string.IsNullOrWhiteSpace(DefaultExportPathDraft)
                ? _settingsService.CreateDefaultSettings().DefaultExportPath
                : DefaultExportPathDraft,
            CreateDateFolders = CreateDateFoldersDraft,
            DefaultImageCompressionPercent = DefaultImageCompressionDraft,
            DefaultVideoResolutionDivisor = DefaultVideoResolutionDraft,
            DefaultVideoPreviewLoopEnabled = DefaultVideoPreviewLoopDraft,
            DefaultPdfCompressionLevel = _settings.DefaultPdfCompressionLevel,
            DefaultWindowScalePercent = DefaultWindowScaleDraft,
            DefaultImageFormat = DefaultImageFormatDraft,
            DefaultAudioFormat = DefaultAudioFormatDraft,
            DefaultVideoFormat = DefaultVideoFormatDraft,
            DefaultPdfMode = DefaultPdfActionDraft?.ModeKey ?? "convert",
            DefaultPdfAction = DefaultPdfActionDraft?.Key ?? "pages-jpg",
            ThemeTextColorHex = ThemeTextColorHexDraft,
            ThemeBackgroundColorHex = ThemeBackgroundColorHexDraft,
            AddCustomSuffixToFiles = AddCustomSuffixDraft,
            CustomSuffix = CustomSuffixDraft,
            OpenExportFolderAfterExport = OpenExportFolderAfterExportDraft,
            ReturnHomeAfterExport = ReturnHomeAfterExportDraft,
            RemoveMetadata = RemoveMetadataDraft,
        };

        OptionsPreviewPath = _exportPathService.GetDefaultPreview(draftSettings, _currentMediaKind);
    }

    private static MediaKind DetectMediaKind(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (ImageExtensions.Contains(extension))
        {
            return MediaKind.Image;
        }

        if (AudioExtensions.Contains(extension))
        {
            return MediaKind.Audio;
        }

        if (VideoExtensions.Contains(extension))
        {
            return MediaKind.Video;
        }

        return PdfExtensions.Contains(extension) ? MediaKind.Pdf : MediaKind.None;
    }

    private string BuildCachePath(string sourcePath, string suffix, string extension)
    {
        var token = CreateCacheToken(sourcePath);
        return Path.Combine(_cacheRoot, $"{token}-{suffix}.{extension}");
    }

    private static string BuildCropCacheToken(MediaFileItem item)
    {
        return FormattableString.Invariant(
            $"{(int)Math.Round(item.CropLeftRatio * 1000d):D4}-{(int)Math.Round(item.CropTopRatio * 1000d):D4}-{(int)Math.Round(item.CropRightRatio * 1000d):D4}-{(int)Math.Round(item.CropBottomRatio * 1000d):D4}");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private string BuildCacheFolder(string sourcePath, string suffix)
    {
        var token = CreateCacheToken(sourcePath);
        var folder = Path.Combine(_cacheRoot, $"{token}-{suffix}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string CreateCacheToken(string sourcePath)
    {
        var fileInfo = new FileInfo(sourcePath);
        var seed = $"{sourcePath}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes[..8]).ToLowerInvariant();
    }

    private static void LoadImageMetadata(MediaFileItem item)
    {
        try
        {
            using var stream = File.OpenRead(item.SourcePath);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();

            if (frame is not null)
            {
                item.PixelWidth = frame.PixelWidth;
                item.PixelHeight = frame.PixelHeight;
            }
        }
        catch
        {
            item.PixelWidth = 1920;
            item.PixelHeight = 1080;
        }
    }

    private void ApplyTheme(string? themeColorHex, string? themeBackgroundColorHex)
    {
        if (!TryNormalizeThemeColorHex(themeColorHex, out var normalizedThemeColorHex))
        {
            normalizedThemeColorHex = AppInfo.DefaultThemeTextColorHex;
        }

        if (!TryNormalizeThemeColorHex(themeBackgroundColorHex, out var normalizedThemeBackgroundColorHex))
        {
            normalizedThemeBackgroundColorHex = AppInfo.DefaultThemeBackgroundColorHex;
        }

        var textColor = (Color)ColorConverter.ConvertFromString(normalizedThemeColorHex)!;
        var backgroundColor = (Color)ColorConverter.ConvertFromString(normalizedThemeBackgroundColorHex)!;
        var panelColor = AdjustColor(backgroundColor, 0.02);
        var surfaceColor = AdjustColor(backgroundColor, -0.09);
        var previewColor = AdjustColor(backgroundColor, -0.14);
        var controlColor = AdjustColor(backgroundColor, -0.47);
        var controlBorderColor = AdjustColor(controlColor, 0.23);
        var panelBorderColor = AdjustColor(panelColor, 0.12);

        UpdateThemeResource("PrimaryTextBrush", textColor);
        UpdateThemeResource("MutedBrush", textColor);
        UpdateThemeResource("WindowBackgroundBrush", backgroundColor);
        UpdateThemeResource("PanelBackgroundBrush", panelColor);
        UpdateThemeResource("SurfaceBackgroundBrush", surfaceColor);
        UpdateThemeResource("PreviewBackgroundBrush", previewColor);
        UpdateThemeResource("ControlBackgroundBrush", controlColor);
        UpdateThemeResource("ControlBorderBrush", controlBorderColor);
        UpdateThemeResource("PanelBorderBrush", panelBorderColor);
    }

    private void UpdateThemeResource(string resourceKey, Color color)
    {
        Resources[resourceKey] = new SolidColorBrush(color);
    }

    private static bool TryNormalizeThemeColorHex(string? value, out string normalizedThemeColorHex)
    {
        normalizedThemeColorHex = AppInfo.DefaultThemeTextColorHex;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value.Trim())!;
            normalizedThemeColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Brush CreateThemeBrush(string? value, string fallbackHex = AppInfo.DefaultThemeTextColorHex)
    {
        if (!TryNormalizeThemeColorHex(value, out var normalizedThemeColorHex))
        {
            normalizedThemeColorHex = fallbackHex;
        }

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(normalizedThemeColorHex)!);
    }

    private static Color AdjustColor(Color color, double amount)
    {
        amount = Math.Clamp(amount, -1d, 1d);

        if (amount >= 0)
        {
            return Color.FromRgb(
                MixTowards(color.R, byte.MaxValue, amount),
                MixTowards(color.G, byte.MaxValue, amount),
                MixTowards(color.B, byte.MaxValue, amount));
        }

        var factor = 1d + amount;
        return Color.FromRgb(
            (byte)Math.Round(color.R * factor),
            (byte)Math.Round(color.G * factor),
            (byte)Math.Round(color.B * factor));
    }

    private static byte MixTowards(byte start, byte target, double amount)
    {
        return (byte)Math.Round(start + ((target - start) * amount));
    }

    private static string? PickFolder(string? startingFolder)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = string.IsNullOrWhiteSpace(startingFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : startingFolder,
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    private static string ShortenPath(string path)
    {
        if (path.Length <= 36)
        {
            return path;
        }

        return $"{path[..14]}...{path[^18..]}";
    }

    private string? FindBrandingAsset(string assetName, params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(_brandingRoot))
        {
            return null;
        }

        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(_brandingRoot, $"{assetName}{extension}");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static ImageSource? LoadBrandingImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private string ApplyCustomSuffix(string baseName)
    {
        if (!_settings.AddCustomSuffixToFiles || string.IsNullOrWhiteSpace(_settings.CustomSuffix))
        {
            return baseName;
        }

        return $"{baseName}{_settings.CustomSuffix}";
    }

    private string GetExportBaseName(MediaFileItem item)
    {
        return ApplyCustomSuffix(item.BaseName);
    }

    private bool EnsureSourceFilesExist(IReadOnlyList<MediaFileItem> items, string message)
    {
        var missingItems = items
            .Where(item => !File.Exists(item.SourcePath))
            .ToList();

        if (missingItems.Count == 0)
        {
            return true;
        }

        var previewLines = missingItems
            .Take(3)
            .Select(item => item.SourcePath);

        var details = string.Join(Environment.NewLine, previewLines);
        if (missingItems.Count > 3)
        {
            details += $"{Environment.NewLine}...and {missingItems.Count - 3} more.";
        }

        MessageBox.Show(
            this,
            $"{message}{Environment.NewLine}{Environment.NewLine}Missing:{Environment.NewLine}{details}",
            "Missing Source File",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        StatusMessage = "One or more imported files went missing before export could start.";
        return false;
    }

    private bool EnsureSourceFileExists(MediaFileItem item, string message)
    {
        return EnsureSourceFilesExist([item], message);
    }

    private string BuildPdfPageBaseName(MediaFileItem item, int pageNumber)
    {
        return $"{ApplyCustomSuffix(item.BaseName)}_page-{pageNumber:D3}";
    }

    private bool OpenFolderInExplorer(string folderPath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _toastDismissTimer.Stop();
        ToastNotificationBorder?.BeginAnimation(UIElement.OpacityProperty, null);

        if (ToastNotificationBorder is not null)
        {
            ToastNotificationBorder.Opacity = 1d;
        }

        ToastMessage = message;
        _toastDismissTimer.Start();
    }

    private void ToastDismissTimer_Tick(object? sender, EventArgs e)
    {
        _toastDismissTimer.Stop();

        if (string.IsNullOrWhiteSpace(ToastMessage))
        {
            return;
        }

        if (ToastNotificationBorder is null)
        {
            ToastMessage = string.Empty;
            return;
        }

        var animation = new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(350))
        {
            FillBehavior = FillBehavior.Stop,
        };

        animation.Completed += (_, _) =>
        {
            ToastNotificationBorder.Opacity = 1d;
            ToastMessage = string.Empty;
        };

        ToastNotificationBorder.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeRestoreButton_Click(sender, new RoutedEventArgs());
            return;
        }

        DragMove();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmSizing && WindowState == WindowState.Normal)
        {
            var rect = Marshal.PtrToStructure<RectInt>(lParam);
            ResizeRectToAspectRatio(ref rect, wParam.ToInt32());
            Marshal.StructureToPtr(rect, lParam, true);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ResizeRectToAspectRatio(ref RectInt rect, int edge)
    {
        var currentWidth = Math.Max(1, rect.Right - rect.Left);
        var currentHeight = Math.Max(1, rect.Bottom - rect.Top);
        var adjustedWidth = currentWidth;
        var adjustedHeight = currentHeight;

        var draggingHorizontal = edge is WmszLeft or WmszRight or WmszTopLeft or WmszTopRight or WmszBottomLeft or WmszBottomRight;
        var draggingVertical = edge is WmszTop or WmszBottom or WmszTopLeft or WmszTopRight or WmszBottomLeft or WmszBottomRight;

        if (draggingHorizontal && !draggingVertical)
        {
            adjustedHeight = (int)Math.Round(adjustedWidth / WindowAspectRatio);
        }
        else if (!draggingHorizontal && draggingVertical)
        {
            adjustedWidth = (int)Math.Round(adjustedHeight * WindowAspectRatio);
        }
        else
        {
            var widthFromHeight = adjustedHeight * WindowAspectRatio;
            var heightFromWidth = adjustedWidth / WindowAspectRatio;

            if (Math.Abs(currentWidth - widthFromHeight) <= Math.Abs(currentHeight - heightFromWidth))
            {
                adjustedWidth = (int)Math.Round(widthFromHeight);
            }
            else
            {
                adjustedHeight = (int)Math.Round(heightFromWidth);
            }
        }

        adjustedWidth = Math.Max((int)Math.Round(MinWidth), adjustedWidth);
        adjustedHeight = Math.Max((int)Math.Round(MinHeight), adjustedHeight);

        if (adjustedWidth / WindowAspectRatio < adjustedHeight)
        {
            adjustedWidth = (int)Math.Round(adjustedHeight * WindowAspectRatio);
        }
        else
        {
            adjustedHeight = (int)Math.Round(adjustedWidth / WindowAspectRatio);
        }

        switch (edge)
        {
            case WmszLeft:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: true);
                ApplyVerticalResizeCentered(ref rect, adjustedHeight);
                break;
            case WmszRight:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: false);
                ApplyVerticalResizeCentered(ref rect, adjustedHeight);
                break;
            case WmszTop:
                ApplyHorizontalResizeCentered(ref rect, adjustedWidth);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: true);
                break;
            case WmszBottom:
                ApplyHorizontalResizeCentered(ref rect, adjustedWidth);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: false);
                break;
            case WmszTopLeft:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: true);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: true);
                break;
            case WmszTopRight:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: false);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: true);
                break;
            case WmszBottomLeft:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: true);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: false);
                break;
            case WmszBottomRight:
                ApplyHorizontalResize(ref rect, adjustedWidth, stickToLeft: false);
                ApplyVerticalResize(ref rect, adjustedHeight, stickToTop: false);
                break;
        }
    }

    private static void ApplyHorizontalResize(ref RectInt rect, int width, bool stickToLeft)
    {
        if (stickToLeft)
        {
            rect.Left = rect.Right - width;
        }
        else
        {
            rect.Right = rect.Left + width;
        }
    }

    private static void ApplyVerticalResize(ref RectInt rect, int height, bool stickToTop)
    {
        if (stickToTop)
        {
            rect.Top = rect.Bottom - height;
        }
        else
        {
            rect.Bottom = rect.Top + height;
        }
    }

    private static void ApplyHorizontalResizeCentered(ref RectInt rect, int width)
    {
        var center = (rect.Left + rect.Right) / 2;
        rect.Left = center - (width / 2);
        rect.Right = rect.Left + width;
    }

    private static void ApplyVerticalResizeCentered(ref RectInt rect, int height)
    {
        var center = (rect.Top + rect.Bottom) / 2;
        rect.Top = center - (height / 2);
        rect.Bottom = rect.Top + height;
    }

    private void PrepareVideoPreview(MediaFileItem item, bool autoPlay)
    {
        StopVideoPreview();

        _pendingPreviewSeek = item.PreviewStart;
        _playPreviewWhenReady = autoPlay;
        ApplyVideoPreviewSpeed(item);

        if (_loadedVideoSourcePath == item.SourcePath &&
            PreviewMediaElement.Source is not null)
        {
            SeekVideoPreview(item.PreviewStart, autoPlay);
            return;
        }

        _loadedVideoSourcePath = item.SourcePath;
        PreviewMediaElement.Source = new Uri(item.SourcePath);
        IsVideoPreviewFallbackVisible = true;
        OnPropertyChanged(nameof(VideoPlayButtonText));
    }

    private void PrepareAudioPreview(MediaFileItem item, bool autoPlay)
    {
        StopAudioPreview();

        _pendingAudioPreviewSeek = item.PreviewStart;
        _playAudioWhenReady = autoPlay;

        if (_loadedAudioSourcePath == item.SourcePath && AudioPreviewElement.Source is not null)
        {
            SeekAudioPreview(item.PreviewStart, autoPlay);
            return;
        }

        _loadedAudioSourcePath = item.SourcePath;
        AudioPreviewElement.Source = new Uri(item.SourcePath);
        OnPropertyChanged(nameof(AudioPlayButtonText));
    }

    private void StopVideoPreview()
    {
        _previewStopTimer.Stop();
        _playPreviewWhenReady = false;
        _isPreviewPlaying = false;

        try
        {
            PreviewMediaElement.Pause();
        }
        catch
        {
            // MediaElement throws if no source is loaded yet; safe to ignore here.
        }

        OnPropertyChanged(nameof(VideoPlayButtonText));
    }

    private void StopAudioPreview()
    {
        _audioPreviewStopTimer.Stop();
        _playAudioWhenReady = false;
        _isAudioPreviewPlaying = false;

        try
        {
            AudioPreviewElement.Pause();
        }
        catch
        {
            // MediaElement throws if no source is loaded yet; safe to ignore here.
        }

        OnPropertyChanged(nameof(AudioPlayButtonText));
    }

    private void AudioPreviewPlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        if (_isAudioPreviewPlaying)
        {
            StopAudioPreview();
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
            SeekAudioPreview(SelectedMediaItem.TrimStart, autoPlay: false);
            return;
        }

        if (SelectedMediaItem.PreviewStart >= SelectedMediaItem.TrimEnd - TimeSpan.FromMilliseconds(100))
        {
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
        }

        PrepareAudioPreview(SelectedMediaItem, autoPlay: true);
    }

    private void PreviewPlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        if (_isPreviewPlaying)
        {
            StopVideoPreview();
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
            SeekVideoPreview(SelectedMediaItem.TrimStart, autoPlay: false);
            return;
        }

        if (SelectedMediaItem.PreviewStart >= SelectedMediaItem.TrimEnd - TimeSpan.FromMilliseconds(100))
        {
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
        }

        PrepareVideoPreview(SelectedMediaItem, autoPlay: true);
    }

    private void PreviewMediaElement_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        ApplyVideoPreviewSpeed(SelectedMediaItem);

        if (_playPreviewWhenReady)
        {
            IsVideoPreviewFallbackVisible = false;
            SeekVideoPreview(_pendingPreviewSeek, autoPlay: true);
            return;
        }

        IsVideoPreviewFallbackVisible = true;
    }

    private void PreviewMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StopVideoPreview();
        IsVideoPreviewFallbackVisible = true;
    }

    private void PreviewMediaElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        if (IsVideoPreviewLoopEnabled)
        {
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
            SeekVideoPreview(SelectedMediaItem.TrimStart, autoPlay: true);
            return;
        }

        StopVideoPreview();
        SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
        SeekVideoPreview(SelectedMediaItem.PreviewStart, autoPlay: false);
    }

    private void AudioPreviewElement_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null || SelectedMediaItem.Kind != MediaKind.Audio)
        {
            return;
        }

        SeekAudioPreview(_pendingAudioPreviewSeek, _playAudioWhenReady);
    }

    private void AudioPreviewElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        StopAudioPreview();
        StatusMessage = "Audio preview could not be played for this file.";
    }

    private void AudioPreviewElement_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (SelectedMediaItem is null || SelectedMediaItem.Kind != MediaKind.Audio)
        {
            return;
        }

        StopAudioPreview();
        SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
        SeekAudioPreview(SelectedMediaItem.PreviewStart, autoPlay: false);
    }

    private void AudioPreviewStopTimer_Tick(object? sender, EventArgs e)
    {
        if (SelectedMediaItem is null || SelectedMediaItem.Kind != MediaKind.Audio || !_isAudioPreviewPlaying)
        {
            return;
        }

        if (SelectedMediaItem.TrimEnd <= SelectedMediaItem.TrimStart)
        {
            return;
        }

        if (AudioPreviewElement.Position >= SelectedMediaItem.TrimEnd)
        {
            StopAudioPreview();
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
            SeekAudioPreview(SelectedMediaItem.PreviewStart, autoPlay: false);
            return;
        }

        SyncPreviewStartToPlaybackPosition(SelectedMediaItem, AudioPreviewElement.Position);
    }

    private void PreviewStopTimer_Tick(object? sender, EventArgs e)
    {
        if (SelectedMediaItem is null || !_isPreviewPlaying)
        {
            return;
        }

        if (SelectedMediaItem.TrimEnd <= SelectedMediaItem.TrimStart)
        {
            return;
        }

        if (PreviewMediaElement.Position >= SelectedMediaItem.TrimEnd)
        {
            SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;

            if (IsVideoPreviewLoopEnabled)
            {
                SeekVideoPreview(SelectedMediaItem.TrimStart, autoPlay: true);
                return;
            }

            StopVideoPreview();
            SeekVideoPreview(SelectedMediaItem.PreviewStart, autoPlay: false);
            return;
        }

        SyncPreviewStartToPlaybackPosition(SelectedMediaItem, PreviewMediaElement.Position);
    }

    private void SelectedMediaItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (SelectedMediaItem is null)
        {
            return;
        }

        if (e.PropertyName == nameof(MediaFileItem.PreviewStartRatio) && _isUpdatingPreviewStartFromPlayback)
        {
            return;
        }

        if (e.PropertyName is nameof(MediaFileItem.TrimStartRatio) or nameof(MediaFileItem.TrimEndRatio) or nameof(MediaFileItem.PreviewStartRatio))
        {
            if (SelectedMediaItem.Kind == MediaKind.Audio)
            {
                _pendingAudioPreviewSeek = SelectedMediaItem.PreviewStart;

                if (_isAudioPreviewPlaying && AudioPreviewElement.Source is not null)
                {
                    SeekAudioPreview(SelectedMediaItem.PreviewStart, autoPlay: true);
                }
            }
            else if (SelectedMediaItem.Kind == MediaKind.Video)
            {
                if (e.PropertyName == nameof(MediaFileItem.TrimStartRatio))
                {
                    _isUpdatingPreviewStartFromPlayback = true;
                    try
                    {
                        SelectedMediaItem.PreviewStartRatio = SelectedMediaItem.TrimStartRatio;
                    }
                    finally
                    {
                        _isUpdatingPreviewStartFromPlayback = false;
                    }
                }

                _pendingPreviewSeek = SelectedMediaItem.PreviewStart;
                IsVideoPreviewFallbackVisible = !_isPreviewPlaying;

                if (_isPreviewPlaying && PreviewMediaElement.Source is not null)
                {
                    if (e.PropertyName == nameof(MediaFileItem.TrimStartRatio))
                    {
                        SeekVideoPreview(SelectedMediaItem.TrimStart, autoPlay: true);
                    }
                    else
                    {
                        SeekVideoPreview(SelectedMediaItem.PreviewStart, autoPlay: true);
                    }
                }
            }
        }

        if (e.PropertyName == nameof(MediaFileItem.PlaybackSpeedMultiplier) &&
            SelectedMediaItem.Kind == MediaKind.Video)
        {
            ApplyVideoPreviewSpeed(SelectedMediaItem);
        }
    }

    private void SeekAudioPreview(TimeSpan targetPosition, bool autoPlay)
    {
        _pendingAudioPreviewSeek = targetPosition;
        _playAudioWhenReady = autoPlay;

        try
        {
            AudioPreviewElement.Pause();
            AudioPreviewElement.Position = targetPosition;
        }
        catch
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                AudioPreviewElement.Position = targetPosition;

                if (autoPlay)
                {
                    AudioPreviewElement.Play();
                    _isAudioPreviewPlaying = true;
                    _audioPreviewStopTimer.Start();
                }
                else
                {
                    AudioPreviewElement.Pause();
                    _isAudioPreviewPlaying = false;
                }

                OnPropertyChanged(nameof(AudioPlayButtonText));
            }
            catch
            {
                // If the media element is not ready yet, MediaOpened will perform the same seek.
            }
        }, DispatcherPriority.Background);
    }

    private void SyncPreviewStartToPlaybackPosition(MediaFileItem item, TimeSpan playbackPosition)
    {
        if (item.Duration <= TimeSpan.Zero)
        {
            return;
        }

        var clampedPosition = playbackPosition < item.TrimStart
            ? item.TrimStart
            : playbackPosition > item.TrimEnd
                ? item.TrimEnd
                : playbackPosition;

        var ratio = clampedPosition.TotalMilliseconds / item.Duration.TotalMilliseconds;

        _isUpdatingPreviewStartFromPlayback = true;
        try
        {
            item.PreviewStartRatio = ratio;
        }
        finally
        {
            _isUpdatingPreviewStartFromPlayback = false;
        }
    }

    private void SeekVideoPreview(TimeSpan targetPosition, bool autoPlay)
    {
        _pendingPreviewSeek = targetPosition;
        _playPreviewWhenReady = autoPlay;
        IsVideoPreviewFallbackVisible = !autoPlay;
        ApplyVideoPreviewSpeed(SelectedMediaItem);

        try
        {
            PreviewMediaElement.Pause();
            PreviewMediaElement.Position = targetPosition;
        }
        catch
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                PreviewMediaElement.Position = targetPosition;

                if (autoPlay)
                {
                    IsVideoPreviewFallbackVisible = false;
                    PreviewMediaElement.Play();
                    _isPreviewPlaying = true;
                    _previewStopTimer.Start();
                }
                else
                {
                    PreviewMediaElement.Pause();
                    _isPreviewPlaying = false;
                }

                OnPropertyChanged(nameof(VideoPlayButtonText));
            }
            catch
            {
                // If the media element is not ready yet, MediaOpened will perform the same seek.
            }
        }, DispatcherPriority.Background);
    }

    private void ApplyVideoPreviewSpeed(MediaFileItem? item)
    {
        if (item is null || item.Kind != MediaKind.Video)
        {
            return;
        }

        try
        {
            PreviewMediaElement.SpeedRatio = item.PlaybackSpeedMultiplier;
        }
        catch
        {
            // Some codecs can briefly reject playback-rate changes while media is initializing.
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectInt
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

