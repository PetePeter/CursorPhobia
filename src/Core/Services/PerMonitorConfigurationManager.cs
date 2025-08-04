using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service that manages per-monitor configuration during monitor changes
/// Coordinates with monitor configuration watcher and settings migrator
/// </summary>
public class PerMonitorConfigurationManager : IPerMonitorConfigurationManager
{
    private readonly IMonitorConfigurationWatcher _monitorWatcher;
    private readonly IPerMonitorSettingsMigrator _settingsMigrator;
    private readonly IMonitorManager _monitorManager;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger _logger;

    private readonly ReaderWriterLockSlim _stateLock = new();
    private bool _disposed;
    private List<MonitorInfo> _lastKnownMonitors = new();

    /// <summary>
    /// Event raised when per-monitor settings are migrated due to monitor configuration changes
    /// </summary>
    public event EventHandler<PerMonitorSettingsMigratedEventArgs>? SettingsMigrated;

    /// <summary>
    /// Gets whether the manager is currently monitoring for changes
    /// </summary>
    public bool IsMonitoring 
    { 
        get 
        {
            try
            {
                _stateLock.EnterReadLock();
                return !_disposed && _monitorWatcher.IsMonitoring;
            }
            finally
            {
                _stateLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Creates a new PerMonitorConfigurationManager instance
    /// </summary>
    /// <param name="monitorWatcher">Service for monitoring monitor configuration changes</param>
    /// <param name="settingsMigrator">Service for migrating per-monitor settings</param>
    /// <param name="monitorManager">Service for querying monitor information</param>
    /// <param name="configurationService">Service for loading/saving configuration</param>
    /// <param name="logger">Logger for diagnostic output</param>
    public PerMonitorConfigurationManager(
        IMonitorConfigurationWatcher monitorWatcher,
        IPerMonitorSettingsMigrator settingsMigrator,
        IMonitorManager monitorManager,
        IConfigurationService configurationService,
        ILogger logger)
    {
        _monitorWatcher = monitorWatcher ?? throw new ArgumentNullException(nameof(monitorWatcher));
        _settingsMigrator = settingsMigrator ?? throw new ArgumentNullException(nameof(settingsMigrator));
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to monitor configuration changes
        _monitorWatcher.MonitorConfigurationChanged += OnMonitorConfigurationChanged;
    }

    /// <summary>
    /// Starts monitoring for monitor configuration changes and handles automatic migration
    /// </summary>
    public void StartMonitoring()
    {
        try
        {
            _stateLock.EnterWriteLock();
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerMonitorConfigurationManager));

            try
            {
                // Initialize last known monitors
                _lastKnownMonitors = _monitorManager.GetAllMonitors();
                
                // Start the monitor watcher
                _monitorWatcher.StartMonitoring();
                
                _logger.LogInformation("PerMonitorConfigurationManager started monitoring with {Count} monitors",
                    _lastKnownMonitors.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start per-monitor configuration monitoring: {ex.Message}");
                throw;
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Stops monitoring for monitor configuration changes
    /// </summary>
    public void StopMonitoring()
    {
        try
        {
            _stateLock.EnterWriteLock();
            
            if (_disposed)
                return;

            try
            {
                _monitorWatcher.StopMonitoring();
                _lastKnownMonitors.Clear();
                
                _logger.LogInformation("PerMonitorConfigurationManager stopped monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping per-monitor configuration monitoring: {ex.Message}");
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Manually triggers a migration of per-monitor settings based on current monitor configuration
    /// </summary>
    /// <param name="configuration">Configuration to migrate</param>
    /// <returns>Migrated configuration</returns>
    public Task<CursorPhobiaConfiguration> MigrateCurrentConfigurationAsync(CursorPhobiaConfiguration configuration)
    {
        List<MonitorInfo> lastKnownMonitors;
        
        try
        {
            _stateLock.EnterReadLock();
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerMonitorConfigurationManager));
                
            lastKnownMonitors = new List<MonitorInfo>(_lastKnownMonitors);
        }
        finally
        {
            _stateLock.ExitReadLock();
        }
        
        try
        {
            var currentMonitors = _monitorManager.GetAllMonitors();
            var migratedConfiguration = _settingsMigrator.MigrateSettings(configuration, lastKnownMonitors, currentMonitors);
            
            // Update our known monitors under write lock
            try
            {
                _stateLock.EnterWriteLock();
                
                if (!_disposed)
                {
                    _lastKnownMonitors = currentMonitors;
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }
            
            _logger.LogInformation("Manual per-monitor configuration migration completed");
            return Task.FromResult(migratedConfiguration);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during manual configuration migration: {ex.Message}");
            return Task.FromResult(configuration); // Return original on error
        }
    }

    /// <summary>
    /// Cleans up orphaned per-monitor settings for the current monitor configuration
    /// </summary>
    /// <param name="configuration">Configuration to clean up</param>
    /// <returns>Cleaned configuration</returns>
    public Task<CursorPhobiaConfiguration> CleanupCurrentConfigurationAsync(CursorPhobiaConfiguration configuration)
    {
        try
        {
            var currentMonitors = _monitorManager.GetAllMonitors();
            var cleanedConfiguration = _settingsMigrator.CleanupOrphanedSettings(configuration, currentMonitors);
            
            _logger.LogInformation("Per-monitor configuration cleanup completed");
            return Task.FromResult(cleanedConfiguration);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during configuration cleanup: {ex.Message}");
            return Task.FromResult(configuration); // Return original on error
        }
    }

    /// <summary>
    /// Handles monitor configuration change events
    /// </summary>
    private async void OnMonitorConfigurationChanged(object? sender, MonitorChangeEventArgs e)
    {
        List<MonitorInfo> oldMonitors;
        
        try
        {
            _stateLock.EnterReadLock();
            
            if (_disposed)
                return;
                
            oldMonitors = new List<MonitorInfo>(_lastKnownMonitors);
        }
        finally
        {
            _stateLock.ExitReadLock();
        }

        try
        {
            _logger.LogInformation("Monitor configuration changed, starting per-monitor settings migration");

            // Get current configuration
            var configPath = await _configurationService.GetDefaultConfigurationPathAsync();
            var currentConfiguration = await _configurationService.LoadConfigurationAsync(configPath);

            // Perform migration
            var newMonitors = e.CurrentMonitors.ToList();
            
            var migratedConfiguration = _settingsMigrator.MigrateSettings(currentConfiguration, oldMonitors, newMonitors);

            // Calculate migration statistics
            var migratedCount = CountMigratedSettings(currentConfiguration, migratedConfiguration, oldMonitors, newMonitors);
            var orphanedCount = CountOrphanedSettings(currentConfiguration, migratedConfiguration);
            var newMonitorCount = CountNewMonitors(oldMonitors, newMonitors);

            // Save the migrated configuration
            await _configurationService.SaveConfigurationAsync(migratedConfiguration, configPath);

            // Update our tracking under write lock
            try
            {
                _stateLock.EnterWriteLock();
                
                if (!_disposed)
                {
                    _lastKnownMonitors = new List<MonitorInfo>(newMonitors);
                }
            }
            finally
            {
                _stateLock.ExitWriteLock();
            }

            // Raise migration event (outside lock to prevent deadlocks)
            var migrationEventArgs = new PerMonitorSettingsMigratedEventArgs(
                currentConfiguration,
                migratedConfiguration,
                e,
                migratedCount,
                orphanedCount,
                newMonitorCount);

            try
            {
                SettingsMigrated?.Invoke(this, migrationEventArgs);
            }
            catch (Exception eventEx)
            {
                _logger.LogError($"Error notifying settings migration subscribers: {eventEx.Message}");
            }

            _logger.LogInformation("Per-monitor settings migration completed: {Migrated} migrated, {Orphaned} orphaned, {New} new",
                migratedCount, orphanedCount, newMonitorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error during automatic per-monitor settings migration: {ex.Message}");
        }
    }

    /// <summary>
    /// Counts the number of settings that were successfully migrated
    /// </summary>
    private int CountMigratedSettings(CursorPhobiaConfiguration original, CursorPhobiaConfiguration migrated,
        List<MonitorInfo> oldMonitors, List<MonitorInfo> newMonitors)
    {
        if (original.MultiMonitor?.PerMonitorSettings == null || migrated.MultiMonitor?.PerMonitorSettings == null)
            return 0;

        // Count settings that existed in original and still exist in migrated (but potentially with different keys)
        var originalCount = original.MultiMonitor.PerMonitorSettings.Count;
        var migratedCount = migrated.MultiMonitor.PerMonitorSettings.Count;
        var newMonitorCount = newMonitors.Count - oldMonitors.Count;

        // Migrated count is the minimum of what we had and what we kept, minus any purely new monitors
        return Math.Max(0, Math.Min(originalCount, migratedCount - Math.Max(0, newMonitorCount)));
    }

    /// <summary>
    /// Counts the number of orphaned settings that were removed
    /// </summary>
    private int CountOrphanedSettings(CursorPhobiaConfiguration original, CursorPhobiaConfiguration migrated)
    {
        if (original.MultiMonitor?.PerMonitorSettings == null)
            return 0;

        var originalCount = original.MultiMonitor.PerMonitorSettings.Count;
        var migratedCount = migrated.MultiMonitor?.PerMonitorSettings?.Count ?? 0;

        return Math.Max(0, originalCount - migratedCount);
    }

    /// <summary>
    /// Counts the number of new monitors
    /// </summary>
    private int CountNewMonitors(List<MonitorInfo> oldMonitors, List<MonitorInfo> newMonitors)
    {
        return Math.Max(0, newMonitors.Count - oldMonitors.Count);
    }

    /// <summary>
    /// Disposes the manager and releases all resources
    /// </summary>
    public void Dispose()
    {
        try
        {
            _stateLock.EnterWriteLock();
            
            if (_disposed)
                return;

            try
            {
                // Unsubscribe from events
                _monitorWatcher.MonitorConfigurationChanged -= OnMonitorConfigurationChanged;

                // Stop monitoring
                _monitorWatcher.StopMonitoring();
                _lastKnownMonitors.Clear();

                _disposed = true;
                _logger.LogDebug("PerMonitorConfigurationManager disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during PerMonitorConfigurationManager disposal: {ex.Message}");
            }
        }
        finally
        {
            _stateLock.ExitWriteLock();
            _stateLock.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}