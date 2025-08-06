using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages single instance application behavior using named mutex and named pipe communication
/// Prevents multiple CursorPhobia instances and enables inter-process communication
/// </summary>
public class SingleInstanceManager : ISingleInstanceManager
{
    private readonly ILogger _logger;
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pipeServerTask;
    private bool _disposed = false;
    private bool _isOwner = false;
    private bool _isInitialized = false;

    /// <summary>
    /// Event raised when another instance attempts to start and requests activation
    /// </summary>
    public event EventHandler<InstanceActivationEventArgs>? InstanceActivationRequested;

    /// <summary>
    /// Gets whether this instance holds the single instance lock
    /// </summary>
    public bool IsOwner => _isOwner && !_disposed;

    /// <summary>
    /// Gets whether the single instance manager is currently initialized
    /// </summary>
    public bool IsInitialized => _isInitialized && !_disposed;

    /// <summary>
    /// Creates a new SingleInstanceManager instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public SingleInstanceManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Generate unique names based on user SID for security
        var userSid = GetCurrentUserSid();
        _mutexName = $"Global\\CursorPhobia-{userSid}";
        _pipeName = $"CursorPhobia-{userSid}";

        _logger.LogDebug("SingleInstanceManager created with mutex: {MutexName}, pipe: {PipeName}", _mutexName, _pipeName);
    }

    /// <summary>
    /// Attempts to acquire the single instance lock
    /// </summary>
    /// <returns>True if this instance is the only one, false if another instance already exists</returns>
    public async Task<bool> TryAcquireLockAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot acquire lock on disposed SingleInstanceManager");
            return false;
        }

        if (_isOwner)
        {
            _logger.LogDebug("Single instance lock already acquired");
            return true;
        }

        try
        {
            _logger.LogDebug("Attempting to acquire single instance lock: {MutexName}", _mutexName);

            // Try to create or open the named mutex
            _mutex = new Mutex(true, _mutexName, out bool createdNew);

            if (createdNew)
            {
                _logger.LogInformation("Successfully acquired single instance lock - this is the primary instance");
                _isOwner = true;
                return true;
            }
            else
            {
                // Another instance exists, try to acquire the mutex with a short timeout
                bool acquired = _mutex.WaitOne(TimeSpan.FromMilliseconds(100));
                if (acquired)
                {
                    _logger.LogInformation("Acquired single instance lock from previous instance");
                    _isOwner = true;
                    return true;
                }
                else
                {
                    _logger.LogInformation("Another CursorPhobia instance is already running");
                    _mutex.Dispose();
                    _mutex = null;
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring single instance lock");
            _mutex?.Dispose();
            _mutex = null;
            return false;
        }
    }

    /// <summary>
    /// Initializes the named pipe server for inter-process communication
    /// Should be called after successfully acquiring the lock
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot initialize disposed SingleInstanceManager");
            return false;
        }

        if (!_isOwner)
        {
            _logger.LogWarning("Cannot initialize SingleInstanceManager without owning the lock");
            return false;
        }

        if (_isInitialized)
        {
            _logger.LogDebug("SingleInstanceManager is already initialized");
            return true;
        }

        try
        {
            _logger.LogDebug("Initializing named pipe server: {PipeName}", _pipeName);

            // Create cancellation token for pipe server
            _cancellationTokenSource = new CancellationTokenSource();

            // Start the named pipe server task
            _pipeServerTask = StartPipeServerAsync(_cancellationTokenSource.Token);

            _isInitialized = true;
            _logger.LogInformation("SingleInstanceManager initialized successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SingleInstanceManager");
            await ShutdownAsync();
            return false;
        }
    }

    /// <summary>
    /// Sends an activation request to the existing instance
    /// Should be called when TryAcquireLock returns false
    /// </summary>
    /// <param name="args">Command line arguments to pass to the existing instance</param>
    /// <returns>True if the activation request was sent successfully, false otherwise</returns>
    public async Task<bool> SendActivationRequestAsync(string[] args)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot send activation request from disposed SingleInstanceManager");
            return false;
        }

        try
        {
            _logger.LogDebug("Sending activation request to existing instance via pipe: {PipeName}", _pipeName);

            var request = new ActivationRequest
            {
                Arguments = args ?? Array.Empty<string>(),
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(request);
            var data = Encoding.UTF8.GetBytes(json);

            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);

            // Connect with timeout
            await client.ConnectAsync(5000);
            await client.WriteAsync(data);
            await client.FlushAsync();

            _logger.LogInformation("Activation request sent successfully to existing instance");
            return true;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout connecting to existing instance - it may be shutting down");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending activation request to existing instance");
            return false;
        }
    }

    /// <summary>
    /// Releases the single instance lock and stops the named pipe server
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_disposed)
            return;

        try
        {
            _logger.LogDebug("Shutting down SingleInstanceManager");

            // Stop the pipe server
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();

                if (_pipeServerTask != null)
                {
                    try
                    {
                        await _pipeServerTask.WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelling
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogWarning("Pipe server shutdown timed out");
                    }
                }
            }

            // Dispose pipe server
            _pipeServer?.Dispose();
            _pipeServer = null;

            // Release mutex
            if (_mutex != null)
            {
                try
                {
                    if (_isOwner)
                    {
                        _mutex.ReleaseMutex();
                        _logger.LogDebug("Released single instance mutex");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing mutex");
                }
                finally
                {
                    _mutex.Dispose();
                    _mutex = null;
                }
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _pipeServerTask = null;

            _isOwner = false;
            _isInitialized = false;

            _logger.LogInformation("SingleInstanceManager shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SingleInstanceManager shutdown");
        }
    }

    /// <summary>
    /// Starts the named pipe server to listen for activation requests
    /// </summary>
    private async Task StartPipeServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create a new pipe server instance
                    _pipeServer = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1, // Max instances
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    _logger.LogDebug("Waiting for pipe client connection");

                    // Wait for client connection
                    await _pipeServer.WaitForConnectionAsync(cancellationToken);

                    _logger.LogDebug("Pipe client connected, reading activation request");

                    // Read the activation request
                    var buffer = new byte[4096];
                    int bytesRead = await _pipeServer.ReadAsync(buffer, cancellationToken);

                    if (bytesRead > 0)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        var request = JsonSerializer.Deserialize<ActivationRequest>(json);

                        if (request != null)
                        {
                            _logger.LogInformation("Received activation request with {ArgCount} arguments", request.Arguments.Length);

                            // Raise the activation event
                            var eventArgs = new InstanceActivationEventArgs(request.Arguments);
                            InstanceActivationRequested?.Invoke(this, eventArgs);
                        }
                    }

                    // Disconnect and dispose this pipe instance
                    _pipeServer.Disconnect();
                    _pipeServer.Dispose();
                    _pipeServer = null;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in pipe server loop");

                    // Wait a bit before retrying
                    try
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down
            _logger.LogDebug("Pipe server task cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in pipe server task");
        }
    }

    /// <summary>
    /// Gets the current user's security identifier for unique naming
    /// </summary>
    private static string GetCurrentUserSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? "Unknown";
        }
        catch
        {
            // Fallback to username if SID is unavailable
            return Environment.UserName ?? "Default";
        }
    }

    /// <summary>
    /// Disposes the single instance manager and releases all resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogDebug("Disposing SingleInstanceManager");

        try
        {
            // Shutdown synchronously with timeout
            var shutdownTask = ShutdownAsync();
            if (!shutdownTask.Wait(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("SingleInstanceManager disposal timed out");
            }

            _disposed = true;
            _logger.LogDebug("SingleInstanceManager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SingleInstanceManager disposal");
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal class for activation request serialization
/// </summary>
internal class ActivationRequest
{
    public string[] Arguments { get; set; } = Array.Empty<string>();
    public DateTime Timestamp { get; set; }
}