using System.Collections.Concurrent;
using System.Drawing;
using System.Diagnostics;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Main orchestration engine that coordinates all CursorPhobia services
/// Implements 5-second hover timer, CTRL override, multi-window coordination, and performance optimization
/// </summary>
public class CursorPhobiaEngine : ICursorPhobiaEngine, IDisposable
{
    private readonly ILogger _logger;
    private readonly ICursorTracker _cursorTracker;
    private readonly IProximityDetector _proximityDetector;
    private readonly IWindowDetectionService _windowDetectionService;
    private readonly IWindowPusher _windowPusher;
    private readonly ISafetyManager _safetyManager;
    private readonly IMonitorManager _monitorManager;
    private volatile CursorPhobiaConfiguration _config;
    private readonly object _configurationLock = new();
    
    // Engine state
    private volatile bool _isRunning = false;
    private volatile bool _disposed = false;
    private readonly object _stateLock = new();
    
    // Main update loop
    private readonly System.Timers.Timer _updateTimer;
    private readonly CancellationTokenSource _cancellationTokenSource;
    
    // Window tracking and hover timing
    private readonly ConcurrentDictionary<IntPtr, WindowTrackingInfo> _trackedWindows = new();
    private readonly object _windowTrackingLock = new();
    
    // CTRL key state tracking for tolerance implementation
    private bool _wasCtrlPressedLastUpdate = false;
    
    // Performance monitoring
    private readonly Stopwatch _performanceStopwatch = Stopwatch.StartNew();
    private long _updateCount = 0;
    private long _totalUpdateTimeMs = 0;
    private long _successfulUpdates = 0;
    private long _failedUpdates = 0;
    
    /// <summary>
    /// Gets whether the engine is currently running
    /// </summary>
    public bool IsRunning => _isRunning && !_disposed;
    
    /// <summary>
    /// Gets the number of windows currently being tracked
    /// </summary>
    public int TrackedWindowCount => _trackedWindows.Count;
    
    /// <summary>
    /// Gets the average update cycle time in milliseconds
    /// </summary>
    public double AverageUpdateTimeMs => _updateCount > 0 ? (double)_totalUpdateTimeMs / _updateCount : 0;
    
    /// <summary>
    /// Event raised when the engine starts
    /// </summary>
    public event EventHandler? EngineStarted;
    
    /// <summary>
    /// Event raised when the engine stops
    /// </summary>
    public event EventHandler? EngineStopped;
    
    /// <summary>
    /// Event raised when a window push operation occurs
    /// </summary>
    public event EventHandler<WindowPushEventArgs>? WindowPushed;
    
    /// <summary>
    /// Event raised when the engine state changes (for tray notifications)
    /// </summary>
    public event EventHandler<EngineStateChangedEventArgs>? EngineStateChanged;
    
    /// <summary>
    /// Event raised when performance issues are detected (for tray warnings)
    /// </summary>
    public event EventHandler<EnginePerformanceEventArgs>? PerformanceIssueDetected;
    
    /// <summary>
    /// Event raised when configuration is updated (for UI updates and logging)
    /// </summary>
    public event EventHandler<ConfigurationUpdatedEventArgs>? ConfigurationUpdated;
    
    /// <summary>
    /// Creates a new CursorPhobiaEngine instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="cursorTracker">Service for tracking cursor position and key states</param>
    /// <param name="proximityDetector">Service for calculating proximity between cursor and windows</param>
    /// <param name="windowDetectionService">Service for finding and monitoring windows</param>
    /// <param name="windowPusher">Service for moving windows with animation</param>
    /// <param name="safetyManager">Service for validating window positions</param>
    /// <param name="monitorManager">Monitor manager for multi-monitor support</param>
    /// <param name="config">Configuration for engine behavior</param>
    public CursorPhobiaEngine(
        ILogger logger,
        ICursorTracker cursorTracker,
        IProximityDetector proximityDetector,
        IWindowDetectionService windowDetectionService,
        IWindowPusher windowPusher,
        ISafetyManager safetyManager,
        IMonitorManager monitorManager,
        CursorPhobiaConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cursorTracker = cursorTracker ?? throw new ArgumentNullException(nameof(cursorTracker));
        _proximityDetector = proximityDetector ?? throw new ArgumentNullException(nameof(proximityDetector));
        _windowDetectionService = windowDetectionService ?? throw new ArgumentNullException(nameof(windowDetectionService));
        _windowPusher = windowPusher ?? throw new ArgumentNullException(nameof(windowPusher));
        _safetyManager = safetyManager ?? throw new ArgumentNullException(nameof(safetyManager));
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
        _config = config ?? CursorPhobiaConfiguration.CreateDefault();
        
        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }
        
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Initialize update timer
        _updateTimer = new System.Timers.Timer(_config.UpdateIntervalMs);
        _updateTimer.Elapsed += UpdateTimer_Elapsed;
        _updateTimer.AutoReset = true;
        
