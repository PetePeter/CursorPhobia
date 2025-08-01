namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for managing Windows startup integration
/// </summary>
public interface IStartupManager
{
    /// <summary>
    /// Checks if the application is configured to start with Windows
    /// </summary>
    /// <returns>True if auto-start is enabled</returns>
    Task<bool> IsAutoStartEnabledAsync();

    /// <summary>
    /// Enables the application to start with Windows
    /// </summary>
    /// <returns>True if successfully enabled</returns>
    Task<bool> EnableAutoStartAsync();

    /// <summary>
    /// Disables the application from starting with Windows
    /// </summary>
    /// <returns>True if successfully disabled</returns>
    Task<bool> DisableAutoStartAsync();

    /// <summary>
    /// Gets the command line that would be used for auto-start
    /// </summary>
    /// <returns>The auto-start command line</returns>
    Task<string> GetAutoStartCommandAsync();

    /// <summary>
    /// Gets the registry key path used for startup management
    /// </summary>
    string RegistryKeyPath { get; }

    /// <summary>
    /// Gets the application name used in the registry
    /// </summary>
    string ApplicationName { get; }
}