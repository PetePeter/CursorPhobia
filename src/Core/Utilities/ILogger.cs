using Microsoft.Extensions.Logging;

namespace CursorPhobia.Core.Utilities;

/// <summary>
/// Interface for logging operations in CursorPhobia
/// </summary>
public interface ILogger
{
    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogDebug(string message, params object[] args);
    
    /// <summary>
    /// Logs an information message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogInformation(string message, params object[] args);
    
    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogWarning(string message, params object[] args);
    
    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogError(string message, params object[] args);
    
    /// <summary>
    /// Logs an error message with exception
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogError(Exception ex, string message, params object[] args);
    
    /// <summary>
    /// Logs a critical message
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="message">The message to log</param>
    /// <param name="args">Arguments for the message</param>
    void LogCritical(Exception ex, string message, params object[] args);
    
    /// <summary>
    /// Logs window-related operations with structured properties
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="operation">The window operation being performed</param>
    /// <param name="windowHandle">Window handle</param>
    /// <param name="windowTitle">Window title</param>
    /// <param name="message">Additional message</param>
    /// <param name="additionalProperties">Additional contextual properties</param>
    void LogWindowOperation(Microsoft.Extensions.Logging.LogLevel logLevel, string operation, IntPtr windowHandle, 
        string? windowTitle = null, string? message = null, params (string Key, object Value)[] additionalProperties);
}