        _logger.LogInformation("CursorPhobiaEngine initialized with update interval: {UpdateInterval}ms, hover timeout: {HoverTimeout}ms",
            _config.UpdateIntervalMs, _config.HoverTimeoutMs);
    }
    
    /// <summary>
    /// Starts the cursor phobia engine
    /// </summary>
    /// <returns>True if started successfully, false otherwise</returns>
    public async Task<bool> StartAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot start disposed CursorPhobiaEngine");
            return false;
        }
        
        bool alreadyRunning;
        lock (_stateLock)
        {
            alreadyRunning = _isRunning;
        }
        
        if (alreadyRunning)
        {
            _logger.LogDebug("CursorPhobiaEngine is already running");
            return true;
        }
        
        try
        {
            _logger.LogInformation("Starting CursorPhobiaEngine...");
            
            // Start cursor tracking
            if (!_cursorTracker.StartTracking())
            {
                _logger.LogError("Failed to start cursor tracking");
                return false;
            }
            
            // Perform initial window discovery
            await RefreshTrackedWindowsAsync();
            
            // Start the main update loop and set running state
            lock (_stateLock)
            {
                _updateTimer.Start();
                _isRunning = true;
                
                _performanceStopwatch.Restart();
                _updateCount = 0;
                _totalUpdateTimeMs = 0;
                _successfulUpdates = 0;
                _failedUpdates = 0;
            }
            
            _logger.LogInformation("CursorPhobiaEngine started successfully. Tracking {WindowCount} windows", 
                _trackedWindows.Count);
            
            EngineStarted?.Invoke(this, EventArgs.Empty);
            EngineStateChanged?.Invoke(this, new EngineStateChangedEventArgs(EngineState.Running, "Engine started successfully"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting CursorPhobiaEngine");
            EngineStateChanged?.Invoke(this, new EngineStateChangedEventArgs(EngineState.Error, $"Failed to start: {ex.Message}"));
            return false;
        }
    }
    
    /// <summary>
    /// Stops the cursor phobia engine
    /// </summary>
    public Task StopAsync()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
            {
                _logger.LogDebug("CursorPhobiaEngine is already stopped");
                return Task.CompletedTask;
            }
            
            try
            {
                _logger.LogInformation("Stopping CursorPhobiaEngine...");
                
                _isRunning = false;
                
                // Stop the update timer
                _updateTimer.Stop();
                
                // Cancel all window animations
                _windowPusher.CancelAllAnimations();
                
                // Stop cursor tracking
                _cursorTracker.StopTracking();
                
                // Clear tracked windows
                _trackedWindows.Clear();
                
                _logger.LogInformation("CursorPhobiaEngine stopped successfully. Average update time: {AvgTime:F2}ms", 
                    AverageUpdateTimeMs);
                
                EngineStopped?.Invoke(this, EventArgs.Empty);
                EngineStateChanged?.Invoke(this, new EngineStateChangedEventArgs(EngineState.Stopped, "Engine stopped successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping CursorPhobiaEngine");
                EngineStateChanged?.Invoke(this, new EngineStateChangedEventArgs(EngineState.Error, $"Error during stop: {ex.Message}"));
            }
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Forces a refresh of the tracked windows list
    /// </summary>
    public Task RefreshTrackedWindowsAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing tracked windows...");
            
            bool applyToAllWindows;
            lock (_configurationLock)
            {
                applyToAllWindows = _config.ApplyToAllWindows;
            }
            
            var discoveredWindows = applyToAllWindows 
                ? _windowDetectionService.EnumerateVisibleWindows()
                : _windowDetectionService.GetAllTopMostWindows();
            
            var currentTime = DateTime.UtcNow;
            var newTrackedWindows = new Dictionary<IntPtr, WindowTrackingInfo>();
            
            // Update existing windows and add new ones
            foreach (var window in discoveredWindows)
            {
                if (window.IsMinimized)
                    continue; // Skip minimized windows
                
                if (_trackedWindows.TryGetValue(window.WindowHandle, out var existingInfo))
                {
                    // Update existing window info
                    existingInfo.WindowInfo = window;
                    newTrackedWindows[window.WindowHandle] = existingInfo;
                }
                else
                {
                    // Add new window
                    var trackingInfo = new WindowTrackingInfo
                    {
                        WindowInfo = window,
                        FirstSeenTime = currentTime,
                        LastProximityCheckTime = DateTime.MinValue,
                        HoverStartTime = null,
                        IsInProximity = false,
                        IsHoveringTimeout = false
                    };
                    newTrackedWindows[window.WindowHandle] = trackingInfo;
                    _logger.LogDebug("Started tracking window: {Title} ({Handle:X})", 
                        window.Title, window.WindowHandle.ToInt64());
                }
            }
            
            // Remove windows that no longer exist
            var removedCount = 0;
            foreach (var kvp in _trackedWindows)
            {
                if (!newTrackedWindows.ContainsKey(kvp.Key))
                {
                    removedCount++;
                    _logger.LogDebug("Stopped tracking window: {Title} ({Handle:X})", 
                        kvp.Value.WindowInfo.Title, kvp.Key.ToInt64());
                }
            }
            
            // Update the tracked windows collection
            lock (_windowTrackingLock)
            {
                _trackedWindows.Clear();
                foreach (var kvp in newTrackedWindows)
                {
                    _trackedWindows[kvp.Key] = kvp.Value;
                }
            }
            
            _logger.LogInformation("Window refresh complete: {Total} tracked ({New} new, {Removed} removed)", 
                newTrackedWindows.Count, 
                newTrackedWindows.Count - (_trackedWindows.Count - removedCount),
                removedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing tracked windows");
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Main update timer event handler
    /// </summary>
    private async void UpdateTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isRunning || _disposed)
            return;
        
        var updateStartTime = Stopwatch.StartNew();
        var updateSuccessful = false;
        
        try
        {
            await ProcessUpdateCycleAsync();
            updateSuccessful = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update cycle");
        }
        finally
        {
            // Update performance metrics
            updateStartTime.Stop();
            _updateCount++;
            _totalUpdateTimeMs += updateStartTime.ElapsedMilliseconds;
            
            if (updateSuccessful)
                _successfulUpdates++;
            else
                _failedUpdates++;
            
            // Log performance warnings if updates are taking too long
            int maxUpdateIntervalMs;
            lock (_configurationLock)
            {
                maxUpdateIntervalMs = _config.MaxUpdateIntervalMs;
            }
            
            if (updateStartTime.ElapsedMilliseconds > maxUpdateIntervalMs)
            {
                _logger.LogWarning("Update cycle took {Duration}ms, exceeding max interval of {MaxInterval}ms", 
                    updateStartTime.ElapsedMilliseconds, maxUpdateIntervalMs);
                
                // Raise performance issue event for tray notification
                PerformanceIssueDetected?.Invoke(this, new EnginePerformanceEventArgs(
                    "Slow Update Cycle", 
                    $"Update cycle took {updateStartTime.ElapsedMilliseconds}ms (max: {maxUpdateIntervalMs}ms)",
                    updateStartTime.ElapsedMilliseconds));
            }
        }
    }
    
    /// <summary>
    /// Processes a single update cycle
    /// </summary>
    private async Task ProcessUpdateCycleAsync()
    {
        // Get configuration values in a thread-safe manner
        bool enableCtrlOverride;
        lock (_configurationLock)
        {
            enableCtrlOverride = _config.EnableCtrlOverride;
        }
        
        // Get current cursor position (needed for CTRL tolerance logic)
        var cursorPosition = _cursorTracker.GetCurrentCursorPosition();
        if (cursorPosition.IsEmpty)
        {
            _logger.LogWarning("Failed to get cursor position during update cycle");
            return;
        }

        // Check CTRL key state and handle tolerance logic
        bool isCtrlPressed = enableCtrlOverride && _cursorTracker.IsCtrlKeyPressed();
        bool wasCtrlJustReleased = _wasCtrlPressedLastUpdate && !isCtrlPressed;
        
        if (isCtrlPressed)
        {
            // CTRL is pressed - cancel all animations and skip processing
            _windowPusher.CancelAllAnimations();
            
            // Reset hover states for all windows
            lock (_windowTrackingLock)
            {
                foreach (var trackingInfo in _trackedWindows.Values)
                {
                    trackingInfo.IsInProximity = false;
                    trackingInfo.HoverStartTime = null;
                    trackingInfo.IsHoveringTimeout = false;
                    // Clear any existing tolerance state
                    trackingInfo.CtrlReleaseTime = null;
                    trackingInfo.CtrlReleaseCursorPosition = null;
                }
            }
            
            _wasCtrlPressedLastUpdate = true;
            return;
        }
        
        // Handle CTRL release - set tolerance state for windows under cursor
        if (wasCtrlJustReleased)
        {
            var ctrlReleaseTime = DateTime.UtcNow;
            lock (_windowTrackingLock)
            {
                foreach (var kvp in _trackedWindows)
                {
                    var trackingInfo = kvp.Value;
                    var windowBounds = trackingInfo.WindowInfo.Bounds;
                    
                    // Check if cursor is over this window
                    if (windowBounds.Contains(cursorPosition))
                    {
                        trackingInfo.CtrlReleaseTime = ctrlReleaseTime;
                        trackingInfo.CtrlReleaseCursorPosition = cursorPosition;
                        _logger.LogDebug("CTRL tolerance activated for window: {Title}", trackingInfo.WindowInfo.Title);
                    }
                }
            }
        }
        
        _wasCtrlPressedLastUpdate = isCtrlPressed;
        
        var currentTime = DateTime.UtcNow;
        var windowsToProcess = new List<(IntPtr Handle, WindowTrackingInfo Info)>();
        
        // Collect windows that need processing
        lock (_windowTrackingLock)
        {
            foreach (var kvp in _trackedWindows)
            {
                windowsToProcess.Add((kvp.Key, kvp.Value));
            }
        }
        
        // Process each window
        var pushTasks = new List<Task>();
        
        foreach (var (windowHandle, trackingInfo) in windowsToProcess)
        {
            try
            {
                await ProcessWindowAsync(windowHandle, trackingInfo, cursorPosition, currentTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing window {Handle:X}", windowHandle.ToInt64());
            }
        }
        
        // Periodically refresh window list (every 100 update cycles)
        if (_updateCount % 100 == 0)
        {
            _ = Task.Run(() => RefreshTrackedWindowsAsync());
        }
    }
    
    /// <summary>
    /// Processes a single window for proximity detection and pushing
    /// </summary>
    private async Task ProcessWindowAsync(IntPtr windowHandle, WindowTrackingInfo trackingInfo, Point cursorPosition, DateTime currentTime)
    {
        // Check if window is currently being animated
        if (_windowPusher.IsWindowAnimating(windowHandle))
        {
            // Reset proximity state during animation to prevent interference
            trackingInfo.IsInProximity = false;
            trackingInfo.HoverStartTime = null;
            trackingInfo.IsHoveringTimeout = false;
            return;
        }

        // Get per-monitor configuration values
        var monitorSettings = GetEffectiveMonitorSettings(trackingInfo.WindowInfo.Bounds);
        
        // Skip processing if per-monitor CursorPhobia is disabled for this monitor
        if (!monitorSettings.enabled)
        {
            // Reset proximity state when disabled
            trackingInfo.IsInProximity = false;
            trackingInfo.HoverStartTime = null;
            trackingInfo.IsHoveringTimeout = false;
            return;
        }
        
        int proximityThreshold = monitorSettings.proximityThreshold;
        int pushDistance = monitorSettings.pushDistance;
        int hoverTimeoutMs = monitorSettings.hoverTimeoutMs;
        bool enableHoverTimeout = monitorSettings.enableHoverTimeout;
        bool enableWrapping = monitorSettings.enableWrapping;
        WrapPreference wrapPreference = monitorSettings.wrapPreference;
        
        // Check proximity (with CTRL tolerance if active)
        var windowBounds = trackingInfo.WindowInfo.Bounds;
        bool isInProximity = false;
        
        // Check if window is in CTRL tolerance mode
        bool isInToleranceMode = trackingInfo.CtrlReleaseTime.HasValue && 
                                trackingInfo.CtrlReleaseCursorPosition.HasValue;
        
        if (isInToleranceMode)
        {
            // Calculate distance from the CTRL release position
            var toleranceDistance = _config.CtrlReleaseToleranceDistance;
            var releasePos = trackingInfo.CtrlReleaseCursorPosition!.Value;
            var currentDistance = Math.Sqrt(
                Math.Pow(cursorPosition.X - releasePos.X, 2) + 
                Math.Pow(cursorPosition.Y - releasePos.Y, 2));
            
            if (currentDistance <= toleranceDistance)
            {
                // Still within tolerance - suppress cursor phobia
                isInProximity = false;
                _logger.LogDebug("Window {Title} in CTRL tolerance mode - distance: {Distance:F1}px", 
                    trackingInfo.WindowInfo.Title, currentDistance);
            }
            else
            {
                // Moved beyond tolerance - clear tolerance and resume normal detection
                trackingInfo.CtrlReleaseTime = null;
                trackingInfo.CtrlReleaseCursorPosition = null;
                isInProximity = _proximityDetector.IsWithinProximity(
                    cursorPosition, windowBounds, proximityThreshold);
                _logger.LogDebug("CTRL tolerance cleared for window: {Title}", trackingInfo.WindowInfo.Title);
            }
        }
        else
        {
            // Normal proximity detection
            isInProximity = _proximityDetector.IsWithinProximity(
                cursorPosition, windowBounds, proximityThreshold);
        }
        
        trackingInfo.LastProximityCheckTime = currentTime;
        
        // Handle proximity state changes
        if (isInProximity && !trackingInfo.IsInProximity)
        {
            // Cursor entered proximity
            trackingInfo.IsInProximity = true;
            trackingInfo.HoverStartTime = enableHoverTimeout ? currentTime : null;
            trackingInfo.IsHoveringTimeout = false;
            
            _logger.LogDebug("Cursor entered proximity of window: {Title} ({Handle:X})", 
                trackingInfo.WindowInfo.Title, windowHandle.ToInt64());
        }
        else if (!isInProximity && trackingInfo.IsInProximity)
        {
            // Cursor left proximity
            trackingInfo.IsInProximity = false;
            trackingInfo.HoverStartTime = null;
            trackingInfo.IsHoveringTimeout = false;
            
            _logger.LogDebug("Cursor left proximity of window: {Title} ({Handle:X})", 
                trackingInfo.WindowInfo.Title, windowHandle.ToInt64());
        }
        else if (isInProximity && trackingInfo.IsInProximity)
        {
            // Check hover timeout
            if (enableHoverTimeout && 
                trackingInfo.HoverStartTime.HasValue && 
                !trackingInfo.IsHoveringTimeout)
            {
                var hoverDuration = currentTime - trackingInfo.HoverStartTime.Value;
                if (hoverDuration.TotalMilliseconds >= hoverTimeoutMs)
                {
                    trackingInfo.IsHoveringTimeout = true;
                    _logger.LogDebug("Hover timeout reached for window: {Title} ({Handle:X})", 
                        trackingInfo.WindowInfo.Title, windowHandle.ToInt64());
                }
            }
        }
        
        // Decide whether to push the window
        if (trackingInfo.IsInProximity && !trackingInfo.IsHoveringTimeout)
        {
            // Push the window away from the cursor
            var pushSuccess = await _windowPusher.PushWindowAsync(
                windowHandle, 
                cursorPosition, 
                pushDistance);
            
            if (pushSuccess)
            {
                _logger.LogDebug("Successfully pushed window: {Title} ({Handle:X})", 
                    trackingInfo.WindowInfo.Title, windowHandle.ToInt64());
                
                // Raise event
                WindowPushed?.Invoke(this, new WindowPushEventArgs(
                    trackingInfo.WindowInfo,
                    cursorPosition,
                    pushDistance));
            }
        }
    }
    
    /// <summary>
    /// Updates the engine configuration with hot-swapping support
    /// </summary>
    /// <param name="newConfiguration">The new configuration to apply</param>
    /// <returns>Result of the configuration update operation</returns>
    public async Task<ConfigurationUpdateResult> UpdateConfigurationAsync(CursorPhobiaConfiguration newConfiguration)
    {
        if (newConfiguration == null)
            throw new ArgumentNullException(nameof(newConfiguration));
            
        if (_disposed)
        {
            return ConfigurationUpdateResult.CreateFailure(
                "Cannot update configuration on disposed engine",
                new ConfigurationChangeAnalysis(_config, newConfiguration));
        }
        
        // Validate the new configuration
        var validationErrors = newConfiguration.Validate();
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("Configuration validation failed: {Errors}", string.Join(", ", validationErrors));
            return ConfigurationUpdateResult.CreateValidationFailure(
                new ConfigurationChangeAnalysis(_config, newConfiguration),
                validationErrors);
        }
        
        CursorPhobiaConfiguration oldConfiguration;
        ConfigurationChangeAnalysis changeAnalysis;
        
        // Analyze changes while holding the configuration lock
        lock (_configurationLock)
        {
            oldConfiguration = _config;
            changeAnalysis = new ConfigurationChangeAnalysis(_config, newConfiguration);
        }
        
        try
        {
            _logger.LogInformation("Processing configuration update: {Summary}", changeAnalysis.GetSummary());
            
            // If no changes detected, return success immediately
            if (!changeAnalysis.HasChanges)
            {
                _logger.LogDebug("No configuration changes detected");
                var noChangeResult = ConfigurationUpdateResult.CreateSuccess(changeAnalysis);
                
                // Still fire the event for consistency
                ConfigurationUpdated?.Invoke(this, new ConfigurationUpdatedEventArgs(
                    noChangeResult, newConfiguration, oldConfiguration));
                    
                return noChangeResult;
            }
            
            // Apply hot-swappable changes immediately
            var appliedChanges = new List<string>();
            var queuedForRestart = new List<string>();
            
            lock (_configurationLock)
            {
                // Create a copy of the current configuration to modify
                var updatedConfig = CloneConfiguration(_config);
                
                // Apply hot-swappable changes
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.ProximityThreshold)))
                {
                    updatedConfig.ProximityThreshold = newConfiguration.ProximityThreshold;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.ProximityThreshold));
                    _logger.LogDebug("Updated ProximityThreshold from {Old} to {New}", 
                        oldConfiguration.ProximityThreshold, newConfiguration.ProximityThreshold);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.PushDistance)))
                {
                    updatedConfig.PushDistance = newConfiguration.PushDistance;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.PushDistance));
                    _logger.LogDebug("Updated PushDistance from {Old} to {New}", 
                        oldConfiguration.PushDistance, newConfiguration.PushDistance);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableCtrlOverride)))
                {
                    updatedConfig.EnableCtrlOverride = newConfiguration.EnableCtrlOverride;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.EnableCtrlOverride));
                    _logger.LogDebug("Updated EnableCtrlOverride from {Old} to {New}", 
                        oldConfiguration.EnableCtrlOverride, newConfiguration.EnableCtrlOverride);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer)))
                {
                    updatedConfig.ScreenEdgeBuffer = newConfiguration.ScreenEdgeBuffer;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer));
                    _logger.LogDebug("Updated ScreenEdgeBuffer from {Old} to {New}", 
                        oldConfiguration.ScreenEdgeBuffer, newConfiguration.ScreenEdgeBuffer);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationDurationMs)))
                {
                    updatedConfig.AnimationDurationMs = newConfiguration.AnimationDurationMs;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.AnimationDurationMs));
                    _logger.LogDebug("Updated AnimationDurationMs from {Old} to {New}", 
                        oldConfiguration.AnimationDurationMs, newConfiguration.AnimationDurationMs);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableAnimations)))
                {
                    updatedConfig.EnableAnimations = newConfiguration.EnableAnimations;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.EnableAnimations));
                    _logger.LogDebug("Updated EnableAnimations from {Old} to {New}", 
                        oldConfiguration.EnableAnimations, newConfiguration.EnableAnimations);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationEasing)))
                {
                    updatedConfig.AnimationEasing = newConfiguration.AnimationEasing;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.AnimationEasing));
                    _logger.LogDebug("Updated AnimationEasing from {Old} to {New}", 
                        oldConfiguration.AnimationEasing, newConfiguration.AnimationEasing);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.HoverTimeoutMs)))
                {
                    updatedConfig.HoverTimeoutMs = newConfiguration.HoverTimeoutMs;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.HoverTimeoutMs));
                    _logger.LogDebug("Updated HoverTimeoutMs from {Old} to {New}", 
                        oldConfiguration.HoverTimeoutMs, newConfiguration.HoverTimeoutMs);
                }
                
                if (changeAnalysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout)))
                {
                    updatedConfig.EnableHoverTimeout = newConfiguration.EnableHoverTimeout;
                    appliedChanges.Add(nameof(CursorPhobiaConfiguration.EnableHoverTimeout));
                    _logger.LogDebug("Updated EnableHoverTimeout from {Old} to {New}", 
                        oldConfiguration.EnableHoverTimeout, newConfiguration.EnableHoverTimeout);
                        
                    // Reset hover states for all windows if hover timeout was disabled
                    if (!newConfiguration.EnableHoverTimeout)
                    {
                        lock (_windowTrackingLock)
                        {
                            foreach (var trackingInfo in _trackedWindows.Values)
                            {
                                trackingInfo.HoverStartTime = null;
                                trackingInfo.IsHoveringTimeout = false;
                            }
                        }
                        _logger.LogDebug("Reset hover states for all windows due to hover timeout being disabled");
                    }
                }
                
                // Apply the updated configuration
                _config = updatedConfig;
            }
            
            // Log restart-required changes
            queuedForRestart.AddRange(changeAnalysis.RestartRequiredChanges);
            if (queuedForRestart.Count > 0)
            {
                _logger.LogInformation("Configuration changes require engine restart: {Changes}", 
                    string.Join(", ", queuedForRestart));
            }
            
            var result = ConfigurationUpdateResult.CreateSuccess(changeAnalysis, appliedChanges, queuedForRestart);
            
            _logger.LogInformation("Configuration update completed: {Summary}", result.GetSummary());
            
            // Fire configuration updated event
            ConfigurationUpdated?.Invoke(this, new ConfigurationUpdatedEventArgs(
                result, newConfiguration, oldConfiguration));
            
            await Task.CompletedTask;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration update");
            var errorResult = ConfigurationUpdateResult.CreateFailure(
                $"Configuration update failed: {ex.Message}",
                changeAnalysis);
            
            // Fire configuration updated event even for failures
            ConfigurationUpdated?.Invoke(this, new ConfigurationUpdatedEventArgs(
                errorResult, newConfiguration, oldConfiguration));
            
            return errorResult;
        }
    }
    
    /// <summary>
    /// Creates a deep copy of the configuration for thread-safe updates
    /// </summary>
    /// <param name="original">Configuration to clone</param>
    /// <returns>Cloned configuration</returns>
    private CursorPhobiaConfiguration CloneConfiguration(CursorPhobiaConfiguration original)
    {
        var cloned = new CursorPhobiaConfiguration
        {
            ProximityThreshold = original.ProximityThreshold,
            PushDistance = original.PushDistance,
            UpdateIntervalMs = original.UpdateIntervalMs,
            MaxUpdateIntervalMs = original.MaxUpdateIntervalMs,
            EnableCtrlOverride = original.EnableCtrlOverride,
            ScreenEdgeBuffer = original.ScreenEdgeBuffer,
            ApplyToAllWindows = original.ApplyToAllWindows,
            AnimationDurationMs = original.AnimationDurationMs,
            EnableAnimations = original.EnableAnimations,
            AnimationEasing = original.AnimationEasing,
            HoverTimeoutMs = original.HoverTimeoutMs,
            EnableHoverTimeout = original.EnableHoverTimeout,
            MultiMonitor = CloneMultiMonitorConfiguration(original.MultiMonitor)
        };
        
        return cloned;
    }
    
    /// <summary>
    /// Creates a deep copy of multi-monitor configuration
    /// </summary>
    /// <param name="original">Original multi-monitor configuration</param>
    /// <returns>Cloned multi-monitor configuration</returns>
    private MultiMonitorConfiguration? CloneMultiMonitorConfiguration(MultiMonitorConfiguration? original)
    {
        if (original == null) return null;
        
        var cloned = new MultiMonitorConfiguration
        {
            EnableWrapping = original.EnableWrapping,
            PreferredWrapBehavior = original.PreferredWrapBehavior,
            RespectTaskbarAreas = original.RespectTaskbarAreas,
            PerMonitorSettings = new Dictionary<string, PerMonitorSettings>()
        };
        
        foreach (var kvp in original.PerMonitorSettings)
        {
            if (kvp.Value != null)
            {
                cloned.PerMonitorSettings[kvp.Key] = new PerMonitorSettings
                {
                    Enabled = kvp.Value.Enabled,
                    CustomProximityThreshold = kvp.Value.CustomProximityThreshold,
                    CustomPushDistance = kvp.Value.CustomPushDistance
                };
            }
        }
        
        return cloned;
    }
    
    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    /// <returns>Performance statistics object</returns>
    public EnginePerformanceStats GetPerformanceStats()
    {
        CursorPhobiaConfiguration currentConfig;
        lock (_configurationLock)
        {
            currentConfig = _config;
        }
        
        return new EnginePerformanceStats
        {
            IsRunning = IsRunning,
            UptimeMs = _performanceStopwatch.ElapsedMilliseconds,
            UpdateCount = _updateCount,
            AverageUpdateTimeMs = AverageUpdateTimeMs,
            TrackedWindowCount = TrackedWindowCount,
            ConfiguredUpdateIntervalMs = currentConfig.UpdateIntervalMs,
            SuccessfulUpdates = _successfulUpdates,
            FailedUpdates = _failedUpdates
        };
    }
    
    /// <summary>
    /// Gets the effective monitor settings for a given window bounds
    /// Combines global settings with per-monitor overrides
    /// </summary>
    /// <param name="windowBounds">Window bounds to find the monitor for</param>
    /// <returns>Effective settings for the monitor containing the window</returns>
    private (int proximityThreshold, int pushDistance, int hoverTimeoutMs, bool enableHoverTimeout, bool enabled, bool enableWrapping, WrapPreference wrapPreference) GetEffectiveMonitorSettings(Rectangle windowBounds)
    {
        CursorPhobiaConfiguration currentConfig;
        lock (_configurationLock)
        {
            currentConfig = _config;
        }
        
        // Get the monitor containing this window
        var monitor = _monitorManager.GetMonitorContaining(windowBounds);
        if (monitor == null)
        {
            // Fallback to global settings if monitor not found
            return (
                currentConfig.ProximityThreshold,
                currentConfig.PushDistance,
                currentConfig.HoverTimeoutMs,
                currentConfig.EnableHoverTimeout,
                true,
                currentConfig.MultiMonitor?.EnableWrapping ?? true,
                currentConfig.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart
            );
        }
        
        // Check for per-monitor settings
        var monitorKey = monitor.GetStableKey();
        if (currentConfig.MultiMonitor?.PerMonitorSettings?.TryGetValue(monitorKey, out var perMonitorSettings) == true)
        {
            // Apply per-monitor overrides
            var proximityThreshold = perMonitorSettings.CustomProximityThreshold ?? currentConfig.ProximityThreshold;
            var pushDistance = perMonitorSettings.CustomPushDistance ?? currentConfig.PushDistance;
            var enableWrapping = perMonitorSettings.CustomEnableWrapping ?? currentConfig.MultiMonitor?.EnableWrapping ?? true;
            var wrapPreference = perMonitorSettings.CustomWrapPreference ?? currentConfig.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart;
            
            return (
                proximityThreshold,
                pushDistance,
                currentConfig.HoverTimeoutMs, // No per-monitor override for hover timeout yet
                currentConfig.EnableHoverTimeout, // No per-monitor override for hover timeout yet
                perMonitorSettings.Enabled,
                enableWrapping,
                wrapPreference
            );
        }
        
        // Return global settings as fallback
        return (
            currentConfig.ProximityThreshold,
            currentConfig.PushDistance,
            currentConfig.HoverTimeoutMs,
            currentConfig.EnableHoverTimeout,
            true,
            currentConfig.MultiMonitor?.EnableWrapping ?? true,
            currentConfig.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart
        );
    }
    
    /// <summary>
    /// Gets the effective wrap behavior for a window on a specific monitor
    /// </summary>
    /// <param name="windowBounds">Window bounds to determine monitor</param>
    /// <returns>Wrap behavior configuration for the monitor</returns>
    public WrapBehavior GetEffectiveWrapBehavior(Rectangle windowBounds)
    {
        var settings = GetEffectiveMonitorSettings(windowBounds);
        
        return new WrapBehavior
        {
            EnableWrapping = settings.enableWrapping,
            PreferredBehavior = settings.wrapPreference,
            RespectTaskbarAreas = true // Always respect taskbar areas for safety
        };
    }
    
    /// <summary>
    /// Disposes the engine and releases all resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _logger.LogInformation("Disposing CursorPhobiaEngine");
        
        try
        {
            // Stop the engine if it's running
            Task.Run(StopAsync).Wait(TimeSpan.FromSeconds(5));
            
            // Dispose resources
            _updateTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _performanceStopwatch?.Stop();
            
            _disposed = true;
            _logger.LogDebug("CursorPhobiaEngine disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CursorPhobiaEngine disposal");
        }
        
        GC.SuppressFinalize(this);
    }
}


