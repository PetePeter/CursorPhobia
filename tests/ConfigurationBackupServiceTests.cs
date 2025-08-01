using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for ConfigurationBackupService
/// Tests backup creation, rotation, restoration, cleanup, and error handling
/// </summary>
public class ConfigurationBackupServiceTests : IDisposable
{
    private readonly TestLogger _testLogger;
    private readonly ConfigurationBackupService _backupService;
    private readonly string _testDirectory;
    private readonly CursorPhobiaConfiguration _testConfiguration;

    public ConfigurationBackupServiceTests()
    {
        _testLogger = new TestLogger();
        _backupService = new ConfigurationBackupService(_testLogger);
        
        // Create a temporary directory for test files
        _testDirectory = Path.Combine(Path.GetTempPath(), "CursorPhobiaBackupTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        
        // Create a test configuration
        _testConfiguration = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75,
            PushDistance = 150,
            EnableCtrlOverride = false
        };
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

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var service = new ConfigurationBackupService(_testLogger);
        
        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigurationBackupService(null!));
    }

    #endregion

    #region GetBackupFileName Tests

    [Fact]
    public void GetBackupFileName_ValidBackupNumber_ReturnsCorrectFormat()
    {
        // Arrange
        const int backupNumber = 1;
        
        // Act
        var fileName = _backupService.GetBackupFileName(backupNumber);
        
        // Assert
        Assert.Equal("config.backup.1.json", fileName);
    }

    [Fact]
    public void GetBackupFileName_MultipleBackupNumbers_ReturnsCorrectFormats()
    {
        // Act & Assert
        Assert.Equal("config.backup.1.json", _backupService.GetBackupFileName(1));
        Assert.Equal("config.backup.2.json", _backupService.GetBackupFileName(2));
        Assert.Equal("config.backup.3.json", _backupService.GetBackupFileName(3));
        Assert.Equal("config.backup.10.json", _backupService.GetBackupFileName(10));
    }

    [Fact]
    public void GetBackupFileName_ZeroBackupNumber_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _backupService.GetBackupFileName(0));
    }

    [Fact]
    public void GetBackupFileName_NegativeBackupNumber_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _backupService.GetBackupFileName(-1));
    }

    #endregion

    #region CreateBackupAsync Tests

    [Fact]
    public async Task CreateBackupAsync_NullSourcePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CreateBackupAsync(null!, _testDirectory));
    }

    [Fact]
    public async Task CreateBackupAsync_EmptySourcePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CreateBackupAsync("", _testDirectory));
    }

    [Fact]
    public async Task CreateBackupAsync_NullBackupDirectory_ThrowsArgumentException()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CreateBackupAsync(sourceFile, null!));
    }

    [Fact]
    public async Task CreateBackupAsync_EmptyBackupDirectory_ThrowsArgumentException()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CreateBackupAsync(sourceFile, ""));
    }

    [Fact]
    public async Task CreateBackupAsync_NonExistentSourceFile_LogsWarningAndReturns()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.json");
        var backupDir = Path.Combine(_testDirectory, "backups");
        
        // Act
        await _backupService.CreateBackupAsync(nonExistentFile, backupDir);
        
        // Assert
        Assert.Contains(_testLogger.Logs, log => log.Contains("WARN") && log.Contains("does not exist"));
        Assert.False(Directory.Exists(backupDir));
    }

    [Fact]
    public async Task CreateBackupAsync_ValidSourceFile_CreatesBackup()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        var backupDir = Path.Combine(_testDirectory, "backups");
        
        // Act
        await _backupService.CreateBackupAsync(sourceFile, backupDir);
        
        // Assert
        var backupFile = Path.Combine(backupDir, "config.backup.1.json");
        Assert.True(File.Exists(backupFile));
        
        var backupContent = await File.ReadAllTextAsync(backupFile);
        var originalContent = await File.ReadAllTextAsync(sourceFile);
        Assert.Equal(originalContent, backupContent);
        
        Assert.Contains(_testLogger.Logs, log => log.Contains("INFO") && log.Contains("Successfully created backup"));
    }

    [Fact]
    public async Task CreateBackupAsync_NonExistentBackupDirectory_CreatesDirectory()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        var backupDir = Path.Combine(_testDirectory, "new_backup_dir");
        
        // Act
        await _backupService.CreateBackupAsync(sourceFile, backupDir);
        
        // Assert
        Assert.True(Directory.Exists(backupDir));
        var backupFile = Path.Combine(backupDir, "config.backup.1.json");
        Assert.True(File.Exists(backupFile));
    }

    [Fact]
    public async Task CreateBackupAsync_MultipleBackups_RotatesCorrectly()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        var backupDir = Path.Combine(_testDirectory, "backups");
        
        // Create first backup
        await _backupService.CreateBackupAsync(sourceFile, backupDir);
        
        // Modify source file
        await File.WriteAllTextAsync(sourceFile, JsonSerializer.Serialize(new CursorPhobiaConfiguration
        {
            ProximityThreshold = 100
        }, new JsonSerializerOptions { WriteIndented = true }));
        
        // Act - Create second backup
        await _backupService.CreateBackupAsync(sourceFile, backupDir);
        
        // Assert
        var backup1 = Path.Combine(backupDir, "config.backup.1.json");
        var backup2 = Path.Combine(backupDir, "config.backup.2.json");
        
        Assert.True(File.Exists(backup1));
        Assert.True(File.Exists(backup2));
        
        // Backup 1 should have the new content (ProximityThreshold = 100)
        var backup1Content = await File.ReadAllTextAsync(backup1);
        var backup1Config = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(backup1Content);
        Assert.Equal(100, backup1Config!.ProximityThreshold);
        
        // Backup 2 should have the old content (ProximityThreshold = 75)
        var backup2Content = await File.ReadAllTextAsync(backup2);
        var backup2Config = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(backup2Content, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
        Assert.Equal(75, backup2Config!.ProximityThreshold);
    }

    [Fact]
    public async Task CreateBackupAsync_FourBackups_DeletesOldest()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        var backupDir = Path.Combine(_testDirectory, "backups");
        
        // Create three backups with different content
        for (int i = 1; i <= 4; i++)
        {
            await File.WriteAllTextAsync(sourceFile, JsonSerializer.Serialize(new CursorPhobiaConfiguration
            {
                ProximityThreshold = i * 10
            }, new JsonSerializerOptions { WriteIndented = true }));
            
            await _backupService.CreateBackupAsync(sourceFile, backupDir);
            
            // Small delay to ensure different timestamps
            await Task.Delay(10);
        }
        
        // Assert
        var backup1 = Path.Combine(backupDir, "config.backup.1.json");
        var backup2 = Path.Combine(backupDir, "config.backup.2.json");
        var backup3 = Path.Combine(backupDir, "config.backup.3.json");
        var backup4 = Path.Combine(backupDir, "config.backup.4.json");
        
        Assert.True(File.Exists(backup1));
        Assert.True(File.Exists(backup2));
        Assert.True(File.Exists(backup3));
        Assert.False(File.Exists(backup4)); // Fourth backup should not exist (max 3)
        
        // Verify content rotation
        var backup1Content = await File.ReadAllTextAsync(backup1);
        var backup1Config = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(backup1Content);
        Assert.Equal(40, backup1Config!.ProximityThreshold); // Most recent
    }

    #endregion

    #region GetAvailableBackupsAsync Tests

    [Fact]
    public async Task GetAvailableBackupsAsync_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.GetAvailableBackupsAsync(null!));
    }

    [Fact]
    public async Task GetAvailableBackupsAsync_EmptyDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.GetAvailableBackupsAsync(""));
    }

    [Fact]
    public async Task GetAvailableBackupsAsync_NonExistentDirectory_ReturnsEmptyArray()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");
        
        // Act
        var backups = await _backupService.GetAvailableBackupsAsync(nonExistentDir);
        
        // Assert
        Assert.Empty(backups);
    }

    [Fact]
    public async Task GetAvailableBackupsAsync_EmptyDirectory_ReturnsEmptyArray()
    {
        // Arrange
        var emptyDir = Path.Combine(_testDirectory, "empty");
        Directory.CreateDirectory(emptyDir);
        
        // Act
        var backups = await _backupService.GetAvailableBackupsAsync(emptyDir);
        
        // Assert
        Assert.Empty(backups);
    }

    [Fact]
    public async Task GetAvailableBackupsAsync_WithBackups_ReturnsSortedArray()
    {
        // Arrange
        var backupDir = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(backupDir);
        
        // Create backup files (out of order to test sorting)
        var backup3 = Path.Combine(backupDir, "config.backup.3.json");
        var backup1 = Path.Combine(backupDir, "config.backup.1.json");
        var backup2 = Path.Combine(backupDir, "config.backup.2.json");
        
        await File.WriteAllTextAsync(backup3, "{}");
        await Task.Delay(10);
        await File.WriteAllTextAsync(backup1, "{}");
        await Task.Delay(10);
        await File.WriteAllTextAsync(backup2, "{}");
        
        // Act
        var backups = await _backupService.GetAvailableBackupsAsync(backupDir);
        
        // Assert
        Assert.Equal(3, backups.Length);
        Assert.Equal(backup1, backups[0]); // Backup 1 should be first (most recent)
        Assert.Equal(backup2, backups[1]); // Backup 2 should be second
        Assert.Equal(backup3, backups[2]); // Backup 3 should be last (oldest)
    }

    [Fact]
    public async Task GetAvailableBackupsAsync_MixedFiles_ReturnsOnlyBackupFiles()
    {
        // Arrange
        var backupDir = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(backupDir);
        
        // Create backup files and other files
        var backup1 = Path.Combine(backupDir, "config.backup.1.json");
        var backup2 = Path.Combine(backupDir, "config.backup.2.json");
        var regularFile = Path.Combine(backupDir, "config.json");
        var otherFile = Path.Combine(backupDir, "other.txt");
        
        await File.WriteAllTextAsync(backup1, "{}");
        await File.WriteAllTextAsync(backup2, "{}");
        await File.WriteAllTextAsync(regularFile, "{}");
        await File.WriteAllTextAsync(otherFile, "content");
        
        // Act
        var backups = await _backupService.GetAvailableBackupsAsync(backupDir);
        
        // Assert
        Assert.Equal(2, backups.Length);
        Assert.Contains(backup1, backups);
        Assert.Contains(backup2, backups);
        Assert.DoesNotContain(regularFile, backups);
        Assert.DoesNotContain(otherFile, backups);
    }

    #endregion

    #region RestoreFromBackupAsync Tests

    [Fact]
    public async Task RestoreFromBackupAsync_NullBackupPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.RestoreFromBackupAsync(null!));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_EmptyBackupPath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.RestoreFromBackupAsync(""));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "nonexistent.backup.1.json");
        
        // Act
        var config = await _backupService.RestoreFromBackupAsync(nonExistentFile);
        
        // Assert
        Assert.Null(config);
        Assert.Contains(_testLogger.Logs, log => log.Contains("WARN") && log.Contains("does not exist"));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        var emptyBackupFile = Path.Combine(_testDirectory, "empty.backup.1.json");
        await File.WriteAllTextAsync(emptyBackupFile, "");
        
        // Act
        var config = await _backupService.RestoreFromBackupAsync(emptyBackupFile);
        
        // Assert
        Assert.Null(config);
        Assert.Contains(_testLogger.Logs, log => log.Contains("WARN") && log.Contains("empty"));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidBackupFile = Path.Combine(_testDirectory, "invalid.backup.1.json");
        await File.WriteAllTextAsync(invalidBackupFile, "{ invalid json content");
        
        // Act
        var config = await _backupService.RestoreFromBackupAsync(invalidBackupFile);
        
        // Assert
        Assert.Null(config);
        Assert.Contains(_testLogger.Logs, log => log.Contains("ERROR") && log.Contains("JSON parsing error"));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_InvalidConfiguration_ReturnsNull()
    {
        // Arrange
        var invalidConfigBackup = Path.Combine(_testDirectory, "invalid_config.backup.1.json");
        var invalidConfig = new
        {
            proximityThreshold = -10, // Invalid value
            pushDistance = -5 // Invalid value
        };
        await File.WriteAllTextAsync(invalidConfigBackup, JsonSerializer.Serialize(invalidConfig));
        
        // Act
        var config = await _backupService.RestoreFromBackupAsync(invalidConfigBackup);
        
        // Assert
        Assert.Null(config);
        Assert.Contains(_testLogger.Logs, log => log.Contains("WARN") && log.Contains("validation errors"));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_ValidBackup_ReturnsConfiguration()
    {
        // Arrange
        var validBackupFile = Path.Combine(_testDirectory, "valid.backup.1.json");
        var expectedConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 80,
            PushDistance = 120,
            EnableCtrlOverride = false
        };
        await File.WriteAllTextAsync(validBackupFile, JsonSerializer.Serialize(expectedConfig, 
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        
        // Act
        var restoredConfig = await _backupService.RestoreFromBackupAsync(validBackupFile);
        
        // Assert
        Assert.NotNull(restoredConfig);
        Assert.Equal(expectedConfig.ProximityThreshold, restoredConfig.ProximityThreshold);
        Assert.Equal(expectedConfig.PushDistance, restoredConfig.PushDistance);
        Assert.Equal(expectedConfig.EnableCtrlOverride, restoredConfig.EnableCtrlOverride);
        
        Assert.Contains(_testLogger.Logs, log => log.Contains("INFO") && log.Contains("Successfully restored"));
    }

    #endregion

    #region CleanupOldBackupsAsync Tests

    [Fact]
    public async Task CleanupOldBackupsAsync_NullDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CleanupOldBackupsAsync(null!));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_EmptyDirectory_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CleanupOldBackupsAsync(""));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_ZeroMaxBackups_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CleanupOldBackupsAsync(_testDirectory, 0));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_NegativeMaxBackups_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _backupService.CleanupOldBackupsAsync(_testDirectory, -1));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_NonExistentDirectory_LogsAndReturns()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_testDirectory, "nonexistent");
        
        // Act
        await _backupService.CleanupOldBackupsAsync(nonExistentDir);
        
        // Assert
        Assert.Contains(_testLogger.Logs, log => log.Contains("DEBUG") && log.Contains("does not exist"));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_FewerBackupsThanLimit_NoCleanup()
    {
        // Arrange
        var backupDir = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(backupDir);
        
        // Create 2 backup files (less than default limit of 3)
        var backup1 = Path.Combine(backupDir, "config.backup.1.json");
        var backup2 = Path.Combine(backupDir, "config.backup.2.json");
        await File.WriteAllTextAsync(backup1, "{}");
        await File.WriteAllTextAsync(backup2, "{}");
        
        // Act
        await _backupService.CleanupOldBackupsAsync(backupDir);
        
        // Assert
        Assert.True(File.Exists(backup1));
        Assert.True(File.Exists(backup2));
        Assert.Contains(_testLogger.Logs, log => log.Contains("DEBUG") && log.Contains("no cleanup needed"));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_MoreBackupsThanLimit_RemovesOldest()
    {
        // Arrange
        var backupDir = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(backupDir);
        
        // Create 5 backup files (more than default limit of 3)
        var backupFiles = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            var backupFile = Path.Combine(backupDir, $"config.backup.{i}.json");
            await File.WriteAllTextAsync(backupFile, $"{{\"backupNumber\": {i}}}");
            backupFiles.Add(backupFile);
            await Task.Delay(10); // Ensure different timestamps
        }
        
        // Act
        await _backupService.CleanupOldBackupsAsync(backupDir, 3);
        
        // Assert
        // First 3 backups should remain (backup.1, backup.2, backup.3)
        Assert.True(File.Exists(backupFiles[0])); // backup.1
        Assert.True(File.Exists(backupFiles[1])); // backup.2
        Assert.True(File.Exists(backupFiles[2])); // backup.3
        
        // Last 2 backups should be deleted (backup.4, backup.5)
        Assert.False(File.Exists(backupFiles[3])); // backup.4
        Assert.False(File.Exists(backupFiles[4])); // backup.5
        
        Assert.Contains(_testLogger.Logs, log => log.Contains("INFO") && log.Contains("Cleaned up") && log.Contains("old backups") && log.Contains("retained"));
    }

    [Fact]
    public async Task CleanupOldBackupsAsync_CustomMaxBackups_RespectsLimit()
    {
        // Arrange
        var backupDir = Path.Combine(_testDirectory, "backups");
        Directory.CreateDirectory(backupDir);
        
        // Create 4 backup files
        var backupFiles = new List<string>();
        for (int i = 1; i <= 4; i++)
        {
            var backupFile = Path.Combine(backupDir, $"config.backup.{i}.json");
            await File.WriteAllTextAsync(backupFile, $"{{\"backupNumber\": {i}}}");
            backupFiles.Add(backupFile);
            await Task.Delay(10);
        }
        
        // Act - Set limit to 2
        await _backupService.CleanupOldBackupsAsync(backupDir, 2);
        
        // Assert
        // First 2 backups should remain
        Assert.True(File.Exists(backupFiles[0])); // backup.1
        Assert.True(File.Exists(backupFiles[1])); // backup.2
        
        // Last 2 backups should be deleted
        Assert.False(File.Exists(backupFiles[2])); // backup.3
        Assert.False(File.Exists(backupFiles[3])); // backup.4
        
        Assert.Contains(_testLogger.Logs, log => log.Contains("INFO") && log.Contains("Cleaned up") && log.Contains("old backups") && log.Contains("retained"));
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullBackupWorkflow_CreateRestoreCleanup_WorksCorrectly()
    {
        // Arrange
        var sourceFile = CreateTestConfigFile();
        var backupDir = Path.Combine(_testDirectory, "backups");
        
        // Act 1: Create backup
        await _backupService.CreateBackupAsync(sourceFile, backupDir);
        
        // Act 2: Get available backups
        var backups = await _backupService.GetAvailableBackupsAsync(backupDir);
        
        // Act 3: Restore from backup
        var restoredConfig = await _backupService.RestoreFromBackupAsync(backups[0]);
        
        // Act 4: Create more backups to test cleanup
        for (int i = 0; i < 4; i++)
        {
            await File.WriteAllTextAsync(sourceFile, JsonSerializer.Serialize(new CursorPhobiaConfiguration
            {
                ProximityThreshold = 100 + i * 10
            }, new JsonSerializerOptions { WriteIndented = true }));
            
            await _backupService.CreateBackupAsync(sourceFile, backupDir);
            await Task.Delay(10);
        }
        
        // Act 5: Cleanup old backups
        await _backupService.CleanupOldBackupsAsync(backupDir, 3);
        
        // Assert
        Assert.Single(backups); // Initially had 1 backup
        Assert.NotNull(restoredConfig);
        Assert.Equal(_testConfiguration.ProximityThreshold, restoredConfig.ProximityThreshold);
        
        var finalBackups = await _backupService.GetAvailableBackupsAsync(backupDir);
        Assert.Equal(3, finalBackups.Length); // Should have exactly 3 after cleanup
    }

    #endregion

    #region Helper Methods

    private string CreateTestConfigFile(CursorPhobiaConfiguration? config = null)
    {
        config ??= _testConfiguration;
        var filePath = Path.Combine(_testDirectory, $"test_config_{Guid.NewGuid()}.json");
        
        var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true, 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        File.WriteAllText(filePath, jsonContent);
        return filePath;
    }

    #endregion
}