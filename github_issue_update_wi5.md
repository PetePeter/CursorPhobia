# WI#5 - System Tray Integration and Settings UI

## Description and Severity
**Severity**: High - User Interface
**Category**: UI/UX Implementation

Implement the system tray integration with context menu and settings dialog. This provides the primary user interface for controlling and configuring CursorPhobia.

## Discovery Information
- **Requirements Source**: requirements/2025-07-31-1815-cursor-phobia-app/requirements-spec.md
- **Dependencies**: WI#4 (Configuration system), WI#1-3 (Core functionality)
- **UI Framework**: WinForms for system tray and settings dialog
- **Design Decision**: System tray only interface, no main window
- **Timestamp**: 2025-07-31 18:58:09 AUSEST

## Probable Root Cause
New feature implementation - user interface layer for configuration and control

## DETAILED PHASED IMPLEMENTATION PLAN

### Infrastructure Analysis - Strong Foundations Available
- ✅ **CursorPhobiaConfiguration** class with comprehensive settings and validation
- ✅ **ICursorPhobiaEngine** interface with start/stop operations and event monitoring  
- ✅ **IConfigurationService** for configuration persistence
- ✅ **WinForms support** enabled in Core project (.NET 8.0-windows)
- ✅ **Dependency injection patterns** established throughout codebase
- ✅ **Comprehensive test infrastructure** in place

---

## 🚀 PHASE A: Core System Tray Integration (3-5 days)
**Goal:** Basic system tray functionality with enable/disable and settings launcher

### Phase A Key Deliverables
**New Files:**
- `src/Core/Services/ISystemTrayManager.cs` - Interface for tray management
- `src/Core/Services/SystemTrayManager.cs` - Core tray functionality  
- `src/Core/Models/TrayIconState.cs` - Tray icon state enumeration
- `src/Core/Services/ApplicationLifecycleManager.cs` - App lifecycle management
- `tests/SystemTrayManagerTests.cs` - Unit tests

**Core Interface:**
```csharp
public interface ISystemTrayManager : IDisposable
{
    bool IsInitialized { get; }
    TrayIconState CurrentState { get; }
    
    Task InitializeAsync();
    Task UpdateStateAsync(TrayIconState state, string? statusText = null);
    Task ShowNotificationAsync(string title, string message, NotificationIcon icon);
    
    event EventHandler<TrayActionEventArgs>? TrayActionRequested;
}
```

**Context Menu Structure:**
```
📌 CursorPhobia [Status: Enabled/Disabled]
├── ✅/❌ Toggle Enable/Disable
├── ⚙️ Settings...
├── ℹ️ About...
├── ─────────────
└── ❌ Exit
```

**Tray Icon States:**
- 🟢 **Enabled**: Green icon, tooltip shows "CursorPhobia - Active"
- 🔴 **Disabled**: Red icon, tooltip shows "CursorPhobia - Disabled"  
- 🟡 **Warning**: Yellow icon for configuration issues
- ⚫ **Error**: Gray icon when hooks fail to install

### Phase A Acceptance Criteria
- [ ] System tray icon appears with correct initial state
- [ ] Right-click context menu shows all menu items
- [ ] Toggle enable/disable works and updates engine state
- [ ] Tray icon state reflects actual engine status in real-time
- [ ] Exit menu item cleanly shuts down application
- [ ] Tray icon properly removed on application exit
- [ ] Basic notification system works for status messages
- [ ] Application handles Windows shutdown/logoff gracefully

---

## 🎛️ PHASE B: Comprehensive Settings Dialog (5-7 days)
**Goal:** Full-featured settings dialog with real-time preview and configuration integration

### Phase B Key Deliverables
**New Files:**
- `src/Core/UI/Forms/SettingsForm.cs` - Main settings dialog
- `src/Core/UI/Forms/SettingsForm.Designer.cs` - UI layout
- `src/Core/UI/Models/SettingsViewModel.cs` - Data binding model
- `src/Core/UI/Controls/ProximitySlider.cs` - Custom slider with preview
- `src/Core/UI/Controls/MonitorLayoutControl.cs` - Visual monitor representation
- `tests/UI/SettingsFormTests.cs` - UI behavior tests

