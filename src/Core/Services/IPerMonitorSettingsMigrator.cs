using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for managing per-monitor settings during monitor configuration changes
/// </summary>
public interface IPerMonitorSettingsMigrator
{
    /// <summary>
    /// Migrates per-monitor settings when monitor configuration changes
    /// </summary>
    /// <param name="configuration">Configuration to migrate settings for</param>
    /// <param name="oldMonitors">Previous monitor configuration</param>
    /// <param name="newMonitors">New monitor configuration</param>
    /// <returns>Migrated configuration with updated per-monitor settings</returns>
    CursorPhobiaConfiguration MigrateSettings(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> oldMonitors,
        List<MonitorInfo> newMonitors);

    /// <summary>
    /// Cleans up orphaned per-monitor settings for monitors that no longer exist
    /// </summary>
    /// <param name="configuration">Configuration to clean up</param>
    /// <param name="currentMonitors">Currently connected monitors</param>
    /// <returns>Configuration with orphaned settings removed</returns>
    CursorPhobiaConfiguration CleanupOrphanedSettings(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> currentMonitors);

    /// <summary>
    /// Attempts to match a monitor from the old configuration to a monitor in the new configuration
    /// </summary>
    /// <param name="oldMonitor">Monitor from old configuration</param>
    /// <param name="newMonitors">Monitors in new configuration</param>
    /// <returns>Best matching monitor or null if no good match found</returns>
    MonitorInfo? FindBestMatch(MonitorInfo oldMonitor, List<MonitorInfo> newMonitors);

    /// <summary>
    /// Creates default per-monitor settings for newly detected monitors
    /// </summary>
    /// <param name="configuration">Current configuration</param>
    /// <param name="newMonitors">Newly detected monitors</param>
    /// <returns>Configuration with default settings for new monitors</returns>
    CursorPhobiaConfiguration CreateDefaultSettingsForNewMonitors(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> newMonitors);
}