using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Moq;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for configuration hot-swapping functionality in CursorPhobiaEngine
/// </summary>
public class ConfigurationHotSwapTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<ICursorTracker> _mockCursorTracker;
    private readonly Mock<IProximityDetector> _mockProximityDetector;
    private readonly Mock<IWindowDetectionService> _mockWindowDetectionService;
    private readonly Mock<IWindowPusher> _mockWindowPusher;
    private readonly Mock<ISafetyManager> _mockSafetyManager;
    private readonly Mock<IMonitorManager> _mockMonitorManager;
    private readonly CursorPhobiaEngine _engine;
    private readonly CursorPhobiaConfiguration _baseConfiguration;

    public ConfigurationHotSwapTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockCursorTracker = new Mock<ICursorTracker>();
        _mockProximityDetector = new Mock<IProximityDetector>();
        _mockWindowDetectionService = new Mock<IWindowDetectionService>();
        _mockWindowPusher = new Mock<IWindowPusher>();
        _mockSafetyManager = new Mock<ISafetyManager>();
        _mockMonitorManager = new Mock<IMonitorManager>();

        _baseConfiguration = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            UpdateIntervalMs = 16,
            MaxUpdateIntervalMs = 33,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs,
            EnableHoverTimeout = true
        };

        _engine = new CursorPhobiaEngine(
            _mockLogger.Object,
            _mockCursorTracker.Object,
            _mockProximityDetector.Object,
            _mockWindowDetectionService.Object,
            _mockWindowPusher.Object,
            _mockSafetyManager.Object,
            _mockMonitorManager.Object,
            _baseConfiguration);
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithNoChanges_ReturnsSuccessWithNoChanges()
    {
        // Arrange
        var identicalConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            UpdateIntervalMs = 16,
            MaxUpdateIntervalMs = 33,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs,
            EnableHoverTimeout = true
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(identicalConfig);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.ChangeAnalysis.HasChanges);
        Assert.Empty(result.AppliedChanges);
        Assert.Empty(result.QueuedForRestart);
        Assert.False(result.RequiresRestart);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithHotSwappableChanges_AppliesChangesImmediately()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Changed (hot-swappable)
            PushDistance = 150, // Changed (hot-swappable)
            UpdateIntervalMs = 16, // Same (hardcoded)
            MaxUpdateIntervalMs = 33, // Same (hardcoded)
            EnableCtrlOverride = false, // Same as hardcoded value (no longer user-configurable)
            ScreenEdgeBuffer = 30, // Same as hardcoded value (no longer user-configurable)
            ApplyToAllWindows = false, // Same
            AnimationDurationMs = 300, // Same as hardcoded value (no longer user-configurable)
            EnableAnimations = false, // Same as hardcoded value (no longer user-configurable)
            AnimationEasing = AnimationEasing.Linear, // Same as hardcoded value (no longer user-configurable)
            HoverTimeoutMs = 3000, // Same as hardcoded value (no longer user-configurable)
            EnableHoverTimeout = false // Changed (hot-swappable)
        };

        bool configUpdatedEventFired = false;
        ConfigurationUpdatedEventArgs? eventArgs = null;
        _engine.ConfigurationUpdated += (sender, args) =>
        {
            configUpdatedEventFired = true;
            eventArgs = args;
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangeAnalysis.HasChanges);
        Assert.Equal(3, result.AppliedChanges.Count); // Only user-configurable hot-swappable changes
        Assert.Empty(result.QueuedForRestart);
        Assert.False(result.RequiresRestart);

        // Verify specific changes were applied (only user-configurable properties)
        Assert.Contains(nameof(CursorPhobiaConfiguration.ProximityThreshold), result.AppliedChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.PushDistance), result.AppliedChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout), result.AppliedChanges);

        // Verify hardcoded properties are NOT in applied changes
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.EnableCtrlOverride), result.AppliedChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer), result.AppliedChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.AnimationDurationMs), result.AppliedChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.EnableAnimations), result.AppliedChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.AnimationEasing), result.AppliedChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.HoverTimeoutMs), result.AppliedChanges);

        // Verify event was fired
        Assert.True(configUpdatedEventFired);
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.UpdateResult.Success);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithRestartRequiredChanges_QueuesChangesForRestart()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50, // Same
            PushDistance = 100, // Same
            UpdateIntervalMs = 33, // Changed (restart required)
            MaxUpdateIntervalMs = 100, // Changed (restart required)
            EnableCtrlOverride = true, // Same
            ScreenEdgeBuffer = 20, // Same
            ApplyToAllWindows = true, // Changed (restart required)
            AnimationDurationMs = 200, // Same
            EnableAnimations = true, // Same
            AnimationEasing = AnimationEasing.EaseOut, // Same
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs, // Same
            EnableHoverTimeout = true // Same
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangeAnalysis.HasChanges);
        Assert.Empty(result.AppliedChanges);
        Assert.Equal(3, result.QueuedForRestart.Count);
        Assert.True(result.RequiresRestart);

        // Verify specific changes were queued for restart
        Assert.Contains(nameof(CursorPhobiaConfiguration.UpdateIntervalMs), result.QueuedForRestart);
        Assert.Contains(nameof(CursorPhobiaConfiguration.MaxUpdateIntervalMs), result.QueuedForRestart);
        Assert.Contains(nameof(CursorPhobiaConfiguration.ApplyToAllWindows), result.QueuedForRestart);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithMixedChanges_AppliesHotSwappableAndQueuesRestartRequired()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Changed (hot-swappable)
            PushDistance = 150, // Changed (hot-swappable)
            UpdateIntervalMs = 33, // Changed (restart required)
            MaxUpdateIntervalMs = 100, // Changed (restart required)
            EnableCtrlOverride = false, // Same as hardcoded value (no longer user-configurable)
            ScreenEdgeBuffer = 30, // Same as hardcoded value (no longer user-configurable)
            ApplyToAllWindows = true, // Changed (restart required)
            AnimationDurationMs = 300, // Same as hardcoded value (no longer user-configurable)
            EnableAnimations = false, // Same as hardcoded value (no longer user-configurable)
            AnimationEasing = AnimationEasing.Linear, // Same as hardcoded value (no longer user-configurable)
            HoverTimeoutMs = 3000, // Same as hardcoded value (no longer user-configurable)
            EnableHoverTimeout = false // Changed (hot-swappable)
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangeAnalysis.HasChanges);
        Assert.Equal(3, result.AppliedChanges.Count); // Only user-configurable hot-swappable changes
        Assert.Equal(3, result.QueuedForRestart.Count); // Restart required changes
        Assert.True(result.RequiresRestart);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithInvalidConfiguration_ReturnsValidationFailure()
    {
        // Arrange
        var invalidConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = -10, // Invalid (negative)
            PushDistance = 2000, // Invalid (too large)
            UpdateIntervalMs = 0, // Invalid (too small)
            MaxUpdateIntervalMs = 33,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs,
            EnableHoverTimeout = true
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(invalidConfig);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.ValidationErrors.Count > 0);
        Assert.True(result.ErrorMessage?.Contains("validation failed"));
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _engine.UpdateConfigurationAsync(null!));
    }

    [Fact]
    public async Task UpdateConfigurationAsync_OnDisposedEngine_ReturnsFailure()
    {
        // Arrange
        _engine.Dispose();
        var newConfig = CursorPhobiaConfiguration.CreateDefault();

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.ErrorMessage?.Contains("disposed"));
    }

    [Fact]
    public async Task UpdateConfigurationAsync_DisablingHoverTimeout_ResetsWindowHoverStates()
    {
        // Arrange
        await _engine.StartAsync();

        var windowInfo = new WindowInfo
        {
            WindowHandle = new IntPtr(12345),
            Title = "Test Window",
            Bounds = new Rectangle(100, 100, 300, 200),
            IsMinimized = false
        };

        _mockWindowDetectionService
            .Setup(x => x.GetAllTopMostWindows())
            .Returns(new List<WindowInfo> { windowInfo });

        _mockCursorTracker
            .Setup(x => x.StartTracking())
            .Returns(true);

        // Let the engine process the window
        await _engine.RefreshTrackedWindowsAsync();

        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            UpdateIntervalMs = 16,
            MaxUpdateIntervalMs = 33,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs,
            EnableHoverTimeout = false // Disable hover timeout
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout), result.AppliedChanges);

        await _engine.StopAsync();
    }

    [Fact]
    public void ConfigurationChangeAnalysis_IdentifiesHotSwappableChanges()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Changed (hot-swappable)
            PushDistance = 150, // Changed (hot-swappable)
            UpdateIntervalMs = 16, // Same (hardcoded)
            MaxUpdateIntervalMs = 33, // Same (hardcoded)
            EnableCtrlOverride = false, // Same as hardcoded value (no longer user-configurable)
            ScreenEdgeBuffer = 30, // Same as hardcoded value (no longer user-configurable)
            ApplyToAllWindows = false, // Same
            AnimationDurationMs = 300, // Same as hardcoded value (no longer user-configurable)
            EnableAnimations = false, // Same as hardcoded value (no longer user-configurable)
            AnimationEasing = AnimationEasing.Linear, // Same as hardcoded value (no longer user-configurable)
            HoverTimeoutMs = 3000, // Same as hardcoded value (no longer user-configurable)
            EnableHoverTimeout = false // Changed (hot-swappable)
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, newConfig);

        // Assert
        Assert.True(analysis.HasChanges);
        Assert.False(analysis.RequiresRestart);
        Assert.Equal(3, analysis.HotSwappableChanges.Count); // Only user-configurable properties
        Assert.Empty(analysis.RestartRequiredChanges);

        // Verify only user-configurable properties are detected as hot-swappable
        Assert.Contains(nameof(CursorPhobiaConfiguration.ProximityThreshold), analysis.HotSwappableChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.PushDistance), analysis.HotSwappableChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout), analysis.HotSwappableChanges);

        // Verify hardcoded properties are NOT detected as changes
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.EnableCtrlOverride), analysis.HotSwappableChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer), analysis.HotSwappableChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.AnimationDurationMs), analysis.HotSwappableChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.EnableAnimations), analysis.HotSwappableChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.AnimationEasing), analysis.HotSwappableChanges);
        Assert.DoesNotContain(nameof(CursorPhobiaConfiguration.HoverTimeoutMs), analysis.HotSwappableChanges);
    }

    [Fact]
    public void ConfigurationChangeAnalysis_IdentifiesRestartRequiredChanges()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50, // Same
            PushDistance = 100, // Same
            UpdateIntervalMs = 33, // Changed (restart required)
            MaxUpdateIntervalMs = 100, // Changed (restart required)
            EnableCtrlOverride = true, // Same
            ScreenEdgeBuffer = 20, // Same
            ApplyToAllWindows = true, // Changed (restart required)
            AnimationDurationMs = 200, // Same
            EnableAnimations = true, // Same
            AnimationEasing = AnimationEasing.EaseOut, // Same
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs, // Same
            EnableHoverTimeout = true // Same
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, newConfig);

        // Assert
        Assert.True(analysis.HasChanges);
        Assert.True(analysis.RequiresRestart);
        Assert.Empty(analysis.HotSwappableChanges);
        Assert.Equal(3, analysis.RestartRequiredChanges.Count);

        Assert.Contains(nameof(CursorPhobiaConfiguration.UpdateIntervalMs), analysis.RestartRequiredChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.MaxUpdateIntervalMs), analysis.RestartRequiredChanges);
        Assert.Contains(nameof(CursorPhobiaConfiguration.ApplyToAllWindows), analysis.RestartRequiredChanges);
    }

    [Fact]
    public void ConfigurationChangeAnalysis_WithNoChanges_ReturnsNoChanges()
    {
        // Arrange
        var identicalConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            UpdateIntervalMs = 16,
            MaxUpdateIntervalMs = 33,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs,
            EnableHoverTimeout = true
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, identicalConfig);

        // Assert
        Assert.False(analysis.HasChanges);
        Assert.False(analysis.RequiresRestart);
        Assert.Empty(analysis.HotSwappableChanges);
        Assert.Empty(analysis.RestartRequiredChanges);
    }

    [Fact]
    public void ConfigurationUpdateResult_Success_CreatesSuccessfulResult()
    {
        // Arrange
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, _baseConfiguration);
        var appliedChanges = new[] { "ProximityThreshold", "PushDistance" };
        var queuedForRestart = new[] { "UpdateIntervalMs" };

        // Act
        var result = ConfigurationUpdateResult.CreateSuccess(analysis, appliedChanges, queuedForRestart);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.AppliedChanges.Count);
        Assert.Equal(1, result.QueuedForRestart.Count);
        Assert.True(result.RequiresRestart);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void ConfigurationUpdateResult_Failure_CreatesFailedResult()
    {
        // Arrange
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, _baseConfiguration);
        var errorMessage = "Test error";

        // Act
        var result = ConfigurationUpdateResult.CreateFailure(errorMessage, analysis);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(errorMessage, result.ErrorMessage);
        Assert.Empty(result.AppliedChanges);
        Assert.Empty(result.QueuedForRestart);
        Assert.False(result.RequiresRestart);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public void ConfigurationUpdateResult_ValidationFailure_CreatesValidationFailedResult()
    {
        // Arrange
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, _baseConfiguration);
        var validationErrors = new[] { "Error 1", "Error 2" };

        // Act
        var result = ConfigurationUpdateResult.CreateValidationFailure(analysis, validationErrors);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.ErrorMessage?.Contains("validation failed"));
        Assert.Empty(result.AppliedChanges);
        Assert.Empty(result.QueuedForRestart);
        Assert.False(result.RequiresRestart);
        Assert.Equal(2, result.ValidationErrors.Count);
    }
}