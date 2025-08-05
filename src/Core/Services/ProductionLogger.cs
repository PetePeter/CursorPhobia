using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Production-grade logging implementation using NLog
/// Provides structured logging, performance metrics, and contextual properties
/// </summary>
public class ProductionLogger : IProductionLogger
{
    private readonly NLog.Logger _nlogLogger;
    private readonly Microsoft.Extensions.Logging.ILogger _extensionsLogger;
    private readonly string? _serviceName;

    /// <summary>
    /// Creates a ProductionLogger instance
    /// </summary>
    /// <param name="nlogLogger">The underlying NLog logger</param>
    /// <param name="extensionsLogger">Microsoft Extensions logger for compatibility</param>
    /// <param name="serviceName">Optional service name for scoped logging</param>
    public ProductionLogger(NLog.Logger nlogLogger, Microsoft.Extensions.Logging.ILogger extensionsLogger, string? serviceName = null)
    {
        _nlogLogger = nlogLogger ?? throw new ArgumentNullException(nameof(nlogLogger));
        _extensionsLogger = extensionsLogger ?? throw new ArgumentNullException(nameof(extensionsLogger));
        _serviceName = serviceName;
    }

    #region ILogger Implementation
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _extensionsLogger.BeginScope(state);
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return _extensionsLogger.IsEnabled(logLevel) && _nlogLogger.IsEnabled(ConvertLogLevel(logLevel));
    }

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var nlogLevel = ConvertLogLevel(logLevel);
        
        var logEvent = new LogEventInfo(nlogLevel, _nlogLogger.Name, message)
        {
            Exception = exception
        };

        // Add service name if available
        if (!string.IsNullOrEmpty(_serviceName))
        {
            logEvent.Properties["ServiceName"] = _serviceName;
        }

        // Add event ID if provided
        if (eventId.Id != 0)
        {
            logEvent.Properties["EventId"] = eventId.Id;
            if (!string.IsNullOrEmpty(eventId.Name))
            {
                logEvent.Properties["EventName"] = eventId.Name;
            }
        }

        _nlogLogger.Log(logEvent);
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

        var nlogLevel = ConvertLogLevel(logLevel);
        var logEvent = new LogEventInfo(nlogLevel, _nlogLogger.Name, message)
        {
            Exception = exception
        };

        // Add service name if available
        if (!string.IsNullOrEmpty(_serviceName))
        {
            logEvent.Properties["ServiceName"] = _serviceName;
        }

        // Add contextual properties
        foreach (var (key, value) in properties)
        {
            logEvent.Properties[key] = value;
        }

        _nlogLogger.Log(logEvent);
    }

    public void LogPerformance(string serviceName, string operation, TimeSpan duration, bool success = true, params (string Key, object Value)[] additionalContext)
    {
        var logLevel = success ? Microsoft.Extensions.Logging.LogLevel.Information : Microsoft.Extensions.Logging.LogLevel.Warning;
        var nlogLevel = ConvertLogLevel(logLevel);
        
        var message = success 
            ? "Operation completed successfully: {Operation} in {Duration}ms" 
            : "Operation completed with issues: {Operation} in {Duration}ms";

        var logEvent = new LogEventInfo(nlogLevel, _nlogLogger.Name, message);
        
        // Add performance properties
        logEvent.Properties["ServiceName"] = serviceName;
        logEvent.Properties["Operation"] = operation;
        logEvent.Properties["Duration"] = duration.TotalMilliseconds;
        logEvent.Properties["Success"] = success;
        logEvent.Properties["LogType"] = "Performance";

        // Add additional context
        foreach (var (key, value) in additionalContext)
        {
            logEvent.Properties[key] = value;
        }

        _nlogLogger.Log(logEvent);
    }

    public void LogWindowOperation(Microsoft.Extensions.Logging.LogLevel logLevel, string operation, IntPtr windowHandle, string? windowTitle = null, string? message = null, params (string Key, object Value)[] additionalProperties)
    {
        if (!IsEnabled(logLevel))
            return;

        var nlogLevel = ConvertLogLevel(logLevel);
        var logMessage = message ?? "Window operation: {Operation} on window {WindowHandle}";
        
        var logEvent = new LogEventInfo(nlogLevel, _nlogLogger.Name, logMessage);
        
        // Add service name if available
        if (!string.IsNullOrEmpty(_serviceName))
        {
            logEvent.Properties["ServiceName"] = _serviceName;
        }

        // Add window-specific properties
        logEvent.Properties["Operation"] = operation;
        logEvent.Properties["WindowHandle"] = $"0x{windowHandle:X}";
        logEvent.Properties["LogType"] = "WindowOperation";
        
        if (!string.IsNullOrEmpty(windowTitle))
        {
            logEvent.Properties["WindowTitle"] = windowTitle;
        }

        // Add additional properties
        foreach (var (key, value) in additionalProperties)
        {
            logEvent.Properties[key] = value;
        }

        _nlogLogger.Log(logEvent);
    }

    public void LogSystemEvent(Microsoft.Extensions.Logging.LogLevel logLevel, string systemComponent, string @event, string message, params (string Key, object Value)[] additionalProperties)
    {
        if (!IsEnabled(logLevel))
            return;

        var nlogLevel = ConvertLogLevel(logLevel);
        var logEvent = new LogEventInfo(nlogLevel, _nlogLogger.Name, message);
        
        // Add service name if available
        if (!string.IsNullOrEmpty(_serviceName))
        {
            logEvent.Properties["ServiceName"] = _serviceName;
        }

        // Add system event properties
        logEvent.Properties["SystemComponent"] = systemComponent;
        logEvent.Properties["Event"] = @event;
        logEvent.Properties["LogType"] = "SystemEvent";

        // Add additional properties
        foreach (var (key, value) in additionalProperties)
        {
            logEvent.Properties[key] = value;
        }

        _nlogLogger.Log(logEvent);
    }

    public IProductionLogger CreateServiceLogger(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
            throw new ArgumentException("Service name cannot be null or empty", nameof(serviceName));

        // Create a new logger with the service name appended
        var serviceLoggerName = $"{_nlogLogger.Name}.{serviceName}";
        var nlogServiceLogger = LogManager.GetLogger(serviceLoggerName);
        
        // Create a corresponding Microsoft Extensions logger using the factory
        var loggerFactory = new NLogLoggerFactory();
        var extensionsServiceLogger = loggerFactory.CreateLogger(serviceLoggerName);
        
        return new ProductionLogger(nlogServiceLogger, extensionsServiceLogger, serviceName);
    }

    public IDisposable BeginPerformanceScope(string serviceName, string operation, params (string Key, object Value)[] additionalContext)
    {
        return new PerformanceScope(this, serviceName, operation, additionalContext);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts Microsoft.Extensions.Logging.LogLevel to NLog.LogLevel
    /// </summary>
    private static NLog.LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel switch
        {
            Microsoft.Extensions.Logging.LogLevel.Trace => NLog.LogLevel.Trace,
            Microsoft.Extensions.Logging.LogLevel.Debug => NLog.LogLevel.Debug,
            Microsoft.Extensions.Logging.LogLevel.Information => NLog.LogLevel.Info,
            Microsoft.Extensions.Logging.LogLevel.Warning => NLog.LogLevel.Warn,
            Microsoft.Extensions.Logging.LogLevel.Error => NLog.LogLevel.Error,
            Microsoft.Extensions.Logging.LogLevel.Critical => NLog.LogLevel.Fatal,
            Microsoft.Extensions.Logging.LogLevel.None => NLog.LogLevel.Off,
            _ => NLog.LogLevel.Info
        };
    }

    #endregion
}

/// <summary>
/// Performance timing scope implementation
/// </summary>
internal class PerformanceScope : IPerformanceScope
{
    private readonly IProductionLogger _logger;
    private readonly string _serviceName;
    private readonly string _operation;
    private readonly Dictionary<string, object> _context;
    private readonly Stopwatch _stopwatch;
    private bool _failed = false;
    private string? _failureReason;
    private bool _disposed = false;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public PerformanceScope(IProductionLogger logger, string serviceName, string operation, params (string Key, object Value)[] additionalContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        
        _context = new Dictionary<string, object>();
        foreach (var (key, value) in additionalContext)
        {
            _context[key] = value;
        }

        _stopwatch = Stopwatch.StartNew();
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