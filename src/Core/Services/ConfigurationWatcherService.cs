using System.Collections.Concurrent;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for monitoring configuration file changes and triggering hot-reload
/// Implements FileSystemWatcher with debouncing, temporary file filtering, and robust error handling
/// </summary>
public class ConfigurationWatcherService : IConfigurationWatcherService
{
    private readonly ILogger _logger;
    private readonly IConfigurationService _configurationService;
    private readonly ISystemTrayManager? _trayManager;

    // File watching infrastructure
    private FileSystemWatcher? _fileWatcher;
    private System.Timers.Timer? _debounceTimer;
    private readonly object _watcherLock = new();
    private volatile bool _disposed = false;

    // Configuration
    private string? _watchedFilePath;
    private int _debounceDelayMs = 500;

    // Debouncing state
    private volatile int _pendingEventCount = 0;
    private DateTime _lastEventTime = DateTime.MinValue;
    private readonly object _debounceStateLock = new();

    // Statistics tracking
    private readonly ConfigurationWatcherStatistics _statistics = new();
    private readonly object _statisticsLock = new();

    // Temporary file patterns to ignore
    private static readonly string[] TempFilePatterns = new[]
    {
        ".tmp", ".temp", ".bak", ".backup", ".swp", ".swo", "~",
        ".part", ".download", ".crdownload", ".partial"
    };

    // Editor-specific temporary file patterns
    private static readonly string[] EditorTempPatterns = new[]
    {
        ".#", "#", "~$", ".lock", ".orig", ".rej"
    };

