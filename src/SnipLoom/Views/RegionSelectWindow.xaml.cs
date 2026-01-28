using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenRecorderLib;
using SnipLoom.Services;

namespace SnipLoom.Views;

/// <summary>
/// Represents display information for multi-monitor support
/// </summary>
public class DisplayInfo
{
    public string DeviceName { get; set; } = "";
    public Rect Bounds { get; set; } // Physical pixel bounds in virtual screen space
    public Rect DipBounds { get; set; } // DIP bounds in virtual screen space
    public double DpiScale { get; set; } = 1.0;
}

public partial class RegionSelectWindow : Window
{
    // P/Invoke for monitor enumeration
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
    
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
    
    private const uint MONITORINFOF_PRIMARY = 1;

    private enum DragMode { None, Creating, Moving, ResizingNW, ResizingN, ResizingNE, ResizingW, ResizingE, ResizingSW, ResizingS, ResizingSE }
    
    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private Rect _selection;
    private Rect _originalSelection;
    private double _aspectRatio = 16.0 / 9.0; // Default to 16:9
    private bool _hasSelection = false;
    
    // Multi-monitor support
    private List<DisplayInfo> _displays = new();
    private double _virtualScreenLeft;
    private double _virtualScreenTop;

    public CaptureSelection? Result { get; private set; }

    public RegionSelectWindow()
    {
        InitializeComponent();
        
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += OnKeyDown;
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load display information for multi-monitor support
        LoadDisplayInfo();
        
        // Cover ALL monitors using virtual screen coordinates
        _virtualScreenLeft = SystemParameters.VirtualScreenLeft;
        _virtualScreenTop = SystemParameters.VirtualScreenTop;
        
        Left = _virtualScreenLeft;
        Top = _virtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        
        Debug.WriteLine($"RegionSelectWindow spanning virtual screen: ({Left},{Top}) {Width}x{Height}");
        Debug.WriteLine($"Found {_displays.Count} display(s)");
        
        UpdateDimOverlay();
        
        // Focus the window to receive keyboard events
        Focus();
        Activate();
    }
    
    // Temporary storage for monitor enumeration
    private static List<(string device, Rect bounds, bool isPrimary)>? _tempMonitorInfos;
    
    private static bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
    {
        var mi = new MONITORINFOEX();
        mi.cbSize = Marshal.SizeOf<MONITORINFOEX>();
        
        if (GetMonitorInfo(hMonitor, ref mi))
        {
            var bounds = new Rect(
                mi.rcMonitor.Left,
                mi.rcMonitor.Top,
                mi.rcMonitor.Right - mi.rcMonitor.Left,
                mi.rcMonitor.Bottom - mi.rcMonitor.Top);
            
            bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
            _tempMonitorInfos?.Add((mi.szDevice, bounds, isPrimary));
        }
        return true; // Continue enumeration
    }
    
