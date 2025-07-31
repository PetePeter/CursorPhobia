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
/// </summary>
public class WindowPusher : IWindowPusher, IDisposable
{
    private readonly ILogger _logger;
    private readonly IWindowManipulationService _windowService;
    private readonly ISafetyManager _safetyManager;
    private readonly IProximityDetector _proximityDetector;
    private readonly CursorPhobiaConfiguration _config;
    private readonly MonitorManager _monitorManager;
    private readonly EdgeWrapHandler _edgeWrapHandler;
    
    // Animation tracking
    private readonly Dictionary<IntPtr, WindowAnimation> _activeAnimations;
    private readonly object _animationLock = new();
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;
    
    /// <summary>
    /// Creates a new WindowPusher instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="windowService">Service for window manipulation</param>
    /// <param name="safetyManager">Safety manager for boundary validation</param>
    /// <param name="proximityDetector">Proximity detector for push calculations</param>
    /// <param name="monitorManager">Monitor manager for multi-monitor support</param>
    /// <param name="edgeWrapHandler">Edge wrap handler for screen boundary wrapping</param>
    /// <param name="config">Configuration for animation and behavior settings</param>
    public WindowPusher(
        ILogger logger,
        IWindowManipulationService windowService,
        ISafetyManager safetyManager,
        IProximityDetector proximityDetector,
        MonitorManager monitorManager,
        EdgeWrapHandler edgeWrapHandler,
        CursorPhobiaConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        _safetyManager = safetyManager ?? throw new ArgumentNullException(nameof(safetyManager));
        _proximityDetector = proximityDetector ?? throw new ArgumentNullException(nameof(proximityDetector));
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
        _edgeWrapHandler = edgeWrapHandler ?? throw new ArgumentNullException(nameof(edgeWrapHandler));
        _config = config ?? CursorPhobiaConfiguration.CreateDefault();
        
        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationErrors)}");
        }
        
        _activeAnimations = new Dictionary<IntPtr, WindowAnimation>();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _logger.LogDebug("WindowPusher initialized with animations {Enabled}, duration {Duration}ms, easing {Easing}",
            _config.EnableAnimations, _config.AnimationDurationMs, _config.AnimationEasing);
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
        
        try
        {
            // Get current window bounds
            var currentBounds = _windowService.GetWindowBounds(windowHandle);
            if (currentBounds.IsEmpty)
            {
                _logger.LogWarning("Could not get bounds for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }
            
            // Calculate push vector using proximity detector
            var pushVector = _proximityDetector.CalculatePushVector(cursorPosition, currentBounds, pushDistance);
            if (pushVector.IsEmpty)
            {
                _logger.LogWarning("Could not calculate push vector for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }
            
            // Calculate target position
            var targetPosition = new Point(
                currentBounds.X + pushVector.X,
                currentBounds.Y + pushVector.Y
            );
            
            // Check for edge wrapping
            var wrapBehavior = new WrapBehavior
            {
                EnableWrapping = _config.MultiMonitor?.EnableWrapping ?? true,
                PreferredBehavior = _config.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart,
                RespectTaskbarAreas = _config.MultiMonitor?.RespectTaskbarAreas ?? true
            };
            
            var wrapDestination = _edgeWrapHandler.CalculateWrapDestination(currentBounds, pushVector, wrapBehavior);
            if (wrapDestination.HasValue && _edgeWrapHandler.IsWrapSafe(currentBounds.Location, wrapDestination.Value, currentBounds.Size))
            {
                _logger.LogDebug("Edge wrapping detected for window {Handle:X} from ({CurrentX},{CurrentY}) to ({WrapX},{WrapY})",
                    windowHandle.ToInt64(), currentBounds.X, currentBounds.Y, wrapDestination.Value.X, wrapDestination.Value.Y);
                targetPosition = wrapDestination.Value;
            }
            
            // Validate the target position with safety manager
            var safePosition = _safetyManager.ValidateWindowPosition(currentBounds, targetPosition);
            
            _logger.LogDebug("Pushing window {Handle:X} from ({CurrentX},{CurrentY}) to ({TargetX},{TargetY}) -> ({SafeX},{SafeY})",
                windowHandle.ToInt64(), currentBounds.X, currentBounds.Y, 
                targetPosition.X, targetPosition.Y, safePosition.X, safePosition.Y);
            
            return await PushWindowToPositionAsync(windowHandle, safePosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing window {Handle:X} away from cursor ({CursorX},{CursorY})",
                windowHandle.ToInt64(), cursorPosition.X, cursorPosition.Y);
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
        
        try
        {
            // Get current window bounds
            var currentBounds = _windowService.GetWindowBounds(windowHandle);
            if (currentBounds.IsEmpty)
            {
                _logger.LogWarning("Could not get bounds for window {Handle:X}", windowHandle.ToInt64());
                return false;
            }
            
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
                return _windowService.MoveWindow(windowHandle, targetPosition.X, targetPosition.Y);
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
            
            // Run the animation
            return await RunAnimationAsync(animation, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pushing window {Handle:X} to position ({X},{Y})",
                windowHandle.ToInt64(), targetPosition.X, targetPosition.Y);
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
            
            return success;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Animation for window {Handle:X} was cancelled", animation.WindowHandle.ToInt64());
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during animation for window {Handle:X}", animation.WindowHandle.ToInt64());
            return false;
        }
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
    /// Disposes the WindowPusher and cancels all animations
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _logger.LogDebug("Disposing WindowPusher");
        
        CancelAllAnimations();
        
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