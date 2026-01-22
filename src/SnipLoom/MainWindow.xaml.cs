using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SnipLoom.Services;
using SnipLoom.Views;

namespace SnipLoom;

public partial class MainWindow : Window
{
    private EncoderService? _encoderService;
    private DispatcherTimer? _timer;
    private DateTime _recordingStartTime;
    private string? _tempFilePath;
    private string _recordingDescription = "display";
    private RecordingFrameWindow? _recordingFrame;
    private string? _lastSavedFilePath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Disable start button immediately
            StartButton.IsEnabled = false;
            StatusText.Text = "Starting...";
            
            // Force UI update
            await Task.Yield();

            // Create temp file path for recording
            var tempDir = Path.Combine(Path.GetTempPath(), "SnipLoom");
            Directory.CreateDirectory(tempDir);
            _tempFilePath = Path.Combine(tempDir, $"recording-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.mp4");

            // Initialize encoder based on selected mode
            _encoderService = new EncoderService();
            _encoderService.RecordingCompleted += OnRecordingCompleted;
            _encoderService.RecordingFailed += OnRecordingFailed;

            bool initialized = false;

            if (ModeDisplay.IsChecked == true)
            {
                initialized = await InitializeDisplayMode();
            }
            else if (ModeWindow.IsChecked == true)
            {
                initialized = await InitializeWindowMode();
            }
            else if (ModeRegion.IsChecked == true)
            {
                initialized = await InitializeRegionMode();
            }

            if (!initialized)
            {
                StatusText.Text = "Ready to record";
                CleanupEncoder();
                StartButton.IsEnabled = true;
                return;
            }

            // Start recording
            _encoderService.StartRecording();
            
            // Brief delay to check if recording started
            await Task.Delay(600);
            
            // Check if still recording
            if (_encoderService == null || !_encoderService.IsRecording)
            {
                StatusText.Text = "Failed to start recording";
                CleanupEncoder();
                StartButton.IsEnabled = true;
                return;
            }
            
            _recordingStartTime = DateTime.Now;
            
            // Start UI timer
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Update UI - CRITICAL: swap button visibility
            StatusText.Text = $"Recording {_recordingDescription}...";
            StartButton.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            StopButton.IsEnabled = true;
            ModeDisplay.IsEnabled = false;
            ModeWindow.IsEnabled = false;
            ModeRegion.IsEnabled = false;

            // Force layout refresh so the button actually appears immediately.
            StopButton.UpdateLayout();
            this.UpdateLayout();
            
