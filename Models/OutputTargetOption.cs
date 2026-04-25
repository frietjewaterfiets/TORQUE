namespace TORQUE.Models;

public sealed class OutputTargetOption
{
    public OutputTargetOption(string key, string label, string? folderPath, bool isDefault)
    {
        Key = key;
        Label = label;
        FolderPath = folderPath;
        IsDefault = isDefault;
    }

    public string Key { get; }

    public string Label { get; }

    public string? FolderPath { get; }

    public bool IsDefault { get; }

    public override string ToString()
    {
        return Label;
    }
}

