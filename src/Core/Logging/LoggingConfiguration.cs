using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.Logging;

/// <summary>
/// Configuration management for production logging with NLog integration
/// Handles initialization, configuration, and service registration for the logging infrastructure
/// </summary>
public static class LoggingConfiguration
{
    private static bool _initialized = false;
    private static readonly object _initializationLock = new();

    /// <summary>
    /// Initializes the production logging system with NLog
    /// Creates log directories, configures NLog, and sets up error handling
    /// </summary>
    /// <param name="isDebugMode">Whether to enable debug-level logging</param>
    /// <param name="configFilePath">Optional path to NLog config file (defaults to NLog.config in app directory)</param>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public static bool InitializeLogging(bool isDebugMode = false, string? configFilePath = null)
    {
        lock (_initializationLock)
        {
            if (_initialized)
                return true;

            try
            {
                // Ensure log directories exist
                EnsureLogDirectoriesExist();

                // Configure NLog
                ConfigureNLog(configFilePath, isDebugMode);

                // Set NLog as the default logger factory
                var nlogLoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.ClearProviders();
                    builder.AddNLog();
                    builder.SetMinimumLevel(isDebugMode ? Microsoft.Extensions.Logging.LogLevel.Debug : Microsoft.Extensions.Logging.LogLevel.Information);
                });

                // Initialize the legacy logger factory for backward compatibility
                CursorPhobia.Core.Utilities.LoggerFactory.Initialize(nlogLoggerFactory);

                _initialized = true;
                
                // Log successful initialization
                var logger = LogManager.GetCurrentClassLogger();
                logger.Info("Production logging system initialized successfully");
                logger.Info("Log directory: {LogDirectory}", GetLogDirectory());
                logger.Info("Debug mode: {DebugMode}", isDebugMode);
                logger.Info("Config file: {ConfigFile}", configFilePath ?? "NLog.config (default)");

                return true;
            }
            catch (Exception ex)
            {
                // Use fallback console logging for initialization errors
                Console.WriteLine($"Failed to initialize production logging: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }

    /// <summary>
    /// Configures dependency injection services for production logging
    /// Registers both legacy and new logging interfaces
    /// </summary>
    /// <param name="services">Service collection to configure</param>
    /// <param name="isDebugMode">Whether debug mode is enabled</param>
    public static void ConfigureLoggingServices(IServiceCollection services, bool isDebugMode = false)
    {
        // Ensure logging is initialized
        if (!_initialized)
        {
            InitializeLogging(isDebugMode);
        }

        // Register Microsoft Extensions Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddNLog();
            builder.SetMinimumLevel(isDebugMode ? Microsoft.Extensions.Logging.LogLevel.Debug : Microsoft.Extensions.Logging.LogLevel.Information);
        });

        // Register legacy Logger for backward compatibility
        services.AddSingleton<CursorPhobia.Core.Utilities.Logger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return CursorPhobia.Core.Utilities.LoggerFactory.CreateLogger("CursorPhobia");
        });

        // Register legacy ILogger interface
        services.AddSingleton<CursorPhobia.Core.Utilities.ILogger>(provider =>
        {
            return provider.GetRequiredService<CursorPhobia.Core.Utilities.Logger>();
        });

        // Register production logger factory
        services.AddSingleton<IProductionLoggerFactory, ProductionLoggerFactory>();

        // Register default production logger
        services.AddSingleton<IProductionLogger>(provider =>
        {
            var factory = provider.GetRequiredService<IProductionLoggerFactory>();
            return factory.CreateLogger("CursorPhobia");
        });
    }

    /// <summary>
    /// Gets the configured log directory path
    /// </summary>
    /// <returns>Full path to the log directory</returns>
    public static string GetLogDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "CursorPhobia", "Logs");
    }

    /// <summary>
    /// Ensures all required log directories exist with proper permissions
    /// </summary>
    private static void EnsureLogDirectoriesExist()
    {
        var logDirectory = GetLogDirectory();
        var archiveDirectory = Path.Combine(logDirectory, "archives");

        try
        {
            // Create main log directory
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Create archive directory
            if (!Directory.Exists(archiveDirectory))
            {
                Directory.CreateDirectory(archiveDirectory);
            }

            // Verify write permissions by creating and deleting a test file
            var testFile = Path.Combine(logDirectory, "write-test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create or access log directories: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Configures NLog with the specified configuration
    /// </summary>
    /// <param name="configFilePath">Path to NLog config file</param>
    /// <param name="isDebugMode">Whether debug mode is enabled</param>
    private static void ConfigureNLog(string? configFilePath, bool isDebugMode)
    {
        try
        {
            // Determine config file path
            var configPath = configFilePath;
            if (string.IsNullOrEmpty(configPath))
            {
                // Look for NLog.config in the application directory
                var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
                var rootDirectory = FindProjectRoot(appDirectory);
                configPath = Path.Combine(rootDirectory, "NLog.config");
            }

            // Load NLog configuration
            if (File.Exists(configPath))
            {
                LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(configPath);
            }
            else
            {
                // Fallback to programmatic configuration if config file not found
                CreateFallbackConfiguration(isDebugMode);
                Console.WriteLine($"Warning: NLog config file not found at {configPath}, using fallback configuration");
            }

            // Configure global exception handling for NLog
            LogManager.ThrowExceptions = isDebugMode;
            LogManager.ThrowConfigExceptions = isDebugMode;
        }
        catch (Exception ex)
        {
            // Create minimal fallback configuration
            CreateFallbackConfiguration(isDebugMode);
            Console.WriteLine($"Warning: Failed to load NLog configuration, using fallback: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a fallback NLog configuration if the config file is not available
    /// </summary>
    /// <param name="isDebugMode">Whether debug mode is enabled</param>
    private static void CreateFallbackConfiguration(bool isDebugMode)
    {
        var config = new NLog.Config.LoggingConfiguration();

        // Console target for immediate feedback
        var consoleTarget = new NLog.Targets.ConsoleTarget("console")
        {
            Layout = "${time} [${uppercase:${level:padding=-5}}] ${logger:shortName=true}: ${message} ${exception:format=tostring}"
        };

        // File target for basic logging
        var logDirectory = GetLogDirectory();
        var fileTarget = new NLog.Targets.FileTarget("file")
        {
            FileName = Path.Combine(logDirectory, "cursorphobia-fallback-${shortdate}.log"),
            Layout = "${longdate} ${uppercase:${level}} ${logger} ${message} ${exception:format=tostring}",
            ArchiveAboveSize = 10 * 1024 * 1024, // 10MB
            MaxArchiveFiles = 5,
            ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling
        };

        config.AddTarget(consoleTarget);
        config.AddTarget(fileTarget);

        // Logging rules
        var consoleLevel = isDebugMode ? NLog.LogLevel.Debug : NLog.LogLevel.Info;
        var fileLevel = isDebugMode ? NLog.LogLevel.Debug : NLog.LogLevel.Info;

        config.AddRule(consoleLevel, NLog.LogLevel.Fatal, consoleTarget, "*");
        config.AddRule(fileLevel, NLog.LogLevel.Fatal, fileTarget, "*");

        LogManager.Configuration = config;
    }

    /// <summary>
    /// Finds the project root directory by looking for solution or project files
    /// </summary>
    /// <param name="startDirectory">Directory to start searching from</param>
    /// <returns>Project root directory path</returns>
    private static string FindProjectRoot(string startDirectory)
    {
        var currentDir = new DirectoryInfo(startDirectory);
        
        while (currentDir != null)
        {
            // Look for solution file or NLog.config in current directory
            if (currentDir.GetFiles("*.sln").Any() || 
                currentDir.GetFiles("NLog.config").Any() ||
                currentDir.GetFiles("CursorPhobia.sln").Any())
            {
                return currentDir.FullName;
            }
            
            currentDir = currentDir.Parent;
        }
        
        // Fallback to start directory if no project root found
        return startDirectory;
    }

    /// <summary>
    /// Shuts down the logging system gracefully
    /// </summary>
    public static void ShutdownLogging()
    {
        lock (_initializationLock)
        {
            if (_initialized)
            {
                try
                {
                    var logger = LogManager.GetCurrentClassLogger();
                    logger.Info("Shutting down production logging system");
                    
                    LogManager.Shutdown();
                    _initialized = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during logging shutdown: {ex.Message}");
                }
            }
        }
    }
}

/// <summary>
/// Factory for creating IProductionLogger instances
/// </summary>
public interface IProductionLoggerFactory
{
    /// <summary>
    /// Creates a production logger for the specified category
    /// </summary>
    /// <param name="categoryName">Logger category name</param>
    /// <returns>A new IProductionLogger instance</returns>
    IProductionLogger CreateLogger(string categoryName);

    /// <summary>
    /// Creates a production logger for the specified type
    /// </summary>
    /// <typeparam name="T">Type to use for the logger category</typeparam>
    /// <returns>A new IProductionLogger instance</returns>
    IProductionLogger CreateLogger<T>();
}

/// <summary>
/// Default implementation of IProductionLoggerFactory
/// </summary>
public class ProductionLoggerFactory : IProductionLoggerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ProductionLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public IProductionLogger CreateLogger(string categoryName)
    {
        var nlogLogger = LogManager.GetLogger(categoryName);
        var extensionsLogger = _loggerFactory.CreateLogger(categoryName);
        return new ProductionLogger(nlogLogger, extensionsLogger);
    }

    public IProductionLogger CreateLogger<T>()
    {
        return CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }
}