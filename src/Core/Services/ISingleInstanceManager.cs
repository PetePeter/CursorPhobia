namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for managing single instance application behavior
/// Prevents multiple instances of CursorPhobia from running simultaneously using named mutex
/// Provides inter-process communication for instance activation
/// </summary>
public interface ISingleInstanceManager : IDisposable
{
    /// <summary>
    /// Event raised when another instance attempts to start and requests activation
    /// </summary>
    event EventHandler<InstanceActivationEventArgs>? InstanceActivationRequested;

    /// <summary>
    /// Gets whether this instance holds the single instance lock
    /// </summary>
    bool IsOwner { get; }

    /// <summary>
    /// Gets whether the single instance manager is currently initialized
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Attempts to acquire the single instance lock
    /// </summary>
    /// <returns>True if this instance is the only one, false if another instance already exists</returns>
    Task<bool> TryAcquireLockAsync();

    /// <summary>
    /// Initializes the named pipe server for inter-process communication
    /// Should be called after successfully acquiring the lock
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Sends an activation request to the existing instance
    /// Should be called when TryAcquireLock returns false
    /// </summary>
    /// <param name="args">Command line arguments to pass to the existing instance</param>
    /// <returns>True if the activation request was sent successfully, false otherwise</returns>
    Task<bool> SendActivationRequestAsync(string[] args);

    /// <summary>
    /// Releases the single instance lock and stops the named pipe server
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// Event arguments for instance activation requests
/// </summary>
public class InstanceActivationEventArgs : EventArgs
{
    /// <summary>
    /// Command line arguments from the requesting instance
    /// </summary>
    public string[] Arguments { get; }

    /// <summary>
    /// Timestamp when the activation request was received
    /// </summary>
    public DateTime RequestTime { get; }

    /// <summary>
    /// Creates new instance activation event arguments
    /// </summary>
    /// <param name="arguments">Command line arguments</param>
    public InstanceActivationEventArgs(string[] arguments)
    {
        Arguments = arguments ?? Array.Empty<string>();
        RequestTime = DateTime.UtcNow;
    }
}