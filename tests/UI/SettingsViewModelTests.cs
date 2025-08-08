using Xunit;
using CursorPhobia.Core.UI.Models;
using CursorPhobia.Core.Models;
using System.ComponentModel;

namespace CursorPhobia.Tests.UI;

/// <summary>
/// Tests for SettingsViewModel Phase B functionality
/// </summary>
public class SettingsViewModelTests
{
    [Fact]
    public void SettingsViewModel_Constructor_CreatesInstance()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();

        // Act
        var viewModel = new SettingsViewModel(config);

        // Assert
        Assert.NotNull(viewModel);
        Assert.Same(config, viewModel.Configuration);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_Constructor_WithNullConfig_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!));
    }

    [Fact]
    public void SettingsViewModel_ProximityThreshold_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.ProximityThreshold = 150;

        // Assert
        Assert.Equal(150, viewModel.ProximityThreshold);
        Assert.Equal(150, config.ProximityThreshold);
        Assert.Contains(nameof(SettingsViewModel.ProximityThreshold), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_PushDistance_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.PushDistance = 250;

        // Assert
        Assert.Equal(250, viewModel.PushDistance);
        Assert.Equal(250, config.PushDistance);
        Assert.Contains(nameof(SettingsViewModel.PushDistance), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_CurrentEnableCtrlOverride_ReturnsHardcodedValue()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act & Assert
        Assert.Equal(HardcodedDefaults.EnableCtrlOverride, viewModel.CurrentEnableCtrlOverride);
    }

    [Fact]
    public void SettingsViewModel_ApplyToAllWindows_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.ApplyToAllWindows = !config.ApplyToAllWindows;

        // Assert
        Assert.Equal(viewModel.ApplyToAllWindows, config.ApplyToAllWindows);
        Assert.Contains(nameof(SettingsViewModel.ApplyToAllWindows), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_CurrentAnimationSettings_ReturnsHardcodedValues()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act
        var animationSettings = viewModel.CurrentAnimationSettings;

        // Assert
        Assert.Contains(HardcodedDefaults.EnableAnimations.ToString(), animationSettings);
        Assert.Contains(HardcodedDefaults.AnimationDurationMs.ToString(), animationSettings);
        Assert.Contains(HardcodedDefaults.AnimationEasing.ToString(), animationSettings);
    }

    [Fact]
    public void SettingsViewModel_CurrentUpdateInterval_ReturnsHardcodedValue()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act & Assert
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, viewModel.CurrentUpdateInterval);
    }

    [Fact]
    public void SettingsViewModel_CurrentPerformanceSettings_ReturnsHardcodedValues()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act
        var performanceSettings = viewModel.CurrentPerformanceSettings;

        // Assert
        Assert.Contains(HardcodedDefaults.UpdateIntervalMs.ToString(), performanceSettings);
        Assert.Contains(HardcodedDefaults.MaxUpdateIntervalMs.ToString(), performanceSettings);
        Assert.Contains(HardcodedDefaults.ScreenEdgeBuffer.ToString(), performanceSettings);
    }

    [Fact]
    public void SettingsViewModel_EnableWrapping_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.EnableWrapping = !viewModel.EnableWrapping;

        // Assert
        Assert.Equal(viewModel.EnableWrapping, config.MultiMonitor?.EnableWrapping ?? true);
        Assert.Contains(nameof(SettingsViewModel.EnableWrapping), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_PreferredWrapBehavior_ReturnsHardcodedValue()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act & Assert
        Assert.Equal(HardcodedDefaults.PreferredWrapBehavior, viewModel.PreferredWrapBehavior);
    }

    [Fact]
    public void SettingsViewModel_RespectTaskbarAreas_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.RespectTaskbarAreas = !viewModel.RespectTaskbarAreas;

        // Assert
        Assert.Equal(viewModel.RespectTaskbarAreas, config.MultiMonitor?.RespectTaskbarAreas ?? true);
        Assert.Contains(nameof(SettingsViewModel.RespectTaskbarAreas), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_ValidateConfiguration_ReturnsConfigurationErrors()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = -1; // Invalid value - this is still user-configurable
        var viewModel = new SettingsViewModel(config);

        // Act
        var errors = viewModel.ValidateConfiguration();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ProximityThreshold"));
        // Note: HoverTimeoutMs is now hardcoded and no longer validated
    }

    [Fact]
    public void SettingsViewModel_ApplyPreset_Default_UpdatesConfiguration()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = 999; // Change from default
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.ApplyPreset("default");

        // Assert
        var defaultConfig = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(defaultConfig.ProximityThreshold, viewModel.ProximityThreshold);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_ApplyPreset_Performance_UpdatesConfiguration()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.ApplyPreset("performance");

        // Assert
        // Note: UpdateIntervalMs is now hardcoded, so we just verify the preset applied successfully
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
        // Verify that hardcoded values are still accessible
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, viewModel.CurrentUpdateInterval);
    }

    [Fact]
    public void SettingsViewModel_ApplyPreset_Responsive_UpdatesConfiguration()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.ApplyPreset("responsive");

        // Assert
        // Note: UpdateIntervalMs is now hardcoded, so we just verify the preset applied successfully
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
        // Verify that hardcoded values are still accessible
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, viewModel.CurrentUpdateInterval);
    }

    [Fact]
    public void SettingsViewModel_ApplyPreset_UnknownPreset_ThrowsException()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => viewModel.ApplyPreset("unknown"));
    }

    [Fact]
    public void SettingsViewModel_Configuration_SetNewConfig_TriggersPropertyChanges()
    {
        // Arrange
        var config1 = CursorPhobiaConfiguration.CreateDefault();
        var config2 = CursorPhobiaConfiguration.CreatePerformanceOptimized();
        var viewModel = new SettingsViewModel(config1);

        var propertyChanges = new List<string>();
        viewModel.PropertyChanged += (s, e) => propertyChanges.Add(e.PropertyName!);

        // Act
        viewModel.Configuration = config2;

        // Assert
        Assert.Same(config2, viewModel.Configuration);
        Assert.Contains(nameof(SettingsViewModel.Configuration), propertyChanges);
        Assert.Contains(nameof(SettingsViewModel.ProximityThreshold), propertyChanges);
        // Note: UpdateIntervalMs is now hardcoded, check for CurrentUpdateInterval refresh instead
        Assert.Contains(nameof(SettingsViewModel.CurrentUpdateInterval), propertyChanges);
    }

    [Fact]
    public void SettingsViewModel_ResetChanges_ClearsUnsavedFlag()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        viewModel.ProximityThreshold = 999; // Make a change
        Assert.True(viewModel.HasUnsavedChanges);

        // Act
        viewModel.ResetChanges();

        // Assert
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_HardcodedValues_AreExposedCorrectly()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);

        // Act & Assert - Hardcoded values should match constants
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, viewModel.CurrentUpdateInterval);
        
        // Performance settings should show hardcoded values
        var perfSettings = viewModel.CurrentPerformanceSettings;
        Assert.Contains(HardcodedDefaults.UpdateIntervalMs.ToString(), perfSettings);
        Assert.Contains(HardcodedDefaults.MaxUpdateIntervalMs.ToString(), perfSettings);
        Assert.Contains(HardcodedDefaults.ScreenEdgeBuffer.ToString(), perfSettings);
        
        // Animation settings should show hardcoded values
        var animSettings = viewModel.CurrentAnimationSettings;
        Assert.Contains(HardcodedDefaults.EnableAnimations.ToString(), animSettings);
        Assert.Contains(HardcodedDefaults.AnimationDurationMs.ToString(), animSettings);
        Assert.Contains(HardcodedDefaults.AnimationEasing.ToString(), animSettings);
    }

    [Fact]
    public void SettingsViewModel_ConfigurationChange_DoesNotAffectHardcodedValues()
    {
        // Arrange
        var config1 = CursorPhobiaConfiguration.CreateDefault();
        var config2 = CursorPhobiaConfiguration.CreatePerformanceOptimized();
        var viewModel = new SettingsViewModel(config1);

        var originalUpdateInterval = viewModel.CurrentUpdateInterval;
        var originalPerfSettings = viewModel.CurrentPerformanceSettings;
        var originalAnimSettings = viewModel.CurrentAnimationSettings;

        // Act
        viewModel.Configuration = config2;

        // Assert - Hardcoded values should remain the same regardless of configuration
        Assert.Equal(originalUpdateInterval, viewModel.CurrentUpdateInterval);
        Assert.Equal(originalPerfSettings, viewModel.CurrentPerformanceSettings);
        Assert.Equal(originalAnimSettings, viewModel.CurrentAnimationSettings);
        
        // Assert - Should still match hardcoded defaults
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, viewModel.CurrentUpdateInterval);
    }
}