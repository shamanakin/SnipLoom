using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SnipLoom.Views;

/// <summary>
/// A transparent window that shows a red border around the recording region.
/// Used to indicate the capture area during region recording.
/// The window is excluded from screen capture so it never appears in recordings.
/// </summary>
public partial class RecordingFrameWindow : Window
{
    private DispatcherTimer? _flashTimer;
    private bool _isVisible = true;
    private int _flashCount = 0;
    
    // Frame thickness in DIPs - renders OUTSIDE the capture region
    private const double FrameThickness = 4;

    // P/Invoke for excluding window from capture
    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
    
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    public RecordingFrameWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Exclude this window from all screen capture (Windows 10 2004+)
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // This makes the window invisible to screen capture APIs
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        }
    }

    /// <summary>
    /// Position the frame OUTSIDE the capture region (in WPF DIPs).
    /// The frame renders around the region, not inside it.
    /// </summary>
    public void SetRegionDips(double x, double y, double width, double height)
    {
        // Position window so the inner edge of the frame aligns with the capture region
        // Frame renders OUTSIDE, so we expand outward
        Left = x - FrameThickness;
        Top = y - FrameThickness;
        Width = width + (FrameThickness * 2);
        Height = height + (FrameThickness * 2);
        
        // Update the inner cutout to match the capture region exactly
        UpdateFrameGeometry(width, height);
    }

    private void UpdateFrameGeometry(double innerWidth, double innerHeight)
    {
        // The frame is drawn as 4 rectangles around the edges
        // Top bar
        TopBar.Width = innerWidth + (FrameThickness * 2);
        TopBar.Height = FrameThickness;
        
        // Bottom bar
        BottomBar.Width = innerWidth + (FrameThickness * 2);
        BottomBar.Height = FrameThickness;
        BottomBar.Margin = new Thickness(0, innerHeight + FrameThickness, 0, 0);
        
        // Left bar (between top and bottom)
        LeftBar.Width = FrameThickness;
        LeftBar.Height = innerHeight;
        LeftBar.Margin = new Thickness(0, FrameThickness, 0, 0);
        
        // Right bar (between top and bottom)
        RightBar.Width = FrameThickness;
        RightBar.Height = innerHeight;
        RightBar.Margin = new Thickness(innerWidth + FrameThickness, FrameThickness, 0, 0);
    }

    /// <summary>
    /// Start a brief flashing animation to draw attention to the recording area.
    /// </summary>
    public void StartFlashAnimation()
    {
        _flashCount = 0;
        _flashTimer = new DispatcherTimer();
        _flashTimer.Interval = TimeSpan.FromMilliseconds(200);
        _flashTimer.Tick += FlashTimer_Tick;
        _flashTimer.Start();
    }

    private void FlashTimer_Tick(object? sender, EventArgs e)
    {
        _flashCount++;
        
        if (_flashCount >= 6) // 3 flashes (6 toggles)
        {
            _flashTimer?.Stop();
            _flashTimer = null;
            Opacity = 1.0;
            return;
        }
        
        Opacity = _isVisible ? 0.3 : 1.0;
        _isVisible = !_isVisible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _flashTimer?.Stop();
        _flashTimer = null;
        base.OnClosed(e);
    }
}
