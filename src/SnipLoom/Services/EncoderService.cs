using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ScreenRecorderLib;

namespace SnipLoom.Services;

/// <summary>
/// Thin wrapper around ScreenRecorderLib.
/// Supports display, window, and region capture modes.
/// </summary>
public sealed class EncoderService : IDisposable
{
    private Recorder? _recorder;
    private string? _outputPath;
    private bool _isInitialized;
    private bool _isRecording;
    private TaskCompletionSource<bool>? _recordingComplete;

    public bool IsInitialized => _isInitialized;
    public bool IsRecording => _isRecording;
    
    // Audio settings
    public bool CaptureSystemAudio { get; set; } = true;
    public bool CaptureMicrophone { get; set; } = false;

    public event EventHandler<string>? RecordingCompleted;
    public event EventHandler<string>? RecordingFailed;

    /// <summary>
    /// Creates audio options for the recorder based on current settings
    /// </summary>
    private AudioOptions CreateAudioOptions()
    {
        return new AudioOptions
        {
            IsAudioEnabled = CaptureSystemAudio || CaptureMicrophone,
            IsOutputDeviceEnabled = CaptureSystemAudio,
            IsInputDeviceEnabled = CaptureMicrophone
        };
    }

    /// <summary>
    /// Initialize for full display capture (primary display, default settings)
    /// </summary>
    public void InitializeForDisplay(string outputPath)
    {
        _outputPath = outputPath;
        
        var options = new RecorderOptions
        {
            AudioOptions = CreateAudioOptions()
        };
        
        _recorder = Recorder.CreateRecorder(options);
        WireEvents();
        _isInitialized = true;
        Debug.WriteLine($"EncoderService initialized for default display: {_outputPath}");
    }

    /// <summary>
    /// Initialize for a specific display by device name
    /// </summary>
    public void InitializeForDisplay(string outputPath, string deviceName)
    {
        _outputPath = outputPath;
        
        try
        {
            var sources = new List<RecordingSourceBase>
            {
                new DisplayRecordingSource(deviceName)
            };
            
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = sources
                },
                AudioOptions = CreateAudioOptions()
            };
            
            _recorder = Recorder.CreateRecorder(options);
            WireEvents();
            _isInitialized = true;
            Debug.WriteLine($"EncoderService initialized for display '{deviceName}': {_outputPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to init for display '{deviceName}', falling back to default: {ex}");
            // Fallback to default display with audio
            var options = new RecorderOptions { AudioOptions = CreateAudioOptions() };
            _recorder = Recorder.CreateRecorder(options);
            WireEvents();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Initialize for window capture by handle
    /// </summary>
    public void InitializeForWindow(string outputPath, IntPtr windowHandle)
    {
        _outputPath = outputPath;
        
        try
        {
            var sources = new List<RecordingSourceBase>
            {
                new WindowRecordingSource(windowHandle)
            };
            
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = sources
                },
                AudioOptions = CreateAudioOptions()
            };
            
