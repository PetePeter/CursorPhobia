# WI#4 Missing Phases: Detailed Implementation Plans

**Product Priority:**
1. **Phase 4: Engine Integration and Configuration Hot-Swapping** (HIGH)
2. **Phase 3: File Watcher and Live Reloading** (MEDIUM) 
3. **Phase 5: Configuration CLI and Validation Tools** (LOW)

---

## Phase 3: File Watcher and Live Reloading (MEDIUM)

### Overview
Implement FileSystemWatcher to detect external configuration file changes and automatically reload configuration without requiring application restart.

### Files to Create/Modify

#### 1. Create: `src/Core/Services/IConfigurationWatcherService.cs`
```csharp
public interface IConfigurationWatcherService : IDisposable
{
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    Task StartWatchingAsync(string configPath);
    Task StopWatchingAsync();
    bool IsWatching { get; }
}
```

#### 2. Create: `src/Core/Services/ConfigurationWatcherService.cs`
- Uses FileSystemWatcher to monitor config file changes
- Implements debouncing to prevent multiple rapid fire events
- Handles temporary file exclusions (editors create .tmp files)
- Validates changed configuration before raising events
- Thread-safe implementation

#### 3. Modify: `src/Core/Services/IConfigurationService.cs`
**Add methods:**
- `Task<CursorPhobiaConfiguration> ReloadConfigurationAsync(string filePath)`
- `Task<bool> ValidateConfigurationFileAsync(string filePath)`

#### 4. Modify: `src/Core/Services/ConfigurationService.cs`
**Add implementations:**
- ReloadConfigurationAsync method
- ValidateConfigurationFileAsync method
- Better error handling for file corruption detection

### Implementation Approach

**FileSystemWatcher Setup:**
- Monitor specific config file (not directory to avoid noise)
- Filter events: Changed, Deleted, Renamed
- Debounce events (500ms delay) to handle editor save patterns
- Exclude temp files (.tmp, .bak, ~, .swp)

**Event Flow:**
1. FileSystemWatcher detects file change
2. Wait 500ms (debounce)
3. Validate file is not locked/in-use
4. Load and validate new configuration
5. If valid, raise ConfigurationChanged event
6. If invalid, log error and keep current config

**Error Handling:**
- File locked → Retry after delay
- Invalid JSON → Log error, maintain current config
- File deleted → Log warning, continue with current config
- File corrupted → Attempt backup restoration

### Integration Points

**With ConfigurationService:**
- ConfigurationWatcherService uses IConfigurationService.ReloadConfigurationAsync
- Shares validation logic

**With CursorPhobiaEngine:**
- Engine subscribes to ConfigurationChanged events
- Applies hot-swappable settings immediately
- Queues settings requiring restart for next restart

### Testing Strategy

**Unit Tests:**
```csharp
[Test] void FileChange_ValidConfig_RaisesEvent()
[Test] void FileChange_InvalidConfig_KeepsCurrentConfig()
[Test] void FileChange_Debouncing_SingleEventForRapidChanges()
[Test] void TempFileChange_Ignored()
[Test] void FileDeleted_LogsWarning_KeepsCurrentConfig()
```

**Integration Tests:**
- Real FileSystemWatcher with temporary files
- Test with actual text editors (notepad, VS Code)
- Test concurrent file access scenarios

### Success Criteria
- [ ] External config file changes detected within 1 second
- [ ] Rapid sequential changes debounced to single reload
- [ ] Invalid changes don't crash or reset current config
- [ ] Temporary editor files ignored
- [ ] Thread-safe operation under concurrent access

---

## Phase 4: Engine Integration and Configuration Hot-Swapping (HIGH)

### Overview
Enable CursorPhobiaEngine to receive and apply configuration changes at runtime without requiring restart. Implement hot-swapping for performance-critical settings.

### Files to Create/Modify

#### 1. Modify: `src/Core/Services/CursorPhobiaEngine.cs`
**Add methods:**
- `Task UpdateConfigurationAsync(CursorPhobiaConfiguration newConfig)`
- `Task<bool> CanHotSwapConfigurationAsync(CursorPhobiaConfiguration newConfig)`
- `Task RestartWithNewConfigurationAsync(CursorPhobiaConfiguration newConfig)`

