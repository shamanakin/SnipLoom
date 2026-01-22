using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using ScreenRecorderLib;

namespace SnipLoom.Views;

/// <summary>
/// Display item for the list
/// </summary>
public class DisplayItem
{
    public string DeviceName { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public string Details { get; set; } = "";
    public RecordingSourceBase? Source { get; set; }
}

public partial class DisplaySelectWindow : Window
{
    /// <summary>
    /// The selected display device name (e.g., \\.\DISPLAY1)
    /// </summary>
    public string? SelectedDeviceName { get; private set; }
    
    /// <summary>
    /// The selected display recording source
    /// </summary>
    public DisplayRecordingSource? SelectedSource { get; private set; }

    public DisplaySelectWindow()
    {
        InitializeComponent();
        LoadDisplays();
    }

    private void LoadDisplays()
    {
        try
        {
            var displays = Recorder.GetDisplays();
            var items = new List<DisplayItem>();
            int index = 1;
            
            // Get primary screen info
            var primaryScreen = System.Windows.SystemParameters.PrimaryScreenWidth + "x" + 
                                System.Windows.SystemParameters.PrimaryScreenHeight;
            
            foreach (var display in displays)
            {
                var deviceName = display.DeviceName ?? $"Display {index}";
                var friendlyName = string.IsNullOrEmpty(display.FriendlyName) 
                    ? $"Display {index}" 
                    : display.FriendlyName;
                
                // Build details string with index and primary indicator
                var isPrimary = index == 1;
                var details = $"Monitor {index}";
                if (isPrimary)
                {
                    details += " (Primary)";
                }
                
                var item = new DisplayItem
                {
                    DeviceName = deviceName,
                    FriendlyName = friendlyName,
                    Details = details,
                    Source = display
                };
                items.Add(item);
                index++;
            }
            
            if (items.Count == 0)
            {
                items.Add(new DisplayItem
                {
                    DeviceName = "Primary Display",
                    FriendlyName = "Primary Display",
                    Details = "Default monitor",
                    Source = null
                });
            }
            
            DisplayList.ItemsSource = items;
            
            // Select first item by default
            if (items.Count > 0)
            {
                DisplayList.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading displays: {ex}");
            MessageBox.Show($"Failed to enumerate displays: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DisplayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnSelect.IsEnabled = DisplayList.SelectedItem != null;
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (DisplayList.SelectedItem is DisplayItem item)
        {
            SelectedDeviceName = item.DeviceName;
            SelectedSource = item.Source as DisplayRecordingSource;
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
