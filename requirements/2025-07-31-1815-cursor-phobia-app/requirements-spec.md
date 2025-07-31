# [FEATURE] CursorPhobia - Always-On-Top Window Avoidance App

## Problem Statement
Users often have always-on-top windows (notifications, media players, utility windows, etc.) that interfere with normal workflow by blocking content or mouse interactions. Current solutions require manually moving these windows or disabling their always-on-top behavior entirely.

## Solution Overview
CursorPhobia is a Windows background service that automatically moves always-on-top windows away from the mouse cursor, creating a "phobic" behavior where these windows avoid the cursor. The app provides intelligent behavior with user control and CTRL-key override functionality.

## Functional Requirements

### Core Behavior
1. **Global Mouse Tracking**: Monitor mouse cursor position system-wide
2. **Always-On-Top Detection**: Identify windows with `WS_EX_TOPMOST` flag
3. **Proximity Detection**: Detect when mouse approaches always-on-top windows
4. **Window Avoidance**: Push windows away from approaching mouse cursor
5. **Edge Wrapping**: When windows reach screen edges, teleport to opposite side or another monitor
6. **CTRL Override**: Disable push-away behavior when CTRL key is held
7. **Hover Tolerance**: Allow mouse to hover over windows for up to 5 seconds before re-enabling push behavior
8. **Timer Logic**: Start 5-second timer when mouse exits window boundary, don't reset on re-entry

### Multi-Monitor Support
- Detect all connected monitors
- Support window wrapping across multiple displays
- Handle different monitor resolutions and arrangements

### Configuration System
- **Sensitivity Settings**: Configurable proximity distance for triggering push-away
- **Timing Controls**: Adjustable 5-second hover timer duration
- **Movement Parameters**: Configurable push-away distance and animation speed
- **Application Lists**: 
  - Exclusion list for windows that should never be pushed
  - Inclusion list for specific applications to target beyond WS_EX_TOPMOST
- **Monitor Preferences**: Per-monitor behavior settings

### User Interface
- **System Tray Integration**: Background operation with notification area icon
- **Context Menu Access**: All configuration through right-click tray menu
- **Status Indicators**: Visual feedback for enabled/disabled state
- **Configuration Window**: Modal settings dialog accessible from tray

### Logging and Debugging
- **Activity Logging**: Record window movements, triggers, and system events
- **Performance Metrics**: Track resource usage and response times
- **Error Handling**: Log failures and provide user-friendly error messages
- **Debug Mode**: Verbose logging for troubleshooting

## Technical Requirements

### Technology Stack
- **Framework**: C# with .NET Framework/Core
- **UI Framework**: WinForms for simple dialogs and system tray
- **Windows APIs**: User32.dll for hooks and window manipulation

### Windows API Integration
- `SetWindowsHookEx` with `WH_MOUSE_LL` for global mouse tracking
- `GetWindowLong`/`GetWindowLongPtr` with `GWL_EXSTYLE` for topmost detection
- `SetWindowPos` for window positioning and movement
- `GetKeyState` for CTRL key detection
- `EnumWindows` and `EnumDisplayMonitors` for window and monitor enumeration

### Security and Permissions
- **Standard User Operation**: Run as regular user by default
- **Elevation on Demand**: Prompt for administrator privileges only when installing global hooks
- **UAC Integration**: Proper elevation request handling

### Performance Considerations
- **Low Resource Usage**: Minimal CPU and memory footprint
- **Efficient Polling**: Optimized mouse tracking without excessive system calls
- **Responsive Movement**: Smooth window animations without lag

## Implementation Hints

### Project Structure
```
CursorPhobia/
├── src/
│   ├── Core/
│   │   ├── MouseTracker.cs          # Global mouse hook implementation
│   │   ├── WindowManager.cs         # Window detection and manipulation
│   │   ├── ConfigManager.cs         # Settings persistence
│   │   └── Logger.cs                # Logging system
│   ├── UI/
│   │   ├── SystemTrayManager.cs     # Notification area integration
│   │   └── SettingsForm.cs          # Configuration dialog
│   └── Program.cs                   # Main entry point
├── CursorPhobia.csproj
└── README.md
```

### Key Classes and Methods
- `MouseTracker`: Implements `ILowLevelMouseProc` callback for mouse hooks
- `WindowManager`: `GetTopMostWindows()`, `MoveWindowAway()`, `GetMonitorBounds()`
- `ConfigManager`: JSON-based settings with `Load()`, `Save()`, `GetDefaults()`

### Build and Deployment
- **Single Executable**: Compile to standalone .exe with embedded dependencies
- **No Installation Required**: Portable application that can run from any location
- **Auto-Start Integration**: Optional Windows startup integration

## Acceptance Criteria

### Core Functionality
- [ ] App runs in background with system tray icon
- [ ] Mouse cursor pushes away always-on-top windows within configurable distance
- [ ] Windows wrap to opposite screen edges or other monitors when pushed to boundaries
- [ ] CTRL key prevents push-away behavior when held
- [ ] Mouse can hover over windows for 5 seconds before push behavior resumes
- [ ] Configuration accessible through system tray context menu

### Configuration
- [ ] Users can adjust proximity sensitivity (default: 50 pixels)
- [ ] Users can modify hover timer duration (default: 5 seconds)
- [ ] Users can set push-away distance and speed
- [ ] Users can exclude specific applications from being pushed
- [ ] Users can include non-topmost windows from specific applications
- [ ] Settings persist between application restarts

### Multi-Monitor
- [ ] App detects and works across all connected monitors
- [ ] Windows wrap correctly between monitors of different resolutions
- [ ] Per-monitor settings can be configured independently

### System Integration
- [ ] App requests elevation only when needed for hook installation
- [ ] System tray icon shows current enabled/disabled state
- [ ] App can be enabled/disabled without restart
- [ ] Graceful shutdown removes all system hooks

### Logging and Debugging
- [ ] Activity logs created in configurable location
- [ ] Log rotation prevents unlimited disk usage
- [ ] Debug mode available for troubleshooting
- [ ] Performance metrics available in logs

## Assumptions
- Windows 10/11 environment with standard window management APIs
- Users have basic familiarity with system tray applications
- Most always-on-top windows are small utility windows rather than full-screen applications
- 50-pixel proximity default provides good balance between responsiveness and accidental triggering
- JSON configuration files are acceptable for settings persistence

## Future Enhancements (Out of Scope)
- Cross-platform support (macOS, Linux)
- Advanced window animation effects
- Machine learning for user behavior adaptation
- Integration with specific applications' APIs
- Voice command integration