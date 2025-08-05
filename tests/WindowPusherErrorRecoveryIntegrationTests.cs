using Microsoft.VisualStudio.TestTools.UnitTesting;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.Models;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CursorPhobia.Tests;

/// <summary>
/// Integration tests for WindowPusher with ErrorRecoveryManager
/// Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
[TestClass]
public class WindowPusherErrorRecoveryIntegrationTests
{
    private TestLogger _logger = null!;
    private TestWindowManipulationService _windowService = null!;
    private TestSafetyManager _safetyManager = null!;
    private TestProximityDetector _proximityDetector = null!;
    private TestWindowDetectionService _windowDetectionService = null!;
    private MonitorManager _monitorManager = null!;
    private EdgeWrapHandler _edgeWrapHandler = null!;
    private ErrorRecoveryManager _errorRecoveryManager = null!;
    private WindowPusher _windowPusher = null!;
    
    [TestInitialize]
    public void TestInitialize()
    {
        _logger = new TestLogger();
        _windowService = new TestWindowManipulationService();
        _safetyManager = new TestSafetyManager();
        _proximityDetector = new TestProximityDetector();
        _windowDetectionService = new TestWindowDetectionService();
        _monitorManager = new MonitorManager(_logger);
        _edgeWrapHandler = new EdgeWrapHandler(_logger, _monitorManager);
        _errorRecoveryManager = new ErrorRecoveryManager(_logger);
        
        // Initialize error recovery manager
        Task.Run(async () => await _errorRecoveryManager.InitializeAsync()).Wait();
        
        _windowPusher = new WindowPusher(
            _logger,
            _windowService,
            _safetyManager,
            _proximityDetector,
            _windowDetectionService,
            _monitorManager,
            _edgeWrapHandler,
            null, // config - use default
            _errorRecoveryManager,
            null  // tray manager
        );
        
        // Wait for error recovery registration to complete
        Task.Delay(100).Wait();
    }
    
    [TestCleanup]
    public void TestCleanup()
    {
        _windowPusher?.Dispose();
        _errorRecoveryManager?.Dispose();
    }
    
    [TestMethod]
    public async Task PushWindowAsync_ShouldReportSuccess_WhenOperationSucceeds()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        _windowService.SetWindowBounds(windowHandle, new Rectangle(90, 90, 100, 50));
        _proximityDetector.SetPushVector(new Point(50, 0)); // Push right
        _safetyManager.SetValidPosition(new Point(140, 90)); // Allow the push
        
        // Act
        var result = await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Assert
        Assert.IsTrue(result);
        
        // Verify that error recovery manager has the component registered
        var registeredComponents = _errorRecoveryManager.GetRegisteredComponents();
        Assert.IsTrue(registeredComponents.Contains("WindowPusher"));
        
