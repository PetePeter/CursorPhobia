using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Logging;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using System.IO;
using log4net;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for log4net integration and logging functionality
/// Verifies that log4net properly replaces the custom logging system
/// </summary>
public class Log4NetLoggingTests : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private string _tempLogDirectory = string.Empty;

    public Log4NetLoggingTests()
    {
        // Create a temporary directory for test logs
        _tempLogDirectory = Path.Combine(Path.GetTempPath(), "CursorPhobiaTests", Guid.NewGuid().ToString());
        
        // Ensure the directory exists
        Directory.CreateDirectory(_tempLogDirectory);
        
        // Set up log4net configuration for testing
        var testConfigPath = CreateTestLog4NetConfig();
        
        // Initialize logging with log4net
        var success = Log4NetConfiguration.InitializeLogging(true, testConfigPath);
        Assert.True(success);
        
        // Set up dependency injection
        var services = new ServiceCollection();
        Log4NetConfiguration.ConfigureLoggingServices(services, true);
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        Log4NetConfiguration.ShutdownLogging();
        
        // Clean up test log directory
        try
        {
            if (Directory.Exists(_tempLogDirectory))
            {
                Directory.Delete(_tempLogDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    [Fact]
    public void Log4NetLogger_CanBeCreated()
    {
        // Arrange & Act
        var log4netLogger = LogManager.GetLogger("Test");
        var logger = new Log4NetLogger(log4netLogger, "Test");

        // Assert
        Assert.NotNull(logger);
        Assert.IsAssignableFrom<CursorPhobia.Core.Utilities.ILogger>(logger);
        Assert.IsAssignableFrom<Microsoft.Extensions.Logging.ILogger>(logger);
        Assert.IsAssignableFrom<IProductionLogger>(logger);
    }

    [Fact]
    public void Log4NetLoggerFactory_CreatesLoggers()
    {
        // Arrange
        var factory = new Log4NetLoggerFactory();

        // Act
        var logger = factory.CreateLogger("Test");

        // Assert
        Assert.NotNull(logger);
        Assert.IsType<Log4NetLogger>(logger);
    }

    [Fact]
    public void DependencyInjection_RegistersLoggers()
    {
        // Arrange & Act
        var legacyLogger = _serviceProvider!.GetService<CursorPhobia.Core.Utilities.ILogger>();
        var productionLogger = _serviceProvider.GetService<IProductionLogger>();
        var loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

        // Assert
        Assert.NotNull(legacyLogger);
        Assert.NotNull(productionLogger);
        Assert.NotNull(loggerFactory);
        Assert.IsType<Log4NetLoggerFactory>(loggerFactory);
    }

    [Fact]
    public void Logger_LogsAtDifferentLevels()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<CursorPhobia.Core.Utilities.ILogger>();

        // Act & Assert - Should not throw
        logger.LogDebug("Debug message");
        logger.LogInformation("Information message");
        logger.LogWarning("Warning message");
        logger.LogError("Error message");
        logger.LogCritical(new Exception("Test exception"), "Critical message");
    }

    [Fact]
    public void ProductionLogger_LogsWithContext()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();

        // Act & Assert - Should not throw
        logger.LogWithContext(LogLevel.Information, "Test message", 
            ("Property1", "Value1"), 
            ("Property2", 42));
    }

    [Fact]
    public void ProductionLogger_LogsPerformanceMetrics()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();

        // Act & Assert - Should not throw
        logger.LogPerformance("TestService", "TestOperation", TimeSpan.FromMilliseconds(100), true,
            ("AdditionalContext", "ContextValue"));
    }

    [Fact]
    public void ProductionLogger_LogsWindowOperations()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();
        var windowHandle = new IntPtr(0x12345);

        // Act & Assert - Should not throw
        logger.LogWindowOperation(LogLevel.Information, "PushWindow", windowHandle, 
            "Test Window", "Window operation test", 
            ("PushDistance", 150));
    }

    [Fact]
    public void ProductionLogger_LogsSystemEvents()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();

        // Act & Assert - Should not throw
        logger.LogSystemEvent(LogLevel.Information, "Cursor", "PositionChanged", 
            "Cursor position changed", 
            ("X", 100), 
            ("Y", 200));
    }

    [Fact]
    public void ProductionLogger_CreatesServiceLogger()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();

        // Act
        var serviceLogger = logger.CreateServiceLogger("WindowPusher");

        // Assert
        Assert.NotNull(serviceLogger);
        Assert.IsAssignableFrom<IProductionLogger>(serviceLogger);
    }

    [Fact]
    public void ProductionLogger_CreatesPerformanceScope()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();

        // Act
        using var scope = logger.BeginPerformanceScope("TestService", "TestOperation", 
            ("Context", "Value"));

        // Assert
        Assert.NotNull(scope);
        Assert.IsAssignableFrom<IPerformanceScope>(scope);
        
        // Test scope functionality  
        var perfScope = scope as IPerformanceScope;
        Assert.NotNull(perfScope);
        perfScope.AddContext("AdditionalContext", "AdditionalValue");
        perfScope.MarkAsFailed("Test failure");
        
        Assert.True(perfScope.Elapsed >= TimeSpan.Zero);
    }

    [Fact]
    public void PerformanceScope_DisposesCorrectly()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<IProductionLogger>();
        IPerformanceScope? scope = null;

        // Act
        using (var disposableScope = logger.BeginPerformanceScope("TestService", "TestOperation"))
        {
            scope = disposableScope as IPerformanceScope;
            Assert.NotNull(scope);
            Thread.Sleep(10); // Ensure some time passes
            scope.AddContext("TestKey", "TestValue");
        }

        // Assert - Should not throw when disposed
        Assert.True(scope.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public void Log4NetLogger_SupportsIsEnabled()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<ILoggerFactory>().CreateLogger("Test");

        // Act & Assert
        Assert.True(logger.IsEnabled(LogLevel.Debug)); // Should be true in debug mode
        Assert.True(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.None));
    }

    [Fact]
    public void Log4NetLogger_SupportsScopes()
    {
        // Arrange
        var logger = _serviceProvider!.GetRequiredService<ILoggerFactory>().CreateLogger("Test");

        // Act & Assert - Should not throw
        using var scope = logger.BeginScope("TestScope");
        logger.LogInformation("Message within scope");
    }

    [Fact]
    public void Configuration_HandlesInvalidConfigPath()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), "nonexistent", "config.xml");

        // Act
        var success = Log4NetConfiguration.InitializeLogging(false, invalidPath);

        // Assert
        Assert.True(success, "Should succeed with fallback configuration when config file doesn't exist");
    }

    [Fact]
    public void LoggingConfiguration_SupportsProviderSelection()
    {
        // Act & Assert - Should not throw
        var success1 = LoggingConfiguration.InitializeLogging(LoggingProvider.Log4Net, false);
        Assert.True(success1, "log4net provider should initialize successfully");

        LoggingConfiguration.ShutdownLogging();

        var success2 = LoggingConfiguration.InitializeLogging(LoggingProvider.NLog, false);
        Assert.True(success2, "NLog provider should initialize successfully");
    }

    /// <summary>
    /// Creates a test log4net configuration file
    /// </summary>
    private string CreateTestLog4NetConfig()
    {
        var configPath = Path.Combine(_tempLogDirectory, "test-log4net.config");
        var config = $@"<?xml version=""1.0"" encoding=""utf-8"" ?>
<log4net>
  <appender name=""TestFileAppender"" type=""log4net.Appender.FileAppender"">
    <file value=""{Path.Combine(_tempLogDirectory, "test.log").Replace("\\", "\\\\")}"" />
    <appendToFile value=""true"" />
    <layout type=""log4net.Layout.PatternLayout"">
      <conversionPattern value=""%date [%level] %logger - %message%newline"" />
    </layout>
  </appender>

  <appender name=""TestConsoleAppender"" type=""log4net.Appender.ConsoleAppender"">
    <layout type=""log4net.Layout.PatternLayout"">
      <conversionPattern value=""%date [%level] %logger - %message%newline"" />
    </layout>
  </appender>

  <root>
    <level value=""DEBUG"" />
    <appender-ref ref=""TestFileAppender"" />
    <appender-ref ref=""TestConsoleAppender"" />
  </root>
</log4net>";

        File.WriteAllText(configPath, config);
        return configPath;
    }
}