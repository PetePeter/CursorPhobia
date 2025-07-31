# CursorPhobia - Phase 1 Implementation

CursorPhobia is a Windows application that automatically moves always-on-top windows away from the mouse cursor. This repository contains the Phase 1 implementation focusing on core Windows API foundation.

## Phase 1 Status: ✅ COMPLETED

### What's Implemented

Phase 1 delivers the core Windows API foundation with the following components:

#### 🏗️ Architecture
- **Clean layered architecture** with proper separation of concerns
- **Dependency injection** ready with interfaces
- **Comprehensive error handling** and logging
- **Unit testable** design with mocked Windows API calls

#### 📁 Directory Structure
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

#### 🔧 Core Functionality

**WindowDetectionService:**
- ✅ `GetAllTopMostWindows()` - Finds all always-on-top windows
- ✅ `IsWindowAlwaysOnTop(IntPtr hWnd)` - Checks topmost flag
- ✅ `GetWindowInformation(IntPtr hWnd)` - Gets comprehensive window info
- ✅ `EnumerateVisibleWindows()` - Lists all visible windows

**WindowManipulationService:**
- ✅ `MoveWindow(IntPtr hWnd, int x, int y)` - Moves windows (preserves size)
- ✅ `GetWindowBounds(IntPtr hWnd)` - Gets window rectangle
- ✅ `IsWindowVisible(IntPtr hWnd)` - Checks window visibility

#### 🧪 Testing Infrastructure
- **Unit tests** with XUnit and Moq for mocking
- **Integration tests** for real Windows API validation
- **Console test application** for manual verification
- **Automated test runner** (`runTests.bat`)

## Prerequisites

- Windows 10 or Windows 11
- .NET 6.0 SDK or later
- Visual Studio 2022 or Visual Studio Code (recommended)

## Building and Running

### Build the Solution
```bash
dotnet build CursorPhobia.sln
```

### Run Unit Tests
```bash
dotnet test tests/CursorPhobia.Tests.csproj
```

### Run Console Test Application
```bash
dotnet run --project src/Console/CursorPhobia.Console.csproj
```

### Run All Tests (Windows)
```bash
runTests.bat
```

## Usage Example

```csharp
// Setup dependency injection
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<Logger>();
services.AddTransient<IWindowDetectionService, WindowDetectionService>();
services.AddTransient<IWindowManipulationService, WindowManipulationService>();

var provider = services.BuildServiceProvider();

// Use the services
var detectionService = provider.GetRequiredService<IWindowDetectionService>();
var manipulationService = provider.GetRequiredService<IWindowManipulationService>();

// Find all topmost windows
var topmostWindows = detectionService.GetAllTopMostWindows();
foreach (var window in topmostWindows)
{
    Console.WriteLine($"Topmost window: {window.title} at {window.bounds}");
    
    // Move window away by 100 pixels
    manipulationService.MoveWindow(window.windowHandle, 
        window.bounds.X + 100, window.bounds.Y + 100);
}
```

## Technical Details

### Windows API Integration
- **Proper P/Invoke patterns** with error handling
- **64-bit compatibility** using safe handle operations
- **Memory-safe** string operations with StringBuilder
- **Structured error reporting** with Win32 error codes

### Key Features
- **Always-on-top detection** using `WS_EX_TOPMOST` extended style
- **Window filtering** to exclude system/hidden windows
- **Multi-monitor awareness** with monitor information models
- **Process/thread information** retrieval for each window
- **Comprehensive logging** at Debug/Info/Warning/Error levels

### Error Handling
- All Windows API calls are wrapped with try/catch
- Invalid handles return safe default values
- Win32 error codes are logged with meaningful messages
- Services never throw unhandled exceptions

## Phase 1 Acceptance Criteria Status

- ✅ Can enumerate all visible windows on system
- ✅ Correctly identifies windows with WS_EX_TOPMOST flag
- ✅ Can retrieve comprehensive window information (title, class, bounds)
- ✅ Can move windows to specified coordinates
- ✅ All Windows API calls have proper error handling
- ✅ Logging system captures all significant events
- ✅ Unit test coverage > 80% for core detection logic

## What's Next: Phase 2

Phase 2 will implement:
- Global low-level mouse hook
- Mouse cursor position tracking
- Basic proximity detection and window pushing
- Multi-monitor support foundation

## Architecture Decisions

### Why This Structure?
1. **Layered Architecture**: Separates Windows API concerns from business logic
2. **Interface-Based Design**: Enables unit testing with mocked dependencies
3. **Dependency Injection**: Promotes loose coupling and testability
4. **Comprehensive Logging**: Essential for debugging Windows API interactions
5. **Error-First Design**: All operations can fail gracefully

### Windows API Choices
- **User32.dll**: Core window management functions
- **Kernel32.dll**: System-level operations and error handling
- **SetWindowPos vs MoveWindow**: SetWindowPos provides more control
- **GetWindowLongPtr**: 64-bit safe window property retrieval

## Contributing

This is Phase 1 of a 4-phase implementation plan. Each phase builds upon the previous one:

1. **Phase 1**: ✅ Core Windows API Foundation (Current)
2. **Phase 2**: Global Mouse Tracking and Basic Avoidance
3. **Phase 3**: Advanced Timing Logic and Enhanced Features  
4. **Phase 4**: User Interface and Configuration System

## License

MIT License - See LICENSE file for details.