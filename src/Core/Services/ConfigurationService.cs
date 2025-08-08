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
    // Constants for magic strings
    private const string TempFileExtension = ".tmp";
    private const string DefaultConfigFileName = "config.json";
    private const string ApplicationDirectoryName = "CursorPhobia";

    // Path validation constants
    private static readonly string[] ForbiddenPathElements = { ".." };
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IConfigurationBackupService? _backupService;

    /// <summary>
    /// Creates a new ConfigurationService instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public ConfigurationService(ILogger logger) : this(logger, null)
    {
    }

    /// <summary>
    /// Creates a new ConfigurationService instance with backup support
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="backupService">Optional backup service for automatic backup creation</param>
    public ConfigurationService(ILogger logger, IConfigurationBackupService? backupService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backupService = backupService;

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

            // Apply migration logic for old configurations
            var migratedConfig = MigrateConfiguration(configuration, filePath);

            // Validate the migrated configuration
            var validationErrors = migratedConfig.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning("Migrated configuration has validation errors: {Errors}. Using default configuration.",
                    string.Join(", ", validationErrors));
                return GetDefaultConfiguration();
            }

            _logger.LogInformation("Successfully loaded and migrated configuration from: {FilePath}", filePath);
            return migratedConfig;
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

            // Validate the file path to prevent directory traversal attacks
            ValidateConfigurationPath(filePath);

            // Create backup if backup service is available and the file already exists
            if (_backupService != null && File.Exists(filePath))
            {
                try
                {
                    var backupDirectory = directory ?? Path.GetDirectoryName(Path.GetFullPath(filePath));
                    if (!string.IsNullOrEmpty(backupDirectory))
                    {
                        await _backupService.CreateBackupAsync(filePath, backupDirectory);
                        _logger.LogDebug("Created backup before saving configuration");
                    }
                }
                catch (Exception backupEx)
                {
                    // Backup failure should not prevent config save
                    _logger.LogWarning("Failed to create backup before saving configuration, continuing with save operation. Exception: {Exception}", backupEx.Message);
                }
            }

            // Use atomic write: write to temp file first, then rename
            var tempFilePath = filePath + TempFileExtension;

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

                // Atomic replacement - this ensures we never have a partially written config file
                // Use File.Replace for true atomic operation that handles existing files correctly
                if (File.Exists(filePath))
                {
                    // File.Replace handles the atomic replacement operation
                    File.Replace(tempFilePath, filePath, null);
                }
                else
                {
                    // If target doesn't exist, just move the temp file
                    File.Move(tempFilePath, filePath);
                }

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
        return await Task.FromResult(GetDefaultConfigurationPath());
    }

    /// <summary>
    /// Gets the default configuration file path (%APPDATA%\CursorPhobia\config.json)
    /// This is a synchronous operation since no async work is performed
    /// </summary>
    /// <returns>Default configuration file path</returns>
    private string GetDefaultConfigurationPath()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appDataPath))
            {
                _logger.LogWarning("Could not determine APPDATA folder path. Using current directory.");
                appDataPath = Environment.CurrentDirectory;
            }

            var cursorPhobiaDirectory = Path.Combine(appDataPath, ApplicationDirectoryName);
            var configFilePath = Path.Combine(cursorPhobiaDirectory, DefaultConfigFileName);

            _logger.LogDebug("Default configuration path: {ConfigPath}", configFilePath);
            return configFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining default configuration path. Using fallback.");
            return Path.Combine(Environment.CurrentDirectory, ApplicationDirectoryName, DefaultConfigFileName);
        }
    }

    /// <summary>
    /// Validates a configuration file path to prevent directory traversal attacks and ensure path safety
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <exception cref="ArgumentException">Thrown when the path contains suspicious patterns</exception>
    private void ValidateConfigurationPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            // Check for directory traversal patterns
            var normalizedPath = filePath.Replace('\\', '/');
            if (ForbiddenPathElements.Any(element => normalizedPath.Contains(element)))
            {
                throw new ArgumentException($"Path contains directory traversal patterns: {filePath}", nameof(filePath));
            }

            // Check for invalid path characters (but allow drive colons on Windows)
            var pathToCheck = filePath;
            if (Path.IsPathRooted(filePath) && filePath.Length >= 2 && filePath[1] == ':')
            {
                // Skip the drive letter part for Windows paths (e.g., "C:" part)
                pathToCheck = filePath.Substring(2);
            }

            if (pathToCheck.IndexOfAny(InvalidPathChars) >= 0)
            {
                throw new ArgumentException($"Path contains invalid characters: {filePath}", nameof(filePath));
            }

            // Check filename for invalid characters
            var fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrEmpty(fileName) && fileName.IndexOfAny(InvalidFileNameChars) >= 0)
            {
                throw new ArgumentException($"Filename contains invalid characters: {fileName}", nameof(filePath));
            }

            // Get the full path to check for suspicious absolute paths
            var fullPath = Path.GetFullPath(filePath);

            // Prevent access to system directories (basic check)
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            if (!string.IsNullOrEmpty(systemPath) && fullPath.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to system directory not allowed: {fullPath}", nameof(filePath));
            }

            if (!string.IsNullOrEmpty(windowsPath) && fullPath.StartsWith(windowsPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to Windows directory not allowed: {fullPath}", nameof(filePath));
            }

            if (!string.IsNullOrEmpty(programFilesPath) && fullPath.StartsWith(programFilesPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to Program Files directory not allowed: {fullPath}", nameof(filePath));
            }

            _logger.LogDebug("Path validation successful for: {FilePath}", filePath);
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            _logger.LogError(ex, "Error during path validation for: {FilePath}", filePath);
            throw new ArgumentException($"Invalid path format: {filePath}", nameof(filePath), ex);
        }
    }

    /// <summary>
    /// Migrates old configuration files to ensure they use hardcoded defaults for obsolete properties
    /// and gracefully handles out-of-range values by resetting them to optimal defaults
    /// </summary>
    /// <param name="config">Configuration loaded from file</param>
    /// <param name="filePath">File path for logging purposes</param>
    /// <returns>Migrated configuration with hardcoded defaults applied</returns>
    private CursorPhobiaConfiguration MigrateConfiguration(CursorPhobiaConfiguration config, string filePath)
    {
        var wasMigrated = false;
        var migrationMessages = new List<string>();

        // Check if obsolete properties have non-hardcoded values and migrate them
        if (config.UpdateIntervalMs != HardcodedDefaults.UpdateIntervalMs)
        {
            _logger.LogInformation("Migrating UpdateIntervalMs from {OldValue}ms to hardcoded default {NewValue}ms for config: {FilePath}",
                config.UpdateIntervalMs, HardcodedDefaults.UpdateIntervalMs, filePath);
            var oldValue = config.UpdateIntervalMs;
            config.UpdateIntervalMs = HardcodedDefaults.UpdateIntervalMs;
            migrationMessages.Add($"UpdateIntervalMs: {oldValue} → {HardcodedDefaults.UpdateIntervalMs}");
            wasMigrated = true;
        }

        if (config.MaxUpdateIntervalMs != HardcodedDefaults.MaxUpdateIntervalMs)
        {
            _logger.LogInformation("Migrating MaxUpdateIntervalMs from {OldValue}ms to hardcoded default {NewValue}ms for config: {FilePath}",
                config.MaxUpdateIntervalMs, HardcodedDefaults.MaxUpdateIntervalMs, filePath);
            var oldValue = config.MaxUpdateIntervalMs;
            config.MaxUpdateIntervalMs = HardcodedDefaults.MaxUpdateIntervalMs;
            migrationMessages.Add($"MaxUpdateIntervalMs: {oldValue} → {HardcodedDefaults.MaxUpdateIntervalMs}");
            wasMigrated = true;
        }

        if (config.ScreenEdgeBuffer != HardcodedDefaults.ScreenEdgeBuffer)
        {
            _logger.LogInformation("Migrating ScreenEdgeBuffer from {OldValue}px to hardcoded default {NewValue}px for config: {FilePath}",
                config.ScreenEdgeBuffer, HardcodedDefaults.ScreenEdgeBuffer, filePath);
            var oldValue = config.ScreenEdgeBuffer;
            config.ScreenEdgeBuffer = HardcodedDefaults.ScreenEdgeBuffer;
            migrationMessages.Add($"ScreenEdgeBuffer: {oldValue} → {HardcodedDefaults.ScreenEdgeBuffer}");
            wasMigrated = true;
        }

        if (config.CtrlReleaseToleranceDistance != HardcodedDefaults.CtrlReleaseToleranceDistance)
        {
            _logger.LogInformation("Migrating CtrlReleaseToleranceDistance from {OldValue}px to hardcoded default {NewValue}px for config: {FilePath}",
                config.CtrlReleaseToleranceDistance, HardcodedDefaults.CtrlReleaseToleranceDistance, filePath);
            var oldValue = config.CtrlReleaseToleranceDistance;
            config.CtrlReleaseToleranceDistance = HardcodedDefaults.CtrlReleaseToleranceDistance;
            migrationMessages.Add($"CtrlReleaseToleranceDistance: {oldValue} → {HardcodedDefaults.CtrlReleaseToleranceDistance}");
            wasMigrated = true;
        }

        if (config.AlwaysOnTopRepelBorderDistance != HardcodedDefaults.AlwaysOnTopRepelBorderDistance)
        {
            _logger.LogInformation("Migrating AlwaysOnTopRepelBorderDistance from {OldValue}px to hardcoded default {NewValue}px for config: {FilePath}",
                config.AlwaysOnTopRepelBorderDistance, HardcodedDefaults.AlwaysOnTopRepelBorderDistance, filePath);
            var oldValue = config.AlwaysOnTopRepelBorderDistance;
            config.AlwaysOnTopRepelBorderDistance = HardcodedDefaults.AlwaysOnTopRepelBorderDistance;
            migrationMessages.Add($"AlwaysOnTopRepelBorderDistance: {oldValue} → {HardcodedDefaults.AlwaysOnTopRepelBorderDistance}");
            wasMigrated = true;
        }

        if (config.AnimationDurationMs != HardcodedDefaults.AnimationDurationMs)
        {
            _logger.LogInformation("Migrating AnimationDurationMs from {OldValue}ms to hardcoded default {NewValue}ms for config: {FilePath}",
                config.AnimationDurationMs, HardcodedDefaults.AnimationDurationMs, filePath);
            var oldValue = config.AnimationDurationMs;
            config.AnimationDurationMs = HardcodedDefaults.AnimationDurationMs;
            migrationMessages.Add($"AnimationDurationMs: {oldValue} → {HardcodedDefaults.AnimationDurationMs}");
            wasMigrated = true;
        }

        if (config.EnableAnimations != HardcodedDefaults.EnableAnimations)
        {
            _logger.LogInformation("Migrating EnableAnimations from {OldValue} to hardcoded default {NewValue} for config: {FilePath}",
                config.EnableAnimations, HardcodedDefaults.EnableAnimations, filePath);
            var oldValue = config.EnableAnimations;
            config.EnableAnimations = HardcodedDefaults.EnableAnimations;
            migrationMessages.Add($"EnableAnimations: {oldValue} → {HardcodedDefaults.EnableAnimations}");
            wasMigrated = true;
        }

        if (config.AnimationEasing != HardcodedDefaults.AnimationEasing)
        {
            _logger.LogInformation("Migrating AnimationEasing from {OldValue} to hardcoded default {NewValue} for config: {FilePath}",
                config.AnimationEasing, HardcodedDefaults.AnimationEasing, filePath);
            var oldValue = config.AnimationEasing;
            config.AnimationEasing = HardcodedDefaults.AnimationEasing;
            migrationMessages.Add($"AnimationEasing: {oldValue} → {HardcodedDefaults.AnimationEasing}");
            wasMigrated = true;
        }

        // Apply graceful degradation for user-configurable properties that are out of range
        wasMigrated |= ApplyGracefulDegradation(config, filePath, migrationMessages);

        if (wasMigrated)
        {
            _logger.LogInformation("Configuration migration completed for {FilePath}. Migrated settings: {MigratedSettings}",
                filePath, string.Join(", ", migrationMessages));
        }
        else
        {
            _logger.LogDebug("No migration needed for configuration: {FilePath}", filePath);
        }

        return config;
    }

    /// <summary>
    /// Applies graceful degradation for user-configurable properties that are outside optimal ranges
    /// </summary>
    /// <param name="config">Configuration to check and potentially modify</param>
    /// <param name="filePath">File path for logging purposes</param>
    /// <param name="migrationMessages">List to add migration messages to</param>
    /// <returns>True if any values were modified, false otherwise</returns>
    private bool ApplyGracefulDegradation(CursorPhobiaConfiguration config, string filePath, List<string> migrationMessages)
    {
        var wasModified = false;

        // Graceful degradation for ProximityThreshold
        if (config.ProximityThreshold < 10 || config.ProximityThreshold > 500)
        {
            var oldValue = config.ProximityThreshold;
            config.ProximityThreshold = HardcodedDefaults.ProximityThreshold;
            _logger.LogWarning("ProximityThreshold value {OldValue} is outside optimal range (10-500px). " +
                "Applying graceful degradation to {NewValue}px for config: {FilePath}",
                oldValue, config.ProximityThreshold, filePath);
            migrationMessages.Add($"ProximityThreshold: {oldValue} → {config.ProximityThreshold} (out-of-range)");
            wasModified = true;
        }

        // Graceful degradation for PushDistance
        if (config.PushDistance < 10 || config.PushDistance > 1000)
        {
            var oldValue = config.PushDistance;
            config.PushDistance = HardcodedDefaults.PushDistance;
            _logger.LogWarning("PushDistance value {OldValue} is outside optimal range (10-1000px). " +
                "Applying graceful degradation to {NewValue}px for config: {FilePath}",
                oldValue, config.PushDistance, filePath);
            migrationMessages.Add($"PushDistance: {oldValue} → {config.PushDistance} (out-of-range)");
            wasModified = true;
        }

        // Graceful degradation for HoverTimeoutMs
        if (config.HoverTimeoutMs < 100 || config.HoverTimeoutMs > 30000)
        {
            var oldValue = config.HoverTimeoutMs;
            config.HoverTimeoutMs = HardcodedDefaults.HoverTimeoutMs; // Default hover timeout
            _logger.LogWarning("HoverTimeoutMs value {OldValue} is outside optimal range (100-30000ms). " +
                "Applying graceful degradation to {NewValue}ms for config: {FilePath}",
                oldValue, config.HoverTimeoutMs, filePath);
            migrationMessages.Add($"HoverTimeoutMs: {oldValue} → {config.HoverTimeoutMs} (out-of-range)");
            wasModified = true;
        }

        return wasModified;
    }
}