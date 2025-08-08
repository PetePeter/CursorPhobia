using CursorPhobia.Core.Models;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for CursorPhobiaConfiguration
/// </summary>
public class CursorPhobiaConfigurationTests
{
    [Fact]
    public void Constructor_DefaultValues_SetsCorrectDefaults()
    {
        // Act
        var config = new CursorPhobiaConfiguration();

        // Assert - Only test user-configurable properties
        Assert.Equal(50, config.ProximityThreshold);
        Assert.Equal(100, config.PushDistance);
        Assert.True(config.EnableCtrlOverride);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(HardcodedDefaults.HoverTimeoutMs, config.HoverTimeoutMs);
        Assert.True(config.EnableHoverTimeout);
        Assert.NotNull(config.MultiMonitor);
        
        // Assert hardcoded values are set (these are no longer user-configurable)
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, config.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, config.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, config.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, config.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, config.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, config.AnimationEasing);
    }

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsNoErrors()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration();

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(-1, "ProximityThreshold must be greater than 0")]
    [InlineData(0, "ProximityThreshold must be greater than 0")]
    [InlineData(600, "ProximityThreshold should not exceed 500 pixels for usability")]
    public void Validate_WithInvalidProximityThreshold_ReturnsError(int threshold, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { ProximityThreshold = threshold };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Theory]
    [InlineData(-1, "PushDistance must be greater than 0")]
    [InlineData(0, "PushDistance must be greater than 0")]
    [InlineData(1100, "PushDistance should not exceed 1000 pixels to prevent windows moving off-screen")]
    public void Validate_WithInvalidPushDistance_ReturnsError(int distance, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { PushDistance = distance };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }





    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration
        {
            ProximityThreshold = -1,
            PushDistance = 0
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.True(errors.Count >= 2);
        Assert.Contains("ProximityThreshold must be greater than 0", errors);
        Assert.Contains("PushDistance must be greater than 0", errors);
    }

    [Fact]
    public void CreateDefault_ReturnsValidConfiguration()
    {
        // Act
        var config = CursorPhobiaConfiguration.CreateDefault();

        // Assert
        Assert.NotNull(config);
        Assert.Empty(config.Validate());
    }

    [Fact]
    public void CreatePerformanceOptimized_ReturnsValidConfiguration()
    {
        // Act
        var config = CursorPhobiaConfiguration.CreatePerformanceOptimized();

        // Assert
        Assert.NotNull(config);
        Assert.Empty(config.Validate());
        // Note: UpdateIntervalMs and MaxUpdateIntervalMs are now set but hardcoded values are used in practice
        Assert.Equal(33, config.UpdateIntervalMs);
        Assert.Equal(100, config.MaxUpdateIntervalMs);
    }

    [Fact]
    public void CreateResponsivenessOptimized_ReturnsValidConfiguration()
    {
        // Act
        var config = CursorPhobiaConfiguration.CreateResponsivenessOptimized();

        // Assert
        Assert.NotNull(config);
        Assert.Empty(config.Validate());
        // Note: UpdateIntervalMs and MaxUpdateIntervalMs are now set but hardcoded values are used in practice
        Assert.Equal(8, config.UpdateIntervalMs);
        Assert.Equal(25, config.MaxUpdateIntervalMs);
    }

    [Fact]
    public void PropertySetters_WithValidValues_UpdateCorrectly()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration();

        // Act - Only test user-configurable properties
        config.ProximityThreshold = 75;
        config.PushDistance = 150;
        config.EnableCtrlOverride = false;
        config.ApplyToAllWindows = true;
        config.HoverTimeoutMs = 3000;
        config.EnableHoverTimeout = false;
        
        // These properties can still be set but use hardcoded values in practice
        config.UpdateIntervalMs = 20;
        config.MaxUpdateIntervalMs = 60;
        config.ScreenEdgeBuffer = 30;
        config.AnimationDurationMs = 300;
        config.EnableAnimations = false;
        config.AnimationEasing = AnimationEasing.EaseIn;

        // Assert - Test user-configurable properties
        Assert.Equal(75, config.ProximityThreshold);
        Assert.Equal(150, config.PushDistance);
        Assert.False(config.EnableCtrlOverride);
        Assert.True(config.ApplyToAllWindows);
        Assert.Equal(3000, config.HoverTimeoutMs);
        Assert.False(config.EnableHoverTimeout);
        
        // Assert - These properties can be set but hardcoded values are used in practice
        Assert.Equal(20, config.UpdateIntervalMs);
        Assert.Equal(60, config.MaxUpdateIntervalMs);
        Assert.Equal(30, config.ScreenEdgeBuffer);
        Assert.Equal(300, config.AnimationDurationMs);
        Assert.False(config.EnableAnimations);
        Assert.Equal(AnimationEasing.EaseIn, config.AnimationEasing);
    }
}

