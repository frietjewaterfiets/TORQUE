namespace TORQUE;

public static class AppInfo
{
    public const string DisplayName = "TORQUE";
    public const string Version = "v1.0";
    public const string AuthorName = "frietjewaterfiets";
    public const string DefaultThemeTextColorHex = "#EC5E27";
    public const string DefaultThemeBackgroundColorHex = "#202020";
    public const string DataDirectoryName = "TORQUE";
    public const string DefaultExportFolderName = "TORQUE";
    public const string BrandingFolderName = "branding";
    public static string DisplayCreditLine => $"{DisplayName} - By {AuthorName}";

    // Keep these old names so existing user settings/export folders migrate cleanly.
    public static IReadOnlyList<string> LegacyDataDirectoryNames { get; } =
    [
        "EpicMultiMediaTool",
        "SuperUltraMultiTool",
    ];

    public static IReadOnlyList<string> LegacyDefaultExportFolderNames { get; } =
    [
        "epicmultimediatool",
        "superultramultitool",
    ];
}

