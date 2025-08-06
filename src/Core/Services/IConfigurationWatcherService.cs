using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for monitoring configuration file changes and triggering hot-reload
/// Provides file system watching with debouncing, temporary file filtering, and error handling
/// </summary>
public interface IConfigurationWatcherService : IDisposable
{
    /// <summary>
    /// Gets whether the watcher is currently active and monitoring file changes
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the file path currently being watched for changes
    /// </summary>
    string? WatchedFilePath { get; }

    /// <summary>
    /// Event raised when a configuration file change is detected and processed
    /// Includes the loaded configuration and any validation/processing results
    /// </summary>
    event EventHandler<ConfigurationFileChangedEventArgs>? ConfigurationFileChanged;

    /// <summary>
    /// Event raised when a configuration file change is detected but processing failed
    /// Includes error information for debugging and user notification
    /// </summary>
    event EventHandler<ConfigurationFileChangeFailedEventArgs>? ConfigurationFileChangeFailed;

    /// <summary>
    /// Starts watching the specified configuration file for changes
    /// Uses FileSystemWatcher with debouncing to handle rapid successive changes
    /// </summary>
    /// <param name="configurationFilePath">Path to the configuration file to watch</param>
    /// <param name="debounceDelayMs">Delay in milliseconds to debounce rapid changes (default: 500ms)</param>
    /// <returns>True if watching started successfully, false otherwise</returns>
    Task<bool> StartWatchingAsync(string configurationFilePath, int debounceDelayMs = 500);

    /// <summary>
    /// Stops watching for configuration file changes
    /// Cleans up FileSystemWatcher and any pending timers
    /// </summary>
    /// <returns>Task representing the async stop operation</returns>
    Task StopWatchingAsync();

    /// <summary>
    /// Gets statistics about file watching activity for monitoring/debugging
    /// </summary>
    /// <returns>Watcher statistics including change counts and timing information</returns>
    ConfigurationWatcherStatistics GetStatistics();
}

/// <summary>
/// Event arguments for successful configuration file change events
/// </summary>
public class ConfigurationFileChangedEventArgs : EventArgs
{
    /// <summary>
    /// The newly loaded configuration from the changed file
    /// </summary>
    public CursorPhobiaConfiguration Configuration { get; }

    /// <summary>
    /// Path to the configuration file that changed
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Timestamp when the change was detected and processed
    /// </summary>
    public DateTime ChangeTimestamp { get; }

    /// <summary>
    /// Number of file system events that were debounced into this single change
    /// </summary>
    public int DebouncedEventCount { get; }

    /// <summary>
    /// Creates new configuration file changed event arguments
    /// </summary>
    /// <param name="configuration">The loaded configuration</param>
    /// <param name="filePath">Path to the changed file</param>
    /// <param name="changeTimestamp">When the change was processed</param>
    /// <param name="debouncedEventCount">Number of debounced events</param>
    public ConfigurationFileChangedEventArgs(
        CursorPhobiaConfiguration configuration,
        string filePath,
        DateTime changeTimestamp,
        int debouncedEventCount = 1)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        ChangeTimestamp = changeTimestamp;
        DebouncedEventCount = debouncedEventCount;
    }
}

/// <summary>
/// Event arguments for failed configuration file change events
/// </summary>
public class ConfigurationFileChangeFailedEventArgs : EventArgs
{
    /// <summary>
    /// Path to the configuration file that changed
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Exception that occurred during file processing
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Timestamp when the change was attempted
    /// </summary>
    public DateTime ChangeTimestamp { get; }

    /// <summary>
    /// Reason for the failure (parsing error, file lock, validation, etc.)
    /// </summary>
    public string FailureReason { get; }

    /// <summary>
    /// Number of file system events that were debounced into this failed change attempt
    /// </summary>
    public int DebouncedEventCount { get; }

    /// <summary>
    /// Creates new configuration file change failed event arguments
    /// </summary>
    /// <param name="filePath">Path to the file that failed to process</param>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="changeTimestamp">When the change was attempted</param>
    /// <param name="failureReason">Reason for failure</param>
    /// <param name="debouncedEventCount">Number of debounced events</param>
    public ConfigurationFileChangeFailedEventArgs(
        string filePath,
        Exception exception,
        DateTime changeTimestamp,
        string failureReason,
        int debouncedEventCount = 1)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        ChangeTimestamp = changeTimestamp;
        FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason));
        DebouncedEventCount = debouncedEventCount;
    }
}

/// <summary>
/// Statistics about configuration file watcher activity
/// </summary>
public class ConfigurationWatcherStatistics
{
    /// <summary>
    /// Total number of file system events received
    /// </summary>
    public long TotalFileSystemEvents { get; set; }

    /// <summary>
    /// Number of events that were filtered out (temporary files, etc.)
    /// </summary>
    public long FilteredEvents { get; set; }

    /// <summary>
    /// Number of successful configuration reloads
    /// </summary>
    public long SuccessfulReloads { get; set; }

    /// <summary>
    /// Number of failed configuration reload attempts
    /// </summary>
    public long FailedReloads { get; set; }

    /// <summary>
    /// Number of events that were debounced (combined with other events)
    /// </summary>
    public long DebouncedEvents { get; set; }

    /// <summary>
    /// Average debounce delay in milliseconds
    /// </summary>
    public double AverageDebounceDelayMs { get; set; }

    /// <summary>
    /// When watching was started
    /// </summary>
    public DateTime? WatchingStartedAt { get; set; }

    /// <summary>
    /// Last time a configuration change was successfully processed
    /// </summary>
    public DateTime? LastSuccessfulReload { get; set; }

    /// <summary>
    /// Last time a configuration change failed to process
    /// </summary>
    public DateTime? LastFailedReload { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage
    /// </summary>
    public double SuccessRate
    {
        get
        {
            var totalAttempts = SuccessfulReloads + FailedReloads;
            return totalAttempts > 0 ? (SuccessfulReloads * 100.0 / totalAttempts) : 0.0;
        }
    }

    /// <summary>
    /// Gets the total uptime if currently watching
    /// </summary>
    public TimeSpan? Uptime => WatchingStartedAt.HasValue ? DateTime.UtcNow - WatchingStartedAt.Value : null;
}