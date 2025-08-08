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

        // Assert
        Assert.Equal(50, config.ProximityThreshold);
        Assert.Equal(100, config.PushDistance);
        Assert.Equal(16, config.UpdateIntervalMs);
        Assert.Equal(33, config.MaxUpdateIntervalMs);
        Assert.True(config.EnableCtrlOverride);
        Assert.Equal(20, config.ScreenEdgeBuffer);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(200, config.AnimationDurationMs);
        Assert.True(config.EnableAnimations);
        Assert.Equal(AnimationEasing.EaseOut, config.AnimationEasing);
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

    [Theory]
    [InlineData(0, "UpdateIntervalMs must be at least 1ms")]
    [InlineData(100, "UpdateIntervalMs cannot be greater than MaxUpdateIntervalMs")]
    public void Validate_WithInvalidUpdateInterval_ReturnsError(int interval, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration
        {
            UpdateIntervalMs = interval,
            MaxUpdateIntervalMs = 33
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Theory]
    [InlineData(5, "MaxUpdateIntervalMs must be at least 10ms to prevent excessive CPU usage")]
    [InlineData(1500, "MaxUpdateIntervalMs should not exceed 1000ms for responsiveness")]
    public void Validate_WithInvalidMaxUpdateInterval_ReturnsError(int maxInterval, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { MaxUpdateIntervalMs = maxInterval };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Theory]
    [InlineData(-1, "ScreenEdgeBuffer cannot be negative")]
    [InlineData(150, "ScreenEdgeBuffer should not exceed 100 pixels for usability")]
    public void Validate_WithInvalidScreenEdgeBuffer_ReturnsError(int buffer, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { ScreenEdgeBuffer = buffer };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains(expectedError, errors);
    }

    [Theory]
    [InlineData(-1, "AnimationDurationMs cannot be negative")]
    [InlineData(2500, "AnimationDurationMs should not exceed 2000ms for usability")]
    public void Validate_WithInvalidAnimationDuration_ReturnsError(int duration, string expectedError)
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { AnimationDurationMs = duration };

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
            PushDistance = 0,
            UpdateIntervalMs = 0
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.True(errors.Count >= 3);
        Assert.Contains("ProximityThreshold must be greater than 0", errors);
        Assert.Contains("PushDistance must be greater than 0", errors);
        Assert.Contains("UpdateIntervalMs must be at least 1ms", errors);
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
        Assert.Equal(8, config.UpdateIntervalMs);
        Assert.Equal(25, config.MaxUpdateIntervalMs);
    }

    [Fact]
    public void PropertySetters_WithValidValues_UpdateCorrectly()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration();

        // Act
        config.ProximityThreshold = 75;
        config.PushDistance = 150;
        config.UpdateIntervalMs = 20;
        config.MaxUpdateIntervalMs = 60;
        config.EnableCtrlOverride = false;
        config.ScreenEdgeBuffer = 30;
        config.ApplyToAllWindows = true;
        config.AnimationDurationMs = 300;
        config.EnableAnimations = false;
        config.AnimationEasing = AnimationEasing.EaseIn;

        // Assert
        Assert.Equal(75, config.ProximityThreshold);
        Assert.Equal(150, config.PushDistance);
        Assert.Equal(20, config.UpdateIntervalMs);
        Assert.Equal(60, config.MaxUpdateIntervalMs);
        Assert.False(config.EnableCtrlOverride);
        Assert.Equal(30, config.ScreenEdgeBuffer);
        Assert.True(config.ApplyToAllWindows);
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
        
        // Verify performance settings
        Assert.Equal(16, config.UpdateIntervalMs); // ~60 FPS
        Assert.Equal(33, config.MaxUpdateIntervalMs); // ~30 FPS minimum
        
        // Verify spatial settings
        Assert.Equal(50, config.ProximityThreshold);
        Assert.Equal(100, config.PushDistance);
        Assert.Equal(20, config.ScreenEdgeBuffer);
        Assert.Equal(50, config.CtrlReleaseToleranceDistance);
        Assert.Equal(30, config.AlwaysOnTopRepelBorderDistance);
        
        // Verify animation settings
        Assert.True(config.EnableAnimations);
        Assert.Equal(200, config.AnimationDurationMs);
        Assert.Equal(AnimationEasing.EaseOut, config.AnimationEasing);
        
        // Verify default feature settings
        Assert.True(config.EnableCtrlOverride);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(5000, config.HoverTimeoutMs);
        Assert.True(config.EnableHoverTimeout);
        Assert.NotNull(config.MultiMonitor);
        
        // Verify configuration is valid
        var errors = config.Validate();
        Assert.Empty(errors);
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