**Add private methods:**
- `ApplyHotSwappableSettings(CursorPhobiaConfiguration config)`
- `DetermineRestartRequiredSettings(CursorPhobiaConfiguration current, CursorPhobiaConfiguration new)`

#### 2. Modify: `src/Core/Services/ICursorPhobiaEngine.cs`
**Add interface methods:**
```csharp
Task UpdateConfigurationAsync(CursorPhobiaConfiguration newConfig);
Task<bool> CanHotSwapConfigurationAsync(CursorPhobiaConfiguration newConfig);
event EventHandler<ConfigurationChangeResultEventArgs> ConfigurationUpdateCompleted;
```

#### 3. Create: `src/Core/Models/ConfigurationChangeResult.cs`
```csharp
public class ConfigurationChangeResult
{
    public bool Success { get; set; }
    public bool RequiresRestart { get; set; }
    public List<string> HotSwappedSettings { get; set; }
    public List<string> RestartRequiredSettings { get; set; }
    public string ErrorMessage { get; set; }
}
```

#### 4. Modify: `src/Console/Program.cs`
**In SetupTrayIntegration method:**
- Wire up ConfigurationWatcherService
- Connect configuration change handler to engine
- Handle restart-required scenarios gracefully

### Implementation Approach

**Hot-Swappable Settings (Apply Immediately):**
- ProximityThreshold
- PushDistance 
- HoverTimeoutMs
- EnableHoverTimeout
- EnableCtrlOverride
- AnimationDurationMs
- AnimationEasing
- UpdateIntervalMs (within bounds)
- MaxUpdateIntervalMs

**Restart-Required Settings:**
- EnableAnimations (changes animation system)
- ApplyToAllWindows vs TopMostOnly (changes window detection)
- WindowFilter settings (requires hook reinstallation)

**Hot-Swap Implementation:**
```csharp
private async Task ApplyHotSwappableSettings(CursorPhobiaConfiguration config)
{
    // Update timer interval
    if (_config.UpdateIntervalMs != config.UpdateIntervalMs)
    {
        _updateTimer.Interval = config.UpdateIntervalMs;
        _logger.LogInformation("Updated timer interval to {Interval}ms", config.UpdateIntervalMs);
    }
    
    // Update proximity detector if needed
    // Update window pusher animation settings
    // Update safety manager bounds
    // etc.
}
```

**Configuration Comparison:**
```csharp
private List<string> DetermineRestartRequiredSettings(
    CursorPhobiaConfiguration current, 
    CursorPhobiaConfiguration new)
{
    var restartRequired = new List<string>();
    
    if (current.ApplyToAllWindows != new.ApplyToAllWindows)
        restartRequired.Add("Window Detection Mode");
    
    if (current.EnableAnimations != new.EnableAnimations)
        restartRequired.Add("Animation System");
        
    // ... other comparisons
    
    return restartRequired;
}
```

### Integration Points

**With ConfigurationWatcherService:**
- Engine subscribes to ConfigurationChanged events
- Engine calls UpdateConfigurationAsync when changes detected

**With System Tray:**
- Show notifications for successful config changes
- Show restart prompts for restart-required changes
- Update tray menu state if needed

**With Settings Dialog:**
- Settings dialog can trigger immediate updates
- Settings dialog shows which changes require restart
- Settings dialog can offer restart option

### Testing Strategy

**Unit Tests:**
```csharp
[Test] void UpdateConfiguration_HotSwappableOnly_AppliesImmediately()
[Test] void UpdateConfiguration_RestartRequired_QueuesForRestart()
[Test] void UpdateConfiguration_MixedChanges_HandlesCorrectly()
[Test] void CanHotSwap_ValidatesCorrectly()
```

**Integration Tests:**
- Test with actual FileSystemWatcher events
- Test performance impact of configuration changes
- Test engine stability during config updates

### Success Criteria
- [ ] Hot-swappable settings apply within 1 second of file change
- [ ] Engine performance remains stable during config updates
- [ ] Restart-required changes handled gracefully with user notification
- [ ] Configuration validation prevents invalid hot-swaps
- [ ] Thread-safe configuration updates
- [ ] Rollback capability if hot-swap fails

---

## Phase 5: Configuration CLI and Validation Tools (LOW)

### Overview
Extend the console application with CLI parsing to provide configuration management commands and validation utilities.

### Files to Create/Modify

