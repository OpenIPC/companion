using System.Collections.Generic;

namespace Companion.Models;

public sealed class FirmwareSourceDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Track { get; init; } = "Stable";
    public string SelectionMode { get; init; } = "Automatic";
    public IReadOnlyList<FirmwareChannelDefinition> Channels { get; init; } = [];
}

public sealed class FirmwareChannelDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Distribution { get; init; } = "GitHubRelease";
    public string Owner { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Branch { get; init; } = "main";
    public string ReleaseTag { get; init; } = "latest";
    public string Path { get; init; } = string.Empty;
    public string FileExtension { get; init; } = ".tgz";
}
