using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SnipLoom.Services;

public class AudioDataEventArgs : EventArgs
{
    public byte[] Buffer { get; }
    public TimeSpan Timestamp { get; }

    public AudioDataEventArgs(byte[] buffer, TimeSpan timestamp)
    {
        Buffer = buffer;
        Timestamp = timestamp;
    }
}

public class AudioService : IDisposable
{
    private WasapiLoopbackCapture? _loopbackCapture;
    private DateTime _startTime;
    private bool _isCapturing;

    public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;
    
    public WaveFormat? WaveFormat => _loopbackCapture?.WaveFormat;
    public bool IsCapturing => _isCapturing;

    public void StartCapture()
    {
        try
        {
            // Create loopback capture for system audio
            _loopbackCapture = new WasapiLoopbackCapture();
            
            _loopbackCapture.DataAvailable += OnDataAvailable;
            _loopbackCapture.RecordingStopped += OnRecordingStopped;

            _startTime = DateTime.Now;
            _isCapturing = true;
            _loopbackCapture.StartRecording();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AudioService.StartCapture error: {ex}");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isCapturing || e.BytesRecorded == 0) return;

        var timestamp = DateTime.Now - _startTime;
        var buffer = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, buffer, e.BytesRecorded);

        AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(buffer, timestamp));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            System.Diagnostics.Debug.WriteLine($"Recording stopped with error: {e.Exception}");
        }
    }

    public void StopCapture()
    {
        _isCapturing = false;

        if (_loopbackCapture != null)
        {
            _loopbackCapture.DataAvailable -= OnDataAvailable;
            _loopbackCapture.RecordingStopped -= OnRecordingStopped;
            _loopbackCapture.StopRecording();
            _loopbackCapture.Dispose();
            _loopbackCapture = null;
        }
    }

    public void Dispose()
    {
        StopCapture();
    }
}
