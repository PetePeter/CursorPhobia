using Microsoft.VisualStudio.TestTools.UnitTesting;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System;
using System.Threading.Tasks;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for ErrorRecoveryManager - Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
[TestClass]
public class ErrorRecoveryManagerTests
{
    private TestLogger _logger = null!;
    private ErrorRecoveryManager _errorRecoveryManager = null!;
    
    [TestInitialize]
    public void TestInitialize()
    {
        _logger = new TestLogger();
        _errorRecoveryManager = new ErrorRecoveryManager(_logger);
    }
    
    [TestCleanup]
    public void TestCleanup()
    {
        _errorRecoveryManager?.Dispose();
    }
    
    [TestMethod]
    public async Task InitializeAsync_ShouldReturnTrue_WhenCalledFirstTime()
    {
        // Act
        var result = await _errorRecoveryManager.InitializeAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_errorRecoveryManager.IsInitialized);
    }
    
    [TestMethod]
    public async Task InitializeAsync_ShouldReturnTrue_WhenCalledMultipleTimes()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.InitializeAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_errorRecoveryManager.IsInitialized);
    }
    
    [TestMethod]
    public async Task RegisterComponentAsync_ShouldReturnTrue_WithValidParameters()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Assert
        Assert.IsTrue(result);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.IsTrue(components.Contains("TestComponent"));
    }
    
    [TestMethod]
    public async Task RegisterComponentAsync_ShouldReturnFalse_WithNullComponentName()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync(null!, recoveryAction);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task RegisterComponentAsync_ShouldReturnFalse_WithNullRecoveryAction()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync("TestComponent", null!);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task UnregisterComponentAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.UnregisterComponentAsync("TestComponent");
        
        // Assert
        Assert.IsTrue(result);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.IsFalse(components.Contains("TestComponent"));
    }
    
    [TestMethod]
    public async Task UnregisterComponentAsync_ShouldReturnFalse_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.UnregisterComponentAsync("NonExistentComponent");
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task ReportFailureAsync_ShouldTriggerRecovery_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        bool recoveryTriggered = false;
        var recoveryAction = new Func<Task<bool>>(() => 
        {
            recoveryTriggered = true;
            return Task.FromResult(true);
        });
        
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        var exception = new InvalidOperationException("Test failure");
        
        // Act
        var result = await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        
        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(recoveryTriggered);
        Assert.AreEqual(1, result.AttemptsCount);
    }
    
    [TestMethod]
    public async Task ReportFailureAsync_ShouldReturnFailure_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var exception = new InvalidOperationException("Test failure");
        
        // Act
        var result = await _errorRecoveryManager.ReportFailureAsync("NonExistentComponent", exception);
        
        // Assert
        Assert.IsFalse(result.Success);
        Assert.IsTrue(result.ErrorMessage?.Contains("not found") == true);
    }
    
    [TestMethod]
    public async Task ReportFailureAsync_ShouldRetryOnFailure()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        int attemptCount = 0;
        var recoveryAction = new Func<Task<bool>>(() => 
        {
            attemptCount++;
            return Task.FromResult(attemptCount >= 2); // Succeed on second attempt
        });
        
        var options = new RecoveryOptions { MaxRetryAttempts = 3 };
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction, options);
        var exception = new InvalidOperationException("Test failure");
        
        // Act
        var result = await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        
        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.AttemptsCount);
        Assert.AreEqual(2, attemptCount);
    }
    
    [TestMethod]
    public async Task ReportFailureAsync_ShouldFailAfterMaxAttempts()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        int attemptCount = 0;
        var recoveryAction = new Func<Task<bool>>(() => 
        {
            attemptCount++;
            return Task.FromResult(false); // Always fail
        });
        
        var options = new RecoveryOptions { MaxRetryAttempts = 2 };
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction, options);
        var exception = new InvalidOperationException("Test failure");
        
        // Act
        var result = await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        
        // Assert
        Assert.IsFalse(result.Success);
        Assert.AreEqual(2, result.AttemptsCount);
        Assert.AreEqual(2, attemptCount);
    }
    
    [TestMethod]
    public async Task ReportSuccessAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.ReportSuccessAsync("TestComponent");
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [TestMethod]
    public async Task ReportSuccessAsync_ShouldReturnFalse_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.ReportSuccessAsync("NonExistentComponent");
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task GetCircuitBreakerState_ShouldReturnClosed_ForNewComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var state = _errorRecoveryManager.GetCircuitBreakerState("TestComponent");
        
        // Assert
        Assert.AreEqual(CircuitBreakerState.Closed, state);
    }
    
    [TestMethod]
    public async Task GetCircuitBreakerState_ShouldReturnDisabled_ForUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var state = _errorRecoveryManager.GetCircuitBreakerState("NonExistentComponent");
        
        // Assert
        Assert.AreEqual(CircuitBreakerState.Disabled, state);
    }
    
    [TestMethod]
    public async Task GetRecoveryStatistics_ShouldReturnNull_ForUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var stats = _errorRecoveryManager.GetRecoveryStatistics("NonExistentComponent");
        
        // Assert
        Assert.IsNull(stats);
    }
    
    [TestMethod]
    public async Task GetRecoveryStatistics_ShouldReturnValidStats_ForRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Simulate some failures and recoveries
        var exception = new InvalidOperationException("Test failure");
        await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        
        // Act
        var stats = _errorRecoveryManager.GetRecoveryStatistics("TestComponent");
        
        // Assert
        Assert.IsNotNull(stats);
        Assert.AreEqual("TestComponent", stats.ComponentName);
        Assert.AreEqual(2, stats.TotalFailures);
        Assert.AreEqual(2, stats.SuccessfulRecoveries);
        Assert.AreEqual(0, stats.FailedRecoveries);
    }
    
    [TestMethod]
    public async Task TriggerRecoveryAsync_ShouldReturnSuccess_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        bool recoveryTriggered = false;
        var recoveryAction = new Func<Task<bool>>(() => 
        {
            recoveryTriggered = true;
            return Task.FromResult(true);
        });
        
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.TriggerRecoveryAsync("TestComponent", "Manual test");
        
        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(recoveryTriggered);
        Assert.AreEqual("Manual test", result.Context);
    }
    
    [TestMethod]
    public async Task ResetCircuitBreakerAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.ResetCircuitBreakerAsync("TestComponent");
        
        // Assert
        Assert.IsTrue(result);
    }
    
    [TestMethod]
    public async Task PerformHealthCheckAsync_ShouldReturnHealthyResult_WithNoComponents()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.PerformHealthCheckAsync();
        
        // Assert
        Assert.IsTrue(result.IsHealthy);
        Assert.AreEqual(0, result.TotalComponents);
    }
    
    [TestMethod]
    public async Task PerformHealthCheckAsync_ShouldReturnHealthyResult_WithHealthyComponents()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.PerformHealthCheckAsync();
        
        // Assert
        Assert.IsTrue(result.IsHealthy);
        Assert.AreEqual(1, result.TotalComponents);
        Assert.AreEqual(1, result.HealthyComponents);
    }
    
    [TestMethod]
    public async Task GetRegisteredComponents_ShouldReturnEmptyList_Initially()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var components = _errorRecoveryManager.GetRegisteredComponents();
        
        // Assert
        Assert.IsNotNull(components);
        Assert.AreEqual(0, components.Count);
    }
    
    [TestMethod]
    public async Task GetRegisteredComponents_ShouldReturnRegisteredComponents()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("Component1", recoveryAction);
        await _errorRecoveryManager.RegisterComponentAsync("Component2", recoveryAction);
        
        // Act
        var components = _errorRecoveryManager.GetRegisteredComponents();
        
        // Assert
        Assert.AreEqual(2, components.Count);
        Assert.IsTrue(components.Contains("Component1"));
        Assert.IsTrue(components.Contains("Component2"));
    }
    
    [TestMethod]
    public async Task CircuitBreakerStateChanged_EventShouldFire_WhenStateChanges()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(false)); // Always fail
        var options = new RecoveryOptions { FailureThreshold = 2, EnableCircuitBreaker = true };
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction, options);
        
        bool eventFired = false;
        CircuitBreakerStateChangedEventArgs? eventArgs = null;
        _errorRecoveryManager.CircuitBreakerStateChanged += (sender, args) =>
        {
            eventFired = true;
            eventArgs = args;
        };
        
        var exception = new InvalidOperationException("Test failure");
        
        // Act - Trigger enough failures to open circuit breaker
        await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        await _errorRecoveryManager.ReportFailureAsync("TestComponent", exception);
        
        // Assert
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(eventArgs);
        Assert.AreEqual("TestComponent", eventArgs.ComponentName);
        Assert.AreEqual(CircuitBreakerState.Open, eventArgs.NewState);
    }
    
    [TestMethod]
    public async Task ShutdownAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        await _errorRecoveryManager.ShutdownAsync();
        
        // Assert
        Assert.IsFalse(_errorRecoveryManager.IsInitialized);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.AreEqual(0, components.Count);
    }
    
    [TestMethod]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var errorRecoveryManager = new ErrorRecoveryManager(_logger);
        
        // Act & Assert
        Assert.DoesNotThrow(() => errorRecoveryManager.Dispose());
        
        // Multiple disposes should not throw
        Assert.DoesNotThrow(() => errorRecoveryManager.Dispose());
    }
}