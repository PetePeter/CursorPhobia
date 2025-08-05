using System;
using System.Threading.Tasks;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Tests
{
    /// <summary>
    /// Simple test program to verify error recovery functionality
    /// </summary>
    class TestErrorRecovery
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("CursorPhobia Error Recovery Test");
            Console.WriteLine("================================");
            
            var logger = new TestLogger();
            var errorRecoveryManager = new ErrorRecoveryManager(logger);
            
            try
            {
                // Initialize error recovery manager
                Console.WriteLine("1. Initializing error recovery manager...");
                var initialized = await errorRecoveryManager.InitializeAsync();
                Console.WriteLine($"   Result: {(initialized ? "SUCCESS" : "FAILED")}");
                
                if (!initialized)
                {
                    Console.WriteLine("Failed to initialize error recovery manager");
                    return;
                }
                
                // Register a test component
                Console.WriteLine("2. Registering test component...");
                int recoveryAttempts = 0;
                var registered = await errorRecoveryManager.RegisterComponentAsync("TestComponent", async () =>
                {
                    recoveryAttempts++;
                    Console.WriteLine($"   Recovery attempt #{recoveryAttempts}");
                    return recoveryAttempts <= 2; // Fail first 2 attempts, succeed on 3rd
                });
                Console.WriteLine($"   Result: {(registered ? "SUCCESS" : "FAILED")}");
                
                // Simulate failures
                Console.WriteLine("3. Simulating component failures...");
                for (int i = 1; i <= 3; i++)
                {
                    Console.WriteLine($"   Reporting failure #{i}...");
                    var result = await errorRecoveryManager.ReportFailureAsync("TestComponent", 
                        new InvalidOperationException($"Test failure #{i}"), $"Test context {i}");
                    Console.WriteLine($"   Recovery result: Success={result.Success}, Attempts={result.AttemptsCount}");
                }
                
                // Check circuit breaker state
                Console.WriteLine("4. Checking circuit breaker state...");
                var circuitState = errorRecoveryManager.GetCircuitBreakerState("TestComponent");
                Console.WriteLine($"   Circuit breaker state: {circuitState}");
                
                // Get recovery statistics
                Console.WriteLine("5. Getting recovery statistics...");
                var stats = errorRecoveryManager.GetRecoveryStatistics("TestComponent");
                if (stats != null)
                {
                    Console.WriteLine($"   Total failures: {stats.TotalFailures}");
                    Console.WriteLine($"   Successful recoveries: {stats.SuccessfulRecoveries}");
                    Console.WriteLine($"   Failed recoveries: {stats.FailedRecoveries}");
                    Console.WriteLine($"   Consecutive failures: {stats.ConsecutiveFailures}");
                }
                
                // Perform health check
                Console.WriteLine("6. Performing health check...");
                var healthCheck = await errorRecoveryManager.PerformHealthCheckAsync();
                Console.WriteLine($"   Overall health: {(healthCheck.IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
                Console.WriteLine($"   Components checked: {healthCheck.ComponentResults.Count}");
                
                Console.WriteLine("\n✓ Error recovery test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error recovery test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Cleanup
                Console.WriteLine("7. Cleaning up...");
                await errorRecoveryManager.ShutdownAsync();
                errorRecoveryManager.Dispose();
                Console.WriteLine("   Cleanup completed");
            }
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
    
    /// <summary>
    /// Simple test logger implementation
    /// </summary>
    public class TestLogger : ILogger
    {
        public void LogDebug(string message, params object[] args)
        {
            Console.WriteLine($"[DEBUG] {string.Format(message, args)}");
        }
        
        public void LogInformation(string message, params object[] args)
        {
            Console.WriteLine($"[INFO]  {string.Format(message, args)}");
        }
        
        public void LogWarning(string message, params object[] args)
        {
            Console.WriteLine($"[WARN]  {string.Format(message, args)}");
        }
        
        public void LogError(string message, params object[] args)
        {
            Console.WriteLine($"[ERROR] {string.Format(message, args)}");
        }
        
        public void LogCritical(string message, params object[] args)
        {
            Console.WriteLine($"[CRIT]  {string.Format(message, args)}");
        }
    }
}