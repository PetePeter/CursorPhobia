using System;
using System.Threading.Tasks;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;

/// <summary>
/// Demonstration of the configuration hot-swapping functionality
/// </summary>
public class ConfigurationHotSwapDemo
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== CursorPhobia Configuration Hot-Swap Demo ===");
        Console.WriteLine();

        // Create a logger
        var logger = new ConsoleLogger();
        
        // Create mock services for demonstration
        var mockCursorTracker = new MockService<ICursorTracker>();
        var mockProximityDetector = new MockService<IProximityDetector>();
        var mockWindowDetectionService = new MockService<IWindowDetectionService>();
        var mockWindowPusher = new MockService<IWindowPusher>();
        var mockSafetyManager = new MockService<ISafetyManager>();

        // Create initial configuration
        var initialConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 50,
            PushDistance = 100,
            EnableCtrlOverride = true,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = true,
            AnimationDurationMs = 200,
            EnableAnimations = true,
            UpdateIntervalMs = 16
        };

        Console.WriteLine("Initial Configuration:");
        PrintConfiguration(initialConfig);
        Console.WriteLine();

        // Create engine
        var engine = new CursorPhobiaEngine(
            logger,
            mockCursorTracker.Instance,
            mockProximityDetector.Instance,
            mockWindowDetectionService.Instance,
            mockWindowPusher.Instance,
            mockSafetyManager.Instance,
            initialConfig);

        // Subscribe to configuration update events
        engine.ConfigurationUpdated += (sender, e) =>
        {
            Console.WriteLine($"Configuration Updated: {e.UpdateResult.GetSummary()}");
            Console.WriteLine();
        };

        Console.WriteLine("=== Hot-Swappable Configuration Changes ===");
        Console.WriteLine("These changes can be applied immediately without restarting the engine:");
        Console.WriteLine();

        // Test 1: Hot-swappable changes
        var hotSwapConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Hot-swappable
            PushDistance = 150, // Hot-swappable
            EnableCtrlOverride = false, // Hot-swappable
            HoverTimeoutMs = 3000, // Hot-swappable
            EnableHoverTimeout = false, // Hot-swappable
            AnimationDurationMs = 300, // Hot-swappable
            EnableAnimations = false, // Hot-swappable
            UpdateIntervalMs = 16 // Same (no change)
        };

        Console.WriteLine("Applying hot-swappable changes...");
        var result1 = await engine.UpdateConfigurationAsync(hotSwapConfig);
        Console.WriteLine($"Result: {(result1.Success ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"Applied immediately: {string.Join(", ", result1.AppliedChanges)}");
        Console.WriteLine($"Requires restart: {string.Join(", ", result1.QueuedForRestart)}");
        Console.WriteLine();

        // Test 2: Restart-required changes
        Console.WriteLine("=== Restart-Required Configuration Changes ===");
        Console.WriteLine("These changes require an engine restart to take effect:");
        Console.WriteLine();

        var restartRequiredConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 75, // Same
            PushDistance = 150, // Same
            EnableCtrlOverride = false, // Same
            HoverTimeoutMs = 3000, // Same
            EnableHoverTimeout = false, // Same
            AnimationDurationMs = 300, // Same
            EnableAnimations = false, // Same
            UpdateIntervalMs = 33, // Restart required
            MaxUpdateIntervalMs = 100, // Restart required
            ApplyToAllWindows = true // Restart required
        };

        Console.WriteLine("Applying restart-required changes...");
        var result2 = await engine.UpdateConfigurationAsync(restartRequiredConfig);
        Console.WriteLine($"Result: {(result2.Success ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"Applied immediately: {string.Join(", ", result2.AppliedChanges)}");
        Console.WriteLine($"Requires restart: {string.Join(", ", result2.QueuedForRestart)}");
        Console.WriteLine();

        // Test 3: Mixed changes
        Console.WriteLine("=== Mixed Configuration Changes ===");
        Console.WriteLine("Some changes can be applied immediately, others require restart:");
        Console.WriteLine();

        var mixedConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = 100, // Hot-swappable
            PushDistance = 200, // Hot-swappable
            EnableCtrlOverride = true, // Hot-swappable
            HoverTimeoutMs = 4000, // Hot-swappable
            EnableHoverTimeout = true, // Hot-swappable
            AnimationDurationMs = 400, // Hot-swappable
            EnableAnimations = true, // Hot-swappable
            UpdateIntervalMs = 50, // Restart required
            MaxUpdateIntervalMs = 150, // Restart required
            ApplyToAllWindows = false // Restart required
        };

        Console.WriteLine("Applying mixed changes...");
        var result3 = await engine.UpdateConfigurationAsync(mixedConfig);
        Console.WriteLine($"Result: {(result3.Success ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"Applied immediately: {string.Join(", ", result3.AppliedChanges)}");
        Console.WriteLine($"Requires restart: {string.Join(", ", result3.QueuedForRestart)}");
        Console.WriteLine();

        // Test 4: Validation failure
        Console.WriteLine("=== Invalid Configuration ===");
        Console.WriteLine("Invalid configurations are rejected with detailed error messages:");
        Console.WriteLine();

        var invalidConfig = new CursorPhobiaConfiguration
        {
            ProximityThreshold = -10, // Invalid
            PushDistance = 2000, // Invalid
            UpdateIntervalMs = 0, // Invalid
            HoverTimeoutMs = 50 // Invalid
        };

        Console.WriteLine("Applying invalid configuration...");
        var result4 = await engine.UpdateConfigurationAsync(invalidConfig);
        Console.WriteLine($"Result: {(result4.Success ? "SUCCESS" : "FAILED")}");
        Console.WriteLine($"Error: {result4.ErrorMessage}");
        Console.WriteLine($"Validation Errors: {string.Join(", ", result4.ValidationErrors)}");
        Console.WriteLine();

        Console.WriteLine("=== Demo Complete ===");
        Console.WriteLine("The configuration hot-swapping system provides:");
        Console.WriteLine("- Thread-safe configuration updates");
        Console.WriteLine("- Immediate application of compatible changes");
        Console.WriteLine("- Clear indication of changes requiring restart");
        Console.WriteLine("- Comprehensive validation and error handling");
        Console.WriteLine("- Events for UI updates and logging");

        engine.Dispose();
    }

    private static void PrintConfiguration(CursorPhobiaConfiguration config)
    {
        Console.WriteLine($"  ProximityThreshold: {config.ProximityThreshold}px");
        Console.WriteLine($"  PushDistance: {config.PushDistance}px");
        Console.WriteLine($"  EnableCtrlOverride: {config.EnableCtrlOverride}");
        Console.WriteLine($"  HoverTimeoutMs: {config.HoverTimeoutMs}ms");
        Console.WriteLine($"  EnableHoverTimeout: {config.EnableHoverTimeout}");
        Console.WriteLine($"  AnimationDurationMs: {config.AnimationDurationMs}ms");
        Console.WriteLine($"  EnableAnimations: {config.EnableAnimations}");
        Console.WriteLine($"  UpdateIntervalMs: {config.UpdateIntervalMs}ms");
    }
}

