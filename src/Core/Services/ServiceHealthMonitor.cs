using System.Collections.Concurrent;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Implements service health monitoring with automatic restart capabilities and comprehensive health tracking
/// Part of Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public class ServiceHealthMonitor : IServiceHealthMonitor
{
    private readonly ILogger _logger;
    private readonly ISystemTrayManager? _trayManager;
    private readonly ConcurrentDictionary<string, MonitoredService> _services = new();
    private readonly System.Threading.Timer _monitoringTimer;
    private readonly object _lock = new();
    private bool _disposed = false;
    private bool _running = false;
    private DateTime _startTime;
    private SystemHealthStatus _currentSystemHealth = SystemHealthStatus.Unknown;

    /// <summary>
    /// Event raised when a service health status changes
    /// </summary>
    public event EventHandler<ServiceHealthChangedEventArgs>? ServiceHealthChanged;

    /// <summary>
    /// Event raised when a critical service becomes unhealthy
    /// </summary>
    public event EventHandler<CriticalServiceUnhealthyEventArgs>? CriticalServiceUnhealthy;

    /// <summary>
    /// Event raised when overall system health changes
    /// </summary>
    public event EventHandler<SystemHealthChangedEventArgs>? SystemHealthChanged;

    /// <summary>
    /// Event raised when a service restart is recommended
    /// </summary>
    public event EventHandler<ServiceRestartRecommendedEventArgs>? ServiceRestartRecommended;

    /// <summary>
    /// Gets whether the health monitor is currently running
    /// </summary>
    public bool IsRunning => _running && !_disposed;

    /// <summary>
    /// Gets the current overall system health status
    /// </summary>
    public SystemHealthStatus SystemHealth => _currentSystemHealth;

    /// <summary>
    /// Gets or sets the monitoring interval for health checks
    /// </summary>
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a new ServiceHealthMonitor instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="trayManager">Optional system tray manager for notifications</param>
    public ServiceHealthMonitor(ILogger logger, ISystemTrayManager? trayManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trayManager = trayManager;

        // Initialize monitoring timer (not started)
        _monitoringTimer = new System.Threading.Timer(PerformHealthChecks, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Starts the health monitoring service
    /// </summary>
    /// <returns>True if started successfully</returns>
    public async Task<bool> StartAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot start disposed ServiceHealthMonitor");
            return false;
        }

        if (_running)
        {
            _logger.LogDebug("ServiceHealthMonitor is already running");
            return true;
        }

        try
        {
            _logger.LogInformation("Starting service health monitor...");

            _startTime = DateTime.UtcNow;
            _running = true;

            // Start monitoring timer
            _monitoringTimer.Change(TimeSpan.Zero, MonitoringInterval);

            _logger.LogInformation("Service health monitor started with {ServiceCount} registered services (interval: {Interval})",
                _services.Count, MonitoringInterval);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service health monitor");
            _running = false;
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the health monitoring service
    /// </summary>
    /// <returns>True if stopped successfully</returns>
    public async Task<bool> StopAsync()
    {
        if (!_running)
        {
            return true;
        }

        try
        {
            _logger.LogInformation("Stopping service health monitor...");

            // Stop monitoring timer
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _running = false;
            _currentSystemHealth = SystemHealthStatus.Unknown;

            _logger.LogInformation("Service health monitor stopped");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping service health monitor");
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Registers a service for health monitoring
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="healthCheck">Health check function</param>
    /// <param name="options">Monitoring options</param>
    /// <returns>True if registration was successful</returns>
    public async Task<bool> RegisterServiceAsync(string serviceName, Func<Task<ServiceHealthResult>> healthCheck, ServiceMonitoringOptions? options = null)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            _logger.LogWarning("Cannot register service with null or empty name");
            return false;
        }

        if (healthCheck == null)
        {
            _logger.LogWarning("Cannot register service '{ServiceName}' with null health check function", serviceName);
            return false;
        }

        if (_disposed)
        {
            _logger.LogWarning("Cannot register service with disposed ServiceHealthMonitor");
            return false;
        }

        try
        {
            var effectiveOptions = options ?? new ServiceMonitoringOptions();
            var service = new MonitoredService
            {
                Name = serviceName,
                HealthCheck = healthCheck,
                Options = effectiveOptions,
                Status = ServiceHealthStatus.Unknown,
                RegistrationTime = DateTime.UtcNow
            };

            _services.AddOrUpdate(serviceName, service, (key, existing) =>
            {
                _logger.LogInformation("Updating existing service registration: {ServiceName}", serviceName);
                existing.HealthCheck = healthCheck;
                existing.Options = effectiveOptions;
                return existing;
            });

            _logger.LogInformation("Registered service for health monitoring: {ServiceName} (Critical: {IsCritical}, Interval: {Interval})",
                serviceName, effectiveOptions.IsCritical, effectiveOptions.CheckInterval);

            // Perform initial health check
            if (_running)
            {
                _ = Task.Run(async () => await CheckSingleServiceHealthAsync(service));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register service: {ServiceName}", serviceName);
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Unregisters a service from health monitoring
    /// </summary>
    /// <param name="serviceName">Name of the service to unregister</param>
    /// <returns>True if unregistration was successful</returns>
    public async Task<bool> UnregisterServiceAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return false;
        }

        try
        {
            if (_services.TryRemove(serviceName, out var service))
            {
                _logger.LogInformation("Unregistered service from health monitoring: {ServiceName}", serviceName);

                // Update system health after service removal
                if (_running)
                {
                    await UpdateSystemHealthAsync();
                }

                return true;
            }

            _logger.LogDebug("Service not found for unregistration: {ServiceName}", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister service: {ServiceName}", serviceName);
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current health status of a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Current health status or null if service not found</returns>
    public ServiceHealthStatus? GetServiceHealth(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName) || !_services.TryGetValue(serviceName, out var service))
        {
            return null;
        }

        return service.Status;
    }

    /// <summary>
    /// Gets detailed health information for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Detailed health information or null if service not found</returns>
    public ServiceHealthInfo? GetServiceHealthInfo(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName) || !_services.TryGetValue(serviceName, out var service))
        {
            return null;
        }

        return new ServiceHealthInfo
        {
            ServiceName = serviceName,
            Status = service.Status,
            LastCheckResult = service.LastCheckResult,
            LastHealthyTime = service.LastHealthyTime,
            LastUnhealthyTime = service.LastUnhealthyTime,
            ConsecutiveFailures = service.ConsecutiveFailures,
            ConsecutiveSuccesses = service.ConsecutiveSuccesses,
            TotalChecks = service.TotalChecks,
            TotalFailures = service.TotalFailures,
            AverageResponseTime = service.AverageResponseTime,
            Options = service.Options,
            RestartAttempts = service.RestartAttempts,
            LastRestartTime = service.LastRestartTime
        };
    }

    /// <summary>
    /// Gets health information for all monitored services
    /// </summary>
    /// <returns>Dictionary of service health information</returns>
    public IReadOnlyDictionary<string, ServiceHealthInfo> GetAllServiceHealth()
    {
        var result = new Dictionary<string, ServiceHealthInfo>();

        foreach (var kvp in _services)
        {
            var healthInfo = GetServiceHealthInfo(kvp.Key);
            if (healthInfo != null)
            {
                result[kvp.Key] = healthInfo;
            }
        }

        return result;
    }

    /// <summary>
    /// Performs an immediate health check on a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Health check result</returns>
    public async Task<ServiceHealthResult> CheckServiceHealthAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName) || !_services.TryGetValue(serviceName, out var service))
        {
            return new ServiceHealthResult
            {
                Status = ServiceHealthStatus.Unknown,
                Description = "Service not found or not registered",
                ResponseTime = TimeSpan.Zero
            };
        }

        return await CheckSingleServiceHealthAsync(service);
    }

    /// <summary>
    /// Performs an immediate health check on all registered services
    /// </summary>
    /// <returns>Overall system health check result</returns>
    public async Task<SystemHealthCheckResult> CheckAllServicesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new SystemHealthCheckResult
        {
            CheckTime = DateTime.UtcNow,
            ServiceResults = new Dictionary<string, ServiceHealthResult>()
        };

        try
        {
            var tasks = _services.Values.Select(async service =>
            {
                var healthResult = await CheckSingleServiceHealthAsync(service);
                return new { ServiceName = service.Name, Result = healthResult };
            });

            var serviceResults = await Task.WhenAll(tasks);

            foreach (var serviceResult in serviceResults)
            {
                result.ServiceResults[serviceResult.ServiceName] = serviceResult.Result;
            }

            // Determine system health based on service results
            result.SystemStatus = DetermineSystemHealth(result.ServiceResults.Values);
            result.CheckDuration = stopwatch.Elapsed;
            result.Summary = GenerateHealthSummary(result);

            // Update current system health
            await UpdateSystemHealthAsync(result.SystemStatus);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during system health check");
            result.SystemStatus = SystemHealthStatus.Unhealthy;
            result.CheckDuration = stopwatch.Elapsed;
            result.Summary = $"Health check failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Gets a list of all registered service names
    /// </summary>
    /// <returns>List of service names</returns>
    public IReadOnlyList<string> GetRegisteredServices()
    {
        return _services.Keys.ToList();
    }

    /// <summary>
    /// Gets health monitoring statistics
    /// </summary>
    /// <returns>Health monitoring statistics</returns>
    public HealthMonitoringStatistics GetStatistics()
    {
        var services = _services.Values.ToList();

        return new HealthMonitoringStatistics
        {
            TotalServices = services.Count,
            CriticalServices = services.Count(s => s.Options.IsCritical),
            TotalHealthChecks = services.Sum(s => s.TotalChecks),
            TotalFailures = services.Sum(s => s.TotalFailures),
            AverageResponseTime = services.Count > 0
                ? TimeSpan.FromMilliseconds(services.Average(s => s.AverageResponseTime.TotalMilliseconds))
                : TimeSpan.Zero,
            MonitoringStartTime = _startTime,
            MonitoringUptime = _running ? DateTime.UtcNow - _startTime : TimeSpan.Zero,
            TotalRestarts = services.Sum(s => s.RestartAttempts)
        };
    }

    /// <summary>
    /// Resets health statistics for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>True if reset was successful</returns>
    public async Task<bool> ResetServiceStatisticsAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName) || !_services.TryGetValue(serviceName, out var service))
        {
            return false;
        }

        try
        {
            service.TotalChecks = 0;
            service.TotalFailures = 0;
            service.ConsecutiveFailures = 0;
            service.ConsecutiveSuccesses = 0;
            service.RestartAttempts = 0;
            service.LastHealthyTime = null;
            service.LastUnhealthyTime = null;
            service.LastRestartTime = null;
            service.AverageResponseTime = TimeSpan.Zero;
            service.ResponseTimes.Clear();

            _logger.LogInformation("Reset health statistics for service: {ServiceName}", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting statistics for service: {ServiceName}", serviceName);
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Updates the monitoring configuration for a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="options">New monitoring options</param>
    /// <returns>True if update was successful</returns>
    public async Task<bool> UpdateServiceMonitoringAsync(string serviceName, ServiceMonitoringOptions options)
    {
        if (string.IsNullOrEmpty(serviceName) || !_services.TryGetValue(serviceName, out var service))
        {
            return false;
        }

        try
        {
            service.Options = options ?? new ServiceMonitoringOptions();
            _logger.LogInformation("Updated monitoring options for service: {ServiceName}", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating monitoring options for service: {ServiceName}", serviceName);
            return false;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Performs health checks on all services (timer callback)
    /// </summary>
    private async void PerformHealthChecks(object? state)
    {
        if (_disposed || !_running)
            return;

        try
        {
            await CheckAllServicesHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during periodic health checks");
        }
    }

    /// <summary>
    /// Performs health check on a single service
    /// </summary>
    private async Task<ServiceHealthResult> CheckSingleServiceHealthAsync(MonitoredService service)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ServiceHealthResult result;

        try
        {
            // Use timeout for health check
            using var cancellationTokenSource = new CancellationTokenSource(service.Options.CheckTimeout);

            result = await service.HealthCheck();
            result.ResponseTime = stopwatch.Elapsed;

            if (result.CheckTime == default)
            {
                result.CheckTime = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            result = new ServiceHealthResult
            {
                Status = ServiceHealthStatus.Critical,
                Description = $"Health check timed out after {service.Options.CheckTimeout.TotalSeconds} seconds",
                ResponseTime = stopwatch.Elapsed,
                Exception = new TimeoutException("Health check timeout")
            };
        }
        catch (Exception ex)
        {
            result = new ServiceHealthResult
            {
                Status = ServiceHealthStatus.Critical,
                Description = $"Health check failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed,
                Exception = ex
            };
        }

        // Update service statistics
        service.TotalChecks++;
        service.LastCheckResult = result;
        service.UpdateAverageResponseTime(result.ResponseTime);

        var previousStatus = service.Status;
        var isHealthy = result.Status == ServiceHealthStatus.Healthy;

        if (isHealthy)
        {
            service.ConsecutiveSuccesses++;
            service.ConsecutiveFailures = 0;
            service.LastHealthyTime = DateTime.UtcNow;

            // Update status if we have enough consecutive successes
            if (service.ConsecutiveSuccesses >= service.Options.RecoveryThreshold)
            {
                service.Status = ServiceHealthStatus.Healthy;
            }
        }
        else
        {
            service.TotalFailures++;
            service.ConsecutiveFailures++;
            service.ConsecutiveSuccesses = 0;
            service.LastUnhealthyTime = DateTime.UtcNow;

            // Update status if we have enough consecutive failures
            if (service.ConsecutiveFailures >= service.Options.FailureThreshold)
            {
                service.Status = result.Status;
            }
        }

        // Check for status change
        if (service.Status != previousStatus)
        {
            _logger.LogInformation("Service health status changed: {ServiceName} {PreviousStatus} -> {NewStatus}",
                service.Name, previousStatus, service.Status);

            // Raise service health changed event
            ServiceHealthChanged?.Invoke(this, new ServiceHealthChangedEventArgs
            {
                ServiceName = service.Name,
                PreviousStatus = previousStatus,
                NewStatus = service.Status,
                CheckResult = result,
                IsCritical = service.Options.IsCritical
            });

            // Handle critical service becoming unhealthy
            if (service.Options.IsCritical && service.Status != ServiceHealthStatus.Healthy)
            {
                await HandleCriticalServiceUnhealthyAsync(service, result);
            }

            // Show notification if enabled
            if (service.Options.EnableNotifications && _trayManager != null)
            {
                var isError = service.Status == ServiceHealthStatus.Critical || service.Status == ServiceHealthStatus.Unhealthy;
                await _trayManager.ShowNotificationAsync("CursorPhobia Health Monitor",
                    $"Service '{service.Name}' is now {service.Status}", isError);
            }
        }

        return result;
    }

    /// <summary>
    /// Handles critical service becoming unhealthy
    /// </summary>
    private async Task HandleCriticalServiceUnhealthyAsync(MonitoredService service, ServiceHealthResult result)
    {
        _logger.LogWarning("Critical service '{ServiceName}' is unhealthy: {Description}",
            service.Name, result.Description);

        // Raise critical service unhealthy event
        var eventArgs = new CriticalServiceUnhealthyEventArgs
        {
            ServiceName = service.Name,
            CheckResult = result,
            ConsecutiveFailures = service.ConsecutiveFailures,
            RestartRecommended = result.RecommendRestart || service.Options.EnableAutoRestart
        };

        CriticalServiceUnhealthy?.Invoke(this, eventArgs);

        // Handle automatic restart if enabled
        if (service.Options.EnableAutoRestart && service.RestartAttempts < service.Options.MaxRestartAttempts)
        {
            await AttemptServiceRestartAsync(service, result);
        }
        else if (result.RecommendRestart)
        {
            // Raise restart recommendation event
            ServiceRestartRecommended?.Invoke(this, new ServiceRestartRecommendedEventArgs
            {
                ServiceName = service.Name,
                Reason = result.RestartReason ?? "Service health check failed",
                CheckResult = result,
                IsAutomatic = false,
                Priority = service.Options.IsCritical ? 5 : 1
            });
        }
    }

    /// <summary>
    /// Attempts to restart a service
    /// </summary>
    private async Task AttemptServiceRestartAsync(MonitoredService service, ServiceHealthResult result)
    {
        try
        {
            service.RestartAttempts++;
            service.LastRestartTime = DateTime.UtcNow;

            _logger.LogInformation("Attempting automatic restart of service '{ServiceName}' (attempt {Attempt}/{MaxAttempts})",
                service.Name, service.RestartAttempts, service.Options.MaxRestartAttempts);

            // Wait before restart attempt
            await Task.Delay(service.Options.RestartDelay);

            // Raise restart event
            ServiceRestartRecommended?.Invoke(this, new ServiceRestartRecommendedEventArgs
            {
                ServiceName = service.Name,
                Reason = $"Automatic restart attempt {service.RestartAttempts}",
                CheckResult = result,
                IsAutomatic = true,
                Priority = service.Options.IsCritical ? 10 : 5
            });

            // Note: Actual restart implementation would depend on the specific service type
            // This is a placeholder for restart logic that would be implemented by the service itself

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during automatic restart attempt for service: {ServiceName}", service.Name);
        }
    }

    /// <summary>
    /// Determines overall system health based on individual service results
    /// </summary>
    private SystemHealthStatus DetermineSystemHealth(IEnumerable<ServiceHealthResult> serviceResults)
    {
        var results = serviceResults.ToList();
        if (!results.Any())
        {
            return SystemHealthStatus.Unknown;
        }

        var criticalCount = results.Count(r => r.Status == ServiceHealthStatus.Critical);
        var unhealthyCount = results.Count(r => r.Status == ServiceHealthStatus.Unhealthy);
        var warningCount = results.Count(r => r.Status == ServiceHealthStatus.Warning);
        var healthyCount = results.Count(r => r.Status == ServiceHealthStatus.Healthy);

        // Determine system health based on service distribution
        if (criticalCount > 0)
        {
            return SystemHealthStatus.Unhealthy;
        }

        if (unhealthyCount > 0)
        {
            return SystemHealthStatus.Unhealthy;
        }

        if (warningCount > 0)
        {
            return SystemHealthStatus.Degraded;
        }

        if (healthyCount == results.Count)
        {
            return SystemHealthStatus.Healthy;
        }

        return SystemHealthStatus.Degraded;
    }

    /// <summary>
    /// Generates a health summary for system health check results
    /// </summary>
    private string GenerateHealthSummary(SystemHealthCheckResult result)
    {
        return $"{result.HealthyServices}/{result.TotalServices} services healthy, " +
               $"{result.WarningServices} warnings, {result.UnhealthyServices} unhealthy, {result.CriticalServices} critical";
    }

    /// <summary>
    /// Updates the current system health status
    /// </summary>
    private async Task UpdateSystemHealthAsync(SystemHealthStatus? newStatus = null)
    {
        SystemHealthStatus systemHealth;

        if (newStatus.HasValue)
        {
            systemHealth = newStatus.Value;
        }
        else
        {
            // Calculate system health from current service states
            var serviceStates = _services.Values.Select(s => new ServiceHealthResult { Status = s.Status });
            systemHealth = DetermineSystemHealth(serviceStates);
        }

        if (systemHealth != _currentSystemHealth)
        {
            var previousHealth = _currentSystemHealth;
            _currentSystemHealth = systemHealth;

            _logger.LogInformation("System health status changed: {PreviousHealth} -> {NewHealth}",
                previousHealth, systemHealth);

            // Raise system health changed event
            SystemHealthChanged?.Invoke(this, new SystemHealthChangedEventArgs
            {
                PreviousStatus = previousHealth,
                CurrentStatus = systemHealth,
                TriggeringComponent = _services.Keys.FirstOrDefault() ?? "System",
                Description = $"System health changed to {systemHealth}"
            });
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the service health monitor
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _monitoringTimer?.Dispose();
            Task.Run(async () => await StopAsync()).Wait(TimeSpan.FromSeconds(5));
            _services.Clear();
            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ServiceHealthMonitor disposal");
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal class representing a monitored service
/// </summary>
internal class MonitoredService
{
    public string Name { get; set; } = string.Empty;
    public Func<Task<ServiceHealthResult>> HealthCheck { get; set; } = null!;
    public ServiceMonitoringOptions Options { get; set; } = new();
    public ServiceHealthStatus Status { get; set; } = ServiceHealthStatus.Unknown;
    public ServiceHealthResult? LastCheckResult { get; set; }
    public DateTime? LastHealthyTime { get; set; }
    public DateTime? LastUnhealthyTime { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }
    public int TotalChecks { get; set; }
    public int TotalFailures { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public int RestartAttempts { get; set; }
    public DateTime? LastRestartTime { get; set; }
    public DateTime RegistrationTime { get; set; }

    public readonly List<TimeSpan> ResponseTimes = new();

    public void UpdateAverageResponseTime(TimeSpan responseTime)
    {
        ResponseTimes.Add(responseTime);

        // Keep only the last 100 response times to prevent memory bloat
        if (ResponseTimes.Count > 100)
        {
            ResponseTimes.RemoveAt(0);
        }

        AverageResponseTime = TimeSpan.FromMilliseconds(ResponseTimes.Average(t => t.TotalMilliseconds));
    }
}