/// <summary>
/// Tracking information for a window managed by the engine
/// </summary>
internal class WindowTrackingInfo
{
    /// <summary>
    /// The window information
    /// </summary>
    public WindowInfo WindowInfo { get; set; } = null!;
    
    /// <summary>
    /// When this window was first discovered and tracked
    /// </summary>
    public DateTime FirstSeenTime { get; set; }
    
    /// <summary>
    /// Last time proximity was checked for this window
    /// </summary>
    public DateTime LastProximityCheckTime { get; set; }
    
    /// <summary>
    /// Time when cursor first entered proximity, null if not in proximity
    /// </summary>
    public DateTime? HoverStartTime { get; set; }
    
    /// <summary>
    /// Whether cursor is currently in proximity of this window
    /// </summary>
    public bool IsInProximity { get; set; }
    
    /// <summary>
    /// Whether hover timeout has been reached for this window
    /// </summary>
    public bool IsHoveringTimeout { get; set; }
    
    /// <summary>
    /// Time when CTRL was released while cursor was over this window
    /// Used for implementing tolerance period after CTRL release
    /// </summary>
    public DateTime? CtrlReleaseTime { get; set; }
    
    /// <summary>
    /// Cursor position when CTRL was released over this window
    /// Used to calculate tolerance distance
    /// </summary>
    public Point? CtrlReleaseCursorPosition { get; set; }
}

