using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Microsoft.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CursorPhobia.Tests;

[TestClass]
public class StartupManagerTests
{
    private const string TEST_REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "CursorPhobia";
    
    private StartupManager _startupManager;
    private TestLogger _logger;

    [TestInitialize]
    public void Initialize()
    {
        _logger = new TestLogger();
        _startupManager = new StartupManager(_logger);
        
        // Clean up any existing test entries
        CleanupRegistryEntry();
    }

    [TestCleanup]
    public void Cleanup()
    {
        CleanupRegistryEntry();
        _startupManager?.Dispose();
    }

    [TestMethod]
    public async Task IsAutoStartEnabledAsync_WhenNoRegistryEntry_ReturnsFalse()
    {
        // Arrange - ensure no registry entry exists
        CleanupRegistryEntry();
        
        // Act
        var result = await _startupManager.IsAutoStartEnabledAsync();
        
        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task EnableAutoStartAsync_CreatesRegistryEntry_ReturnsTrue()
    {
        // Act
        var result = await _startupManager.EnableAutoStartAsync();
        
        // Assert
        Assert.IsTrue(result);
        
        // Verify registry entry was created
        using var key = Registry.CurrentUser.OpenSubKey(TEST_REGISTRY_KEY, false);
        var value = key?.GetValue(APP_NAME)?.ToString();
        
        Assert.IsNotNull(value);
        Assert.IsTrue(value.Contains("CursorPhobia"));
        Assert.IsTrue(value.Contains("--minimized"));
    }

    [TestMethod]
    public async Task DisableAutoStartAsync_RemovesRegistryEntry_ReturnsTrue()
    {
        // Arrange - first enable auto-start
        await _startupManager.EnableAutoStartAsync();
        var wasEnabled = await _startupManager.IsAutoStartEnabledAsync();
        Assert.IsTrue(wasEnabled);
        
        // Act
        var result = await _startupManager.DisableAutoStartAsync();
        
        // Assert
        Assert.IsTrue(result);
        
        // Verify registry entry was removed
        var isStillEnabled = await _startupManager.IsAutoStartEnabledAsync();
        Assert.IsFalse(isStillEnabled);
    }

    [TestMethod]
    public async Task GetAutoStartCommandAsync_WhenEnabled_ReturnsCommand()
    {
        // Arrange
        await _startupManager.EnableAutoStartAsync();
        
        // Act
        var command = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.IsNotNull(command);
        Assert.IsTrue(command.Contains("CursorPhobia"));
        Assert.IsTrue(command.Contains("--minimized"));
    }

    [TestMethod]
    public async Task GetAutoStartCommandAsync_WhenDisabled_ReturnsNull()
    {
        // Arrange - ensure auto-start is disabled
        await _startupManager.DisableAutoStartAsync();
        
        // Act
        var command = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.IsNull(command);
    }

    [TestMethod]
    public async Task ValidateAutoStartConfigurationAsync_WhenProperlyConfigured_ReturnsTrue()
    {
        // Arrange
        await _startupManager.EnableAutoStartAsync();
        
        // Act
        var isValid = await _startupManager.ValidateAutoStartConfigurationAsync();
        
        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    public async Task ValidateAutoStartConfigurationAsync_WhenNotConfigured_ReturnsFalse()
    {
        // Arrange - ensure auto-start is disabled
        await _startupManager.DisableAutoStartAsync();
        
        // Act
        var isValid = await _startupManager.ValidateAutoStartConfigurationAsync();
        
        // Assert
        Assert.IsFalse(isValid);
    }

    [TestMethod]
    public async Task EnableAutoStartAsync_WhenAlreadyEnabled_UpdatesEntry()
    {
        // Arrange - enable auto-start first
        await _startupManager.EnableAutoStartAsync();
        var command1 = await _startupManager.GetAutoStartCommandAsync();
        
        // Act - enable again
        var result = await _startupManager.EnableAutoStartAsync();
        var command2 = await _startupManager.GetAutoStartCommandAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsNotNull(command1);
        Assert.IsNotNull(command2);
        Assert.AreEqual(command1, command2); // Should be the same command
    }

    [TestMethod]
    public async Task DisableAutoStartAsync_WhenAlreadyDisabled_ReturnsTrue()
    {
        // Arrange - ensure auto-start is disabled
        await _startupManager.DisableAutoStartAsync();
        
        // Act - disable again
        var result = await _startupManager.DisableAutoStartAsync();
        
        // Assert
        Assert.IsTrue(result); // Should still succeed
    }

    [TestMethod]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new StartupManager(null!));
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