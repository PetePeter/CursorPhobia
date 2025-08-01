# WI#5 - System Tray Integration and Settings UI
## Detailed Phased Implementation Plan

### Executive Summary
Implement system tray integration and settings UI for CursorPhobia, building on the existing configuration and engine infrastructure. The implementation is structured in three logical phases that can be developed, tested, and deployed independently.

### Existing Infrastructure Analysis
**Strong Foundations Available:**
- `CursorPhobiaConfiguration` class with comprehensive settings and validation
- `ICursorPhobiaEngine` interface with start/stop operations and event monitoring
- `IConfigurationService` for configuration persistence
- WinForms support enabled in Core project (.NET 8.0-windows)
- Dependency injection patterns established throughout codebase
- Comprehensive test infrastructure in place

---

## PHASE A: Core System Tray Integration
**Goal:** Basic system tray functionality with enable/disable and settings launcher
**Duration:** 3-5 days
**Dependencies:** WI#1-4 (Core functionality and configuration system)

### Phase A Deliverables

#### A1: System Tray Manager Infrastructure
**Files to Create:**
- `src/Core/Services/ISystemTrayManager.cs` - Interface for tray management
- `src/Core/Services/SystemTrayManager.cs` - Core tray functionality
- `src/Core/Models/TrayIconState.cs` - Tray icon state enumeration
- `tests/SystemTrayManagerTests.cs` - Unit tests

**SystemTrayManager Responsibilities:**
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

