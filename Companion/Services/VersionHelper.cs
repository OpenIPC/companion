using System;
using System.IO;
using System.Reflection;
using Serilog;

namespace Companion.Services;

public interface IFileSystem
{
    bool Exists(string path);
    string ReadAllText(string path);
}

public class FileSystem : IFileSystem
{
    public bool Exists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }
}

public static class VersionHelper
{
    private static IFileSystem _fileSystem = new FileSystem();

    public static void SetFileSystem(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }


    public static string GetAppVersion()
    {
        try
        {
            var versionFilePath = Path.Combine(AppContext.BaseDirectory, "VERSION");
            if (_fileSystem.Exists(versionFilePath))
            {
                var fileVersion = _fileSystem.ReadAllText(versionFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(fileVersion) &&
                    !fileVersion.Equals("v0.0.0", StringComparison.OrdinalIgnoreCase) &&
                    !fileVersion.Equals("0.0.0", StringComparison.OrdinalIgnoreCase))
                {
                    return EnsureVersionPrefix(fileVersion);
                }
            }
            var informationalVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return EnsureVersionPrefix(informationalVersion.Trim());

            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            return string.IsNullOrWhiteSpace(assemblyVersion)
                ? "Unknown Version"
                : EnsureVersionPrefix(assemblyVersion);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to get app version: {ex}");
            return "Unknown Version";
        }
    }

    private static string EnsureVersionPrefix(string version)
    {
        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
    }
}
