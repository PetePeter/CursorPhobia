using Microsoft.Extensions.Logging;

namespace CursorPhobia.Core.Utilities;

/// <summary>
/// Basic logging infrastructure for CursorPhobia
/// </summary>
public class Logger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly string _categoryName;
    
    /// <summary>
    /// Creates a new Logger instance
    /// </summary>
    /// <param name="innerLogger">The underlying logger implementation</param>
    /// <param name="categoryName">The category name for this logger</param>
    public Logger(ILogger innerLogger, string categoryName)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
    }
    
    /// <summary>
    /// Begins a logical operation scope
    /// </summary>
    /// <typeparam name="TState">The type of the state to begin the scope for</typeparam>
    /// <param name="state">The identifier for the scope</param>
    /// <returns>An IDisposable that ends the logical operation scope on dispose</returns>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _innerLogger.BeginScope(state);
    }
    
    /// <summary>
    /// Checks if the given logLevel is enabled
    /// </summary>
    /// <param name="logLevel">Level to be checked</param>
    /// <returns>True if enabled; false otherwise</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }
    
    /// <summary>
    /// Writes a log entry
    /// </summary>
    /// <typeparam name="TState">The type of the object to be written</typeparam>
    /// <param name="logLevel">Entry will be written on this level</param>
    /// <param name="eventId">Id of the event</param>
    /// <param name="state">The entry to be written</param>
    /// <param name="exception">The exception related to this entry</param>
    /// <param name="formatter">Function to create a string message of the state and exception</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _innerLogger.Log(logLevel, eventId, state, exception, formatter);
    }
    
    /// <summary>
    /// Logs a debug message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogDebug(string message, params object[] args)
    {
        _innerLogger.LogDebug($"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogInformation(string message, params object[] args)
    {
        _innerLogger.LogInformation($"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogWarning(string message, params object[] args)
    {
        _innerLogger.LogWarning($"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogError(string message, params object[] args)
    {
        _innerLogger.LogError($"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs an error message with exception
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogError(Exception exception, string message, params object[] args)
    {
        _innerLogger.LogError(exception, $"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs a critical error message
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogCritical(string message, params object[] args)
    {
        _innerLogger.LogCritical($"[{_categoryName}] {message}", args);
    }
    
    /// <summary>
    /// Logs a critical error message with exception
    /// </summary>
    /// <param name="exception">The exception to log</param>
    /// <param name="message">The message to log</param>
    /// <param name="args">Optional format arguments</param>
    public void LogCritical(Exception exception, string message, params object[] args)
    {
        _innerLogger.LogCritical(exception, $"[{_categoryName}] {message}", args);
    }
}

/// <summary>
/// Static factory for creating loggers
/// </summary>
public static class LoggerFactory
{
    private static ILoggerFactory? _loggerFactory;
    
    /// <summary>
    /// Initializes the logger factory
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use</param>
    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }
    
    /// <summary>
    /// Creates a logger for the specified category
    /// </summary>
    /// <typeparam name="T">The type whose name is used for the logger category name</typeparam>
    /// <returns>A new logger instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the factory has not been initialized</exception>
    public static Logger CreateLogger<T>()
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("LoggerFactory has not been initialized. Call Initialize() first.");
            
        var innerLogger = _loggerFactory.CreateLogger<T>();
        return new Logger(innerLogger, typeof(T).Name);
    }
    
    /// <summary>
    /// Creates a logger for the specified category name
    /// </summary>
    /// <param name="categoryName">The category name for the logger</param>
    /// <returns>A new logger instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the factory has not been initialized</exception>
    public static Logger CreateLogger(string categoryName)
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("LoggerFactory has not been initialized. Call Initialize() first.");
            
        var innerLogger = _loggerFactory.CreateLogger(categoryName);
        return new Logger(innerLogger, categoryName);
    }
}