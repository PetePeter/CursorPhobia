using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Comprehensive tests for ConfigurationWatcherService
/// Tests file watching, debouncing, temporary file filtering, and error handling
/// </summary>
public class ConfigurationWatcherTests : IDisposable
{
    private TestLogger _logger;
    private IConfigurationService _configService;
    private ConfigurationWatcherService _watcher;
    private string _testDirectory;
    private string _testConfigPath;

    public ConfigurationWatcherTests()
    {
        _logger = new TestLogger();
        _configService = new ConfigurationService(_logger);
        _watcher = new ConfigurationWatcherService(_logger, _configService);

        // Create a unique test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), "CursorPhobiaTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "config.json");

        // Create initial config file
        var defaultConfig = CursorPhobiaConfiguration.CreateDefault();
        File.WriteAllText(_testConfigPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Dispose()
    {
        _watcher?.Dispose();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup test directory: {ex.Message}");
            }
        }
    }

    [Fact]
    public async Task StartWatchingAsync_ValidPath_StartsSuccessfully()
    {
        // Act
        var result = await _watcher.StartWatchingAsync(_testConfigPath);

        // Assert
        Assert.True(result);
        Assert.True(_watcher.IsWatching);
        Assert.Equal(_testConfigPath, _watcher.WatchedFilePath);
    }

