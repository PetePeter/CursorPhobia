using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Console;

/// <summary>
/// Console application to test CursorPhobia functionality including system tray integration (Phase A WI#5)
/// </summary>
class Program
{
    private static ServiceProvider? _serviceProvider;
    private static Logger? _logger;
    private static ISystemTrayManager? _trayManager;
    private static IApplicationLifecycleManager? _lifecycleManager;
    private static ICursorPhobiaEngine? _engine;
    
    static async Task Main(string[] args)
    {
        System.Console.WriteLine("CursorPhobia Test Console - Phase A: System Tray Integration (WI#5)");
        System.Console.WriteLine("=====================================================================");
        System.Console.WriteLine();
        
        // Check for automated mode (when run from batch)
        bool isAutomatedMode = args.Contains("--automated") || System.Console.IsInputRedirected;
        
        // Check for tray mode (runs with system tray instead of console menu)
        bool isTrayMode = args.Contains("--tray") && !isAutomatedMode;
        
        try
        {
            // Setup dependency injection and logging
            SetupServices(isAutomatedMode);
            
            // Get logger after services are set up
            _logger = _serviceProvider!.GetRequiredService<Logger>();
            _logger.LogInformation("Starting CursorPhobia application");
            
            if (isAutomatedMode)
            {
                System.Console.WriteLine("Running in automated mode - basic tests only");
                await RunBasicTests();
                return;
            }
            
            if (isTrayMode)
            {
                System.Console.WriteLine("Starting in tray mode...");
                await RunTrayMode();
                return;
            }
            
            // Show menu options
            ShowMenu();
            
            while (true)
            {
                System.Console.Write("\nEnter your choice (1-4, q to quit): ");
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
                    case "4":
                        await RunTrayDemo();
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
            if (!isAutomatedMode)
            {
                System.Console.WriteLine("\nPress any key to exit...");
                System.Console.ReadKey();
            }
            
            _serviceProvider?.Dispose();
        }
    }
    
    private static void SetupServices(bool isAutomatedMode = false)
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            // Use Warning level in automated mode to reduce noise
            builder.SetMinimumLevel(isAutomatedMode ? LogLevel.Warning : LogLevel.Debug);
        });
        
        // Add our services
        services.AddSingleton<Logger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            CursorPhobia.Core.Utilities.LoggerFactory.Initialize(loggerFactory);
            return CursorPhobia.Core.Utilities.LoggerFactory.CreateLogger<Program>();
        });
        
        // Register ILogger for services that need it
        services.AddSingleton<CursorPhobia.Core.Utilities.ILogger>(provider =>
        {
            return provider.GetRequiredService<Logger>();
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
        
        // Configuration services (Phase B WI#5)
        services.AddSingleton<IConfigurationBackupService, ConfigurationBackupService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        
        // System tray and lifecycle management (Phase A WI#5)
        services.AddSingleton<ISystemTrayManager, SystemTrayManager>();
        services.AddSingleton<IApplicationLifecycleManager, ApplicationLifecycleManager>();
        
        _serviceProvider = services.BuildServiceProvider();
    }
    
    private static void ShowMenu()
    {
        System.Console.WriteLine("Available Tests:");
        System.Console.WriteLine("1. Basic Functionality Tests (Phase 1 & 2A/2B components)");
        System.Console.WriteLine("2. CursorPhobia Engine Demo (Phase 2C - Live window pushing)");
        System.Console.WriteLine("3. Performance Tests (Engine benchmarking)");
        System.Console.WriteLine("4. System Tray Demo (Phase A WI#5 - Tray integration)");
        System.Console.WriteLine("q. Quit");
        System.Console.WriteLine();
        System.Console.WriteLine("Note: Options 2 & 4 will actively push windows away from your cursor!");
        System.Console.WriteLine("      Use CTRL key to temporarily disable, or press any key to stop.");
        System.Console.WriteLine("      Option 4 runs with system tray icon and context menu.");
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
    
    private static async Task RunTrayDemo()
    {
        System.Console.WriteLine("\nStarting System Tray Demo (Phase A WI#5)...\n");
        System.Console.WriteLine("⚠️  WARNING: This will show a system tray icon and actively push windows!");
        System.Console.WriteLine("    Use the tray context menu to control the engine.");
        System.Console.WriteLine("    Press any key to stop the demo.\n");
        
        System.Console.Write("Press ENTER to start, or any other key to cancel: ");
        var key = System.Console.ReadKey();
        System.Console.WriteLine();
        
        if (key.Key != ConsoleKey.Enter)
        {
            System.Console.WriteLine("Demo cancelled.");
            return;
        }
        
        // Initialize tray and lifecycle managers
        _trayManager = _serviceProvider!.GetRequiredService<ISystemTrayManager>();
        _lifecycleManager = _serviceProvider!.GetRequiredService<IApplicationLifecycleManager>();
        _engine = _serviceProvider!.GetRequiredService<ICursorPhobiaEngine>();
        
        try
        {
            await SetupTrayIntegration();
            
            System.Console.WriteLine("✅ System tray demo started!");
            System.Console.WriteLine("📍 Check your system tray for the CursorPhobia icon");
            System.Console.WriteLine("🔧 Right-click the tray icon to control the engine");
            System.Console.WriteLine("\nPress any key to stop the demo...\n");
            
            System.Console.ReadKey(true);
            System.Console.WriteLine("\nStopping tray demo...");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"❌ Tray demo error: {ex.Message}");
        }
        finally
        {
            await CleanupTrayIntegration();
            System.Console.WriteLine("✅ Tray demo stopped");
        }
    }
    
    private static async Task RunTrayMode()
    {
        try
        {
            // Initialize application with Windows Forms
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Initialize services
            _trayManager = _serviceProvider!.GetRequiredService<ISystemTrayManager>();
            _lifecycleManager = _serviceProvider!.GetRequiredService<IApplicationLifecycleManager>();
            _engine = _serviceProvider!.GetRequiredService<ICursorPhobiaEngine>();
            
            await SetupTrayIntegration();
            
            _logger!.LogInformation("CursorPhobia started in tray mode");
            System.Console.WriteLine("✅ CursorPhobia is now running in the system tray");
            System.Console.WriteLine("Right-click the tray icon to control the application");
            
            // Run the Windows Forms message loop
            Application.Run();
            
            _logger.LogInformation("Application message loop ended");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in tray mode");
            System.Console.WriteLine($"❌ Tray mode error: {ex.Message}");
        }
        finally
        {
            await CleanupTrayIntegration();
        }
    }
    
    private static async Task SetupTrayIntegration()
    {
        // Initialize lifecycle manager
        if (!await _lifecycleManager!.InitializeAsync())
        {
            throw new Exception("Failed to initialize lifecycle manager");
        }
        
        // Register services for lifecycle management
        if (_engine is IDisposable disposableEngine)
        {
            _lifecycleManager.RegisterService(disposableEngine, "CursorPhobia Engine");
        }
        _lifecycleManager.RegisterService(_trayManager!, "System Tray Manager");
        
        // Initialize tray manager
        if (!await _trayManager!.InitializeAsync())
        {
            throw new Exception("Failed to initialize tray manager");
        }
        
        // Setup tray event handlers
        _trayManager.ToggleEngineRequested += OnTrayToggleEngineRequested;
        _trayManager.SettingsRequested += OnTraySettingsRequested;
        _trayManager.AboutRequested += OnTrayAboutRequested;
        _trayManager.ExitRequested += OnTrayExitRequested;
        
        // Setup engine event handlers for tray notifications
        _engine!.EngineStateChanged += OnEngineStateChanged;
        _engine.PerformanceIssueDetected += OnEnginePerformanceIssue;
        _engine.WindowPushed += OnEngineWindowPushed;
        
        // Setup lifecycle event handlers
        _lifecycleManager.ApplicationExitRequested += OnApplicationExitRequested;
        
        // Start with engine disabled
        await _trayManager.UpdateStateAsync(TrayIconState.Disabled);
        await _trayManager.UpdateMenuStateAsync(false);
    }
    
    private static async Task CleanupTrayIntegration()
    {
        try
        {
            if (_engine != null)
            {
                if (_engine.IsRunning)
                {
                    await _engine.StopAsync();
                }
                
                _engine.EngineStateChanged -= OnEngineStateChanged;
                _engine.PerformanceIssueDetected -= OnEnginePerformanceIssue;
                _engine.WindowPushed -= OnEngineWindowPushed;
            }
            
            if (_trayManager != null)
            {
                _trayManager.ToggleEngineRequested -= OnTrayToggleEngineRequested;
                _trayManager.SettingsRequested -= OnTraySettingsRequested;
                _trayManager.AboutRequested -= OnTrayAboutRequested;
                _trayManager.ExitRequested -= OnTrayExitRequested;
                
                await _trayManager.HideAsync();
            }
            
            if (_lifecycleManager != null)
            {
                _lifecycleManager.ApplicationExitRequested -= OnApplicationExitRequested;
                await _lifecycleManager.ShutdownAsync();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during tray integration cleanup");
        }
    }
    
    // Tray event handlers
    private static async void OnTrayToggleEngineRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_engine!.IsRunning)
            {
                await _engine.StopAsync();
                await _trayManager!.ShowNotificationAsync("CursorPhobia", "Engine disabled", false);
            }
            else
            {
                if (await _engine.StartAsync())
                {
                    await _trayManager!.ShowNotificationAsync("CursorPhobia", "Engine enabled", false);
                }
                else
                {
                    await _trayManager!.ShowNotificationAsync("CursorPhobia", "Failed to start engine", true);
                }
            }
            
            await _trayManager.UpdateMenuStateAsync(_engine.IsRunning);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error toggling engine from tray");
            await _trayManager!.ShowNotificationAsync("CursorPhobia", $"Error: {ex.Message}", true);
        }
    }
    
    private static void OnTraySettingsRequested(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogInformation("Opening settings dialog from tray");
            
            // Create and show settings form
            using var settingsForm = new CursorPhobia.Core.UI.Forms.SettingsForm(
                _serviceProvider!.GetRequiredService<IConfigurationService>(),
                _engine!,
                _logger!);
            
            var result = settingsForm.ShowDialog();
            
            if (result == DialogResult.OK)
            {
                _logger?.LogInformation("Settings saved successfully");
                _trayManager?.ShowNotificationAsync("CursorPhobia", 
                    "Settings saved successfully", false);
            }
            else
            {
                _logger?.LogInformation("Settings dialog cancelled");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error opening settings dialog");
            _trayManager?.ShowNotificationAsync("CursorPhobia", 
                "Error opening settings: " + ex.Message, true);
        }
    }
    
    private static void OnTrayAboutRequested(object? sender, EventArgs e)
    {
        var message = "CursorPhobia v1.0 (Phase A)\nSystem Tray Integration\n\nPushes windows away from cursor";
        MessageBox.Show(message, "About CursorPhobia", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    
    private static async void OnTrayExitRequested(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Exit requested from tray");
        await _lifecycleManager!.ShutdownAsync();
        
        // Exit the application message loop if running in tray mode
        if (Application.MessageLoop)
        {
            Application.Exit();
        }
    }
    
    // Engine event handlers for tray notifications
    private static async void OnEngineStateChanged(object? sender, EngineStateChangedEventArgs e)
    {
        try
        {
            var trayState = e.State switch
            {
                EngineState.Running => TrayIconState.Enabled,
                EngineState.Stopped => TrayIconState.Disabled,
                EngineState.Error => TrayIconState.Error,
                _ => TrayIconState.Disabled
            };
            
            var tooltip = e.Message ?? $"CursorPhobia - {e.State}";
            await _trayManager!.UpdateStateAsync(trayState, tooltip);
            
            _logger?.LogDebug("Tray state updated: {State} - {Message}", e.State, e.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating tray state");
        }
    }
    
    private static async void OnEnginePerformanceIssue(object? sender, EnginePerformanceEventArgs e)
    {
        try
        {
            await _trayManager!.UpdateStateAsync(TrayIconState.Warning, 
                $"CursorPhobia - Performance Warning: {e.IssueType}");
            
            _logger?.LogWarning("Performance issue detected: {IssueType} - {Description}", 
                e.IssueType, e.Description);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling performance issue");
        }
    }
    
    private static void OnEngineWindowPushed(object? sender, WindowPushEventArgs e)
    {
        // Optional: Could show brief notifications or update statistics
        _logger?.LogDebug("Window pushed: {Title}", e.WindowInfo.Title);
    }
    
    private static void OnApplicationExitRequested(object? sender, EventArgs e)
    {
        _logger?.LogInformation("Application exit requested by lifecycle manager");
        
        // Exit the application message loop if running
        if (Application.MessageLoop)
        {
            Application.Exit();
        }
    }
}