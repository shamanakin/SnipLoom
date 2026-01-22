namespace SnipLoom.Models;

public enum RecordingStatus
{
    Idle,
    Recording,
    Paused,
    Stopping
}

public class RecordingState
{
    private RecordingStatus _status = RecordingStatus.Idle;
    private readonly object _lock = new();

    public RecordingStatus Status
    {
        get { lock (_lock) return _status; }
        private set { lock (_lock) _status = value; }
    }

    public bool IsRecording => Status == RecordingStatus.Recording;
    public bool IsPaused => Status == RecordingStatus.Paused;
    public bool IsIdle => Status == RecordingStatus.Idle;

    public void Start()
    {
        Status = RecordingStatus.Recording;
    }

    public void Pause()
    {
        if (Status == RecordingStatus.Recording)
            Status = RecordingStatus.Paused;
    }

    public void Resume()
    {
        if (Status == RecordingStatus.Paused)
            Status = RecordingStatus.Recording;
    }

    public void Stop()
    {
        Status = RecordingStatus.Stopping;
    }

    public void Reset()
    {
        Status = RecordingStatus.Idle;
    }
}
