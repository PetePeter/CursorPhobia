using System.ComponentModel;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.UI.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.UI.Forms;

/// <summary>
/// Main settings dialog for configuring CursorPhobia behavior
/// </summary>
public partial class SettingsForm : Form
{
    private readonly IConfigurationService _configService;
    private readonly ICursorPhobiaEngine _engine;
    private readonly ILogger _logger;
    private readonly IMonitorManager? _monitorManager;
    private readonly SettingsViewModel _viewModel;
    private readonly System.Windows.Forms.Timer _previewUpdateTimer;
    private readonly System.Windows.Forms.ToolTip _validationToolTip;
    private string? _configurationPath;
    private bool _isInitializing;
    private bool _isClosing;

    public SettingsForm(
        IConfigurationService configService,
        ICursorPhobiaEngine engine,
        ILogger logger,
        IMonitorManager? monitorManager = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _monitorManager = monitorManager;

        // Create view model with default configuration initially
        _viewModel = new SettingsViewModel(CursorPhobiaConfiguration.CreateDefault());

        // Initialize preview update timer
        _previewUpdateTimer = new System.Windows.Forms.Timer
        {
            Interval = 100, // 10 FPS for preview updates
            Enabled = false
        };
        _previewUpdateTimer.Tick += OnPreviewUpdateTimerTick;

        // Single ToolTip instance for validation errors
        _validationToolTip = new System.Windows.Forms.ToolTip
        {
            ToolTipTitle = "Validation Error",
            ToolTipIcon = ToolTipIcon.Error,
            IsBalloon = true
        };

        InitializeComponent();
        SetupDataBindings();
        SetupEventHandlers();
    }

    /// <summary>
    /// Gets the current configuration from the view model
    /// </summary>
    public CursorPhobiaConfiguration CurrentConfiguration => _viewModel.Configuration;

    /// <summary>
    /// Gets whether there are unsaved changes
    /// </summary>
    public bool HasUnsavedChanges => _viewModel.HasUnsavedChanges;

