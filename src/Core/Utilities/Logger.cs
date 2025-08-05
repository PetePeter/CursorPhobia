using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.Utilities;

/// <summary>
/// Basic logging infrastructure for CursorPhobia with NLog backend integration
/// Maintains backward compatibility while supporting production logging features
/// </summary>
public class Logger : ILogger, Microsoft.Extensions.Logging.ILogger
{
    private readonly Microsoft.Extensions.Logging.ILogger _innerLogger;
    private readonly string _categoryName;
    private readonly IProductionLogger? _productionLogger;
    
    /// <summary>
    /// Creates a new Logger instance
    /// </summary>
    /// <param name="innerLogger">The underlying logger implementation</param>
    /// <param name="categoryName">The category name for this logger</param>
    /// <param name="productionLogger">Optional production logger for enhanced features</param>
    public Logger(Microsoft.Extensions.Logging.ILogger innerLogger, string categoryName, IProductionLogger? productionLogger = null)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _productionLogger = productionLogger;
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
    
    /// <summary>
    /// Gets the underlying production logger if available
    /// </summary>
    public IProductionLogger? ProductionLogger => _productionLogger;
    
    /// <summary>
    /// Logs performance metrics if production logger is available
    /// </summary>
    /// <param name="operation">The operation name</param>
    /// <param name="duration">Duration of the operation</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="additionalContext">Additional contextual information</param>
    public void LogPerformance(string operation, TimeSpan duration, bool success = true, 
        params (string Key, object Value)[] additionalContext)
    {
        if (_productionLogger != null)
        {
            _productionLogger.LogPerformance(_categoryName, operation, duration, success, additionalContext);
        }
        else
        {
            // Fallback to regular logging
            var level = success ? LogLevel.Information : LogLevel.Warning;
            var message = success 
                ? $"Operation '{operation}' completed in {duration.TotalMilliseconds:F2}ms"
                : $"Operation '{operation}' completed with issues in {duration.TotalMilliseconds:F2}ms";
            
            _innerLogger.Log(level, $"[{_categoryName}] {message}");
        }
    }
    
    /// <summary>
    /// Logs window operations if production logger is available
    /// </summary>
    /// <param name="logLevel">The log level</param>
    /// <param name="operation">The window operation</param>
    /// <param name="windowHandle">Window handle</param>
    /// <param name="windowTitle">Window title</param>
    /// <param name="message">Additional message</param>
    /// <param name="additionalProperties">Additional properties</param>
    public void LogWindowOperation(LogLevel logLevel, string operation, IntPtr windowHandle, 
        string? windowTitle = null, string? message = null, params (string Key, object Value)[] additionalProperties)
    {
        if (_productionLogger != null)
        {
            _productionLogger.LogWindowOperation(logLevel, operation, windowHandle, windowTitle, message, additionalProperties);
        }
        else
        {
            // Fallback to regular logging
            var fullMessage = message ?? $"Window operation: {operation}";
            if (!string.IsNullOrEmpty(windowTitle))
            {
                fullMessage += $" on '{windowTitle}'";
            }
            fullMessage += $" (0x{windowHandle:X})";
            
            _innerLogger.Log(logLevel, $"[{_categoryName}] {fullMessage}");
        }
    }
    
    /// <summary>
    /// Creates a performance timing scope
    /// </summary>
    /// <param name="operation">The operation name</param>
    /// <param name="additionalContext">Additional contextual information</param>
    /// <returns>A disposable scope that logs performance metrics on disposal</returns>
    public IDisposable BeginPerformanceScope(string operation, params (string Key, object Value)[] additionalContext)
    {
        if (_productionLogger != null)
        {
            return _productionLogger.BeginPerformanceScope(_categoryName, operation, additionalContext);
        }
        else
        {
            // Fallback to a simple timing scope
            return new SimplePerformanceScope(this, operation, additionalContext);
        }
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

/// <summary>
/// Simple performance scope for fallback logging when production logger is not available
/// </summary>
internal class SimplePerformanceScope : IDisposable, IPerformanceScope
{
    private readonly Logger _logger;
    private readonly string _operation;
    private readonly Dictionary<string, object> _context;
    private readonly System.Diagnostics.Stopwatch _stopwatch;
    private bool _failed = false;
    private string? _failureReason;
    private bool _disposed = false;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public SimplePerformanceScope(Logger logger, string operation, params (string Key, object Value)[] additionalContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        
        _context = new Dictionary<string, object>();
        foreach (var (key, value) in additionalContext)
        {
            _context[key] = value;
        }

        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    public void MarkAsFailed(string? error = null)
    {
        _failed = true;
        _failureReason = error;
    }

    public void AddContext(string key, object value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Context key cannot be null or empty", nameof(key));
        
        _context[key] = value;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _stopwatch.Stop();

        // Log the performance metrics using the regular logger
        _logger.LogPerformance(_operation, _stopwatch.Elapsed, !_failed, _context.Select(kvp => (kvp.Key, kvp.Value)).ToArray());

        if (_failed && !string.IsNullOrEmpty(_failureReason))
        {
            _logger.LogWarning("Performance scope completed with failure: {FailureReason}", _failureReason);
        }

        _disposed = true;
    }
}