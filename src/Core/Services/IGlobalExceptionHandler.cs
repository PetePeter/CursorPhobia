namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for global exception handling and crash prevention
/// Provides comprehensive error recovery and graceful degradation patterns
/// Integrates with Application.ThreadException and AppDomain.UnhandledException
/// </summary>
public interface IGlobalExceptionHandler : IDisposable
{
    /// <summary>
    /// Event raised when a recoverable exception is handled gracefully
    /// </summary>
    event EventHandler<ExceptionHandledEventArgs>? ExceptionHandled;

    /// <summary>
    /// Event raised when a critical exception occurs that may require application restart
    /// </summary>
    event EventHandler<CriticalExceptionEventArgs>? CriticalExceptionOccurred;

    /// <summary>
    /// Gets whether the global exception handler is currently active
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the total number of exceptions handled since initialization
    /// </summary>
    int TotalExceptionsHandled { get; }

    /// <summary>
    /// Gets the number of critical exceptions that have occurred
    /// </summary>
    int CriticalExceptionsCount { get; }

    /// <summary>
    /// Initializes the global exception handler and hooks into system exception events
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Handles an exception with appropriate recovery strategies
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="context">Context information about where the exception occurred</param>
    /// <param name="canRecover">Whether recovery is possible for this exception</param>
    /// <returns>True if the exception was handled successfully, false if the application should terminate</returns>
    Task<bool> HandleExceptionAsync(Exception exception, string context, bool canRecover = true);

    /// <summary>
    /// Shows a user-friendly error message without technical details
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="title">Dialog title</param>
    /// <param name="isCritical">Whether this is a critical error</param>
    Task ShowUserErrorAsync(string message, string title = "CursorPhobia Error", bool isCritical = false);

    /// <summary>
    /// Attempts to recover from a service failure by restarting the service
    /// </summary>
    /// <param name="serviceName">Name of the failed service</param>
    /// <param name="restartAction">Action to restart the service</param>
    /// <returns>True if recovery was successful, false otherwise</returns>
    Task<bool> AttemptServiceRecoveryAsync(string serviceName, Func<Task<bool>> restartAction);

    /// <summary>
    /// Shuts down the global exception handler and unhooks from system events
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// Event arguments for handled exceptions
/// </summary>
public class ExceptionHandledEventArgs : EventArgs
{
    /// <summary>
    /// The exception that was handled
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Context information about where the exception occurred
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Whether recovery was successful
    /// </summary>
    public bool RecoverySuccessful { get; }

    /// <summary>
    /// Timestamp when the exception was handled
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates new exception handled event arguments
    /// </summary>
    /// <param name="exception">The exception that was handled</param>
    /// <param name="context">Context information</param>
    /// <param name="recoverySuccessful">Whether recovery was successful</param>
    public ExceptionHandledEventArgs(Exception exception, string context, bool recoverySuccessful)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Context = context ?? string.Empty;
        RecoverySuccessful = recoverySuccessful;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for critical exceptions
/// </summary>
public class CriticalExceptionEventArgs : EventArgs
{
    /// <summary>
    /// The critical exception that occurred
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Context information about where the exception occurred
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Whether the application is terminating due to this exception
    /// </summary>
    public bool IsTerminating { get; }

    /// <summary>
    /// Timestamp when the critical exception occurred
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates new critical exception event arguments
    /// </summary>
    /// <param name="exception">The critical exception</param>
    /// <param name="context">Context information</param>
    /// <param name="isTerminating">Whether the application is terminating</param>
    public CriticalExceptionEventArgs(Exception exception, string context, bool isTerminating)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Context = context ?? string.Empty;
        IsTerminating = isTerminating;
        Timestamp = DateTime.UtcNow;
    }
}