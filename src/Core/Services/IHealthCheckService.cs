using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CursorPhobia.Core.Services
{
    /// <summary>
    /// Service for monitoring overall system health and providing health status information
    /// for production monitoring and alerting systems.
    /// </summary>
    public interface IHealthCheckService : IDisposable
    {
        /// <summary>
        /// Event fired when overall system health status changes
        /// </summary>
        event EventHandler<SystemHealthChangedEventArgs> SystemHealthChanged;

        /// <summary>
        /// Gets the current overall system health status
        /// </summary>
        /// <returns>Current system health status</returns>
        Task<SystemHealthStatus> GetSystemHealthAsync();

        /// <summary>
        /// Gets detailed health information for all monitored components
        /// </summary>
        /// <returns>Dictionary of component names and their health status</returns>
        Task<Dictionary<string, ComponentHealthStatus>> GetDetailedHealthAsync();

        /// <summary>
        /// Starts the health monitoring service with specified check interval
        /// </summary>
        /// <param name="checkInterval">How often to perform health checks</param>
        Task StartMonitoringAsync(TimeSpan checkInterval);

        /// <summary>
        /// Stops the health monitoring service
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Manually triggers a health check of all components
        /// </summary>
        /// <returns>Current system health status after the check</returns>
        Task<SystemHealthStatus> PerformHealthCheckAsync();

        /// <summary>
        /// Registers a component for health monitoring
        /// </summary>
        /// <param name="componentName">Name of the component to monitor</param>
        /// <param name="healthCheckFunc">Function to check component health</param>
        /// <param name="isCritical">Whether this component is critical for system operation</param>
        void RegisterComponent(string componentName, Func<Task<bool>> healthCheckFunc, bool isCritical = false);

        /// <summary>
        /// Unregisters a component from health monitoring
        /// </summary>
        /// <param name="componentName">Name of the component to stop monitoring</param>
        void UnregisterComponent(string componentName);

        /// <summary>
        /// Gets health metrics for monitoring dashboards
        /// </summary>
        /// <returns>Health metrics including uptime, check counts, and failure rates</returns>
        Task<HealthMetrics> GetHealthMetricsAsync();
    }

    /// <summary>
    /// Overall health status of the system
    /// </summary>
    public enum SystemHealthStatus
    {
        /// <summary>
        /// All components are healthy and functioning normally
        /// </summary>
        Healthy,

        /// <summary>
        /// Some non-critical components have issues but system is operational
        /// </summary>
        Degraded,

        /// <summary>
        /// Critical components have failed, system functionality is impaired
        /// </summary>
        Unhealthy,

        /// <summary>
        /// Health status cannot be determined due to monitoring system issues
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Health status of an individual component
    /// </summary>
    public class ComponentHealthStatus
    {
        /// <summary>
        /// Whether the component is currently healthy
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Whether this component is critical for system operation
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Last time this component was checked
        /// </summary>
        public DateTime LastChecked { get; set; }

        /// <summary>
        /// Last exception encountered during health check, if any
        /// </summary>
        public Exception LastException { get; set; }

        /// <summary>
        /// Number of consecutive failed health checks
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// Human-readable status description
        /// </summary>
        public string StatusDescription { get; set; }

        /// <summary>
        /// Additional health check data specific to the component
        /// </summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Event arguments for system health changes
    /// </summary>
    public class SystemHealthChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Previous health status
        /// </summary>
        public SystemHealthStatus PreviousStatus { get; set; }

        /// <summary>
        /// Current health status
        /// </summary>
        public SystemHealthStatus CurrentStatus { get; set; }

        /// <summary>
        /// Timestamp when the status change occurred
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Component that triggered the status change, if applicable
        /// </summary>
        public string TriggeringComponent { get; set; }

        /// <summary>
        /// Additional context about the health change
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Health metrics for monitoring and alerting
    /// </summary>
    public class HealthMetrics
    {
        /// <summary>
        /// How long the system has been running
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Total number of health checks performed
        /// </summary>
        public long TotalHealthChecks { get; set; }

        /// <summary>
        /// Number of failed health checks
        /// </summary>
        public long FailedHealthChecks { get; set; }

        /// <summary>
        /// Percentage of successful health checks
        /// </summary>
        public double SuccessRate => TotalHealthChecks > 0 ?
            (double)(TotalHealthChecks - FailedHealthChecks) / TotalHealthChecks * 100 : 0;

        /// <summary>
        /// Number of registered components being monitored
        /// </summary>
        public int MonitoredComponents { get; set; }

        /// <summary>
        /// Number of critical components currently unhealthy
        /// </summary>
        public int UnhealthyCriticalComponents { get; set; }

        /// <summary>
        /// Number of non-critical components currently unhealthy
        /// </summary>
        public int UnhealthyNonCriticalComponents { get; set; }

        /// <summary>
        /// Average health check duration in milliseconds
        /// </summary>
        public double AverageCheckDurationMs { get; set; }

        /// <summary>
        /// Last time system health changed
        /// </summary>
        public DateTime LastHealthChange { get; set; }

        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }

        /// <summary>
        /// Number of health status changes in the last hour
        /// </summary>
        public int HealthChangesLastHour { get; set; }
    }
}