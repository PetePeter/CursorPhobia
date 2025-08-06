using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Configuration backup service for creating, managing, and restoring configuration backups
/// Implements automatic backup rotation, cleanup, and atomic backup operations
/// </summary>
public class ConfigurationBackupService : IConfigurationBackupService
{
    // Constants for backup file naming and operations
    private const string BackupFilePrefix = "config.backup.";
    private const string BackupFileExtension = ".json";
    private const string TempFileExtension = ".tmp";
    private const int DefaultMaxBackups = 3;

    // Path validation constants (reused from ConfigurationService)
    private static readonly string[] ForbiddenPathElements = { ".." };
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new ConfigurationBackupService instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public ConfigurationBackupService(ILogger logger)
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
    /// Creates a backup of the specified configuration file
    /// Automatically rotates existing backups (1→2, 2→3, 3→delete, new→1)
    /// </summary>
    /// <param name="sourceFilePath">Path to the source configuration file to backup</param>
    /// <param name="backupDirectory">Directory where backups should be stored</param>
    /// <returns>Task representing the async backup operation</returns>
    public async Task CreateBackupAsync(string sourceFilePath, string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path cannot be null or empty", nameof(sourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));
        }

        try
        {
            // Validate paths to prevent directory traversal attacks
            ValidateBackupPath(sourceFilePath);
            ValidateBackupPath(backupDirectory);

            // Check if source file exists
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogWarning("Source configuration file does not exist, skipping backup: {SourcePath}", sourceFilePath);
                return;
            }

            _logger.LogDebug("Creating backup of {SourcePath} in {BackupDirectory}", sourceFilePath, backupDirectory);

