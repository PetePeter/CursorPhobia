using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services
{
    /// <summary>
    /// Production health monitoring service providing comprehensive system health tracking
    /// and alerting capabilities for enterprise deployment monitoring.
    /// </summary>
    public class HealthCheckService : IHealthCheckService
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, ComponentHealthInfo> _components;
        private readonly System.Threading.Timer _healthCheckTimer;
        private readonly object _lockObject = new object();
        private readonly DateTime _startTime;
        
        private volatile bool _isDisposed;
        private volatile bool _isMonitoring;
        private SystemHealthStatus _currentSystemHealth;
        private long _totalHealthChecks;
        private long _failedHealthChecks;
        private readonly List<DateTime> _healthChanges;

        /// <summary>
        /// Event fired when overall system health status changes
        /// </summary>
        public event EventHandler<SystemHealthChangedEventArgs> SystemHealthChanged;

        /// <summary>
        /// Initializes a new instance of the HealthCheckService
        /// </summary>
        /// <param name="logger">Logger for health monitoring events</param>
        public HealthCheckService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _components = new ConcurrentDictionary<string, ComponentHealthInfo>();
            _healthCheckTimer = new System.Threading.Timer(PerformScheduledHealthCheck, null, Timeout.Infinite, Timeout.Infinite);
            _startTime = DateTime.UtcNow;
            _currentSystemHealth = SystemHealthStatus.Unknown;
            _healthChanges = new List<DateTime>();
            
            _logger.LogInformation("HealthCheckService initialized");
        }

        /// <summary>
        /// Gets the current overall system health status
        /// </summary>
        /// <returns>Current system health status</returns>
        public async Task<SystemHealthStatus> GetSystemHealthAsync()
        {
            if (_isDisposed)
                return SystemHealthStatus.Unknown;

            await PerformHealthCheckAsync();
            return _currentSystemHealth;
        }

        /// <summary>
        /// Gets detailed health information for all monitored components
        /// </summary>
        /// <returns>Dictionary of component names and their health status</returns>
        public async Task<Dictionary<string, ComponentHealthStatus>> GetDetailedHealthAsync()
        {
            if (_isDisposed)
                return new Dictionary<string, ComponentHealthStatus>();

            var result = new Dictionary<string, ComponentHealthStatus>();
            
            foreach (var kvp in _components)
            {
                var component = kvp.Value;
                result[kvp.Key] = new ComponentHealthStatus
                {
                    IsHealthy = component.IsHealthy,
                    IsCritical = component.IsCritical,
                    LastChecked = component.LastChecked,
                    LastException = component.LastException,
                    ConsecutiveFailures = component.ConsecutiveFailures,
                    StatusDescription = component.StatusDescription,
                    AdditionalData = new Dictionary<string, object>(component.AdditionalData)
                };
            }

            return result;
        }

        /// <summary>
        /// Starts the health monitoring service with specified check interval
        /// </summary>
        /// <param name="checkInterval">How often to perform health checks</param>
        public async Task StartMonitoringAsync(TimeSpan checkInterval)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HealthCheckService));

            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("Health monitoring is already running");
                    return;
                }

                _isMonitoring = true;
                _healthCheckTimer.Change(TimeSpan.Zero, checkInterval);
                
                _logger.LogInformation("Health monitoring started with interval: {CheckInterval}", checkInterval);
            }

            // Perform initial health check
            await PerformHealthCheckAsync();
        }

        /// <summary>
        /// Stops the health monitoring service
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_isDisposed)
                return;

            lock (_lockObject)
            {
                if (!_isMonitoring)
                    return;

                _isMonitoring = false;
                _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                _logger.LogInformation("Health monitoring stopped");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Manually triggers a health check of all components
        /// </summary>
        /// <returns>Current system health status after the check</returns>
        public async Task<SystemHealthStatus> PerformHealthCheckAsync()
        {
            if (_isDisposed)
                return SystemHealthStatus.Unknown;

            var stopwatch = Stopwatch.StartNew();
            var healthCheckTasks = new List<Task>();
            var previousSystemHealth = _currentSystemHealth;

            try
            {
                // Perform health checks for all registered components
                foreach (var kvp in _components)
                {
                    var componentName = kvp.Key;
                    var componentInfo = kvp.Value;
                    
                    healthCheckTasks.Add(CheckComponentHealthAsync(componentName, componentInfo));
                }

                // Wait for all health checks to complete
                await Task.WhenAll(healthCheckTasks);

                // Calculate overall system health
                var newSystemHealth = CalculateSystemHealth();
                
                lock (_lockObject)
                {
                    _totalHealthChecks++;
                    
                    if (newSystemHealth != _currentSystemHealth)
                    {
                        var oldHealth = _currentSystemHealth;
                        _currentSystemHealth = newSystemHealth;
                        _healthChanges.Add(DateTime.UtcNow);

                        // Raise system health changed event
                        SystemHealthChanged?.Invoke(this, new SystemHealthChangedEventArgs
                        {
                            PreviousStatus = oldHealth,
                            CurrentStatus = newSystemHealth,
                            Timestamp = DateTime.UtcNow,
                            Description = $"System health changed from {oldHealth} to {newSystemHealth}"
                        });

                        _logger.LogWarning("System health changed from {OldHealth} to {NewHealth}", 
                            oldHealth, newSystemHealth);
                    }
                }

                stopwatch.Stop();
                _logger.LogDebug("Health check completed in {Duration}ms. System health: {SystemHealth}", 
                    stopwatch.ElapsedMilliseconds, _currentSystemHealth);

                return _currentSystemHealth;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                lock (_lockObject)
                {
                    _totalHealthChecks++;
                    _failedHealthChecks++;
                }

                _logger.LogError(ex, "Error during health check execution");
                return SystemHealthStatus.Unknown;
            }
        }

        /// <summary>
        /// Registers a component for health monitoring
        /// </summary>
        /// <param name="componentName">Name of the component to monitor</param>
        /// <param name="healthCheckFunc">Function to check component health</param>
        /// <param name="isCritical">Whether this component is critical for system operation</param>
        public void RegisterComponent(string componentName, Func<Task<bool>> healthCheckFunc, bool isCritical = false)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(HealthCheckService));

            if (string.IsNullOrWhiteSpace(componentName))
                throw new ArgumentException("Component name cannot be null or empty", nameof(componentName));

            if (healthCheckFunc == null)
                throw new ArgumentNullException(nameof(healthCheckFunc));

            var componentInfo = new ComponentHealthInfo
            {
                HealthCheckFunc = healthCheckFunc,
                IsCritical = isCritical,
                IsHealthy = true,
                LastChecked = DateTime.UtcNow,
                StatusDescription = "Not yet checked"
            };

            _components.AddOrUpdate(componentName, componentInfo, (key, existing) =>
            {
                existing.HealthCheckFunc = healthCheckFunc;
                existing.IsCritical = isCritical;
                return existing;
            });

            _logger.LogInformation("Registered component '{ComponentName}' for health monitoring (Critical: {IsCritical})", 
                componentName, isCritical);
        }

        /// <summary>
        /// Unregisters a component from health monitoring
        /// </summary>
        /// <param name="componentName">Name of the component to stop monitoring</param>
        public void UnregisterComponent(string componentName)
        {
            if (_isDisposed)
                return;

            if (string.IsNullOrWhiteSpace(componentName))
                return;

            if (_components.TryRemove(componentName, out _))
            {
                _logger.LogInformation("Unregistered component '{ComponentName}' from health monitoring", componentName);
            }
        }

        /// <summary>
        /// Gets health metrics for monitoring dashboards
        /// </summary>
        /// <returns>Health metrics including uptime, check counts, and failure rates</returns>
        public async Task<HealthMetrics> GetHealthMetricsAsync()
        {
            if (_isDisposed)
                return new HealthMetrics();

            var currentTime = DateTime.UtcNow;
            var memoryUsage = GC.GetTotalMemory(false);
            var healthChangesLastHour = _healthChanges.Count(hc => currentTime - hc <= TimeSpan.FromHours(1));

            var detailedHealth = await GetDetailedHealthAsync();
            var unhealthyCritical = detailedHealth.Values.Count(c => c.IsCritical && !c.IsHealthy);
            var unhealthyNonCritical = detailedHealth.Values.Count(c => !c.IsCritical && !c.IsHealthy);

            return new HealthMetrics
            {
                Uptime = currentTime - _startTime,
                TotalHealthChecks = _totalHealthChecks,
                FailedHealthChecks = _failedHealthChecks,
                MonitoredComponents = _components.Count,
                UnhealthyCriticalComponents = unhealthyCritical,
                UnhealthyNonCriticalComponents = unhealthyNonCritical,
                AverageCheckDurationMs = CalculateAverageCheckDuration(),
                LastHealthChange = _healthChanges.LastOrDefault(),
                MemoryUsageBytes = memoryUsage,
                HealthChangesLastHour = healthChangesLastHour
            };
        }

        /// <summary>
        /// Performs a scheduled health check (called by timer)
        /// </summary>
        /// <param name="state">Timer state (unused)</param>
        private async void PerformScheduledHealthCheck(object state)
        {
            if (_isDisposed || !_isMonitoring)
                return;

            try
            {
                await PerformHealthCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled health check");
            }
        }

        /// <summary>
        /// Checks the health of a specific component
        /// </summary>
        /// <param name="componentName">Name of the component</param>
        /// <param name="componentInfo">Component health information</param>
        private async Task CheckComponentHealthAsync(string componentName, ComponentHealthInfo componentInfo)
        {
            var checkStartTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var isHealthy = await componentInfo.HealthCheckFunc();
                
                stopwatch.Stop();
                var duration = stopwatch.ElapsedMilliseconds;

                componentInfo.LastChecked = checkStartTime;
                componentInfo.LastException = null;
                componentInfo.AdditionalData["LastCheckDurationMs"] = duration;

                if (isHealthy)
                {
                    componentInfo.IsHealthy = true;
                    componentInfo.ConsecutiveFailures = 0;
                    componentInfo.StatusDescription = "Healthy";
                }
                else
                {
                    componentInfo.IsHealthy = false;
                    componentInfo.ConsecutiveFailures++;
                    componentInfo.StatusDescription = $"Unhealthy (consecutive failures: {componentInfo.ConsecutiveFailures})";
                    
                    _logger.LogWarning("Component '{ComponentName}' health check failed. Consecutive failures: {ConsecutiveFailures}", 
                        componentName, componentInfo.ConsecutiveFailures);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                componentInfo.LastChecked = checkStartTime;
                componentInfo.LastException = ex;
                componentInfo.IsHealthy = false;
                componentInfo.ConsecutiveFailures++;
                componentInfo.StatusDescription = $"Health check exception: {ex.Message}";
                componentInfo.AdditionalData["LastCheckDurationMs"] = stopwatch.ElapsedMilliseconds;

                _logger.LogError(ex, "Exception during health check for component '{ComponentName}'. Consecutive failures: {ConsecutiveFailures}", 
                    componentName, componentInfo.ConsecutiveFailures);

                lock (_lockObject)
                {
                    _failedHealthChecks++;
                }
            }
        }

        /// <summary>
        /// Calculates overall system health based on component health
        /// </summary>
        /// <returns>Overall system health status</returns>
        private SystemHealthStatus CalculateSystemHealth()
        {
            if (_components.IsEmpty)
                return SystemHealthStatus.Unknown;

            var criticalComponents = _components.Values.Where(c => c.IsCritical).ToList();
            var nonCriticalComponents = _components.Values.Where(c => !c.IsCritical).ToList();

            // If any critical component is unhealthy, system is unhealthy
            if (criticalComponents.Any(c => !c.IsHealthy))
                return SystemHealthStatus.Unhealthy;

            // If all critical components are healthy but some non-critical are unhealthy, system is degraded
            if (nonCriticalComponents.Any(c => !c.IsHealthy))
                return SystemHealthStatus.Degraded;

            // All components are healthy
            return SystemHealthStatus.Healthy;
        }

        /// <summary>
        /// Calculates average health check duration
        /// </summary>
        /// <returns>Average duration in milliseconds</returns>
        private double CalculateAverageCheckDuration()
        {
            var durations = _components.Values
                .Where(c => c.AdditionalData.ContainsKey("LastCheckDurationMs"))
                .Select(c => Convert.ToDouble(c.AdditionalData["LastCheckDurationMs"]))
                .ToList();

            return durations.Any() ? durations.Average() : 0;
        }

        /// <summary>
        /// Disposes the health check service and releases resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                _healthCheckTimer?.Dispose();
                _components.Clear();
                
                _logger.LogInformation("HealthCheckService disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing HealthCheckService");
            }
        }

        /// <summary>
        /// Internal component health information
        /// </summary>
        private class ComponentHealthInfo
        {
            public Func<Task<bool>> HealthCheckFunc { get; set; }
            public bool IsCritical { get; set; }
            public bool IsHealthy { get; set; }
            public DateTime LastChecked { get; set; }
            public Exception LastException { get; set; }
            public int ConsecutiveFailures { get; set; }
            public string StatusDescription { get; set; }
            public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
        }
    }
}