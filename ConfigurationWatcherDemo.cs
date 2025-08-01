using System.Text.Json;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia;

/// <summary>
/// Demonstration of Phase 3 WI#4: File Watcher and Live Reloading capability
/// Shows how the ConfigurationWatcherService monitors config.json and triggers hot-swaps
/// </summary>
class ConfigurationWatcherDemo
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("CursorPhobia Configuration File Watcher Demo");
        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine("This demo shows Phase 3 WI#4: File Watcher and Live Reloading");
        Console.WriteLine();

        // Setup services
        var logger = new TestLogger();
        var configService = new ConfigurationService(logger);
        var watcher = new ConfigurationWatcherService(logger, configService);

        // Get or create configuration path
        var configPath = await configService.GetDefaultConfigurationPathAsync();
        Console.WriteLine($"Configuration file: {configPath}");
        Console.WriteLine();

        // Ensure config file exists with default settings
        if (!File.Exists(configPath))
        {
            var defaultConfig = CursorPhobiaConfiguration.CreateDefault();
            await configService.SaveConfigurationAsync(defaultConfig, configPath);
            Console.WriteLine("Created default configuration file");
        }

        // Setup event handlers
        watcher.ConfigurationFileChanged += (sender, args) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Configuration reloaded successfully!");
            Console.WriteLine($"  - File: {args.FilePath}");
            Console.WriteLine($"  - Proximity Threshold: {args.Configuration.ProximityThreshold}px");
            Console.WriteLine($"  - Push Distance: {args.Configuration.PushDistance}px");
            Console.WriteLine($"  - Update Interval: {args.Configuration.UpdateIntervalMs}ms");
            Console.WriteLine($"  - Debounced Events: {args.DebouncedEventCount}");
            Console.WriteLine();
        };

        watcher.ConfigurationFileChangeFailed += (sender, args) =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Configuration reload FAILED!");
            Console.WriteLine($"  - File: {args.FilePath}");
            Console.WriteLine($"  - Reason: {args.FailureReason}");
            Console.WriteLine($"  - Error: {args.Exception.Message}");
            Console.WriteLine();
        };

        try
        {
            // Start watching
            Console.WriteLine("Starting configuration file watcher...");
            var success = await watcher.StartWatchingAsync(configPath, 500);
            
            if (!success)
            {
                Console.WriteLine("Failed to start file watcher!");
                return;
            }

            Console.WriteLine("File watcher started successfully!");
            Console.WriteLine();
            Console.WriteLine("DEMO INSTRUCTIONS:");
            Console.WriteLine("==================");
            Console.WriteLine($"1. Open your configuration file: {configPath}");
            Console.WriteLine("2. Modify values (e.g., change proximityThreshold from 50 to 100)");
            Console.WriteLine("3. Save the file");
            Console.WriteLine("4. Watch this console for live reload messages");
            Console.WriteLine("5. Try making multiple rapid changes to test debouncing");
            Console.WriteLine("6. Try saving invalid JSON to test error handling");
            Console.WriteLine();
            Console.WriteLine("Key Features Demonstrated:");
            Console.WriteLine("- FileSystemWatcher with 500ms debouncing");
            Console.WriteLine("- Temporary file filtering (.tmp, .bak, etc.)");
            Console.WriteLine("- Robust error handling for file locks and invalid JSON");
            Console.WriteLine("- Event-driven notifications for success and failure");
            Console.WriteLine("- Statistical tracking of watcher activity");
            Console.WriteLine();
            Console.WriteLine("Press 's' to show statistics, or any other key to exit...");

            // Wait for user input
            ConsoleKeyInfo keyInfo;
            do
            {
                keyInfo = Console.ReadKey(true);
                
                if (keyInfo.Key == ConsoleKey.S)
                {
                    ShowStatistics(watcher);
                }
            } 
            while (keyInfo.Key == ConsoleKey.S);

            Console.WriteLine("\nStopping file watcher...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Demo error: {ex.Message}");
        }
        finally
        {
            await watcher.StopWatchingAsync();
            watcher.Dispose();
            Console.WriteLine("File watcher stopped.");
        }
    }

    private static void ShowStatistics(ConfigurationWatcherService watcher)
    {
        Console.WriteLine();
        Console.WriteLine("WATCHER STATISTICS:");
        Console.WriteLine("==================");
        
        var stats = watcher.GetStatistics();
        
        Console.WriteLine($"Total File System Events: {stats.TotalFileSystemEvents}");
        Console.WriteLine($"Filtered Events: {stats.FilteredEvents}");
        Console.WriteLine($"Successful Reloads: {stats.SuccessfulReloads}");
        Console.WriteLine($"Failed Reloads: {stats.FailedReloads}");
        Console.WriteLine($"Debounced Events: {stats.DebouncedEvents}");
        Console.WriteLine($"Success Rate: {stats.SuccessRate:F1}%");
        Console.WriteLine($"Average Debounce Delay: {stats.AverageDebounceDelayMs:F1}ms");
        
        if (stats.WatchingStartedAt.HasValue)
        {
            Console.WriteLine($"Watching Started: {stats.WatchingStartedAt:HH:mm:ss}");
            var uptime = stats.Uptime;
            if (uptime.HasValue)
            {
                Console.WriteLine($"Uptime: {uptime.Value.TotalSeconds:F1} seconds");
            }
        }
        
        if (stats.LastSuccessfulReload.HasValue)
        {
            Console.WriteLine($"Last Successful Reload: {stats.LastSuccessfulReload:HH:mm:ss}");
        }
        
        if (stats.LastFailedReload.HasValue)
        {
            Console.WriteLine($"Last Failed Reload: {stats.LastFailedReload:HH:mm:ss}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Press 's' to show statistics again, or any other key to exit...");
    }
}

/// <summary>
/// Simple test logger for the demo
/// </summary>
public class TestLogger : ILogger
{
    public void LogDebug(string message, params object[] args) { }
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) 
    {
        Console.WriteLine($"[WARNING] {string.Format(message, args)}");
    }
    public void LogError(string message, params object[] args) 
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}");
    }
    public void LogError(Exception ex, string message, params object[] args) 
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}: {ex.Message}");
    }
    public void LogCritical(Exception ex, string message, params object[] args) 
    {
        Console.WriteLine($"[CRITICAL] {string.Format(message, args)}: {ex.Message}");
    }
}