using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for ConfigurationService
/// Tests JSON persistence, error handling, atomic writes, and default behavior
/// </summary>
public class ConfigurationServiceTests : IDisposable
{
    private readonly TestLogger _testLogger;
    private readonly ConfigurationService _configurationService;
    private readonly string _testDirectory;
    
    public ConfigurationServiceTests()
    {
        _testLogger = new TestLogger();
        _configurationService = new ConfigurationService(_testLogger);
        
        // Create a temporary directory for test files
        _testDirectory = Path.Combine(Path.GetTempPath(), "CursorPhobiaTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }
    
    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
    
    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var service = new ConfigurationService(_testLogger);
        
        // Assert
        Assert.NotNull(service);
    }
    
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigurationService(null!));
    }
    
    [Fact]
    public void GetDefaultConfiguration_ReturnsValidConfiguration()
    {
        // Arrange & Act
        var config = _configurationService.GetDefaultConfiguration();
        
        // Assert
        Assert.NotNull(config);
        Assert.Equal(50, config.ProximityThreshold);
        Assert.Equal(100, config.PushDistance);
        Assert.Equal(16, config.UpdateIntervalMs);
        Assert.Equal(50, config.MaxUpdateIntervalMs);
        Assert.True(config.EnableCtrlOverride);
        Assert.Equal(20, config.ScreenEdgeBuffer);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(200, config.AnimationDurationMs);
        Assert.True(config.EnableAnimations);
        Assert.Equal(AnimationEasing.EaseOut, config.AnimationEasing);
        Assert.Equal(5000, config.HoverTimeoutMs);
        Assert.True(config.EnableHoverTimeout);
        Assert.NotNull(config.MultiMonitor);
        
        var validationErrors = config.Validate();
        Assert.Empty(validationErrors);
    }
    
    [Fact]
    public async Task GetDefaultConfigurationPathAsync_ReturnsValidPath()
    {
        // Arrange & Act
        var path = await _configurationService.GetDefaultConfigurationPathAsync();
        
        // Assert
        Assert.NotNull(path);
        Assert.EndsWith("config.json", path);
        Assert.Contains("CursorPhobia", path);
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfig_SavesFile()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var filePath = Path.Combine(_testDirectory, "test_config.json");
        
        // Act
        await _configurationService.SaveConfigurationAsync(config, filePath);
        
        // Assert
        Assert.True(File.Exists(filePath));
        
        var jsonContent = await File.ReadAllTextAsync(filePath);
        Assert.False(string.IsNullOrWhiteSpace(jsonContent));
        
        // Verify JSON is valid and contains expected values
        var savedConfig = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(savedConfig);
        Assert.Equal(config.ProximityThreshold, savedConfig.ProximityThreshold);
        Assert.Equal(config.PushDistance, savedConfig.PushDistance);
        Assert.Equal(config.UpdateIntervalMs, savedConfig.UpdateIntervalMs);
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_CreatesDirectoryIfNeeded()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var nestedDirectory = Path.Combine(_testDirectory, "nested", "path");
        var filePath = Path.Combine(nestedDirectory, "config.json");
        
        // Act
        await _configurationService.SaveConfigurationAsync(config, filePath);
        
        // Assert
        Assert.True(Directory.Exists(nestedDirectory));
        Assert.True(File.Exists(filePath));
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_WithInvalidConfig_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = -1, // Invalid value
            PushDistance = 100
        };
        var filePath = Path.Combine(_testDirectory, "invalid_config.json");
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _configurationService.SaveConfigurationAsync(invalidConfig, filePath);
        });
        
        // Verify file was not created
        Assert.False(File.Exists(filePath));
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_WithNullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "null_config.json");
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _configurationService.SaveConfigurationAsync(null!, filePath);
        });
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _configurationService.SaveConfigurationAsync(config, "");
        });
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithValidFile_LoadsConfiguration()
    {
        // Arrange
        var originalConfig = CursorPhobiaConfiguration.CreateDefault();
        originalConfig.ProximityThreshold = 75; // Change from default
        originalConfig.PushDistance = 150;
        
        var filePath = Path.Combine(_testDirectory, "valid_config.json");
        await _configurationService.SaveConfigurationAsync(originalConfig, filePath);
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(75, loadedConfig.ProximityThreshold);
        Assert.Equal(150, loadedConfig.PushDistance);
        Assert.Equal(originalConfig.UpdateIntervalMs, loadedConfig.UpdateIntervalMs);
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithNonExistentFile_CreatesDefaultConfigurationFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent_config.json");
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        
        // Verify it's the default configuration
        var expectedDefault = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(expectedDefault.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(expectedDefault.PushDistance, loadedConfig.PushDistance);
        
        // Verify file was created
        Assert.True(File.Exists(filePath));
        
        // Verify we can load the created file
        var reloadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        Assert.Equal(loadedConfig.ProximityThreshold, reloadedConfig.ProximityThreshold);
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithCorruptJson_ReturnsDefaultConfiguration()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "corrupt_config.json");
        await File.WriteAllTextAsync(filePath, "{ invalid json content "); // Malformed JSON
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        
        // Should be default configuration
        var expectedDefault = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(expectedDefault.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(expectedDefault.PushDistance, loadedConfig.PushDistance);
        
        // Verify error was logged
        var logs = _testLogger.Logs;
        Assert.Contains(logs, log => log.Contains("JSON parsing error"));
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithEmptyFile_ReturnsDefaultConfiguration()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty_config.json");
        await File.WriteAllTextAsync(filePath, ""); // Empty file
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        
        // Should be default configuration
        var expectedDefault = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(expectedDefault.ProximityThreshold, loadedConfig.ProximityThreshold);
        
        // Verify warning was logged
        var logs = _testLogger.Logs;
        Assert.Contains(logs, log => log.Contains("Configuration file is empty"));
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithInvalidConfiguration_ReturnsDefaultConfiguration()
    {
        // Arrange
        var invalidConfigJson = JsonSerializer.Serialize(new
        {
            proximityThreshold = -50, // Invalid value
            pushDistance = 100,
            updateIntervalMs = 16
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        
        var filePath = Path.Combine(_testDirectory, "invalid_values_config.json");
        await File.WriteAllTextAsync(filePath, invalidConfigJson);
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        
        // Should be default configuration since loaded config was invalid
        var expectedDefault = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(expectedDefault.ProximityThreshold, loadedConfig.ProximityThreshold);
        
        // Verify validation error was logged
        var logs = _testLogger.Logs;
        Assert.Contains(logs, log => log.Contains("validation errors"));
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_WithNullOrEmptyFilePath_ReturnsDefaultConfiguration()
    {
        // Act & Assert - null path
        var config1 = await _configurationService.LoadConfigurationAsync(null!);
        Assert.NotNull(config1);
        
        // Act & Assert - empty path
        var config2 = await _configurationService.LoadConfigurationAsync("");
        Assert.NotNull(config2);
        
        // Act & Assert - whitespace path
        var config3 = await _configurationService.LoadConfigurationAsync("   ");
        Assert.NotNull(config3);
        
        // All should be default configurations
        var expectedDefault = CursorPhobiaConfiguration.CreateDefault();
        Assert.Equal(expectedDefault.ProximityThreshold, config1.ProximityThreshold);
        Assert.Equal(expectedDefault.ProximityThreshold, config2.ProximityThreshold);
        Assert.Equal(expectedDefault.ProximityThreshold, config3.ProximityThreshold);
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_AtomicWrite_TempFileIsUsed()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var filePath = Path.Combine(_testDirectory, "atomic_test_config.json");
        var tempFilePath = filePath + ".tmp";
        
        // Act
        await _configurationService.SaveConfigurationAsync(config, filePath);
        
        // Assert
        Assert.True(File.Exists(filePath));
        Assert.False(File.Exists(tempFilePath));
        
        // Verify the final file contains valid JSON
        var jsonContent = await File.ReadAllTextAsync(filePath);
        var deserializedConfig = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(jsonContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        
        Assert.NotNull(deserializedConfig);
        Assert.Equal(config.ProximityThreshold, deserializedConfig.ProximityThreshold);
    }
    
    [Fact]
    public async Task SaveConfigurationAsync_OverwriteExistingFile_Succeeds()
    {
        // Arrange
        var config1 = CursorPhobiaConfiguration.CreateDefault();
        config1.ProximityThreshold = 60;
        
        var config2 = CursorPhobiaConfiguration.CreateDefault();
        config2.ProximityThreshold = 80;
        
        var filePath = Path.Combine(_testDirectory, "overwrite_test_config.json");
        
        // Act - Save first configuration
        await _configurationService.SaveConfigurationAsync(config1, filePath);
        
        // Verify first save
        var loaded1 = await _configurationService.LoadConfigurationAsync(filePath);
        Assert.Equal(60, loaded1.ProximityThreshold);
        
        // Act - Save second configuration (overwrite)
        await _configurationService.SaveConfigurationAsync(config2, filePath);
        
        // Assert - Verify overwrite worked
        var loaded2 = await _configurationService.LoadConfigurationAsync(filePath);
        Assert.Equal(80, loaded2.ProximityThreshold);
    }
    
    [Fact]
    public async Task LoadConfigurationAsync_JsonWithCamelCase_LoadsCorrectly()
    {
        // Arrange - Create JSON with camelCase property names
        var camelCaseJson = @"{
            ""proximityThreshold"": 125,
            ""pushDistance"": 175,
            ""updateIntervalMs"": 25,
            ""maxUpdateIntervalMs"": 75,
            ""enableCtrlOverride"": false,
            ""screenEdgeBuffer"": 30,
            ""applyToAllWindows"": true,
            ""animationDurationMs"": 300,
            ""enableAnimations"": false,
            ""animationEasing"": 1,
            ""hoverTimeoutMs"": 3000,
            ""enableHoverTimeout"": false,
            ""multiMonitor"": {
                ""enableWrapping"": false,
                ""preferredWrapBehavior"": 1,
                ""respectTaskbarAreas"": false,
                ""perMonitorSettings"": {}
            }
        }";
        
        var filePath = Path.Combine(_testDirectory, "camelcase_config.json");
        await File.WriteAllTextAsync(filePath, camelCaseJson);
        
        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert
        Assert.NotNull(loadedConfig);
        Assert.Equal(125, loadedConfig.ProximityThreshold);
        Assert.Equal(175, loadedConfig.PushDistance);
        Assert.Equal(25, loadedConfig.UpdateIntervalMs);
        Assert.Equal(75, loadedConfig.MaxUpdateIntervalMs);
        Assert.False(loadedConfig.EnableCtrlOverride);
        Assert.Equal(30, loadedConfig.ScreenEdgeBuffer);
        Assert.True(loadedConfig.ApplyToAllWindows);
        Assert.Equal(300, loadedConfig.AnimationDurationMs);
        Assert.False(loadedConfig.EnableAnimations);
        Assert.Equal(AnimationEasing.EaseIn, loadedConfig.AnimationEasing);
        Assert.Equal(3000, loadedConfig.HoverTimeoutMs);
        Assert.False(loadedConfig.EnableHoverTimeout);
        
        Assert.NotNull(loadedConfig.MultiMonitor);
        Assert.False(loadedConfig.MultiMonitor.EnableWrapping);
        Assert.Equal(WrapPreference.Opposite, loadedConfig.MultiMonitor.PreferredWrapBehavior);
        Assert.False(loadedConfig.MultiMonitor.RespectTaskbarAreas);
    }
    
    [Fact]
    public async Task ConfigurationService_RoundTrip_PreservesAllProperties()
    {
        // Arrange - Create a configuration with non-default values
        var originalConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75,
            PushDistance = 125,
            UpdateIntervalMs = 20,
            MaxUpdateIntervalMs = 60,
            EnableCtrlOverride = false,
            ScreenEdgeBuffer = 25,
            ApplyToAllWindows = true,
            AnimationDurationMs = 250,
            EnableAnimations = false,
            AnimationEasing = AnimationEasing.EaseIn,
            HoverTimeoutMs = 4000,
            EnableHoverTimeout = false,
            MultiMonitor = new MultiMonitorConfiguration
            {
                EnableWrapping = false,
                PreferredWrapBehavior = WrapPreference.Adjacent,
                RespectTaskbarAreas = false,
                PerMonitorSettings = new Dictionary<string, PerMonitorSettings>
                {
                    ["TestMonitor"] = new PerMonitorSettings
                    {
                        Enabled = false,
                        CustomProximityThreshold = 100,
                        CustomPushDistance = 200
                    }
                }
            }
        };
        
        var filePath = Path.Combine(_testDirectory, "roundtrip_config.json");
        
        // Act - Save and load
        await _configurationService.SaveConfigurationAsync(originalConfig, filePath);
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);
        
        // Assert - All properties should be preserved
        Assert.Equal(originalConfig.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(originalConfig.PushDistance, loadedConfig.PushDistance);
        Assert.Equal(originalConfig.UpdateIntervalMs, loadedConfig.UpdateIntervalMs);
        Assert.Equal(originalConfig.MaxUpdateIntervalMs, loadedConfig.MaxUpdateIntervalMs);
        Assert.Equal(originalConfig.EnableCtrlOverride, loadedConfig.EnableCtrlOverride);
        Assert.Equal(originalConfig.ScreenEdgeBuffer, loadedConfig.ScreenEdgeBuffer);
        Assert.Equal(originalConfig.ApplyToAllWindows, loadedConfig.ApplyToAllWindows);
        Assert.Equal(originalConfig.AnimationDurationMs, loadedConfig.AnimationDurationMs);
        Assert.Equal(originalConfig.EnableAnimations, loadedConfig.EnableAnimations);
        Assert.Equal(originalConfig.AnimationEasing, loadedConfig.AnimationEasing);
        Assert.Equal(originalConfig.HoverTimeoutMs, loadedConfig.HoverTimeoutMs);
        Assert.Equal(originalConfig.EnableHoverTimeout, loadedConfig.EnableHoverTimeout);
        
        // Multi-monitor configuration
        Assert.NotNull(loadedConfig.MultiMonitor);
        Assert.Equal(originalConfig.MultiMonitor.EnableWrapping, loadedConfig.MultiMonitor.EnableWrapping);
        Assert.Equal(originalConfig.MultiMonitor.PreferredWrapBehavior, loadedConfig.MultiMonitor.PreferredWrapBehavior);
        Assert.Equal(originalConfig.MultiMonitor.RespectTaskbarAreas, loadedConfig.MultiMonitor.RespectTaskbarAreas);
        
        // Per-monitor settings
        Assert.Single(loadedConfig.MultiMonitor.PerMonitorSettings);
        Assert.True(loadedConfig.MultiMonitor.PerMonitorSettings.ContainsKey("TestMonitor"));
        
        var loadedPerMonitorSettings = loadedConfig.MultiMonitor.PerMonitorSettings["TestMonitor"];
        var originalPerMonitorSettings = originalConfig.MultiMonitor.PerMonitorSettings["TestMonitor"];
        
        Assert.Equal(originalPerMonitorSettings.Enabled, loadedPerMonitorSettings.Enabled);
        Assert.Equal(originalPerMonitorSettings.CustomProximityThreshold, loadedPerMonitorSettings.CustomProximityThreshold);
        Assert.Equal(originalPerMonitorSettings.CustomPushDistance, loadedPerMonitorSettings.CustomPushDistance);
    }
}