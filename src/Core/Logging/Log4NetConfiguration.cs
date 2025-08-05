using System.Reflection;
using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.Logging;

/// <summary>
/// Configuration management for log4net integration
/// Handles initialization, configuration, and service registration for the log4net-based logging infrastructure
/// </summary>
public static class Log4NetConfiguration
{
    private static bool _initialized = false;
    private static readonly object _initializationLock = new();

    /// <summary>
    /// Initializes the log4net logging system
    /// Creates log directories, configures log4net, and sets up error handling
    /// </summary>
    /// <param name="isDebugMode">Whether to enable debug-level logging</param>
    /// <param name="configFilePath">Optional path to log4net config file (defaults to log4net.config in app directory)</param>
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

                // Configure log4net
                ConfigureLog4Net(configFilePath, isDebugMode);

                // Set up legacy logger factory for compatibility
                var log4netLoggerFactory = new Log4NetLoggerFactory();
                CursorPhobia.Core.Utilities.LoggerFactory.Initialize(log4netLoggerFactory);

                _initialized = true;
                
                // Log successful initialization
                var logger = LogManager.GetLogger(typeof(Log4NetConfiguration));
                logger.Info("Log4net logging system initialized successfully");
                logger.Info($"Log directory: {GetLogDirectory()}");
                logger.Info($"Debug mode: {isDebugMode}");
                logger.Info($"Config file: {configFilePath ?? "log4net.config (default)"}");

                return true;
            }
            catch (Exception ex)
            {
                // Use fallback console logging for initialization errors
                Console.WriteLine($"Failed to initialize log4net logging: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }

    /// <summary>
    /// Configures dependency injection services for log4net logging
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

        // Register Microsoft Extensions Logging factory using log4net
        services.AddSingleton<ILoggerFactory, Log4NetLoggerFactory>();

        // Register legacy Logger for backward compatibility with production logging
        services.AddSingleton<CursorPhobia.Core.Utilities.Logger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var productionLoggerFactory = provider.GetRequiredService<IProductionLoggerFactory>();
            var innerLogger = loggerFactory.CreateLogger("CursorPhobia");
            var productionLogger = productionLoggerFactory.CreateLogger("CursorPhobia");
            return new CursorPhobia.Core.Utilities.Logger(innerLogger, "CursorPhobia", productionLogger);
        });

        // Register legacy ILogger interface
        services.AddSingleton<CursorPhobia.Core.Utilities.ILogger>(provider =>
        {
            return provider.GetRequiredService<CursorPhobia.Core.Utilities.Logger>();
        });

        // Register production logger factory
        services.AddSingleton<IProductionLoggerFactory, Log4NetProductionLoggerFactory>();

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
    /// Configures log4net with the specified configuration
    /// </summary>
    /// <param name="configFilePath">Path to log4net config file</param>
    /// <param name="isDebugMode">Whether debug mode is enabled</param>
    private static void ConfigureLog4Net(string? configFilePath, bool isDebugMode)
    {
        try
        {
            // Determine config file path
            var configPath = configFilePath;
            if (string.IsNullOrEmpty(configPath))
            {
                // Look for log4net.config in the application directory
                var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
                var rootDirectory = FindProjectRoot(appDirectory);
                configPath = Path.Combine(rootDirectory, "log4net.config");
            }

            // Load log4net configuration
            if (File.Exists(configPath))
            {
                var configFileInfo = new FileInfo(configPath);
                XmlConfigurator.ConfigureAndWatch(configFileInfo);
            }
            else
            {
                // Fallback to programmatic configuration if config file not found
                CreateFallbackConfiguration(isDebugMode);
                Console.WriteLine($"Warning: log4net config file not found at {configPath}, using fallback configuration");
            }

            // Configure internal log4net logging
            log4net.Util.LogLog.InternalDebugging = isDebugMode;
        }
        catch (Exception ex)
        {
            // Create minimal fallback configuration
            CreateFallbackConfiguration(isDebugMode);
            Console.WriteLine($"Warning: Failed to load log4net configuration, using fallback: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a fallback log4net configuration if the config file is not available
    /// </summary>
    /// <param name="isDebugMode">Whether debug mode is enabled</param>
    private static void CreateFallbackConfiguration(bool isDebugMode)
    {
        // Use basic configuration for fallback
        BasicConfigurator.Configure();
        
        var logger = LogManager.GetLogger(typeof(Log4NetConfiguration));
        logger.Warn("Using fallback log4net configuration - limited functionality");
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
            // Look for solution file or log4net.config in current directory
            if (currentDir.GetFiles("*.sln").Any() || 
                currentDir.GetFiles("log4net.config").Any() ||
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
    /// Shuts down the log4net logging system gracefully
    /// </summary>
    public static void ShutdownLogging()
    {
        lock (_initializationLock)
        {
            if (_initialized)
            {
                try
                {
                    var logger = LogManager.GetLogger(typeof(Log4NetConfiguration));
                    logger.Info("Shutting down log4net logging system");
                    
                    LogManager.Shutdown();
                    _initialized = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during log4net shutdown: {ex.Message}");
                }
            }
        }
    }
}

/// <summary>
/// Log4net implementation of ILoggerFactory for Microsoft.Extensions.Logging compatibility
/// </summary>
public class Log4NetLoggerFactory : ILoggerFactory
{
    private bool _disposed = false;

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        var log4netLogger = LogManager.GetLogger(categoryName);
        return new Log4NetLogger(log4netLogger, categoryName);
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // Not applicable for log4net - providers are managed through log4net configuration
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Factory for creating IProductionLogger instances using log4net
/// </summary>
public interface ILog4NetProductionLoggerFactory
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
/// Log4net implementation of IProductionLoggerFactory
/// </summary>
public class Log4NetProductionLoggerFactory : IProductionLoggerFactory, ILog4NetProductionLoggerFactory
{
    public IProductionLogger CreateLogger(string categoryName)
    {
        var log4netLogger = LogManager.GetLogger(categoryName);
        return new Log4NetLogger(log4netLogger, categoryName);
    }

    public IProductionLogger CreateLogger<T>()
    {
        return CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }
}