            Debug.WriteLine("UI updated: Stop button should now be visible");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            CleanupEncoder();
            StartButton.IsEnabled = true;
            StartButton.Visibility = Visibility.Visible;
            StopButton.Visibility = Visibility.Collapsed;
            Debug.WriteLine($"Start error: {ex}");
        }
    }

    private async Task<bool> InitializeDisplayMode()
    {
        // Show display picker
        var picker = new DisplaySelectWindow { Owner = this };
        
        var result = picker.ShowDialog();
        ResetCursor();
        
        if (result == true && !string.IsNullOrEmpty(picker.SelectedDeviceName))
        {
            _recordingDescription = $"display ({picker.SelectedDeviceName})";
            _encoderService!.InitializeForDisplay(_tempFilePath!, picker.SelectedDeviceName);
            return _encoderService.IsInitialized;
        }
        
        return false;
    }

    private async Task<bool> InitializeWindowMode()
    {
        // Show window picker
        var picker = new WindowSelectWindow { Owner = this };
        
        // Hide main window briefly so it doesn't appear in the list
        this.Hide();
        await Task.Delay(100);
        
        var result = picker.ShowDialog();
        
        this.Show();
        this.Activate();
        ResetCursor();
        
        if (result == true && picker.SelectedHandle != IntPtr.Zero)
        {
            _recordingDescription = $"window ({picker.SelectedTitle ?? "Window"})";
            _encoderService!.InitializeForWindow(_tempFilePath!, picker.SelectedHandle);
            return _encoderService.IsInitialized;
        }
        
        return false;
    }

    private async Task<bool> InitializeRegionMode()
    {
        // Show region selection window
        var regionWindow = new RegionSelectWindow();
        
        // Hide main window so it doesn't interfere with region selection
        this.Hide();
        await Task.Delay(100);
        
        regionWindow.ShowDialog();
        
        this.Show();
        this.Activate();
        ResetCursor();
        
        if (regionWindow.Result != null)
        {
            var r = regionWindow.Result;
            _recordingDescription = $"region ({r.RegionWidth}x{r.RegionHeight})";
            _encoderService!.InitializeForRegion(_tempFilePath!, r.RegionX, r.RegionY, r.RegionWidth, r.RegionHeight);
            
            if (_encoderService.IsInitialized)
            {
                // Show recording frame overlay using DIP coordinates
                _recordingFrame = new RecordingFrameWindow();
                _recordingFrame.SetRegionDips(r.RegionDipX, r.RegionDipY, r.RegionDipWidth, r.RegionDipHeight);
                _recordingFrame.Show();
                _recordingFrame.StartFlashAnimation();
            }
            
            return _encoderService.IsInitialized;
        }
        
        return false;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _recordingStartTime;
        TimerText.Text = elapsed.ToString(@"hh\:mm\:ss");
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("Stop button clicked");
        
        try
        {
            StatusText.Text = "Stopping...";
            StopButton.IsEnabled = false;

            // Stop timer
            _timer?.Stop();
            _timer = null;
            
            if (_encoderService != null)
            {
                _encoderService.StopRecording();
                
                // Wait for recording to complete
                StatusText.Text = "Finalizing...";
                var completed = await _encoderService.WaitForRecordingComplete(10000);
                Debug.WriteLine($"Recording complete: {completed}");
                
                _tempFilePath = _encoderService.GetActualOutputPath();
                CleanupEncoder();
            }

            // Reset UI first
            ResetUI();
            
            // Then show save dialog
            ShowSaveDialog();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            CleanupEncoder();
            ResetUI();
            Debug.WriteLine($"Stop error: {ex}");
        }
    }

    private void OnRecordingCompleted(object? sender, string filePath)
    {
        Debug.WriteLine($"Recording completed event: {filePath}");
    }

    private void OnRecordingFailed(object? sender, string error)
    {
        Debug.WriteLine($"Recording failed event: {error}");
        
        Dispatcher.BeginInvoke(() =>
        {
            StatusText.Text = $"Recording failed: {error}";
            
            // Stop timer if running
            _timer?.Stop();
            _timer = null;
            
            CleanupEncoder();
            ResetUI();
        });
    }

    private void CleanupEncoder()
    {
        // Close recording frame overlay if open
        if (_recordingFrame != null)
        {
            try { _recordingFrame.Close(); } catch { }
            _recordingFrame = null;
        }
        
        if (_encoderService != null)
        {
            try
            {
                _encoderService.RecordingCompleted -= OnRecordingCompleted;
                _encoderService.RecordingFailed -= OnRecordingFailed;
                _encoderService.Dispose();
            }
            catch { }
            _encoderService = null;
        }
    }

    private void ResetUI()
    {
        TimerText.Text = "00:00:00";
        StartButton.IsEnabled = true;
        StartButton.Visibility = Visibility.Visible;
        StopButton.IsEnabled = true;
        StopButton.Visibility = Visibility.Collapsed;
        ModeDisplay.IsEnabled = true;
        ModeWindow.IsEnabled = true;
        ModeRegion.IsEnabled = true;
        ResetCursor();
    }

    /// <summary>
    /// Reset cursor to normal state after picker/selection windows close.
    /// </summary>
    private void ResetCursor()
    {
        Mouse.OverrideCursor = null;
        this.Cursor = Cursors.Arrow;
        this.ForceCursor = false;
    }

    private void ShowSaveDialog()
    {
        if (string.IsNullOrEmpty(_tempFilePath))
        {
            StatusText.Text = "No recording to save";
            return;
        }
        
        if (!File.Exists(_tempFilePath))
        {
            StatusText.Text = "Recording file not found";
            return;
        }

        var fileInfo = new FileInfo(_tempFilePath);
        Debug.WriteLine($"Recording file: {_tempFilePath}, size: {fileInfo.Length} bytes");

        var saveDialog = new SaveFileDialog
        {
            Filter = "MP4 Video|*.mp4|All Files|*.*",
            FileName = $"Recording {DateTime.Now:yyyy-MM-dd HH-mm-ss}.mp4",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                File.Move(_tempFilePath, saveDialog.FileName, overwrite: true);
                StatusText.Text = $"Saved: {Path.GetFileName(saveDialog.FileName)}";
                
                // Update last saved file info
                _lastSavedFilePath = saveDialog.FileName;
                ShowLastSavedFile(saveDialog.FileName);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Save failed: {ex.Message}";
            }
        }
        else
        {
            StatusText.Text = "Recording discarded";
            try { File.Delete(_tempFilePath); } catch { }
        }
        
        _tempFilePath = null;
    }

    private void ShowLastSavedFile(string filePath)
    {
        LastFileName.Text = Path.GetFileName(filePath);
        LastFilePath.Text = Path.GetDirectoryName(filePath);
        EmptyState.Visibility = Visibility.Collapsed;
        FileInfoState.Visibility = Visibility.Visible;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSavedFilePath) && File.Exists(_lastSavedFilePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _lastSavedFilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Could not open file: {ex.Message}";
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastSavedFilePath))
        {
            var folder = Path.GetDirectoryName(_lastSavedFilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                try
                {
                    // Open folder and select the file
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{_lastSavedFilePath}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Could not open folder: {ex.Message}";
                }
            }
        }
    }
}
