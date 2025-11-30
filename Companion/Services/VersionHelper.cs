using System;
using System.IO;
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
            if (_fileSystem.Exists(versionFilePath)) return _fileSystem.ReadAllText(versionFilePath).Trim();
            return "v0.0.1";


            // return Assembly.GetExecutingAssembly()
            //     .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            //     .InformationalVersion ?? "Unknown Version";
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to get app version: {ex}");
            return "Unknown Version";
        }
    }

    private static bool IsDevelopment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }
}