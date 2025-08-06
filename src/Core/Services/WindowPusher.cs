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

    // Animation tracking
    private readonly Dictionary<IntPtr, WindowAnimation> _activeAnimations;
    private readonly object _animationLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

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
        ISystemTrayManager? trayManager = null)
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

        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }

        _activeAnimations = new Dictionary<IntPtr, WindowAnimation>();
        _cancellationTokenSource = new CancellationTokenSource();

        // Phase 3 WI#8: Register with error recovery manager for hook failures
        if (_errorRecoveryManager != null)
        {
            Task.Run(async () => await RegisterForErrorRecoveryAsync());
        }

        _logger.LogDebug("WindowPusher initialized with animations {Enabled}, duration {Duration}ms, easing {Easing}, hook recovery {HookRecovery}",
            _config.EnableAnimations, _config.AnimationDurationMs, _config.AnimationEasing, _errorRecoveryManager != null);
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

            // Calculate push vector using proximity detector
            var pushVector = _proximityDetector.CalculatePushVector(cursorPosition, currentBounds, pushDistance);
            if (pushVector.IsEmpty)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("Could not calculate push vector");
                _logger.LogWarning("Could not calculate push vector for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }

            (perfScope as IPerformanceScope)?.AddContext("PushVectorX", pushVector.X);
            (perfScope as IPerformanceScope)?.AddContext("PushVectorY", pushVector.Y);

            // Calculate target position
            var targetPosition = new Point(
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
                targetPosition = wrapDestination.Value;
            }

            // Validate the target position with safety manager
            var safePosition = _safetyManager.ValidateWindowPosition(currentBounds, targetPosition);

            // Check if the window was constrained by safety manager (hit an edge and can't move further)
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

            _logger.LogDebug("Pushing window {Handle:X} from ({CurrentX},{CurrentY}) to ({TargetX},{TargetY}) -> ({SafeX},{SafeY})",
                windowHandle.ToInt64(), currentBounds.X, currentBounds.Y,
                targetPosition.X, targetPosition.Y, safePosition.X, safePosition.Y);

            (perfScope as IPerformanceScope)?.AddContext("TargetX", targetPosition.X);
            (perfScope as IPerformanceScope)?.AddContext("TargetY", targetPosition.Y);
            (perfScope as IPerformanceScope)?.AddContext("SafeX", safePosition.X);
            (perfScope as IPerformanceScope)?.AddContext("SafeY", safePosition.Y);

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
            if (!_config.EnableAnimations || _config.AnimationDurationMs <= 0)
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

            // Create and start new animation
            var animation = new WindowAnimation(
                windowHandle,
                currentPosition,
                targetPosition,
                _config.AnimationDurationMs,
                _config.AnimationEasing
            );

            lock (_animationLock)
            {
                _activeAnimations[windowHandle] = animation;
            }

            _logger.LogDebug("Starting animation for window {Handle:X} from ({StartX},{StartY}) to ({EndX},{EndY}) over {Duration}ms",
                windowHandle.ToInt64(), currentPosition.X, currentPosition.Y,
                targetPosition.X, targetPosition.Y, _config.AnimationDurationMs);

            // Phase 2 WI#8: Window operation logging with log4net
            _logger.LogWindowOperation(Microsoft.Extensions.Logging.LogLevel.Information,
                "StartAnimation", windowHandle, null,
                $"Starting animated push from ({currentPosition.X},{currentPosition.Y}) to ({targetPosition.X},{targetPosition.Y})",
                ("AnimationDuration", _config.AnimationDurationMs),
                ("AnimationEasing", _config.AnimationEasing.ToString()));

            (perfScope as IPerformanceScope)?.AddContext("AnimationType", "Animated");
            (perfScope as IPerformanceScope)?.AddContext("AnimationDuration", _config.AnimationDurationMs);
            (perfScope as IPerformanceScope)?.AddContext("AnimationEasing", _config.AnimationEasing.ToString());

            // Run the animation
            var animationResult = await RunAnimationAsync(animation, _cancellationTokenSource.Token);
            if (!animationResult)
            {
                (perfScope as IPerformanceScope)?.MarkAsFailed("Animation failed or was cancelled");
            }

            return animationResult;
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
        lock (_animationLock)
        {
            return _activeAnimations.ContainsKey(windowHandle) &&
                   _activeAnimations[windowHandle].IsActive;
        }
    }

    /// <summary>
    /// Cancels any ongoing animation for a specific window
    /// </summary>
    /// <param name="windowHandle">Handle to the window</param>
    public void CancelWindowAnimation(IntPtr windowHandle)
    {
        lock (_animationLock)
        {
            if (_activeAnimations.TryGetValue(windowHandle, out var animation))
            {
                animation.Cancel();
                _activeAnimations.Remove(windowHandle);
                _logger.LogDebug("Cancelled animation for window {Handle:X}", windowHandle.ToInt64());
            }
        }
    }

    /// <summary>
    /// Cancels all ongoing window animations
    /// </summary>
    public void CancelAllAnimations()
    {
        lock (_animationLock)
        {
            foreach (var animation in _activeAnimations.Values)
            {
                animation.Cancel();
            }
            _activeAnimations.Clear();
            _logger.LogDebug("Cancelled all window animations ({Count} animations)", _activeAnimations.Count);
        }
    }

    /// <summary>
    /// Runs an animation to completion
    /// </summary>
    private async Task<bool> RunAnimationAsync(WindowAnimation animation, CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var frameTime = Math.Max(8, _config.UpdateIntervalMs); // Minimum 8ms per frame (~120fps max)

            while (!animation.IsComplete && !cancellationToken.IsCancellationRequested)
            {
                var elapsed = stopwatch.ElapsedMilliseconds;
                var progress = Math.Min(1.0, elapsed / (double)animation.DurationMs);

                // Apply easing function
                var easedProgress = ApplyEasing(progress, animation.Easing);

                // Calculate current position
                var currentX = (int)Math.Round(animation.StartPosition.X +
                    (animation.EndPosition.X - animation.StartPosition.X) * easedProgress);
                var currentY = (int)Math.Round(animation.StartPosition.Y +
                    (animation.EndPosition.Y - animation.StartPosition.Y) * easedProgress);

                // Move the window
                if (!_windowService.MoveWindow(animation.WindowHandle, currentX, currentY))
                {
                    _logger.LogWarning("Failed to move window {Handle:X} during animation",
                        animation.WindowHandle.ToInt64());
                    break;
                }

                // Check if animation is complete
                if (progress >= 1.0)
                {
                    animation.Complete();
                    break;
                }

                // Wait for next frame
                await Task.Delay(frameTime, cancellationToken);
            }

            // Clean up animation
            lock (_animationLock)
            {
                _activeAnimations.Remove(animation.WindowHandle);
            }

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