/// <summary>
/// Simple console logger for demonstration
/// </summary>
public class ConsoleLogger : ILogger
{
    public void LogDebug(string message, params object[] args) { }
    public void LogInformation(string message, params object[] args)
    {
        Console.WriteLine($"[INFO] {string.Format(message, args)}");
    }
    public void LogWarning(string message, params object[] args)
    {
        Console.WriteLine($"[WARN] {string.Format(message, args)}");
    }
    public void LogError(Exception ex, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}: {ex.Message}");
    }
    public void LogError(string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}");
    }
    public void LogCritical(Exception ex, string message, params object[] args)
    {
        Console.WriteLine($"[CRITICAL] {string.Format(message, args)}: {ex.Message}");
    }
}

/// <summary>
/// Mock service implementation for demonstration
/// </summary>
public class MockService<T> where T : class
{
    public T Instance { get; }

    public MockService()
    {
        var mockObject = System.Runtime.Remoting.Proxies.RealProxy.GetTransparentProxy(
            new MockProxy(typeof(T))) as T;
        Instance = mockObject ?? throw new InvalidOperationException($"Could not create mock for {typeof(T).Name}");
    }
}

/// <summary>
/// Simple mock proxy for demonstration
/// </summary>
public class MockProxy : System.Runtime.Remoting.Proxies.RealProxy
{
    public MockProxy(Type type) : base(type) { }

    public override System.Runtime.Remoting.Messaging.IMessage Invoke(System.Runtime.Remoting.Messaging.IMessage msg)
    {
        var methodCall = msg as System.Runtime.Remoting.Messaging.IMethodCallMessage;
        if (methodCall != null)
        {
            // Return default values for different return types
            var returnType = methodCall.MethodBase.ReturnType;
            object returnValue = null;

            if (returnType == typeof(bool))
                returnValue = false;
            else if (returnType == typeof(int))
                returnValue = 0;
            else if (returnType == typeof(string))
                returnValue = string.Empty;
            else if (returnType == typeof(Task))
                returnValue = Task.CompletedTask;
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var taskType = returnType.GetGenericArguments()[0];
                if (taskType == typeof(bool))
                    returnValue = Task.FromResult(false);
                else
                    returnValue = Activator.CreateInstance(returnType);
            }

            return new System.Runtime.Remoting.Messaging.ReturnMessage(
                returnValue, null, 0, methodCall.LogicalCallContext, methodCall);
        }

        return msg;
    }
}