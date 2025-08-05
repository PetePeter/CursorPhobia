using System.IO;
using System.Text.Json;
using log4net.Core;
using log4net.Layout;

namespace CursorPhobia.Core.Logging;

/// <summary>
/// Custom log4net layout that outputs structured JSON logs
/// Provides better integration with log analysis tools and structured logging systems
/// </summary>
public class JsonPatternLayout : LayoutSkeleton
{
    /// <summary>
    /// Formats the logging event as JSON
    /// </summary>
    /// <param name="writer">The TextWriter to write to</param>
    /// <param name="loggingEvent">The event to format</param>
    public override void Format(TextWriter writer, LoggingEvent loggingEvent)
    {
        var logEntry = new
        {
            timestamp = loggingEvent.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            level = loggingEvent.Level.Name,
            logger = loggingEvent.LoggerName,
            message = loggingEvent.RenderedMessage,
            thread = loggingEvent.ThreadName,
            exception = loggingEvent.ExceptionObject?.ToString(),
            properties = ExtractProperties(loggingEvent)
        };

        var jsonString = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        writer.WriteLine(jsonString);
    }

    /// <summary>
    /// Extracts properties from the logging event for structured logging
    /// </summary>
    /// <param name="loggingEvent">The logging event</param>
    /// <returns>Dictionary of properties</returns>
    private static Dictionary<string, object?> ExtractProperties(LoggingEvent loggingEvent)
    {
        var properties = new Dictionary<string, object?>();

        // Add standard properties
        if (loggingEvent.Properties != null)
        {
            foreach (var key in loggingEvent.Properties.GetKeys())
            {
                if (key != null)
                {
                    var value = loggingEvent.Properties[key];
                    properties[key] = value;
                }
            }
        }

        // Add location information if available
        if (loggingEvent.LocationInformation != null)
        {
            properties["className"] = loggingEvent.LocationInformation.ClassName;
            properties["methodName"] = loggingEvent.LocationInformation.MethodName;
            properties["fileName"] = loggingEvent.LocationInformation.FileName;
            properties["lineNumber"] = loggingEvent.LocationInformation.LineNumber;
        }

        return properties;
    }

    /// <summary>
    /// The content type output by this layout
    /// </summary>
    public override string ContentType => "application/json";

    /// <summary>
    /// The header for the layout format
    /// </summary>
    public override string Header => string.Empty;

    /// <summary>
    /// The footer for the layout format  
    /// </summary>
    public override string Footer => string.Empty;

    /// <summary>
    /// Flag indicating whether this layout ignores exceptions
    /// </summary>
    public override bool IgnoresException => false;

    /// <summary>
    /// Activates any options that were specified in the configuration
    /// </summary>
    public override void ActivateOptions()
    {
        // No specific options to activate for this layout
    }
}