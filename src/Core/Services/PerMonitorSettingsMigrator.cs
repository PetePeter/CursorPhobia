using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for managing per-monitor settings during monitor configuration changes
/// Handles migration, cleanup, and matching of monitor settings during hotplug events
/// </summary>
public class PerMonitorSettingsMigrator : IPerMonitorSettingsMigrator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Creates a new PerMonitorSettingsMigrator instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public PerMonitorSettingsMigrator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Migrates per-monitor settings when monitor configuration changes
    /// </summary>
    /// <param name="configuration">Configuration to migrate settings for</param>
    /// <param name="oldMonitors">Previous monitor configuration</param>
    /// <param name="newMonitors">New monitor configuration</param>
    /// <returns>Migrated configuration with updated per-monitor settings</returns>
    public CursorPhobiaConfiguration MigrateSettings(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> oldMonitors,
        List<MonitorInfo> newMonitors)
    {
        if (configuration?.MultiMonitor?.PerMonitorSettings == null)
        {
            _logger.LogDebug("No per-monitor settings to migrate");
            return configuration ?? CursorPhobiaConfiguration.CreateDefault();
        }

        _logger.LogInformation("Migrating per-monitor settings: {OldCount} -> {NewCount} monitors",
            oldMonitors.Count, newMonitors.Count);

        var migratedConfiguration = CloneConfiguration(configuration);
        var newPerMonitorSettings = new Dictionary<string, PerMonitorSettings>();
        var migratedCount = 0;
        var orphanedCount = 0;

        // Try to migrate existing settings to new monitors
        foreach (var kvp in configuration.MultiMonitor.PerMonitorSettings)
        {
            var oldMonitorKey = kvp.Key;
            var settings = kvp.Value;

            // Find the old monitor that matches this key
            var oldMonitor = oldMonitors.FirstOrDefault(m => m.GetStableKey() == oldMonitorKey);
            if (oldMonitor == null)
            {
                _logger.LogWarning("Could not find old monitor for key: {MonitorKey}", oldMonitorKey);
                orphanedCount++;
                continue;
            }

            // Try to find the best matching new monitor
            var matchingNewMonitor = FindBestMatch(oldMonitor, newMonitors);
            if (matchingNewMonitor != null)
            {
                var newMonitorKey = matchingNewMonitor.GetStableKey();
                newPerMonitorSettings[newMonitorKey] = settings;
                migratedCount++;

                _logger.LogDebug("Migrated settings from {OldKey} to {NewKey} for monitor {DeviceName}",
                    oldMonitorKey, newMonitorKey, matchingNewMonitor.deviceName);
            }
            else
            {
                _logger.LogWarning("Could not find matching new monitor for: {DeviceName} ({OldKey})",
                    oldMonitor.deviceName, oldMonitorKey);
                orphanedCount++;
            }
        }

        // Update the configuration with migrated settings
        migratedConfiguration.MultiMonitor!.PerMonitorSettings = newPerMonitorSettings;

        _logger.LogInformation("Per-monitor settings migration complete: {Migrated} migrated, {Orphaned} orphaned",
            migratedCount, orphanedCount);

        // Create default settings for any completely new monitors
        return CreateDefaultSettingsForNewMonitors(migratedConfiguration, newMonitors);
    }

    /// <summary>
    /// Cleans up orphaned per-monitor settings for monitors that no longer exist
    /// </summary>
    /// <param name="configuration">Configuration to clean up</param>
    /// <param name="currentMonitors">Currently connected monitors</param>
    /// <returns>Configuration with orphaned settings removed</returns>
    public CursorPhobiaConfiguration CleanupOrphanedSettings(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> currentMonitors)
    {
        if (configuration?.MultiMonitor?.PerMonitorSettings == null)
        {
            return configuration ?? CursorPhobiaConfiguration.CreateDefault();
        }

        var cleanedConfiguration = CloneConfiguration(configuration);
        var currentMonitorKeys = new HashSet<string>(currentMonitors.Select(m => m.GetStableKey()));
        var orphanedKeys = new List<string>();

        // Find orphaned settings
        foreach (var kvp in configuration.MultiMonitor.PerMonitorSettings)
        {
            if (!currentMonitorKeys.Contains(kvp.Key))
            {
                orphanedKeys.Add(kvp.Key);
            }
        }

        // Remove orphaned settings
        foreach (var orphanedKey in orphanedKeys)
        {
            cleanedConfiguration.MultiMonitor!.PerMonitorSettings.Remove(orphanedKey);
            _logger.LogDebug("Removed orphaned per-monitor settings for key: {MonitorKey}", orphanedKey);
        }

        if (orphanedKeys.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} orphaned per-monitor settings", orphanedKeys.Count);
        }

        return cleanedConfiguration;
    }

    /// <summary>
    /// Attempts to match a monitor from the old configuration to a monitor in the new configuration
    /// </summary>
    /// <param name="oldMonitor">Monitor from old configuration</param>
    /// <param name="newMonitors">Monitors in new configuration</param>
    /// <returns>Best matching monitor or null if no good match found</returns>
    public MonitorInfo? FindBestMatch(MonitorInfo oldMonitor, List<MonitorInfo> newMonitors)
    {
        if (newMonitors.Count == 0)
            return null;

        // 1. Try exact stable ID match first (best case)
        var exactMatch = newMonitors.FirstOrDefault(m => m.GetStableKey() == oldMonitor.GetStableKey());
        if (exactMatch != null)
        {
            _logger.LogDebug("Found exact stable ID match for monitor: {DeviceName}", oldMonitor.deviceName);
            return exactMatch;
        }

        // 2. Try device name match
        var deviceNameMatch = newMonitors.FirstOrDefault(m =>
            string.Equals(m.deviceName, oldMonitor.deviceName, StringComparison.OrdinalIgnoreCase));
        if (deviceNameMatch != null)
        {
            _logger.LogDebug("Found device name match for monitor: {DeviceName}", oldMonitor.deviceName);
            return deviceNameMatch;
        }

        // 3. Try size and primary status match
        var sizeAndPrimaryMatch = newMonitors.FirstOrDefault(m =>
            m.width == oldMonitor.width &&
            m.height == oldMonitor.height &&
            m.isPrimary == oldMonitor.isPrimary);
        if (sizeAndPrimaryMatch != null)
        {
            _logger.LogDebug("Found size and primary status match for monitor: {Width}x{Height} (Primary: {IsPrimary})",
                oldMonitor.width, oldMonitor.height, oldMonitor.isPrimary);
            return sizeAndPrimaryMatch;
        }

        // 4. Try size match only
        var sizeMatch = newMonitors.FirstOrDefault(m =>
            m.width == oldMonitor.width && m.height == oldMonitor.height);
        if (sizeMatch != null)
        {
            _logger.LogDebug("Found size match for monitor: {Width}x{Height}",
                oldMonitor.width, oldMonitor.height);
            return sizeMatch;
        }

        // 5. If old monitor was primary, match to new primary
        if (oldMonitor.isPrimary)
        {
            var newPrimary = newMonitors.FirstOrDefault(m => m.isPrimary);
            if (newPrimary != null)
            {
                _logger.LogDebug("Matched old primary monitor to new primary monitor");
                return newPrimary;
            }
        }

        // 6. No good match found
        _logger.LogDebug("No suitable match found for monitor: {DeviceName} ({Width}x{Height})",
            oldMonitor.deviceName, oldMonitor.width, oldMonitor.height);
        return null;
    }

    /// <summary>
    /// Creates default per-monitor settings for newly detected monitors
    /// </summary>
    /// <param name="configuration">Current configuration</param>
    /// <param name="newMonitors">Newly detected monitors</param>
    /// <returns>Configuration with default settings for new monitors</returns>
    public CursorPhobiaConfiguration CreateDefaultSettingsForNewMonitors(
        CursorPhobiaConfiguration configuration,
        List<MonitorInfo> newMonitors)
    {
        if (newMonitors.Count == 0)
            return configuration;

        var updatedConfiguration = CloneConfiguration(configuration);

        // Ensure MultiMonitor configuration exists
        updatedConfiguration.MultiMonitor ??= new MultiMonitorConfiguration();
        updatedConfiguration.MultiMonitor.PerMonitorSettings ??= new Dictionary<string, PerMonitorSettings>();

        var existingKeys = new HashSet<string>(updatedConfiguration.MultiMonitor.PerMonitorSettings.Keys);
        var newMonitorCount = 0;

        foreach (var monitor in newMonitors)
        {
            var monitorKey = monitor.GetStableKey();

            // Skip if settings already exist for this monitor
            if (existingKeys.Contains(monitorKey))
                continue;

            // Create default settings for new monitor
            var defaultSettings = new PerMonitorSettings
            {
                Enabled = true,
                CustomProximityThreshold = null, // Use global settings by default
                CustomPushDistance = null        // Use global settings by default
            };

            updatedConfiguration.MultiMonitor.PerMonitorSettings[monitorKey] = defaultSettings;
            newMonitorCount++;

            _logger.LogDebug("Created default settings for new monitor: {DeviceName} ({MonitorKey})",
                monitor.deviceName, monitorKey);
        }

        if (newMonitorCount > 0)
        {
            _logger.LogInformation("Created default settings for {Count} new monitors", newMonitorCount);
        }

        return updatedConfiguration;
    }

    /// <summary>
    /// Creates a deep copy of the configuration for safe modification
    /// </summary>
    private static CursorPhobiaConfiguration CloneConfiguration(CursorPhobiaConfiguration original)
    {
        var cloned = new CursorPhobiaConfiguration
        {
            ProximityThreshold = original.ProximityThreshold,
            PushDistance = original.PushDistance,
            UpdateIntervalMs = original.UpdateIntervalMs,
            MaxUpdateIntervalMs = original.MaxUpdateIntervalMs,
            EnableCtrlOverride = original.EnableCtrlOverride,
            ScreenEdgeBuffer = original.ScreenEdgeBuffer,
            ApplyToAllWindows = original.ApplyToAllWindows,
            AnimationDurationMs = original.AnimationDurationMs,
            EnableAnimations = original.EnableAnimations,
            AnimationEasing = original.AnimationEasing,
            HoverTimeoutMs = original.HoverTimeoutMs,
            EnableHoverTimeout = original.EnableHoverTimeout,
            MultiMonitor = CloneMultiMonitorConfiguration(original.MultiMonitor)
        };

        return cloned;
    }

    /// <summary>
    /// Creates a deep copy of multi-monitor configuration
    /// </summary>
    private static MultiMonitorConfiguration? CloneMultiMonitorConfiguration(MultiMonitorConfiguration? original)
    {
        if (original == null) return null;

        var cloned = new MultiMonitorConfiguration
        {
            EnableWrapping = original.EnableWrapping,
            PreferredWrapBehavior = original.PreferredWrapBehavior,
            RespectTaskbarAreas = original.RespectTaskbarAreas,
            PerMonitorSettings = new Dictionary<string, PerMonitorSettings>()
        };

        foreach (var kvp in original.PerMonitorSettings)
        {
            if (kvp.Value != null)
            {
                cloned.PerMonitorSettings[kvp.Key] = new PerMonitorSettings
                {
                    Enabled = kvp.Value.Enabled,
                    CustomProximityThreshold = kvp.Value.CustomProximityThreshold,
                    CustomPushDistance = kvp.Value.CustomPushDistance,
                    CustomEnableWrapping = kvp.Value.CustomEnableWrapping,
                    CustomWrapPreference = kvp.Value.CustomWrapPreference
                };
            }
        }

        return cloned;
    }
}