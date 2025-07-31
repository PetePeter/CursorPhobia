# Context Findings

## Codebase Analysis
- **Current State:** Empty repository with only LICENSE file (MIT)
- **Technology Stack:** Not yet determined
- **Architecture:** Not yet established

## Technology Recommendations for Windows Desktop App
Given the requirements for:
- Windows background service
- Global mouse tracking
- Window manipulation
- System tray integration
- Easy compilation

**Recommended Tech Stack:**
1. **C# with .NET** - Native Windows integration, easy compilation, excellent Windows API access
2. **Electron** - Cross-platform, easy development, but heavier resource usage
3. **Go** - Single binary compilation, good performance, but more complex Windows API integration
4. **C++** - Maximum performance and control, but complex development

## Key Windows APIs Needed
- `SetWindowsHookEx` for global mouse tracking
- `EnumWindows` and `GetWindowLong` for finding always-on-top windows
- `SetWindowPos` for window manipulation
- `GetKeyState` for CTRL key detection
- Shell notification APIs for system tray

## Technical Constraints
- Must run as background service
- Requires elevated permissions for global hooks
- Need timer mechanisms for 5-second delay logic
- Multi-monitor support for window "warping"