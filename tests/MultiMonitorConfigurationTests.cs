using CursorPhobia.Core.Models;
using Xunit;

namespace CursorPhobia.Tests;

public class MultiMonitorConfigurationTests
{
    [Fact]
    public void DefaultConfiguration_ShouldHaveValidSettings()
    {
        // Arrange & Act
        var config = new MultiMonitorConfiguration();

        // Assert
        Assert.True(config.EnableWrapping);
        Assert.Equal(WrapPreference.Smart, config.PreferredWrapBehavior);
        Assert.True(config.RespectTaskbarAreas);
        Assert.NotNull(config.PerMonitorSettings);
        Assert.Empty(config.PerMonitorSettings);
    }

    [Fact]
    public void Validate_DefaultConfiguration_ShouldReturnNoErrors()
    {
        // Arrange
        var config = new MultiMonitorConfiguration();

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyMonitorKey_ShouldReturnError()
    {
        // Arrange
        var config = new MultiMonitorConfiguration();
        config.PerMonitorSettings[""] = new PerMonitorSettings();

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("empty monitor identifier", errors[0]);
    }

    [Fact]
    public void Validate_ValidPerMonitorSettings_ShouldReturnNoErrors()
    {
        // Arrange
        var config = new MultiMonitorConfiguration();
        config.PerMonitorSettings["Monitor1"] = new PerMonitorSettings
        {
            Enabled = true,
            CustomProximityThreshold = 75,
            CustomPushDistance = 150
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidPerMonitorSettings_ShouldReturnErrors()
    {
        // Arrange
        var config = new MultiMonitorConfiguration();
        config.PerMonitorSettings["Monitor1"] = new PerMonitorSettings
        {
            CustomProximityThreshold = -10, // Invalid
            CustomPushDistance = 2000 // Invalid
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("CustomProximityThreshold must be greater than 0"));
        Assert.Contains(errors, e => e.Contains("CustomPushDistance should not exceed 1000 pixels"));
    }
}

public class PerMonitorSettingsTests
{
    [Fact]
    public void DefaultSettings_ShouldHaveValidValues()
    {
        // Arrange & Act
        var settings = new PerMonitorSettings();

        // Assert
        Assert.True(settings.Enabled);
        Assert.Null(settings.CustomProximityThreshold);
        Assert.Null(settings.CustomPushDistance);
    }

    [Fact]
    public void Validate_DefaultSettings_ShouldReturnNoErrors()
    {
        // Arrange
        var settings = new PerMonitorSettings();

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidCustomThreshold_ShouldReturnNoErrors()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomProximityThreshold = 100
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidCustomThreshold_Zero_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomProximityThreshold = 0
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomProximityThreshold must be greater than 0", errors[0]);
    }

    [Fact]
    public void Validate_InvalidCustomThreshold_Negative_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomProximityThreshold = -50
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomProximityThreshold must be greater than 0", errors[0]);
    }

    [Fact]
    public void Validate_InvalidCustomThreshold_TooHigh_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomProximityThreshold = 1000
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomProximityThreshold should not exceed 500 pixels", errors[0]);
    }

    [Fact]
    public void Validate_ValidCustomPushDistance_ShouldReturnNoErrors()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomPushDistance = 200
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidCustomPushDistance_Zero_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomPushDistance = 0
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomPushDistance must be greater than 0", errors[0]);
    }

    [Fact]
    public void Validate_InvalidCustomPushDistance_Negative_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomPushDistance = -100
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomPushDistance must be greater than 0", errors[0]);
    }

    [Fact]
    public void Validate_InvalidCustomPushDistance_TooHigh_ShouldReturnError()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomPushDistance = 1500
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(1, errors.Count);
        Assert.Contains("CustomPushDistance should not exceed 1000 pixels", errors[0]);
    }

    [Fact]
    public void Validate_MultipleInvalidSettings_ShouldReturnMultipleErrors()
    {
        // Arrange
        var settings = new PerMonitorSettings
        {
            CustomProximityThreshold = -10,
            CustomPushDistance = 2000
        };

        // Act
        var errors = settings.Validate();

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("CustomProximityThreshold"));
        Assert.Contains(errors, e => e.Contains("CustomPushDistance"));
    }
}