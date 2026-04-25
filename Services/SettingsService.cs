using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using TORQUE;
using TORQUE.Models;

namespace TORQUE.Services;

public sealed class SettingsService
{
    private static readonly string[] SupportedImageFormats = ["jpg", "png", "webp", "bmp", "tiff"];
    private static readonly string[] SupportedAudioFormats = ["wav", "mp3", "flac", "aac", "m4a", "ogg", "aiff"];
    private static readonly string[] SupportedVideoFormats = ["mp4", "mov", "mkv", "webm", "avi", "gif", "mp3", "wav"];
    private static readonly string[] SupportedPdfActions = ["pages-jpg", "pages-png", "pages-webp", "merge", "split-pages", "invert-order"];
    private const int DefaultWindowScalePercent = 90;
    private const int LegacyDefaultWindowScalePercent = 88;
    private const int MinWindowScalePercent = 66;
    private const int MaxWindowScalePercent = 120;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.DataDirectoryName);

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync()
    {
        Directory.CreateDirectory(SettingsDirectory);

        if (File.Exists(SettingsFilePath))
        {
            var currentSettings = await LoadSettingsAsync(SettingsFilePath);
            return NormalizeSettings(currentSettings);
        }

        foreach (var legacySettingsFilePath in GetLegacySettingsFilePaths())
        {
            if (!File.Exists(legacySettingsFilePath))
            {
                continue;
            }

            var legacySettings = NormalizeSettings(await LoadSettingsAsync(legacySettingsFilePath));
            await SaveAsync(legacySettings);
            return legacySettings;
        }

        return CreateDefaultSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        await using var stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, NormalizeSettings(settings), _jsonOptions);
    }

    public AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            DefaultExportPath = GetDefaultExportPath(),
            CreateDateFolders = true,
            DefaultImageCompressionPercent = 0,
            DefaultVideoResolutionDivisor = 1,
            DefaultPdfCompressionLevel = 0,
            DefaultWindowScalePercent = DefaultWindowScalePercent,
            DefaultImageFormat = "jpg",
            DefaultAudioFormat = "wav",
            DefaultVideoFormat = "mp4",
            DefaultVideoPreviewLoopEnabled = true,
            DefaultPdfMode = "convert",
            DefaultPdfAction = "pages-jpg",
            ThemeTextColorHex = AppInfo.DefaultThemeTextColorHex,
            ThemeBackgroundColorHex = AppInfo.DefaultThemeBackgroundColorHex,
            OpenExportFolderAfterExport = true,
            ReturnHomeAfterExport = false,
            RemoveMetadata = true,
        };
    }

    private async Task<AppSettings?> LoadSettingsAsync(string settingsFilePath)
    {
        var json = await File.ReadAllTextAsync(settingsFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
        if (settings is null)
        {
            return null;
        }

        if (!JsonContainsProperty(json, nameof(AppSettings.RemoveMetadata)))
        {
            settings.RemoveMetadata = true;
        }

        return settings;
    }

    private AppSettings NormalizeSettings(AppSettings? settings)
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.DefaultExportPath))
        {
            return CreateDefaultSettings();
        }

        if (IsLegacyDefaultExportPath(settings.DefaultExportPath))
        {
            settings.DefaultExportPath = GetDefaultExportPath();
        }

        settings.DefaultImageCompressionPercent = Math.Clamp(settings.DefaultImageCompressionPercent, 0, 100);
        settings.DefaultVideoResolutionDivisor = Math.Clamp(settings.DefaultVideoResolutionDivisor, 1, 4);
        settings.DefaultPdfCompressionLevel = PdfService.NormalizeCompressionLevel(settings.DefaultPdfCompressionLevel);
        if (settings.DefaultWindowScalePercent == LegacyDefaultWindowScalePercent)
        {
            settings.DefaultWindowScalePercent = DefaultWindowScalePercent;
        }

        settings.DefaultWindowScalePercent = NormalizeWindowScale(settings.DefaultWindowScalePercent);
        settings.DefaultImageFormat = NormalizeFormat(settings.DefaultImageFormat, SupportedImageFormats, "jpg");
        settings.DefaultAudioFormat = NormalizeFormat(settings.DefaultAudioFormat, SupportedAudioFormats, "wav");
        settings.DefaultVideoFormat = NormalizeFormat(settings.DefaultVideoFormat, SupportedVideoFormats, "mp4");
        settings.DefaultPdfMode = "convert";
        settings.DefaultPdfAction = NormalizeFormat(settings.DefaultPdfAction, SupportedPdfActions, "pages-jpg");
        settings.ThemeTextColorHex = NormalizeThemeColor(settings.ThemeTextColorHex, AppInfo.DefaultThemeTextColorHex);
        settings.ThemeBackgroundColorHex = NormalizeThemeColor(settings.ThemeBackgroundColorHex, AppInfo.DefaultThemeBackgroundColorHex);
        settings.CustomSuffix ??= string.Empty;

        return settings;
    }

    private static bool JsonContainsProperty(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out _);
        }
        catch
        {
            return false;
        }
    }

    private static int NormalizeWindowScale(int scalePercent)
    {
        if (scalePercent <= 0)
        {
            return DefaultWindowScalePercent;
        }

        return Math.Clamp(scalePercent, MinWindowScalePercent, MaxWindowScalePercent);
    }

    private static string NormalizeFormat(string? value, IReadOnlyCollection<string> supportedFormats, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return supportedFormats.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : fallback;
    }

    private static string NormalizeThemeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value.Trim())!;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return fallback;
        }
    }

    private static string GetDefaultExportPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            AppInfo.DefaultExportFolderName,
            "Exports");
    }

    private static bool IsLegacyDefaultExportPath(string path)
    {
        return AppInfo.LegacyDefaultExportFolderNames.Any(folderName =>
            string.Equals(
                path,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    folderName,
                    "exports"),
                StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetLegacySettingsFilePaths()
    {
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return AppInfo.LegacyDataDirectoryNames.Select(directoryName =>
            Path.Combine(roamingAppData, directoryName, "settings.json"));
    }
}