### Settings Tabs Structure

#### 🔧 General Tab
- Enable/Disable CursorPhobia (large toggle)
- Start with Windows (checkbox)
- Show notifications (checkbox with options)
- Language selection (future-proofing)

#### ⚡ Behavior Tab  
- **Proximity distance slider** (10-500px) with real-time preview
- **Push distance slider** (50-1000px) with visual indicator
- **Animation speed slider** (50-2000ms) with sample animation
- **Hover timeout spinner** (1-30 seconds)
- **CTRL override** enable/disable

#### 🖥️ Multi-Monitor Tab
- Enable edge wrapping (checkbox)
- Wrap behavior dropdown (Smart/Adjacent/Opposite)
- **Monitor layout visual representation**
- Per-monitor enable/disable checkboxes
- DPI scaling options

#### ⚙️ Advanced Tab
- Logging level dropdown
- Performance optimization presets
- **Export/Import configuration** buttons
- Reset to defaults button
- Advanced proximity algorithm selection

**Real-time Preview Features:**
```csharp
public class BehaviorPreviewControl : UserControl
{
    public void UpdateProximityPreview(int proximityDistance);
    public void UpdatePushDistancePreview(int pushDistance);
    public void PlayAnimationPreview(int animationDuration, AnimationEasing easing);
}
```

### Phase B Acceptance Criteria
- [ ] Settings dialog opens and displays current configuration accurately
- [ ] All sliders provide immediate visual feedback with real-time preview
- [ ] Multi-monitor layout displays correctly on systems with multiple displays
- [ ] Configuration validation provides clear, actionable error messages
- [ ] Apply/OK/Cancel buttons work correctly with proper change detection
- [ ] Import/Export functionality preserves all settings accurately
- [ ] Keyboard navigation works throughout all tabs
- [ ] Settings persist correctly between application restarts

---

## ✨ PHASE C: Advanced Features and Polish (4-6 days)
**Goal:** Professional-grade features including auto-start, window targeting, and UX enhancements

### Phase C Key Deliverables
**New Files:**
- `src/Core/Services/StartupManager.cs` - Windows auto-start integration
- `src/Core/UI/Controls/WindowTargetingTool.cs` - Window selection crosshair
- `src/Core/Services/SnoozeManager.cs` - Temporary disable functionality
- `src/Core/UI/Forms/PerformanceStatsDialog.cs` - Performance metrics display

#### 🚀 Windows Auto-Start Integration
```csharp
public interface IStartupManager
{
    Task<bool> IsAutoStartEnabledAsync();
    Task<bool> EnableAutoStartAsync();
    Task<bool> DisableAutoStartAsync();
}
// Uses: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

#### 🎯 Window Targeting Tool
- **Crosshair cursor** for window selection
- **Real-time window highlighting** during selection
- Window exclusion/inclusion list management
- Process-based and window-class-based filtering

#### 😴 Snooze Functionality
- Temporary disable for X minutes/hours
- Snooze countdown in tray tooltip
- Quick snooze options (5min, 15min, 1hr, until restart)
- Custom snooze duration picker

#### 📈 Enhanced Context Menu
```
📌 CursorPhobia [Status: Active | Snoozed: 15min remaining]
├── ✅/❌ Enable/Disable  
├── 😴 Snooze ►
│   ├── 5 minutes
│   ├── 15 minutes  
│   ├── 1 hour
│   ├── Until restart
│   └── Custom...
├── ⚙️ Settings...
├── 📊 Performance Stats...
├── ℹ️ About CursorPhobia...
├── ─────────────
└── ❌ Exit
```

### Phase C Acceptance Criteria
- [ ] Auto-start functionality works correctly across Windows versions
- [ ] Window targeting tool accurately selects specific windows
- [ ] Exclusion list prevents CursorPhobia from affecting excluded windows
- [ ] Snooze functionality temporarily disables behavior with accurate countdown
- [ ] Performance statistics provide meaningful system impact metrics
- [ ] Enhanced context menu provides quick access to all common functions
- [ ] Professional-grade error handling and user feedback

---

## 🧪 COMPREHENSIVE TESTING STRATEGY

### Unit Tests (All Phases)
```csharp
// Phase A Tests
[TestMethod] public async Task SystemTray_Initialize_CreatesNotifyIcon()
[TestMethod] public async Task TrayState_Update_ChangesToCorrectIcon()
[TestMethod] public async Task ContextMenu_RightClick_ShowsAllRequiredItems()

