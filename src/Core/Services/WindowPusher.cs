using System.Collections.Concurrent;
using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using System.Diagnostics;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for window pushing and animation services
/// </summary>
public interface IWindowPusher
{
    /// <summary>
    /// Pushes a window away from the cursor with smooth animation
    /// </summary>
    /// <param name="windowHandle">Handle to the window to push</param>
    /// <param name="cursorPosition">Current cursor position</param>
    /// <param name="pushDistance">Distance to push the window</param>
    /// <returns>True if the push was initiated successfully</returns>
    Task<bool> PushWindowAsync(IntPtr windowHandle, Point cursorPosition, int pushDistance);

    /// <summary>
    /// Pushes a window to a specific target position with animation
    /// </summary>
    /// <param name="windowHandle">Handle to the window to push</param>
    /// <param name="targetPosition">Target position for the window</param>
    /// <returns>True if the push was initiated successfully</returns>
    Task<bool> PushWindowToPositionAsync(IntPtr windowHandle, Point targetPosition);

    /// <summary>
    /// Checks if a window is currently being animated
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <returns>True if the window is currently being animated</returns>
    bool IsWindowAnimating(IntPtr windowHandle);

    /// <summary>
    /// Cancels any ongoing animation for a specific window
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    void CancelWindowAnimation(IntPtr windowHandle);

    /// <summary>
    /// Cancels all ongoing window animations
    /// </summary>
    void CancelAllAnimations();
}

/// <summary>
/// Service for pushing windows away from the cursor with smooth animations
/// Enhanced with Phase 3 WI#8: Hook recovery and error handling
/// </summary>
public class WindowPusher : IWindowPusher, IDisposable
{
    private readonly ILogger _logger;
    private readonly IWindowManipulationService _windowService;
    private readonly ISafetyManager _safetyManager;
    private readonly IProximityDetector _proximityDetector;
    private readonly IWindowDetectionService _windowDetectionService;
    private readonly CursorPhobiaConfiguration _config;
    private readonly MonitorManager _monitorManager;
    private readonly EdgeWrapHandler _edgeWrapHandler;
    private readonly IErrorRecoveryManager? _errorRecoveryManager;
    private readonly ISystemTrayManager? _trayManager;
    private readonly ITransitionFeedbackService? _transitionFeedbackService;

    // Animation tracking with improved thread safety
    private readonly ConcurrentDictionary<IntPtr, WindowAnimation> _activeAnimations = new();
    private readonly SemaphoreSlim _animationSemaphore = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private volatile bool _disposed = false;
    
    // Spatial caching for performance
    private readonly ConcurrentDictionary<IntPtr, CachedWindowPosition> _positionCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const int CacheExpiryMs = 100; // Cache positions for 100ms

    // Phase 3 WI#8: Hook recovery tracking
    private readonly object _hookRecoveryLock = new();
    private DateTime _lastHookFailure = DateTime.MinValue;
    private int _consecutiveHookFailures = 0;
    private bool _hookRecoveryInProgress = false;
    private const int MaxConsecutiveHookFailures = 3;
    private const int HookRecoveryTimeoutMs = 5000; // 5 seconds

    /// <summary>
    /// Creates a new WindowPusher instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="windowService">Service for window manipulation</param>
    /// <param name="safetyManager">Safety manager for boundary validation</param>
    /// <param name="proximityDetector">Proximity detector for push calculations</param>
    /// <param name="windowDetectionService">Service for detecting window properties</param>
    /// <param name="monitorManager">Monitor manager for multi-monitor support</param>
    /// <param name="edgeWrapHandler">Edge wrap handler for screen boundary wrapping</param>
    /// <param name="config">Configuration for animation and behavior settings</param>
    /// <param name="errorRecoveryManager">Optional error recovery manager for hook recovery</param>
    /// <param name="trayManager">Optional system tray manager for user notifications</param>
    /// <param name="transitionFeedbackService">Optional transition feedback service for visual feedback</param>
    public WindowPusher(
        ILogger logger,
        IWindowManipulationService windowService,
        ISafetyManager safetyManager,
        IProximityDetector proximityDetector,
        IWindowDetectionService windowDetectionService,
        MonitorManager monitorManager,
        EdgeWrapHandler edgeWrapHandler,
        CursorPhobiaConfiguration? config = null,
        IErrorRecoveryManager? errorRecoveryManager = null,
        ISystemTrayManager? trayManager = null,
        ITransitionFeedbackService? transitionFeedbackService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _safetyManager = safetyManager ?? throw new ArgumentNullException(nameof(safetyManager));
        _proximityDetector = proximityDetector ?? throw new ArgumentNullException(nameof(proximityDetector));
        _windowDetectionService = windowDetectionService ?? throw new ArgumentNullException(nameof(windowDetectionService));
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
        _edgeWrapHandler = edgeWrapHandler ?? throw new ArgumentNullException(nameof(edgeWrapHandler));
        _config = config ?? CursorPhobiaConfiguration.CreateDefault();
        _errorRecoveryManager = errorRecoveryManager;
        _trayManager = trayManager;
        _transitionFeedbackService = transitionFeedbackService;

        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }

        // Initialize concurrent collections - no need for explicit initialization

        // Phase 3 WI#8: Register with error recovery manager for hook failures
        if (_errorRecoveryManager != null)
        {
            Task.Run(async () => await RegisterForErrorRecoveryAsync());
        }

