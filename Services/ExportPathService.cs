using System.Globalization;
using System.IO;
using TORQUE.Models;

namespace TORQUE.Services;

public sealed class ExportPathService
{
    public string ResolveSessionFolder(AppSettings settings, MediaKind mediaKind, string? customFolder)
    {
        var usingDefaultFolder = string.IsNullOrWhiteSpace(customFolder);
        var baseFolder = usingDefaultFolder ? settings.DefaultExportPath : customFolder;

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            throw new InvalidOperationException("No export folder has been configured.");
        }

        var targetFolder = baseFolder;

        if (usingDefaultFolder && settings.CreateDateFolders)
        {
            targetFolder = Path.Combine(targetFolder, DateTime.Now.ToString("d-M-yyyy", CultureInfo.InvariantCulture));
        }

        Directory.CreateDirectory(targetFolder);
        return targetFolder;
    }

    public string GetDefaultPreview(AppSettings settings, MediaKind mediaKind)
    {
        var baseFolder = settings.DefaultExportPath;

        if (settings.CreateDateFolders)
        {
            baseFolder = Path.Combine(baseFolder, DateTime.Now.ToString("d-M-yyyy", CultureInfo.InvariantCulture));
        }

        return baseFolder;
    }

    public string GetUniqueOutputPath(string folderPath, string baseName, string extension)
    {
        var safeExtension = extension.StartsWith('.') ? extension : $".{extension}";
        var candidate = Path.Combine(folderPath, $"{baseName}{safeExtension}");
        var attempt = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName}-{attempt}{safeExtension}");
            attempt++;
        }

        return candidate;
    }
}