public enum TrayIconState
{
    Enabled,      // Green icon - CursorPhobia active
    Disabled,     // Red icon - CursorPhobia disabled  
    Warning,      // Yellow icon - Configuration issues
    Error         // Gray icon - Hook failures
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

#### A2: Basic Engine Integration
**Files to Modify:**
- `src/Core/Services/CursorPhobiaEngine.cs` - Add tray state notifications
- `src/Console/Program.cs` - Initialize tray manager

**Integration Points:**
- Engine start/stop events → Tray state updates
- Engine performance stats → Tray tooltip text
- Engine errors → Tray warning/error states
- CTRL override activation → Temporary tray notification (optional)

#### A3: Application Lifecycle Management
**Files to Create:**
- `src/Core/Services/IApplicationLifecycleManager.cs` - Interface
- `src/Core/Services/ApplicationLifecycleManager.cs` - Implementation
- `tests/ApplicationLifecycleManagerTests.cs` - Tests

**Responsibilities:**
```csharp
public interface IApplicationLifecycleManager
{
    Task InitializeAsync();
    Task ShutdownAsync();
    Task RestartAsync();
    
    bool IsShuttingDown { get; }
    event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;
}
```

### Phase A Acceptance Criteria
- [ ] System tray icon appears with correct initial state (enabled/disabled)
- [ ] Right-click context menu shows all menu items
- [ ] Toggle enable/disable works correctly and updates engine state
- [ ] Tray icon state reflects actual engine status in real-time
- [ ] Exit menu item cleanly shuts down application
- [ ] Tray icon is properly removed on application exit
- [ ] Basic notification system works for status messages
- [ ] Application handles Windows shutdown/logoff gracefully

### Phase A Testing Strategy
```csharp
[TestClass]
public class SystemTrayManagerTests
{
    [TestMethod] public async Task Initialize_CreatesNotifyIcon()
    [TestMethod] public async Task UpdateState_ChangesToCorrectIcon()
    [TestMethod] public async Task ContextMenu_ShowsAllRequiredItems()
    [TestMethod] public async Task ToggleEnabled_UpdatesEngineState()
    [TestMethod] public async Task ShowNotification_DisplaysCorrectMessage()
    [TestMethod] public void Dispose_RemovesTrayIcon()
}
```

---

## PHASE B: Comprehensive Settings Dialog
**Goal:** Full-featured settings dialog with real-time preview and configuration integration
**Duration:** 5-7 days
**Dependencies:** Phase A completed

### Phase B Deliverables

#### B1: Settings Form Foundation
**Files to Create:**
- `src/Core/UI/Forms/SettingsForm.cs` - Main settings dialog
- `src/Core/UI/Forms/SettingsForm.Designer.cs` - UI layout
- `src/Core/UI/Models/SettingsViewModel.cs` - Data binding model
- `src/Core/UI/Controls/ProximitySlider.cs` - Custom slider with preview
- `tests/UI/SettingsFormTests.cs` - UI behavior tests

**Form Architecture:**
```csharp
public partial class SettingsForm : Form
{
    private readonly IConfigurationService _configService;
    private readonly ICursorPhobiaEngine _engine;
    private readonly SettingsViewModel _viewModel;
    private CursorPhobiaConfiguration _workingConfig;
    private bool _hasUnsavedChanges;
    
    public async Task LoadConfigurationAsync();
    public async Task<bool> SaveConfigurationAsync();
    public bool ValidateCurrentSettings();
    private void UpdatePreview();
}
```

#### B2: Tab Implementation - General Settings
**UI Elements:**
- Enable/Disable CursorPhobia (large toggle)
- Start with Windows (checkbox)
- Show notifications (checkbox with options)
- Language selection (future-proofing)

**Data Binding:**
```csharp
public class GeneralSettingsViewModel
{
    public bool IsEnabled { get; set; }
    public bool StartWithWindows { get; set; }
    public bool ShowNotifications { get; set; }
    public NotificationLevel NotificationLevel { get; set; }
}
```

#### B3: Tab Implementation - Behavior Settings  
**UI Elements:**
- Proximity distance slider (10-500px) with real-time preview
- Push distance slider (50-1000px) with visual indicator  
- Animation speed slider (50-2000ms) with sample animation
- Hover timeout spinner (1-30 seconds)
- CTRL override enable/disable

**Real-time Preview Features:**
```csharp
public class BehaviorPreviewControl : UserControl
{
    public void UpdateProximityPreview(int proximityDistance);
    public void UpdatePushDistancePreview(int pushDistance);
    public void PlayAnimationPreview(int animationDuration, AnimationEasing easing);
}
```

#### B4: Tab Implementation - Multi-Monitor Settings
**UI Elements:**
- Enable edge wrapping (checkbox)
- Wrap behavior dropdown (Smart/Adjacent/Opposite)
- Monitor layout visual representation
- Per-monitor enable/disable checkboxes
- DPI scaling options

**Visual Monitor Layout:**
```csharp
public class MonitorLayoutControl : UserControl
{
    public void DisplayMonitorConfiguration(List<MonitorInfo> monitors);
    public void UpdateMonitorSettings(string monitorId, PerMonitorSettings settings);
    
    event EventHandler<MonitorSettingsChangedEventArgs>? MonitorSettingsChanged;
}
```

#### B5: Tab Implementation - Advanced Settings
**UI Elements:**
- Logging level dropdown
- Performance optimization presets
- Export/Import configuration buttons
- Reset to defaults button
- Advanced proximity algorithm selection

**Configuration Management:**
```csharp
public class ConfigurationManager
{
    public async Task<bool> ExportConfigurationAsync(string filePath);
    public async Task<CursorPhobiaConfiguration?> ImportConfigurationAsync(string filePath);
    public CursorPhobiaConfiguration CreateResetConfiguration();
}
```

### Phase B Acceptance Criteria
- [ ] Settings dialog opens and displays current configuration accurately
- [ ] All sliders and controls provide immediate visual feedback
- [ ] Real-time proximity preview shows accurate trigger distances
- [ ] Animation preview demonstrates actual movement behavior
- [ ] Multi-monitor layout displays correctly on systems with multiple displays
- [ ] Per-monitor settings can be configured independently
- [ ] Configuration validation provides clear, actionable error messages
- [ ] Apply/OK/Cancel buttons work correctly with proper change detection
- [ ] Import/Export functionality preserves all settings accurately
- [ ] Keyboard navigation works throughout all tabs
- [ ] Settings persist correctly between application restarts

### Phase B Testing Strategy
```csharp
[TestClass]
public class SettingsFormIntegrationTests
{
    [TestMethod] public async Task LoadConfiguration_PopulatesAllControls()
    [TestMethod] public async Task ProximitySlider_UpdatesPreviewInRealTime()
    [TestMethod] public async Task ValidationErrors_HighlightProblematicControls()
    [TestMethod] public async Task SaveConfiguration_PersistsAllChanges()
    [TestMethod] public async Task ImportExport_PreservesCompleteConfiguration()
    [TestMethod] public void KeyboardNavigation_WorksInAllTabs()
}
```

---

## PHASE C: Advanced Features and Polish
**Goal:** Professional-grade features including auto-start, window targeting, and user experience enhancements
**Duration:** 4-6 days  
**Dependencies:** Phase B completed

### Phase C Deliverables

#### C1: Windows Auto-Start Integration
**Files to Create:**
- `src/Core/Services/IStartupManager.cs` - Interface
- `src/Core/Services/StartupManager.cs` - Registry integration
- `tests/StartupManagerTests.cs` - Auto-start tests

**Implementation:**
```csharp
public interface IStartupManager
{
    Task<bool> IsAutoStartEnabledAsync();
    Task<bool> EnableAutoStartAsync();
    Task<bool> DisableAutoStartAsync();
    Task<string> GetAutoStartCommandAsync();
}

// Uses: HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
public class StartupManager : IStartupManager
{
    private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "CursorPhobia";
}
```

#### C2: Window Targeting Tool
**Files to Create:**
- `src/Core/UI/Controls/WindowTargetingTool.cs` - Target selection UI
- `src/Core/Services/IWindowTargetingService.cs` - Interface
- `src/Core/Services/WindowTargetingService.cs` - Implementation
- `tests/WindowTargetingServiceTests.cs` - Tests

**Features:**
- Crosshair cursor for window selection
- Real-time window highlighting during selection
- Window exclusion/inclusion list management
- Process-based and window-class-based filtering

```csharp
public class WindowTargetingTool : Form
{
    public WindowInfo? SelectedWindow { get; private set; }
    
    public void StartTargeting();
    public void StopTargeting();
    
    event EventHandler<WindowSelectedEventArgs>? WindowSelected;
    event EventHandler? TargetingCancelled;
}
```

#### C3: Enhanced Exclusions Management
**Integration with existing settings:**
- Add "Exclusions" tab to settings dialog
- Window/process exclusion list with add/remove functionality
- Test exclusion button using window targeting tool
- Import/export exclusion lists

**UI Elements:**
```csharp
public class ExclusionsTabControl : UserControl
{
    private ListBox _excludedWindowsList;
    private Button _addExclusionButton;
    private Button _removeExclusionButton;
    private Button _testExclusionButton;
    
    public void AddExclusion(WindowExclusion exclusion);
    public void RemoveSelectedExclusion();
    public void TestExclusionAgainstCurrentWindows();
}
```

#### C4: Snooze Functionality
**Files to Create:**
- `src/Core/Services/ISnoozeManager.cs` - Interface
- `src/Core/Services/SnoozeManager.cs` - Implementation
- `src/Core/UI/Forms/SnoozeDialog.cs` - Snooze time selection
- `tests/SnoozeManagerTests.cs` - Tests

**Features:**
- Temporary disable for X minutes/hours
- Snooze countdown in tray tooltip
- Quick snooze options (5min, 15min, 1hr, until restart)
- Custom snooze duration picker

```csharp
public interface ISnoozeManager
{
    bool IsSnoozing { get; }
    DateTime? SnoozeEndTime { get; }
    TimeSpan? RemainingSnoozeTime { get; }
    
    Task SnoozeAsync(TimeSpan duration);
    Task EndSnoozeAsync();
    
    event EventHandler? SnoozeStarted;
    event EventHandler? SnoozeEnded;
}
```

#### C5: User Experience Polish
**Enhanced Context Menu:**
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

**Performance Statistics Dialog:**
- Real-time performance metrics display
- Average response time, CPU usage estimation
- Memory usage, tracked window count
- Export performance log functionality

### Phase C Acceptance Criteria  
- [ ] Auto-start functionality works correctly across Windows versions
- [ ] Window targeting tool accurately selects specific windows
- [ ] Exclusion list prevents CursorPhobia from affecting excluded windows
- [ ] Snooze functionality temporarily disables behavior with accurate countdown
- [ ] Performance statistics provide meaningful system impact metrics
- [ ] Enhanced context menu provides quick access to all common functions
- [ ] All new features integrate seamlessly with existing functionality
- [ ] Professional-grade error handling and user feedback
- [ ] Comprehensive help tooltips and user guidance

### Phase C Testing Strategy
```csharp
[TestClass] 
public class AdvancedFeaturesTests
{
    [TestMethod] public async Task AutoStart_EnablesOnWindowsStartup()
    [TestMethod] public async Task WindowTargeting_SelectsCorrectWindow() 
    [TestMethod] public async Task Exclusions_PreventWindowPushing()
    [TestMethod] public async Task Snooze_TemporarilyDisablesEngine()
    [TestMethod] public async Task PerformanceStats_ShowAccurateMetrics()
}
```

---

## Cross-Phase Technical Requirements

### Dependency Injection Registration
```csharp
// Program.cs service registration
services.AddSingleton<ISystemTrayManager, SystemTrayManager>();
services.AddSingleton<IApplicationLifecycleManager, ApplicationLifecycleManager>();
services.AddTransient<SettingsForm>();
services.AddSingleton<IStartupManager, StartupManager>();
services.AddSingleton<IWindowTargetingService, WindowTargetingService>();
services.AddSingleton<ISnoozeManager, SnoozeManager>();
```

### Resource Management
**Icon Resources Needed:**
- `resources/icons/tray_enabled.ico` (16x16, 32x32)
- `resources/icons/tray_disabled.ico` (16x16, 32x32)
- `resources/icons/tray_warning.ico` (16x16, 32x32)
- `resources/icons/tray_error.ico` (16x16, 32x32)

### Error Handling Strategy
**Consistent Error Patterns:**
```csharp
public class UIErrorHandler
{
    public static void HandleConfigurationError(Exception ex, ILogger logger);
    public static void HandleUIException(Exception ex, Form parentForm, ILogger logger);
    public static bool ShowErrorDialog(string message, string title, bool allowRetry = false);
}
```

### Performance Considerations
- UI updates should not block engine operations
- Settings form should load asynchronously to prevent UI freezing
- Real-time previews should be throttled to prevent excessive updates
- All tray operations should be non-blocking

### Accessibility Requirements
- Full keyboard navigation support
- Screen reader compatibility for all controls
- High contrast mode support
- Proper tab order throughout all dialogs

### Testing Strategy Summary
**Unit Tests:** Core logic, configuration handling, state management
**Integration Tests:** UI interactions, engine integration, configuration persistence  
**Manual Tests:** User experience, accessibility, cross-Windows-version compatibility
**Performance Tests:** UI responsiveness, memory usage, resource leaks

---

## Implementation Timeline

**Total Estimated Duration:** 12-18 days

| Phase | Duration | Parallel Work Possible |
|-------|----------|----------------------|
| Phase A | 3-5 days | Tests can be written in parallel |
| Phase B | 5-7 days | UI design and logic can be parallel |
| Phase C | 4-6 days | Feature integration and polish |

**Critical Path Dependencies:**
1. Phase A must complete before Phase B (tray foundation required)
2. Phase B completion enables Phase C development  
3. Each phase includes its own testing and validation
4. Cross-phase integration testing required at the end

**Risk Mitigation:**
- Each phase delivers working functionality independently
- Rollback plan available if any phase encounters blockers
- Comprehensive testing at each phase prevents accumulation of technical debt
- Clear acceptance criteria enable objective quality assessment