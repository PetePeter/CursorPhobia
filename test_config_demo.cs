using System;
using System.IO;
using System.Threading.Tasks;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

class ConfigurationDemo
{
    static async Task Main(string[] args)
    {
        // Initialize logger
        var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information));
        CursorPhobia.Core.Utilities.LoggerFactory.Initialize(loggerFactory);
        var logger = CursorPhobia.Core.Utilities.LoggerFactory.CreateLogger<ConfigurationDemo>();

        // Test configuration service
        var backupService = new ConfigurationBackupService(logger);
        var configService = new ConfigurationService(logger, backupService);

        Console.WriteLine("CursorPhobia Configuration System - User Experience Demo");
        Console.WriteLine("=======================================================\n");

        // Test 1: Default configuration path
        Console.WriteLine("1. Default Configuration Path:");
        var defaultPath = await configService.GetDefaultConfigurationPathAsync();
        Console.WriteLine($"   {defaultPath}");
        Console.WriteLine($"   Directory exists: {Directory.Exists(Path.GetDirectoryName(defaultPath))}");
        Console.WriteLine();

        // Test 2: First-time user experience
        Console.WriteLine("2. First-Time User Experience:");
        var config = await configService.LoadConfigurationAsync(defaultPath);
        Console.WriteLine($"   Configuration loaded successfully: {config != null}");
        Console.WriteLine($"   ProximityThreshold: {config.ProximityThreshold}px");
        Console.WriteLine($"   PushDistance: {config.PushDistance}px");
        Console.WriteLine($"   UpdateInterval: {config.UpdateIntervalMs}ms");
        Console.WriteLine($"   CTRL Override: {config.EnableCtrlOverride}");
        Console.WriteLine();

        // Test 3: Check if config file was created
        Console.WriteLine("3. Configuration File Creation:");
        Console.WriteLine($"   Config file exists: {File.Exists(defaultPath)}");
        if (File.Exists(defaultPath))
        {
            var fileInfo = new FileInfo(defaultPath);
            Console.WriteLine($"   File size: {fileInfo.Length} bytes");
            Console.WriteLine($"   Created: {fileInfo.CreationTime}");
        }
        Console.WriteLine();

        // Test 4: Show file contents
        if (File.Exists(defaultPath))
        {
            Console.WriteLine("4. Configuration File Contents:");
            var content = await File.ReadAllTextAsync(defaultPath);
            var lines = content.Split('\n');
            foreach (var line in lines.Take(10))
            {
                Console.WriteLine($"   {line.TrimEnd()}");
            }
            if (lines.Length > 10)
            {
                Console.WriteLine($"   ... and {lines.Length - 10} more lines");
            }
            Console.WriteLine();
        }

        // Test 5: Modify and save configuration
        Console.WriteLine("5. Configuration Modification Test:");
        config.ProximityThreshold = 75;
        config.PushDistance = 125;
        config.EnableHoverTimeout = false;
        
        try
        {
            await configService.SaveConfigurationAsync(config, defaultPath);
            Console.WriteLine("   ✓ Configuration saved successfully");
            
            // Check for backup files
            var backupDir = Path.GetDirectoryName(defaultPath);
            var backups = await backupService.GetAvailableBackupsAsync(backupDir);
            Console.WriteLine($"   Backup files created: {backups.Length}");
            foreach (var backup in backups)
            {
                Console.WriteLine($"     - {Path.GetFileName(backup)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ✗ Save failed: {ex.Message}");
        }
        Console.WriteLine();

        // Test 6: Error handling - corrupted file
        Console.WriteLine("6. Error Handling Test - Corrupted File:");
        var corruptedPath = Path.Combine(Path.GetTempPath(), "corrupted_config.json");
        await File.WriteAllTextAsync(corruptedPath, "{ invalid json content }");
        
        var corruptedConfig = await configService.LoadConfigurationAsync(corruptedPath);
        Console.WriteLine($"   Loaded from corrupted file: {corruptedConfig != null}");
        Console.WriteLine($"   Fallback to defaults: {corruptedConfig?.ProximityThreshold == 50}");
        Console.WriteLine();

        // Test 7: Error handling - permission denied
        Console.WriteLine("7. Error Handling Test - Invalid Path:");
        try
        {
            var invalidConfig = await configService.LoadConfigurationAsync("Z:\\invalid\\path\\config.json");
            Console.WriteLine($"   Handled invalid path gracefully: {invalidConfig != null}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   Exception: {ex.Message}");
        }

        Console.WriteLine("\nDemo completed!");
    }
}