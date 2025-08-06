# CursorPhobia

A Windows application that automatically pushes away always-on-top windows when your cursor approaches them, keeping your desktop clean and distraction-free.

## Warning

**This project is "vibe coded" and experimental.** Not everything will work exactly as hoped. However, the core functionality of shooing away always-on-top windows appears to work reliably.

Use at your own risk and expect some rough edges.

## What It Does

CursorPhobia monitors your cursor position and automatically moves always-on-top windows away when you get too close to them. This is particularly useful for:

- Persistent notification windows
- Always-on-top utility applications
- Floating toolbars and panels
- Any window that stays in front of your work

The application runs quietly in your system tray and can be toggled on/off as needed.

## Features

- **System Tray Integration**: Runs silently in the background with tray icon controls
- **Always-On-Top Detection**: Specifically targets windows with the always-on-top property
- **Configurable Behavior**: Adjustable push distances and detection zones
- **Safety First**: Starts disabled by default - you choose when to activate
- **Performance Monitoring**: Built-in performance tracking and health monitoring
- **Multi-Monitor Support**: Works across multiple monitor setups

## Quick Start

### Prerequisites

- Windows 10/11
- .NET 8.0 Runtime

### Running the Application

1. **Build and Run Tests**:
   ```
   runTests.bat
   ```

2. **Build and Run in System Tray**:
   ```
   runApp.bat
   ```

The application will start in the system tray with the engine **disabled by default** for safety. Right-click the tray icon to enable it when you're ready.

### System Tray Controls

Right-click the CursorPhobia tray icon to:
- Enable/Disable the cursor phobia engine
- Access settings and configuration
- View performance statistics
- Exit the application

## Technical Details

### Architecture

- **Core Engine**: Window detection and manipulation logic
- **System Integration**: Windows API integration for window management
- **Health Monitoring**: Comprehensive error recovery and performance tracking
- **Configuration Management**: Live configuration reloading
- **Single Instance**: Prevents multiple instances from running

### Configuration

The application supports various configuration options for:
- Detection sensitivity
- Push distances
- Excluded applications
- Performance thresholds

Configuration files are stored in `%APPDATA%\CursorPhobia\`

### Logging

Comprehensive logging is available at:
```
%APPDATA%\CursorPhobia\Logs\
```

Log levels include performance metrics, window operations, and error tracking.

## Development

### Project Structure

```
src/
├── Console/          # Main application entry point
└── Core/            # Core engine and services
tests/               # Unit and integration tests
```

### Building from Source

```bash
# Build solution
dotnet build CursorPhobia.sln

# Run tests
dotnet test

# Run application
dotnet run --project src/Console/CursorPhobia.Console.csproj
```

### Testing

The application includes comprehensive test suites:
- Unit tests for core functionality
- Integration tests for system components
- Performance and stress testing
- Manual verification utilities

## Known Issues

This is experimental software with known limitations:

- May not work perfectly with all window types
- Performance can vary depending on system load
- Some edge cases in multi-monitor setups may not be handled
- Configuration UI is still in development
- Not all planned features are fully implemented

## Safety and Privacy

- **No Network Activity**: The application operates entirely locally
- **No Data Collection**: No telemetry or usage data is transmitted
- **Minimal System Impact**: Designed to be lightweight and non-intrusive
- **User Control**: You maintain full control over when and how it operates

## License

MIT License - see LICENSE file for details.

## Contributing

This is an experimental project. Contributions are welcome but expect the codebase to be somewhat chaotic as it's been "vibe coded" rather than formally architected.

Feel free to:
- Report issues and bugs
- Suggest improvements
- Submit pull requests for fixes
- Share your experiences with different window types

## Disclaimer

This software is provided "as is" without any warranties. The core functionality of moving always-on-top windows works, but the overall experience may be rough around the edges. Use responsibly and be prepared for unexpected behavior.