    /// <summary>
    /// Load display information using Win32 API for accurate bounds
    /// </summary>
    private void LoadDisplayInfo()
    {
        _displays.Clear();
        
        try
        {
            double dpiScale = GetDpiScale();
            _tempMonitorInfos = new List<(string device, Rect bounds, bool isPrimary)>();
            
            // Enumerate all monitors using Win32 API
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
            
            // Sort so primary is first
            var sortedMonitors = _tempMonitorInfos.OrderByDescending(m => m.isPrimary).ToList();
            _tempMonitorInfos = null;
            
            foreach (var (device, bounds, isPrimary) in sortedMonitors)
            {
                var info = new DisplayInfo
                {
                    DeviceName = device,
                    Bounds = bounds,
                    DpiScale = dpiScale,
                    // Calculate DIP bounds (WPF coordinates)
                    DipBounds = new Rect(
                        bounds.X / dpiScale,
                        bounds.Y / dpiScale,
                        bounds.Width / dpiScale,
                        bounds.Height / dpiScale)
                };
                
                _displays.Add(info);
                Debug.WriteLine($"Display '{info.DeviceName}': Bounds={info.Bounds}, DipBounds={info.DipBounds}, Primary={isPrimary}");
            }
            
            if (_displays.Count == 0)
            {
                throw new InvalidOperationException("No monitors found");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load display info: {ex}");
            _tempMonitorInfos = null;
            
            // Fallback: create a single display entry for primary screen
            double dpiScale = GetDpiScale();
            _displays.Add(new DisplayInfo
            {
                DeviceName = "", // Empty means main/primary monitor
                Bounds = new Rect(0, 0, 
                    SystemParameters.PrimaryScreenWidth * dpiScale, 
                    SystemParameters.PrimaryScreenHeight * dpiScale),
                DipBounds = new Rect(0, 0, 
                    SystemParameters.PrimaryScreenWidth, 
                    SystemParameters.PrimaryScreenHeight),
                DpiScale = dpiScale
            });
        }
    }
    
    /// <summary>
    /// Find which display contains the center of the selection
    /// </summary>
    private DisplayInfo? GetDisplayForSelection()
    {
        if (_displays.Count == 0) return null;
        if (_displays.Count == 1) return _displays[0];
        
        // Convert selection from window-relative to virtual screen DIPs
        var selectionInVirtualScreen = new Rect(
            _selection.X + _virtualScreenLeft,
            _selection.Y + _virtualScreenTop,
            _selection.Width,
            _selection.Height);
        
        // Get the center of the selection in virtual screen DIP coordinates
        var center = new Point(
            selectionInVirtualScreen.X + selectionInVirtualScreen.Width / 2,
            selectionInVirtualScreen.Y + selectionInVirtualScreen.Height / 2);
        
        // Find the display that contains this point
        foreach (var display in _displays)
        {
            if (display.DipBounds.Contains(center))
            {
                return display;
            }
        }
        
        // Fallback: find display with most overlap
        DisplayInfo? bestMatch = null;
        double bestOverlap = 0;
        
        foreach (var display in _displays)
        {
            var overlap = Rect.Intersect(selectionInVirtualScreen, display.DipBounds);
            if (!overlap.IsEmpty)
            {
                double area = overlap.Width * overlap.Height;
                if (area > bestOverlap)
                {
                    bestOverlap = area;
                    bestMatch = display;
                }
            }
        }
        
        return bestMatch ?? _displays[0];
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = null;
            Close();
        }
        else if (e.Key == Key.Enter && _hasSelection)
        {
            ConfirmSelection();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        var pos = e.GetPosition(this);
        _dragStart = pos;
        
        // Check if clicking on existing selection for move/resize
        if (_hasSelection)
        {
            var hitMode = GetHitMode(pos);
            if (hitMode != DragMode.None)
            {
                _dragMode = hitMode;
                _originalSelection = _selection;
                CaptureMouse();
                return;
            }
        }
        
        // Start new selection
        _dragMode = DragMode.Creating;
        _selection = new Rect(pos, new Size(0, 0));
        _hasSelection = false;
        InstructionsOverlay.Visibility = Visibility.Collapsed;
        SelectionBorder.Visibility = Visibility.Visible;
        SizeIndicator.Visibility = Visibility.Visible;
        BtnConfirm.Visibility = Visibility.Collapsed;
        HideHandles();
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        
        if (_dragMode == DragMode.None)
        {
            // Update cursor based on position
            if (_hasSelection)
            {
                var hitMode = GetHitMode(pos);
                Cursor = GetCursorForMode(hitMode);
            }
            return;
        }
        
        switch (_dragMode)
        {
            case DragMode.Creating:
                _selection = CreateRectWithAspectRatio(_dragStart, pos, _aspectRatio);
                break;
            case DragMode.Moving:
                var delta = pos - _dragStart;
                _selection = new Rect(
                    _originalSelection.X + delta.X,
                    _originalSelection.Y + delta.Y,
                    _originalSelection.Width,
                    _originalSelection.Height);
                ClampSelectionToScreen();
                break;
            default:
                ResizeSelection(pos);
                // Apply aspect ratio after resize
                ApplyAspectRatio();
                break;
        }
        
        UpdateVisuals();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None) return;
        
        ReleaseMouseCapture();
        
        if (_dragMode == DragMode.Creating && _selection.Width > 20 && _selection.Height > 20)
        {
            _hasSelection = true;
            ShowHandles();
            BtnConfirm.Visibility = Visibility.Visible;
            InstructionsOverlay.Visibility = Visibility.Collapsed;
        }
        
        _dragMode = DragMode.None;
        UpdateVisuals();
    }

