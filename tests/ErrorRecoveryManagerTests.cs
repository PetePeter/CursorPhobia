using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for ErrorRecoveryManager - Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public class ErrorRecoveryManagerTests : IDisposable
{
    private readonly TestLogger _logger;
    private readonly ErrorRecoveryManager _errorRecoveryManager;
    
    public ErrorRecoveryManagerTests()
    {
        _logger = new TestLogger();
        _errorRecoveryManager = new ErrorRecoveryManager(_logger);
    }
    
    public void Dispose()
    {
        _errorRecoveryManager?.Dispose();
    }
    
    [Fact]
    public async Task InitializeAsync_ShouldReturnTrue_WhenCalledFirstTime()
    {
        // Act
        var result = await _errorRecoveryManager.InitializeAsync();
        
        // Assert
        Assert.True(result);
        Assert.True(_errorRecoveryManager.IsInitialized);
    }
    
    [Fact]
    public async Task InitializeAsync_ShouldReturnTrue_WhenCalledMultipleTimes()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.InitializeAsync();
        
        // Assert
        Assert.True(result);
        Assert.True(_errorRecoveryManager.IsInitialized);
    }
    
    [Fact]
    public async Task RegisterComponentAsync_ShouldReturnTrue_WithValidParameters()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Assert
        Assert.True(result);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.True(components.Contains("TestComponent"));
    }
    
    [Fact]
    public async Task RegisterComponentAsync_ShouldReturnFalse_WithNullComponentName()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync(null!, recoveryAction);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task RegisterComponentAsync_ShouldReturnFalse_WithNullRecoveryAction()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.RegisterComponentAsync("TestComponent", null!);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task UnregisterComponentAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.UnregisterComponentAsync("TestComponent");
        
        // Assert
        Assert.True(result);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.False(components.Contains("TestComponent"));
    }
    
    [Fact]
    public async Task UnregisterComponentAsync_ShouldReturnFalse_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.UnregisterComponentAsync("NonExistentComponent");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
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
        Assert.True(result.Success);
        Assert.True(recoveryTriggered);
        Assert.Equal(1, result.AttemptsCount);
    }
    
    [Fact]
    public async Task ReportFailureAsync_ShouldReturnFailure_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var exception = new InvalidOperationException("Test failure");
        
        // Act
        var result = await _errorRecoveryManager.ReportFailureAsync("NonExistentComponent", exception);
        
        // Assert
        Assert.False(result.Success);
        Assert.True(result.ErrorMessage?.Contains("not found") == true);
    }
    
    [Fact]
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
        Assert.True(result.Success);
        Assert.Equal(2, result.AttemptsCount);
        Assert.Equal(2, attemptCount);
    }
    
    [Fact]
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
        Assert.False(result.Success);
        Assert.Equal(2, result.AttemptsCount);
        Assert.Equal(2, attemptCount);
    }
    
    [Fact]
    public async Task ReportSuccessAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.ReportSuccessAsync("TestComponent");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task ReportSuccessAsync_ShouldReturnFalse_WithUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.ReportSuccessAsync("NonExistentComponent");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task GetCircuitBreakerState_ShouldReturnClosed_ForNewComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var state = _errorRecoveryManager.GetCircuitBreakerState("TestComponent");
        
        // Assert
        Assert.Equal(CircuitBreakerState.Closed, state);
    }
    
    [Fact]
    public async Task GetCircuitBreakerState_ShouldReturnDisabled_ForUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var state = _errorRecoveryManager.GetCircuitBreakerState("NonExistentComponent");
        
        // Assert
        Assert.Equal(CircuitBreakerState.Disabled, state);
    }
    
    [Fact]
    public async Task GetRecoveryStatistics_ShouldReturnNull_ForUnregisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var stats = _errorRecoveryManager.GetRecoveryStatistics("NonExistentComponent");
        
        // Assert
        Assert.Null(stats);
    }
    
    [Fact]
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
        Assert.NotNull(stats);
        Assert.Equal("TestComponent", stats.ComponentName);
        Assert.Equal(2, stats.TotalFailures);
        Assert.Equal(2, stats.SuccessfulRecoveries);
        Assert.Equal(0, stats.FailedRecoveries);
    }
    
    [Fact]
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
        Assert.True(result.Success);
        Assert.True(recoveryTriggered);
        Assert.Equal("Manual test", result.Context);
    }
    
    [Fact]
    public async Task ResetCircuitBreakerAsync_ShouldReturnTrue_WithRegisteredComponent()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.ResetCircuitBreakerAsync("TestComponent");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task PerformHealthCheckAsync_ShouldReturnHealthyResult_WithNoComponents()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var result = await _errorRecoveryManager.PerformHealthCheckAsync();
        
        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal(0, result.TotalComponents);
    }
    
    [Fact]
    public async Task PerformHealthCheckAsync_ShouldReturnHealthyResult_WithHealthyComponents()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        var result = await _errorRecoveryManager.PerformHealthCheckAsync();
        
        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal(1, result.TotalComponents);
        Assert.Equal(1, result.HealthyComponents);
    }
    
    [Fact]
    public async Task GetRegisteredComponents_ShouldReturnEmptyList_Initially()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        
        // Act
        var components = _errorRecoveryManager.GetRegisteredComponents();
        
        // Assert
        Assert.NotNull(components);
        Assert.Equal(0, components.Count);
    }
    
    [Fact]
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
        Assert.Equal(2, components.Count);
        Assert.True(components.Contains("Component1"));
        Assert.True(components.Contains("Component2"));
    }
    
    [Fact]
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
        Assert.True(eventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal("TestComponent", eventArgs.ComponentName);
        Assert.Equal(CircuitBreakerState.Open, eventArgs.NewState);
    }
    
    [Fact]
    public async Task ShutdownAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _errorRecoveryManager.InitializeAsync();
        var recoveryAction = new Func<Task<bool>>(() => Task.FromResult(true));
        await _errorRecoveryManager.RegisterComponentAsync("TestComponent", recoveryAction);
        
        // Act
        await _errorRecoveryManager.ShutdownAsync();
        
        // Assert
        Assert.False(_errorRecoveryManager.IsInitialized);
        var components = _errorRecoveryManager.GetRegisteredComponents();
        Assert.Equal(0, components.Count);
    }
    
    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var errorRecoveryManager = new ErrorRecoveryManager(_logger);
        
        // Act & Assert
        // XUnit doesn't have DoesNotThrow - just call the method
        errorRecoveryManager.Dispose();
        
        // Multiple disposes should not throw
        errorRecoveryManager.Dispose();
    }
}