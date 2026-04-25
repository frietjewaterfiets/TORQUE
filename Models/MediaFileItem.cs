using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using TORQUE.Infrastructure;

namespace TORQUE.Models;

public sealed class MediaFileItem : ObservableObject
{
    private const double MinimumCropRatio = 0.05d;
    private const double MaxEditorCanvasWidth = 960d;
    private const double MaxEditorCanvasHeight = 520d;

    private TimeSpan _duration;
    private double _trimStartRatio;
    private double _trimEndRatio = 1d;
    private double _previewStartRatio;
    private double _playbackSpeedMultiplier = 1d;
    private double _cropLeftRatio;
    private double _cropTopRatio;
    private double _cropRightRatio = 1d;
    private double _cropBottomRatio = 1d;
    private int _pixelWidth = 1920;
    private int _pixelHeight = 1080;
    private int _pageCount = 1;
    private string? _thumbnailPath;
    private string? _previewPath;
    private bool _assetsPrepared;

    public MediaFileItem(string sourcePath, MediaKind kind)
    {
        SourcePath = sourcePath;
        Kind = kind;
        DisplayName = Path.GetFileName(sourcePath);
        BaseName = Path.GetFileNameWithoutExtension(sourcePath);
        Extension = Path.GetExtension(sourcePath).TrimStart('.').ToUpperInvariant();
    }

    public string SourcePath { get; }

    public MediaKind Kind { get; }

    public string DisplayName { get; }

    public string BaseName { get; }

    public string Extension { get; }