/// <summary>
/// Event arguments for window push events
/// </summary>
public class WindowPushEventArgs : EventArgs
{
    /// <summary>
    /// The window that was pushed
    /// </summary>
    public WindowInfo WindowInfo { get; }
    
    /// <summary>
    /// The cursor position that triggered the push
    /// </summary>
    public Point CursorPosition { get; }
    
    /// <summary>
    /// The distance the window was pushed
    /// </summary>
    public int PushDistance { get; }
    
    /// <summary>
    /// Creates new window push event arguments
    /// </summary>
    /// <param name="windowInfo">The window that was pushed</param>
    /// <param name="cursorPosition">The cursor position that triggered the push</param>
    /// <param name="pushDistance">The distance the window was pushed</param>
    public WindowPushEventArgs(WindowInfo windowInfo, Point cursorPosition, int pushDistance)
    {
        WindowInfo = windowInfo;
        CursorPosition = cursorPosition;
        PushDistance = pushDistance;
    }
}

/// <summary>
/// Performance statistics for the CursorPhobia engine
/// </summary>
public class EnginePerformanceStats
{
    /// <summary>
    /// Whether the engine is currently running
    /// </summary>
    public bool IsRunning { get; set; }
    
    /// <summary>
    /// Engine uptime in milliseconds
    /// </summary>
    public long UptimeMs { get; set; }
    
