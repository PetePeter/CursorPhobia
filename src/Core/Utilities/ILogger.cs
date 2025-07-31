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
}