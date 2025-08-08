using Xunit;
using CursorPhobia.Core.UI.Forms;
using CursorPhobia.Core.UI.Models;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System.Drawing;
using System.Threading.Tasks;

namespace CursorPhobia.Tests.UI;

/// <summary>
/// Tests for SettingsForm Phase B functionality
/// </summary>
public class SettingsFormTests
{
    private readonly TestLogger _logger;
    private readonly MockConfigurationService _configService;
    private readonly MockCursorPhobiaEngine _engine;

    public SettingsFormTests()
    {
        _logger = new TestLogger();
        _configService = new MockConfigurationService();
        _engine = new MockCursorPhobiaEngine();
    }

    [Fact]
    public void SettingsForm_Constructor_CreatesInstance()
    {
        // Act
        using var form = new SettingsForm(_configService, _engine, _logger);

        // Assert
        Assert.NotNull(form);
        Assert.NotNull(form.CurrentConfiguration);
        Assert.False(form.HasUnsavedChanges);
    }

    [Fact]
    public async Task SettingsForm_LoadConfiguration_LoadsCorrectly()
    {
        // Arrange
        var expectedConfig = CursorPhobiaConfiguration.CreateDefault();
        expectedConfig.ProximityThreshold = 123;
        _configService.SetConfiguration(expectedConfig);

        using var form = new SettingsForm(_configService, _engine, _logger);

        // Act
        await form.LoadConfigurationAsync();

        // Assert
        Assert.Equal(123, form.CurrentConfiguration.ProximityThreshold);
        Assert.False(form.HasUnsavedChanges);
    }

    [Fact]
    public async Task SettingsForm_SaveConfiguration_SavesCorrectly()
    {
        // Arrange
        using var form = new SettingsForm(_configService, _engine, _logger);
        await form.LoadConfigurationAsync();

        // Modify configuration
        form.CurrentConfiguration.ProximityThreshold = 456;

        // Act
        var result = await form.SaveConfigurationAsync();

        // Assert
        Assert.True(result);
        Assert.Equal(456, _configService.LastSavedConfiguration?.ProximityThreshold);
        Assert.False(form.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsForm_ValidateCurrentSettings_WithValidSettings_ReturnsTrue()
    {
        // Arrange
        using var form = new SettingsForm(_configService, _engine, _logger);

        // Act
        var result = form.ValidateCurrentSettings();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SettingsForm_ResetToDefaults_ResetsConfiguration()
    {
        // Arrange
        using var form = new SettingsForm(_configService, _engine, _logger);
        form.CurrentConfiguration.ProximityThreshold = 999;

        // Note: ResetToDefaults shows a dialog, so we can't easily test it in unit tests
        // This would need integration testing or mocking MessageBox
    }

    // Export and Import test methods removed in Phase 4 - functionality eliminated per user feedback
}

/// <summary>
/// Mock configuration service for testing
/// </summary>
public class MockConfigurationService : IConfigurationService
{
    private CursorPhobiaConfiguration _configuration = CursorPhobiaConfiguration.CreateDefault();
    public CursorPhobiaConfiguration? LastSavedConfiguration { get; private set; }

    public void SetConfiguration(CursorPhobiaConfiguration config)
    {
        _configuration = config;
    }

    public Task<CursorPhobiaConfiguration> LoadConfigurationAsync(string filePath)
    {
        return Task.FromResult(_configuration);
    }

    public Task SaveConfigurationAsync(CursorPhobiaConfiguration config, string filePath)
    {
        LastSavedConfiguration = config;
        return Task.CompletedTask;
    }

    public CursorPhobiaConfiguration GetDefaultConfiguration()
    {
        return CursorPhobiaConfiguration.CreateDefault();
    }

    public Task<string> GetDefaultConfigurationPathAsync()
    {
        return Task.FromResult(@"C:\Test\config.json");
    }
}

/// <summary>
/// Mock engine for testing
/// </summary>
public class MockCursorPhobiaEngine : ICursorPhobiaEngine
{
    public bool IsRunning => false;
    public int TrackedWindowCount => 0;
    public double AverageUpdateTimeMs => 0;

    public event EventHandler? EngineStarted;
    public event EventHandler? EngineStopped;
    public event EventHandler<EngineStateChangedEventArgs>? EngineStateChanged;
    public event EventHandler<EnginePerformanceEventArgs>? PerformanceIssueDetected;
    public event EventHandler<WindowPushEventArgs>? WindowPushed;
    public event EventHandler<ConfigurationUpdatedEventArgs>? ConfigurationUpdated;

    public void Dispose() { }

    public EnginePerformanceStats GetPerformanceStats()
    {
        return new EnginePerformanceStats
        {
            IsRunning = false,
            UptimeMs = 0,
            UpdateCount = 0,
            AverageUpdateTimeMs = 0,
            TrackedWindowCount = 0,
            ConfiguredUpdateIntervalMs = 16
        };
    }

    public Task RefreshTrackedWindowsAsync()
    {
        return Task.CompletedTask;
    }

    public Task<ConfigurationUpdateResult> UpdateConfigurationAsync(CursorPhobiaConfiguration newConfiguration)
    {
        var analysis = new ConfigurationChangeAnalysis(CursorPhobiaConfiguration.CreateDefault(), newConfiguration);
        var result = ConfigurationUpdateResult.CreateSuccess(analysis);
        return Task.FromResult(result);
    }

    public Task<bool> StartAsync()
    {
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public WrapBehavior GetEffectiveWrapBehavior(Rectangle windowBounds)
    {
        return new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Adjacent
        };
    }
}