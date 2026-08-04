using System.Reflection;

namespace QueryPlus.App.Services;

/// <summary>
/// The running build's version, read from the assembly rather than from file timestamps.
/// <see cref="Display"/> is the semantic version for the UI; <see cref="Detail"/> adds the
/// build id so two builds of the same version can be told apart.
/// </summary>
public static class AppVersion
{
    static AppVersion()
    {
        // InformationalVersion looks like "1.0.0+build.20260804.0812" (SemVer + build metadata).
        var informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        var plus = informational.IndexOf('+');
        Semantic = plus > 0 ? informational[..plus] : informational;
        BuildId = plus > 0 && plus + 1 < informational.Length ? informational[(plus + 1)..] : string.Empty;
    }

    /// <summary>e.g. "1.0.0".</summary>
    public static string Semantic { get; }

    /// <summary>e.g. "build.20260804.0812"; empty when the build supplied no metadata.</summary>
    public static string BuildId { get; }

    /// <summary>Short form for the status bar, e.g. "v1.0.0".</summary>
    public static string Display => "v" + Semantic;

    /// <summary>Full form for tooltips / --version, e.g. "QueryPlus v1.0.0 (build.20260804.0812)".</summary>
    public static string Detail => string.IsNullOrEmpty(BuildId)
        ? $"QueryPlus {Display}"
        : $"QueryPlus {Display}  ({BuildId})";
}
