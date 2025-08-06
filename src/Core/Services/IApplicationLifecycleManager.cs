namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for managing application lifecycle events and clean shutdown
/// Coordinates between engine, tray manager, and configuration services
/// </summary>
public interface IApplicationLifecycleManager : IDisposable
{
    /// <summary>
    /// Event raised when the application should exit
    /// </summary>
    event EventHandler? ApplicationExitRequested;

    /// <summary>
    /// Gets whether the lifecycle manager is currently initialized
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets whether a shutdown is currently in progress
    /// </summary>
    bool IsShuttingDown { get; }

    /// <summary>
    /// Initializes the application lifecycle manager
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Initiates a graceful shutdown of all application components
    /// </summary>
    /// <param name="exitCode">Exit code for the application</param>
    Task ShutdownAsync(int exitCode = 0);

    /// <summary>
    /// Registers a service for lifecycle management
    /// Services are disposed in reverse order of registration during shutdown
    /// </summary>
    /// <param name="service">Service to register for disposal</param>
    /// <param name="name">Optional name for the service (for logging purposes)</param>
    void RegisterService(IDisposable service, string? name = null);

    /// <summary>
    /// Unregisters a service from lifecycle management
    /// </summary>
    /// <param name="service">Service to unregister</param>
    void UnregisterService(IDisposable service);
}