# CursorPhobia - Phased Implementation Plan

## Overview
This document outlines a 4-phase implementation strategy for CursorPhobia, breaking down the complex Windows API wrapper and mouse hook infrastructure into manageable, incrementally testable phases.

---

## Phase 1: Core Windows API Foundation
**Goals**: Establish fundamental Windows API integration and window management capabilities  
**Complexity**: Medium  
**Dependencies**: None  

### Deliverables
- Basic Windows API P/Invoke wrapper infrastructure
- Always-on-top window detection system
- Window enumeration and information gathering
- Basic window positioning functionality

### Files to Create/Modify
```
src/Core/
├── WindowsAPI/
│   ├── User32.cs               # P/Invoke declarations for User32.dll
│   ├── Kernel32.cs             # P/Invoke declarations for Kernel32.dll
│   └── WindowsStructures.cs    # Windows API structures and constants
├── Models/
│   ├── WindowInfo.cs           # Window information data model
│   └── MonitorInfo.cs          # Monitor information data model
├── Services/
│   ├── WindowDetectionService.cs   # Always-on-top window detection
│   └── WindowManipulationService.cs # Basic window positioning
└── Utilities/
    └── Logger.cs               # Basic logging infrastructure
```

### Key Functions/Classes to Implement

#### WindowDetectionService.cs
```csharp
- GetAllTopMostWindows() -> List<WindowInfo>
- IsWindowAlwaysOnTop(IntPtr hWnd) -> bool
- GetWindowInformation(IntPtr hWnd) -> WindowInfo
- EnumerateVisibleWindows() -> List<WindowInfo>
```

#### WindowManipulationService.cs
```csharp
- MoveWindow(IntPtr hWnd, int x, int y) -> bool
- GetWindowBounds(IntPtr hWnd) -> Rectangle
- IsWindowVisible(IntPtr hWnd) -> bool
```

#### WindowsAPI/User32.cs
```csharp
- EnumWindows, GetWindowLong, SetWindowPos
- GetWindowRect, IsWindowVisible
- GetWindowText, GetClassName
```

### Testing Strategy
- **Unit Tests**: Mock Windows API calls for core detection logic
- **Integration Tests**: Test against real always-on-top windows (create test utility windows)
- **Manual Testing**: Verify detection works with common always-on-top apps (media players, notifications)

### Acceptance Criteria
- [ ] Can enumerate all visible windows on system
- [ ] Correctly identifies windows with WS_EX_TOPMOST flag
- [ ] Can retrieve comprehensive window information (title, class, bounds)
- [ ] Can move windows to specified coordinates
- [ ] All Windows API calls have proper error handling
- [ ] Logging system captures all significant events
- [ ] Unit test coverage > 80% for core detection logic

---

## Phase 2: Global Mouse Tracking and Basic Avoidance
**Goals**: Implement global mouse hook and basic cursor avoidance behavior  
**Complexity**: High  
**Dependencies**: Phase 1  

### Deliverables
- Global low-level mouse hook implementation
- Mouse cursor position tracking
- Basic proximity detection and window pushing
- Multi-monitor support foundation

### Files to Create/Modify
```
src/Core/
├── Services/
│   ├── MouseTrackingService.cs     # Global mouse hook implementation
│   ├── ProximityDetectionService.cs # Distance calculations and collision detection
│   └── AvoidanceEngineService.cs   # Core push-away logic
├── Models/
│   ├── MousePosition.cs            # Mouse coordinate data model
│   └── ProximityEvent.cs           # Proximity detection event data
└── Utilities/
    └── GeometryUtils.cs            # Distance calculations, screen bounds
```

### Key Functions/Classes to Implement

#### MouseTrackingService.cs
```csharp
- InstallGlobalMouseHook() -> bool
- UninstallMouseHook() -> bool
- GetCurrentMousePosition() -> Point
- MouseMoveCallback(int nCode, IntPtr wParam, IntPtr lParam) -> IntPtr
- OnMouseMove event handler
```

