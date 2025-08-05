using CursorPhobia.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace CursorPhobia.Tests;

/// <summary>
/// Simple test logger for unit tests
/// </summary>
public class TestLogger : CursorPhobia.Core.Utilities.ILogger
{
    private readonly List<string> _logs = new();

    public List<string> Logs => _logs.AsReadOnly().ToList();

    public void LogDebug(string message, params object[] args)
    {
        try
        {
            _logs.Add($"DEBUG: {(args?.Length > 0 ? string.Format(message, args) : message)}");
        }
        catch
        {
            _logs.Add($"DEBUG: {message} (formatting failed)");
        }
    }

    public void LogInformation(string message, params object[] args)
    {
        try
        {
            _logs.Add($"INFO: {(args?.Length > 0 ? string.Format(message, args) : message)}");
        }
        catch
        {
            _logs.Add($"INFO: {message} (formatting failed)");
        }
    }

    public void LogWarning(string message, params object[] args)
    {
        try
        {
            _logs.Add($"WARN: {(args?.Length > 0 ? string.Format(message, args) : message)}");
        }
        catch
        {
            _logs.Add($"WARN: {message} (formatting failed)");
        }
    }

    public void LogError(string message, params object[] args)
    {
        try
        {
            _logs.Add($"ERROR: {(args?.Length > 0 ? string.Format(message, args) : message)}");
        }
        catch
        {
            _logs.Add($"ERROR: {message} (formatting failed)");
        }
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        try
        {
            var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
            _logs.Add($"ERROR: {formattedMessage} - Exception: {exception.Message}");
        }
        catch
        {
            _logs.Add($"ERROR: {message} (formatting failed) - Exception: {exception.Message}");
        }
    }

    public void LogCritical(string message, params object[] args)
    {
        try
        {
            _logs.Add($"CRITICAL: {(args?.Length > 0 ? string.Format(message, args) : message)}");
        }
        catch
        {
            _logs.Add($"CRITICAL: {message} (formatting failed)");
        }
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        try
        {
            var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
            _logs.Add($"CRITICAL: {formattedMessage} - Exception: {exception.Message}");
        }
        catch
        {
            _logs.Add($"CRITICAL: {message} (formatting failed) - Exception: {exception.Message}");
        }
    }

    public void LogWindowOperation(LogLevel logLevel, string operation, IntPtr windowHandle, string? windowTitle = null, string? message = null, params (string Key, object Value)[] additionalProperties)
    {
        var logEntry = $"{logLevel.ToString().ToUpper()}: WindowOperation - {operation} on 0x{windowHandle:X}";
        if (!string.IsNullOrEmpty(windowTitle))
        {
            logEntry += $" ({windowTitle})";
        }
        if (!string.IsNullOrEmpty(message))
        {
            logEntry += $" - {message}";
        }
        if (additionalProperties?.Length > 0)
        {
            var props = string.Join(", ", additionalProperties.Select(p => $"{p.Key}={p.Value}"));
            logEntry += $" [{props}]";
        }
        _logs.Add(logEntry);
    }

    public void ClearLogs()
    {
        _logs.Clear();
    }
}