    public ObservableCollection<string> TimelineFrames { get; } = new();

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                OnPropertyChanged(nameof(DurationLabel));
                OnPropertyChanged(nameof(TrimStart));
                OnPropertyChanged(nameof(TrimEnd));
                OnPropertyChanged(nameof(TrimWindowLabel));
                OnPropertyChanged(nameof(PreviewStart));
                OnPropertyChanged(nameof(PreviewStartLabel));
            }
        }
    }

    public double TrimStartRatio
    {
        get => _trimStartRatio;
        set
        {
            var safeValue = Math.Clamp(value, 0d, Math.Max(0d, TrimEndRatio - 0.01d));
            if (SetProperty(ref _trimStartRatio, safeValue))
            {
                CoercePreviewStartRatio();
                OnPropertyChanged(nameof(TrimStart));
                OnPropertyChanged(nameof(TrimWindowLabel));
            }
        }
    }

    public double TrimEndRatio
    {
        get => _trimEndRatio;
        set
        {
            var safeValue = Math.Clamp(value, Math.Min(0.01d, 1d), 1d);
            safeValue = Math.Max(safeValue, TrimStartRatio + 0.01d);

            if (SetProperty(ref _trimEndRatio, Math.Min(1d, safeValue)))
            {
                CoercePreviewStartRatio();
                OnPropertyChanged(nameof(TrimEnd));
                OnPropertyChanged(nameof(TrimWindowLabel));
            }
        }
    }

    public TimeSpan TrimStart => TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * TrimStartRatio);

    public TimeSpan TrimEnd => TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * TrimEndRatio);

    public double PreviewStartRatio
    {
        get => _previewStartRatio;
        set
        {
            var safeValue = Math.Clamp(value, TrimStartRatio, TrimEndRatio);
            if (SetProperty(ref _previewStartRatio, safeValue))
            {
                OnPropertyChanged(nameof(PreviewStart));
                OnPropertyChanged(nameof(PreviewStartLabel));
            }
        }
    }

    public TimeSpan PreviewStart => TimeSpan.FromMilliseconds(Duration.TotalMilliseconds * PreviewStartRatio);

    public double PlaybackSpeedMultiplier
    {
        get => _playbackSpeedMultiplier;
        set
        {
            var safeValue = value switch
            {
                <= 0.5d => 0.5d,
                <= 1d => 1d,
                _ => 2d,
            };

            if (SetProperty(ref _playbackSpeedMultiplier, safeValue))
            {
                OnPropertyChanged(nameof(PlaybackSpeedOptionLabel));
                OnPropertyChanged(nameof(PlaybackSpeedLabel));
            }
        }
    }

    public double CropLeftRatio
    {
        get => _cropLeftRatio;
        set
        {
            var safeValue = Math.Clamp(value, 0d, Math.Max(0d, CropRightRatio - MinimumCropRatio));
            if (SetProperty(ref _cropLeftRatio, safeValue))
            {
                OnCropChanged();
            }
        }
    }

    public double CropTopRatio
    {
        get => _cropTopRatio;
        set
        {
            var safeValue = Math.Clamp(value, 0d, Math.Max(0d, CropBottomRatio - MinimumCropRatio));
            if (SetProperty(ref _cropTopRatio, safeValue))
            {
                OnCropChanged();
            }
        }
    }

    public double CropRightRatio
    {
        get => _cropRightRatio;
        set
        {
            var safeValue = Math.Clamp(value, Math.Min(MinimumCropRatio, 1d), 1d);
            safeValue = Math.Max(safeValue, CropLeftRatio + MinimumCropRatio);

            if (SetProperty(ref _cropRightRatio, Math.Min(1d, safeValue)))
            {
                OnCropChanged();
            }
        }
    }

    public double CropBottomRatio
    {
        get => _cropBottomRatio;
        set
        {
            var safeValue = Math.Clamp(value, Math.Min(MinimumCropRatio, 1d), 1d);
            safeValue = Math.Max(safeValue, CropTopRatio + MinimumCropRatio);

            if (SetProperty(ref _cropBottomRatio, Math.Min(1d, safeValue)))
            {
                OnCropChanged();
            }
        }
    }

    public int PixelWidth
    {
        get => _pixelWidth;
        set
        {
            if (SetProperty(ref _pixelWidth, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(EditorCanvasWidth));
                OnPropertyChanged(nameof(EditorCanvasHeight));
            }
        }
    }

    public int PixelHeight
    {
        get => _pixelHeight;
        set
        {
            if (SetProperty(ref _pixelHeight, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(EditorCanvasWidth));
                OnPropertyChanged(nameof(EditorCanvasHeight));
            }
        }
    }

    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (SetProperty(ref _pageCount, Math.Max(1, value)))
            {
                OnPropertyChanged(nameof(PageCountLabel));
            }
        }
    }

    public string DurationLabel => Duration <= TimeSpan.Zero
        ? string.Empty
        : FormatEditorTime(Duration);

    public string TrimWindowLabel => Duration <= TimeSpan.Zero
        ? "CUT: --:-- - --:--"
        : $"CUT: {FormatEditorTime(TrimStart)} - {FormatEditorTime(TrimEnd)}";

    public string PreviewStartLabel => Duration <= TimeSpan.Zero
        ? "PREVIEW: --:--"
        : $"PREVIEW: {FormatEditorTime(PreviewStart)}";

    public string PlaybackSpeedOptionLabel => FormatSpeedMultiplier(PlaybackSpeedMultiplier);

    public string PlaybackSpeedLabel => $"SPEED: {PlaybackSpeedOptionLabel}";

    public double CropWidthRatio => Math.Max(0d, CropRightRatio - CropLeftRatio);

    public double CropHeightRatio => Math.Max(0d, CropBottomRatio - CropTopRatio);

    public string CropWindowLabel => $"CROP: {Math.Round(CropWidthRatio * 100d):0}% x {Math.Round(CropHeightRatio * 100d):0}%";

    public string PageCountLabel => PageCount == 1
        ? "1 PAGE"
        : $"{PageCount} PAGES";

    public double EditorCanvasWidth
    {
        get
        {
            var scale = Math.Min(MaxEditorCanvasWidth / PixelWidth, MaxEditorCanvasHeight / PixelHeight);
            return PixelWidth * scale;
        }
    }

    public double EditorCanvasHeight
    {
        get
        {
            var scale = Math.Min(MaxEditorCanvasWidth / PixelWidth, MaxEditorCanvasHeight / PixelHeight);
            return PixelHeight * scale;
        }
    }

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set => SetProperty(ref _thumbnailPath, value);
    }

    public string? PreviewPath
    {
        get => _previewPath;
        set => SetProperty(ref _previewPath, value);
    }

    public bool AssetsPrepared
    {
        get => _assetsPrepared;
        set => SetProperty(ref _assetsPrepared, value);
    }

    public void ReplaceTimelineFrames(IEnumerable<string> frames)
    {
        TimelineFrames.Clear();

        foreach (var frame in frames)
        {
            TimelineFrames.Add(frame);
        }
    }

    private static string FormatEditorTime(TimeSpan value)
    {
        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatSpeedMultiplier(double value)
    {
        return value % 1d == 0d
            ? $"x{value.ToString("0", CultureInfo.InvariantCulture)}"
            : $"x{value.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    private void OnCropChanged()
    {
        OnPropertyChanged(nameof(CropWidthRatio));
        OnPropertyChanged(nameof(CropHeightRatio));
        OnPropertyChanged(nameof(CropWindowLabel));
    }

    private void CoercePreviewStartRatio()
    {
        PreviewStartRatio = Math.Clamp(PreviewStartRatio, TrimStartRatio, TrimEndRatio);
    }
}