/// <summary>
/// Unit tests for ProximityConfiguration
/// </summary>
public class ProximityConfigurationTests
{
    [Fact]
    public void Constructor_DefaultValues_SetsCorrectDefaults()
    {
        // Act
        var config = new ProximityConfiguration();

        // Assert
        Assert.Equal(ProximityAlgorithm.EuclideanDistance, config.Algorithm);
        Assert.True(config.UseNearestEdge);
        Assert.Equal(1.0, config.HorizontalSensitivityMultiplier);
        Assert.Equal(1.0, config.VerticalSensitivityMultiplier);
    }

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsNoErrors()
    {
        // Arrange
        var config = new ProximityConfiguration();

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(-1.0, "HorizontalSensitivityMultiplier must be greater than 0")]
    [InlineData(0.0, "HorizontalSensitivityMultiplier must be greater than 0")]
    [InlineData(15.0, "HorizontalSensitivityMultiplier should not exceed 10 for usability")]
    public void Validate_WithInvalidHorizontalSensitivity_ReturnsError(double multiplier, string expectedError)
    {
        // Arrange
        var config = new ProximityConfiguration { HorizontalSensitivityMultiplier = multiplier };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Theory]
    [InlineData(-1.0, "VerticalSensitivityMultiplier must be greater than 0")]
    [InlineData(0.0, "VerticalSensitivityMultiplier must be greater than 0")]
    [InlineData(12.0, "VerticalSensitivityMultiplier should not exceed 10 for usability")]
    public void Validate_WithInvalidVerticalSensitivity_ReturnsError(double multiplier, string expectedError)
    {
        // Arrange
        var config = new ProximityConfiguration { VerticalSensitivityMultiplier = multiplier };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = new ProximityConfiguration
        {
            HorizontalSensitivityMultiplier = -1.0,
            VerticalSensitivityMultiplier = 0.0
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains("HorizontalSensitivityMultiplier must be greater than 0", errors);
        Assert.Contains("VerticalSensitivityMultiplier must be greater than 0", errors);
    }

    [Theory]
    [InlineData(ProximityAlgorithm.EuclideanDistance)]
    [InlineData(ProximityAlgorithm.ManhattanDistance)]
    [InlineData(ProximityAlgorithm.NearestEdgeDistance)]
    public void Algorithm_PropertySetter_UpdatesCorrectly(ProximityAlgorithm algorithm)
    {
        // Arrange
        var config = new ProximityConfiguration();

        // Act
        config.Algorithm = algorithm;

        // Assert
        Assert.Equal(algorithm, config.Algorithm);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UseNearestEdge_PropertySetter_UpdatesCorrectly(bool useNearestEdge)
    {
        // Arrange
        var config = new ProximityConfiguration();

        // Act
        config.UseNearestEdge = useNearestEdge;

        // Assert
        Assert.Equal(useNearestEdge, config.UseNearestEdge);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(10.0)]
    public void SensitivityMultipliers_WithValidValues_UpdateCorrectly(double multiplier)
    {
        // Arrange
        var config = new ProximityConfiguration();

        // Act
        config.HorizontalSensitivityMultiplier = multiplier;
        config.VerticalSensitivityMultiplier = multiplier;

        // Assert
        Assert.Equal(multiplier, config.HorizontalSensitivityMultiplier);
        Assert.Equal(multiplier, config.VerticalSensitivityMultiplier);
        Assert.Empty(config.Validate());
    }

    [Fact]
    public void ProximityConfiguration_WithMixedSettings_ValidatesCorrectly()
    {
        // Arrange
        var config = new ProximityConfiguration
        {
            Algorithm = ProximityAlgorithm.ManhattanDistance,
            UseNearestEdge = false,
            HorizontalSensitivityMultiplier = 2.0,
            VerticalSensitivityMultiplier = 0.5
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
        Assert.Equal(ProximityAlgorithm.ManhattanDistance, config.Algorithm);
        Assert.False(config.UseNearestEdge);
        Assert.Equal(2.0, config.HorizontalSensitivityMultiplier);
        Assert.Equal(0.5, config.VerticalSensitivityMultiplier);
    }

    [Fact]
    public void CreateOptimalDefaults_ReturnsConfigurationWithOptimalValues()
    {
        // Act
        var config = CursorPhobiaConfiguration.CreateOptimalDefaults();

        // Assert
        Assert.NotNull(config);
        
        // Verify hardcoded performance settings match constants
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, config.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, config.MaxUpdateIntervalMs);
        
        // Verify hardcoded spatial settings match constants
        Assert.Equal(HardcodedDefaults.ProximityThreshold, config.ProximityThreshold);
        Assert.Equal(HardcodedDefaults.PushDistance, config.PushDistance);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, config.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.CtrlReleaseToleranceDistance, config.CtrlReleaseToleranceDistance);
        Assert.Equal(HardcodedDefaults.AlwaysOnTopRepelBorderDistance, config.AlwaysOnTopRepelBorderDistance);
        
        // Verify hardcoded animation settings match constants
        Assert.Equal(HardcodedDefaults.EnableAnimations, config.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, config.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.AnimationEasing, config.AnimationEasing);
        
        // Verify default feature settings
        Assert.Equal(HardcodedDefaults.EnableCtrlOverride, config.EnableCtrlOverride);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(HardcodedDefaults.HoverTimeoutMs, config.HoverTimeoutMs);
        Assert.True(config.EnableHoverTimeout);
        Assert.NotNull(config.MultiMonitor);
        
        // Verify configuration is valid
        var errors = config.Validate();
        Assert.Empty(errors);
    }
}

/// <summary>
/// Unit tests for HardcodedDefaults constants
/// </summary>
public class HardcodedDefaultsTests
{
    [Fact]
    public void HardcodedDefaults_UpdateIntervalMs_HasOptimalValue()
    {
        // Assert - ~60 FPS for smooth tracking
        Assert.Equal(16, HardcodedDefaults.UpdateIntervalMs);
    }

