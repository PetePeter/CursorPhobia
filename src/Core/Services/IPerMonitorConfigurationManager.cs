using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for managing per-monitor configuration during monitor changes
/// </summary>
public interface IPerMonitorConfigurationManager : IDisposable
{
    /// <summary>
    /// Event raised when per-monitor settings are migrated due to monitor configuration changes
    /// </summary>
    event EventHandler<PerMonitorSettingsMigratedEventArgs>? SettingsMigrated;

    /// <summary>
    /// Starts monitoring for monitor configuration changes and handles automatic migration
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stops monitoring for monitor configuration changes
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Gets whether the manager is currently monitoring for changes
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Manually triggers a migration of per-monitor settings based on current monitor configuration
    /// </summary>
    /// <param name="configuration">Configuration to migrate</param>
    /// <returns>Migrated configuration</returns>
    Task<CursorPhobiaConfiguration> MigrateCurrentConfigurationAsync(CursorPhobiaConfiguration configuration);

    /// <summary>
    /// Cleans up orphaned per-monitor settings for the current monitor configuration
    /// </summary>
    /// <param name="configuration">Configuration to clean up</param>
    /// <returns>Cleaned configuration</returns>
    Task<CursorPhobiaConfiguration> CleanupCurrentConfigurationAsync(CursorPhobiaConfiguration configuration);
}

/// <summary>
/// Event arguments for per-monitor settings migration events
/// </summary>
public class PerMonitorSettingsMigratedEventArgs : EventArgs
{
    /// <summary>
    /// The original configuration before migration
    /// </summary>
    public CursorPhobiaConfiguration OriginalConfiguration { get; }

    /// <summary>
    /// The migrated configuration after changes
    /// </summary>
    public CursorPhobiaConfiguration MigratedConfiguration { get; }

    /// <summary>
    /// The monitor change event that triggered the migration
    /// </summary>
    public MonitorChangeEventArgs MonitorChange { get; }

    /// <summary>
    /// Number of settings that were successfully migrated
    /// </summary>
    public int MigratedSettingsCount { get; }

    /// <summary>
    /// Number of orphaned settings that were removed
    /// </summary>
    public int OrphanedSettingsCount { get; }

    /// <summary>
    /// Number of new monitors that received default settings
    /// </summary>
    public int NewMonitorsCount { get; }

    /// <summary>
    /// Creates new migration event arguments
    /// </summary>
    public PerMonitorSettingsMigratedEventArgs(
        CursorPhobiaConfiguration originalConfiguration,
        CursorPhobiaConfiguration migratedConfiguration,
        MonitorChangeEventArgs monitorChange,
        int migratedSettingsCount = 0,
        int orphanedSettingsCount = 0,
        int newMonitorsCount = 0)
    {
        OriginalConfiguration = originalConfiguration;
        MigratedConfiguration = migratedConfiguration;
        MonitorChange = monitorChange;
        MigratedSettingsCount = migratedSettingsCount;
        OrphanedSettingsCount = orphanedSettingsCount;
        NewMonitorsCount = newMonitorsCount;
    }
}