using System;
using System.Collections.Generic;
using System.Linq;
using Companion.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Companion.Services;

public sealed class FirmwareSourceService : IFirmwareSourceService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public FirmwareSourceService(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger.ForContext<FirmwareSourceService>();
    }

    public IReadOnlyList<FirmwareSourceDefinition> GetSources()
    {
        var configuredSources = _configuration.GetSection("FirmwareSources")
            .GetChildren()
            .Select(ReadSource)
            .Where(source => source != null)
            .Cast<FirmwareSourceDefinition>()
            .ToList();

        if (configuredSources.Count > 0)
            return configuredSources;

        _logger.Information("No firmware sources configured; using built-in defaults.");
        return CreateDefaults();
    }

    private FirmwareSourceDefinition? ReadSource(IConfigurationSection section)
    {
        string name = section["Name"] ?? string.Empty;
        var channels = section.GetSection("Channels")
            .GetChildren()
            .Select(ReadChannel)
            .Where(channel => channel != null)
            .Cast<FirmwareChannelDefinition>()
            .ToList();

        if (string.IsNullOrWhiteSpace(name) || channels.Count == 0)
        {
            _logger.Warning("Ignoring invalid firmware source at configuration key {Key}.", section.Path);
            return null;
        }

        return new FirmwareSourceDefinition
        {
            Name = name,
            Track = FirmwareTrack.Normalize(section["Track"] ?? FirmwareTrack.Stable),
            SelectionMode = section["SelectionMode"] ?? "Automatic",
            Channels = channels
        };
    }

    private FirmwareChannelDefinition? ReadChannel(IConfigurationSection section)
    {
        string name = section["Name"] ?? string.Empty;
        string owner = section["Owner"] ?? string.Empty;
        string repository = section["Repository"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner) ||
            string.IsNullOrWhiteSpace(repository))
            return null;

        return new FirmwareChannelDefinition
        {
            Name = name,
            Distribution = section["Distribution"] ?? "GitHubRelease",
            Owner = owner,
            Repository = repository,
            Branch = section["Branch"] ?? "main",
            ReleaseTag = section["ReleaseTag"] ?? "latest",
            Path = section["Path"] ?? string.Empty,
            FileExtension = section["FileExtension"] ?? ".tgz"
        };
    }

    public static IReadOnlyList<FirmwareSourceDefinition> CreateDefaults() =>
    [
        new FirmwareSourceDefinition
        {
            Name = "OpenIPC Builder",
            Track = "Stable",
            SelectionMode = "Automatic",
            Channels =
            [
                new FirmwareChannelDefinition
                {
                    Name = "Latest",
                    Owner = "OpenIPC",
                    Repository = "builder"
                }
            ]
        },
        new FirmwareSourceDefinition
        {
            Name = "Greg APFPV",
            Track = FirmwareTrack.Experimental,
            SelectionMode = "BySoc",
            Channels =
            [
                new FirmwareChannelDefinition
                {
                    Name = "Latest",
                    Distribution = "RepositoryContents",
                    Owner = "sickgreg",
                    Repository = "OpenIPC_sickgregFPV_apfpv",
                    Branch = "main"
                }
            ]
        },
        new FirmwareSourceDefinition
        {
            Name = "Mabur",
            Track = FirmwareTrack.Experimental,
            SelectionMode = "BySoc",
            Channels =
            [
                new FirmwareChannelDefinition
                {
                    Name = "Latest",
                    Distribution = GitHubReleaseList.Distribution,
                    Owner = "gilankpam",
                    Repository = "openipc-builder"
                }
            ]
        }
    ];
}
