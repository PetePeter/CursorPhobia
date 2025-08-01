using System.Reflection;
using System.Security;
using Microsoft.Win32;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Windows Registry-based startup manager for auto-start functionality
/// </summary>
public class StartupManager : IStartupManager
{
    private const string REGISTRY_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string APP_NAME = "CursorPhobia";
    
    private readonly ILogger _logger;

    public StartupManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string RegistryKeyPath => REGISTRY_KEY;

    /// <inheritdoc />
    public string ApplicationName => APP_NAME;

    /// <inheritdoc />
    public async Task<bool> IsAutoStartEnabledAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, false);
            if (key == null)
            {
                _logger.LogDebug("Registry key not found: {RegistryKey}", REGISTRY_KEY);
                return false;
            }

            var value = key.GetValue(APP_NAME) as string;
            var isEnabled = !string.IsNullOrEmpty(value);
            
            _logger.LogDebug("Auto-start status: {IsEnabled}, Value: {Value}", isEnabled, value);
            return isEnabled;
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception checking auto-start status");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auto-start status");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnableAutoStartAsync()
    {
        try
        {
            var command = await GetAutoStartCommandAsync();
            
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
            if (key == null)
            {
                _logger.LogError("Unable to open registry key for writing: {RegistryKey}", REGISTRY_KEY);
                return false;
            }

            key.SetValue(APP_NAME, command, RegistryValueKind.String);
            
            _logger.LogInformation("Auto-start enabled with command: {Command}", command);
            return true;
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception enabling auto-start - requires admin privileges");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access enabling auto-start - check permissions");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling auto-start");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DisableAutoStartAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
            if (key == null)
            {
                _logger.LogDebug("Registry key not found when disabling auto-start");
                return true; // Already disabled
            }

            if (key.GetValue(APP_NAME) != null)
            {
                key.DeleteValue(APP_NAME, false);
                _logger.LogInformation("Auto-start disabled");
            }
            else
            {
                _logger.LogDebug("Auto-start was already disabled");
            }
            
            return true;
        }
        catch (SecurityException ex)
        {
            _logger.LogError(ex, "Security exception disabling auto-start");
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Unauthorized access disabling auto-start");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling auto-start");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetAutoStartCommandAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            // Get the current executable path
            var exePath = Assembly.GetExecutingAssembly().Location;
            
            // If it's a .dll (published as framework-dependent), we need to use dotnet.exe
            if (Path.GetExtension(exePath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var dotnetPath = GetDotNetPath();
                if (!string.IsNullOrEmpty(dotnetPath))
                {
                    return $"\"{dotnetPath}\" \"{exePath}\" --tray";
                }
            }
            
            // For self-contained deployment or .exe, use the executable directly
            if (Path.GetExtension(exePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return $"\"{exePath}\" --tray";
            }
            
            // Fallback: try to find the console executable
            var currentDirectory = Path.GetDirectoryName(exePath);
            var consoleExe = Path.Combine(currentDirectory ?? "", "CursorPhobia.Console.exe");
            
            if (File.Exists(consoleExe))
            {
                return $"\"{consoleExe}\" --tray";
            }
            
            // Last resort: cannot create a reliable startup command
            _logger.LogWarning("Cannot create auto-start command: no suitable executable found");
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auto-start command");
            return "";
        }
    }

    /// <summary>
    /// Attempts to find the dotnet.exe path
    /// </summary>
    /// <returns>Path to dotnet.exe or null if not found</returns>
    private string? GetDotNetPath()
    {
        try
        {
            // Try common paths
            var commonPaths = new[]
            {
                @"C:\Program Files\dotnet\dotnet.exe",
                @"C:\Program Files (x86)\dotnet\dotnet.exe"
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Try PATH environment variable
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                var paths = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in paths)
                {
                    var dotnetPath = Path.Combine(path, "dotnet.exe");
                    if (File.Exists(dotnetPath))
                    {
                        return dotnetPath;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding dotnet.exe path");
            return null;
        }
    }

    /// <summary>
    /// Validates that the current user has the necessary permissions for startup management
    /// </summary>
    /// <returns>True if permissions are sufficient</returns>
    public async Task<bool> ValidatePermissionsAsync()
    {
        try
        {
            await Task.CompletedTask;
            
            // Try to open the registry key for writing
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
            return key != null;
        }
        catch (SecurityException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating permissions");
            return false;
        }
    }
}