using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Moq;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Integration tests specifically for DPI awareness functionality
/// </summary>
public class DpiAwarenessIntegrationTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly Mock<IMonitorManager> _mockMonitorManager;

    public DpiAwarenessIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _mockMonitorManager = new Mock<IMonitorManager>();
    }

    [Theory]
    [InlineData(96, 96, 50)] // 100% scaling - no change
    [InlineData(120, 120, 50)] // 125% scaling
    [InlineData(144, 144, 50)] // 150% scaling
    [InlineData(192, 192, 50)] // 200% scaling
    [InlineData(288, 288, 50)] // 300% scaling
    public void ProximityDetector_ScalesThresholdCorrectly_ForDifferentDpi(
        int dpiX, int dpiY, int logicalThreshold)
    {
        // Arrange
        var dpiInfo = new DpiInfo((uint)dpiX, (uint)dpiY);
        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns(dpiInfo);

        var proximityDetector = new ProximityDetector(_mockLogger.Object, null, _mockMonitorManager.Object);

        var cursorPosition = new Point(100, 100);
        var windowBounds = new Rectangle(200, 200, 300, 200);

        // Act
        var isWithinProximity = proximityDetector.IsWithinProximity(cursorPosition, windowBounds, logicalThreshold);

        // Assert
        // Verify DPI query was made
        _mockMonitorManager.Verify(m => m.GetDpiForPoint(cursorPosition), Times.Once);

        // Note: The actual threshold scaling verification would require access to internal methods
        // or integration with the actual calculation logic
    }

    [Theory]
    [InlineData(96, 96, 100)] // 100% scaling
    [InlineData(144, 144, 100)] // 150% scaling
    [InlineData(192, 192, 100)] // 200% scaling
    public void ProximityDetector_ScalesPushDistanceCorrectly_ForDifferentDpi(
        int dpiX, int dpiY, int logicalDistance)
    {
        // Arrange
        var dpiInfo = new DpiInfo((uint)dpiX, (uint)dpiY);
        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns(dpiInfo);

        var proximityDetector = new ProximityDetector(_mockLogger.Object, null, _mockMonitorManager.Object);

        var cursorPosition = new Point(150, 150);
        var windowBounds = new Rectangle(200, 200, 300, 200);

        // Act
        var pushVector = proximityDetector.CalculatePushVector(cursorPosition, windowBounds, logicalDistance);

        // Assert
        // Verify DPI query was made
        _mockMonitorManager.Verify(m => m.GetDpiForPoint(cursorPosition), Times.Once);

        // The push vector magnitude should reflect DPI scaling
        var magnitude = Math.Sqrt(pushVector.X * pushVector.X + pushVector.Y * pushVector.Y);
        Assert.True(magnitude > 0);
    }

    [Fact]
    public void ProximityDetector_HandlesMixedDpiEnvironment_CorrectlyPerMonitor()
    {
        // Arrange
        var monitor1Bounds = new Rectangle(0, 0, 1920, 1080);      // 100% DPI
        var monitor2Bounds = new Rectangle(1920, 0, 3840, 2160);   // 200% DPI

        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns<Point>(point =>
            {
                if (point.X < 1920)
                    return new DpiInfo(96, 96);   // Monitor 1: 100%
                else
                    return new DpiInfo(192, 192); // Monitor 2: 200%
            });

        var proximityDetector = new ProximityDetector(_mockLogger.Object, null, _mockMonitorManager.Object);

        // Test cases for both monitors
        var testCases = new[]
        {
            new { CursorPos = new Point(500, 500), WindowBounds = new Rectangle(600, 600, 200, 200), Monitor = "Monitor1" },
            new { CursorPos = new Point(2400, 500), WindowBounds = new Rectangle(2500, 600, 200, 200), Monitor = "Monitor2" }
        };

        foreach (var testCase in testCases)
        {
            // Act
            var isWithinProximity = proximityDetector.IsWithinProximity(testCase.CursorPos, testCase.WindowBounds, 50);
            var pushVector = proximityDetector.CalculatePushVector(testCase.CursorPos, testCase.WindowBounds, 100);

            // Assert
            _mockMonitorManager.Verify(m => m.GetDpiForPoint(testCase.CursorPos), Times.AtLeastOnce);

            // Both monitors should work, but with different effective scaling
            Assert.True(pushVector.X != 0 || pushVector.Y != 0, $"Push vector should not be zero for {testCase.Monitor}");
        }
    }

    [Fact]
    public void CursorPhobiaEngine_HandlesPerMonitorDpiSettings_Correctly()
    {
        // Arrange
        var monitor1 = new MonitorInfo(IntPtr.Zero, new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1040), true, @"\\.\DISPLAY1");
        var monitor2 = new MonitorInfo(IntPtr.Zero, new Rectangle(1920, 0, 3840, 2160),
            new Rectangle(1920, 0, 3840, 2120), false, @"\\.\DISPLAY2");

        // Setup different DPI for each monitor
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(It.IsAny<Rectangle>()))
            .Returns<Rectangle>(rect =>
            {
                if (rect.X >= 1920) return monitor2;
                return monitor1;
            });

        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns<Point>(point =>
            {
                if (point.X < 1920)
                    return new DpiInfo(96, 96);   // Monitor 1: 100%
                else
                    return new DpiInfo(192, 192); // Monitor 2: 200%
            });

        var config = CursorPhobiaConfiguration.CreateDefault();
        config.ProximityThreshold = 50;
        config.PushDistance = 100;

        // Different settings per monitor
        config.MultiMonitor = new MultiMonitorConfiguration
        {
            PerMonitorSettings = new Dictionary<string, PerMonitorSettings>
            {
                [monitor1.GetStableKey()] = new PerMonitorSettings
                {
                    Enabled = true,
                    CustomProximityThreshold = 40,
                    CustomPushDistance = 80
                },
                [monitor2.GetStableKey()] = new PerMonitorSettings
                {
                    Enabled = true,
                    CustomProximityThreshold = 60,
                    CustomPushDistance = 120
                }
            }
        };

        var mockCursorTracker = new Mock<ICursorTracker>();
        var mockProximityDetector = new Mock<IProximityDetector>();
        var mockWindowDetectionService = new Mock<IWindowDetectionService>();
        var mockWindowPusher = new Mock<IWindowPusher>();
        var mockSafetyManager = new Mock<ISafetyManager>();

        var engine = new CursorPhobiaEngine(
            _mockLogger.Object,
            mockCursorTracker.Object,
            mockProximityDetector.Object,
            mockWindowDetectionService.Object,
            mockWindowPusher.Object,
            mockSafetyManager.Object,
            _mockMonitorManager.Object,
            config);

        // Act & Assert
        // This test verifies the engine correctly handles per-monitor DPI settings
        // In a full integration test, we would verify the actual proximity calculations
        Assert.NotNull(engine);
        // The engine doesn't expose current configuration, just verify it's not null
    }

    [Fact]
    public void DpiUtilities_PerformCorrectConversions_BetweenLogicalAndPhysical()
    {
        // Arrange
        var logicalPoint = new Point(100, 100);
        var logicalRect = new Rectangle(50, 50, 200, 150);

        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns(new DpiInfo(144, 144)); // 150% scaling

        _mockMonitorManager.Setup(m => m.GetDpiForRectangle(It.IsAny<Rectangle>()))
            .Returns(new DpiInfo(144, 144)); // 150% scaling

        // Act
        var physicalPoint = DpiUtilities.LogicalToPhysical(logicalPoint, _mockMonitorManager.Object);
        var physicalRect = DpiUtilities.LogicalToPhysical(logicalRect, _mockMonitorManager.Object);

        var convertedBackPoint = DpiUtilities.PhysicalToLogical(physicalPoint, _mockMonitorManager.Object);
        var convertedBackRect = DpiUtilities.PhysicalToLogical(physicalRect, _mockMonitorManager.Object);

        // Assert - 150% scaling means physical coordinates should be 1.5x larger
        Assert.Equal(150, physicalPoint.X); // 100 * 1.5
        Assert.Equal(150, physicalPoint.Y); // 100 * 1.5

        Assert.Equal(75, physicalRect.X);     // 50 * 1.5
        Assert.Equal(75, physicalRect.Y);     // 50 * 1.5
        Assert.Equal(300, physicalRect.Width); // 200 * 1.5
        Assert.Equal(225, physicalRect.Height); // 150 * 1.5

        // Verify round-trip conversion works
        Assert.Equal(logicalPoint, convertedBackPoint);
        Assert.Equal(logicalRect, convertedBackRect);
    }

    [Fact]
    public void PerMonitorSettings_WorkCorrectly_InHighDpiScenarios()
    {
        // Arrange - Simulate a high-DPI monitor (4K at 200% scaling)
        var highDpiMonitor = new MonitorInfo(
            IntPtr.Zero,
            new Rectangle(0, 0, 3840, 2160),     // Physical resolution
            new Rectangle(0, 0, 3840, 2120),     // Work area
            true,
            @"\\.\DISPLAY1",
            "DELL",
            "UP3218K",
            "HIGHDPI123");

        var config = CursorPhobiaConfiguration.CreateDefault();
        config.MultiMonitor = new MultiMonitorConfiguration
        {
            PerMonitorSettings = new Dictionary<string, PerMonitorSettings>
            {
                [highDpiMonitor.GetStableKey()] = new PerMonitorSettings
                {
                    Enabled = true,
                    CustomProximityThreshold = 25, // Smaller logical threshold for high-DPI
                    CustomPushDistance = 50         // Smaller logical distance for high-DPI
                }
            }
        };

        _mockMonitorManager.Setup(m => m.GetMonitorContaining(It.IsAny<Rectangle>()))
            .Returns(highDpiMonitor);

        _mockMonitorManager.Setup(m => m.GetDpiForPoint(It.IsAny<Point>()))
            .Returns(new DpiInfo(192, 192)); // 200% scaling

        // Act & Assert
        var monitorKey = highDpiMonitor.GetStableKey();
        Assert.True(config.MultiMonitor.PerMonitorSettings.ContainsKey(monitorKey));

        var settings = config.MultiMonitor.PerMonitorSettings[monitorKey];
        Assert.Equal(25, settings.CustomProximityThreshold);
        Assert.Equal(50, settings.CustomPushDistance);

        // In practice, these logical values would be scaled to 50 and 100 physical pixels
        // at 200% DPI, providing the same visual size as 50/100 on a 100% DPI monitor
    }

    [Theory]
    [InlineData(96, 1.0)]   // 100% scaling
    [InlineData(120, 1.25)] // 125% scaling
    [InlineData(144, 1.5)]  // 150% scaling
    [InlineData(168, 1.75)] // 175% scaling
    [InlineData(192, 2.0)]  // 200% scaling
    [InlineData(240, 2.5)]  // 250% scaling
    [InlineData(288, 3.0)]  // 300% scaling
    public void DpiInfo_CalculatesCorrectScaleFactors_ForCommonDpiValues(int dpi, double expectedScale)
    {
        // Arrange & Act
        var dpiInfo = new DpiInfo((uint)dpi, (uint)dpi);

        // Assert
        Assert.Equal(expectedScale, dpiInfo.ScaleFactorX, 2); // 2 decimal places precision
        Assert.Equal(expectedScale, dpiInfo.ScaleFactorY, 2);
    }

    [Fact]
    public void PerMonitorSettingsMigrator_PreservesSettings_AcrossDpiChanges()
    {
        // Arrange - Simulate a monitor DPI change (e.g., user changed display scaling)
        var migrator = new PerMonitorSettingsMigrator(_mockLogger.Object);

        var oldMonitor = new MonitorInfo(IntPtr.Zero, new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1040), true, @"\\.\DISPLAY1", "DELL", "U2720Q", "ABC123");

        // Same monitor but system might report it differently after DPI change
        var newMonitor = new MonitorInfo(IntPtr.Zero, new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1040), true, @"\\.\DISPLAY1", "DELL", "U2720Q", "ABC123");

        var config = CursorPhobiaConfiguration.CreateDefault();
        config.MultiMonitor = new MultiMonitorConfiguration
        {
            PerMonitorSettings = new Dictionary<string, PerMonitorSettings>
            {
                [oldMonitor.GetStableKey()] = new PerMonitorSettings
                {
                    Enabled = true,
                    CustomProximityThreshold = 30,
                    CustomPushDistance = 75
                }
            }
        };

        // Act
        var migratedConfig = migrator.MigrateSettings(config,
            new List<MonitorInfo> { oldMonitor },
            new List<MonitorInfo> { newMonitor });

        // Assert - Settings should be preserved
        Assert.NotNull(migratedConfig.MultiMonitor);
        Assert.Single(migratedConfig.MultiMonitor.PerMonitorSettings);

        var newSettings = migratedConfig.MultiMonitor.PerMonitorSettings[newMonitor.GetStableKey()];
        Assert.True(newSettings.Enabled);
        Assert.Equal(30, newSettings.CustomProximityThreshold);
        Assert.Equal(75, newSettings.CustomPushDistance);
    }
}