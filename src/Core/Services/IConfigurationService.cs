using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for configuration persistence operations
/// Provides methods to save/load CursorPhobia configuration to/from JSON files with error handling
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Loads configuration from the specified file path
    /// Returns default configuration if file doesn't exist or is corrupt
    /// </summary>
    /// <param name="filePath">Path to the configuration JSON file</param>
    /// <returns>Loaded configuration or defaults if loading fails</returns>
    Task<CursorPhobiaConfiguration> LoadConfigurationAsync(string filePath);
    
    /// <summary>
    /// Saves configuration to the specified file path using atomic writes
    /// Creates directories as needed and handles write failures gracefully
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="filePath">Path where to save the configuration JSON file</param>
    /// <returns>Task representing the async save operation</returns>
    Task SaveConfigurationAsync(CursorPhobiaConfiguration config, string filePath);
    
    /// <summary>
    /// Gets a default configuration instance with recommended settings
    /// </summary>
    /// <returns>Default configuration instance</returns>
    CursorPhobiaConfiguration GetDefaultConfiguration();
    
    /// <summary>
    /// Gets the default configuration file path (%APPDATA%\CursorPhobia\config.json)
    /// </summary>
    /// <returns>Default configuration file path</returns>
    Task<string> GetDefaultConfigurationPathAsync();
}