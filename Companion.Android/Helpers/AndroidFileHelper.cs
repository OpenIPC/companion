using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Util;

namespace OpenIPC_Config.Android.Helpers;

public class AndroidFileHelper
{
    public static string ReadAssetFile(string relativePath)
    {
        // var assets = Android.App.Application.Context.Assets;
        var assets = Application.Context.Assets;

        using (var stream = assets.Open(relativePath))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }

    public static string[] ListAssetFiles(string folderPath)
    {
        var assets = Application.Context.Assets;
        return assets.List(folderPath); // Lists all files in the folder
    }

    public static void CopyAssetsToInternalStorage(Context context)
    {
        var assets = context.Assets;
        var internalStoragePath = context.FilesDir.AbsolutePath; // Internal storage path

        // Start recursive copy from the root "binaries" folder
        CopyFolder(assets, "binaries", internalStoragePath);
    }

    private static void CopyFolder(AssetManager assets, string sourceFolder, string destinationFolder)
    {
        try
        {
            // Ensure the destination folder exists
            Directory.CreateDirectory(destinationFolder);

            // List all items (files and folders) in the source folder
            string[] items = assets.List(sourceFolder);

            foreach (var item in items)
            {
                var sourcePath = string.IsNullOrEmpty(sourceFolder) ? item : $"{sourceFolder}/{item}";
                var destinationPath = Path.Combine(destinationFolder, item);

                // Check if the item is a directory or file
                if (assets.List(sourcePath).Length > 0)
                {
                    // If it's a folder, recursively copy its contents
                    CopyFolder(assets, sourcePath, destinationPath);
                }
                else
                {
                    // If it's a file, copy it
                    using (var input = assets.Open(sourcePath))
                    using (var output = new FileStream(destinationPath, FileMode.Create))
                    {
                        input.CopyTo(output);
                    }

                    Log.Debug("FileHelper", $"Copied file: {sourcePath} to {destinationPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("FileHelper", $"Error copying assets: {ex.Message}");
        }
    }
}