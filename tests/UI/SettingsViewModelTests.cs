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
    public void SettingsViewModel_EnableCtrlOverride_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.EnableCtrlOverride = !config.EnableCtrlOverride;

        // Assert
        Assert.Equal(viewModel.EnableCtrlOverride, config.EnableCtrlOverride);
        Assert.Contains(nameof(SettingsViewModel.EnableCtrlOverride), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
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
    public void SettingsViewModel_EnableAnimations_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.EnableAnimations = !config.EnableAnimations;

        // Assert
        Assert.Equal(viewModel.EnableAnimations, config.EnableAnimations);
        Assert.Contains(nameof(SettingsViewModel.EnableAnimations), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_AnimationDurationMs_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.AnimationDurationMs = 750;

        // Assert
        Assert.Equal(750, viewModel.AnimationDurationMs);
        Assert.Equal(750, config.AnimationDurationMs);
        Assert.Contains(nameof(SettingsViewModel.AnimationDurationMs), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsViewModel_AnimationEasing_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.AnimationEasing = AnimationEasing.EaseIn;

        // Assert
        Assert.Equal(AnimationEasing.EaseIn, viewModel.AnimationEasing);
        Assert.Equal(AnimationEasing.EaseIn, config.AnimationEasing);
        Assert.Contains(nameof(SettingsViewModel.AnimationEasing), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
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
    public void SettingsViewModel_PreferredWrapBehavior_PropertyNotification()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var viewModel = new SettingsViewModel(config);
        var changedProperties = new List<string>();
        
        viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        // Act
        viewModel.PreferredWrapBehavior = WrapPreference.Adjacent;

        // Assert
        Assert.Equal(WrapPreference.Adjacent, viewModel.PreferredWrapBehavior);
        Assert.Equal(WrapPreference.Adjacent, config.MultiMonitor?.PreferredWrapBehavior);
        Assert.Contains(nameof(SettingsViewModel.PreferredWrapBehavior), changedProperties);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
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
        config.ProximityThreshold = -1; // Invalid value
        var viewModel = new SettingsViewModel(config);

        // Act
        var errors = viewModel.ValidateConfiguration();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ProximityThreshold"));
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
        var performanceConfig = CursorPhobiaConfiguration.CreatePerformanceOptimized();
        Assert.Equal(performanceConfig.UpdateIntervalMs, viewModel.UpdateIntervalMs);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
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
        var responsiveConfig = CursorPhobiaConfiguration.CreateResponsivenessOptimized();
        Assert.Equal(responsiveConfig.UpdateIntervalMs, viewModel.UpdateIntervalMs);
        Assert.Contains(nameof(SettingsViewModel.HasUnsavedChanges), changedProperties);
        Assert.True(viewModel.HasUnsavedChanges);
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
        Assert.Contains(nameof(SettingsViewModel.UpdateIntervalMs), propertyChanges);
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
}