using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ScreenRecorderLib;
using SnipLoom.Services;

namespace SnipLoom.Views;

/// <summary>
/// Window item for the list
/// </summary>
public class WindowItem
{
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public IntPtr Handle { get; set; }
    public RecordingSourceBase? Source { get; set; }
}

public partial class WindowSelectWindow : Window
{
    private List<WindowItem> _allWindows = new();
    
    /// <summary>
    /// The selected window handle
    /// </summary>
    public IntPtr SelectedHandle { get; private set; }
    
    /// <summary>
    /// The selected window recording source
    /// </summary>
    public WindowRecordingSource? SelectedSource { get; private set; }
    
    /// <summary>
    /// The selected window title
    /// </summary>
    public string? SelectedTitle { get; private set; }

    public WindowSelectWindow()
    {
        InitializeComponent();
        LoadWindows();
    }

    private void LoadWindows()
    {
        try
        {
            var windows = Recorder.GetWindows();
            _allWindows = new List<WindowItem>();
            
            // Get our own window handle to exclude
            var ourHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var mainWindowHandle = Application.Current.MainWindow != null 
                ? new System.Windows.Interop.WindowInteropHelper(Application.Current.MainWindow).Handle 
                : IntPtr.Zero;
            
            foreach (var window in windows)
            {
                // Skip our own windows
                if (window.Handle == ourHandle || window.Handle == mainWindowHandle)
                    continue;
                
                // Skip windows with empty titles
                if (string.IsNullOrWhiteSpace(window.Title))
                    continue;
                
                var item = new WindowItem
                {
                    Title = window.Title ?? "Untitled",
                    ProcessName = GetProcessNameFromHandle(window.Handle),
                    Handle = window.Handle,
                    Source = window
                };
                _allWindows.Add(item);
            }
            
            // Sort by title
            _allWindows = _allWindows.OrderBy(x => x.Title).ToList();
            
            // Apply filter if search box has text
            ApplyFilter();
            
            Debug.WriteLine($"Loaded {_allWindows.Count} windows");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading windows: {ex}");
            MessageBox.Show($"Failed to enumerate windows: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilter()
    {
        var filter = SearchBox?.Text?.Trim() ?? "";
        
        if (string.IsNullOrEmpty(filter))
        {
            WindowList.ItemsSource = _allWindows;
        }
        else
        {
            var filtered = _allWindows.Where(w => 
                w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                w.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            WindowList.ItemsSource = filtered;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private static string GetProcessNameFromHandle(IntPtr handle)
    {
        try
        {
            GetWindowThreadProcessId(handle, out uint processId);
            if (processId > 0)
            {
                var process = System.Diagnostics.Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
        }
        catch
        {
            // Ignore errors getting process name
        }
        return "Unknown";
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private void WindowList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnSelect.IsEnabled = WindowList.SelectedItem != null;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadWindows();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is WindowItem item)
        {
            SelectedHandle = item.Handle;
            SelectedSource = item.Source as WindowRecordingSource;
            SelectedTitle = item.Title;
            
            // Bring the selected window to front before recording
            if (item.Handle != IntPtr.Zero)
            {
                CaptureService.BringWindowToFront(item.Handle);
            }
            
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