        _logger.LogDebug("WindowPusher initialized with animations {Enabled}, duration {Duration}ms, easing {Easing}, hook recovery {HookRecovery}",
            HardcodedDefaults.EnableAnimations, HardcodedDefaults.AnimationDurationMs, HardcodedDefaults.AnimationEasing, _errorRecoveryManager != null);
    }

    /// <summary>
    /// Pushes a window away from the cursor with smooth animation
    /// </summary>
    /// <param name="windowHandle">Handle to the window to push</param>
    /// <param name="cursorPosition">Current cursor position</param>
    /// <param name="pushDistance">Distance to push the window</param>
    /// <returns>True if the push was initiated successfully</returns>
    public async Task<bool> PushWindowAsync(IntPtr windowHandle, Point cursorPosition, int pushDistance)
    {
        if (windowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to PushWindowAsync");
            return false;
        }

        if (pushDistance <= 0)
        {
            _logger.LogWarning("Invalid push distance: {Distance}. Must be greater than 0", pushDistance);
            return false;
        }

        // Phase 2 WI#8: Performance logging for window push operations
        using var perfScope = _logger is Logger loggerWithPerf ?
            loggerWithPerf.BeginPerformanceScope("PushWindow",
                ("WindowHandle", $"0x{windowHandle:X}"),
                ("CursorX", cursorPosition.X),
                ("CursorY", cursorPosition.Y),
                ("PushDistance", pushDistance)) :
            null;

        try
        {
            // Get current window bounds
            var currentBounds = _windowService.GetWindowBounds(windowHandle);
            if (currentBounds.IsEmpty)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("Could not get window bounds");
                _logger.LogWarning("Could not get bounds for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }

            (perfScope as IPerformanceScope)?.AddContext("CurrentX", currentBounds.X);
            (perfScope as IPerformanceScope)?.AddContext("CurrentY", currentBounds.Y);
            (perfScope as IPerformanceScope)?.AddContext("WindowWidth", currentBounds.Width);
            (perfScope as IPerformanceScope)?.AddContext("WindowHeight", currentBounds.Height);

            // Get DPI information for the current window's monitor for proper scaling
            var currentMonitorDpi = _monitorManager.GetDpiForRectangle(currentBounds);
            var scaledPushDistance = currentMonitorDpi.ScaleDistance(pushDistance);

            // Calculate push vector using proximity detector with DPI-scaled distance
            var pushVector = _proximityDetector.CalculatePushVector(cursorPosition, currentBounds, scaledPushDistance);
            if (pushVector.IsEmpty)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("Could not calculate push vector");
                _logger.LogWarning("Could not calculate push vector for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }

            (perfScope as IPerformanceScope)?.AddContext("PushVectorX", pushVector.X);
            (perfScope as IPerformanceScope)?.AddContext("PushVectorY", pushVector.Y);
            (perfScope as IPerformanceScope)?.AddContext("DpiScaleFactor", currentMonitorDpi.ScaleFactor);
            (perfScope as IPerformanceScope)?.AddContext("ScaledPushDistance", scaledPushDistance);

            // Calculate initial target position
            var initialTargetPosition = new Point(
                currentBounds.X + pushVector.X,
                currentBounds.Y + pushVector.Y
            );

            // Check if this is an always-on-top window
            bool isTopmost = _windowDetectionService.IsWindowAlwaysOnTop(windowHandle);

            // Check for edge wrapping
            var wrapBehavior = new WrapBehavior
            {
                EnableWrapping = isTopmost ? true : (_config.MultiMonitor?.EnableWrapping ?? true),
                PreferredBehavior = isTopmost ? WrapPreference.Opposite : (_config.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart),
                RespectTaskbarAreas = _config.MultiMonitor?.RespectTaskbarAreas ?? true
            };

            if (isTopmost)
            {
                _logger.LogDebug("Always-on-top window {Handle:X} - enabling opposite edge teleportation", windowHandle.ToInt64());
            }

            var wrapDestination = _edgeWrapHandler.CalculateWrapDestination(currentBounds, pushVector, wrapBehavior);
            if (wrapDestination.HasValue && _edgeWrapHandler.IsWrapSafe(currentBounds.Location, wrapDestination.Value, currentBounds.Size))
            {
                _logger.LogDebug("Edge wrapping detected for window {Handle:X} from ({CurrentX},{CurrentY}) to ({WrapX},{WrapY})",
                    windowHandle.ToInt64(), currentBounds.X, currentBounds.Y, wrapDestination.Value.X, wrapDestination.Value.Y);
                initialTargetPosition = wrapDestination.Value;
            }

            // Apply cross-monitor DPI scaling if the target position is on a different monitor
            var targetPosition = ApplyCrossMonitorDpiScaling(currentBounds, initialTargetPosition, currentMonitorDpi);

            // Validate the target position with safety manager (now supports multi-monitor transitions)
            var safePosition = _safetyManager.ValidateWindowPosition(currentBounds, targetPosition);

            // Check if the window was constrained by safety manager (hit an edge and can't move further)
            // This now only occurs at the overall desktop boundaries, not individual monitor boundaries
            if (!targetPosition.Equals(safePosition))
            {
                // Detect which edge the window hit
                var constrainedEdge = DetectConstrainedEdge(currentBounds, targetPosition, safePosition);
                if (constrainedEdge != EdgeType.None)
                {
                    _logger.LogDebug("Window {Handle:X} constrained at {Edge} edge, attempting wrap",
                        windowHandle.ToInt64(), constrainedEdge);

                    // Try to wrap to opposite edge
                    var constrainedWrapBehavior = new WrapBehavior
                    {
                        EnableWrapping = true,
                        PreferredBehavior = WrapPreference.Opposite, // Force opposite edge wrap
                        RespectTaskbarAreas = _config.MultiMonitor?.RespectTaskbarAreas ?? true
                    };

                    var constrainedWrapDestination = _edgeWrapHandler.CalculateWrapDestinationForConstrainedWindow(
                        currentBounds, constrainedEdge, constrainedWrapBehavior);

                    if (constrainedWrapDestination.HasValue &&
                        _edgeWrapHandler.IsWrapSafe(currentBounds.Location, constrainedWrapDestination.Value, currentBounds.Size))
                    {
                        _logger.LogDebug("Edge constraint wrap detected for window {Handle:X} from ({CurrentX},{CurrentY}) to ({WrapX},{WrapY})",
                            windowHandle.ToInt64(), currentBounds.X, currentBounds.Y,
                            constrainedWrapDestination.Value.X, constrainedWrapDestination.Value.Y);
                        safePosition = constrainedWrapDestination.Value;
                    }
                }
            }

            // Log cross-monitor transition information and handle visual feedback
            var currentMonitor = _monitorManager.GetMonitorContaining(currentBounds);
            var targetMonitor = _monitorManager.GetMonitorContaining(new Rectangle(safePosition.X, safePosition.Y, currentBounds.Width, currentBounds.Height));
            var isMultiMonitorMove = currentMonitor?.monitorHandle != targetMonitor?.monitorHandle;

            _logger.LogDebug("Pushing window {Handle:X} from ({CurrentX},{CurrentY}) to ({TargetX},{TargetY}) -> ({SafeX},{SafeY}) " +
                           "[Cross-monitor: {IsMultiMonitor}]",
                windowHandle.ToInt64(), currentBounds.X, currentBounds.Y,
                targetPosition.X, targetPosition.Y, safePosition.X, safePosition.Y, isMultiMonitorMove);

            // Show visual feedback for cross-monitor transitions if enabled
            if (isMultiMonitorMove && 
                currentMonitor != null && 
                targetMonitor != null && 
                _config.MultiMonitor?.ShowTransitionFeedback == true &&
                _transitionFeedbackService != null)
            {
                var feedbackType = _config.MultiMonitor.FeedbackType;
                var feedbackDuration = _config.MultiMonitor.TransitionFeedbackDurationMs;
                
                // Start feedback asynchronously - don't await to avoid blocking the window move
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _transitionFeedbackService.ShowTransitionFeedbackAsync(
                            windowHandle, currentMonitor, targetMonitor, feedbackType, feedbackDuration);
                    }
                    catch (Exception feedbackEx)
                    {
                        _logger.LogWarning("Error showing transition feedback: {Message}", feedbackEx.Message);
                    }
                });
            }

            (perfScope as IPerformanceScope)?.AddContext("TargetX", targetPosition.X);
            (perfScope as IPerformanceScope)?.AddContext("TargetY", targetPosition.Y);
            (perfScope as IPerformanceScope)?.AddContext("SafeX", safePosition.X);
            (perfScope as IPerformanceScope)?.AddContext("SafeY", safePosition.Y);
            (perfScope as IPerformanceScope)?.AddContext("IsMultiMonitorMove", isMultiMonitorMove);

            var result = await PushWindowToPositionAsync(windowHandle, safePosition);
            if (!result)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("PushWindowToPositionAsync failed");
            }
            else
            {
                // Phase 3 WI#8: Report successful hook operation
                await ReportHookSuccessAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            (perfScope as IPerformanceScope)?.MarkAsFailed(ex.Message);
            _logger.LogError("Error pushing window {Handle:X} away from cursor ({CursorX},{CursorY}): {Message}",
                windowHandle.ToInt64(), cursorPosition.X, cursorPosition.Y, ex.Message);

            // Phase 3 WI#8: Report hook failure for recovery
            await ReportHookFailureAsync("PushWindowAsync", ex);

            return false;
        }
    }

    /// <summary>
    /// Pushes a window to a specific target position with animation
    /// </summary>
    /// <param name="windowHandle">Handle to the window to push</param>
    /// <param name="targetPosition">Target position for the window</param>
    /// <returns>True if the push was initiated successfully</returns>
    public async Task<bool> PushWindowToPositionAsync(IntPtr windowHandle, Point targetPosition)
    {
        if (windowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to PushWindowToPositionAsync");
            return false;
        }

        // Phase 2 WI#8: Performance logging for window positioning
        using var perfScope = _logger is Logger loggerWithPerf ?
            loggerWithPerf.BeginPerformanceScope("PushWindowToPosition",
                ("WindowHandle", $"0x{windowHandle:X}"),
                ("TargetX", targetPosition.X),
                ("TargetY", targetPosition.Y),
                ("AnimationsEnabled", _config.EnableAnimations)) :
            null;

        try
        {
            // Get current window bounds
            var currentBounds = _windowService.GetWindowBounds(windowHandle);
            if (currentBounds.IsEmpty)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("Could not get window bounds");
                _logger.LogWarning("Could not get bounds for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }

            (perfScope as IPerformanceScope)?.AddContext("CurrentX", currentBounds.X);
            (perfScope as IPerformanceScope)?.AddContext("CurrentY", currentBounds.Y);

            var currentPosition = new Point(currentBounds.X, currentBounds.Y);

            // Check if we're already at the target position
            if (currentPosition.Equals(targetPosition))
            {
                _logger.LogDebug("Window {Handle:X} is already at target position ({X},{Y})",
                    windowHandle.ToInt64(), targetPosition.X, targetPosition.Y);
                return true;
            }

            // If animations are disabled, move immediately
            if (!HardcodedDefaults.EnableAnimations || HardcodedDefaults.AnimationDurationMs <= 0)
            {
                (perfScope as IPerformanceScope)?.AddContext("AnimationType", "Immediate");
                var moveResult = _windowService.MoveWindow(windowHandle, targetPosition.X, targetPosition.Y);
                if (!moveResult)
                {
                    (perfScope as IPerformanceScope)?.MarkAsFailed("MoveWindow failed");
                }
                return moveResult;
            }

            // Cancel any existing animation for this window
            CancelWindowAnimation(windowHandle);

            // Create and start new animation with proper async state management
            var animation = new WindowAnimation(
                windowHandle,
                currentPosition,
                targetPosition,
                HardcodedDefaults.AnimationDurationMs,
                HardcodedDefaults.AnimationEasing
            );

            // Atomically add or update the animation
            _activeAnimations.AddOrUpdate(windowHandle, animation, (key, existing) => 
            {
                existing.Cancel(); // Cancel any existing animation
                return animation;
            });

            _logger.LogDebug("Starting animation for window {Handle:X} from ({StartX},{StartY}) to ({EndX},{EndY}) over {Duration}ms",
                windowHandle.ToInt64(), currentPosition.X, currentPosition.Y,
                targetPosition.X, targetPosition.Y, HardcodedDefaults.AnimationDurationMs);

            // Phase 2 WI#8: Window operation logging with log4net
            _logger.LogWindowOperation(Microsoft.Extensions.Logging.LogLevel.Information,
                "StartAnimation", windowHandle, null,
                $"Starting animated push from ({currentPosition.X},{currentPosition.Y}) to ({targetPosition.X},{targetPosition.Y})",
                ("AnimationDuration", HardcodedDefaults.AnimationDurationMs),
                ("AnimationEasing", HardcodedDefaults.AnimationEasing.ToString()));

            (perfScope as IPerformanceScope)?.AddContext("AnimationType", "Animated");
            (perfScope as IPerformanceScope)?.AddContext("AnimationDuration", HardcodedDefaults.AnimationDurationMs);
            (perfScope as IPerformanceScope)?.AddContext("AnimationEasing", HardcodedDefaults.AnimationEasing.ToString());

            // Run the animation with semaphore-based concurrency control
            await _animationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                var animationResult = await RunAnimationAsync(animation, _cancellationTokenSource.Token);
                if (!animationResult)
                {
                    (perfScope as IPerformanceScope)?.MarkAsFailed("Animation failed or was cancelled");
                }
                return animationResult;
            }
            finally
            {
                _animationSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            (perfScope as IPerformanceScope)?.MarkAsFailed(ex.Message);
            _logger.LogError("Error pushing window {Handle:X} to position ({X},{Y}): {Message}",
                windowHandle.ToInt64(), targetPosition.X, targetPosition.Y, ex.Message);

            // Phase 3 WI#8: Report hook failure for recovery
            await ReportHookFailureAsync("PushWindowToPositionAsync", ex);

            return false;
        }
    }

    /// <summary>
    /// Checks if a window is currently being animated
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    /// <returns>True if the window is currently being animated</returns>
    public bool IsWindowAnimating(IntPtr windowHandle)
    {
        return _activeAnimations.TryGetValue(windowHandle, out var animation) && 
               animation.IsActive;
    }

    /// <summary>
    /// Cancels any ongoing animation for a specific window
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    public void CancelWindowAnimation(IntPtr windowHandle)
    {
        if (_activeAnimations.TryRemove(windowHandle, out var animation))
        {
            animation.Cancel();
            _logger.LogDebug("Cancelled animation for window {Handle:X}", windowHandle.ToInt64());
        }
    }

    /// <summary>
    /// Cancels all ongoing window animations
    /// </summary>
    public void CancelAllAnimations()
    {
        var animations = _activeAnimations.ToArray();
        var animationCount = animations.Length;
        
        foreach (var kvp in animations)
        {
            if (_activeAnimations.TryRemove(kvp.Key, out var animation))
            {
                animation.Cancel();
            }
        }
        
        _logger.LogDebug("Cancelled all window animations ({Count} animations)", animationCount);
    }

    /// <summary>
    /// Runs an animation to completion with async window movement and high-resolution timing
    /// </summary>
    private async Task<bool> RunAnimationAsync(WindowAnimation animation, CancellationToken cancellationToken)
    {
        try
        {
            // Use high-resolution timing for smoother animations
            var stopwatch = Stopwatch.StartNew();
            var targetFrameTimeMs = Math.Max(8, HardcodedDefaults.UpdateIntervalMs); // Minimum 8ms per frame (~120fps max)
            var lastMoveTime = 0L;
            var moveThrottleMs = 1; // Throttle window moves to prevent excessive API calls
            var frameCount = 0;
            var lastFeedbackTime = 0L;
            const int feedbackIntervalMs = 50; // Visual feedback every 50ms

            // Provide initial visual feedback (as recommended by UX evaluator)
            await NotifyAnimationStartAsync(animation);

            while (!animation.IsComplete && !cancellationToken.IsCancellationRequested)
            {
                var frameStartTime = stopwatch.ElapsedMilliseconds;
                var progress = Math.Min(1.0, frameStartTime / (double)animation.DurationMs);

                // Apply easing function
                var easedProgress = ApplyEasing(progress, animation.Easing);

                // Calculate current position with sub-pixel precision
                var currentX = (int)Math.Round(animation.StartPosition.X +
                    (animation.EndPosition.X - animation.StartPosition.X) * easedProgress);
                var currentY = (int)Math.Round(animation.StartPosition.Y +
                    (animation.EndPosition.Y - animation.StartPosition.Y) * easedProgress);

                // Throttle window movement calls to reduce API overhead and prevent race conditions
                if (frameStartTime - lastMoveTime >= moveThrottleMs || progress >= 1.0)
                {
                    // Move the window asynchronously to prevent blocking the animation thread
                    var moveSuccess = await MoveWindowAsyncNonBlocking(animation.WindowHandle, currentX, currentY);
                    if (!moveSuccess)
                    {
                        _logger.LogWarning("Failed to move window {Handle:X} during animation at frame {Frame}",
                            animation.WindowHandle.ToInt64(), frameCount);
                        break;
                    }
                    lastMoveTime = frameStartTime;
                }

                // Provide visual feedback during long animations
                if (frameStartTime - lastFeedbackTime >= feedbackIntervalMs)
                {
                    await NotifyAnimationProgressAsync(animation, progress);
                    lastFeedbackTime = frameStartTime;
                }

                // Check if animation is complete
                if (progress >= 1.0)
                {
                    animation.Complete();
                    break;
                }

                frameCount++;

                // High-resolution delay with frame rate compensation
                var frameProcessingTime = stopwatch.ElapsedMilliseconds - frameStartTime;
                var remainingFrameTime = Math.Max(1, targetFrameTimeMs - frameProcessingTime);
                
                // Use high-resolution timer for more precise timing
                await HighResolutionDelayAsync((int)remainingFrameTime, cancellationToken);
            }

            // Clean up animation with proper thread safety
            _activeAnimations.TryRemove(animation.WindowHandle, out _);
            
            // Clear cached position on animation completion
            _positionCache.TryRemove(animation.WindowHandle, out _);

            var success = animation.IsComplete && !cancellationToken.IsCancellationRequested;

            _logger.LogDebug("Animation for window {Handle:X} {Status} after {Elapsed}ms",
                animation.WindowHandle.ToInt64(),
                success ? "completed" : "cancelled",
                stopwatch.ElapsedMilliseconds);

            // Phase 2 WI#8: Window operation logging for animation completion with log4net
            var logLevel = success ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Warning;
            _logger.LogWindowOperation(logLevel,
                success ? "CompleteAnimation" : "CancelAnimation",
                animation.WindowHandle, null,
                $"Animation {(success ? "completed" : "cancelled")} after {stopwatch.ElapsedMilliseconds}ms",
                ("ElapsedMs", stopwatch.ElapsedMilliseconds),
                ("Success", success),
                ("FinalX", animation.EndPosition.X),
                ("FinalY", animation.EndPosition.Y));

            return success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Animation for window {Handle:X} was cancelled", animation.WindowHandle.ToInt64());
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during animation for window {Handle:X}: {Message}", animation.WindowHandle.ToInt64(), ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Moves a window asynchronously without blocking the calling thread
    /// Enhanced with spatial caching and optimized batching
    /// </summary>
    /// <param name="windowHandle">Handle to the window to move</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>True if the move was successful</returns>
    private async Task<bool> MoveWindowAsyncNonBlocking(IntPtr windowHandle, int x, int y)
    {
        try
        {
            // Check spatial cache to avoid redundant moves
            var newPosition = new Point(x, y);
            if (await IsPositionCachedAndCurrentAsync(windowHandle, newPosition))
            {
                return true; // Position hasn't changed, no need to move
            }

            // Perform the move operation asynchronously
            var moveResult = await _windowService.MoveWindowAsync(windowHandle, x, y);
            
            // Update spatial cache on successful move
            if (moveResult)
            {
                await UpdatePositionCacheAsync(windowHandle, newPosition);
            }
            
            return moveResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error in async window move for {Handle:X}: {Message}", 
                windowHandle.ToInt64(), ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if a position is cached and current for a window
    /// </summary>
    private async Task<bool> IsPositionCachedAndCurrentAsync(IntPtr windowHandle, Point position)
    {
        if (!_positionCache.TryGetValue(windowHandle, out var cachedPosition))
            return false;

        // Check if cache is expired
        if (DateTime.UtcNow - cachedPosition.CacheTime > TimeSpan.FromMilliseconds(CacheExpiryMs))
        {
            _positionCache.TryRemove(windowHandle, out _);
            return false;
        }

        // Check if position matches
        return cachedPosition.Position.Equals(position);
    }

    /// <summary>
    /// Updates the position cache for a window
    /// </summary>
    private async Task UpdatePositionCacheAsync(IntPtr windowHandle, Point position)
    {
        var cachedPosition = new CachedWindowPosition(position, DateTime.UtcNow);
        _positionCache.AddOrUpdate(windowHandle, cachedPosition, (key, existing) => cachedPosition);
    }

    /// <summary>
    /// Applies cross-monitor DPI scaling when windows move between monitors with different DPI settings
    /// Enhanced with configuration-based control and improved scaling algorithm
    /// </summary>
    /// <param name="currentBounds">Current window bounds</param>
    /// <param name="targetPosition">Proposed target position</param>
    /// <param name="currentMonitorDpi">DPI information for the current monitor</param>
    /// <returns>DPI-adjusted target position</returns>
    private Point ApplyCrossMonitorDpiScaling(Rectangle currentBounds, Point targetPosition, DpiInfo currentMonitorDpi)
    {
        try
        {
            // Check if auto DPI adjustment is enabled
            if (_config.MultiMonitor?.EnableAutoDpiAdjustment != true)
            {
                _logger.LogDebug("Auto DPI adjustment disabled, using original position");
                return targetPosition;
            }

            // Get the target monitor for the proposed position
            var targetWindowBounds = new Rectangle(targetPosition.X, targetPosition.Y, currentBounds.Width, currentBounds.Height);
            var targetMonitorDpi = _monitorManager.GetDpiForRectangle(targetWindowBounds);

            // If DPI scaling factors are very close, no adjustment needed
            var dpiDifference = Math.Abs(currentMonitorDpi.ScaleFactor - targetMonitorDpi.ScaleFactor);
            if (dpiDifference < 0.05) // 5% tolerance for minor DPI differences
            {
                _logger.LogDebug("DPI difference negligible ({Difference:F3}), no scaling needed", dpiDifference);
                return targetPosition;
            }

            // Check if we're crossing monitor threshold distance
            var currentMonitor = _monitorManager.GetMonitorContaining(currentBounds);
            var targetMonitor = _monitorManager.GetMonitorContaining(targetWindowBounds);
            
            if (currentMonitor != null && targetMonitor != null && currentMonitor.monitorHandle != targetMonitor.monitorHandle)
            {
                // Calculate distance between monitors
                var monitorDistance = CalculateMonitorDistance(currentMonitor, targetMonitor);
                if (monitorDistance < (_config.MultiMonitor?.CrossMonitorThreshold ?? 10))
                {
                    _logger.LogDebug("Monitors too close ({Distance}px), skipping DPI scaling to prevent jitter", monitorDistance);
                    return targetPosition;
                }
            }

            // Calculate DPI scaling ratio
            var dpiRatio = targetMonitorDpi.ScaleFactor / currentMonitorDpi.ScaleFactor;

            // For cross-monitor moves, we need to adjust positioning intelligently
            // Use a hybrid approach that considers both relative positioning and absolute coordinates
            var currentPosition = new Point(currentBounds.X, currentBounds.Y);
            var movementVector = new Point(
                targetPosition.X - currentPosition.X,
                targetPosition.Y - currentPosition.Y
            );

            // Apply scaling to movement vector with dampening for large differences
            var scalingFactor = dpiRatio;
            if (Math.Abs(dpiRatio - 1.0) > 0.5) // For large DPI differences, apply dampening
            {
                scalingFactor = 1.0 + ((dpiRatio - 1.0) * 0.7); // Reduce scaling by 30%
                _logger.LogDebug("Applied DPI scaling dampening: original ratio={OriginalRatio:F3}, damped ratio={DampedRatio:F3}", 
                    dpiRatio, scalingFactor);
            }

            var scaledMovementVector = new Point(
                (int)(movementVector.X * scalingFactor),
                (int)(movementVector.Y * scalingFactor)
            );

            var adjustedPosition = new Point(
                currentPosition.X + scaledMovementVector.X,
                currentPosition.Y + scaledMovementVector.Y
            );

            _logger.LogDebug("Cross-monitor DPI scaling applied: source DPI={SourceDpi:F2}, target DPI={TargetDpi:F2}, " +
                           "scaling factor={ScalingFactor:F3}, original=({OriginalX},{OriginalY}), adjusted=({AdjustedX},{AdjustedY})",
                currentMonitorDpi.ScaleFactor, targetMonitorDpi.ScaleFactor, scalingFactor,
                targetPosition.X, targetPosition.Y, adjustedPosition.X, adjustedPosition.Y);

            return adjustedPosition;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error applying cross-monitor DPI scaling: {Message}. Using original target position.", ex.Message);
            return targetPosition;
        }
    }

    /// <summary>
    /// Calculates the minimum distance between two monitors
    /// </summary>
    /// <param name="monitor1">First monitor</param>
    /// <param name="monitor2">Second monitor</param>
    /// <returns>Distance in pixels</returns>
    private double CalculateMonitorDistance(MonitorInfo monitor1, MonitorInfo monitor2)
    {
        var bounds1 = monitor1.monitorBounds;
        var bounds2 = monitor2.monitorBounds;

        // If monitors overlap, distance is 0
        if (bounds1.IntersectsWith(bounds2))
            return 0;

        // Calculate minimum distance between rectangles
        var dx = Math.Max(0, Math.Max(bounds1.Left - bounds2.Right, bounds2.Left - bounds1.Right));
        var dy = Math.Max(0, Math.Max(bounds1.Top - bounds2.Bottom, bounds2.Top - bounds1.Bottom));

        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Detects which edge a window was constrained against by comparing target vs safe positions
    /// </summary>
    /// <param name="currentBounds">Current window bounds</param>
    /// <param name="targetPosition">Intended target position</param>
    /// <param name="safePosition">Actual safe position returned by safety manager</param>
    /// <returns>The edge type that caused the constraint</returns>
    private EdgeType DetectConstrainedEdge(Rectangle currentBounds, Point targetPosition, Point safePosition)
    {
        var tolerance = 5; // Small tolerance for floating point comparison

        // Check horizontal constraint
        if (Math.Abs(targetPosition.X - safePosition.X) > tolerance)
        {
            if (targetPosition.X < safePosition.X)
            {
                // Window was trying to move left but got constrained - hit left edge
                return EdgeType.Left;
            }
            else
            {
                // Window was trying to move right but got constrained - hit right edge
                return EdgeType.Right;
            }
        }

        // Check vertical constraint
        if (Math.Abs(targetPosition.Y - safePosition.Y) > tolerance)
        {
            if (targetPosition.Y < safePosition.Y)
            {
                // Window was trying to move up but got constrained - hit top edge
                return EdgeType.Top;
            }
            else
            {
                // Window was trying to move down but got constrained - hit bottom edge
                return EdgeType.Bottom;
            }
        }

        return EdgeType.None;
    }

    /// <summary>
    /// Applies easing function to animation progress
    /// </summary>
    private static double ApplyEasing(double progress, AnimationEasing easing)
    {
        return easing switch
        {
            AnimationEasing.Linear => progress,
            AnimationEasing.EaseIn => progress * progress,
            AnimationEasing.EaseOut => 1 - Math.Pow(1 - progress, 2),
            AnimationEasing.EaseInOut => progress < 0.5
                ? 2 * progress * progress
                : 1 - Math.Pow(-2 * progress + 2, 2) / 2,
            _ => progress
        };
    }

    /// <summary>
    /// High-resolution delay for smoother animations
    /// </summary>
    private static async Task HighResolutionDelayAsync(int delayMs, CancellationToken cancellationToken)
    {
        if (delayMs <= 0) return;

        // For very short delays, use spin-wait for higher precision
        if (delayMs <= 2)
        {
            var start = Stopwatch.GetTimestamp();
            var targetTicks = start + (delayMs * Stopwatch.Frequency / 1000);
            
            while (Stopwatch.GetTimestamp() < targetTicks && !cancellationToken.IsCancellationRequested)
            {
                Thread.SpinWait(100);
            }
        }
        else
        {
            await Task.Delay(delayMs, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies about animation start for visual feedback
    /// </summary>
    private async Task NotifyAnimationStartAsync(WindowAnimation animation)
    {
        try
        {
            // Visual feedback through system tray if available
            if (_trayManager != null && HardcodedDefaults.AnimationDurationMs > 500)
            {
                // Only show notification for long animations to avoid spam
                await _trayManager.ShowNotificationAsync("Window Animation", 
                    $"Moving window to new position...", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error showing animation start notification: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Notifies about animation progress for visual feedback
    /// </summary>
    private async Task NotifyAnimationProgressAsync(WindowAnimation animation, double progress)
    {
        try
        {
            // Log progress for debugging long animations
            if (HardcodedDefaults.AnimationDurationMs > 1000)
            {
                _logger.LogDebug("Animation progress for window {Handle:X}: {Progress:P0}",
                    animation.WindowHandle.ToInt64(), progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error updating animation progress: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Registers the WindowPusher for error recovery
    /// </summary>
    private async Task RegisterForErrorRecoveryAsync()
    {
        if (_errorRecoveryManager == null)
            return;

        try
        {
            var recoveryOptions = new RecoveryOptions
            {
                MaxRetryAttempts = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                MaxRetryDelay = TimeSpan.FromSeconds(5),
                FailureThreshold = MaxConsecutiveHookFailures,
                CircuitBreakerTimeout = TimeSpan.FromMinutes(2),
                EnableCircuitBreaker = true,
                ShowUserNotifications = true,
                Priority = RecoveryPriority.High
            };

            await _errorRecoveryManager.RegisterComponentAsync("WindowPusher", RecoverHookAsync, recoveryOptions);
            _logger.LogInformation("WindowPusher registered for error recovery with hook recovery support");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register WindowPusher for error recovery");
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Hook recovery function for error recovery manager
    /// </summary>
    private async Task<bool> RecoverHookAsync()
    {
        lock (_hookRecoveryLock)
        {
            if (_hookRecoveryInProgress)
            {
                _logger.LogDebug("Hook recovery already in progress");
                return false;
            }
            _hookRecoveryInProgress = true;
        }

        try
        {
            _logger.LogInformation("Starting WindowPusher hook recovery...");

            // Cancel all active animations to clear state
            CancelAllAnimations();

            // Wait a short time for cleanup
            await Task.Delay(500);

            // Attempt to validate hook functionality by testing window operations
            bool recoverySuccessful = await ValidateHookFunctionalityAsync();

            if (recoverySuccessful)
            {
                lock (_hookRecoveryLock)
                {
                    _consecutiveHookFailures = 0;
                    _lastHookFailure = DateTime.MinValue;
                }

                _logger.LogInformation("WindowPusher hook recovery completed successfully");

                // Notify user of successful recovery
                if (_trayManager != null)
                {
                    await _trayManager.ShowNotificationAsync("CursorPhobia Recovery",
                        "Window pushing functionality recovered successfully", false);
                }
            }
            else
            {
                _logger.LogError("WindowPusher hook recovery failed - functionality not restored");
            }

            return recoverySuccessful;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during WindowPusher hook recovery: {Message}", ex.Message);
            return false;
        }
        finally
        {
            lock (_hookRecoveryLock)
            {
                _hookRecoveryInProgress = false;
            }
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Validates hook functionality after recovery
    /// </summary>
    private async Task<bool> ValidateHookFunctionalityAsync()
    {
        try
        {
            // Test basic window detection and manipulation capabilities
            var windows = _windowDetectionService.EnumerateVisibleWindows();
            if (windows.Count == 0)
            {
                _logger.LogWarning("No visible windows found during hook validation");
                return false;
            }

            // Try to get information about the first few windows
            int testedWindows = 0;
            int successfulTests = 0;

            foreach (var window in windows.Take(3))
            {
                testedWindows++;

                try
                {
                    // Test window information retrieval
                    var windowInfo = _windowDetectionService.GetWindowInformation(window.WindowHandle);
                    if (windowInfo != null)
                    {
                        // Test bounds retrieval
                        var bounds = _windowService.GetWindowBounds(window.WindowHandle);
                        if (!bounds.IsEmpty)
                        {
                            // Test visibility check
                            var isVisible = _windowService.IsWindowVisible(window.WindowHandle);
                            if (isVisible)
                            {
                                successfulTests++;
                                _logger.LogDebug("Hook validation successful for window: {Title}", windowInfo.Title);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Hook validation failed for window {Handle:X}: {Message}", window.WindowHandle.ToInt64(), ex.Message);
                }
            }

            var successRate = testedWindows > 0 ? (double)successfulTests / testedWindows : 0.0;
            bool isValid = successRate >= 0.5; // At least 50% of tests should pass

            _logger.LogInformation("Hook validation completed: {Successful}/{Total} tests passed ({Rate:P1})",
                successfulTests, testedWindows, successRate);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during hook functionality validation: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Reports hook failure and potentially triggers recovery
    /// </summary>
    private async Task ReportHookFailureAsync(string operation, Exception exception)
    {
        lock (_hookRecoveryLock)
        {
            _lastHookFailure = DateTime.UtcNow;
            _consecutiveHookFailures++;
        }

        _logger.LogWarning("Hook failure detected in {Operation}: {ExceptionType} - {Message} (consecutive failures: {Count})",
            operation, exception.GetType().Name, exception.Message, _consecutiveHookFailures);

        // Report to error recovery manager if available
        if (_errorRecoveryManager != null)
        {
            try
            {
                await _errorRecoveryManager.ReportFailureAsync("WindowPusher", exception, operation);
            }
            catch (Exception recoveryEx)
            {
                _logger.LogError("Error reporting hook failure to recovery manager: {Message}", recoveryEx.Message);
            }
        }

        // If we've exceeded the failure threshold, show user notification
        if (_consecutiveHookFailures >= MaxConsecutiveHookFailures && _trayManager != null)
        {
            await _trayManager.ShowNotificationAsync("CursorPhobia Error",
                $"Window pushing has failed {_consecutiveHookFailures} times. Recovery will be attempted.", true);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Reports successful hook operation (resets failure counters)
    /// </summary>
    private async Task ReportHookSuccessAsync()
    {
        bool hadFailures;
        lock (_hookRecoveryLock)
        {
            hadFailures = _consecutiveHookFailures > 0;
            _consecutiveHookFailures = 0;
        }

        // Report success to error recovery manager if available
        if (_errorRecoveryManager != null && hadFailures)
        {
            try
            {
                await _errorRecoveryManager.ReportSuccessAsync("WindowPusher");
                _logger.LogDebug("Reported hook operation success to recovery manager");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error reporting hook success to recovery manager: {Message}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Checks if hook recovery is needed based on recent failures
    /// </summary>
    private bool ShouldTriggerHookRecovery()
    {
        lock (_hookRecoveryLock)
        {
            if (_hookRecoveryInProgress)
                return false;

            if (_consecutiveHookFailures >= MaxConsecutiveHookFailures)
                return true;

            // Check if we've had recent failures within the timeout window
            if (_lastHookFailure != DateTime.MinValue)
            {
                var timeSinceLastFailure = DateTime.UtcNow - _lastHookFailure;
                return timeSinceLastFailure.TotalMilliseconds < HookRecoveryTimeoutMs && _consecutiveHookFailures > 0;
            }

            return false;
        }
    }

    /// <summary>
    /// Disposes the WindowPusher and cancels all animations
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogDebug("Disposing WindowPusher");

        CancelAllAnimations();

        // Phase 3 WI#8: Unregister from error recovery
        if (_errorRecoveryManager != null)
        {
            try
            {
                Task.Run(async () => await _errorRecoveryManager.UnregisterComponentAsync("WindowPusher")).Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error unregistering WindowPusher from error recovery during disposal: {Message}", ex.Message);
            }
        }

        try
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }

        _cancellationTokenSource.Dispose();
        _animationSemaphore.Dispose();
        _cacheLock.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents an active window animation
/// </summary>
internal class WindowAnimation
{
    public IntPtr WindowHandle { get; }
    public Point StartPosition { get; }
    public Point EndPosition { get; }
    public int DurationMs { get; }
    public AnimationEasing Easing { get; }
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    public WindowAnimation(IntPtr windowHandle, Point startPosition, Point endPosition, int durationMs, AnimationEasing easing)
    {
        WindowHandle = windowHandle;
        StartPosition = startPosition;
        EndPosition = endPosition;
        DurationMs = durationMs;
        Easing = easing;
        IsActive = true;
        IsComplete = false;
    }

    public void Cancel()
    {
        IsActive = false;
    }

    public void Complete()
    {
        IsActive = false;
        IsComplete = true;
    }
}

/// <summary>
/// Represents a cached window position with timestamp
/// </summary>
internal sealed class CachedWindowPosition
{
    public Point Position { get; }
    public DateTime CacheTime { get; }

    public CachedWindowPosition(Point position, DateTime cacheTime)
    {
        Position = position;
        CacheTime = cacheTime;
    }
}