            // Ensure backup directory exists
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                _logger.LogDebug("Created backup directory: {BackupDirectory}", backupDirectory);
            }

            // Rotate existing backups before creating new one
            await RotateBackupsAsync(backupDirectory);

            // Create new backup as backup.1
            var newBackupPath = Path.Combine(backupDirectory, GetBackupFileName(1));
            await CopyFileAtomicallyAsync(sourceFilePath, newBackupPath);

            _logger.LogInformation("Successfully created backup: {BackupPath}", newBackupPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while creating backup of {SourcePath}", sourceFilePath);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found while creating backup of {SourcePath}", sourceFilePath);
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error while creating backup of {SourcePath}", sourceFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating backup of {SourcePath}", sourceFilePath);
            throw;
        }
    }

    /// <summary>
    /// Gets a list of available backup files in the specified directory
    /// Returns backup file paths sorted by backup number (most recent first)
    /// </summary>
    /// <param name="backupDirectory">Directory to search for backup files</param>
    /// <returns>Array of backup file paths sorted by recency</returns>
    public async Task<string[]> GetAvailableBackupsAsync(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));
        }

        try
        {
            ValidateBackupPath(backupDirectory);

            if (!Directory.Exists(backupDirectory))
            {
                _logger.LogDebug("Backup directory does not exist: {BackupDirectory}", backupDirectory);
                return Array.Empty<string>();
            }

            // Find all backup files that match our naming pattern
            var backupFiles = new List<(string filePath, int backupNumber, DateTime lastWrite)>();

            foreach (var filePath in Directory.GetFiles(backupDirectory, $"{BackupFilePrefix}*{BackupFileExtension}"))
            {
                var fileName = Path.GetFileName(filePath);
                if (TryParseBackupFileName(fileName, out var backupNumber))
                {
                    var lastWriteTime = File.GetLastWriteTime(filePath);
                    backupFiles.Add((filePath, backupNumber, lastWriteTime));
                }
            }

            // Sort by backup number (1 = most recent first), then by last write time as fallback
            var sortedBackups = backupFiles
                .OrderBy(b => b.backupNumber)
                .ThenByDescending(b => b.lastWrite)
                .Select(b => b.filePath)
                .ToArray();

            _logger.LogDebug("Found {Count} backup files in {BackupDirectory}", sortedBackups.Length, backupDirectory);
            return await Task.FromResult(sortedBackups);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while listing backups in {BackupDirectory}", backupDirectory);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found while listing backups in {BackupDirectory}", backupDirectory);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while listing backups in {BackupDirectory}", backupDirectory);
            throw;
        }
    }

    /// <summary>
    /// Restores configuration from the specified backup file
    /// Validates the backup file and deserializes the configuration
    /// </summary>
    /// <param name="backupFilePath">Path to the backup file to restore from</param>
    /// <returns>Configuration loaded from backup, or null if restore fails</returns>
    public async Task<CursorPhobiaConfiguration?> RestoreFromBackupAsync(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath))
        {
            throw new ArgumentException("Backup file path cannot be null or empty", nameof(backupFilePath));
        }

        try
        {
            ValidateBackupPath(backupFilePath);

            if (!File.Exists(backupFilePath))
            {
                _logger.LogWarning("Backup file does not exist: {BackupPath}", backupFilePath);
                return null;
            }

            _logger.LogDebug("Restoring configuration from backup: {BackupPath}", backupFilePath);

            // Read and deserialize the backup file
            string jsonContent;
            using (var fileStream = new FileStream(backupFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fileStream))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                _logger.LogWarning("Backup file is empty: {BackupPath}", backupFilePath);
                return null;
            }

            var configuration = JsonSerializer.Deserialize<CursorPhobiaConfiguration>(jsonContent, _jsonOptions);

            if (configuration == null)
            {
                _logger.LogWarning("Failed to deserialize configuration from backup: {BackupPath}", backupFilePath);
                return null;
            }

            // Validate the restored configuration
            var validationErrors = configuration.Validate();
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning("Restored configuration has validation errors: {Errors}. Backup: {BackupPath}",
                    string.Join(", ", validationErrors), backupFilePath);
                return null;
            }

            _logger.LogInformation("Successfully restored configuration from backup: {BackupPath}", backupFilePath);
            return configuration;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while restoring from backup: {BackupPath}", backupFilePath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while restoring from backup: {BackupPath}", backupFilePath);
            return null;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogError(ex, "Directory not found while restoring from backup: {BackupPath}", backupFilePath);
            return null;
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "File not found while restoring from backup: {BackupPath}", backupFilePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while restoring from backup: {BackupPath}", backupFilePath);
            return null;
        }
    }

    /// <summary>
    /// Cleans up old backup files, keeping only the specified maximum number
    /// Removes the oldest backups first while preserving the most recent ones
    /// </summary>
    /// <param name="backupDirectory">Directory containing backup files</param>
    /// <param name="maxBackups">Maximum number of backups to retain (default: 3)</param>
    /// <returns>Task representing the async cleanup operation</returns>
    public async Task CleanupOldBackupsAsync(string backupDirectory, int maxBackups = DefaultMaxBackups)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));
        }

        if (maxBackups < 1)
        {
            throw new ArgumentException("Maximum backups must be at least 1", nameof(maxBackups));
        }

        try
        {
            ValidateBackupPath(backupDirectory);

            if (!Directory.Exists(backupDirectory))
            {
                _logger.LogDebug("Backup directory does not exist, no cleanup needed: {BackupDirectory}", backupDirectory);
                return;
            }

            var availableBackups = await GetAvailableBackupsAsync(backupDirectory);

            if (availableBackups.Length <= maxBackups)
            {
                _logger.LogDebug("Found {Count} backups, within limit of {MaxBackups}, no cleanup needed",
                    availableBackups.Length, maxBackups);
                return;
            }

            // Remove the oldest backups (those beyond the maxBackups limit)
            var backupsToRemove = availableBackups.Skip(maxBackups).ToArray();

            foreach (var backupPath in backupsToRemove)
            {
                try
                {
                    File.Delete(backupPath);
                    _logger.LogDebug("Deleted old backup: {BackupPath}", backupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to delete old backup: {BackupPath}. Exception: {Exception}", backupPath, ex.Message);
                    // Continue with other backups even if one fails
                }
            }

            _logger.LogInformation("Cleaned up {Count} old backups, retained {Retained} most recent backups",
                backupsToRemove.Length, Math.Min(availableBackups.Length, maxBackups));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cleaning up backups in {BackupDirectory}", backupDirectory);
            throw;
        }
    }

    /// <summary>
    /// Generates the backup file name for the specified backup number
    /// Follows the naming convention: config.backup.{backupNumber}.json
    /// </summary>
    /// <param name="backupNumber">Backup number (1 = most recent, 3 = oldest)</param>
    /// <returns>Backup file name following the established convention</returns>
    public string GetBackupFileName(int backupNumber)
    {
        if (backupNumber < 1)
        {
            throw new ArgumentException("Backup number must be at least 1", nameof(backupNumber));
        }

        return $"{BackupFilePrefix}{backupNumber}{BackupFileExtension}";
    }

    /// <summary>
    /// Rotates existing backup files to make room for a new backup
    /// Moves backup.1 → backup.2, backup.2 → backup.3, deletes backup.3 if it exists
    /// </summary>
    /// <param name="backupDirectory">Directory containing backup files</param>
    /// <returns>Task representing the async rotation operation</returns>
    private async Task RotateBackupsAsync(string backupDirectory)
    {
        try
        {
            // Rotate in reverse order to avoid conflicts (3→delete, 2→3, 1→2)
            for (int i = DefaultMaxBackups; i >= 1; i--)
            {
                var currentBackupPath = Path.Combine(backupDirectory, GetBackupFileName(i));

                if (File.Exists(currentBackupPath))
                {
                    if (i == DefaultMaxBackups)
                    {
                        // Delete the oldest backup
                        File.Delete(currentBackupPath);
                        _logger.LogDebug("Deleted oldest backup: {BackupPath}", currentBackupPath);
                    }
                    else
                    {
                        // Move to next backup number
                        var nextBackupPath = Path.Combine(backupDirectory, GetBackupFileName(i + 1));
                        File.Move(currentBackupPath, nextBackupPath);
                        _logger.LogDebug("Rotated backup {Current} → {Next}", currentBackupPath, nextBackupPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backup rotation in {BackupDirectory}", backupDirectory);
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Copies a file atomically using a temporary file approach
    /// Ensures the destination file is never in a partially written state
    /// </summary>
    /// <param name="sourcePath">Source file path</param>
    /// <param name="destinationPath">Destination file path</param>
    /// <returns>Task representing the async copy operation</returns>
    private async Task CopyFileAtomicallyAsync(string sourcePath, string destinationPath)
    {
        var tempFilePath = destinationPath + TempFileExtension;

        try
        {
            // Copy to temporary file first
            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destinationStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await sourceStream.CopyToAsync(destinationStream);
                await destinationStream.FlushAsync();
            }

            // Atomic replacement
            if (File.Exists(destinationPath))
            {
                File.Replace(tempFilePath, destinationPath, null);
            }
            else
            {
                File.Move(tempFilePath, destinationPath);
            }
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
                _logger.LogWarning("Failed to clean up temporary backup file: {TempFile}. Exception: {Exception}", tempFilePath, cleanupEx.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// Tries to parse a backup file name and extract the backup number
    /// </summary>
    /// <param name="fileName">File name to parse</param>
    /// <param name="backupNumber">Extracted backup number if successful</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    private bool TryParseBackupFileName(string fileName, out int backupNumber)
    {
        backupNumber = 0;

        if (string.IsNullOrEmpty(fileName) ||
            !fileName.StartsWith(BackupFilePrefix) ||
            !fileName.EndsWith(BackupFileExtension))
        {
            return false;
        }

        // Extract the number part between prefix and extension
        var startIndex = BackupFilePrefix.Length;
        var endIndex = fileName.Length - BackupFileExtension.Length;

        if (startIndex >= endIndex)
        {
            return false;
        }

        var numberPart = fileName.Substring(startIndex, endIndex - startIndex);
        return int.TryParse(numberPart, out backupNumber) && backupNumber > 0;
    }

    /// <summary>
    /// Validates a backup-related path to prevent directory traversal attacks and ensure path safety
    /// Uses the same validation logic as ConfigurationService for consistency
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <exception cref="ArgumentException">Thrown when the path contains suspicious patterns</exception>
    private void ValidateBackupPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        try
        {
            // Check for directory traversal patterns
            var normalizedPath = path.Replace('\\', '/');
            if (ForbiddenPathElements.Any(element => normalizedPath.Contains(element)))
            {
                throw new ArgumentException($"Path contains directory traversal patterns: {path}", nameof(path));
            }

            // Check for invalid path characters (but allow drive colons on Windows)
            var pathToCheck = path;
            if (Path.IsPathRooted(path) && path.Length >= 2 && path[1] == ':')
            {
                // Skip the drive letter part for Windows paths (e.g., "C:" part)
                pathToCheck = path.Substring(2);
            }

            if (pathToCheck.IndexOfAny(InvalidPathChars) >= 0)
            {
                throw new ArgumentException($"Path contains invalid characters: {path}", nameof(path));
            }

            // Check filename for invalid characters if this is a file path
            if (File.Exists(path) || Path.HasExtension(path))
            {
                var fileName = Path.GetFileName(path);
                if (!string.IsNullOrEmpty(fileName) && fileName.IndexOfAny(InvalidFileNameChars) >= 0)
                {
                    throw new ArgumentException($"Filename contains invalid characters: {fileName}", nameof(path));
                }
            }

            // Get the full path to check for suspicious absolute paths
            var fullPath = Path.GetFullPath(path);

            // Prevent access to system directories (basic check)
            var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            if (!string.IsNullOrEmpty(systemPath) && fullPath.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to system directory not allowed: {fullPath}", nameof(path));
            }

            if (!string.IsNullOrEmpty(windowsPath) && fullPath.StartsWith(windowsPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to Windows directory not allowed: {fullPath}", nameof(path));
            }

            if (!string.IsNullOrEmpty(programFilesPath) && fullPath.StartsWith(programFilesPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Access to Program Files directory not allowed: {fullPath}", nameof(path));
            }

            _logger.LogDebug("Backup path validation successful for: {Path}", path);
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            _logger.LogError(ex, "Error during backup path validation for: {Path}", path);
            throw new ArgumentException($"Invalid path format: {path}", nameof(path), ex);
        }
    }
}