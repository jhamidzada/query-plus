using System.Data;

namespace QueryPlus.Core.Results;

/// <summary>One result set returned by a script, tagged with the originating script name.</summary>
public sealed class ResultSet
{
    public ResultSet(string scriptName, DataTable table)
    {
        ScriptName = scriptName;
        Table = table;
    }

    public string ScriptName { get; }

    public DataTable Table { get; }
}
