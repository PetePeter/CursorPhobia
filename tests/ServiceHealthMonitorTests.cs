using Microsoft.VisualStudio.TestTools.UnitTesting;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System;
using System.Threading.Tasks;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for ServiceHealthMonitor - Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
[TestClass]
public class ServiceHealthMonitorTests
{
    private TestLogger _logger = null!;
    private ServiceHealthMonitor _healthMonitor = null!;
    
    [TestInitialize]
    public void TestInitialize()
    {
        _logger = new TestLogger();
        _healthMonitor = new ServiceHealthMonitor(_logger);
    }
    
    [TestCleanup]
    public void TestCleanup()
    {
        _healthMonitor?.Dispose();
    }
    
    [TestMethod]
    public async Task StartAsync_ShouldReturnTrue_WhenCallingFirstTime()
    {
        // Act
        var result = await _healthMonitor.StartAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_healthMonitor.IsRunning);
    }
    
    [TestMethod]
    public async Task StartAsync_ShouldReturnTrue_WhenCallingMultipleTimes()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        
        // Act
        var result = await _healthMonitor.StartAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_healthMonitor.IsRunning);
    }
    
    [TestMethod]
    public async Task StopAsync_ShouldReturnTrue_WhenRunning()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        
        // Act
        var result = await _healthMonitor.StopAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsFalse(_healthMonitor.IsRunning);
    }
    
    [TestMethod]
    public async Task StopAsync_ShouldReturnTrue_WhenNotRunning()
    {
        // Act
        var result = await _healthMonitor.StopAsync();
        
        // Assert
        Assert.IsTrue(result);
        Assert.IsFalse(_healthMonitor.IsRunning);
    }
    
    [TestMethod]
    public async Task RegisterServiceAsync_ShouldReturnTrue_WithValidParameters()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy, Description = "Test service is healthy" })
        );
        
        // Act
        var result = await _healthMonitor.RegisterServiceAsync("TestService", healthCheck);
        
        // Assert
        Assert.IsTrue(result);
        var services = _healthMonitor.GetRegisteredServices();
        Assert.IsTrue(services.Contains("TestService"));
    }
    
    [TestMethod]
    public async Task RegisterServiceAsync_ShouldReturnFalse_WithNullServiceName()
    {
        // Arrange
        var healthCheck = new Func<Task<ServiceHealthResult>>(() => 
            Task.FromResult(new ServiceHealthResult { Status = ServiceHealthStatus.Healthy })
        );
        
        // Act
        var result = await _healthMonitor.RegisterServiceAsync(null!, healthCheck);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task RegisterServiceAsync_ShouldReturnFalse_WithNullHealthCheck()
    {
        // Act
        var result = await _healthMonitor.RegisterServiceAsync("TestService", null!);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
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
        Assert.IsTrue(result);
        var services = _healthMonitor.GetRegisteredServices();
        Assert.IsFalse(services.Contains("TestService"));
    }
    
    [TestMethod]
    public async Task UnregisterServiceAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.UnregisterServiceAsync("NonExistentService");
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task GetServiceHealth_ShouldReturnNull_ForUnregisteredService()
    {
        // Act
        var health = _healthMonitor.GetServiceHealth("NonExistentService");
        
        // Assert
        Assert.IsNull(health);
    }
    
    [TestMethod]
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
        Assert.IsNotNull(health);
        // Initial status should be Unknown until first health check
        Assert.AreEqual(ServiceHealthStatus.Unknown, health.Value);
    }
    
    [TestMethod]
    public async Task GetServiceHealthInfo_ShouldReturnNull_ForUnregisteredService()
    {
        // Act
        var healthInfo = _healthMonitor.GetServiceHealthInfo("NonExistentService");
        
        // Assert
        Assert.IsNull(healthInfo);
    }
    
    [TestMethod]
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
        Assert.IsNotNull(healthInfo);
        Assert.AreEqual("TestService", healthInfo.ServiceName);
        Assert.IsNotNull(healthInfo.Options);
    }
    
    [TestMethod]
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
        Assert.AreEqual(ServiceHealthStatus.Healthy, result.Status);
        Assert.AreEqual("Service is healthy", result.Description);
        Assert.IsTrue(result.ResponseTime > TimeSpan.Zero);
    }
    
    [TestMethod]
    public async Task CheckServiceHealthAsync_ShouldReturnUnknownResult_ForUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.CheckServiceHealthAsync("NonExistentService");
        
        // Assert
        Assert.AreEqual(ServiceHealthStatus.Unknown, result.Status);
        Assert.IsTrue(result.Description.Contains("not found") || result.Description.Contains("not registered"));
    }
    
    [TestMethod]
    public async Task CheckAllServicesHealthAsync_ShouldReturnEmptyResult_WithNoServices()
    {
        // Act
        var result = await _healthMonitor.CheckAllServicesHealthAsync();
        
        // Assert
        Assert.AreEqual(SystemHealthStatus.Unknown, result.SystemStatus);
        Assert.AreEqual(0, result.TotalServices);
    }
    
    [TestMethod]
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
        Assert.AreEqual(SystemHealthStatus.Healthy, result.SystemStatus);
        Assert.AreEqual(2, result.TotalServices);
        Assert.AreEqual(2, result.HealthyServices);
        Assert.AreEqual(0, result.UnhealthyServices);
    }
    
    [TestMethod]
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
        Assert.AreEqual(SystemHealthStatus.Unhealthy, result.SystemStatus);
        Assert.AreEqual(2, result.TotalServices);
        Assert.AreEqual(1, result.HealthyServices);
        Assert.AreEqual(1, result.UnhealthyServices);
    }
    
    [TestMethod]
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
        Assert.AreEqual(SystemHealthStatus.Critical, result.SystemStatus);
        Assert.AreEqual(1, result.CriticalServices);
    }
    
    [TestMethod]
    public async Task GetAllServiceHealth_ShouldReturnEmptyDictionary_WithNoServices()
    {
        // Act
        var allHealth = _healthMonitor.GetAllServiceHealth();
        
        // Assert
        Assert.IsNotNull(allHealth);
        Assert.AreEqual(0, allHealth.Count);
    }
    
    [TestMethod]
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
        Assert.AreEqual(2, allHealth.Count);
        Assert.IsTrue(allHealth.ContainsKey("Service1"));
        Assert.IsTrue(allHealth.ContainsKey("Service2"));
    }
    
    [TestMethod]
    public async Task GetRegisteredServices_ShouldReturnEmptyList_Initially()
    {
        // Act
        var services = _healthMonitor.GetRegisteredServices();
        
        // Assert
        Assert.IsNotNull(services);
        Assert.AreEqual(0, services.Count);
    }
    
    [TestMethod]
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
        Assert.AreEqual(2, services.Count);
        Assert.IsTrue(services.Contains("Service1"));
        Assert.IsTrue(services.Contains("Service2"));
    }
    
    [TestMethod]
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
        Assert.IsNotNull(stats);
        Assert.AreEqual(1, stats.TotalServices);
        Assert.IsTrue(stats.MonitoringUptime >= TimeSpan.Zero);
    }
    
    [TestMethod]
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
        Assert.IsTrue(result);
    }
    
    [TestMethod]
    public async Task ResetServiceStatisticsAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Act
        var result = await _healthMonitor.ResetServiceStatisticsAsync("NonExistentService");
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
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
        Assert.IsTrue(result);
    }
    
    [TestMethod]
    public async Task UpdateServiceMonitoringAsync_ShouldReturnFalse_WithUnregisteredService()
    {
        // Arrange
        var newOptions = new ServiceMonitoringOptions();
        
        // Act
        var result = await _healthMonitor.UpdateServiceMonitoringAsync("NonExistentService", newOptions);
        
        // Assert
        Assert.IsFalse(result);
    }
    
    [TestMethod]
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
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(eventArgs);
        Assert.AreEqual("TestService", eventArgs.ServiceName);
    }
    
    [TestMethod]
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
        Assert.IsTrue(eventFired);
        Assert.IsNotNull(eventArgs);
        Assert.AreEqual(SystemHealthStatus.Critical, eventArgs.NewStatus);
    }
    
    [TestMethod]
    public void MonitoringInterval_ShouldBeSettable()
    {
        // Arrange
        var newInterval = TimeSpan.FromMinutes(2);
        
        // Act
        _healthMonitor.MonitoringInterval = newInterval;
        
        // Assert
        Assert.AreEqual(newInterval, _healthMonitor.MonitoringInterval);
    }
    
    [TestMethod]
    public void SystemHealth_ShouldReturnUnknown_Initially()
    {
        // Act
        var systemHealth = _healthMonitor.SystemHealth;
        
        // Assert
        Assert.AreEqual(SystemHealthStatus.Unknown, systemHealth);
    }
    
    [TestMethod]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var healthMonitor = new ServiceHealthMonitor(_logger);
        
        // Act & Assert
        Assert.DoesNotThrow(() => healthMonitor.Dispose());
        
        // Multiple disposes should not throw
        Assert.DoesNotThrow(() => healthMonitor.Dispose());
    }
    
    [TestMethod]
    public async Task Dispose_ShouldStopMonitoring_WhenRunning()
    {
        // Arrange
        await _healthMonitor.StartAsync();
        Assert.IsTrue(_healthMonitor.IsRunning);
        
        // Act
        _healthMonitor.Dispose();
        
        // Assert
        Assert.IsFalse(_healthMonitor.IsRunning);
    }
}