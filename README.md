<div align="center">

# SnipLoom

### Lightweight Windows screen recorder with Snipping Tool-style capture

[![Windows](https://img.shields.io/badge/Windows-10%2B-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

*Record your screen to MP4 with system audio. No bloat, no subscriptions, no nonsense.*

</div>

---

## Download

**[Download Latest Release](https://github.com/YOUR_USERNAME/SnipLoom/releases/latest)** - Portable `.zip` with single `SnipLoom.exe`

Just extract and run. No installation required.

---

## Features

| Mode | Description |
|------|-------------|
| **Display** | Record an entire monitor |
| **Window** | Record a specific application window |
| **Draw Region** | Draw a capture area with fixed aspect ratio (16:9, 9:16, 1:1, 5:4, 4:5) |

### What you get

- **System audio capture** via WASAPI loopback
- **MP4 output** with H.264 video + AAC audio
- **No watermarks, no time limits**
- **Single portable EXE** (~30MB, self-contained)
- **Save prompt** after every recording — choose where to save

---

## How to Use

1. **Select capture mode** — Display, Window, or Draw Region
2. **Click "Start Recording"** — pick your target (display, window, or draw your region)
3. **Click "Stop Recording"** when done
4. **Choose where to save** the MP4 file

That's it. No accounts, no cloud, no telemetry.

---

## Requirements

- Windows 10 version 1903+ (Windows 11 recommended)
- x64 architecture

The portable release includes all dependencies. No .NET installation needed.

---

## Known Limitations

- **Window capture**: Some apps using hardware-accelerated rendering (games, some media players, Battle.net) may not capture correctly. Use Display or Region mode instead.
- **Multi-monitor**: Region capture is limited to the primary monitor.
- **Audio**: Captures system audio only (not microphone).

---

## Build from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

### Build

```powershell
cd SnipLoom
dotnet restore
dotnet build -c Release -p:Platform=x64
```

### Run

```powershell
dotnet run --project src/SnipLoom/SnipLoom.csproj
```

### Publish Portable EXE

```powershell
.\scripts\publish.ps1
```

Creates `dist/SnipLoom-win-x64.zip` containing a single self-contained `SnipLoom.exe`.

---

## Tech Stack

| Component | Technology |
|-----------|------------|
| UI Framework | WPF (.NET 8) |
| Screen Capture | ScreenRecorderLib + Windows.Graphics.Capture |
| Video Encoding | Media Foundation (H.264/AAC) |
| Audio Capture | NAudio (WASAPI loopback) |

---

## Project Structure

```
SnipLoom/
├── src/SnipLoom/
│   ├── MainWindow.xaml          # Main UI
│   ├── Views/
│   │   ├── DisplaySelectWindow  # Monitor picker
│   │   ├── WindowSelectWindow   # Application picker
│   │   ├── RegionSelectWindow   # Draw-to-select overlay
│   │   └── RecordingFrameWindow # Recording indicator
│   └── Services/
│       ├── EncoderService.cs    # Video encoding
│       └── CaptureService.cs    # Capture utilities
├── scripts/
│   └── publish.ps1              # Build portable release
├── LICENSE                      # MIT
└── README.md
```

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

<p align="center">
  <strong>No subscriptions. No cloud. Just screen recording.</strong>
</p>