    /// <summary>
    /// Total number of update cycles processed
    /// </summary>
    public long UpdateCount { get; set; }
    
    /// <summary>
    /// Average time per update cycle in milliseconds
    /// </summary>
    public double AverageUpdateTimeMs { get; set; }
    
    /// <summary>
    /// Number of windows currently being tracked
    /// </summary>
    public int TrackedWindowCount { get; set; }
    
    /// <summary>
    /// Configured update interval in milliseconds
    /// </summary>
    public int ConfiguredUpdateIntervalMs { get; set; }
    
    /// <summary>
    /// Total number of successful update cycles
    /// </summary>
    public long SuccessfulUpdates { get; set; }
    
    /// <summary>
    /// Total number of failed update cycles
    /// </summary>
    public long FailedUpdates { get; set; }
    
    /// <summary>
    /// Total number of update cycles (successful + failed)
    /// </summary>
    public long TotalUpdates => SuccessfulUpdates + FailedUpdates;
    
    /// <summary>
    /// Calculated updates per second based on configured interval
    /// </summary>
    public double UpdatesPerSecond => ConfiguredUpdateIntervalMs > 0 ? 1000.0 / ConfiguredUpdateIntervalMs : 0;
    
    /// <summary>
    /// Estimated CPU usage percentage (very rough estimate)
    /// </summary>
    public double EstimatedCpuUsagePercent => AverageUpdateTimeMs > 0 ? (AverageUpdateTimeMs / ConfiguredUpdateIntervalMs) * 100 : 0;
}

