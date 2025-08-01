namespace CursorPhobia.Core.Models;

/// <summary>
/// Result of a configuration update operation
/// </summary>
public class ConfigurationUpdateResult
{
    /// <summary>
    /// Whether the configuration update was successful
    /// </summary>
    public bool Success { get; }
    
    /// <summary>
    /// Error message if the update failed
    /// </summary>
    public string? ErrorMessage { get; }
    
    /// <summary>
    /// The configuration change analysis that was performed
    /// </summary>
    public ConfigurationChangeAnalysis ChangeAnalysis { get; }
    
    /// <summary>
    /// Settings that were successfully applied immediately
    /// </summary>
    public List<string> AppliedChanges { get; } = new();
    
    /// <summary>
    /// Settings that are queued for next engine restart
    /// </summary>
    public List<string> QueuedForRestart { get; } = new();
    
    /// <summary>
    /// Validation errors found in the new configuration
    /// </summary>
    public List<string> ValidationErrors { get; } = new();
    
    /// <summary>
    /// Whether the engine restart is required for all changes to take effect
    /// </summary>
    public bool RequiresRestart => QueuedForRestart.Count > 0;
    
    /// <summary>
    /// Timestamp when the update was processed
    /// </summary>
    public DateTime Timestamp { get; }
    
    /// <summary>
    /// Creates a successful configuration update result
    /// </summary>
    /// <param name="changeAnalysis">The configuration change analysis</param>
    /// <param name="appliedChanges">Settings that were applied immediately</param>
    /// <param name="queuedForRestart">Settings queued for restart</param>
    /// <returns>Successful update result</returns>
    public static ConfigurationUpdateResult CreateSuccess(
        ConfigurationChangeAnalysis changeAnalysis,
        IEnumerable<string>? appliedChanges = null,
        IEnumerable<string>? queuedForRestart = null)
    {
        var result = new ConfigurationUpdateResult(true, null, changeAnalysis);
        
        if (appliedChanges != null)
            result.AppliedChanges.AddRange(appliedChanges);
            
        if (queuedForRestart != null)
            result.QueuedForRestart.AddRange(queuedForRestart);
        
        return result;
    }
    
    /// <summary>
    /// Creates a failed configuration update result
    /// </summary>
    /// <param name="errorMessage">Error message describing the failure</param>
    /// <param name="changeAnalysis">The configuration change analysis</param>
    /// <param name="validationErrors">Validation errors if any</param>
    /// <returns>Failed update result</returns>
    public static ConfigurationUpdateResult CreateFailure(
        string errorMessage,
        ConfigurationChangeAnalysis changeAnalysis,
        IEnumerable<string>? validationErrors = null)
    {
        var result = new ConfigurationUpdateResult(false, errorMessage, changeAnalysis);
        
        if (validationErrors != null)
            result.ValidationErrors.AddRange(validationErrors);
        
        return result;
    }
    
    /// <summary>
    /// Creates a configuration update result with validation errors
    /// </summary>
    /// <param name="changeAnalysis">The configuration change analysis</param>
    /// <param name="validationErrors">Validation errors</param>
    /// <returns>Failed update result due to validation errors</returns>
    public static ConfigurationUpdateResult CreateValidationFailure(
        ConfigurationChangeAnalysis changeAnalysis,
        IEnumerable<string> validationErrors)
    {
        var result = new ConfigurationUpdateResult(false, "Configuration validation failed", changeAnalysis);
        result.ValidationErrors.AddRange(validationErrors);
        return result;
    }
    
    /// <summary>
    /// Private constructor for creating update results
    /// </summary>
    /// <param name="success">Whether the update was successful</param>
    /// <param name="errorMessage">Error message if failed</param>
    /// <param name="changeAnalysis">Configuration change analysis</param>
    private ConfigurationUpdateResult(bool success, string? errorMessage, ConfigurationChangeAnalysis changeAnalysis)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ChangeAnalysis = changeAnalysis ?? throw new ArgumentNullException(nameof(changeAnalysis));
        Timestamp = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Gets a human-readable summary of the update result
    /// </summary>
    /// <returns>Summary string</returns>
    public string GetSummary()
    {
        if (!Success)
        {
            var errorSummary = $"Configuration update failed: {ErrorMessage}";
            if (ValidationErrors.Count > 0)
            {
                errorSummary += $" Validation errors: {string.Join(", ", ValidationErrors)}";
            }
            return errorSummary;
        }
        
        if (!ChangeAnalysis.HasChanges)
        {
            return "No configuration changes detected";
        }
        
        var parts = new List<string>();
        
        if (AppliedChanges.Count > 0)
        {
            parts.Add($"Applied immediately: {string.Join(", ", AppliedChanges)}");
        }
        
        if (QueuedForRestart.Count > 0)
        {
            parts.Add($"Queued for restart: {string.Join(", ", QueuedForRestart)}");
        }
        
        return $"Configuration update successful. {string.Join("; ", parts)}";
    }
}

/// <summary>
/// Event arguments for configuration update events
/// </summary>
public class ConfigurationUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// The result of the configuration update operation
    /// </summary>
    public ConfigurationUpdateResult UpdateResult { get; }
    
    /// <summary>
    /// The new configuration that was applied (or attempted to be applied)
    /// </summary>
    public CursorPhobiaConfiguration NewConfiguration { get; }
    
    /// <summary>
    /// The previous configuration that was replaced
    /// </summary>
    public CursorPhobiaConfiguration PreviousConfiguration { get; }
    
    /// <summary>
    /// Creates new configuration updated event arguments
    /// </summary>
    /// <param name="updateResult">The update operation result</param>
    /// <param name="newConfiguration">The new configuration</param>
    /// <param name="previousConfiguration">The previous configuration</param>
    public ConfigurationUpdatedEventArgs(
        ConfigurationUpdateResult updateResult,
        CursorPhobiaConfiguration newConfiguration,
        CursorPhobiaConfiguration previousConfiguration)
    {
        UpdateResult = updateResult ?? throw new ArgumentNullException(nameof(updateResult));
        NewConfiguration = newConfiguration ?? throw new ArgumentNullException(nameof(newConfiguration));
        PreviousConfiguration = previousConfiguration ?? throw new ArgumentNullException(nameof(previousConfiguration));
    }
}