    [Fact]
    public void HardcodedDefaults_MaxUpdateIntervalMs_HasOptimalValue()
    {
        // Assert - ~30 FPS minimum for responsiveness under load
        Assert.Equal(33, HardcodedDefaults.MaxUpdateIntervalMs);
    }

    [Fact]
    public void HardcodedDefaults_ScreenEdgeBuffer_HasOptimalValue()
    {
        // Assert - Reasonable buffer to prevent windows getting stuck at edges
        Assert.Equal(20, HardcodedDefaults.ScreenEdgeBuffer);
    }

    [Fact]
    public void HardcodedDefaults_ProximityThreshold_HasOptimalValue()
    {
        // Assert - Balanced to avoid accidental triggers while maintaining responsiveness
        Assert.Equal(50, HardcodedDefaults.ProximityThreshold);
    }

    [Fact]
    public void HardcodedDefaults_PushDistance_HasOptimalValue()
    {
        // Assert - Moves windows far enough to be useful but not disruptive
        Assert.Equal(100, HardcodedDefaults.PushDistance);
    }

    [Fact]
    public void HardcodedDefaults_CtrlReleaseToleranceDistance_HasOptimalValue()
    {
        // Assert - Allows fine cursor movements without re-triggering phobia
        Assert.Equal(50, HardcodedDefaults.CtrlReleaseToleranceDistance);
    }

    [Fact]
    public void HardcodedDefaults_AlwaysOnTopRepelBorderDistance_HasOptimalValue()
    {
        // Assert - Provides smooth interaction with always-on-top windows
        Assert.Equal(30, HardcodedDefaults.AlwaysOnTopRepelBorderDistance);
    }

    [Fact]
    public void HardcodedDefaults_EnableAnimations_HasOptimalValue()
    {
        // Assert - Animations improve user experience
        Assert.True(HardcodedDefaults.EnableAnimations);
    }

    [Fact]
    public void HardcodedDefaults_AnimationDurationMs_HasOptimalValue()
    {
        // Assert - Fast enough to be responsive, slow enough to be smooth
        Assert.Equal(200, HardcodedDefaults.AnimationDurationMs);
    }

    [Fact]
    public void HardcodedDefaults_AnimationEasing_HasOptimalValue()
    {
        // Assert - EaseOut provides natural deceleration
        Assert.Equal(AnimationEasing.EaseOut, HardcodedDefaults.AnimationEasing);
    }