/// <summary>
/// Enumeration of possible engine states for tray notifications
/// </summary>
public enum EngineState
{
    /// <summary>
    /// Engine is stopped or not initialized
    /// </summary>
    Stopped,
    
    /// <summary>
    /// Engine is running normally
    /// </summary>
    Running,
    
    /// <summary>
    /// Engine encountered an error
    /// </summary>
    Error
}

/// <summary>
/// Event arguments for engine state change events
/// </summary>
public class EngineStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The new engine state
    /// </summary>
    public EngineState State { get; }
    
    /// <summary>
    /// Optional message describing the state change
    /// </summary>
    public string? Message { get; }
    
    /// <summary>
    /// Timestamp when the state change occurred
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Creates new engine state changed event arguments
    /// </summary>
    /// <param name="state">The new engine state</param>
    /// <param name="message">Optional message describing the state change</param>
    public EngineStateChangedEventArgs(EngineState state, string? message = null)
    {
        State = state;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for engine performance issue events
/// </summary>
public class EnginePerformanceEventArgs : EventArgs
{
    /// <summary>
    /// Title/type of the performance issue
    /// </summary>
    public string IssueType { get; }
    
    /// <summary>
    /// Detailed description of the performance issue
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Performance metric value (e.g., update time in ms)
    /// </summary>
    public double MetricValue { get; }
    
    /// <summary>
    /// Timestamp when the performance issue was detected
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Creates new engine performance event arguments
    /// </summary>
    /// <param name="issueType">Title/type of the performance issue</param>
    /// <param name="description">Detailed description of the performance issue</param>
    /// <param name="metricValue">Performance metric value</param>
    public EnginePerformanceEventArgs(string issueType, string description, double metricValue)
    {
        IssueType = issueType ?? throw new ArgumentNullException(nameof(issueType));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        MetricValue = metricValue;
        Timestamp = DateTime.UtcNow;
    }
}