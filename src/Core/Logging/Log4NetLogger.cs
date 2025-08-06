using System.Globalization;
using log4net;
using log4net.Core;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.Logging;

/// <summary>
/// Log4net-backed implementation of ILogger interface
/// Provides structured logging, performance metrics, and production-ready features using log4net
/// </summary>
public class Log4NetLogger : CursorPhobia.Core.Utilities.ILogger, Microsoft.Extensions.Logging.ILogger, IProductionLogger
{
    private readonly ILog _log4netLogger;
    private readonly string _categoryName;
    private readonly log4net.Core.ILogger _logger;

    /// <summary>
    /// Creates a new Log4NetLogger instance
    /// </summary>
    /// <param name="log4netLogger">The underlying log4net logger</param>
    /// <param name="categoryName">The category name for this logger</param>
    public Log4NetLogger(ILog log4netLogger, string categoryName)
    {
        _log4netLogger = log4netLogger ?? throw new ArgumentNullException(nameof(log4netLogger));
        _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
        _logger = _log4netLogger.Logger;
    }

    #region Microsoft.Extensions.Logging.ILogger Implementation

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // log4net doesn't have built-in scope support, but we can simulate it with properties
        return new Log4NetScope<TState>(state, this);
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => _log4netLogger.IsDebugEnabled,
            Microsoft.Extensions.Logging.LogLevel.Debug => _log4netLogger.IsDebugEnabled,
            Microsoft.Extensions.Logging.LogLevel.Information => _log4netLogger.IsInfoEnabled,
            Microsoft.Extensions.Logging.LogLevel.Warning => _log4netLogger.IsWarnEnabled,
            Microsoft.Extensions.Logging.LogLevel.Error => _log4netLogger.IsErrorEnabled,
            Microsoft.Extensions.Logging.LogLevel.Critical => _log4netLogger.IsFatalEnabled,
            Microsoft.Extensions.Logging.LogLevel.None => false,
            _ => false
        };
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var log4netLevel = ConvertLogLevel(logLevel);

        // Create logging event with properties
        var loggingEvent = new LoggingEvent(_logger.GetType(), _logger.Repository, _categoryName, log4netLevel, message, exception);

        // Add event ID properties if provided
        if (eventId.Id != 0)
        {
            loggingEvent.Properties["EventId"] = eventId.Id;
            if (!string.IsNullOrEmpty(eventId.Name))
            {
                loggingEvent.Properties["EventName"] = eventId.Name;
            }
        }

        // Add short date for file naming
        loggingEvent.Properties["shortdate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _logger.Log(loggingEvent);
    }

    #endregion

    #region CursorPhobia.Core.Utilities.ILogger Implementation

    public void LogDebug(string message, params object[] args)
    {
        if (_log4netLogger.IsDebugEnabled)
        {
            _log4netLogger.DebugFormat($"[{_categoryName}] {message}", args);
        }
    }

    public void LogInformation(string message, params object[] args)
    {
        if (_log4netLogger.IsInfoEnabled)
        {
            _log4netLogger.InfoFormat($"[{_categoryName}] {message}", args);
        }
    }

    public void LogWarning(string message, params object[] args)
    {
        if (_log4netLogger.IsWarnEnabled)
        {
            _log4netLogger.WarnFormat($"[{_categoryName}] {message}", args);
        }
    }

    public void LogError(string message, params object[] args)
    {
        if (_log4netLogger.IsErrorEnabled)
        {
            _log4netLogger.ErrorFormat($"[{_categoryName}] {message}", args);
        }
    }

    public void LogError(Exception ex, string message, params object[] args)
    {
        if (_log4netLogger.IsErrorEnabled)
        {
            var formattedMessage = string.Format($"[{_categoryName}] {message}", args);
            _log4netLogger.Error(formattedMessage, ex);
        }
    }

    public void LogCritical(Exception ex, string message, params object[] args)
    {
        if (_log4netLogger.IsFatalEnabled)
        {
            var formattedMessage = string.Format($"[{_categoryName}] {message}", args);
            _log4netLogger.Fatal(formattedMessage, ex);
        }
    }

    #endregion

    #region IProductionLogger Implementation

    public void LogWithContext(Microsoft.Extensions.Logging.LogLevel logLevel, string message, params (string Key, object Value)[] properties)
    {
        LogWithContext(logLevel, null, message, properties);
    }

    public void LogWithContext(Microsoft.Extensions.Logging.LogLevel logLevel, Exception? exception, string message, params (string Key, object Value)[] properties)
    {
        if (!IsEnabled(logLevel))
            return;

        var log4netLevel = ConvertLogLevel(logLevel);
        var loggingEvent = new LoggingEvent(_logger.GetType(), _logger.Repository, _categoryName, log4netLevel, message, exception);

        // Add contextual properties
        foreach (var (key, value) in properties)
        {
            loggingEvent.Properties[key] = value;
        }

        // Add short date for file naming
        loggingEvent.Properties["shortdate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        _logger.Log(loggingEvent);
    }

    public void LogPerformance(string serviceName, string operation, TimeSpan duration, bool success = true, params (string Key, object Value)[] additionalContext)
    {
        var logLevel = success ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Warning;

        if (!IsEnabled(logLevel))
            return;

        var message = success
            ? $"Operation completed successfully: {operation} in {duration.TotalMilliseconds:F2}ms"
            : $"Operation completed with issues: {operation} in {duration.TotalMilliseconds:F2}ms";

        var log4netLevel = ConvertLogLevel(logLevel);
        var loggingEvent = new LoggingEvent(_logger.GetType(), _logger.Repository, _categoryName, log4netLevel, message, null);

        // Add performance properties
        loggingEvent.Properties["ServiceName"] = serviceName;
        loggingEvent.Properties["Operation"] = operation;
        loggingEvent.Properties["Duration"] = duration.TotalMilliseconds;
        loggingEvent.Properties["Success"] = success;
        loggingEvent.Properties["LogType"] = "Performance";
        loggingEvent.Properties["shortdate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Add additional context
        foreach (var (key, value) in additionalContext)
        {
            loggingEvent.Properties[key] = value;
        }

        _logger.Log(loggingEvent);
    }

    public void LogWindowOperation(Microsoft.Extensions.Logging.LogLevel logLevel, string operation, IntPtr windowHandle, string? windowTitle = null, string? message = null, params (string Key, object Value)[] additionalProperties)
    {
        if (!IsEnabled(logLevel))
            return;

        var logMessage = message ?? $"Window operation: {operation} on window {windowHandle:X}";
        var log4netLevel = ConvertLogLevel(logLevel);
        var loggingEvent = new LoggingEvent(_logger.GetType(), _logger.Repository, _categoryName, log4netLevel, logMessage, null);

        // Add window-specific properties
        loggingEvent.Properties["Operation"] = operation;
        loggingEvent.Properties["WindowHandle"] = $"0x{windowHandle:X}";
        loggingEvent.Properties["LogType"] = "WindowOperation";
        loggingEvent.Properties["shortdate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (!string.IsNullOrEmpty(windowTitle))
        {
            loggingEvent.Properties["WindowTitle"] = windowTitle;
        }

        // Add additional properties
        foreach (var (key, value) in additionalProperties)
        {
            loggingEvent.Properties[key] = value;
        }

        _logger.Log(loggingEvent);
    }

    public void LogSystemEvent(Microsoft.Extensions.Logging.LogLevel logLevel, string systemComponent, string @event, string message, params (string Key, object Value)[] additionalProperties)
    {
        if (!IsEnabled(logLevel))
            return;

        var log4netLevel = ConvertLogLevel(logLevel);
        var loggingEvent = new LoggingEvent(_logger.GetType(), _logger.Repository, _categoryName, log4netLevel, message, null);

        // Add system event properties
        loggingEvent.Properties["SystemComponent"] = systemComponent;
        loggingEvent.Properties["Event"] = @event;
        loggingEvent.Properties["LogType"] = "SystemEvent";
        loggingEvent.Properties["shortdate"] = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Add additional properties
        foreach (var (key, value) in additionalProperties)
        {
            loggingEvent.Properties[key] = value;
        }

        _logger.Log(loggingEvent);
    }

    public IProductionLogger CreateServiceLogger(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        // Create a new logger with the service name appended
        var serviceLoggerName = $"{_categoryName}.{serviceName}";
        var log4netServiceLogger = LogManager.GetLogger(serviceLoggerName);

        return new Log4NetLogger(log4netServiceLogger, serviceLoggerName);
    }

    public IDisposable BeginPerformanceScope(string serviceName, string operation, params (string Key, object Value)[] additionalContext)
    {
        return new Log4NetPerformanceScope(this, serviceName, operation, additionalContext);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts Microsoft.Extensions.Logging.LogLevel to log4net Level
    /// </summary>
    private static Level ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => Level.Debug,
            Microsoft.Extensions.Logging.LogLevel.Debug => Level.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => Level.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => Level.Warn,
            Microsoft.Extensions.Logging.LogLevel.Error => Level.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => Level.Fatal,
            Microsoft.Extensions.Logging.LogLevel.None => Level.Off,
            _ => Level.Info
        };
    }

    #endregion
}

/// <summary>
/// Log4net scope implementation for structured logging
/// </summary>
internal class Log4NetScope<TState> : IDisposable where TState : notnull
{
    private readonly TState _state;
    private readonly Log4NetLogger _logger;
    private bool _disposed = false;

    public Log4NetScope(TState state, Log4NetLogger logger)
    {
        _state = state;
        _logger = logger;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Performance timing scope for log4net
/// </summary>
internal class Log4NetPerformanceScope : IPerformanceScope
{
    private readonly IProductionLogger _logger;
    private readonly string _serviceName;
    private readonly string _operation;
    private readonly Dictionary<string, object> _context;
    private readonly System.Diagnostics.Stopwatch _stopwatch;
    private bool _failed = false;
    private string? _failureReason;
    private bool _disposed = false;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public Log4NetPerformanceScope(IProductionLogger logger, string serviceName, string operation, params (string Key, object Value)[] additionalContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
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

        // Prepare additional context including failure reason if applicable
        var contextArray = _context.Select(kvp => (kvp.Key, kvp.Value)).ToList();

        if (_failed && !string.IsNullOrEmpty(_failureReason))
        {
            contextArray.Add(("FailureReason", _failureReason!));
        }

        // Log the performance metrics
        _logger.LogPerformance(_serviceName, _operation, _stopwatch.Elapsed, !_failed, contextArray.ToArray());

        _disposed = true;
    }
}