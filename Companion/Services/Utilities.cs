using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Companion.Services;

public static class Utilities
{
    public static string RemoveSpecialCharacters(string input)
    {
        var pattern = @"[^a-zA-Z0-9\-\@]"; // Matches any character that is not a letter, number, or dash
        var output = Regex.Replace(input, pattern, "");
        return output;
    }
    
    public static string RemoveLastChar(string input)
    {
        if (input.Length > 0)
        {
            return input.Substring(0, input.Length - 1);
        }
        else
        {
            return input;
        }
    }

    public static string ComputeMd5Hash(byte[] rawData)
    {
        // Use MD5 to compute the hash
        using (var md5Hash = MD5.Create())
        {
            // Compute the hash for the byte array
            var bytes = md5Hash.ComputeHash(rawData);

            // Convert the bytes to a hexadecimal string
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));

            return builder.ToString();
        }
    }


    public static string ComputeSha256Hash(string rawData)
    {
        // Create a SHA256 instance
        using (var sha256Hash = SHA256.Create())
        {
            // Compute the hash as a byte array
            var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            // Convert the byte array to a hex string
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));

            return builder.ToString();
        }
    }

    public static bool IsValidIpAddress(string ipAddress)
    {
        var pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        return Regex.IsMatch(ipAddress, pattern);
    }
}