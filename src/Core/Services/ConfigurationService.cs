using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Configuration service for persisting CursorPhobia configuration to JSON files
/// Implements atomic writes, error handling, and default value fallbacks
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    
    /// <summary>
    /// Creates a new ConfigurationService instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public ConfigurationService(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure JSON serialization options for human-readable output
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
    
    /// <summary>
    /// Loads configuration from the specified file path
    /// Returns default configuration if file doesn't exist or is corrupt
    /// </summary>
    /// <param name="filePath">Path to the configuration JSON file</param>
    /// <returns>Loaded configuration or defaults if loading fails</returns>
    public async Task<CursorPhobiaConfiguration> LoadConfigurationAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogError("LoadConfigurationAsync called with null or empty filePath");
            return GetDefaultConfiguration();
        }
        
        try
        {
            // Check if file exists
            if (!File.Exists(filePath))
            {
                _logger.LogInformation("Configuration file does not exist: {FilePath}. Creating default configuration.", filePath);
                var defaultConfig = GetDefaultConfiguration();
                
                // Save default configuration to the file
                await SaveConfigurationAsync(defaultConfig, filePath);
                return defaultConfig;
            }
            
            _logger.LogDebug("Loading configuration from: {FilePath}", filePath);
            
            // Read and deserialize the configuration file
            string jsonContent;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fileStream))
            {
                jsonContent = await reader.ReadToEndAsync();
            }
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Configuration file is empty: {FilePath}. Using default configuration.", filePath);
                return GetDefaultConfiguration();
            }
            
            var configuration = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(jsonContent, _jsonOptions);
            
            if (configuration == null)
            {
                _logger.LogWarning("Failed to deserialize configuration from {FilePath}. Using default configuration.", filePath);
                return GetDefaultConfiguration();
            }
            
            // Validate the loaded configuration
            var validationErrors = configuration.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning("Loaded configuration has validation errors: {Errors}. Using default configuration.", 
                    string.Join(", ", validationErrors));
                return GetDefaultConfiguration();
            }
            
            _logger.LogInformation("Successfully loaded configuration from: {FilePath}", filePath);
            return configuration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while loading configuration from {FilePath}. Using default configuration.", filePath);
            return GetDefaultConfiguration();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while loading configuration from {FilePath}. Using default configuration.", filePath);
            return GetDefaultConfiguration();
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found while loading configuration from {FilePath}. Using default configuration.", filePath);
            return GetDefaultConfiguration();
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found while loading configuration from {FilePath}. Using default configuration.", filePath);
            return GetDefaultConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading configuration from {FilePath}. Using default configuration.", filePath);
            return GetDefaultConfiguration();
        }
    }
    
    /// <summary>
    /// Saves configuration to the specified file path using atomic writes
    /// Creates directories as needed and handles write failures gracefully
    /// </summary>
    /// <param name="config">Configuration to save</param>
    /// <param name="filePath">Path where to save the configuration JSON file</param>
    /// <returns>Task representing the async save operation</returns>
    public async Task SaveConfigurationAsync(CursorPhobiaConfiguration config, string filePath)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }
        
        try
        {
            // Validate configuration before saving
            var validationErrors = config.Validate();
            if (validationErrors.Count > 0)
            {
                throw new ArgumentException($"Invalid configuration: {string.Join(", ", validationErrors)}");
            }
            
            _logger.LogDebug("Saving configuration to: {FilePath}", filePath);
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }
            
            // Use atomic write: write to temp file first, then rename
            var tempFilePath = filePath + ".tmp";
            
            try
            {
                // Serialize configuration to JSON
                var jsonContent = JsonSerializer.Serialize(config, _jsonOptions);
                
                // Write to temporary file
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fileStream))
                {
                    await writer.WriteAsync(jsonContent);
                    await writer.FlushAsync();
                }
                
                // Atomic rename - this ensures we never have a partially written config file
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                File.Move(tempFilePath, filePath);
                
                _logger.LogInformation("Successfully saved configuration to: {FilePath}", filePath);
            }
            catch
            {
                // Clean up temp file if something went wrong
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning("Failed to clean up temporary file: {TempFile}. Exception: {Exception}", tempFilePath, cleanupEx.Message);
                }
                
                throw; // Re-throw the original exception
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while saving configuration to {FilePath}", filePath);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found while saving configuration to {FilePath}", filePath);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error while saving configuration to {FilePath}", filePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while saving configuration to {FilePath}", filePath);
            throw;
        }
    }
    
    /// <summary>
    /// Gets a default configuration instance with recommended settings
    /// </summary>
    /// <returns>Default configuration instance</returns>
    public CursorPhobiaConfiguration GetDefaultConfiguration()
    {
        _logger.LogDebug("Creating default configuration");
        return CursorPhobiaConfiguration.CreateDefault();
    }
    
    /// <summary>
    /// Gets the default configuration file path (%APPDATA%\CursorPhobia\config.json)
    /// </summary>
    /// <returns>Default configuration file path</returns>
    public async Task<string> GetDefaultConfigurationPathAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appDataPath))
                {
                    _logger.LogWarning("Could not determine APPDATA folder path. Using current directory.");
                    appDataPath = Environment.CurrentDirectory;
                }
                    
                var cursorPhobiaDirectory = Path.Combine(appDataPath, "CursorPhobia");
                var configFilePath = Path.Combine(cursorPhobiaDirectory, "config.json");
                
                _logger.LogDebug("Default configuration path: {ConfigPath}", configFilePath);
                return configFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining default configuration path. Using fallback.");
                return Path.Combine(Environment.CurrentDirectory, "CursorPhobia", "config.json");
            }
        });
    }
}