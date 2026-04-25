namespace TORQUE.Models;

public sealed class PdfActionOption
{
    public PdfActionOption(string key, string label, string modeKey, string outputExtension)
    {
        Key = key;
        Label = label;
        ModeKey = modeKey;
        OutputExtension = outputExtension;
    }

    public string Key { get; }

    public string Label { get; }

    public string ModeKey { get; }

    public string OutputExtension { get; }

    public bool ProducesPdf => OutputExtension.Equals("pdf", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Label;
}

