using Microsoft.Extensions.Logging;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Enhanced production logging interface with structured logging capabilities
/// Extends standard ILogger with contextual properties, performance metrics, and production features
/// </summary>
public interface IProductionLogger : ILogger
{
    /// <summary>
    /// Logs a message with structured contextual properties
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="message">The message template</param>
    /// <param name="properties">Contextual properties as key-value pairs</param>
    void LogWithContext(LogLevel logLevel, string message, params (string Key, object Value)[] properties);
    
    /// <summary>
    /// Logs a message with structured contextual properties and exception
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">The message template</param>
    /// <param name="properties">Contextual properties as key-value pairs</param>
    void LogWithContext(LogLevel logLevel, Exception exception, string message, params (string Key, object Value)[] properties);
    
    /// <summary>
    /// Logs performance metrics for an operation
    /// </summary>
    /// <param name="serviceName">Name of the service performing the operation</param>
    /// <param name="operation">Name of the operation</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="additionalContext">Additional contextual information</param>
    void LogPerformance(string serviceName, string operation, TimeSpan duration, bool success = true, 
        params (string Key, object Value)[] additionalContext);
    
    /// <summary>
    /// Logs window-related operations with structured properties
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="operation">The window operation being performed</param>
    /// <param name="windowHandle">Window handle (will be formatted as hex)</param>
    /// <param name="windowTitle">Window title</param>
    /// <param name="message">Additional message</param>
    /// <param name="additionalProperties">Additional contextual properties</param>
    void LogWindowOperation(LogLevel logLevel, string operation, IntPtr windowHandle, string? windowTitle = null, 
        string? message = null, params (string Key, object Value)[] additionalProperties);
    
    /// <summary>
    /// Logs system-level events with appropriate context
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="systemComponent">The system component (e.g., "Cursor", "Monitor", "Hook")</param>
    /// <param name="event">The event that occurred</param>
    /// <param name="message">Detailed message</param>
    /// <param name="additionalProperties">Additional contextual properties</param>
    void LogSystemEvent(LogLevel logLevel, string systemComponent, string @event, string message,
        params (string Key, object Value)[] additionalProperties);
    
    /// <summary>
    /// Creates a scoped logger for a specific service or component
    /// </summary>
    /// <param name="serviceName">Name of the service or component</param>
    /// <returns>A scoped logger that automatically includes the service name in all log entries</returns>
    IProductionLogger CreateServiceLogger(string serviceName);
    
    /// <summary>
    /// Creates a performance timing scope that automatically logs duration when disposed
    /// </summary>
    /// <param name="serviceName">Name of the service performing the operation</param>
    /// <param name="operation">Name of the operation</param>
    /// <param name="additionalContext">Additional contextual information</param>
    /// <returns>A disposable scope that logs performance metrics on disposal</returns>
    IDisposable BeginPerformanceScope(string serviceName, string operation, 
        params (string Key, object Value)[] additionalContext);
}

/// <summary>
/// Performance timing scope for automatic duration logging
/// </summary>
public interface IPerformanceScope : IDisposable
{
    /// <summary>
    /// Marks the operation as failed
    /// </summary>
    /// <param name="error">Error message or exception details</param>
    void MarkAsFailed(string? error = null);
    
    /// <summary>
    /// Adds additional context to the performance log
    /// </summary>
    /// <param name="key">Context key</param>
    /// <param name="value">Context value</param>
    void AddContext(string key, object value);
    
    /// <summary>
    /// Gets the elapsed time so far
    /// </summary>
    TimeSpan Elapsed { get; }
}