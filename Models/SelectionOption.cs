namespace TORQUE.Models;

public sealed class SelectionOption
{
    public SelectionOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }

    public override string ToString() => Label;
}

