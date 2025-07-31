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
    private readonly CursorPhobiaConfiguration _config;
    
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
    
    // Performance monitoring
    private readonly Stopwatch _performanceStopwatch = Stopwatch.StartNew();
    private long _updateCount = 0;
    private long _totalUpdateTimeMs = 0;
    
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
    /// Creates a new CursorPhobiaEngine instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="cursorTracker">Service for tracking cursor position and key states</param>
    /// <param name="proximityDetector">Service for calculating proximity between cursor and windows</param>
    /// <param name="windowDetectionService">Service for finding and monitoring windows</param>
    /// <param name="windowPusher">Service for moving windows with animation</param>
    /// <param name="safetyManager">Service for validating window positions</param>
    /// <param name="config">Configuration for engine behavior</param>
    public CursorPhobiaEngine(
        ILogger logger,
        ICursorTracker cursorTracker,
        IProximityDetector proximityDetector,
        IWindowDetectionService windowDetectionService,
        IWindowPusher windowPusher,
        ISafetyManager safetyManager,
        CursorPhobiaConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cursorTracker = cursorTracker ?? throw new ArgumentNullException(nameof(cursorTracker));
        _proximityDetector = proximityDetector ?? throw new ArgumentNullException(nameof(proximityDetector));
        _windowDetectionService = windowDetectionService ?? throw new ArgumentNullException(nameof(windowDetectionService));
        _windowPusher = windowPusher ?? throw new ArgumentNullException(nameof(windowPusher));
        _safetyManager = safetyManager ?? throw new ArgumentNullException(nameof(safetyManager));
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
            }
            
            _logger.LogInformation("CursorPhobiaEngine started successfully. Tracking {WindowCount} windows", 
                _trackedWindows.Count);
            
            EngineStarted?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting CursorPhobiaEngine");
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping CursorPhobiaEngine");
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
            
            var discoveredWindows = _config.ApplyToAllWindows 
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
        
        try
        {
            await ProcessUpdateCycleAsync();
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
            
            // Log performance warnings if updates are taking too long
            if (updateStartTime.ElapsedMilliseconds > _config.MaxUpdateIntervalMs)
            {
                _logger.LogWarning("Update cycle took {Duration}ms, exceeding max interval of {MaxInterval}ms", 
                    updateStartTime.ElapsedMilliseconds, _config.MaxUpdateIntervalMs);
            }
        }
    }
    
    /// <summary>
    /// Processes a single update cycle
    /// </summary>
    private async Task ProcessUpdateCycleAsync()
    {
        // Check if CTRL override is active
        if (_config.EnableCtrlOverride && _cursorTracker.IsCtrlKeyPressed())
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
                }
            }
            
            return;
        }
        
        // Get current cursor position
        var cursorPosition = _cursorTracker.GetCurrentCursorPosition();
        if (cursorPosition.IsEmpty)
        {
            _logger.LogWarning("Failed to get cursor position during update cycle");
            return;
        }
        
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
        
        // Check proximity
        var windowBounds = trackingInfo.WindowInfo.Bounds;
        var isInProximity = _proximityDetector.IsWithinProximity(
            cursorPosition, 
            windowBounds, 
            _config.ProximityThreshold);
        
        trackingInfo.LastProximityCheckTime = currentTime;
        
        // Handle proximity state changes
        if (isInProximity && !trackingInfo.IsInProximity)
        {
            // Cursor entered proximity
            trackingInfo.IsInProximity = true;
            trackingInfo.HoverStartTime = _config.EnableHoverTimeout ? currentTime : null;
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
            if (_config.EnableHoverTimeout && 
                trackingInfo.HoverStartTime.HasValue && 
                !trackingInfo.IsHoveringTimeout)
            {
                var hoverDuration = currentTime - trackingInfo.HoverStartTime.Value;
                if (hoverDuration.TotalMilliseconds >= _config.HoverTimeoutMs)
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
                _config.PushDistance);
            
            if (pushSuccess)
            {
                _logger.LogDebug("Successfully pushed window: {Title} ({Handle:X})", 
                    trackingInfo.WindowInfo.Title, windowHandle.ToInt64());
                
                // Raise event
                WindowPushed?.Invoke(this, new WindowPushEventArgs(
                    trackingInfo.WindowInfo,
                    cursorPosition,
                    _config.PushDistance));
            }
        }
    }
    
    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    /// <returns>Performance statistics object</returns>
    public EnginePerformanceStats GetPerformanceStats()
    {
        return new EnginePerformanceStats
        {
            IsRunning = IsRunning,
            UptimeMs = _performanceStopwatch.ElapsedMilliseconds,
            UpdateCount = _updateCount,
            AverageUpdateTimeMs = AverageUpdateTimeMs,
            TrackedWindowCount = TrackedWindowCount,
            ConfiguredUpdateIntervalMs = _config.UpdateIntervalMs
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
/// Interface for the main CursorPhobia engine
/// </summary>
public interface ICursorPhobiaEngine
{
    /// <summary>
    /// Gets whether the engine is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Gets the number of windows currently being tracked
    /// </summary>
    int TrackedWindowCount { get; }
    
    /// <summary>
    /// Gets the average update cycle time in milliseconds
    /// </summary>
    double AverageUpdateTimeMs { get; }
    
    /// <summary>
    /// Event raised when the engine starts
    /// </summary>
    event EventHandler? EngineStarted;
    
    /// <summary>
    /// Event raised when the engine stops
    /// </summary>
    event EventHandler? EngineStopped;
    
    /// <summary>
    /// Event raised when a window push operation occurs
    /// </summary>
    event EventHandler<WindowPushEventArgs>? WindowPushed;
    
    /// <summary>
    /// Starts the cursor phobia engine
    /// </summary>
    /// <returns>True if started successfully, false otherwise</returns>
    Task<bool> StartAsync();
    
    /// <summary>
    /// Stops the cursor phobia engine
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Forces a refresh of the tracked windows list
    /// </summary>
    Task RefreshTrackedWindowsAsync();
    
    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    /// <returns>Performance statistics object</returns>
    EnginePerformanceStats GetPerformanceStats();
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
    /// Calculated updates per second based on configured interval
    /// </summary>
    public double UpdatesPerSecond => ConfiguredUpdateIntervalMs > 0 ? 1000.0 / ConfiguredUpdateIntervalMs : 0;
    
    /// <summary>
    /// Estimated CPU usage percentage (very rough estimate)
    /// </summary>
    public double EstimatedCpuUsagePercent => AverageUpdateTimeMs > 0 ? (AverageUpdateTimeMs / ConfiguredUpdateIntervalMs) * 100 : 0;
}