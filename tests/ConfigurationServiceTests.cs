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
        Assert.Equal(HardcodedDefaults.ProximityThreshold, config.ProximityThreshold);
        Assert.Equal(HardcodedDefaults.PushDistance, config.PushDistance);
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, config.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, config.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.EnableCtrlOverride, config.EnableCtrlOverride);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, config.ScreenEdgeBuffer);
        Assert.False(config.ApplyToAllWindows);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, config.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, config.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, config.AnimationEasing);
        Assert.Equal(HardcodedDefaults.HoverTimeoutMs, config.HoverTimeoutMs);
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

        // Should have applied graceful degradation to fix invalid values
        Assert.Equal(HardcodedDefaults.ProximityThreshold, loadedConfig.ProximityThreshold); // -50 should be fixed to 50
        Assert.Equal(100, loadedConfig.PushDistance); // Valid value should be preserved
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, loadedConfig.UpdateIntervalMs); // Obsolete property should use hardcoded default

        // Verify graceful degradation was logged
        var logs = _testLogger.Logs;
        Assert.Contains(logs, log => log.Contains("Applying graceful degradation"));
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

        // Assert - User-configurable properties should be preserved
        Assert.NotNull(loadedConfig);
        Assert.Equal(125, loadedConfig.ProximityThreshold);
        Assert.Equal(175, loadedConfig.PushDistance);
        Assert.False(loadedConfig.EnableCtrlOverride);
        Assert.True(loadedConfig.ApplyToAllWindows);
        Assert.Equal(3000, loadedConfig.HoverTimeoutMs);
        Assert.False(loadedConfig.EnableHoverTimeout);

        // Assert - Obsolete properties should use hardcoded defaults (migrated automatically)
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, loadedConfig.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, loadedConfig.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, loadedConfig.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, loadedConfig.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, loadedConfig.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, loadedConfig.AnimationEasing);

        Assert.NotNull(loadedConfig.MultiMonitor);
        Assert.False(loadedConfig.MultiMonitor.EnableWrapping);
        Assert.Equal(WrapPreference.Opposite, loadedConfig.MultiMonitor.PreferredWrapBehavior);
        Assert.False(loadedConfig.MultiMonitor.RespectTaskbarAreas);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithPathTraversalAttempt_ThrowsArgumentException()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var maliciousPath = Path.Combine(_testDirectory, "..", "..", "system32", "config.json");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _configurationService.SaveConfigurationAsync(config, maliciousPath);
        });

        Assert.Contains("directory traversal patterns", exception.Message);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithInvalidPathCharacters_ThrowsArgumentException()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var invalidPath = Path.Combine(_testDirectory, "config\x00.json"); // Contains null character which is invalid

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _configurationService.SaveConfigurationAsync(config, invalidPath);
        });

        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithSystemDirectoryPath_ThrowsArgumentException()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var systemConfigPath = Path.Combine(systemPath, "test_config.json");

        // Act & Assert
        if (!string.IsNullOrEmpty(systemPath))
        {
            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await _configurationService.SaveConfigurationAsync(config, systemConfigPath);
            });

            Assert.Contains("system directory not allowed", exception.Message);
        }
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidPath_PassesValidation()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        var validPath = Path.Combine(_testDirectory, "valid", "config.json");

        // Act & Assert - Should not throw
        await _configurationService.SaveConfigurationAsync(config, validPath);

        // Verify file was created
        Assert.True(File.Exists(validPath));
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

        // Assert - User-configurable properties should be preserved
        Assert.Equal(originalConfig.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(originalConfig.PushDistance, loadedConfig.PushDistance);
        Assert.Equal(originalConfig.EnableCtrlOverride, loadedConfig.EnableCtrlOverride);
        Assert.Equal(originalConfig.ApplyToAllWindows, loadedConfig.ApplyToAllWindows);
        Assert.Equal(originalConfig.HoverTimeoutMs, loadedConfig.HoverTimeoutMs);
        Assert.Equal(originalConfig.EnableHoverTimeout, loadedConfig.EnableHoverTimeout);

        // Assert - Obsolete properties should use hardcoded defaults after migration
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, loadedConfig.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, loadedConfig.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, loadedConfig.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, loadedConfig.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, loadedConfig.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, loadedConfig.AnimationEasing);

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

    #region Migration Tests

    [Fact]
    public async Task LoadConfigurationAsync_WithOldConfigurationHavingObsoleteValues_MigratesToHardcodedDefaults()
    {
        // Arrange - Create an old configuration with obsolete values different from hardcoded defaults
        var oldConfigJson = $$"""
        {
            "proximityThreshold": 50,
            "pushDistance": 100,
            "updateIntervalMs": 25,
            "maxUpdateIntervalMs": 50,
            "enableCtrlOverride": true,
            "ctrlReleaseToleranceDistance": 75,
            "alwaysOnTopRepelBorderDistance": 45,
            "screenEdgeBuffer": 15,
            "applyToAllWindows": false,
            "animationDurationMs": 500,
            "enableAnimations": false,
            "animationEasing": 1,
            "hoverTimeoutMs": 5000,
            "enableHoverTimeout": true,
            "multiMonitor": {
                "enableWrapping": true,
                "preferredWrapBehavior": 2,
                "respectTaskbarAreas": true
            }
        }
        """;

        var filePath = Path.Combine(_testDirectory, "old_config.json");
        await File.WriteAllTextAsync(filePath, oldConfigJson);

        // Act
        var migratedConfig = await _configurationService.LoadConfigurationAsync(filePath);

        // Assert - User-configurable values should remain unchanged
        Assert.Equal(50, migratedConfig.ProximityThreshold);
        Assert.Equal(100, migratedConfig.PushDistance);
        Assert.True(migratedConfig.EnableCtrlOverride);
        Assert.False(migratedConfig.ApplyToAllWindows);
        Assert.Equal(5000, migratedConfig.HoverTimeoutMs);
        Assert.True(migratedConfig.EnableHoverTimeout);

        // Assert - Obsolete values should be migrated to hardcoded defaults
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, migratedConfig.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, migratedConfig.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.CtrlReleaseToleranceDistance, migratedConfig.CtrlReleaseToleranceDistance);
        Assert.Equal(HardcodedDefaults.AlwaysOnTopRepelBorderDistance, migratedConfig.AlwaysOnTopRepelBorderDistance);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, migratedConfig.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, migratedConfig.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, migratedConfig.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, migratedConfig.AnimationEasing);

        // Verify migration was logged
        Assert.Contains(_testLogger.Logs, log => 
            log.Contains("INFO:") && 
            log.Contains("Configuration migration completed"));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithValidCurrentConfiguration_PerformsNoMigration()
    {
        // Arrange - Create a current configuration that already has hardcoded defaults
        var currentConfigJson = $$"""
        {
            "proximityThreshold": {{HardcodedDefaults.ProximityThreshold}},
            "pushDistance": {{HardcodedDefaults.PushDistance}},
            "updateIntervalMs": {{HardcodedDefaults.UpdateIntervalMs}},
            "maxUpdateIntervalMs": {{HardcodedDefaults.MaxUpdateIntervalMs}},
            "enableCtrlOverride": true,
            "ctrlReleaseToleranceDistance": {{HardcodedDefaults.CtrlReleaseToleranceDistance}},
            "alwaysOnTopRepelBorderDistance": {{HardcodedDefaults.AlwaysOnTopRepelBorderDistance}},
            "screenEdgeBuffer": {{HardcodedDefaults.ScreenEdgeBuffer}},
            "applyToAllWindows": false,
            "animationDurationMs": {{HardcodedDefaults.AnimationDurationMs}},
            "enableAnimations": {{HardcodedDefaults.EnableAnimations.ToString().ToLowerInvariant()}},
            "animationEasing": {{(int)HardcodedDefaults.AnimationEasing}},
            "hoverTimeoutMs": 5000,
            "enableHoverTimeout": true
        }
        """;

        var filePath = Path.Combine(_testDirectory, "current_config.json");
        await File.WriteAllTextAsync(filePath, currentConfigJson);

        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);

        // Assert - All values should remain as expected
        Assert.Equal(HardcodedDefaults.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(HardcodedDefaults.PushDistance, loadedConfig.PushDistance);
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, loadedConfig.UpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, loadedConfig.MaxUpdateIntervalMs);
        Assert.Equal(HardcodedDefaults.CtrlReleaseToleranceDistance, loadedConfig.CtrlReleaseToleranceDistance);
        Assert.Equal(HardcodedDefaults.AlwaysOnTopRepelBorderDistance, loadedConfig.AlwaysOnTopRepelBorderDistance);
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, loadedConfig.ScreenEdgeBuffer);
        Assert.Equal(HardcodedDefaults.AnimationDurationMs, loadedConfig.AnimationDurationMs);
        Assert.Equal(HardcodedDefaults.EnableAnimations, loadedConfig.EnableAnimations);
        Assert.Equal(HardcodedDefaults.AnimationEasing, loadedConfig.AnimationEasing);

        // Verify no migration was needed
        Assert.Contains(_testLogger.Logs, log => 
            log.Contains("DEBUG:") && 
            log.Contains("No migration needed"));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithOutOfRangeValues_AppliesGracefulDegradation()
    {
        // Arrange - Create configuration with out-of-range values
        var invalidConfigJson = """
        {
            "proximityThreshold": -10,
            "pushDistance": 2000,
            "updateIntervalMs": 16,
            "maxUpdateIntervalMs": 33,
            "enableCtrlOverride": true,
            "ctrlReleaseToleranceDistance": 50,
            "alwaysOnTopRepelBorderDistance": 30,
            "screenEdgeBuffer": 20,
            "applyToAllWindows": false,
            "animationDurationMs": 200,
            "enableAnimations": true,
            "animationEasing": 2,
            "hoverTimeoutMs": 50000,
            "enableHoverTimeout": true
        }
        """;

        var filePath = Path.Combine(_testDirectory, "invalid_config.json");
        await File.WriteAllTextAsync(filePath, invalidConfigJson);

        // Act
        var migratedConfig = await _configurationService.LoadConfigurationAsync(filePath);

        // Assert - Out-of-range values should be reset to hardcoded defaults
        Assert.Equal(HardcodedDefaults.ProximityThreshold, migratedConfig.ProximityThreshold);
        Assert.Equal(HardcodedDefaults.PushDistance, migratedConfig.PushDistance);
        Assert.Equal(HardcodedDefaults.HoverTimeoutMs, migratedConfig.HoverTimeoutMs); // Default hover timeout

        // Verify graceful degradation was logged
        Assert.Contains(_testLogger.Logs, log => 
            log.Contains("WARN:") && 
            log.Contains("Applying graceful degradation"));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithPartialObsoleteValues_MigratesOnlyNecessaryProperties()
    {
        // Arrange - Create configuration with only some obsolete values different from defaults
        var partialOldConfigJson = $$"""
        {
            "proximityThreshold": 75,
            "pushDistance": 150,
            "updateIntervalMs": {{HardcodedDefaults.UpdateIntervalMs}},
            "maxUpdateIntervalMs": 60,
            "enableCtrlOverride": false,
            "ctrlReleaseToleranceDistance": {{HardcodedDefaults.CtrlReleaseToleranceDistance}},
            "alwaysOnTopRepelBorderDistance": {{HardcodedDefaults.AlwaysOnTopRepelBorderDistance}},
            "screenEdgeBuffer": 25,
            "applyToAllWindows": true,
            "animationDurationMs": {{HardcodedDefaults.AnimationDurationMs}},
            "enableAnimations": {{HardcodedDefaults.EnableAnimations.ToString().ToLowerInvariant()}},
            "animationEasing": {{(int)HardcodedDefaults.AnimationEasing}},
            "hoverTimeoutMs": 8000,
            "enableHoverTimeout": false
        }
        """;

        var filePath = Path.Combine(_testDirectory, "partial_old_config.json");
        await File.WriteAllTextAsync(filePath, partialOldConfigJson);

        // Act
        var migratedConfig = await _configurationService.LoadConfigurationAsync(filePath);

        // Assert - User-configurable values should remain unchanged
        Assert.Equal(75, migratedConfig.ProximityThreshold);
        Assert.Equal(150, migratedConfig.PushDistance);
        Assert.False(migratedConfig.EnableCtrlOverride);
        Assert.True(migratedConfig.ApplyToAllWindows);
        Assert.Equal(8000, migratedConfig.HoverTimeoutMs);
        Assert.False(migratedConfig.EnableHoverTimeout);

        // Assert - Only the obsolete values that differed should be migrated
        Assert.Equal(HardcodedDefaults.UpdateIntervalMs, migratedConfig.UpdateIntervalMs); // Already correct, no migration needed
        Assert.Equal(HardcodedDefaults.MaxUpdateIntervalMs, migratedConfig.MaxUpdateIntervalMs); // Should be migrated
        Assert.Equal(HardcodedDefaults.ScreenEdgeBuffer, migratedConfig.ScreenEdgeBuffer); // Should be migrated

        // Verify selective migration was logged
        var migrationLogs = _testLogger.Logs.Where(log => 
            log.Contains("INFO:") && 
            log.Contains("Migrating")).ToList();

        // Should only have migration entries for properties that actually needed migration
        Assert.Contains(migrationLogs, log => log.Contains("MaxUpdateIntervalMs"));
        Assert.Contains(migrationLogs, log => log.Contains("ScreenEdgeBuffer"));
        
        // Should NOT have migration entries for properties that were already correct
        Assert.DoesNotContain(migrationLogs, log => log.Contains("UpdateIntervalMs") && !log.Contains("Max"));
    }

    [Fact]
    public async Task LoadConfigurationAsync_WithMalformedMultiMonitorConfig_HandlesValidationGracefully()
    {
        // Arrange - Create configuration with potentially problematic multi-monitor settings
        var configWithBadMultiMonitorJson = """
        {
            "proximityThreshold": 50,
            "pushDistance": 100,
            "updateIntervalMs": 16,
            "maxUpdateIntervalMs": 33,
            "enableCtrlOverride": true,
            "hoverTimeoutMs": 5000,
            "enableHoverTimeout": true,
            "multiMonitor": {
                "enableWrapping": true,
                "transitionFeedbackDurationMs": -100,
                "crossMonitorThreshold": 200,
                "perMonitorSettings": {
                    "": {
                        "enabled": true,
                        "customProximityThreshold": -50
                    }
                }
            }
        }
        """;

        var filePath = Path.Combine(_testDirectory, "bad_multimonitor_config.json");
        await File.WriteAllTextAsync(filePath, configWithBadMultiMonitorJson);

        // Act
        var loadedConfig = await _configurationService.LoadConfigurationAsync(filePath);

        // Assert - Should fall back to default configuration due to validation errors
        Assert.NotNull(loadedConfig);
        
        // Should be default configuration values
        var defaultConfig = _configurationService.GetDefaultConfiguration();
        Assert.Equal(defaultConfig.ProximityThreshold, loadedConfig.ProximityThreshold);
        Assert.Equal(defaultConfig.PushDistance, loadedConfig.PushDistance);

        // Verify validation error was logged
        Assert.Contains(_testLogger.Logs, log => 
            log.Contains("WARN:") && 
            log.Contains("validation errors"));
    }

    #endregion
}