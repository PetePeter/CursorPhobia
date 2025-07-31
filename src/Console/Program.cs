using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Console;

/// <summary>
/// Console application to test CursorPhobia functionality including the new Phase 2C Engine
/// </summary>
class Program
{
    private static ServiceProvider? _serviceProvider;
    private static Logger? _logger;
    
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("CursorPhobia Test Console - Phase 2C Engine Integration");
        System.Console.WriteLine("========================================================");
        System.Console.WriteLine();
        
        try
        {
            // Setup dependency injection and logging
            SetupServices();
            
            _logger = CursorPhobia.Core.Utilities.LoggerFactory.CreateLogger<Program>();
            _logger.LogInformation("Starting CursorPhobia tests");
            
            // Show menu options
            ShowMenu();
            
            while (true)
            {
                System.Console.Write("\nEnter your choice (1-3, q to quit): ");
                var choice = System.Console.ReadLine()?.ToLower();
                
                switch (choice)
                {
                    case "1":
                        await RunBasicTests();
                        break;
                    case "2":
                        await RunEngineDemo();
                        break;
                    case "3":
                        await RunPerformanceTests();
                        break;
                    case "q":
                    case "quit":
                        return;
                    default:
                        System.Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Fatal error: {ex.Message}");
            _logger?.LogCritical(ex, "Fatal error in main program");
        }
        finally
        {
            System.Console.WriteLine("\nPress any key to exit...");
            System.Console.ReadKey();
            
            _serviceProvider?.Dispose();
        }
    }
    
    private static void SetupServices()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        // Add our services
        services.AddSingleton<Logger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            CursorPhobia.Core.Utilities.LoggerFactory.Initialize(loggerFactory);
            return CursorPhobia.Core.Utilities.LoggerFactory.CreateLogger<Program>();
        });
        
        // Core services
        services.AddTransient<IWindowDetectionService, WindowDetectionService>();
        services.AddTransient<IWindowManipulationService, WindowManipulationService>();
        services.AddTransient<ICursorTracker, CursorTracker>();
        services.AddTransient<IProximityDetector>(provider =>
        {
            var logger = provider.GetRequiredService<Logger>();
            return new ProximityDetector(logger);
        });
        services.AddTransient<ISafetyManager, SafetyManager>();
        services.AddTransient<IWindowPusher, WindowPusher>();
        
        // Engine
        services.AddTransient<ICursorPhobiaEngine, CursorPhobiaEngine>();
        
        _serviceProvider = services.BuildServiceProvider();
    }
    
    private static void ShowMenu()
    {
        System.Console.WriteLine("Available Tests:");
        System.Console.WriteLine("1. Basic Functionality Tests (Phase 1 & 2A/2B components)");
        System.Console.WriteLine("2. CursorPhobia Engine Demo (Phase 2C - Live window pushing)");
        System.Console.WriteLine("3. Performance Tests (Engine benchmarking)");
        System.Console.WriteLine("q. Quit");
        System.Console.WriteLine();
        System.Console.WriteLine("Note: Option 2 will actively push windows away from your cursor!");
        System.Console.WriteLine("      Use CTRL key to temporarily disable, or press any key to stop.");
    }
    
    private static async Task RunBasicTests()
    {
        System.Console.WriteLine("\nRunning Basic Functionality Tests...\n");
        
        var windowDetectionService = _serviceProvider!.GetRequiredService<IWindowDetectionService>();
        var windowManipulationService = _serviceProvider!.GetRequiredService<IWindowManipulationService>();
        
        // Test 1: Enumerate visible windows
        await TestEnumerateVisibleWindows(windowDetectionService);
        
        // Test 2: Check for topmost windows
        await TestTopmostWindowDetection(windowDetectionService);
        
        // Test 3: Test window information retrieval
        await TestWindowInformationRetrieval(windowDetectionService);
        
        // Test 4: Test window bounds retrieval
        await TestWindowBoundsRetrieval(windowManipulationService, windowDetectionService);
        
        // Test 5: Test window visibility check
        await TestWindowVisibilityCheck(windowManipulationService, windowDetectionService);
        
        System.Console.WriteLine("\nAll basic functionality tests completed!");
    }
    
    private static async Task RunEngineDemo()
    {
        System.Console.WriteLine("\nStarting CursorPhobia Engine Demo...\n");
        System.Console.WriteLine("⚠️  WARNING: This will actively push always-on-top windows away from your cursor!");
        System.Console.WriteLine("    Hold CTRL to temporarily disable pushing.");
        System.Console.WriteLine("    Press any key to stop the demo.\n");
        
        System.Console.Write("Press ENTER to start, or any other key to cancel: ");
        var key = System.Console.ReadKey();
        System.Console.WriteLine();
        
        if (key.Key != ConsoleKey.Enter)
        {
            System.Console.WriteLine("Demo cancelled.");
            return;
        }
        
        var engine = _serviceProvider!.GetRequiredService<ICursorPhobiaEngine>();
        
        // Configure engine for demo (more responsive settings)
        var config = new CursorPhobiaConfiguration
        {
            UpdateIntervalMs = 16,  // ~60 FPS
            ProximityThreshold = 100,  // 100 pixels
            PushDistance = 150,  // Push 150 pixels away
            HoverTimeoutMs = 3000,  // 3 second hover timeout (shorter for demo)
            EnableHoverTimeout = true,
            EnableCtrlOverride = true,
            EnableAnimations = true,
            AnimationDurationMs = 300,  // Slightly longer animation for visibility
            AnimationEasing = AnimationEasing.EaseOut
        };
        
        // Create engine with demo configuration
        var demoEngine = new CursorPhobiaEngine(
            _serviceProvider.GetRequiredService<Logger>(),
            _serviceProvider.GetRequiredService<ICursorTracker>(),
            _serviceProvider.GetRequiredService<IProximityDetector>(),
            _serviceProvider.GetRequiredService<IWindowDetectionService>(),
            _serviceProvider.GetRequiredService<IWindowPusher>(),
            _serviceProvider.GetRequiredService<ISafetyManager>(),
            config);
        
        // Subscribe to events for live feedback
        demoEngine.WindowPushed += (sender, args) =>
        {
            System.Console.WriteLine($"🚀 Pushed window: '{args.WindowInfo.Title}' (distance: {args.PushDistance}px)");
        };
        
        try
        {
            System.Console.WriteLine("Starting engine...\n");
            
            if (await demoEngine.StartAsync())
            {
                System.Console.WriteLine($"✅ Engine started! Tracking {demoEngine.TrackedWindowCount} windows");
                System.Console.WriteLine("📍 Now move your cursor near always-on-top windows to see them pushed away!");
                System.Console.WriteLine("🔧 Hold CTRL to temporarily disable pushing");
                System.Console.WriteLine("⏰ Hover over a window for 3+ seconds to stop pushing that window");
                System.Console.WriteLine("\nPress any key to stop...\n");
                
                // Wait for user input while showing live stats
                var statsTask = Task.Run(async () =>
                {
                    while (demoEngine.IsRunning)
                    {
                        var stats = demoEngine.GetPerformanceStats();
                        System.Console.Write($"\r📊 Stats: {stats.UpdateCount} updates, {stats.TrackedWindowCount} windows, {stats.AverageUpdateTimeMs:F1}ms avg, {stats.EstimatedCpuUsagePercent:F1}% CPU");
                        await Task.Delay(1000);
                    }
                });
                
                System.Console.ReadKey(true);  // Wait for any key
                System.Console.WriteLine("\n\nStopping engine...");
            }
            else
            {
                System.Console.WriteLine("❌ Failed to start engine");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Engine error: {ex.Message}");
        }
        finally
        {
            await demoEngine.StopAsync();
            demoEngine.Dispose();
            System.Console.WriteLine("✅ Engine stopped and disposed");
        }
    }
    
    private static async Task RunPerformanceTests()
    {
        System.Console.WriteLine("\nRunning Performance Tests...\n");
        
        var engine = _serviceProvider!.GetRequiredService<ICursorPhobiaEngine>();
        
        // Test different update intervals
        var testConfigs = new[]
        {
            new { Name = "High Performance (8ms, ~120fps)", Config = CursorPhobiaConfiguration.CreateResponsivenessOptimized() },
            new { Name = "Balanced (16ms, ~60fps)", Config = CursorPhobiaConfiguration.CreateDefault() },
            new { Name = "Power Saving (33ms, ~30fps)", Config = CursorPhobiaConfiguration.CreatePerformanceOptimized() }
        };
        
        foreach (var test in testConfigs)
        {
            System.Console.WriteLine($"Testing: {test.Name}");
            
            var testEngine = new CursorPhobiaEngine(
                _serviceProvider.GetRequiredService<Logger>(),
                _serviceProvider.GetRequiredService<ICursorTracker>(),
                _serviceProvider.GetRequiredService<IProximityDetector>(),
                _serviceProvider.GetRequiredService<IWindowDetectionService>(),
                _serviceProvider.GetRequiredService<IWindowPusher>(),
                _serviceProvider.GetRequiredService<ISafetyManager>(),
                test.Config);
            
            try
            {
                if (await testEngine.StartAsync())
                {
                    // Run for 5 seconds
                    await Task.Delay(5000);
                    
                    var stats = testEngine.GetPerformanceStats();
                    System.Console.WriteLine($"  Results: {stats.UpdateCount} updates in 5s");
                    System.Console.WriteLine($"  Average: {stats.AverageUpdateTimeMs:F2}ms per update");
                    System.Console.WriteLine($"  Est. CPU: {stats.EstimatedCpuUsagePercent:F1}%");
                    System.Console.WriteLine($"  UPS: {stats.UpdatesPerSecond:F1} updates/second");
                    System.Console.WriteLine();
                }
            }
            finally
            {
                await testEngine.StopAsync();
                testEngine.Dispose();
            }
        }
        
        System.Console.WriteLine("Performance tests completed!");
    }
    
    private static async Task TestEnumerateVisibleWindows(IWindowDetectionService detectionService)
    {
        System.Console.WriteLine("Test 1: Enumerating visible windows...");
        
        try
        {
            var windows = detectionService.EnumerateVisibleWindows();
            
            System.Console.WriteLine($"Found {windows.Count} visible windows:");
            
            var displayCount = Math.Min(10, windows.Count);
            for (int i = 0; i < displayCount; i++)
            {
                var window = windows[i];
                System.Console.WriteLine($"  {i + 1}. '{window.Title}' ({window.ClassName}) - {window.Bounds}");
            }
            
            if (windows.Count > 10)
            {
                System.Console.WriteLine($"  ... and {windows.Count - 10} more windows");
            }
            
            System.Console.WriteLine("✓ Window enumeration test passed\n");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Window enumeration test failed: {ex.Message}\n");
        }
        
        await Task.Delay(1000);
    }
    
    private static async Task TestTopmostWindowDetection(IWindowDetectionService detectionService)
    {
        System.Console.WriteLine("Test 2: Detecting topmost windows...");
        
        try
        {
            var topmostWindows = detectionService.GetAllTopMostWindows();
            
            System.Console.WriteLine($"Found {topmostWindows.Count} topmost windows:");
            
            foreach (var window in topmostWindows.Take(5))
            {
                System.Console.WriteLine($"  - '{window.Title}' ({window.ClassName})");
            }
            
            if (topmostWindows.Count > 5)
            {
                System.Console.WriteLine($"  ... and {topmostWindows.Count - 5} more topmost windows");
            }
            
            System.Console.WriteLine("✓ Topmost window detection test passed\n");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Topmost window detection test failed: {ex.Message}\n");
        }
        
        await Task.Delay(1000);
    }
    
    private static async Task TestWindowInformationRetrieval(IWindowDetectionService detectionService)
    {
        System.Console.WriteLine("Test 3: Testing window information retrieval...");
        
        try
        {
            var windows = detectionService.EnumerateVisibleWindows();
            if (windows.Count > 0)
            {
                var testWindow = windows.First();
                var windowInfo = detectionService.GetWindowInformation(testWindow.WindowHandle);
                
                if (windowInfo != null)
                {
                    System.Console.WriteLine("Sample window information:");
                    System.Console.WriteLine($"  Handle: 0x{windowInfo.WindowHandle:X}");
                    System.Console.WriteLine($"  Title: '{windowInfo.Title}'");
                    System.Console.WriteLine($"  Class: {windowInfo.ClassName}");
                    System.Console.WriteLine($"  Bounds: {windowInfo.Bounds}");
                    System.Console.WriteLine($"  Process ID: {windowInfo.ProcessId}");
                    System.Console.WriteLine($"  Thread ID: {windowInfo.ThreadId}");
                    System.Console.WriteLine($"  Visible: {windowInfo.IsVisible}");
                    System.Console.WriteLine($"  Topmost: {windowInfo.IsTopmost}");
                    System.Console.WriteLine($"  Minimized: {windowInfo.IsMinimized}");
                    
                    System.Console.WriteLine("✓ Window information retrieval test passed\n");
                }
                else
                {
                    System.Console.WriteLine("✗ Could not retrieve window information\n");
                }
            }
            else
            {
                System.Console.WriteLine("✗ No windows found to test with\n");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Window information retrieval test failed: {ex.Message}\n");
        }
        
        await Task.Delay(1000);
    }
    
    private static async Task TestWindowBoundsRetrieval(IWindowManipulationService manipulationService, IWindowDetectionService detectionService)
    {
        System.Console.WriteLine("Test 4: Testing window bounds retrieval...");
        
        try
        {
            var windows = detectionService.EnumerateVisibleWindows();
            if (windows.Count > 0)
            {
                var testWindow = windows.First();
                var bounds = manipulationService.GetWindowBounds(testWindow.WindowHandle);
                
                System.Console.WriteLine($"Window bounds for '{testWindow.Title}':");
                System.Console.WriteLine($"  Position: ({bounds.X}, {bounds.Y})");
                System.Console.WriteLine($"  Size: {bounds.Width} x {bounds.Height}");
                System.Console.WriteLine($"  Area: {bounds.Width * bounds.Height} pixels");
                
                System.Console.WriteLine("✓ Window bounds retrieval test passed\n");
            }
            else
            {
                System.Console.WriteLine("✗ No windows found to test with\n");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Window bounds retrieval test failed: {ex.Message}\n");
        }
        
        await Task.Delay(1000);
    }
    
    private static async Task TestWindowVisibilityCheck(IWindowManipulationService manipulationService, IWindowDetectionService detectionService)
    {
        System.Console.WriteLine("Test 5: Testing window visibility checks...");
        
        try
        {
            var windows = detectionService.EnumerateVisibleWindows();
            if (windows.Count > 0)
            {
                var visibleCount = 0;
                var testCount = Math.Min(5, windows.Count);
                
                for (int i = 0; i < testCount; i++)
                {
                    var window = windows[i];
                    var isVisible = manipulationService.IsWindowVisible(window.WindowHandle);
                    if (isVisible) visibleCount++;
                }
                
                System.Console.WriteLine($"Tested {testCount} windows:");
                System.Console.WriteLine($"  {visibleCount} are visible");
                System.Console.WriteLine($"  {testCount - visibleCount} are not visible");
                
                System.Console.WriteLine("✓ Window visibility check test passed\n");
            }
            else
            {
                System.Console.WriteLine("✗ No windows found to test with\n");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Window visibility check test failed: {ex.Message}\n");
        }
        
        await Task.Delay(1000);
    }
}