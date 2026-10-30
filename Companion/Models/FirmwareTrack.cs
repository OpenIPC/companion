using System;

namespace Companion.Models;

public static class FirmwareTrack
{
    public const string Stable = "Stable";
    public const string Experimental = "Experimental";

    // Earlier name of the Experimental track, still found in existing settings and preferences.
    public const string LegacyTest = "Test";

    public static string Normalize(string track) =>
        string.Equals(track, LegacyTest, StringComparison.OrdinalIgnoreCase) ? Experimental : track;

    public static bool IsExperimental(string? track) =>
        string.Equals(track, Experimental, StringComparison.OrdinalIgnoreCase);
}