    private DragMode GetHitMode(Point pos)
    {
        if (!_hasSelection) return DragMode.None;
        
        const double handleSize = 15;
        
        bool nearLeft = Math.Abs(pos.X - _selection.Left) < handleSize;
        bool nearRight = Math.Abs(pos.X - _selection.Right) < handleSize;
        bool nearTop = Math.Abs(pos.Y - _selection.Top) < handleSize;
        bool nearBottom = Math.Abs(pos.Y - _selection.Bottom) < handleSize;
        bool insideX = pos.X > _selection.Left && pos.X < _selection.Right;
        bool insideY = pos.Y > _selection.Top && pos.Y < _selection.Bottom;
        
        if (nearLeft && nearTop) return DragMode.ResizingNW;
        if (nearRight && nearTop) return DragMode.ResizingNE;
        if (nearLeft && nearBottom) return DragMode.ResizingSW;
        if (nearRight && nearBottom) return DragMode.ResizingSE;
        if (nearTop && insideX) return DragMode.ResizingN;
        if (nearBottom && insideX) return DragMode.ResizingS;
        if (nearLeft && insideY) return DragMode.ResizingW;
        if (nearRight && insideY) return DragMode.ResizingE;
        if (insideX && insideY) return DragMode.Moving;
        
        return DragMode.None;
    }

    private Cursor GetCursorForMode(DragMode mode) => mode switch
    {
        DragMode.Moving => Cursors.SizeAll,
        DragMode.ResizingNW or DragMode.ResizingSE => Cursors.SizeNWSE,
        DragMode.ResizingNE or DragMode.ResizingSW => Cursors.SizeNESW,
        DragMode.ResizingN or DragMode.ResizingS => Cursors.SizeNS,
        DragMode.ResizingW or DragMode.ResizingE => Cursors.SizeWE,
        _ => Cursors.Cross
    };

    private void ResizeSelection(Point pos)
    {
        var delta = pos - _dragStart;
        double left = _originalSelection.Left;
        double top = _originalSelection.Top;
        double right = _originalSelection.Right;
        double bottom = _originalSelection.Bottom;
        
        switch (_dragMode)
        {
            case DragMode.ResizingNW:
                left += delta.X;
                top += delta.Y;
                break;
            case DragMode.ResizingN:
                top += delta.Y;
                break;
            case DragMode.ResizingNE:
                right += delta.X;
                top += delta.Y;
                break;
            case DragMode.ResizingW:
                left += delta.X;
                break;
            case DragMode.ResizingE:
                right += delta.X;
                break;
            case DragMode.ResizingSW:
                left += delta.X;
                bottom += delta.Y;
                break;
            case DragMode.ResizingS:
                bottom += delta.Y;
                break;
            case DragMode.ResizingSE:
                right += delta.X;
                bottom += delta.Y;
                break;
        }
        
        // Ensure minimum size
        if (right - left < 50) right = left + 50;
        if (bottom - top < 50) bottom = top + 50;
        
        _selection = new Rect(left, top, right - left, bottom - top);
    }

    private void ApplyAspectRatio()
    {
        if (_aspectRatio <= 0) return;
        
        // Adjust height based on width to maintain aspect ratio
        double newHeight = _selection.Width / _aspectRatio;
        _selection = new Rect(_selection.X, _selection.Y, _selection.Width, newHeight);
    }

    private void ClampSelectionToScreen()
    {
        // Clamp to the virtual screen bounds (all monitors)
        double x = Math.Max(0, Math.Min(_selection.X, ActualWidth - _selection.Width));
        double y = Math.Max(0, Math.Min(_selection.Y, ActualHeight - _selection.Height));
        _selection = new Rect(x, y, _selection.Width, _selection.Height);
    }

    private Rect CreateRectFromPoints(Point p1, Point p2)
    {
        return new Rect(
            Math.Min(p1.X, p2.X),
            Math.Min(p1.Y, p2.Y),
            Math.Abs(p2.X - p1.X),
            Math.Abs(p2.Y - p1.Y));
    }

