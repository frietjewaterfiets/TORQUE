namespace TORQUE.Models;

public sealed class AppSettings
{
    public string DefaultExportPath { get; set; } = string.Empty;

    public bool CreateDateFolders { get; set; } = true;

    public int DefaultImageCompressionPercent { get; set; }

    public int DefaultVideoResolutionDivisor { get; set; } = 1;

    public int DefaultPdfCompressionLevel { get; set; }

    public int DefaultWindowScalePercent { get; set; } = 90;

    public string DefaultImageFormat { get; set; } = "jpg";

    public string DefaultAudioFormat { get; set; } = "wav";

    public string DefaultVideoFormat { get; set; } = "mp4";

    public bool DefaultVideoPreviewLoopEnabled { get; set; } = true;

    public string DefaultPdfMode { get; set; } = "convert";

    public string DefaultPdfAction { get; set; } = "pages-jpg";

    public string ThemeTextColorHex { get; set; } = TORQUE.AppInfo.DefaultThemeTextColorHex;

    public string ThemeBackgroundColorHex { get; set; } = TORQUE.AppInfo.DefaultThemeBackgroundColorHex;

    public bool AddCustomSuffixToFiles { get; set; }

    public string CustomSuffix { get; set; } = string.Empty;

    public bool OpenExportFolderAfterExport { get; set; } = true;

    public bool ReturnHomeAfterExport { get; set; }

    public bool RemoveMetadata { get; set; } = true;
}

