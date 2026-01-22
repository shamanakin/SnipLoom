# Changelog

All notable changes to SnipLoom will be documented in this file.

## [1.0.0] - 2026-01-22

### Added
- Display capture mode (record entire monitors)
- Window capture mode (record specific application windows)
- Region capture mode with fixed aspect ratio presets (16:9, 9:16, 1:1, 5:4, 4:5)
- System audio capture via WASAPI loopback
- MP4 output with H.264 video and AAC audio
- Save dialog after recording with file location picker
- Recording frame indicator for region capture mode
- Window search/filter in window picker
- Last saved file quick access (Open File, Open Folder)

### Technical
- Built with WPF on .NET 8
- Uses ScreenRecorderLib for encoding
- Recording frame excluded from capture via `SetWindowDisplayAffinity`
