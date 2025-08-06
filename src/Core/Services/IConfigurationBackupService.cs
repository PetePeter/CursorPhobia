using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for configuration backup management operations
/// Provides methods to create, restore, and manage configuration backups with automatic cleanup
/// </summary>
public interface IConfigurationBackupService
{
    /// <summary>
    /// Creates a backup of the specified configuration file
    /// Automatically rotates existing backups (1→2, 2→3, 3→delete, new→1)
    /// </summary>
    /// <param name="sourceFilePath">Path to the source configuration file to backup</param>
    /// <param name="backupDirectory">Directory where backups should be stored</param>
    /// <returns>Task representing the async backup operation</returns>
    Task CreateBackupAsync(string sourceFilePath, string backupDirectory);

    /// <summary>
    /// Gets a list of available backup files in the specified directory
    /// Returns backup file paths sorted by backup number (most recent first)
    /// </summary>
    /// <param name="backupDirectory">Directory to search for backup files</param>
    /// <returns>Array of backup file paths sorted by recency</returns>
    Task<string[]> GetAvailableBackupsAsync(string backupDirectory);

    /// <summary>
    /// Restores configuration from the specified backup file
    /// Validates the backup file and deserializes the configuration
    /// </summary>
    /// <param name="backupFilePath">Path to the backup file to restore from</param>
    /// <returns>Configuration loaded from backup, or null if restore fails</returns>
    Task<CursorPhobiaConfiguration?> RestoreFromBackupAsync(string backupFilePath);

    /// <summary>
    /// Cleans up old backup files, keeping only the specified maximum number
    /// Removes the oldest backups first while preserving the most recent ones
    /// </summary>
    /// <param name="backupDirectory">Directory containing backup files</param>
    /// <param name="maxBackups">Maximum number of backups to retain (default: 3)</param>
    /// <returns>Task representing the async cleanup operation</returns>
    Task CleanupOldBackupsAsync(string backupDirectory, int maxBackups = 3);

    /// <summary>
    /// Generates the backup file name for the specified backup number
    /// Follows the naming convention: config.backup.{backupNumber}.json
    /// </summary>
    /// <param name="backupNumber">Backup number (1 = most recent, 3 = oldest)</param>
    /// <returns>Backup file name following the established convention</returns>
    string GetBackupFileName(int backupNumber);
}