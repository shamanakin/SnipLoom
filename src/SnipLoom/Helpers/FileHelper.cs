using System;
using System.IO;

namespace SnipLoom.Helpers;

public static class FileHelper
{
    /// <summary>
    /// Gets the temp directory for SnipLoom recordings
    /// </summary>
    public static string GetTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SnipLoom");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Generates a temp file path for a new recording
    /// </summary>
    public static string GetTempRecordingPath()
    {
        var tempDir = GetTempDirectory();
        var fileName = $"recording-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.mp4";
        return Path.Combine(tempDir, fileName);
    }

    /// <summary>
    /// Moves a file atomically (as much as possible on Windows)
    /// </summary>
    public static bool AtomicMove(string sourcePath, string destPath)
    {
        try
        {
            // Ensure destination directory exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // If same drive, use Move (atomic on NTFS)
            // If different drives, copy then delete
            var sourceRoot = Path.GetPathRoot(sourcePath);
            var destRoot = Path.GetPathRoot(destPath);

            if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
            {
                File.Move(sourcePath, destPath, overwrite: true);
            }
            else
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                File.Delete(sourcePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AtomicMove error: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Cleans up old temp recordings (older than specified hours)
    /// </summary>
    public static void CleanupOldTempFiles(int olderThanHours = 24)
    {
        try
        {
            var tempDir = GetTempDirectory();
            if (!Directory.Exists(tempDir)) return;

            var cutoff = DateTime.Now.AddHours(-olderThanHours);
            foreach (var file in Directory.GetFiles(tempDir, "*.mp4"))
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoff)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore files that can't be deleted
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CleanupOldTempFiles error: {ex}");
        }
    }

    /// <summary>
    /// Gets all temp recordings that haven't been saved
    /// </summary>
    public static string[] GetUnsavedRecordings()
    {
        var tempDir = GetTempDirectory();
        if (!Directory.Exists(tempDir))
            return Array.Empty<string>();

        return Directory.GetFiles(tempDir, "recording-*.mp4");
    }
}
