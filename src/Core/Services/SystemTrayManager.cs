using System.Drawing;
using System.Windows.Forms;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using System.Runtime.InteropServices;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages the system tray icon and context menu for CursorPhobia
/// Provides visual feedback for engine state and user interaction through context menu
/// </summary>
public class SystemTrayManager : ISystemTrayManager
{
    private readonly ILogger _logger;
    private readonly SynchronizationContext? _synchronizationContext;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private ToolStripMenuItem? _toggleMenuItem;
    private bool _disposed = false;
    private TrayIconState _currentState = TrayIconState.Disabled;
    
    /// <summary>
    /// Event raised when the user requests to toggle enable/disable from the tray menu
    /// </summary>
    public event EventHandler? ToggleEngineRequested;
    
    /// <summary>
    /// Event raised when the user requests to open settings from the tray menu
    /// </summary>
    public event EventHandler? SettingsRequested;
    
    /// <summary>
    /// Event raised when the user requests to view about information from the tray menu
    /// </summary>
    public event EventHandler? AboutRequested;
    
    /// <summary>
    /// Event raised when the user requests to exit the application from the tray menu
    /// </summary>
    public event EventHandler? ExitRequested;
    
    /// <summary>
    /// Gets whether the tray manager is currently initialized and visible
    /// </summary>
    public bool IsInitialized => _notifyIcon != null && _notifyIcon.Visible;
    
    /// <summary>
    /// Gets the current tray icon state
    /// </summary>
    public TrayIconState CurrentState => _currentState;
    
