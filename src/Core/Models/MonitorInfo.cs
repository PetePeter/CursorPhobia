using System.Drawing;
using System.Security.Cryptography;
using System.Text;

namespace CursorPhobia.Core.Models;

/// <summary>
/// Represents information about a display monitor
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// Handle to the monitor
    /// </summary>
    public IntPtr monitorHandle { get; set; }

    /// <summary>
    /// The display rectangle of the monitor in virtual screen coordinates
    /// </summary>
    public Rectangle monitorBounds { get; set; }

    /// <summary>
    /// The work area rectangle of the monitor in virtual screen coordinates
    /// (excludes taskbars, sidebars, and docked windows)
    /// </summary>
    public Rectangle workAreaBounds { get; set; }

    /// <summary>
    /// Indicates whether this is the primary monitor
    /// </summary>
    public bool isPrimary { get; set; }

    /// <summary>
    /// The monitor's device name
    /// </summary>
    public string deviceName { get; set; } = string.Empty;

    /// <summary>
    /// Stable identifier for this monitor that persists across hotplug events
    /// Based on device name, size, and other persistent characteristics
    /// </summary>
    public string stableID { get; private set; } = string.Empty;

    /// <summary>
    /// Manufacturer ID (parsed from device name if available)
    /// </summary>
    public string manufacturerID { get; set; } = string.Empty;

    /// <summary>
    /// Product ID (parsed from device name if available)
    /// </summary>
    public string productID { get; set; } = string.Empty;

    /// <summary>
    /// Serial number if available
    /// </summary>
    public string serialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Monitor width in pixels
    /// </summary>
    public int width => monitorBounds.Width;

    /// <summary>
    /// Monitor height in pixels
    /// </summary>
    public int height => monitorBounds.Height;

    /// <summary>
    /// Monitor's horizontal position in virtual screen coordinates
    /// </summary>
    public int x => monitorBounds.X;

    /// <summary>
    /// Monitor's vertical position in virtual screen coordinates
    /// </summary>
    public int y => monitorBounds.Y;

    /// <summary>
    /// Creates a new MonitorInfo instance
    /// </summary>
    public MonitorInfo()
    {
        GenerateStableID();
    }

    /// <summary>
    /// Creates a new MonitorInfo instance with specified parameters
    /// </summary>
    /// <param name="handle">Monitor handle</param>
    /// <param name="monitorRect">Monitor bounds</param>
    /// <param name="workRect">Work area bounds</param>
    /// <param name="primary">Whether this is the primary monitor</param>
    /// <param name="name">Device name</param>
    public MonitorInfo(IntPtr handle, Rectangle monitorRect, Rectangle workRect, bool primary, string name = "")
    {
        monitorHandle = handle;
        monitorBounds = monitorRect;
        workAreaBounds = workRect;
        isPrimary = primary;
        deviceName = name;

        ParseDeviceIdentifiers();
        GenerateStableID();
    }

    /// <summary>
    /// Creates a new MonitorInfo instance with full identification details
    /// </summary>
    /// <param name="handle">Monitor handle</param>
    /// <param name="monitorRect">Monitor bounds</param>
    /// <param name="workRect">Work area bounds</param>
    /// <param name="primary">Whether this is the primary monitor</param>
    /// <param name="name">Device name</param>
    /// <param name="manufacturerID">Manufacturer identifier</param>
    /// <param name="productID">Product identifier</param>
    /// <param name="serialNumber">Serial number</param>
    public MonitorInfo(IntPtr handle, Rectangle monitorRect, Rectangle workRect, bool primary,
        string name, string manufacturerID, string productID, string serialNumber)
    {
        monitorHandle = handle;
        monitorBounds = monitorRect;
        workAreaBounds = workRect;
        isPrimary = primary;
        deviceName = name;
        this.manufacturerID = manufacturerID;
        this.productID = productID;
        this.serialNumber = serialNumber;

        GenerateStableID();
    }

    /// <summary>
    /// Determines if a point is within this monitor's bounds
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>True if the point is within the monitor bounds</returns>
    public bool ContainsPoint(Point point)
    {
        return monitorBounds.Contains(point);
    }

    /// <summary>
    /// Determines if a rectangle intersects with this monitor's bounds
    /// </summary>
    /// <param name="rectangle">Rectangle to check</param>
    /// <returns>True if the rectangle intersects with the monitor bounds</returns>
    public bool IntersectsWith(Rectangle rectangle)
    {
        return monitorBounds.IntersectsWith(rectangle);
    }

    /// <summary>
    /// Returns a string representation of the monitor information
    /// </summary>
    public override string ToString()
    {
        return $"Monitor [{deviceName}]: {monitorBounds} (Primary: {isPrimary}, ID: {stableID})";
    }

    /// <summary>
    /// Parses device identifiers from the device name
    /// </summary>
    private void ParseDeviceIdentifiers()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        // Try to parse EDID-style device names like "\\.\DISPLAY1" or hardware IDs
        // This is a simplified parser - real implementations might use WMI or registry

        // For now, use device name as basis for identification
        // In a full implementation, this would extract manufacturer/product codes from EDID
        if (deviceName.StartsWith(@"\\.\"))
        {
            // Extract display number
            var displayPart = deviceName.Substring(4); // Remove "\\." prefix
            manufacturerID = "GENERIC";
            productID = displayPart;
        }
        else
        {
            // Use device name directly
            manufacturerID = "UNKNOWN";
            productID = deviceName;
        }
    }

    /// <summary>
    /// Generates a stable identifier for this monitor
    /// </summary>
    private void GenerateStableID()
    {
        // Create a stable ID based on persistent characteristics
        var identifierData = new StringBuilder();

        // Include manufacturer and product IDs if available
        if (!string.IsNullOrEmpty(manufacturerID))
            identifierData.Append(manufacturerID);
        if (!string.IsNullOrEmpty(productID))
            identifierData.Append("|").Append(productID);
        if (!string.IsNullOrEmpty(serialNumber))
            identifierData.Append("|").Append(serialNumber);

        // Include monitor size as fallback identifier
        identifierData.Append("|").Append(width).Append("x").Append(height);

        // Include device name as additional distinguisher
        if (!string.IsNullOrEmpty(deviceName))
            identifierData.Append("|").Append(deviceName);

        // Generate SHA256 hash for consistent, short identifier
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(identifierData.ToString()));
        stableID = Convert.ToHexString(hashBytes)[..12]; // Use first 12 characters
    }

    /// <summary>
    /// Updates the stable ID after property changes
    /// </summary>
    public void RefreshStableID()
    {
        ParseDeviceIdentifiers();
        GenerateStableID();
    }

    /// <summary>
    /// Gets the stable ID for use as a dictionary key in per-monitor settings
    /// </summary>
    public string GetStableKey() => stableID;
}