    [Fact]
    public void HardcodedDefaults_PerformanceValues_AreConsistent()
    {
        // Assert - UpdateIntervalMs should be less than MaxUpdateIntervalMs
        Assert.True(HardcodedDefaults.UpdateIntervalMs < HardcodedDefaults.MaxUpdateIntervalMs);
        
        // Assert - Both should be positive
        Assert.True(HardcodedDefaults.UpdateIntervalMs > 0);
        Assert.True(HardcodedDefaults.MaxUpdateIntervalMs > 0);
    }

    [Fact]
    public void HardcodedDefaults_SpatialValues_AreReasonable()
    {
        // Assert - All spatial values should be positive
        Assert.True(HardcodedDefaults.ProximityThreshold > 0);
        Assert.True(HardcodedDefaults.PushDistance > 0);
        Assert.True(HardcodedDefaults.ScreenEdgeBuffer > 0);
        Assert.True(HardcodedDefaults.CtrlReleaseToleranceDistance > 0);
        Assert.True(HardcodedDefaults.AlwaysOnTopRepelBorderDistance > 0);
        
        // Assert - PushDistance should be larger than ProximityThreshold for effectiveness
        Assert.True(HardcodedDefaults.PushDistance > HardcodedDefaults.ProximityThreshold);
        
        // Assert - Animation duration should be reasonable (not too fast, not too slow)
        Assert.True(HardcodedDefaults.AnimationDurationMs >= 100);
        Assert.True(HardcodedDefaults.AnimationDurationMs <= 1000);
    }
}

/// <summary>
/// Unit tests for ProximityAlgorithm enum
/// </summary>
public class ProximityAlgorithmTests
{
    [Fact]
    public void ProximityAlgorithm_HasExpectedValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(ProximityAlgorithm), ProximityAlgorithm.EuclideanDistance));
        Assert.True(Enum.IsDefined(typeof(ProximityAlgorithm), ProximityAlgorithm.ManhattanDistance));
        Assert.True(Enum.IsDefined(typeof(ProximityAlgorithm), ProximityAlgorithm.NearestEdgeDistance));
    }

    [Fact]
    public void ProximityAlgorithm_EnumValues_AreDistinct()
    {
        // Arrange
        var algorithms = Enum.GetValues<ProximityAlgorithm>();

        // Act
        var distinctCount = algorithms.Distinct().Count();

        // Assert
        Assert.Equal(algorithms.Length, distinctCount);
    }

    [Theory]
    [InlineData(ProximityAlgorithm.EuclideanDistance, "EuclideanDistance")]
    [InlineData(ProximityAlgorithm.ManhattanDistance, "ManhattanDistance")]
    [InlineData(ProximityAlgorithm.NearestEdgeDistance, "NearestEdgeDistance")]
    public void ProximityAlgorithm_ToString_ReturnsExpectedName(ProximityAlgorithm algorithm, string expectedName)
    {
        // Act
        var name = algorithm.ToString();

        // Assert
        Assert.Equal(expectedName, name);
    }
}

/// <summary>
/// Unit tests for AnimationEasing enum
/// </summary>
public class AnimationEasingTests
{
    [Fact]
    public void AnimationEasing_HasExpectedValues()
    {
        // Assert
        Assert.True(Enum.IsDefined(typeof(AnimationEasing), AnimationEasing.Linear));
        Assert.True(Enum.IsDefined(typeof(AnimationEasing), AnimationEasing.EaseIn));
        Assert.True(Enum.IsDefined(typeof(AnimationEasing), AnimationEasing.EaseOut));
        Assert.True(Enum.IsDefined(typeof(AnimationEasing), AnimationEasing.EaseInOut));
    }

    [Fact]
    public void AnimationEasing_EnumValues_AreDistinct()
    {
        // Arrange
        var easingTypes = Enum.GetValues<AnimationEasing>();

        // Act
        var distinctCount = easingTypes.Distinct().Count();

        // Assert
        Assert.Equal(easingTypes.Length, distinctCount);
    }

    [Theory]
    [InlineData(AnimationEasing.Linear, "Linear")]
    [InlineData(AnimationEasing.EaseIn, "EaseIn")]
    [InlineData(AnimationEasing.EaseOut, "EaseOut")]
    [InlineData(AnimationEasing.EaseInOut, "EaseInOut")]
    public void AnimationEasing_ToString_ReturnsExpectedName(AnimationEasing easing, string expectedName)
    {
        // Act
        var name = easing.ToString();

        // Assert
        Assert.Equal(expectedName, name);
    }

    [Fact]
    public void AnimationEasing_Default_IsEaseOut()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration();

        // Act & Assert
        Assert.Equal(AnimationEasing.EaseOut, config.AnimationEasing);
    }
}