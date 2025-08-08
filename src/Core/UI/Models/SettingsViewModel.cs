using System.ComponentModel;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.UI.Models;

/// <summary>
/// View model for the settings dialog with data binding and change notification
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private CursorPhobiaConfiguration _config;
    private bool _hasUnsavedChanges;

    public SettingsViewModel(CursorPhobiaConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// The underlying configuration object
    /// </summary>
    public CursorPhobiaConfiguration Configuration
    {
        get => _config;
        set
        {
            if (_config != value)
            {
                _config = value;
                OnPropertyChanged();
                RefreshAllProperties();
            }
        }
    }

    /// <summary>
    /// Whether there are unsaved changes
    /// </summary>
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (_hasUnsavedChanges != value)
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }
    }

    // General Settings Properties
    public int ProximityThreshold
    {
        get => _config.ProximityThreshold;
        set => SetConfigProperty(_config.ProximityThreshold, value, v => _config.ProximityThreshold = v);
    }

    public int PushDistance
    {
        get => _config.PushDistance;
        set => SetConfigProperty(_config.PushDistance, value, v => _config.PushDistance = v);
    }

    /// <summary>
    /// Current CTRL override setting (hardcoded value, display-only)
    /// </summary>
    public bool CurrentEnableCtrlOverride => HardcodedDefaults.EnableCtrlOverride;

    public bool ApplyToAllWindows
    {
        get => _config.ApplyToAllWindows;
        set => SetConfigProperty(_config.ApplyToAllWindows, value, v => _config.ApplyToAllWindows = v);
    }

    // Display-only properties for showing hardcoded values in UI tooltips
    /// <summary>
    /// Current update interval setting (hardcoded value)
    /// </summary>
    public int CurrentUpdateInterval => HardcodedDefaults.UpdateIntervalMs;

    /// <summary>
    /// Current maximum update interval setting (hardcoded value)
    /// </summary>
    public int CurrentMaxUpdateInterval => HardcodedDefaults.MaxUpdateIntervalMs;

    /// <summary>
    /// Current animation settings information (hardcoded values)
    /// </summary>
    public string CurrentAnimationSettings => 
        $"Enabled: {HardcodedDefaults.EnableAnimations}, Duration: {HardcodedDefaults.AnimationDurationMs}ms, Easing: {HardcodedDefaults.AnimationEasing}";

    /// <summary>
    /// Current performance settings information (hardcoded values)
    /// </summary>
    public string CurrentPerformanceSettings =>
        $"Update: {HardcodedDefaults.UpdateIntervalMs}ms (~{1000 / HardcodedDefaults.UpdateIntervalMs}fps), Max: {HardcodedDefaults.MaxUpdateIntervalMs}ms (~{1000 / HardcodedDefaults.MaxUpdateIntervalMs}fps), Buffer: {HardcodedDefaults.ScreenEdgeBuffer}px";

    /// <summary>
    /// Current hover timeout setting (hardcoded value, display-only)
    /// </summary>
    public int CurrentHoverTimeoutMs => HardcodedDefaults.HoverTimeoutMs;

    public bool EnableHoverTimeout
    {
        get => _config.EnableHoverTimeout;
        set => SetConfigProperty(_config.EnableHoverTimeout, value, v => _config.EnableHoverTimeout = v);
    }

    // Multi-Monitor Settings Properties
    public bool EnableWrapping
    {
        get => _config.MultiMonitor?.EnableWrapping ?? true;
        set
        {
            EnsureMultiMonitorConfig();
            SetConfigProperty(_config.MultiMonitor!.EnableWrapping, value, v => _config.MultiMonitor!.EnableWrapping = v);
        }
    }

    /// <summary>
    /// Current wrap behavior preference (hardcoded value, display-only)
    /// Note: This property now returns the hardcoded optimal value
    /// </summary>
    public WrapPreference PreferredWrapBehavior => HardcodedDefaults.PreferredWrapBehavior;

    public bool RespectTaskbarAreas
    {
        get => _config.MultiMonitor?.RespectTaskbarAreas ?? true;
        set
        {
            EnsureMultiMonitorConfig();
            SetConfigProperty(_config.MultiMonitor!.RespectTaskbarAreas, value, v => _config.MultiMonitor!.RespectTaskbarAreas = v);
        }
    }

    /// <summary>
    /// Current screen edge buffer setting (hardcoded value)
    /// </summary>
    public int CurrentScreenEdgeBuffer => HardcodedDefaults.ScreenEdgeBuffer;

    /// <summary>
    /// Current CTRL release tolerance distance (hardcoded value)
    /// </summary>
    public int CurrentCtrlReleaseToleranceDistance => HardcodedDefaults.CtrlReleaseToleranceDistance;

    /// <summary>
    /// Current always-on-top repel border distance (hardcoded value)
    /// </summary>
    public int CurrentAlwaysOnTopRepelBorderDistance => HardcodedDefaults.AlwaysOnTopRepelBorderDistance;

    /// <summary>
    /// Current cross-monitor movement setting (hardcoded value, display-only)
    /// </summary>
    public bool CurrentEnableCrossMonitorMovement => HardcodedDefaults.EnableCrossMonitorMovement;

    /// <summary>
    /// Current wrap behavior preference (hardcoded value, display-only)
    /// </summary>
    public WrapPreference CurrentPreferredWrapBehavior => HardcodedDefaults.PreferredWrapBehavior;

    // Auto-start functionality (not part of configuration, handled separately)
    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (_startWithWindows != value)
            {
                _startWithWindows = value;
                OnPropertyChanged();
                HasUnsavedChanges = true;
            }
        }
    }

    /// <summary>
    /// Validates the current configuration
    /// </summary>
    /// <returns>List of validation errors</returns>
    public List<string> ValidateConfiguration()
    {
        return _config.Validate();
    }

    /// <summary>
    /// Resets all changes to the original configuration
    /// </summary>
    public void ResetChanges()
    {
        HasUnsavedChanges = false;
        RefreshAllProperties();
    }

    /// <summary>
    /// Applies a configuration preset
    /// </summary>
    public void ApplyPreset(string presetName)
    {
        var newConfig = presetName.ToLower() switch
        {
            "default" => CursorPhobiaConfiguration.CreateDefault(),
            "performance" => CursorPhobiaConfiguration.CreatePerformanceOptimized(),
            "responsive" => CursorPhobiaConfiguration.CreateResponsivenessOptimized(),
            _ => throw new ArgumentException($"Unknown preset: {presetName}")
        };

        Configuration = newConfig;
        HasUnsavedChanges = true;
    }

    private void EnsureMultiMonitorConfig()
    {
        _config.MultiMonitor ??= new MultiMonitorConfiguration();
    }

    private void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(ProximityThreshold));
        OnPropertyChanged(nameof(PushDistance));
        OnPropertyChanged(nameof(ApplyToAllWindows));
        OnPropertyChanged(nameof(EnableHoverTimeout));
        OnPropertyChanged(nameof(EnableWrapping));
        OnPropertyChanged(nameof(PreferredWrapBehavior));
        OnPropertyChanged(nameof(RespectTaskbarAreas));
        
        // Refresh display-only properties for hardcoded values
        OnPropertyChanged(nameof(CurrentUpdateInterval));
        OnPropertyChanged(nameof(CurrentMaxUpdateInterval));
        OnPropertyChanged(nameof(CurrentAnimationSettings));
        OnPropertyChanged(nameof(CurrentPerformanceSettings));
        OnPropertyChanged(nameof(CurrentScreenEdgeBuffer));
        OnPropertyChanged(nameof(CurrentCtrlReleaseToleranceDistance));
        OnPropertyChanged(nameof(CurrentAlwaysOnTopRepelBorderDistance));
        OnPropertyChanged(nameof(CurrentEnableCtrlOverride));
        OnPropertyChanged(nameof(CurrentHoverTimeoutMs));
        OnPropertyChanged(nameof(CurrentEnableCrossMonitorMovement));
        OnPropertyChanged(nameof(CurrentPreferredWrapBehavior));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Helper method to set configuration properties with change notification and dirty tracking
    /// </summary>
    private void SetConfigProperty<T>(T currentValue, T newValue, Action<T> setter, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            setter(newValue);
            OnPropertyChanged(propertyName);
            HasUnsavedChanges = true;
        }
    }

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}