// Phase B Tests  
[TestMethod] public async Task Settings_LoadConfiguration_PopulatesAllControls()
[TestMethod] public async Task ProximitySlider_Change_UpdatesPreviewInRealTime()
[TestMethod] public async Task ImportExport_Roundtrip_PreservesCompleteConfiguration()

// Phase C Tests
[TestMethod] public async Task AutoStart_Enable_AddsToWindowsRegistry()
[TestMethod] public async Task WindowTargeting_Crosshair_SelectsCorrectWindow()
[TestMethod] public async Task Snooze_Duration_TemporarilyDisablesEngine()
```

### Integration Testing
- **Engine Integration**: Tray state reflects actual engine status
- **Configuration Persistence**: Settings dialog ↔ configuration service
- **Cross-Component**: Auto-start ↔ tray manager ↔ engine lifecycle

### Manual Testing Focus Areas
- **User Experience**: Intuitive navigation, clear feedback
- **Accessibility**: Keyboard navigation, screen reader compatibility
- **Cross-Windows-Version**: Windows 10/11 compatibility
- **Performance**: UI responsiveness, memory usage

---

## 🕐 IMPLEMENTATION TIMELINE

| Phase | Duration | Key Milestones |
|-------|----------|---------------|
| **Phase A** | 3-5 days | Basic tray functional, toggle works |
| **Phase B** | 5-7 days | Full settings dialog, real-time preview |
| **Phase C** | 4-6 days | Advanced features, professional polish |
| **Total** | **12-18 days** | Complete system tray integration |

**Critical Path Dependencies:**
1. Phase A completes → Phase B can begin (tray foundation required)
2. Phase B completes → Phase C can begin (settings integration needed)
3. Each phase includes testing and validation
4. Cross-phase integration testing at completion

---

## 🔧 TECHNICAL INTEGRATION POINTS

### With Existing Systems
**WI#1 (Core):** Engine start/stop events → tray state updates
**WI#2 (Push-Away):** Real-time proximity preview in settings
**WI#3 (Multi-Monitor):** Visual monitor layout in settings dialog
**WI#4 (Configuration):** Complete settings binding and persistence

### Dependency Injection Setup
```csharp
// Program.cs service registration additions
services.AddSingleton<ISystemTrayManager, SystemTrayManager>();
services.AddSingleton<IApplicationLifecycleManager, ApplicationLifecycleManager>();
services.AddTransient<SettingsForm>();
services.AddSingleton<IStartupManager, StartupManager>();
services.AddSingleton<ISnoozeManager, SnoozeManager>();
```

### Resource Requirements
- Tray icons (16x16, 32x32): enabled, disabled, warning, error states
- Form layouts with proper DPI scaling
- Embedded help text and tooltips

---

## 🎯 SUCCESS METRICS

### Phase A Success
- System tray functional with basic controls
- Engine integration working
- Clean application lifecycle management

### Phase B Success  
- Complete settings interface
- Real-time configuration preview
- Multi-monitor support UI
- Import/export functionality

### Phase C Success
- Professional user experience
- Advanced features (auto-start, snooze, targeting)
- Performance monitoring
- Production-ready polish

### Overall WI#5 Success
- **User**: Intuitive interface for all CursorPhobia functionality
- **Technical**: Maintainable, testable UI architecture  
- **Business**: Professional application ready for distribution

---

**See detailed implementation plan:** `WI5_DETAILED_IMPLEMENTATION_PLAN.md`

## Completion Notes
*To be filled during implementation*

### Attempt 1
- **Date**: _TBD_
- **What was done**: _TBD_
- **Why it was done**: _TBD_
- **Value to user**: _TBD_