using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for ServiceHealthMonitor - Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public class ServiceHealthMonitorTests : IDisposable
{
    private readonly TestLogger _logger;
    private readonly ServiceHealthMonitor _healthMonitor;
    
    public ServiceHealthMonitorTests()
    {
        _logger = new TestLogger();
        _healthMonitor = new ServiceHealthMonitor(_logger);
    }
    
    public void Dispose()
    {
        _healthMonitor?.Dispose();
    }
    
    [Fact]
    public async Task StartAsync_ShouldReturnTrue_WhenCallingFirstTime()
    {
        // Act
        var result = await _healthMonitor.StartAsync();
        
        // Assert
        Assert.True(result);
        Assert.True(_healthMonitor.IsRunning);
    }
    
    [Fact]
    public async Task StartAsync_ShouldReturnTrue_WhenCallingMultipleTimes()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        
        // Act
        var result = await _healthMonitor.StartAsync();
        
        // Assert
        Assert.True(result);
        Assert.True(_healthMonitor.IsRunning);
    }
    
    [Fact]
    public async Task StopAsync_ShouldReturnTrue_WhenRunning()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        
        // Act
        var result = await _healthMonitor.StopAsync();
        
        // Assert
        Assert.True(result);
        Assert.False(_healthMonitor.IsRunning);
    }
    
    [Fact]
    public async Task StopAsync_ShouldReturnTrue_WhenNotRunning()
    {
        // Act
        var result = await _healthMonitor.StopAsync();
        
        // Assert
        Assert.True(result);
        Assert.False(_healthMonitor.IsRunning);
    }
    
    [Fact]
    public async Task RegisterServiceAsync_ShouldReturnTrue_WithValidParameters()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy, Description = "Test service is healthy" })
        );
        
        // Act
        var result = await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Assert
        Assert.True(result);
        var services = _healthMonitor.GetRegisteredServices();
        Assert.True(services.Contains("TestService"));
    }
    
    [Fact]
    public async Task RegisterServiceAsync_ShouldReturnFalse_WithNullServiceName()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        
        // Act
        var result = await _healthMonitor.RegisterServiceAsync(null!, healthCheck);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task RegisterServiceAsync_ShouldReturnFalse_WithNullHealthCheck()
    {
        // Act
        var result = await _healthMonitor.RegisterServiceAsync("TestService", null!);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task UnregisterServiceAsync_ShouldReturnTrue_WithRegisteredService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Act
        var result = await _healthMonitor.UnregisterServiceAsync("TestService");
        
        // Assert
        Assert.True(result);
        var services = _healthMonitor.GetRegisteredServices();
        Assert.False(services.Contains("TestService"));
    }
    
    [Fact]
    public async Task UnregisterServiceAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.UnregisterServiceAsync("NonExistentService");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task GetServiceHealth_ShouldReturnNull_ForUnregisteredService()
    {
        // Act
        var health = _healthMonitor.GetServiceHealth("NonExistentService");
        
        // Assert
        Assert.Null(health);
    }
    
    [Fact]
    public async Task GetServiceHealth_ShouldReturnStatus_ForRegisteredService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Act
        var health = _healthMonitor.GetServiceHealth("TestService");
        
        // Assert
        Assert.NotNull(health);
        // Initial status should be Unknown until first health check
        Assert.Equal(ServiceHealthStatus.Unknown, health.Value);
    }
    
    [Fact]
    public async Task GetServiceHealthInfo_ShouldReturnNull_ForUnregisteredService()
    {
        // Act
        var healthInfo = _healthMonitor.GetServiceHealthInfo("NonExistentService");
        
        // Assert
        Assert.Null(healthInfo);
    }
    
    [Fact]
    public async Task GetServiceHealthInfo_ShouldReturnInfo_ForRegisteredService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Act
        var healthInfo = _healthMonitor.GetServiceHealthInfo("TestService");
        
        // Assert
        Assert.NotNull(healthInfo);
        Assert.Equal("TestService", healthInfo.ServiceName);
        Assert.NotNull(healthInfo.Options);
    }
    
    [Fact]
    public async Task CheckServiceHealthAsync_ShouldReturnHealthyResult_ForHealthyService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult 
            { 
                Status = ServiceHealthStatus.Healthy,
                Description = "Service is healthy",
                ResponseTime = TimeSpan.FromMilliseconds(50)
            })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Act
        var result = await _healthMonitor.CheckServiceHealthAsync("TestService");
        
        // Assert
        Assert.Equal(ServiceHealthStatus.Healthy, result.Status);
        Assert.Equal("Service is healthy", result.Description);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
    }
    
    [Fact]
    public async Task CheckServiceHealthAsync_ShouldReturnUnknownResult_ForUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.CheckServiceHealthAsync("NonExistentService");
        
        // Assert
        Assert.Equal(ServiceHealthStatus.Unknown, result.Status);
        Assert.True(result.Description.Contains("not found") || result.Description.Contains("not registered"));
    }
    
    [Fact]
    public async Task CheckAllServicesHealthAsync_ShouldReturnEmptyResult_WithNoServices()
    {
        // Act
        var result = await _healthMonitor.CheckAllServicesHealthAsync();
        
        // Assert
        Assert.Equal(SystemHealthStatus.Unknown, result.SystemStatus);
        Assert.Equal(0, result.TotalServices);
    }
    
    [Fact]
    public async Task CheckAllServicesHealthAsync_ShouldReturnHealthyResult_WithHealthyServices()
    {
        // Arrange
        var healthyCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy, Description = "Healthy" })
        );
        await _healthMonitor.RegisterServiceAsync("Service1", healthyCheck);
        await _healthMonitor.RegisterServiceAsync("Service2", healthyCheck);
        
        // Act
        var result = await _healthMonitor.CheckAllServicesHealthAsync();
        
        // Assert
        Assert.Equal(SystemHealthStatus.Healthy, result.SystemStatus);
        Assert.Equal(2, result.TotalServices);
        Assert.Equal(2, result.HealthyServices);
        Assert.Equal(0, result.UnhealthyServices);
    }
    
    [Fact]
    public async Task CheckAllServicesHealthAsync_ShouldReturnUnhealthyResult_WithUnhealthyServices()
    {
        // Arrange
        var healthyCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        var unhealthyCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Unhealthy })
        );
        
        await _healthMonitor.RegisterServiceAsync("HealthyService", healthyCheck);
        await _healthMonitor.RegisterServiceAsync("UnhealthyService", unhealthyCheck);
        
        // Act
        var result = await _healthMonitor.CheckAllServicesHealthAsync();
        
        // Assert
        Assert.Equal(SystemHealthStatus.Unhealthy, result.SystemStatus);
        Assert.Equal(2, result.TotalServices);
        Assert.Equal(1, result.HealthyServices);
        Assert.Equal(1, result.UnhealthyServices);
    }
    
    [Fact]
    public async Task CheckAllServicesHealthAsync_ShouldReturnCriticalResult_WithCriticalServices()
    {
        // Arrange
        var criticalCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Critical })
        );
        await _healthMonitor.RegisterServiceAsync("CriticalService", criticalCheck);
        
        // Act
        var result = await _healthMonitor.CheckAllServicesHealthAsync();
        
        // Assert
        Assert.Equal(SystemHealthStatus.Unhealthy, result.SystemStatus);
        Assert.Equal(1, result.CriticalServices);
    }
    
    [Fact]
    public async Task GetAllServiceHealth_ShouldReturnEmptyDictionary_WithNoServices()
    {
        // Act
        var allHealth = _healthMonitor.GetAllServiceHealth();
        
        // Assert
        Assert.NotNull(allHealth);
        Assert.Equal(0, allHealth.Count);
    }
    
    [Fact]
    public async Task GetAllServiceHealth_ShouldReturnAllServices_WithRegisteredServices()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("Service1", healthCheck);
        await _healthMonitor.RegisterServiceAsync("Service2", healthCheck);
        
        // Act
        var allHealth = _healthMonitor.GetAllServiceHealth();
        
        // Assert
        Assert.Equal(2, allHealth.Count);
        Assert.True(allHealth.ContainsKey("Service1"));
        Assert.True(allHealth.ContainsKey("Service2"));
    }
    
    [Fact]
    public async Task GetRegisteredServices_ShouldReturnEmptyList_Initially()
    {
        // Act
        var services = _healthMonitor.GetRegisteredServices();
        
        // Assert
        Assert.NotNull(services);
        Assert.Equal(0, services.Count);
    }
    
    [Fact]
    public async Task GetRegisteredServices_ShouldReturnRegisteredServices()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("Service1", healthCheck);
        await _healthMonitor.RegisterServiceAsync("Service2", healthCheck);
        
        // Act
        var services = _healthMonitor.GetRegisteredServices();
        
        // Assert
        Assert.Equal(2, services.Count);
        Assert.True(services.Contains("Service1"));
        Assert.True(services.Contains("Service2"));
    }
    
    [Fact]
    public async Task GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        await _healthMonitor.StartAsync();
        
        // Act
        var stats = _healthMonitor.GetStatistics();
        
        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalServices);
        Assert.True(stats.MonitoringUptime >= TimeSpan.Zero);
    }
    
    [Fact]
    public async Task ResetServiceStatisticsAsync_ShouldReturnTrue_WithRegisteredService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Act
        var result = await _healthMonitor.ResetServiceStatisticsAsync("TestService");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task ResetServiceStatisticsAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.ResetServiceStatisticsAsync("NonExistentService");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task UpdateServiceMonitoringAsync_ShouldReturnTrue_WithRegisteredService()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        var newOptions = new ServiceMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMinutes(5),
            IsCritical = true
        };
        
        // Act
        var result = await _healthMonitor.UpdateServiceMonitoringAsync("TestService", newOptions);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task UpdateServiceMonitoringAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Arrange
        var newOptions = new ServiceMonitoringOptions();
        
        // Act
        var result = await _healthMonitor.UpdateServiceMonitoringAsync("NonExistentService", newOptions);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task ServiceHealthChanged_EventShouldFire_WhenHealthChanges()
    {
        // Arrange
        bool isHealthy = true;
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult 
            { 
                Status = isHealthy ? ServiceHealthStatus.Healthy : ServiceHealthStatus.Unhealthy,
                Description = isHealthy ? "Healthy" : "Unhealthy"
            })
        );
        
        var options = new ServiceMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMilliseconds(100),
            FailureThreshold = 1,
            RecoveryThreshold = 1
        };
        
        await _healthMonitor.RegisterServiceAsync("TestService", healthCheck, options);
        
        bool eventFired = false;
        ServiceHealthChangedEventArgs? eventArgs = null;
        _healthMonitor.ServiceHealthChanged += (sender, args) =>
        {
            eventFired = true;
            eventArgs = args;
        };
        
        await _healthMonitor.StartAsync();
        
        // Wait for initial health check
        await Task.Delay(200);
        
        // Change health status
        isHealthy = false;
        
        // Act - Wait for health check to detect change
        await Task.Delay(300);
        
        // Assert
        Assert.True(eventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal("TestService", eventArgs.ServiceName);
    }
    
    [Fact]
    public async Task SystemHealthChanged_EventShouldFire_WhenSystemHealthChanges()
    {
        // Arrange
        bool isHealthy = true;
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult 
            { 
                Status = isHealthy ? ServiceHealthStatus.Healthy : ServiceHealthStatus.Critical
            })
        );
        
        var options = new ServiceMonitoringOptions
        {
            CheckInterval = TimeSpan.FromMilliseconds(100),
            IsCritical = true,
            FailureThreshold = 1
        };
        
        await _healthMonitor.RegisterServiceAsync("CriticalService", healthCheck, options);
        
        bool eventFired = false;
        SystemHealthChangedEventArgs? eventArgs = null;
        _healthMonitor.SystemHealthChanged += (sender, args) =>
        {
            eventFired = true;
            eventArgs = args;
        };
        
        await _healthMonitor.StartAsync();
        
        // Wait for initial health check
        await Task.Delay(200);
        
        // Change to critical status
        isHealthy = false;
        
        // Act - Wait for health check to detect change
        await Task.Delay(300);
        
        // Assert
        Assert.True(eventFired);
        Assert.NotNull(eventArgs);
        Assert.Equal(SystemHealthStatus.Unhealthy, eventArgs.CurrentStatus);
    }
    
    [Fact]
    public void MonitoringInterval_ShouldBeSettable()
    {
        // Arrange
        var newInterval = TimeSpan.FromMinutes(2);
        
        // Act
        _healthMonitor.MonitoringInterval = newInterval;
        
        // Assert
        Assert.Equal(newInterval, _healthMonitor.MonitoringInterval);
    }
    
    [Fact]
    public void SystemHealth_ShouldReturnUnknown_Initially()
    {
        // Act
        var systemHealth = _healthMonitor.SystemHealth;
        
        // Assert
        Assert.Equal(SystemHealthStatus.Unknown, systemHealth);
    }
    
    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var healthMonitor = new ServiceHealthMonitor(_logger);
        
        // Act & Assert
        // XUnit doesn't have DoesNotThrow - just call the method
        healthMonitor.Dispose();
        
        // Multiple disposes should not throw
        // XUnit doesn't have DoesNotThrow - just call the method
        healthMonitor.Dispose();
    }
    
    [Fact]
    public async Task Dispose_ShouldStopMonitoring_WhenRunning()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        Assert.True(_healthMonitor.IsRunning);
        
        // Act
        _healthMonitor.Dispose();
        
        // Assert
        Assert.False(_healthMonitor.IsRunning);
    }
}