#### 1. Create: `src/Core/Services/IConfigurationCLIService.cs`
```csharp
public interface IConfigurationCLIService
{
    Task<int> ValidateConfigurationAsync(string configPath);
    Task<int> ShowConfigurationAsync(string configPath);
    Task<int> SetConfigurationValueAsync(string configPath, string key, string value);
    Task<int> ResetConfigurationAsync(string configPath);
    Task<int> MigrateConfigurationAsync(string oldPath, string newPath);
}
```

#### 2. Create: `src/Core/Services/ConfigurationCLIService.cs`
**Commands to implement:**
- `--validate-config path` - Validate configuration file
- `--show-config path` - Display current configuration
- `--set-config path key=value` - Set specific configuration value
- `--reset-config path` - Reset to defaults
- `--migrate-config oldPath newPath` - Migrate configuration

#### 3. Modify: `src/Console/Program.cs`
**Add CLI parsing:**
- Use System.CommandLine or simple argument parsing
- Route CLI commands to ConfigurationCLIService
- Exit codes: 0=success, 1=error, 2=validation failed

#### 4. Create: `src/Core/Utilities/ConfigurationKeyPath.cs`
```csharp
public static class ConfigurationKeyPath
{
    public static bool TrySetValue(CursorPhobiaConfiguration config, string keyPath, string value);
    public static object GetValue(CursorPhobiaConfiguration config, string keyPath);
    public static bool IsValidKeyPath(string keyPath);
}
```

### Implementation Approach

**CLI Argument Parsing:**
```csharp
static async Task<int> Main(string[] args)
{
    if (args.Length > 0)
    {
        return await ProcessCLIArguments(args);
    }
    
    // Existing interactive mode
    await RunInteractiveMode();
    return 0;
}
```

**Configuration Key Paths:**
- Use dot notation: `proximity.distance`, `timing.hoverTimeoutMs`
- Support array indexing: `exclusions.byWindowClass[0]`
- Type conversion for primitive values

**Validation Command Output:**
```
Validating configuration: C:\Users\...\config.json
✓ File exists and is readable
✓ Valid JSON format
✓ Schema validation passed
✓ All numeric values within valid ranges
✓ All regex patterns are valid
✗ Warning: proximity.distance (25) is below recommended minimum (50)
✗ Error: timing.hoverTimeoutMs (-1) cannot be negative

Result: 1 error, 1 warning
```

**Set Configuration Examples:**
```bash
CursorPhobia.Console.exe --set-config config.json proximity.distance=75
CursorPhobia.Console.exe --set-config config.json timing.hoverTimeoutMs=3000
CursorPhobia.Console.exe --set-config config.json general.enabled=false
```

### Integration Points

**With ConfigurationService:**
- CLI uses existing load/save methods
- Shares validation logic

**With System Tray Mode:**
- CLI commands work while tray mode is running
- File watcher picks up CLI changes automatically

### Testing Strategy

**Unit Tests:**
```csharp
[Test] void ValidateConfig_ValidFile_ReturnsSuccess()
[Test] void ValidateConfig_InvalidFile_ReturnsError()
[Test] void SetConfigValue_ValidKeyPath_UpdatesValue()
[Test] void SetConfigValue_InvalidKeyPath_ReturnsError()
```

**Integration Tests:**
- Test CLI commands with actual config files
- Test integration with running application
- Test batch operations

### Success Criteria
- [ ] All CLI commands return appropriate exit codes
- [ ] Configuration validation provides clear error messages
- [ ] Set-config command supports all configuration properties
- [ ] CLI works with running application (file watcher integration)
- [ ] Comprehensive help documentation
- [ ] Proper error handling and user feedback

---

## Implementation Sequence

**Phase 4 First (High Priority):**
1. Implement hot-swap capability in CursorPhobiaEngine
2. Add configuration change events
3. Test with manual configuration changes
4. Integration with settings dialog

**Phase 3 Second (Medium Priority):**
1. Implement FileSystemWatcher service
2. Connect to Phase 4 hot-swap system
3. Test external file editing scenarios
4. Performance and stability testing

**Phase 5 Last (Low Priority):**
1. Add CLI argument parsing
2. Implement configuration commands
3. Add validation and reporting
4. Documentation and help system

**Total Estimated Effort:** 2-3 weeks for all phases combined