#### ProximityDetectionService.cs
```csharp
- CheckProximityToWindows(Point mousePos, List<WindowInfo> windows) -> List<ProximityEvent>
- CalculateDistance(Point mouse, Rectangle window) -> double
- IsMouseNearWindow(Point mouse, Rectangle window, int threshold) -> bool
```

#### AvoidanceEngineService.cs
```csharp
- ProcessProximityEvents(List<ProximityEvent> events) -> void
- CalculatePushAwayVector(Point mouse, Rectangle window) -> Vector
- MoveWindowAway(WindowInfo window, Vector pushVector) -> bool
```

### Testing Strategy
- **Unit Tests**: Mock mouse events and test proximity calculations
- **Integration Tests**: Test hook installation/removal, verify mouse tracking accuracy
- **Manual Testing**: Test push-away behavior with various window sizes and positions
- **Performance Tests**: Ensure mouse hook doesn't cause system lag

### Acceptance Criteria
- [ ] Global mouse hook installs successfully (with elevation if needed)
- [ ] Mouse position tracking works across all monitors
- [ ] Proximity detection triggers within configurable distance (default 50px)
- [ ] Windows are pushed away smoothly when mouse approaches
- [ ] Basic multi-monitor support (windows stay within monitor bounds)
- [ ] Hook uninstalls cleanly on application shutdown
- [ ] No noticeable system performance impact
- [ ] Mouse tracking accuracy within 1 pixel

---

## Phase 3: Advanced Timing Logic and Enhanced Features
**Goals**: Implement sophisticated behavior including CTRL override, hover timer, and edge wrapping  
**Complexity**: High  
**Dependencies**: Phase 2  

### Deliverables
- CTRL key override functionality
- 5-second hover timer implementation
- Edge wrapping and monitor teleportation
- Enhanced window movement with animation

### Files to Create/Modify
```
src/Core/
├── Services/
│   ├── KeyboardMonitorService.cs   # CTRL key state monitoring
│   ├── HoverTimerService.cs        # 5-second hover timer logic
│   ├── EdgeWrappingService.cs      # Screen edge and monitor wrapping
│   └── AnimationService.cs         # Smooth window movement
├── Models/
│   ├── HoverState.cs              # Timer state for each window
│   ├── MonitorLayout.cs           # Multi-monitor configuration
│   └── AnimationConfig.cs         # Movement speed and easing settings
└── Utilities/
    └── MonitorUtils.cs            # Monitor enumeration and bounds
```

### Key Functions/Classes to Implement

#### KeyboardMonitorService.cs
```csharp
- IsControlKeyPressed() -> bool
- StartKeyboardMonitoring() -> void
- StopKeyboardMonitoring() -> void
- OnControlKeyStateChanged event
```

#### HoverTimerService.cs
```csharp
- StartHoverTimer(IntPtr windowHandle) -> void
- StopHoverTimer(IntPtr windowHandle) -> void
- IsHoverTimerActive(IntPtr windowHandle) -> bool
- OnHoverTimerExpired event
```

#### EdgeWrappingService.cs
```csharp
- GetWrapDestination(Rectangle window, Vector pushVector) -> Rectangle
- GetAvailableMonitors() -> List<MonitorInfo>
- WrapWindowToOppositeEdge(WindowInfo window) -> Rectangle
- TeleportToNextMonitor(WindowInfo window) -> Rectangle
```

### Testing Strategy
- **Unit Tests**: Test timer logic, edge detection, CTRL key simulation
- **Integration Tests**: Test cross-monitor wrapping scenarios
- **User Experience Tests**: Verify 5-second timer feels natural, CTRL override is responsive
- **Edge Case Tests**: Test with unusual monitor arrangements, ultra-wide displays