    /// <summary>
    /// Creates a rectangle from two points, constrained to the given aspect ratio.
    /// The rectangle is anchored at p1 and sized based on the drag distance to p2.
    /// </summary>
    private Rect CreateRectWithAspectRatio(Point anchor, Point current, double aspectRatio)
    {
        double dx = current.X - anchor.X;
        double dy = current.Y - anchor.Y;
        
        // Determine the direction of the drag
        bool goingRight = dx >= 0;
        bool goingDown = dy >= 0;
        
        // Calculate dimensions based on the larger drag distance
        double width = Math.Abs(dx);
        double height = Math.Abs(dy);
        
        // Calculate what height would be needed for this width at the aspect ratio
        double neededHeight = width / aspectRatio;
        // Calculate what width would be needed for this height at the aspect ratio
        double neededWidth = height * aspectRatio;
        
        // Use the smaller of the two to stay within the drag bounds
        if (neededHeight <= Math.Max(height, width / aspectRatio))
        {
            // Width determines the size
            height = width / aspectRatio;
        }
        else
        {
            // Height determines the size
            width = height * aspectRatio;
        }
        
        // Ensure minimum size
        if (width < 50)
        {
            width = 50;
            height = width / aspectRatio;
        }
        if (height < 50)
        {
            height = 50;
            width = height * aspectRatio;
        }
        
        // Calculate the top-left corner based on drag direction
        double left = goingRight ? anchor.X : anchor.X - width;
        double top = goingDown ? anchor.Y : anchor.Y - height;
        
        return new Rect(left, top, width, height);
    }

    private void UpdateVisuals()
    {
        // Update selection border
        Canvas.SetLeft(SelectionBorder, _selection.X);
        Canvas.SetTop(SelectionBorder, _selection.Y);
        SelectionBorder.Width = Math.Max(0, _selection.Width);
        SelectionBorder.Height = Math.Max(0, _selection.Height);
        
        // Update dim overlay with cutout
        UpdateDimOverlay();
        
        // Update size indicator
        SizeText.Text = $"{(int)_selection.Width} x {(int)_selection.Height}";
        Canvas.SetLeft(SizeIndicator, _selection.X);
        Canvas.SetTop(SizeIndicator, _selection.Bottom + 8);
        
        // Update resize handles
        if (_hasSelection)
        {
            UpdateHandles();
        }
    }

    private void UpdateDimOverlay()
    {
        var screenRect = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        
        if (_hasSelection || _dragMode == DragMode.Creating)
        {
            var selectionGeom = new RectangleGeometry(_selection);
            DimOverlay.Data = new CombinedGeometry(GeometryCombineMode.Exclude, screenRect, selectionGeom);
        }
        else
        {
            DimOverlay.Data = screenRect;
        }
    }

    private void ShowHandles()
    {
        HandleNW.Visibility = Visibility.Visible;
        HandleN.Visibility = Visibility.Visible;
        HandleNE.Visibility = Visibility.Visible;
        HandleW.Visibility = Visibility.Visible;
        HandleE.Visibility = Visibility.Visible;
        HandleSW.Visibility = Visibility.Visible;
        HandleS.Visibility = Visibility.Visible;
        HandleSE.Visibility = Visibility.Visible;
    }

    private void HideHandles()
    {
        HandleNW.Visibility = Visibility.Collapsed;
        HandleN.Visibility = Visibility.Collapsed;
        HandleNE.Visibility = Visibility.Collapsed;
        HandleW.Visibility = Visibility.Collapsed;
        HandleE.Visibility = Visibility.Collapsed;
        HandleSW.Visibility = Visibility.Collapsed;
        HandleS.Visibility = Visibility.Collapsed;
        HandleSE.Visibility = Visibility.Collapsed;
    }

    private void UpdateHandles()
    {
        if (!_hasSelection) return;
        
        const double size = 10;
        const double half = size / 2;
        
        Canvas.SetLeft(HandleNW, _selection.Left - half);
        Canvas.SetTop(HandleNW, _selection.Top - half);
        
        Canvas.SetLeft(HandleN, _selection.Left + _selection.Width / 2 - half);
        Canvas.SetTop(HandleN, _selection.Top - half);
        
        Canvas.SetLeft(HandleNE, _selection.Right - half);
        Canvas.SetTop(HandleNE, _selection.Top - half);
        
        Canvas.SetLeft(HandleW, _selection.Left - half);
        Canvas.SetTop(HandleW, _selection.Top + _selection.Height / 2 - half);
        
        Canvas.SetLeft(HandleE, _selection.Right - half);
        Canvas.SetTop(HandleE, _selection.Top + _selection.Height / 2 - half);
        
        Canvas.SetLeft(HandleSW, _selection.Left - half);
        Canvas.SetTop(HandleSW, _selection.Bottom - half);
        
        Canvas.SetLeft(HandleS, _selection.Left + _selection.Width / 2 - half);
        Canvas.SetTop(HandleS, _selection.Bottom - half);
        
        Canvas.SetLeft(HandleSE, _selection.Right - half);
        Canvas.SetTop(HandleSE, _selection.Bottom - half);
    }

