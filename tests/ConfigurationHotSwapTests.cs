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

        _baseConfiguration = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            UpdateIntervalMs = 16,
            MaxUpdateIntervalMs = 50,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = true
        };

        _engine = new CursorPhobiaEngine(
            _mockLogger.Object,
            _mockCursorTracker.Object,
            _mockProximityDetector.Object,
            _mockWindowDetectionService.Object,
            _mockWindowPusher.Object,
            _mockSafetyManager.Object,
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
            MaxUpdateIntervalMs = 50,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = true
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(identicalConfig);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.ChangeAnalysis.HasChanges);
        Assert.Equal(0, result.AppliedChanges.Count);
        Assert.Equal(0, result.QueuedForRestart.Count);
        Assert.False(result.RequiresRestart);
    }

    [Fact]
    public async Task UpdateConfigurationAsync_WithHotSwappableChanges_AppliesChangesImmediately()
    {
        // Arrange
        var newConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Changed
            PushDistance = 150, // Changed
            UpdateIntervalMs = 16, // Same
            MaxUpdateIntervalMs = 50, // Same
            EnableCtrlOverride = false, // Changed
            ScreenEdgeBuffer = 30, // Changed
            ApplyToAllWindows = false, // Same
            AnimationDurationMs = 300, // Changed
            EnableAnimations = false, // Changed
            AnimationEasing = AnimationEasing.Linear, // Changed
            HoverTimeoutMs = 3000, // Changed
            EnableHoverTimeout = false // Changed
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
        Assert.Equal(9, result.AppliedChanges.Count); // All hot-swappable changes
        Assert.Equal(0, result.QueuedForRestart.Count);
        Assert.False(result.RequiresRestart);

        // Verify specific changes were applied
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.ProximityThreshold)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.PushDistance)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.EnableCtrlOverride)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationDurationMs)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.EnableAnimations)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationEasing)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.HoverTimeoutMs)));
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout)));

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
            HoverTimeoutMs = 5000, // Same
            EnableHoverTimeout = true // Same
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangeAnalysis.HasChanges);
        Assert.Equal(0, result.AppliedChanges.Count);
        Assert.Equal(3, result.QueuedForRestart.Count);
        Assert.True(result.RequiresRestart);

        // Verify specific changes were queued for restart
        Assert.True(result.QueuedForRestart.Contains(nameof(CursorPhobiaConfiguration.UpdateIntervalMs)));
        Assert.True(result.QueuedForRestart.Contains(nameof(CursorPhobiaConfiguration.MaxUpdateIntervalMs)));
        Assert.True(result.QueuedForRestart.Contains(nameof(CursorPhobiaConfiguration.ApplyToAllWindows)));
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
            EnableCtrlOverride = false, // Changed (hot-swappable)
            ScreenEdgeBuffer = 30, // Changed (hot-swappable)
            ApplyToAllWindows = true, // Changed (restart required)
            AnimationDurationMs = 300, // Changed (hot-swappable)
            EnableAnimations = false, // Changed (hot-swappable)
            AnimationEasing = AnimationEasing.Linear, // Changed (hot-swappable)
            HoverTimeoutMs = 3000, // Changed (hot-swappable)
            EnableHoverTimeout = false // Changed (hot-swappable)
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChangeAnalysis.HasChanges);
        Assert.Equal(9, result.AppliedChanges.Count); // Hot-swappable changes
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
            MaxUpdateIntervalMs = 50,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = 5000,
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
            MaxUpdateIntervalMs = 50,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = false // Disable hover timeout
        };

        // Act
        var result = await _engine.UpdateConfigurationAsync(newConfig);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.AppliedChanges.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout)));
        
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
            UpdateIntervalMs = 16, // Same
            MaxUpdateIntervalMs = 50, // Same
            EnableCtrlOverride = false, // Changed (hot-swappable)
            ScreenEdgeBuffer = 30, // Changed (hot-swappable)
            ApplyToAllWindows = false, // Same
            AnimationDurationMs = 300, // Changed (hot-swappable)
            EnableAnimations = false, // Changed (hot-swappable)
            AnimationEasing = AnimationEasing.Linear, // Changed (hot-swappable)
            HoverTimeoutMs = 3000, // Changed (hot-swappable)
            EnableHoverTimeout = false // Changed (hot-swappable)
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, newConfig);

        // Assert
        Assert.True(analysis.HasChanges);
        Assert.False(analysis.RequiresRestart);
        Assert.Equal(9, analysis.HotSwappableChanges.Count);
        Assert.Equal(0, analysis.RestartRequiredChanges.Count);

        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.ProximityThreshold)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.PushDistance)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableCtrlOverride)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.ScreenEdgeBuffer)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationDurationMs)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableAnimations)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.AnimationEasing)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.HoverTimeoutMs)));
        Assert.True(analysis.HotSwappableChanges.Contains(nameof(CursorPhobiaConfiguration.EnableHoverTimeout)));
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
            HoverTimeoutMs = 5000, // Same
            EnableHoverTimeout = true // Same
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, newConfig);

        // Assert
        Assert.True(analysis.HasChanges);
        Assert.True(analysis.RequiresRestart);
        Assert.Equal(0, analysis.HotSwappableChanges.Count);
        Assert.Equal(3, analysis.RestartRequiredChanges.Count);

        Assert.True(analysis.RestartRequiredChanges.Contains(nameof(CursorPhobiaConfiguration.UpdateIntervalMs)));
        Assert.True(analysis.RestartRequiredChanges.Contains(nameof(CursorPhobiaConfiguration.MaxUpdateIntervalMs)));
        Assert.True(analysis.RestartRequiredChanges.Contains(nameof(CursorPhobiaConfiguration.ApplyToAllWindows)));
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
            MaxUpdateIntervalMs = 50,
            EnableCtrlOverride = true,
            ScreenEdgeBuffer = 20,
            ApplyToAllWindows = false,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            AnimationEasing = AnimationEasing.EaseOut,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = true
        };

        // Act
        var analysis = new ConfigurationChangeAnalysis(_baseConfiguration, identicalConfig);

        // Assert
        Assert.False(analysis.HasChanges);
        Assert.False(analysis.RequiresRestart);
        Assert.Equal(0, analysis.HotSwappableChanges.Count);
        Assert.Equal(0, analysis.RestartRequiredChanges.Count);
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
        Assert.Equal(0, result.ValidationErrors.Count);
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
        Assert.Equal(0, result.AppliedChanges.Count);
        Assert.Equal(0, result.QueuedForRestart.Count);
        Assert.False(result.RequiresRestart);
        Assert.Equal(0, result.ValidationErrors.Count);
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
        Assert.Equal(0, result.AppliedChanges.Count);
        Assert.Equal(0, result.QueuedForRestart.Count);
        Assert.False(result.RequiresRestart);
        Assert.Equal(2, result.ValidationErrors.Count);
    }
}