        // Verify recovery statistics show success
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.AreEqual(0, stats.ConsecutiveFailures);
    }
    
    [TestMethod]
    public async Task PushWindowAsync_ShouldReportFailure_WhenOperationFails()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        // Make window service fail
        _windowService.ShouldFail = true;
        _windowService.SetWindowBounds(windowHandle, Rectangle.Empty); // Simulate failure
        
        // Act
        var result = await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Assert
        Assert.IsFalse(result);
        
        // Verify that error recovery manager recorded the failure
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.TotalFailures > 0);
    }
    
    [TestMethod]
    public async Task PushWindowAsync_ShouldTriggerRecovery_AfterMultipleFailures()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        // Make window service fail initially
        _windowService.ShouldFail = true;
        _windowService.SetWindowBounds(windowHandle, Rectangle.Empty);
        
        // Act - Trigger multiple failures to reach failure threshold
        await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Wait for recovery to be triggered
        await Task.Delay(100);
        
        // Assert
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.TotalFailures >= 3);
        
        // Check circuit breaker state
        var circuitBreakerState = _errorRecoveryManager.GetCircuitBreakerState("WindowPusher");
        Assert.AreEqual(CircuitBreakerState.Open, circuitBreakerState);
    }
    
    [TestMethod]
    public async Task ErrorRecovery_ShouldResetFailureCount_WhenRecoverySucceeds()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        // Cause some failures first
        _windowService.ShouldFail = true;
        _windowService.SetWindowBounds(windowHandle, Rectangle.Empty);
        await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Verify failures were recorded
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.ConsecutiveFailures > 0);
        
        // Fix the service and succeed
        _windowService.ShouldFail = false;
        _windowService.SetWindowBounds(windowHandle, new Rectangle(90, 90, 100, 50));
        _proximityDetector.SetPushVector(new Point(50, 0));
        _safetyManager.SetValidPosition(new Point(140, 90));
        
        // Act
        var result = await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Assert
        Assert.IsTrue(result);
        
        // Verify that consecutive failures were reset
        stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.AreEqual(0, stats.ConsecutiveFailures);
    }
    
    [TestMethod]
    public async Task TriggerManualRecovery_ShouldExecuteWindowPusherRecovery()
    {
        // Arrange
        // Setup some windows for the recovery validation
        var windows = new List<WindowInfo>
        {
            new WindowInfo
            {
                WindowHandle = new IntPtr(1),
                Title = "Test Window 1",
                ClassName = "TestClass",
                Bounds = new Rectangle(10, 10, 100, 50),
                IsVisible = true,
                IsTopmost = false
            },
            new WindowInfo
            {
                WindowHandle = new IntPtr(2),
                Title = "Test Window 2",
                ClassName = "TestClass",
                Bounds = new Rectangle(50, 50, 100, 50),
                IsVisible = true,
                IsTopmost = false
            }
        };
        
        _windowDetectionService.SetWindows(windows);
        
        // Act
        var result = await _errorRecoveryManager.TriggerRecoveryAsync("WindowPusher", "Manual test recovery");
        
        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual("Manual test recovery", result.Context);
        Assert.IsTrue(result.AttemptsCount > 0);
        
        // Verify recovery statistics were updated
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.SuccessfulRecoveries > 0);
    }
    
    [TestMethod]
    public async Task RecoveryValidation_ShouldFail_WhenNoWindowsAvailable()
    {
        // Arrange
        _windowDetectionService.SetWindows(new List<WindowInfo>()); // No windows
        
        // Act
        var result = await _errorRecoveryManager.TriggerRecoveryAsync("WindowPusher", "Test with no windows");
        
        // Assert
        Assert.IsFalse(result.Success);
        
        // Verify failure was recorded
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.FailedRecoveries > 0);
    }
    
    [TestMethod]
    public async Task RecoveryValidation_ShouldSucceed_WithValidWindows()
    {
        // Arrange
        var windows = new List<WindowInfo>
        {
            new WindowInfo
            {
                WindowHandle = new IntPtr(1),
                Title = "Valid Window",
                ClassName = "TestClass",
                Bounds = new Rectangle(10, 10, 100, 50),
                IsVisible = true,
                IsTopmost = false,
                ProcessId = 1234,
                ThreadId = 5678
            }
        };
        
        _windowDetectionService.SetWindows(windows);
        _windowService.SetWindowBounds(new IntPtr(1), new Rectangle(10, 10, 100, 50));
        _windowService.SetWindowVisibility(new IntPtr(1), true);
        
        // Act
        var result = await _errorRecoveryManager.TriggerRecoveryAsync("WindowPusher", "Test with valid windows");
        
        // Assert
        Assert.IsTrue(result.Success);
        
        // Verify success was recorded
        var stats = _errorRecoveryManager.GetRecoveryStatistics("WindowPusher");
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.SuccessfulRecoveries > 0);
    }
    
    [TestMethod]
    public async Task CircuitBreaker_ShouldPreventOperations_WhenOpen()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        // Force circuit breaker to open by causing multiple failures
        _windowService.ShouldFail = true;
        _windowService.SetWindowBounds(windowHandle, Rectangle.Empty);
        
        // Trigger enough failures to open circuit breaker
        for (int i = 0; i < 5; i++)
        {
            await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        }
        
        // Verify circuit breaker is open
        var circuitBreakerState = _errorRecoveryManager.GetCircuitBreakerState("WindowPusher");
        Assert.AreEqual(CircuitBreakerState.Open, circuitBreakerState);
        
        // Fix the service
        _windowService.ShouldFail = false;
        _windowService.SetWindowBounds(windowHandle, new Rectangle(90, 90, 100, 50));
        
        // Act - Try to push window while circuit breaker is open
        var result = await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Assert - Operation should still fail due to open circuit breaker
        Assert.IsFalse(result);
    }
    
    [TestMethod]
    public async Task ResetCircuitBreaker_ShouldAllowOperations_AfterReset()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        var cursorPosition = new Point(100, 100);
        var pushDistance = 50;
        
        // Force circuit breaker to open
        _windowService.ShouldFail = true;
        _windowService.SetWindowBounds(windowHandle, Rectangle.Empty);
        
        for (int i = 0; i < 5; i++)
        {
            await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        }
        
        // Verify circuit breaker is open
        Assert.AreEqual(CircuitBreakerState.Open, _errorRecoveryManager.GetCircuitBreakerState("WindowPusher"));
        
        // Fix the service and reset circuit breaker
        _windowService.ShouldFail = false;
        _windowService.SetWindowBounds(windowHandle, new Rectangle(90, 90, 100, 50));
        _proximityDetector.SetPushVector(new Point(50, 0));
        _safetyManager.SetValidPosition(new Point(140, 90));
        
        // Act
        var resetResult = await _errorRecoveryManager.ResetCircuitBreakerAsync("WindowPusher");
        Assert.IsTrue(resetResult);
        
        var pushResult = await _windowPusher.PushWindowAsync(windowHandle, cursorPosition, pushDistance);
        
        // Assert
        Assert.IsTrue(pushResult);
        Assert.AreEqual(CircuitBreakerState.Closed, _errorRecoveryManager.GetCircuitBreakerState("WindowPusher"));
    }
    
    [TestMethod]
    public async Task Dispose_ShouldUnregisterFromErrorRecovery()
    {
        // Arrange
        var registeredComponents = _errorRecoveryManager.GetRegisteredComponents();
        Assert.IsTrue(registeredComponents.Contains("WindowPusher"));
        
        // Act
        _windowPusher.Dispose();
        
        // Wait for unregistration to complete
        await Task.Delay(100);
        
        // Assert
        registeredComponents = _errorRecoveryManager.GetRegisteredComponents();
        Assert.IsFalse(registeredComponents.Contains("WindowPusher"));
    }
    
    [TestMethod]
    public async Task HealthCheck_ShouldReturnHealthyStatus_WhenComponentsAreWorking()
    {
        // Arrange
        var windowHandle = new IntPtr(123);
        _windowService.SetWindowBounds(windowHandle, new Rectangle(90, 90, 100, 50));
        _windowService.SetWindowVisibility(windowHandle, true);
        
        var windows = new List<WindowInfo>
        {
            new WindowInfo
            {
                WindowHandle = windowHandle,
                Title = "Test Window",
                ClassName = "TestClass",
                Bounds = new Rectangle(90, 90, 100, 50),
                IsVisible = true
            }
        };
        _windowDetectionService.SetWindows(windows);
        
        // Act
        var healthCheck = await _errorRecoveryManager.PerformHealthCheckAsync();
        
        // Assert
        Assert.IsTrue(healthCheck.IsHealthy);
        Assert.IsTrue(healthCheck.ComponentResults.ContainsKey("WindowPusher"));
        
        var windowPusherHealth = healthCheck.ComponentResults["WindowPusher"];
        Assert.IsTrue(windowPusherHealth.IsHealthy);
        Assert.AreEqual(CircuitBreakerState.Closed, windowPusherHealth.CircuitBreakerState);
    }
}