    private void AspectRatio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ratioStr)
        {
            _aspectRatio = double.Parse(ratioStr, System.Globalization.CultureInfo.InvariantCulture);
            
            // Update button highlighting - reset all to default
            var defaultBg = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
            var selectedBg = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xd4));
            
            Btn16_9.Background = defaultBg;
            Btn9_16.Background = defaultBg;
            Btn1_1.Background = defaultBg;
            Btn5_4.Background = defaultBg;
            Btn4_5.Background = defaultBg;
            btn.Background = selectedBg;
            
            // If we already have a selection, apply the new aspect ratio to it
            if (_hasSelection)
            {
                ApplyAspectRatio();
                UpdateVisuals();
            }
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }

    private void ConfirmSelection()
    {
        if (!_hasSelection || _selection.Width < 20 || _selection.Height < 20)
        {
            return;
        }
        
        // Find which display the selection is on
        var targetDisplay = GetDisplayForSelection();
        double dpiScale = targetDisplay?.DpiScale ?? GetDpiScale();
        string? displayDevice = targetDisplay?.DeviceName;
        
        // _selection is in window-relative DIPs (window starts at 0,0)
        // First convert to virtual screen DIPs by adding window position
        double virtualDipX = _selection.X + _virtualScreenLeft;
        double virtualDipY = _selection.Y + _virtualScreenTop;
        
        // Then convert to physical pixels
        int virtualPixelX = (int)(virtualDipX * dpiScale);
        int virtualPixelY = (int)(virtualDipY * dpiScale);
        int pixelWidth = (int)(_selection.Width * dpiScale);
        int pixelHeight = (int)(_selection.Height * dpiScale);
        
        // Calculate coordinates RELATIVE to the target display
        // ScreenRecorderLib expects coordinates relative to the display origin
        int relativeX = virtualPixelX;
        int relativeY = virtualPixelY;
        
        if (targetDisplay != null)
        {
            relativeX = virtualPixelX - (int)targetDisplay.Bounds.X;
            relativeY = virtualPixelY - (int)targetDisplay.Bounds.Y;
            
            Debug.WriteLine($"Selection on display '{displayDevice}':");
            Debug.WriteLine($"  Window-relative DIPs: ({_selection.X},{_selection.Y})");
            Debug.WriteLine($"  Virtual screen DIPs: ({virtualDipX},{virtualDipY})");
            Debug.WriteLine($"  Virtual screen pixels: ({virtualPixelX},{virtualPixelY})");
            Debug.WriteLine($"  Display origin (pixels): ({targetDisplay.Bounds.X},{targetDisplay.Bounds.Y})");
            Debug.WriteLine($"  Relative coords: ({relativeX},{relativeY})");
        }
        
        Result = new CaptureSelection
        {
            Type = CaptureSelection.CaptureType.Region,
            DisplayName = $"Region ({pixelWidth}x{pixelHeight})",
            // Physical pixels RELATIVE to target display for ScreenRecorderLib
            RegionX = relativeX,
            RegionY = relativeY,
            RegionWidth = pixelWidth,
            RegionHeight = pixelHeight,
            Width = pixelWidth,
            Height = pixelHeight,
            // DIPs in virtual screen coords for recording frame overlay positioning
            // These need to be virtual screen coords (add window offset)
            RegionDipX = virtualDipX,
            RegionDipY = virtualDipY,
            RegionDipWidth = _selection.Width,
            RegionDipHeight = _selection.Height,
            // Target display for multi-monitor support
            TargetDisplayDevice = displayDevice
        };
        
        Close();
    }

    private double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            return source.CompositionTarget.TransformToDevice.M11;
        }
        return 1.0; // Fallback to no scaling
    }

    protected override void OnClosed(EventArgs e)
    {
        // Clear any cursor overrides when window closes
        Mouse.OverrideCursor = null;
        Cursor = Cursors.Arrow;
        base.OnClosed(e);
    }
}
