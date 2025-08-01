using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Microsoft.Win32;
using Xunit;

namespace CursorPhobia.Tests;

public class StartupManagerTests : IDisposable
{
    private const string TEST_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "CursorPhobia";
    
    private StartupManager _startupManager;
    private TestLogger _logger;

    public StartupManagerTests()
    {
        _logger = new TestLogger();
        _startupManager = new StartupManager(_logger);
        
        // Clean up any existing test entries
        CleanupRegistryEntry();
    }

    public void Dispose()
    {
        CleanupRegistryEntry();
    }

    [Fact]
    public async Task IsAutoStartEnabledAsync_WhenNoRegistryEntry_ReturnsFalse()
    {
        // Arrange - ensure no registry entry exists
        CleanupRegistryEntry();
        
        // Act
        var result = await _startupManager.IsAutoStartEnabledAsync();
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnableAutoStartAsync_CreatesRegistryEntry_ReturnsTrue()
    {
        // Act
        var result = await _startupManager.EnableAutoStartAsync();
        
        // Assert
        Assert.True(result);
        
        // Verify registry entry was created
        using var key = Registry.CurrentUser.OpenSubKey(TEST_REGISTRY_KEY, false);
        var value = key?.GetValue(APP_NAME)?.ToString();
        
        Assert.NotNull(value);
        Assert.Contains("CursorPhobia", value);
        Assert.Contains("--tray", value);
    }

    [Fact]
    public async Task DisableAutoStartAsync_RemovesRegistryEntry_ReturnsTrue()
    {
        // Arrange - first enable auto-start
        await _startupManager.EnableAutoStartAsync();
        var wasEnabled = await _startupManager.IsAutoStartEnabledAsync();
        Assert.True(wasEnabled);
        
        // Act
        var result = await _startupManager.DisableAutoStartAsync();
        
        // Assert
        Assert.True(result);
        
        // Verify registry entry was removed
        var isStillEnabled = await _startupManager.IsAutoStartEnabledAsync();
        Assert.False(isStillEnabled);
    }

    [Fact]
    public async Task GetAutoStartCommandAsync_WhenEnabled_ReturnsCommand()
    {
        // Arrange
        await _startupManager.EnableAutoStartAsync();
        
        // Act
        var command = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.NotNull(command);
        Assert.Contains("CursorPhobia", command);
        Assert.Contains("--tray", command);
    }

    [Fact]
    public async Task GetAutoStartCommandAsync_ReturnsValidCommand()
    {
        // Act
        var command = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.NotNull(command);
        Assert.NotEmpty(command);
        Assert.Contains("--tray", command);
    }

    [Fact]
    public async Task ValidatePermissionsAsync_WhenProperlyConfigured_ReturnsTrue()
    {
        // Act
        var isValid = await _startupManager.ValidatePermissionsAsync();
        
        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task EnableAutoStartAsync_WhenAlreadyEnabled_UpdatesEntry()
    {
        // Arrange - enable auto-start first
        await _startupManager.EnableAutoStartAsync();
        var command1 = await _startupManager.GetAutoStartCommandAsync();
        
        // Act - enable again
        var result = await _startupManager.EnableAutoStartAsync();
        var command2 = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.True(result);
        Assert.NotNull(command1);
        Assert.NotNull(command2);
        Assert.Equal(command1, command2); // Should be the same command
    }

    [Fact]
    public async Task DisableAutoStartAsync_WhenAlreadyDisabled_ReturnsTrue()
    {
        // Arrange - ensure auto-start is disabled
        await _startupManager.DisableAutoStartAsync();
        
        // Act - disable again
        var result = await _startupManager.DisableAutoStartAsync();
        
        // Assert
        Assert.True(result); // Should still succeed
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StartupManager(null!));
    }

    /// <summary>
    /// Helper method to clean up any test registry entries
    /// </summary>
    private void CleanupRegistryEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TEST_REGISTRY_KEY, true);
            if (key?.GetValue(APP_NAME) != null)
            {
                key.DeleteValue(APP_NAME, false);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}