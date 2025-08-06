namespace CursorPhobia.Core.Models;

/// <summary>
/// Analyzes changes between two CursorPhobiaConfiguration instances to determine hot-swap compatibility
/// </summary>
public class ConfigurationChangeAnalysis
{
    /// <summary>
    /// The original configuration being replaced
    /// </summary>
    public CursorPhobiaConfiguration OldConfiguration { get; }

    /// <summary>
    /// The new configuration being applied
    /// </summary>
    public CursorPhobiaConfiguration NewConfiguration { get; }

    /// <summary>
    /// Settings that can be applied immediately without engine restart
    /// </summary>
    public List<string> HotSwappableChanges { get; } = new();

    /// <summary>
    /// Settings that require an engine restart to take effect
    /// </summary>
    public List<string> RestartRequiredChanges { get; } = new();

    /// <summary>
    /// Whether any changes require an engine restart
    /// </summary>
    public bool RequiresRestart => RestartRequiredChanges.Count > 0;

    /// <summary>
    /// Whether there are any changes at all
    /// </summary>
    public bool HasChanges => HotSwappableChanges.Count > 0 || RestartRequiredChanges.Count > 0;

    /// <summary>
    /// Creates a new configuration change analysis
    /// </summary>
    /// <param name="oldConfiguration">The original configuration</param>
    /// <param name="newConfiguration">The new configuration</param>
    public ConfigurationChangeAnalysis(CursorPhobiaConfiguration oldConfiguration, CursorPhobiaConfiguration newConfiguration)
    {
        OldConfiguration = oldConfiguration ?? throw new ArgumentNullException(nameof(oldConfiguration));
        NewConfiguration = newConfiguration ?? throw new ArgumentNullException(nameof(newConfiguration));

        AnalyzeChanges();
    }

    /// <summary>
    /// Analyzes the differences between old and new configurations
    /// </summary>
    private void AnalyzeChanges()
    {
        // Hot-swappable settings (can be applied immediately)
        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.ProximityThreshold),
            OldConfiguration.ProximityThreshold, NewConfiguration.ProximityThreshold);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.PushDistance),
            OldConfiguration.PushDistance, NewConfiguration.PushDistance);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.EnableCtrlOverride),
            OldConfiguration.EnableCtrlOverride, NewConfiguration.EnableCtrlOverride);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer),
            OldConfiguration.ScreenEdgeBuffer, NewConfiguration.ScreenEdgeBuffer);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.AnimationDurationMs),
            OldConfiguration.AnimationDurationMs, NewConfiguration.AnimationDurationMs);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.EnableAnimations),
            OldConfiguration.EnableAnimations, NewConfiguration.EnableAnimations);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.AnimationEasing),
            OldConfiguration.AnimationEasing, NewConfiguration.AnimationEasing);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.HoverTimeoutMs),
            OldConfiguration.HoverTimeoutMs, NewConfiguration.HoverTimeoutMs);

        CheckHotSwappableSetting(nameof(CursorPhobiaConfiguration.EnableHoverTimeout),
            OldConfiguration.EnableHoverTimeout, NewConfiguration.EnableHoverTimeout);

        // Settings that require restart (timing and core behavior changes)
        CheckRestartRequiredSetting(nameof(CursorPhobiaConfiguration.UpdateIntervalMs),
            OldConfiguration.UpdateIntervalMs, NewConfiguration.UpdateIntervalMs);

        CheckRestartRequiredSetting(nameof(CursorPhobiaConfiguration.MaxUpdateIntervalMs),
            OldConfiguration.MaxUpdateIntervalMs, NewConfiguration.MaxUpdateIntervalMs);

        CheckRestartRequiredSetting(nameof(CursorPhobiaConfiguration.ApplyToAllWindows),
            OldConfiguration.ApplyToAllWindows, NewConfiguration.ApplyToAllWindows);

        // Multi-monitor configuration requires restart due to complexity
        if (!AreMultiMonitorConfigurationsEqual(OldConfiguration.MultiMonitor, NewConfiguration.MultiMonitor))
        {
            RestartRequiredChanges.Add(nameof(CursorPhobiaConfiguration.MultiMonitor));
        }
    }

    /// <summary>
    /// Checks if a setting has changed and can be hot-swapped
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="settingName">Name of the setting</param>
    /// <param name="oldValue">Old value</param>
    /// <param name="newValue">New value</param>
    private void CheckHotSwappableSetting<T>(string settingName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            HotSwappableChanges.Add(settingName);
        }
    }

    /// <summary>
    /// Checks if a setting has changed and requires restart
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="settingName">Name of the setting</param>
    /// <param name="oldValue">Old value</param>
    /// <param name="newValue">New value</param>
    private void CheckRestartRequiredSetting<T>(string settingName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            RestartRequiredChanges.Add(settingName);
        }
    }

    /// <summary>
    /// Compares two multi-monitor configurations for equality
    /// </summary>
    /// <param name="config1">First configuration</param>
    /// <param name="config2">Second configuration</param>
    /// <returns>True if configurations are equivalent</returns>
    private bool AreMultiMonitorConfigurationsEqual(MultiMonitorConfiguration? config1, MultiMonitorConfiguration? config2)
    {
        if (config1 == null && config2 == null) return true;
        if (config1 == null || config2 == null) return false;

        if (config1.EnableWrapping != config2.EnableWrapping ||
            config1.PreferredWrapBehavior != config2.PreferredWrapBehavior ||
            config1.RespectTaskbarAreas != config2.RespectTaskbarAreas)
        {
            return false;
        }

        // Compare per-monitor settings
        if (config1.PerMonitorSettings.Count != config2.PerMonitorSettings.Count)
            return false;

        foreach (var kvp in config1.PerMonitorSettings)
        {
            if (!config2.PerMonitorSettings.TryGetValue(kvp.Key, out var otherSettings))
                return false;

            if (!ArePerMonitorSettingsEqual(kvp.Value, otherSettings))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two per-monitor settings for equality
    /// </summary>
    /// <param name="settings1">First settings</param>
    /// <param name="settings2">Second settings</param>
    /// <returns>True if settings are equivalent</returns>
    private bool ArePerMonitorSettingsEqual(PerMonitorSettings? settings1, PerMonitorSettings? settings2)
    {
        if (settings1 == null && settings2 == null) return true;
        if (settings1 == null || settings2 == null) return false;

        return settings1.Enabled == settings2.Enabled &&
               settings1.CustomProximityThreshold == settings2.CustomProximityThreshold &&
               settings1.CustomPushDistance == settings2.CustomPushDistance;
    }

    /// <summary>
    /// Gets a summary of the configuration changes
    /// </summary>
    /// <returns>Human-readable summary of changes</returns>
    public string GetSummary()
    {
        if (!HasChanges)
            return "No configuration changes detected";

        var summary = new List<string>();

        if (HotSwappableChanges.Count > 0)
        {
            summary.Add($"Hot-swappable changes: {string.Join(", ", HotSwappableChanges)}");
        }

        if (RestartRequiredChanges.Count > 0)
        {
            summary.Add($"Restart required for: {string.Join(", ", RestartRequiredChanges)}");
        }

        return string.Join("; ", summary);
    }
}