using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Windows.Graphics.Capture;

namespace SnipLoom.Services;

/// <summary>
/// Result from capture source selection
/// </summary>
public class CaptureSelection
{
    public enum CaptureType { None, Display, Window, Region }
    
    public CaptureType Type { get; init; }
    public string DisplayName { get; init; } = "";
    public IntPtr WindowHandle { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    
    // For region capture - physical pixels (for ScreenRecorderLib)
    // These are relative to the target display, not virtual screen coordinates
    public int RegionX { get; init; }
    public int RegionY { get; init; }
    public int RegionWidth { get; init; }
    public int RegionHeight { get; init; }
    
    // For region capture - WPF DIPs (for recording frame overlay)
    // These are virtual screen coordinates for positioning the overlay
    public double RegionDipX { get; init; }
    public double RegionDipY { get; init; }
    public double RegionDipWidth { get; init; }
    public double RegionDipHeight { get; init; }
    
    // Target display device name for region capture (e.g., \\.\DISPLAY1)
    public string? TargetDisplayDevice { get; init; }
}

/// <summary>
/// Service for selecting capture sources (displays, windows, or regions)
/// </summary>
public class CaptureService
{
    public string? LastError { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    /// <summary>
    /// Show the system capture picker and return the selection
    /// </summary>
    public async Task<CaptureSelection?> PickCaptureSourceAsync()
    {
        LastError = null;
        
        try
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                LastError = "Screen capture is not supported on this device";
                return null;
            }

            // Get the main window handle from WPF
            IntPtr hwnd = IntPtr.Zero;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    hwnd = new WindowInteropHelper(mainWindow).Handle;
                }
            });
            
            if (hwnd == IntPtr.Zero)
            {
                LastError = "Could not get window handle";
                return null;
            }

            var picker = new GraphicsCapturePicker();
            InitializeWithWindow(picker, hwnd);

            var captureItem = await picker.PickSingleItemAsync();
            
            if (captureItem == null)
            {
                LastError = "No capture source selected";
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"Selected: {captureItem.DisplayName}, Size: {captureItem.Size.Width}x{captureItem.Size.Height}");

            // Try to determine if this is a window or display
            // Windows.Graphics.Capture doesn't directly expose the handle, but we can use
            // the GraphicsCaptureItem properties
            var windowHandle = TryGetWindowHandle(captureItem);
            
            // If we got a window handle, bring that window to the front
            if (windowHandle != IntPtr.Zero)
            {
                BringWindowToFront(windowHandle);
                
                return new CaptureSelection
                {
                    Type = CaptureSelection.CaptureType.Window,
                    DisplayName = captureItem.DisplayName,
                    WindowHandle = windowHandle,
                    Width = captureItem.Size.Width,
                    Height = captureItem.Size.Height
                };
            }
            else
            {
                // Assume it's a display
                return new CaptureSelection
                {
                    Type = CaptureSelection.CaptureType.Display,
                    DisplayName = captureItem.DisplayName,
                    Width = captureItem.Size.Width,
                    Height = captureItem.Size.Height
                };
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"PickCaptureSourceAsync error: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Create a selection for a custom region
    /// </summary>
    public CaptureSelection CreateRegionSelection(int x, int y, int width, int height)
    {
        return new CaptureSelection
        {
            Type = CaptureSelection.CaptureType.Region,
            DisplayName = $"Region ({width}x{height})",
            RegionX = x,
            RegionY = y,
            RegionWidth = width,
            RegionHeight = height,
            Width = width,
            Height = height
        };
    }

    private static IntPtr TryGetWindowHandle(GraphicsCaptureItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.DisplayName))
            return IntPtr.Zero;

        var displayName = item.DisplayName;
        
        // Check if this looks like a display (usually contains "Display" or monitor info)
        if (displayName.Contains("Display", StringComparison.OrdinalIgnoreCase) ||
            displayName.Contains("Monitor", StringComparison.OrdinalIgnoreCase) ||
            displayName.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        // Try exact match first
        var hwnd = FindWindow(null, displayName);
        if (hwnd != IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine($"Found window by exact title: {displayName}");
            return hwnd;
        }

        // Try partial match by enumerating windows
        IntPtr foundHwnd = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true; // Continue enumeration
            
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            var windowTitle = sb.ToString();
            
            // Check if the window title contains our search term or vice versa
            if (!string.IsNullOrEmpty(windowTitle) &&
                (windowTitle.Contains(displayName, StringComparison.OrdinalIgnoreCase) ||
                 displayName.Contains(windowTitle, StringComparison.OrdinalIgnoreCase)))
            {
                foundHwnd = hWnd;
                System.Diagnostics.Debug.WriteLine($"Found window by partial match: '{windowTitle}' ~ '{displayName}'");
                return false; // Stop enumeration
            }
            
            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundHwnd;
    }

    /// <summary>
    /// Bring a window to the foreground
    /// </summary>
    public static void BringWindowToFront(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        
        try
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            System.Diagnostics.Debug.WriteLine($"Brought window to front: {hwnd}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BringWindowToFront error: {ex}");
        }
    }

    private static void InitializeWithWindow(object obj, IntPtr hwnd)
    {
        var guid = new Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1");
        IntPtr pUnknown = Marshal.GetIUnknownForObject(obj);
        
        try
        {
            int hr = Marshal.QueryInterface(pUnknown, ref guid, out IntPtr pInit);
            
            if (hr == 0 && pInit != IntPtr.Zero)
            {
                try
                {
                    var vtable = Marshal.ReadIntPtr(pInit);
                    var initializePtr = Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size);
                    var initialize = Marshal.GetDelegateForFunctionPointer<InitializeDelegate>(initializePtr);
                    initialize(pInit, hwnd);
                }
                finally
                {
                    Marshal.Release(pInit);
                }
            }
        }
        finally
        {
            Marshal.Release(pUnknown);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InitializeDelegate(IntPtr pThis, IntPtr hwnd);
}
