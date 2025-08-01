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

    public bool EnableCtrlOverride
    {
        get => _config.EnableCtrlOverride;
        set => SetConfigProperty(_config.EnableCtrlOverride, value, v => _config.EnableCtrlOverride = v);
    }

    public bool ApplyToAllWindows
    {
        get => _config.ApplyToAllWindows;
        set => SetConfigProperty(_config.ApplyToAllWindows, value, v => _config.ApplyToAllWindows = v);
    }

    // Animation Settings Properties
    public bool EnableAnimations
    {
        get => _config.EnableAnimations;
        set => SetConfigProperty(_config.EnableAnimations, value, v => _config.EnableAnimations = v);
    }

    public int AnimationDurationMs
    {
        get => _config.AnimationDurationMs;
        set => SetConfigProperty(_config.AnimationDurationMs, value, v => _config.AnimationDurationMs = v);
    }

    public AnimationEasing AnimationEasing
    {
        get => _config.AnimationEasing;
        set => SetConfigProperty(_config.AnimationEasing, value, v => _config.AnimationEasing = v);
    }

    // Timing Settings Properties
    public int UpdateIntervalMs
    {
        get => _config.UpdateIntervalMs;
        set => SetConfigProperty(_config.UpdateIntervalMs, value, v => _config.UpdateIntervalMs = v);
    }

    public int MaxUpdateIntervalMs
    {
        get => _config.MaxUpdateIntervalMs;
        set => SetConfigProperty(_config.MaxUpdateIntervalMs, value, v => _config.MaxUpdateIntervalMs = v);
    }

    public int HoverTimeoutMs
    {
        get => _config.HoverTimeoutMs;
        set => SetConfigProperty(_config.HoverTimeoutMs, value, v => _config.HoverTimeoutMs = v);
    }

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

    public WrapPreference PreferredWrapBehavior
    {
        get => _config.MultiMonitor?.PreferredWrapBehavior ?? WrapPreference.Smart;
        set
        {
            EnsureMultiMonitorConfig();
            SetConfigProperty(_config.MultiMonitor!.PreferredWrapBehavior, value, v => _config.MultiMonitor!.PreferredWrapBehavior = v);
        }
    }

    public bool RespectTaskbarAreas
    {
        get => _config.MultiMonitor?.RespectTaskbarAreas ?? true;
        set
        {
            EnsureMultiMonitorConfig();
            SetConfigProperty(_config.MultiMonitor!.RespectTaskbarAreas, value, v => _config.MultiMonitor!.RespectTaskbarAreas = v);
        }
    }

    // Advanced Settings Properties
    public int ScreenEdgeBuffer
    {
        get => _config.ScreenEdgeBuffer;
        set => SetConfigProperty(_config.ScreenEdgeBuffer, value, v => _config.ScreenEdgeBuffer = v);
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
        OnPropertyChanged(nameof(EnableCtrlOverride));
        OnPropertyChanged(nameof(ApplyToAllWindows));
        OnPropertyChanged(nameof(EnableAnimations));
        OnPropertyChanged(nameof(AnimationDurationMs));
        OnPropertyChanged(nameof(AnimationEasing));
        OnPropertyChanged(nameof(UpdateIntervalMs));
        OnPropertyChanged(nameof(MaxUpdateIntervalMs));
        OnPropertyChanged(nameof(HoverTimeoutMs));
        OnPropertyChanged(nameof(EnableHoverTimeout));
        OnPropertyChanged(nameof(EnableWrapping));
        OnPropertyChanged(nameof(PreferredWrapBehavior));
        OnPropertyChanged(nameof(RespectTaskbarAreas));
        OnPropertyChanged(nameof(ScreenEdgeBuffer));
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