            _recorder = Recorder.CreateRecorder(options);
            WireEvents();
            _isInitialized = true;
            Debug.WriteLine($"EncoderService initialized for window handle {windowHandle}: {_outputPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to init for window handle {windowHandle}: {ex}");
            _isInitialized = false;
            RecordingFailed?.Invoke(this, $"Failed to initialize window capture: {ex.Message}");
        }
    }

    /// <summary>
    /// Initialize for region capture (crops a display to the specified rect)
    /// </summary>
    /// <param name="outputPath">Output file path</param>
    /// <param name="x">X coordinate relative to the target display (in physical pixels)</param>
    /// <param name="y">Y coordinate relative to the target display (in physical pixels)</param>
    /// <param name="width">Width in physical pixels</param>
    /// <param name="height">Height in physical pixels</param>
    /// <param name="displayDeviceName">Target display device name (e.g., \\.\DISPLAY1), or null for main monitor</param>
    public void InitializeForRegion(string outputPath, int x, int y, int width, int height, string? displayDeviceName = null)
    {
        _outputPath = outputPath;
        
        try
        {
            // Create a display source with the region to capture
            DisplayRecordingSource displaySource;
            
            if (!string.IsNullOrEmpty(displayDeviceName))
            {
                displaySource = new DisplayRecordingSource(displayDeviceName)
                {
                    SourceRect = new ScreenRect(x, y, width, height)
                };
            }
            else
            {
                displaySource = new DisplayRecordingSource(DisplayRecordingSource.MainMonitor)
                {
                    SourceRect = new ScreenRect(x, y, width, height)
                };
            }
            
            var sources = new List<RecordingSourceBase> { displaySource };
            
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = sources
                },
                AudioOptions = CreateAudioOptions()
            };
            
            _recorder = Recorder.CreateRecorder(options);
            WireEvents();
            _isInitialized = true;
            Debug.WriteLine($"EncoderService initialized for region ({x},{y},{width},{height}) on {displayDeviceName ?? "MainMonitor"}: {_outputPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to init for region: {ex}");
            _isInitialized = false;
            RecordingFailed?.Invoke(this, $"Failed to initialize region capture: {ex.Message}");
        }
    }

    private void WireEvents()
    {
        if (_recorder != null)
        {
            _recorder.OnRecordingComplete += OnRecordingComplete;
            _recorder.OnRecordingFailed += OnRecordingFailed;
            _recorder.OnStatusChanged += OnStatusChanged;
        }
    }

    public void StartRecording()
    {
        if (!_isInitialized || _recorder == null || string.IsNullOrWhiteSpace(_outputPath))
        {
            RecordingFailed?.Invoke(this, "Encoder not initialized");
            return;
        }

        _recordingComplete = new TaskCompletionSource<bool>();

        try
        {
            // Record() can do heavy setup; keep UI responsive.
            Task.Run(() =>
            {
                try
                {
                    _recorder.Record(_outputPath);
                    _isRecording = true;
                    Debug.WriteLine("Recorder.Record() returned; recording started");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Record failed: {ex}");
                    _isRecording = false;
                    _recordingComplete?.TrySetResult(false);
                    RecordingFailed?.Invoke(this, ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StartRecording exception: {ex}");
            _isRecording = false;
            _recordingComplete?.TrySetResult(false);
            RecordingFailed?.Invoke(this, ex.Message);
        }
    }

    public void StopRecording()
    {
        if (_recorder == null)
        {
            _recordingComplete?.TrySetResult(false);
            return;
        }

        try
        {
            _recorder.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"StopRecording exception: {ex}");
            _recordingComplete?.TrySetResult(false);
        }
        finally
        {
            _isRecording = false;
        }
    }

    public async Task<bool> WaitForRecordingComplete(int timeoutMs = 30000)
    {
        if (_recordingComplete == null) return false;

        var timeoutTask = Task.Delay(timeoutMs);
        var completedTask = await Task.WhenAny(_recordingComplete.Task, timeoutTask);
        return completedTask == _recordingComplete.Task && _recordingComplete.Task.Result;
    }

    public string? GetActualOutputPath() => _outputPath;

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        Debug.WriteLine($"Recording complete: {e.FilePath}");
        _isRecording = false;
        _recordingComplete?.TrySetResult(true);
        RecordingCompleted?.Invoke(this, e.FilePath);
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        Debug.WriteLine($"Recording failed: {e.Error}");
        _isRecording = false;
        _recordingComplete?.TrySetResult(false);
        RecordingFailed?.Invoke(this, e.Error);
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        Debug.WriteLine($"Recording status: {e.Status}");
    }

    public void Dispose()
    {
        try
        {
            if (_recorder != null)
            {
                _recorder.OnRecordingComplete -= OnRecordingComplete;
                _recorder.OnRecordingFailed -= OnRecordingFailed;
                _recorder.OnStatusChanged -= OnStatusChanged;
                _recorder.Dispose();
            }
        }
        catch { }
        finally
        {
            _recorder = null;
            _isInitialized = false;
            _isRecording = false;
        }
    }
}
