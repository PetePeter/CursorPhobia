# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status
CursorPhobia is a Windows application that prevents windows from interfering with cursor movements by intelligently pushing them away. The application has completed Phase 5 of Issue #10 implementation, which includes smart defaults and simplified UI.

## Development Commands
- `dotnet build` - Build the solution
- `dotnet test` - Run all tests
- `dotnet test --filter "FullyQualifiedName~ConfigurationTests"` - Run configuration tests specifically
- `dotnet run --project src/Console` - Run the console application

## Architecture Overview

### Smart Defaults System
CursorPhobia implements a sophisticated smart defaults system that automatically provides optimal settings for most users:

- **HardcodedDefaults Class**: Contains carefully tuned constants derived from extensive testing and user feedback
- **Optimal Performance**: 16ms update interval (~60 FPS) with 33ms fallback (~30 FPS minimum)  
- **Spatial Settings**: 50px proximity threshold, 100px push distance, 20px screen edge buffer
- **Animation Settings**: Smooth 200ms animations with EaseOut easing for natural deceleration
- **User Experience**: CTRL key override, hover timeout, and intelligent multi-monitor support

### Configuration Management
The configuration system has been simplified through smart defaults:

**User-Configurable Properties:**
- ProximityThreshold (distance that triggers window pushing)
- PushDistance (how far windows are moved)
- EnableCtrlOverride (allows CTRL key to temporarily disable)
- ApplyToAllWindows (affects all windows vs only topmost)
- HoverTimeoutMs (timeout before stopping push behavior)
- EnableHoverTimeout (enables/disables hover timeout feature)
- MultiMonitor settings (edge wrapping and cross-monitor behavior)

**Hardcoded Properties (No Longer User-Configurable):**
- UpdateIntervalMs (16ms - ~60 FPS optimal performance)
- MaxUpdateIntervalMs (33ms - ~30 FPS minimum under load)
- ScreenEdgeBuffer (20px - prevents windows getting stuck at edges)
- CtrlReleaseToleranceDistance (50px - smooth CTRL key interaction)
- AlwaysOnTopRepelBorderDistance (30px - better always-on-top window handling)
- AnimationDurationMs (200ms - balance of smooth and responsive)
- EnableAnimations (true - improves user experience)
- AnimationEasing (EaseOut - natural deceleration)

### Testing Strategy
The test suite validates both the smart defaults system and user-configurable properties:

- **Configuration Tests**: Validate user-configurable properties and ensure hardcoded defaults
- **HardcodedDefaults Tests**: Verify optimal values and consistency of hardcoded constants
- **SettingsViewModel Tests**: Ensure UI correctly exposes hardcoded values as read-only information
- **Integration Tests**: Validate the complete system behavior with smart defaults

## Code Standards
- Use camelCase naming convention (e.g., `typeID` not `type_id`)
- For two-letter abbreviations, use uppercase (e.g., `ID`)
- Hardcoded values should use the `HardcodedDefaults` class constants
- Obsolete properties should be marked with `[Obsolete]` attribute and clear guidance

## Testing
- Framework: xUnit with MSTest compatibility
- Test Categories: Unit tests, integration tests, and end-to-end tests
- Coverage: Focus on user-configurable properties and hardcoded defaults validation
- Run specific test groups using `--filter` parameter for faster feedback

## Smart Defaults Rationale
The smart defaults system was implemented to:
1. **Reduce Configuration Complexity**: Most users don't need to adjust performance settings
2. **Improve User Experience**: Optimal settings work well across different hardware configurations  
3. **Ensure Stability**: Hardcoded values prevent users from setting problematic configurations
4. **Maintain Performance**: Values are based on research and testing for optimal responsiveness
5. **Future-Proof Design**: Easy to update defaults based on new research or feedback

## Notes
- Repository uses MIT License
- Project name: CursorPhobia
- Smart defaults implemented in Phase 5 of Issue #10
- Configuration properties marked obsolete are for backward compatibility only