    /// <summary>
    /// Executes an action on the UI thread safely
    /// </summary>
    /// <param name="action">Action to execute on UI thread</param>
    private void InvokeOnUIThread(Action action)
    {
        if (action == null) return;
        
        // If we have a synchronization context and we're not already on the UI thread
        if (_synchronizationContext != null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(_ => 
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing UI thread action");
                }
            }, null);
        }
        else
        {
            // We're already on the UI thread or no sync context available
            action();
        }
    }
    
    /// <summary>
    /// Creates a new SystemTrayManager instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public SystemTrayManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _synchronizationContext = SynchronizationContext.Current;
    }
    
    /// <summary>
    /// Initializes the system tray icon and context menu
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot initialize disposed SystemTrayManager");
            return false;
        }
        
        if (IsInitialized)
        {
            _logger.LogDebug("SystemTrayManager is already initialized");
            return true;
        }
        
        try
        {
            _logger.LogInformation("Initializing system tray manager...");
            
            // Create the context menu
            CreateContextMenu();
            
            // Create the notify icon
            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = _contextMenu,
                Text = "CursorPhobia - Disabled",
                Visible = true
            };
            
            // Set initial icon state
            await UpdateStateAsync(TrayIconState.Disabled);
            
            _logger.LogInformation("System tray manager initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize system tray manager");
            return false;
        }
    }
    
    /// <summary>
    /// Updates the tray icon state and tooltip
    /// </summary>
    /// <param name="state">New tray icon state</param>
    /// <param name="tooltipText">Optional custom tooltip text, uses default if null</param>
    public Task UpdateStateAsync(TrayIconState state, string? tooltipText = null)
    {
        if (_disposed || _notifyIcon == null)
        {
            _logger.LogWarning("Cannot update state of disposed or uninitialized SystemTrayManager");
            return Task.CompletedTask;
        }
        
        // Validate inputs
        if (tooltipText != null && string.IsNullOrWhiteSpace(tooltipText))
        {
            tooltipText = null;
        }
        
        // Execute UI update on the UI thread
        InvokeOnUIThread(() =>
        {
            try
            {
                _currentState = state;

                // Update icon based on state
                var oldIcon = _notifyIcon.Icon;
                var newIcon = CreateIconForState(state);
                _notifyIcon.Icon = newIcon;
                oldIcon?.Dispose();

                // Update tooltip with proper length validation
                string finalTooltip;
                if (tooltipText != null)
                {
                    finalTooltip = tooltipText.Length > 63 ? tooltipText.Substring(0, 63) : tooltipText;
                }
                else
                {
                    finalTooltip = GetDefaultTooltipForState(state);
                }
                
                _notifyIcon.Text = finalTooltip;

                _logger.LogDebug("Tray icon updated to state: {State}", state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tray icon state to {State}", state);
            }
        });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Updates the menu state (enabled/disabled text) based on engine status
    /// </summary>
    /// <param name="isEngineEnabled">Whether the engine is currently running</param>
    public Task UpdateMenuStateAsync(bool isEngineEnabled)
    {
        if (_disposed || _toggleMenuItem == null)
        {
            _logger.LogWarning("Cannot update menu state of disposed or uninitialized SystemTrayManager");
            return Task.CompletedTask;
        }
        
        // Execute UI update on the UI thread
        InvokeOnUIThread(() =>
        {
            try
            {
                _toggleMenuItem.Text = isEngineEnabled ? "&Disable CursorPhobia" : "&Enable CursorPhobia";
                _logger.LogDebug("Menu state updated: engine enabled = {IsEnabled}", isEngineEnabled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating menu state");
            }
        });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Shows a balloon notification from the tray icon
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="isError">Whether this is an error notification</param>
    public Task ShowNotificationAsync(string title, string message, bool isError = false)
    {
        if (_disposed || _notifyIcon == null)
        {
            _logger.LogWarning("Cannot show notification from disposed or uninitialized SystemTrayManager");
            return Task.CompletedTask;
        }
        
        // Validate inputs
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogWarning("Cannot show notification with null or empty title");
            return Task.CompletedTask;
        }
        
        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("Cannot show notification with null or empty message");
            return Task.CompletedTask;
        }
        
        // Truncate title and message to Windows notification limits
        var finalTitle = title.Length > 63 ? title.Substring(0, 63) : title;
        var finalMessage = message.Length > 255 ? message.Substring(0, 252) + "..." : message;
        
        // Execute UI update on the UI thread
        InvokeOnUIThread(() =>
        {
            try
            {
                var icon = isError ? ToolTipIcon.Error : ToolTipIcon.Info;
                _notifyIcon.ShowBalloonTip(3000, finalTitle, finalMessage, icon);
                _logger.LogDebug("Showed tray notification: {Title} - {Message}", finalTitle, finalMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing tray notification");
            }
        });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Hides the tray icon and cleans up resources
    /// </summary>
    public Task HideAsync()
    {
        if (_disposed)
            return Task.CompletedTask;
        
        // Execute UI update on the UI thread
        InvokeOnUIThread(() =>
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _logger.LogDebug("Tray icon hidden");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hiding tray icon");
            }
        });
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Creates the context menu with all menu items
    /// </summary>
    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();
        
        // Toggle Enable/Disable
        _toggleMenuItem = new ToolStripMenuItem("&Enable CursorPhobia");
        _toggleMenuItem.Click += (sender, e) => ToggleEngineRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Items.Add(_toggleMenuItem);
        
        // Separator
        _contextMenu.Items.Add(new ToolStripSeparator());
        
        // Settings
        var settingsMenuItem = new ToolStripMenuItem("&Settings...");
        settingsMenuItem.Click += (sender, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Items.Add(settingsMenuItem);
        
        // About
        var aboutMenuItem = new ToolStripMenuItem("&About CursorPhobia...");
        aboutMenuItem.Click += (sender, e) => AboutRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Items.Add(aboutMenuItem);
        
        // Separator
        _contextMenu.Items.Add(new ToolStripSeparator());
        
        // Exit
        var exitMenuItem = new ToolStripMenuItem("E&xit");
        exitMenuItem.Click += (sender, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        _contextMenu.Items.Add(exitMenuItem);
        
        _logger.LogDebug("Context menu created with {ItemCount} items", _contextMenu.Items.Count);
    }
    
    /// <summary>
    /// Creates an icon for the specified state
    /// </summary>
    /// <param name="state">The tray icon state</param>
    /// <returns>Icon representing the state</returns>
    private Icon CreateIconForState(TrayIconState state)
    {
        // Create a simple colored icon based on state
        var color = state switch
        {
            TrayIconState.Enabled => Color.Green,
            TrayIconState.Disabled => Color.Red,
            TrayIconState.Warning => Color.Orange,
            TrayIconState.Error => Color.Gray,
            _ => Color.Gray
        };
        
        // Create a simple 16x16 bitmap with the state color
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        
        // Fill with background color
        graphics.Clear(Color.Transparent);
        
        // Draw a filled circle with the state color
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 12, 12);
        
        // Draw a border
        using var pen = new Pen(Color.Black, 1);
        graphics.DrawEllipse(pen, 2, 2, 12, 12);
        
        // Convert to icon
        var iconHandle = bitmap.GetHicon();
        var originalIcon = Icon.FromHandle(iconHandle);
        var icon = (Icon)originalIcon.Clone();
        originalIcon.Dispose();
        DestroyIcon(iconHandle);
        return icon;
    }
    
    /// <summary>
    /// Gets the default tooltip text for a state
    /// </summary>
    /// <param name="state">The tray icon state</param>
    /// <returns>Default tooltip text</returns>
    private string GetDefaultTooltipForState(TrayIconState state)
    {
        return state switch
        {
            TrayIconState.Enabled => "CursorPhobia - Running",
            TrayIconState.Disabled => "CursorPhobia - Disabled",
            TrayIconState.Warning => "CursorPhobia - Warning",
            TrayIconState.Error => "CursorPhobia - Error",
            _ => "CursorPhobia"
        };
    }
    
    /// <summary>
    /// Disposes the tray manager and releases all resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _logger.LogInformation("Disposing SystemTrayManager");
        
        try
        {
            // Mark as disposed first to prevent further operations
            _disposed = true;
            
            // Execute disposal on the UI thread to avoid cross-thread issues
            if (_synchronizationContext != null && SynchronizationContext.Current != _synchronizationContext)
            {
                var waitHandle = new ManualResetEventSlim(false);
                _synchronizationContext.Post(_ =>
                {
                    try
                    {
                        DisposeUIResources();
                    }
                    finally
                    {
                        waitHandle.Set();
                    }
                }, null);
                
                // Wait for disposal to complete, with timeout
                if (!waitHandle.Wait(5000))
                {
                    _logger.LogWarning("Timed out waiting for UI thread disposal");
                }
            }
            else
            {
                DisposeUIResources();
            }
            
            _logger.LogDebug("SystemTrayManager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SystemTrayManager disposal");
        }
        
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Disposes UI resources (must be called on UI thread)
    /// </summary>
    private void DisposeUIResources()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        
        _contextMenu?.Dispose();
        _contextMenu = null;
        _toggleMenuItem = null;
    }
}
