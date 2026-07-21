namespace QueryPlus.Core.Domain;

/// <summary>A single script (one editor tab) to run against every target.</summary>
public sealed class ScriptItem
{
    public ScriptItem(string name, string text)
    {
        Name = name;
        Text = text;
    }

    public string Name { get; }

    public string Text { get; }
}