    /// <summary>
    /// Gets whether the watcher is currently active and monitoring file changes
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_watcherLock)
            {
                return _fileWatcher?.EnableRaisingEvents == true && !_disposed;
            }
        }
    }

    /// <summary>
    /// Gets the file path currently being watched for changes
    /// </summary>
    public string? WatchedFilePath
    {
        get
        {
            lock (_watcherLock)
            {
                return _watchedFilePath;
            }
        }
    }

    /// <summary>
    /// Event raised when a configuration file change is detected and processed
    /// </summary>
    public event EventHandler<ConfigurationFileChangedEventArgs>? ConfigurationFileChanged;

    /// <summary>
    /// Event raised when a configuration file change is detected but processing failed
    /// </summary>
    public event EventHandler<ConfigurationFileChangeFailedEventArgs>? ConfigurationFileChangeFailed;

    /// <summary>
    /// Creates a new ConfigurationWatcherService
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="configurationService">Service for loading configuration files</param>
    /// <param name="trayManager">Optional tray manager for showing notifications</param>
    public ConfigurationWatcherService(
        ILogger logger,
        IConfigurationService configurationService,
        ISystemTrayManager? trayManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _trayManager = trayManager;
    }

    /// <summary>
    /// Starts watching the specified configuration file for changes
    /// </summary>
    /// <param name="configurationFilePath">Path to the configuration file to watch</param>
    /// <param name="debounceDelayMs">Delay in milliseconds to debounce rapid changes</param>
    /// <returns>True if watching started successfully, false otherwise</returns>
    public async Task<bool> StartWatchingAsync(string configurationFilePath, int debounceDelayMs = 500)
    {
        if (string.IsNullOrWhiteSpace(configurationFilePath))
            throw new ArgumentException("Configuration file path cannot be null or empty", nameof(configurationFilePath));

        if (debounceDelayMs < 100 || debounceDelayMs > 10000)
            throw new ArgumentOutOfRangeException(nameof(debounceDelayMs), "Debounce delay must be between 100ms and 10000ms");

        if (_disposed)
        {
            _logger.LogWarning("Cannot start watching on disposed ConfigurationWatcherService");
            return false;
        }

        try
        {
            // Stop any existing watcher
            await StopWatchingAsync();

            var fullPath = Path.GetFullPath(configurationFilePath);
            var directory = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                _logger.LogError("Invalid configuration file path: {Path}", configurationFilePath);
                return false;
            }

            // Ensure the directory exists
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Configuration directory does not exist, creating: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            // Verify the file exists or can be created
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("Configuration file does not exist yet: {Path}", fullPath);
                // Don't fail - we'll watch for it to be created
            }

            lock (_watcherLock)
            {
                if (_disposed) return false;

                // Create FileSystemWatcher
                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    EnableRaisingEvents = false
                };

                // Subscribe to events
                _fileWatcher.Changed += OnFileSystemEvent;
                _fileWatcher.Created += OnFileSystemEvent;
                _fileWatcher.Error += OnFileSystemError;

                // Create debounce timer
                _debounceDelayMs = debounceDelayMs;
                _debounceTimer = new System.Timers.Timer(_debounceDelayMs)
                {
                    AutoReset = false
                };
                _debounceTimer.Elapsed += OnDebounceTimerElapsed;

                // Update state
                _watchedFilePath = fullPath;

                // Reset statistics
                lock (_statisticsLock)
                {
                    _statistics.WatchingStartedAt = DateTime.UtcNow;
                    _statistics.TotalFileSystemEvents = 0;
                    _statistics.FilteredEvents = 0;
                    _statistics.SuccessfulReloads = 0;
                    _statistics.FailedReloads = 0;
                    _statistics.DebouncedEvents = 0;
                    _statistics.AverageDebounceDelayMs = _debounceDelayMs;
                }

                // Start watching
                _fileWatcher.EnableRaisingEvents = true;
            }

            _logger.LogInformation("Started watching configuration file: {Path} (debounce: {Delay}ms)",
                fullPath, debounceDelayMs);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching configuration file: {Path}", configurationFilePath);
            return false;
        }
    }

    /// <summary>
    /// Stops watching for configuration file changes
    /// </summary>
    public async Task StopWatchingAsync()
    {
        try
        {
            lock (_watcherLock)
            {
                if (_fileWatcher != null)
                {
                    try
                    {
                        _fileWatcher.EnableRaisingEvents = false;
                        _fileWatcher.Changed -= OnFileSystemEvent;
                        _fileWatcher.Created -= OnFileSystemEvent;
                        _fileWatcher.Error -= OnFileSystemError;
                        _fileWatcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error disposing FileSystemWatcher: {Error}", ex.Message);
                    }
                    finally
                    {
                        _fileWatcher = null;
                    }
                }

                if (_debounceTimer != null)
                {
                    try
                    {
                        _debounceTimer.Stop();
                        _debounceTimer.Elapsed -= OnDebounceTimerElapsed;
                        _debounceTimer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error disposing debounce timer: {Error}", ex.Message);
                    }
                    finally
                    {
                        _debounceTimer = null;
                    }
                }

                _watchedFilePath = null;
            }

            // Reset debounce state
            lock (_debounceStateLock)
            {
                _pendingEventCount = 0;
                _lastEventTime = DateTime.MinValue;
            }

            _logger.LogDebug("Stopped watching configuration file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping configuration file watcher");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets statistics about file watching activity
    /// </summary>
    public ConfigurationWatcherStatistics GetStatistics()
    {
        lock (_statisticsLock)
        {
            // Return a copy to avoid thread safety issues
            return new ConfigurationWatcherStatistics
            {
                TotalFileSystemEvents = _statistics.TotalFileSystemEvents,
                FilteredEvents = _statistics.FilteredEvents,
                SuccessfulReloads = _statistics.SuccessfulReloads,
                FailedReloads = _statistics.FailedReloads,
                DebouncedEvents = _statistics.DebouncedEvents,
                AverageDebounceDelayMs = _statistics.AverageDebounceDelayMs,
                WatchingStartedAt = _statistics.WatchingStartedAt,
                LastSuccessfulReload = _statistics.LastSuccessfulReload,
                LastFailedReload = _statistics.LastFailedReload
            };
        }
    }

    /// <summary>
    /// Handles file system events from the FileSystemWatcher
    /// </summary>
    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;

        try
        {
            // Update statistics
            lock (_statisticsLock)
            {
                _statistics.TotalFileSystemEvents++;
            }

            _logger.LogDebug("File system event: {EventType} - {Path}", e.ChangeType, e.FullPath);

            // Filter out temporary files
            if (IsTemporaryFile(e.FullPath))
            {
                lock (_statisticsLock)
                {
                    _statistics.FilteredEvents++;
                }
                _logger.LogDebug("Filtered temporary file: {Path}", e.FullPath);
                return;
            }

            // Check if this is the file we're watching
            var watchedPath = WatchedFilePath;
            if (watchedPath == null || !string.Equals(e.FullPath, watchedPath, StringComparison.OrdinalIgnoreCase))
            {
                lock (_statisticsLock)
                {
                    _statistics.FilteredEvents++;
                }
                _logger.LogDebug("Filtered non-target file: {Path}", e.FullPath);
                return;
            }

            // Handle debouncing
            lock (_debounceStateLock)
            {
                _pendingEventCount++;
                _lastEventTime = DateTime.UtcNow;

                // Restart the debounce timer
                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
                }
            }

            _logger.LogDebug("Configuration file change detected, pending debounce: {Path} (events: {Count})",
                e.FullPath, _pendingEventCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file system event for {Path}", e.FullPath);
        }
    }

    /// <summary>
    /// Handles errors from the FileSystemWatcher
    /// </summary>
    private void OnFileSystemError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error occurred");

        // Try to restart the watcher if possible
        Task.Run(async () =>
        {
            var currentPath = WatchedFilePath;
            if (!string.IsNullOrEmpty(currentPath))
            {
                _logger.LogInformation("Attempting to restart file watcher after error");
                await Task.Delay(1000); // Brief delay before restart
                await StartWatchingAsync(currentPath, _debounceDelayMs);
            }
        });
    }

    /// <summary>
    /// Handles the debounce timer elapsed event - processes the configuration change
    /// </summary>
    private async void OnDebounceTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_disposed) return;

        var eventCount = 0;
        var watchedPath = string.Empty;

        try
        {
            // Get the pending event info
            lock (_debounceStateLock)
            {
                eventCount = _pendingEventCount;
                _pendingEventCount = 0;
            }

            watchedPath = WatchedFilePath ?? string.Empty;

            if (eventCount == 0 || string.IsNullOrEmpty(watchedPath))
            {
                return;
            }

            _logger.LogDebug("Processing debounced configuration change: {Path} ({Count} events)",
                watchedPath, eventCount);

            // Update debounce statistics
            lock (_statisticsLock)
            {
                _statistics.DebouncedEvents += eventCount - 1; // First event isn't debounced
            }

            await ProcessConfigurationChangeAsync(watchedPath, eventCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debounced configuration change for {Path}", watchedPath);

            // Fire the failed event
            var failedArgs = new ConfigurationFileChangeFailedEventArgs(
                watchedPath,
                ex,
                DateTime.UtcNow,
                "Error processing debounced change",
                eventCount);

            ConfigurationFileChangeFailed?.Invoke(this, failedArgs);

            lock (_statisticsLock)
            {
                _statistics.FailedReloads++;
                _statistics.LastFailedReload = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Processes a configuration file change by loading and validating the new configuration
    /// </summary>
    private async Task ProcessConfigurationChangeAsync(string filePath, int eventCount)
    {
        try
        {
            // Wait a moment for file operations to complete (some editors write in multiple operations)
            await Task.Delay(50);

            // Check if file exists and is not locked
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Configuration file no longer exists during processing: {Path}", filePath);
                return;
            }

            // Try to load the configuration with retries for file locks
            CursorPhobiaConfiguration? newConfiguration = null;
            Exception? lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    newConfiguration = await _configurationService.LoadConfigurationAsync(filePath);
                    lastException = null;
                    break;
                }
                catch (IOException ioEx) when (IsFileLocked(ioEx))
                {
                    lastException = ioEx;
                    _logger.LogDebug("Configuration file is locked, retrying in {Delay}ms (attempt {Attempt})",
                        100 * (attempt + 1), attempt + 1);
                    await Task.Delay(100 * (attempt + 1));
                }
            }

            if (newConfiguration == null || lastException != null)
            {
                throw lastException ?? new InvalidOperationException("Failed to load configuration after retries");
            }

            // Validate the configuration
            var validationErrors = newConfiguration.Validate();
            if (validationErrors.Count > 0)
            {
                var validationException = new InvalidOperationException(
                    $"Configuration validation failed: {string.Join(", ", validationErrors)}");

                _logger.LogWarning("Loaded configuration failed validation: {Errors}",
                    string.Join(", ", validationErrors));

                var failedArgs = new ConfigurationFileChangeFailedEventArgs(
                    filePath,
                    validationException,
                    DateTime.UtcNow,
                    "Configuration validation failed",
                    eventCount);

                ConfigurationFileChangeFailed?.Invoke(this, failedArgs);

                // Show tray notification for validation errors
                if (_trayManager != null)
                {
                    await _trayManager.ShowNotificationAsync("CursorPhobia Configuration",
                        $"Configuration validation failed: {validationErrors.First()}", true);
                }

                lock (_statisticsLock)
                {
                    _statistics.FailedReloads++;
                    _statistics.LastFailedReload = DateTime.UtcNow;
                }

                return;
            }

            // Success - fire the event
            var changeArgs = new ConfigurationFileChangedEventArgs(
                newConfiguration,
                filePath,
                DateTime.UtcNow,
                eventCount);

            ConfigurationFileChanged?.Invoke(this, changeArgs);

            _logger.LogInformation("Configuration file successfully reloaded: {Path}", filePath);

            // Show tray notification for successful reload
            if (_trayManager != null)
            {
                await _trayManager.ShowNotificationAsync("CursorPhobia Configuration",
                    "Configuration reloaded successfully", false);
            }

            lock (_statisticsLock)
            {
                _statistics.SuccessfulReloads++;
                _statistics.LastSuccessfulReload = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process configuration change for {Path}", filePath);

            var failedArgs = new ConfigurationFileChangeFailedEventArgs(
                filePath,
                ex,
                DateTime.UtcNow,
                "Configuration processing failed",
                eventCount);

            ConfigurationFileChangeFailed?.Invoke(this, failedArgs);

            // Show tray notification for processing errors
            if (_trayManager != null)
            {
                await _trayManager.ShowNotificationAsync("CursorPhobia Configuration",
                    $"Configuration reload failed: {ex.Message}", true);
            }

            lock (_statisticsLock)
            {
                _statistics.FailedReloads++;
                _statistics.LastFailedReload = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Checks if a file path represents a temporary file that should be ignored
    /// </summary>
    private static bool IsTemporaryFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return true;

        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath);

        // Check common temporary file patterns
        foreach (var pattern in TempFilePatterns)
        {
            if (fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check editor-specific temporary file patterns
        foreach (var pattern in EditorTempPatterns)
        {
            if (fileName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for numbered backup files (file.json.1, file.json.bak, etc.)
        if (fileName.Contains(".json.") &&
            (char.IsDigit(fileName.Last()) || fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if an IOException is likely due to file locking
    /// </summary>
    private static bool IsFileLocked(IOException ioException)
    {
        var errorCode = ioException.HResult & 0xFFFF;
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION or ERROR_LOCK_VIOLATION
    }

    /// <summary>
    /// Disposes of the configuration watcher service
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            StopWatchingAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error during ConfigurationWatcherService disposal: {Error}", ex.Message);
        }

        GC.SuppressFinalize(this);
    }
}