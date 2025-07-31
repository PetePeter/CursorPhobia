using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Console;

/// <summary>
/// Console application to test Phase 1 functionality of CursorPhobia
/// </summary>
class Program
{
    private static ServiceProvider? _serviceProvider;
    private static Logger? _logger;
    
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("CursorPhobia Phase 1 Test Console");
        System.Console.WriteLine("===================================");
        System.Console.WriteLine();
        
        try
        {
            // Setup dependency injection and logging
            SetupServices();
            
            _logger = LoggerFactory.CreateLogger<Program>();
            _logger.LogInformation("Starting CursorPhobia Phase 1 tests");
            
            var windowDetectionService = _serviceProvider!.GetRequiredService<IWindowDetectionService>();
            var windowManipulationService = _serviceProvider!.GetRequiredService<IWindowManipulationService>();
            
            await RunTests(windowDetectionService, windowManipulationService);
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
            LoggerFactory.Initialize(loggerFactory);
            return LoggerFactory.CreateLogger<Program>();
        });
        
        services.AddTransient<IWindowDetectionService, WindowDetectionService>();
        services.AddTransient<IWindowManipulationService, WindowManipulationService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }
    
    private static async Task RunTests(IWindowDetectionService detectionService, IWindowManipulationService manipulationService)
    {
        System.Console.WriteLine("Running Phase 1 functionality tests...\n");
        
        // Test 1: Enumerate visible windows
        await TestEnumerateVisibleWindows(detectionService);
        
        // Test 2: Check for topmost windows
        await TestTopmostWindowDetection(detectionService);
        
        // Test 3: Test window information retrieval
        await TestWindowInformationRetrieval(detectionService);
        
        // Test 4: Test window bounds retrieval
        await TestWindowBoundsRetrieval(manipulationService, detectionService);
        
        // Test 5: Test window visibility check
        await TestWindowVisibilityCheck(manipulationService, detectionService);
        
        System.Console.WriteLine("\nAll Phase 1 tests completed!");
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
                System.Console.WriteLine($"  {i + 1}. '{window.title}' ({window.className}) - {window.bounds}");
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
                System.Console.WriteLine($"  - '{window.title}' ({window.className})");
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
                var windowInfo = detectionService.GetWindowInformation(testWindow.windowHandle);
                
                if (windowInfo != null)
                {
                    System.Console.WriteLine("Sample window information:");
                    System.Console.WriteLine($"  Handle: 0x{windowInfo.windowHandle:X}");
                    System.Console.WriteLine($"  Title: '{windowInfo.title}'");
                    System.Console.WriteLine($"  Class: {windowInfo.className}");
                    System.Console.WriteLine($"  Bounds: {windowInfo.bounds}");
                    System.Console.WriteLine($"  Process ID: {windowInfo.processId}");
                    System.Console.WriteLine($"  Thread ID: {windowInfo.threadId}");
                    System.Console.WriteLine($"  Visible: {windowInfo.isVisible}");
                    System.Console.WriteLine($"  Topmost: {windowInfo.isTopmost}");
                    System.Console.WriteLine($"  Minimized: {windowInfo.isMinimized}");
                    
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
                var bounds = manipulationService.GetWindowBounds(testWindow.windowHandle);
                
                System.Console.WriteLine($"Window bounds for '{testWindow.title}':");
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
                    var isVisible = manipulationService.IsWindowVisible(window.windowHandle);
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