    /// <summary>
    /// Loads the configuration and initializes the form
    /// </summary>
    public async Task LoadConfigurationAsync()
    {
        try
        {
            _isInitializing = true;

            // Get the default configuration path
            _configurationPath = await _configService.GetDefaultConfigurationPathAsync();

            // Load the configuration
            var config = await _configService.LoadConfigurationAsync(_configurationPath);

            // Update the view model
            _viewModel.Configuration = config;
            _viewModel.HasUnsavedChanges = false;

            // Update the form title
            Text = $"CursorPhobia Settings - {Path.GetFileName(_configurationPath)}";

            _logger.LogInformation($"Configuration loaded from: {_configurationPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load configuration: {ex.Message}");
            MessageBox.Show(
                $"Failed to load configuration: {ex.Message}\n\nUsing default settings.",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Saves the current configuration
    /// </summary>
    public async Task<bool> SaveConfigurationAsync()
    {
        try
        {
            // Validate the configuration, filtering out performance errors (hardcoded to defaults)
            var errors = _viewModel.ValidateConfiguration()
                .Where(error => !error.Contains("UpdateInterval", StringComparison.OrdinalIgnoreCase) &&
                               !error.Contains("ScreenEdgeBuffer", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (errors.Any())
            {
                var errorMessage = "Configuration validation failed:\n\n" + string.Join("\n", errors);
                MessageBox.Show(errorMessage, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Save the configuration
            if (string.IsNullOrEmpty(_configurationPath))
            {
                _configurationPath = await _configService.GetDefaultConfigurationPathAsync();
            }

            await _configService.SaveConfigurationAsync(_viewModel.Configuration, _configurationPath);
            _viewModel.HasUnsavedChanges = false;

            // Update the window title
            Text = $"CursorPhobia Settings - {Path.GetFileName(_configurationPath)}";

            _logger.LogInformation($"Configuration saved to: {_configurationPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save configuration: {ex.Message}");
            MessageBox.Show(
                $"Failed to save configuration: {ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>
    /// Validates the current settings and highlights any issues
    /// </summary>
    public bool ValidateCurrentSettings()
    {
        try
        {
            var errors = _viewModel.ValidateConfiguration()
                .Where(error => !error.Contains("UpdateInterval", StringComparison.OrdinalIgnoreCase) &&
                               !error.Contains("ScreenEdgeBuffer", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (errors.Any())
            {
                // Clear previous error highlighting
                ClearValidationErrors();

                // Highlight controls with errors and show tooltip
                HighlightValidationErrors(errors);

                return false;
            }

            ClearValidationErrors();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation");
            MessageBox.Show($"An error occurred during validation:\n{ex.Message}",
                "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>
    /// Resets the configuration to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to their default values? This cannot be undone.",
            "Reset to Defaults",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _viewModel.ApplyPreset("default");
            UpdatePreview();
        }
    }

    // Export and Import functionality removed in Phase 4 per user feedback
    // These features added complexity without significant user value
    // Configuration is automatically saved/loaded through normal settings flow

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Load on UI thread to avoid cross-thread flags
        await LoadConfigurationAsync();
        UpdatePreview();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        if (_viewModel.HasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save them before closing?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            switch (result)
            {
                case DialogResult.Yes:
                    // Cancel the close operation and save asynchronously
                    e.Cancel = true;
                    _isClosing = false;
                    _ = SaveAndCloseAsync();
                    return;
                case DialogResult.Cancel:
                    e.Cancel = true;
                    _isClosing = false;
                    return;
            }
        }

        base.OnFormClosing(e);
    }

    private async Task SaveAndCloseAsync()
    {
        try
        {
            var saved = await SaveConfigurationAsync();
            if (saved)
            {
                // Close the form on the UI thread
                if (InvokeRequired)
                {
                    Invoke(() =>
                    {
                        _isClosing = true;
                        Close();
                    });
                }
                else
                {
                    _isClosing = true;
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving configuration during close: {ex.Message}");
            MessageBox.Show(
                $"Failed to save configuration: {ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unsubscribe from events to prevent memory leaks
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // Unsubscribe and dispose resources
            _previewUpdateTimer.Tick -= OnPreviewUpdateTimerTick;
            _previewUpdateTimer?.Dispose();
            _validationToolTip?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SetupDataBindings()
    {
        try
        {
            // General Tab Bindings
            enableCtrlOverrideCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.EnableCtrlOverride), false, DataSourceUpdateMode.OnPropertyChanged);

        applyToAllWindowsCheckBox.DataBindings.Add(
            nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.ApplyToAllWindows), false, DataSourceUpdateMode.OnPropertyChanged);

        startWithWindowsCheckBox.DataBindings.Add(
            nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.StartWithWindows), false, DataSourceUpdateMode.OnPropertyChanged);

        // Behavior Tab Bindings
        proximityThresholdNumeric.DataBindings.Add(
            nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.ProximityThreshold), false, DataSourceUpdateMode.OnPropertyChanged);

        pushDistanceNumeric.DataBindings.Add(
            nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.PushDistance), false, DataSourceUpdateMode.OnPropertyChanged);

        // Animation settings are now hardcoded - set values but disable controls
        enableAnimationsCheckBox.Checked = HardcodedDefaults.EnableAnimations;
        enableAnimationsCheckBox.Enabled = false;
        
        animationDurationNumeric.Value = HardcodedDefaults.AnimationDurationMs;
        animationDurationNumeric.Enabled = false;
        
        animationEasingComboBox.SelectedItem = HardcodedDefaults.AnimationEasing;
        animationEasingComboBox.Enabled = false;

        enableHoverTimeoutCheckBox.DataBindings.Add(
            nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.EnableHoverTimeout), false, DataSourceUpdateMode.OnPropertyChanged);

        hoverTimeoutNumeric.DataBindings.Add(
            nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.HoverTimeoutMs), false, DataSourceUpdateMode.OnPropertyChanged);

        // Multi-Monitor Tab Bindings
        enableWrappingCheckBox.DataBindings.Add(
            nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.EnableWrapping), false, DataSourceUpdateMode.OnPropertyChanged);

        wrapPreferenceComboBox.DataBindings.Add(
            nameof(ComboBox.SelectedItem), _viewModel, nameof(_viewModel.PreferredWrapBehavior), false, DataSourceUpdateMode.OnPropertyChanged);

        respectTaskbarAreasCheckBox.DataBindings.Add(
            nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.RespectTaskbarAreas), false, DataSourceUpdateMode.OnPropertyChanged);

        // Advanced Tab - Performance controls removed in Phase 3 to reduce cognitive load
        // Performance values now use smart defaults: 16ms update, 50ms max, 20px edge buffer

        // Preset selection handler removed in Phase 4 - presets eliminated per user feedback

            // Setup control state management
            SetupControlStates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup data bindings for settings form");
            MessageBox.Show(
                "Failed to initialize some settings controls. Default values will be used.\n\n" +
                $"Error: {ex.Message}",
                "Initialization Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SetupControlStates()
    {
        // Enable/disable animation controls based on animations checkbox
        enableAnimationsCheckBox.CheckedChanged += (s, e) =>
        {
            var enabled = enableAnimationsCheckBox.Checked;
            animationDurationLabel.Enabled = enabled;
            animationDurationNumeric.Enabled = enabled;
            animationEasingLabel.Enabled = enabled;
            animationEasingComboBox.Enabled = enabled;
        };

        // Enable/disable hover timeout numeric based on checkbox
        enableHoverTimeoutCheckBox.CheckedChanged += (s, e) =>
        {
            hoverTimeoutNumeric.Enabled = enableHoverTimeoutCheckBox.Checked;
        };

        // Enable/disable wrap preference based on wrapping checkbox
        enableWrappingCheckBox.CheckedChanged += (s, e) =>
        {
            wrapPreferenceComboBox.Enabled = enableWrappingCheckBox.Checked;
        };
    }

    private void SetupEventHandlers()
    {
        // Subscribe to view model property changes
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Setup per-monitor event handlers
        SetupPerMonitorEventHandlers();
    }

    private void SetupPerMonitorEventHandlers()
    {
        // Monitor selection change
        monitorListBox.SelectedIndexChanged += OnMonitorSelectionChanged;

        // Per-monitor setting changes
        perMonitorEnabledCheckBox.CheckedChanged += OnPerMonitorSettingChanged;

        // Load monitor list when form is first shown
        Load += (s, e) => LoadMonitorList();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing) return;

        // Update preview when settings change
        if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges))
        {
            UpdateFormTitle();
        }
        else
        {
            // Throttle preview updates
            _previewUpdateTimer.Stop();
            _previewUpdateTimer.Start();
        }
    }

    private void OnPreviewUpdateTimerTick(object? sender, EventArgs e)
    {
        _previewUpdateTimer.Stop();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            // Validate configuration first
            var isValid = ValidateCurrentSettings();

            // Update engine configuration for real-time preview
            if (isValid && _engine != null)
            {
                // Apply configuration to engine for preview
                // Note: UpdateConfiguration method may need to be added to ICursorPhobiaEngine interface
                try
                {
                    var configMethod = _engine.GetType().GetMethod("UpdateConfiguration");
                    configMethod?.Invoke(_engine, new object[] { _viewModel.Configuration });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Engine configuration update not available: {ex.Message}");
                }
            }

            // Update any custom preview controls (e.g., animation preview panels)
            UpdatePreviewControls();

            // Force redraw for custom preview controls
            Invalidate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Preview update failed: {ex.Message}");
        }
    }

    private void UpdatePreviewControls()
    {
        // Update preview controls based on current settings
        // This can be expanded when specific preview controls are added

        // Update status labels or preview text
        if (Controls.Find("previewStatusLabel", true).FirstOrDefault() is Label statusLabel)
        {
            statusLabel.Text = _viewModel.HasUnsavedChanges ? "Preview (Unsaved)" : "Preview (Current)";
        }

        // Update any animation preview panels
        var animationPreviews = Controls.Find("animationPreviewPanel", true);
        foreach (Control preview in animationPreviews)
        {
            preview.Enabled = HardcodedDefaults.EnableAnimations;
            preview.Invalidate();
        }
    }

    private void UpdateFormTitle()
    {
        var fileName = !string.IsNullOrEmpty(_configurationPath)
            ? Path.GetFileName(_configurationPath)
            : "New Configuration";

        var unsavedIndicator = _viewModel.HasUnsavedChanges ? "*" : "";
        Text = $"CursorPhobia Settings - {fileName}{unsavedIndicator}";
    }

    private void ClearValidationErrors()
    {
        // Clear error highlighting on all input controls
        ClearControlErrors(this);

        // Clear any error tooltips - tooltips are not stored as controls
        // Individual tooltip cleanup will be handled per control
    }

    private void ClearControlErrors(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            // Reset background color for input controls
            switch (control)
            {
                case NumericUpDown numeric:
                    numeric.BackColor = SystemColors.Window;
                    break;
                case ComboBox combo:
                    combo.BackColor = SystemColors.Window;
                    break;
                case CheckBox checkbox:
                    checkbox.BackColor = Color.Transparent;
                    break;
            }

            // Recursively clear errors in child containers
            if (control.HasChildren)
            {
                ClearControlErrors(control);
            }
        }
    }

    private void HighlightValidationErrors(List<string> errors)
    {
        // Map validation errors to specific controls and highlight them
        foreach (var error in errors)
        {
            HighlightErrorForMessage(error);
        }

        // Show error summary
        var errorMessage = "Please correct the following issues:\n\n" + string.Join("\n", errors);
        MessageBox.Show(errorMessage, "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void HighlightErrorForMessage(string errorMessage)
    {
        var errorColor = Color.FromArgb(255, 192, 192); // Light red background

        // Map error messages to controls based on common validation patterns
        if (errorMessage.Contains("ProximityThreshold", StringComparison.OrdinalIgnoreCase))
        {
            HighlightControl("proximityThresholdNumeric", errorColor, errorMessage);
        }
        else if (errorMessage.Contains("PushDistance", StringComparison.OrdinalIgnoreCase))
        {
            HighlightControl("pushDistanceNumeric", errorColor, errorMessage);
        }
        else if (errorMessage.Contains("AnimationDuration", StringComparison.OrdinalIgnoreCase))
        {
            HighlightControl("animationDurationNumeric", errorColor, errorMessage);
        }
        else if (errorMessage.Contains("HoverTimeout", StringComparison.OrdinalIgnoreCase))
        {
            HighlightControl("hoverTimeoutNumeric", errorColor, errorMessage);
        }
    }

    private void HighlightControl(string controlName, Color errorColor, string errorMessage)
    {
        var controls = Controls.Find(controlName, true);
        foreach (Control control in controls)
        {
            control.BackColor = errorColor;

            // Add tooltip with error message
            _validationToolTip.SetToolTip(control, errorMessage);
        }
    }

    // Event handlers for UI controls will be implemented in Designer.cs
    private async void OnOkButtonClick(object sender, EventArgs e)
    {
        try
        {
            if (ValidateCurrentSettings())
            {
                if (await SaveConfigurationAsync())
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OK button click handler");
            MessageBox.Show($"An error occurred while saving settings:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void OnApplyButtonClick(object sender, EventArgs e)
    {
        try
        {
            if (ValidateCurrentSettings())
            {
                await SaveConfigurationAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Apply button click handler");
            MessageBox.Show($"An error occurred while applying settings:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnCancelButtonClick(object sender, EventArgs e)
    {
        try
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Cancel button click handler");
            MessageBox.Show($"An error occurred:\n{ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnResetButtonClick(object sender, EventArgs e)
    {
        ResetToDefaults();
    }

    // Export and Import button handlers removed in Phase 4
    // Functionality eliminated per user feedback to reduce cognitive load

    #region Per-Monitor Settings Methods

    /// <summary>
    /// Loads monitor information and populates the monitor list
    /// </summary>
    private void LoadMonitorList()
    {
        if (_monitorManager == null)
        {
            monitorListBox.Items.Clear();
            monitorListBox.Items.Add("Monitor Manager not available");
            monitorListBox.Enabled = false;
            perMonitorSettingsPanel.Enabled = false;
            return;
        }

        try
        {
            var monitors = _monitorManager.GetAllMonitors();
            monitorListBox.Items.Clear();

            foreach (var monitor in monitors)
            {
                var displayName = $"{monitor.deviceName} ({monitor.width}x{monitor.height})";
                if (monitor.isPrimary)
                    displayName += " [Primary]";

                monitorListBox.Items.Add(new MonitorListItem(monitor, displayName));
            }

            if (monitors.Count > 0)
            {
                monitorListBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load monitor list: {ex.Message}");
            monitorListBox.Items.Clear();
            monitorListBox.Items.Add("Error loading monitors");
            monitorListBox.Enabled = false;
        }
    }

    /// <summary>
    /// Handles monitor selection changes in the list box
    /// </summary>
    private void OnMonitorSelectionChanged(object? sender, EventArgs e)
    {
        if (monitorListBox.SelectedItem is not MonitorListItem selectedItem)
        {
            selectedMonitorLabel.Text = "No monitor selected";
            perMonitorSettingsPanel.Enabled = false;
            return;
        }

        var monitor = selectedItem.Monitor;
        selectedMonitorLabel.Text = selectedItem.DisplayName;
        perMonitorSettingsPanel.Enabled = true;

        // Load per-monitor settings for this monitor
        LoadPerMonitorSettings(monitor);
    }

    /// <summary>
    /// Loads per-monitor settings for the specified monitor
    /// </summary>
    private void LoadPerMonitorSettings(MonitorInfo monitor)
    {
        _isInitializing = true;

        try
        {
            var monitorKey = monitor.GetStableKey();
            var config = _viewModel.Configuration;

            // Check if per-monitor settings exist
            if (config.MultiMonitor?.PerMonitorSettings?.TryGetValue(monitorKey, out var perMonitorSettings) == true)
            {
                perMonitorEnabledCheckBox.Checked = perMonitorSettings.Enabled;
            }
            else
            {
                // Default to enabled
                perMonitorEnabledCheckBox.Checked = true;
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    /// <summary>
    /// Updates the enabled state of per-monitor controls based on current settings
    /// </summary>
    private void UpdatePerMonitorControlStates()
    {
        // No additional controls to manage with simplified UI
        // The enabled state is handled directly by the checkbox
    }

    /// <summary>
    /// Handles changes to per-monitor settings
    /// </summary>
    private void OnPerMonitorSettingChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || monitorListBox.SelectedItem is not MonitorListItem selectedItem)
            return;

        var monitor = selectedItem.Monitor;
        var monitorKey = monitor.GetStableKey();
        var config = _viewModel.Configuration;

        // Ensure MultiMonitor configuration exists
        config.MultiMonitor ??= new MultiMonitorConfiguration();
        config.MultiMonitor.PerMonitorSettings ??= new Dictionary<string, PerMonitorSettings>();

        // Get or create per-monitor settings
        if (!config.MultiMonitor.PerMonitorSettings.TryGetValue(monitorKey, out var perMonitorSettings))
        {
            perMonitorSettings = new PerMonitorSettings();
            config.MultiMonitor.PerMonitorSettings[monitorKey] = perMonitorSettings;
        }

        // Update only the enabled setting - always use global settings for threshold and distance
        perMonitorSettings.Enabled = perMonitorEnabledCheckBox.Checked;
        perMonitorSettings.CustomProximityThreshold = null; // Always use global
        perMonitorSettings.CustomPushDistance = null; // Always use global

        _viewModel.HasUnsavedChanges = true;
    }

    /// <summary>
    /// Helper class for monitor list items
    /// </summary>
    private class MonitorListItem
    {
        public MonitorInfo Monitor { get; }
        public string DisplayName { get; }

        public MonitorListItem(MonitorInfo monitor, string displayName)
        {
            Monitor = monitor;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    #endregion

}
