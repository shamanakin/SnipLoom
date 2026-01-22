using Windows.Graphics;

namespace SnipLoom.Models;

public class CaptureRegion
{
    /// <summary>
    /// The screen coordinates of the capture region (in physical pixels)
    /// </summary>
    public RectInt32 ScreenRect { get; set; }

    /// <summary>
    /// The output dimensions for the encoded video
    /// </summary>
    public SizeInt32 OutputSize { get; set; }

    /// <summary>
    /// Whether to capture the entire monitor (no cropping needed)
    /// </summary>
    public bool IsFullScreen { get; set; } = true;

    /// <summary>
    /// The monitor/display index this region is on
    /// </summary>
    public int MonitorIndex { get; set; } = 0;

    public static CaptureRegion FullScreen(int width, int height)
    {
        return new CaptureRegion
        {
            ScreenRect = new RectInt32(0, 0, width, height),
            OutputSize = new SizeInt32(width, height),
            IsFullScreen = true
        };
    }

    public static CaptureRegion FromRect(int x, int y, int width, int height)
    {
        return new CaptureRegion
        {
            ScreenRect = new RectInt32(x, y, width, height),
            OutputSize = new SizeInt32(width, height),
            IsFullScreen = false
        };
    }
}
