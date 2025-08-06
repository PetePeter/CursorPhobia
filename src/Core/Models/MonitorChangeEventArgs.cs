using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Models;

/// <summary>
/// Event arguments for monitor configuration changes
/// </summary>
public class MonitorChangeEventArgs : EventArgs
{
    /// <summary>
    /// Type of monitor change that occurred
    /// </summary>
    public MonitorChangeType ChangeType { get; }

    /// <summary>
    /// Monitor configurations before the change
    /// </summary>
    public IReadOnlyList<MonitorInfo> PreviousMonitors { get; }

    /// <summary>
    /// Monitor configurations after the change
    /// </summary>
    public IReadOnlyList<MonitorInfo> CurrentMonitors { get; }

    /// <summary>
    /// Timestamp when the change was detected
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Creates new monitor change event arguments
    /// </summary>
    /// <param name="changeType">Type of change that occurred</param>
    /// <param name="previousMonitors">Monitor configuration before change</param>
    /// <param name="currentMonitors">Monitor configuration after change</param>
    public MonitorChangeEventArgs(
        MonitorChangeType changeType,
        IReadOnlyList<MonitorInfo> previousMonitors,
        IReadOnlyList<MonitorInfo> currentMonitors)
    {
        ChangeType = changeType;
        PreviousMonitors = previousMonitors ?? throw new ArgumentNullException(nameof(previousMonitors));
        CurrentMonitors = currentMonitors ?? throw new ArgumentNullException(nameof(currentMonitors));
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets monitors that were added in this change
    /// </summary>
    /// <returns>List of newly added monitors</returns>
    public List<MonitorInfo> GetAddedMonitors()
    {
        return CurrentMonitors
            .Where(current => !PreviousMonitors.Any(previous =>
                previous.monitorHandle == current.monitorHandle))
            .ToList();
    }

    /// <summary>
    /// Gets monitors that were removed in this change
    /// </summary>
    /// <returns>List of removed monitors</returns>
    public List<MonitorInfo> GetRemovedMonitors()
    {
        return PreviousMonitors
            .Where(previous => !CurrentMonitors.Any(current =>
                current.monitorHandle == previous.monitorHandle))
            .ToList();
    }

    /// <summary>
    /// Gets monitors that changed configuration (position, size, DPI)
    /// </summary>
    /// <returns>List of modified monitors with their previous and current states</returns>
    public List<(MonitorInfo Previous, MonitorInfo Current)> GetModifiedMonitors()
    {
        var modified = new List<(MonitorInfo Previous, MonitorInfo Current)>();

        foreach (var current in CurrentMonitors)
        {
            var previous = PreviousMonitors.FirstOrDefault(p =>
                p.monitorHandle == current.monitorHandle);

            if (previous != null && !MonitorsEqual(previous, current))
            {
                modified.Add((previous, current));
            }
        }

        return modified;
    }

    /// <summary>
    /// Checks if two monitor configurations are equal
    /// </summary>
    /// <param name="monitor1">First monitor</param>
    /// <param name="monitor2">Second monitor</param>
    /// <returns>True if monitors have identical configuration</returns>
    private static bool MonitorsEqual(MonitorInfo monitor1, MonitorInfo monitor2)
    {
        return monitor1.monitorHandle == monitor2.monitorHandle &&
               monitor1.monitorBounds == monitor2.monitorBounds &&
               monitor1.workAreaBounds == monitor2.workAreaBounds &&
               monitor1.isPrimary == monitor2.isPrimary;
    }
}

/// <summary>
/// Types of monitor configuration changes
/// </summary>
public enum MonitorChangeType
{
    /// <summary>
    /// Monitors were added to the system
    /// </summary>
    MonitorsAdded,

    /// <summary>
    /// Monitors were removed from the system
    /// </summary>
    MonitorsRemoved,

    /// <summary>
    /// Monitor positions or sizes changed
    /// </summary>
    MonitorsRepositioned,

    /// <summary>
    /// Monitor DPI settings changed
    /// </summary>
    DpiChanged,

    /// <summary>
    /// Primary monitor changed
    /// </summary>
    PrimaryMonitorChanged,

    /// <summary>
    /// Multiple types of changes occurred simultaneously
    /// </summary>
    ComplexChange,

    /// <summary>
    /// Unknown or unspecified change type
    /// </summary>
    Unknown
}