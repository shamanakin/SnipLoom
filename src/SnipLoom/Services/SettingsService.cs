using System;
using System.IO;
using System.Text.Json;

namespace SnipLoom.Services;

/// <summary>
/// Service for managing user settings.
/// Will be fully implemented in Phase 4.
/// </summary>
public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SnipLoom",
        "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsService.Load error: {ex}");
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsService.Save error: {ex}");
        }
    }
}

public class AppSettings
{
    public int FrameRate { get; set; } = 30;
    public string Quality { get; set; } = "Medium"; // Low, Medium, High
    public bool CaptureCursor { get; set; } = true;
    public bool CaptureSystemAudio { get; set; } = true;
    public bool CaptureMicrophone { get; set; } = false;
    public bool ShowCountdown { get; set; } = true;
    public int CountdownSeconds { get; set; } = 3;
    public string DefaultSaveLocation { get; set; } = "";
    
    // Hotkey settings (virtual key codes)
    public int HotkeyStartModifiers { get; set; } = 6; // Ctrl+Shift
    public int HotkeyStartKey { get; set; } = 0x52; // R
    public int HotkeyStopModifiers { get; set; } = 6;
    public int HotkeyStopKey { get; set; } = 0x53; // S
    public int HotkeyPauseModifiers { get; set; } = 6;
    public int HotkeyPauseKey { get; set; } = 0x50; // P

    public int GetBitrate()
    {
        return Quality switch
        {
            "Low" => 2_000_000,    // 2 Mbps
            "Medium" => 5_000_000,  // 5 Mbps
            "High" => 10_000_000,   // 10 Mbps
            _ => 5_000_000
        };
    }
}
