using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SnipLoom.Services;

namespace SnipLoom.Views;

public partial class RegionSelectWindow : Window
{
    private enum DragMode { None, Creating, Moving, ResizingNW, ResizingN, ResizingNE, ResizingW, ResizingE, ResizingSW, ResizingS, ResizingSE }
    
    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private Rect _selection;
    private Rect _originalSelection;
    private double _aspectRatio = 16.0 / 9.0; // Default to 16:9
    private bool _hasSelection = false;

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
        // Cover the primary screen
        var screen = System.Windows.SystemParameters.WorkArea;
        Left = 0;
        Top = 0;
        Width = System.Windows.SystemParameters.PrimaryScreenWidth;
        Height = System.Windows.SystemParameters.PrimaryScreenHeight;
        
        UpdateDimOverlay();
        
        // Focus the window to receive keyboard events
        Focus();
        Activate();
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
        
        // Get DPI scale to convert DIPs to physical pixels
        double dpiScale = GetDpiScale();
        
        // Physical pixel coordinates for ScreenRecorderLib
        int pixelX = (int)(_selection.X * dpiScale);
        int pixelY = (int)(_selection.Y * dpiScale);
        int pixelWidth = (int)(_selection.Width * dpiScale);
        int pixelHeight = (int)(_selection.Height * dpiScale);
        
        Result = new CaptureSelection
        {
            Type = CaptureSelection.CaptureType.Region,
            DisplayName = $"Region ({pixelWidth}x{pixelHeight})",
            // Physical pixels for ScreenRecorderLib
            RegionX = pixelX,
            RegionY = pixelY,
            RegionWidth = pixelWidth,
            RegionHeight = pixelHeight,
            Width = pixelWidth,
            Height = pixelHeight,
            // DIPs for recording frame overlay
            RegionDipX = _selection.X,
            RegionDipY = _selection.Y,
            RegionDipWidth = _selection.Width,
            RegionDipHeight = _selection.Height
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
