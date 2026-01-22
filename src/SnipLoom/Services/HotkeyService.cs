using System;
using System.Runtime.InteropServices;

namespace SnipLoom.Services;

/// <summary>
/// Service for registering and handling global hotkeys.
/// Will be fully implemented in Phase 3.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    // Hotkey IDs
    public const int HOTKEY_START_SELECTION = 1;
    public const int HOTKEY_STOP_RECORDING = 2;
    public const int HOTKEY_PAUSE_RESUME = 3;

    // Modifier keys
    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _hwnd;
    private bool _isRegistered;

    public event EventHandler? StartSelectionPressed;
    public event EventHandler? StopRecordingPressed;
    public event EventHandler? PauseResumePressed;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public bool RegisterDefaultHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return false;

        try
        {
            // Ctrl+Shift+R for start selection
            RegisterHotKey(_hwnd, HOTKEY_START_SELECTION, 
                (uint)(KeyModifiers.Control | KeyModifiers.Shift), 
                0x52); // R key

            // Ctrl+Shift+S for stop
            RegisterHotKey(_hwnd, HOTKEY_STOP_RECORDING,
                (uint)(KeyModifiers.Control | KeyModifiers.Shift),
                0x53); // S key

            // Ctrl+Shift+P for pause/resume
            RegisterHotKey(_hwnd, HOTKEY_PAUSE_RESUME,
                (uint)(KeyModifiers.Control | KeyModifiers.Shift),
                0x50); // P key

            _isRegistered = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RegisterDefaultHotkeys error: {ex}");
            return false;
        }
    }

    public void HandleHotkeyMessage(int hotkeyId)
    {
        switch (hotkeyId)
        {
            case HOTKEY_START_SELECTION:
                StartSelectionPressed?.Invoke(this, EventArgs.Empty);
                break;
            case HOTKEY_STOP_RECORDING:
                StopRecordingPressed?.Invoke(this, EventArgs.Empty);
                break;
            case HOTKEY_PAUSE_RESUME:
                PauseResumePressed?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void UnregisterHotkeys()
    {
        if (!_isRegistered || _hwnd == IntPtr.Zero) return;

        UnregisterHotKey(_hwnd, HOTKEY_START_SELECTION);
        UnregisterHotKey(_hwnd, HOTKEY_STOP_RECORDING);
        UnregisterHotKey(_hwnd, HOTKEY_PAUSE_RESUME);

        _isRegistered = false;
    }

    public void Dispose()
    {
        UnregisterHotkeys();
    }
}