    [Fact]
    public async Task StartWatchingAsync_InvalidPath_ReturnsFalse()
    {
        // Arrange
        var invalidPath = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _watcher.StartWatchingAsync(invalidPath));
    }

    [Fact]
    public async Task StartWatchingAsync_InvalidDebounceDelay_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _watcher.StartWatchingAsync(_testConfigPath, 50)); // Too small

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _watcher.StartWatchingAsync(_testConfigPath, 15000)); // Too large
    }

    [Fact]
    public async Task StartWatchingAsync_NonExistentDirectory_CreatesDirectoryAndSucceeds()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");
        var configPath = Path.Combine(nonExistentDir, "config.json");

        // Act
        var result = await _watcher.StartWatchingAsync(configPath);

        // Assert
        Assert.True(result);
        Assert.True(Directory.Exists(nonExistentDir));
        Assert.True(_watcher.IsWatching);
    }

    [Fact]
    public async Task StopWatchingAsync_WhenWatching_StopsSuccessfully()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath);
        Assert.True(_watcher.IsWatching);

        // Act
        await _watcher.StopWatchingAsync();

        // Assert
        Assert.False(_watcher.IsWatching);
        Assert.Null(_watcher.WatchedFilePath);
    }

    [Fact]
    public async Task ConfigurationChange_ValidFile_FiresChangedEvent()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200); // Short debounce for testing

        ConfigurationFileChangedEventArgs? receivedArgs = null;
        _watcher.ConfigurationFileChanged += (sender, args) => receivedArgs = args;

        // Act
        var newConfig = CursorPhobiaConfiguration.CreateDefault();
        newConfig.ProximityThreshold = 250; // Change a value

        var json = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);

        // Wait for debouncing and processing
        await Task.Delay(500);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(_testConfigPath, receivedArgs.FilePath);
        Assert.Equal(250, receivedArgs.Configuration.ProximityThreshold);
        Assert.True(receivedArgs.DebouncedEventCount >= 1);
    }

    [Fact]
    public async Task ConfigurationChange_InvalidJson_LoadsDefaultConfiguration()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        ConfigurationFileChangedEventArgs? receivedArgs = null;
        _watcher.ConfigurationFileChanged += (sender, args) => receivedArgs = args;

        // Act
        File.WriteAllText(_testConfigPath, "{ invalid json }");

        // Wait for processing
        await Task.Delay(500);

        // Assert - ConfigurationService is resilient and returns defaults for invalid JSON
        Assert.NotNull(receivedArgs);
        Assert.Equal(_testConfigPath, receivedArgs.FilePath);
        // Should load default configuration when JSON is invalid
        Assert.Equal(CursorPhobiaConfiguration.CreateDefault().ProximityThreshold, receivedArgs.Configuration.ProximityThreshold);
    }

    [Fact]
    public async Task ConfigurationChange_MultipleRapidChanges_DebouncesProperly()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 300);

        var eventCount = 0;
        _watcher.ConfigurationFileChanged += (sender, args) =>
        {
            eventCount++;
            Assert.True(args.DebouncedEventCount > 1);
        };

        // Act - Make multiple rapid changes
        for (int i = 0; i < 5; i++)
        {
            var config = CursorPhobiaConfiguration.CreateDefault();
            config.ProximityThreshold = 100 + i * 10;

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_testConfigPath, json);

            await Task.Delay(50); // Rapid changes within debounce window
        }

        // Wait for debouncing
        await Task.Delay(600);

        // Assert
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task ConfigurationChange_TemporaryFiles_AreFiltered()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        var eventCount = 0;
        _watcher.ConfigurationFileChanged += (sender, args) => eventCount++;

        // Act - Create various temporary files that should be filtered
        var tempFiles = new[]
        {
            Path.Combine(_testDirectory, "config.json.tmp"),
            Path.Combine(_testDirectory, "config.json.bak"),
            Path.Combine(_testDirectory, "config.json.swp"),
            Path.Combine(_testDirectory, "config.json~"),
            Path.Combine(_testDirectory, ".#config.json"),
            Path.Combine(_testDirectory, "#config.json#")
        };

        foreach (var tempFile in tempFiles)
        {
            File.WriteAllText(tempFile, "temporary content");
            await Task.Delay(50);
        }

        // Wait for any potential processing
        await Task.Delay(500);

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task ConfigurationChange_NonTargetFiles_AreFiltered()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        var eventCount = 0;
        _watcher.ConfigurationFileChanged += (sender, args) => eventCount++;

        // Act - Create other files in the same directory
        var otherFiles = new[]
        {
            Path.Combine(_testDirectory, "other.json"),
            Path.Combine(_testDirectory, "settings.json"),
            Path.Combine(_testDirectory, "backup.json")
        };

        foreach (var otherFile in otherFiles)
        {
            File.WriteAllText(otherFile, "{}");
            await Task.Delay(50);
        }

        // Wait for any potential processing
        await Task.Delay(500);

        // Assert
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task GetStatistics_AfterActivity_ReturnsAccurateStats()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        // Act - Generate some activity
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = 300;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);

        await Task.Delay(500); // Wait for processing

        var stats = _watcher.GetStatistics();

        // Assert
        Assert.NotNull(stats.WatchingStartedAt);
        Assert.True(stats.TotalFileSystemEvents > 0);
        Assert.True(stats.SuccessfulReloads >= 1);
        Assert.True(stats.SuccessRate > 0);
        Assert.NotNull(stats.LastSuccessfulReload);
    }

    [Fact]
    public async Task Dispose_WhenWatching_StopsWatchingGracefully()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath);
        Assert.True(_watcher.IsWatching);

        // Act
        _watcher.Dispose();

        // Assert
        Assert.False(_watcher.IsWatching);

        // Subsequent operations should not throw
        var stats = _watcher.GetStatistics();
        Assert.NotNull(stats);
    }

    [Fact]
    public async Task StartWatchingAsync_AfterDisposal_ReturnsFalse()
    {
        // Arrange
        _watcher.Dispose();

        // Act
        var result = await _watcher.StartWatchingAsync(_testConfigPath);

        // Assert
        Assert.False(result);
        Assert.False(_watcher.IsWatching);
    }

    [Fact]
    public async Task ConfigurationChange_WithValidationErrors_LoadsDefaultConfiguration()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        ConfigurationFileChangedEventArgs? receivedArgs = null;
        _watcher.ConfigurationFileChanged += (sender, args) => receivedArgs = args;

        // Act - Create config with validation errors
        var invalidConfig = new
        {
            proximityThreshold = -100, // Invalid negative value
            pushDistance = 0, // Invalid zero value
            updateIntervalMs = 0 // Invalid zero value
        };

        var json = JsonSerializer.Serialize(invalidConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_testConfigPath, json);

        // Wait for processing
        await Task.Delay(500);

        // Assert - ConfigurationService is resilient and returns defaults for invalid configurations
        Assert.NotNull(receivedArgs);
        // Should load default configuration when validation fails
        Assert.Equal(CursorPhobiaConfiguration.CreateDefault().ProximityThreshold, receivedArgs.Configuration.ProximityThreshold);
    }

    [Fact]
    public async Task ConfigurationChange_FileCreation_IsDetected()
    {
        // Arrange
        var newConfigPath = Path.Combine(_testDirectory, "new_config.json");

        await _watcher.StartWatchingAsync(newConfigPath, 200);

        ConfigurationFileChangedEventArgs? receivedArgs = null;
        _watcher.ConfigurationFileChanged += (sender, args) => receivedArgs = args;

        // Act - Create the file
        var config = CursorPhobiaConfiguration.CreateDefault();
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(newConfigPath, json);

        // Wait for processing
        await Task.Delay(500);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(newConfigPath, receivedArgs.FilePath);
    }

    [Fact]
    public async Task ConfigurationChange_DuringFileWrite_HandlesFileLocking()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        var eventFired = false;
        _watcher.ConfigurationFileChanged += (sender, args) => eventFired = true;

        // Act - Simulate a locked file scenario by using exclusive access
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = 999;

        using (var fileStream = new FileStream(_testConfigPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(fileStream))
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await writer.WriteAsync(json);
            await writer.FlushAsync();

            // File is locked here, wait a bit then release
            await Task.Delay(200);
        }

        // Wait for processing with retries
        await Task.Delay(1000);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void GetStatistics_InitialState_ReturnsZeroedStats()
    {
        // Act
        var stats = _watcher.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalFileSystemEvents);
        Assert.Equal(0, stats.SuccessfulReloads);
        Assert.Equal(0, stats.FailedReloads);
        Assert.Equal(0, stats.DebouncedEvents);
        Assert.Null(stats.WatchingStartedAt);
        Assert.Equal(0, stats.SuccessRate);
    }

    [Fact]
    public async Task ConfigurationChange_LargeFile_IsProcessedCorrectly()
    {
        // Arrange
        await _watcher.StartWatchingAsync(_testConfigPath, 200);

        ConfigurationFileChangedEventArgs? receivedArgs = null;
        _watcher.ConfigurationFileChanged += (sender, args) => receivedArgs = args;

        // Act - Create a large configuration with many properties
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = 500;

        // Add some complexity to make the JSON larger
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Make the file even larger by adding whitespace (simulating real-world scenarios)
        var largeJson = json + Environment.NewLine + string.Join(Environment.NewLine, Enumerable.Repeat("", 100));

        File.WriteAllText(_testConfigPath, largeJson);

        // Wait for processing
        await Task.Delay(1000);

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal(500, receivedArgs.Configuration.ProximityThreshold);
    }
}