### Acceptance Criteria
- [ ] CTRL key successfully disables push-away behavior when held
- [ ] 5-second hover timer works correctly (starts on mouse exit, doesn't reset on re-entry)
- [ ] Windows wrap to opposite screen edges when pushed to boundaries
- [ ] Windows teleport to other monitors when appropriate
- [ ] Smooth animation for window movements (no jarring jumps)
- [ ] Works correctly with multiple monitors of different resolutions
- [ ] Timer management doesn't cause memory leaks
- [ ] CTRL key response time < 50ms

---

## Phase 4: User Interface and Configuration System
**Goals**: Complete the application with system tray integration, configuration management, and persistence  
**Complexity**: Medium  
**Dependencies**: Phase 3  

### Deliverables
- System tray integration with context menu
- Configuration dialog and settings management
- JSON-based settings persistence
- Application lifecycle management

### Files to Create/Modify
```
src/UI/
├── SystemTrayManager.cs           # Notification area integration
├── SettingsForm.cs               # Configuration dialog (WinForms)
├── ContextMenuBuilder.cs         # Dynamic context menu creation
└── Resources/
    ├── app-icon.ico              # System tray icons
    ├── app-icon-disabled.ico     # Disabled state icon
    └── Resources.resx            # UI text resources
src/Configuration/
├── ConfigurationManager.cs       # Settings persistence and validation
├── Models/
│   ├── AppConfig.cs              # Main configuration model
│   ├── ProximitySettings.cs      # Distance and timing settings
│   └── ExclusionRules.cs         # App inclusion/exclusion lists
Program.cs                        # Main entry point and lifecycle
```

### Key Functions/Classes to Implement

#### SystemTrayManager.cs
```csharp
- InitializeTrayIcon() -> void
- ShowContextMenu() -> void
- UpdateTrayIconState(bool enabled) -> void
- OnTrayIconDoubleClick event handler
```

#### ConfigurationManager.cs
```csharp
- LoadConfiguration() -> AppConfig
- SaveConfiguration(AppConfig config) -> bool
- GetDefaultConfiguration() -> AppConfig
- ValidateConfiguration(AppConfig config) -> ValidationResult
```

#### SettingsForm.cs
```csharp
- LoadCurrentSettings() -> void
- SaveSettings() -> bool
- ResetToDefaults() -> void
- ValidateInputs() -> bool
```

### Testing Strategy
- **Unit Tests**: Test configuration serialization, validation logic
- **UI Tests**: Test settings dialog functionality, tray interaction
- **Integration Tests**: Test settings persistence across app restarts
- **User Experience Tests**: Verify intuitive configuration workflow

### Acceptance Criteria
- [ ] System tray icon appears and shows current enabled/disabled state
- [ ] Right-click context menu provides access to all major functions
- [ ] Settings dialog allows configuration of all parameters
- [ ] Settings persist correctly between application restarts
- [ ] JSON configuration files are human-readable and editable
- [ ] Application can be enabled/disabled without restart
- [ ] Graceful startup and shutdown behavior
- [ ] Error messages are user-friendly and actionable

---

## Implementation Dependencies and Considerations

### Cross-Phase Dependencies
1. **Phase 1 → Phase 2**: Window detection must be stable before adding mouse tracking
2. **Phase 2 → Phase 3**: Basic avoidance must work before adding complex timing logic
3. **Phase 3 → Phase 4**: Core functionality must be complete before adding UI

### Risk Mitigation
- **Windows API Complexity**: Start with well-documented APIs, add comprehensive error handling
- **Performance Impact**: Monitor system resource usage throughout development
- **UAC/Permissions**: Test elevation scenarios early and often
- **Multi-Monitor Edge Cases**: Test with various monitor configurations

### Testing Strategy Across Phases
- **Automated Testing**: Unit tests for business logic, integration tests for Windows API calls
- **Manual Testing**: Real-world scenarios with different applications and monitor setups
- **Performance Testing**: Ensure mouse hook doesn't impact system responsiveness
- **Compatibility Testing**: Test on Windows 10 and Windows 11

### Rollback Plan
Each phase should be implemented on feature branches with the ability to rollback to the previous stable phase if critical issues are discovered.

---

## Total Estimated Timeline
- **Phase 1**: 1-2 weeks (Foundation)
- **Phase 2**: 2-3 weeks (Core functionality)  
- **Phase 3**: 2-3 weeks (Advanced features)
- **Phase 4**: 1-2 weeks (UI and polish)

**Total**: 6-10 weeks depending on complexity of Windows API integration and testing thoroughness.

This phased approach ensures each increment provides testable value while building toward the complete CursorPhobia solution with proper technical foundation and user experience considerations.