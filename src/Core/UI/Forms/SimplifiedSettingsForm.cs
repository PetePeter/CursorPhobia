using System.ComponentModel;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.UI.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.UI.Forms;

/// <summary>
/// Simplified settings dialog for CursorPhobia with essential controls only
/// Based on GitHub Issue #12 design requirements
/// </summary>
public partial class SimplifiedSettingsForm : Form
{
    private readonly IConfigurationService _configService;
    private readonly ICursorPhobiaEngine _engine;
    private readonly ILogger _logger;
    private readonly SettingsViewModel _viewModel;
    private readonly System.Windows.Forms.ToolTip _toolTip;
    private string? _configurationPath;
    private bool _isInitializing;
    private bool _isClosing;

    // Preset configurations
    private static readonly Dictionary<string, (int ProximityThreshold, int PushDistance)> Presets = new()
    {
        { "gentle", (ProximityThreshold: 75, PushDistance: 75) },
        { "balanced", (ProximityThreshold: 50, PushDistance: 100) },
        { "aggressive", (ProximityThreshold: 25, PushDistance: 150) }
    };

    public SimplifiedSettingsForm(
        IConfigurationService configService,
        ICursorPhobiaEngine engine,
        ILogger logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create view model with default configuration initially
        _viewModel = new SettingsViewModel(CursorPhobiaConfiguration.CreateDefault());

        // Initialize tooltip for help icons
        _toolTip = new System.Windows.Forms.ToolTip
        {
            AutoPopDelay = 8000,
            InitialDelay = 500,
            ReshowDelay = 100,
            ShowAlways = true,
            IsBalloon = true
        };

        InitializeComponent();
        SetupDataBindings();
        SetupEventHandlers();
        SetupTooltips();
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

            // Update the preset selection based on current values
            UpdatePresetSelection();

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
            // Validate the configuration
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
    /// Validates the current settings
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
                var errorMessage = "Please correct the following issues:\n\n" + string.Join("\n", errors);
                MessageBox.Show(errorMessage, "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

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

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadConfigurationAsync();
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

            _toolTip?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SetupDataBindings()
    {
        try
        {
            // Main enable/disable checkbox - using ApplyToAllWindows as proxy for "enabled"
            // In simplified UI, this represents whether CursorPhobia is active
            enableCursorPhobiaCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.ApplyToAllWindows), false, DataSourceUpdateMode.OnPropertyChanged);

            // Fine-tuning controls
            proximityThresholdNumeric.DataBindings.Add(
                nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.ProximityThreshold), false, DataSourceUpdateMode.OnPropertyChanged);

            pushDistanceNumeric.DataBindings.Add(
                nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.PushDistance), false, DataSourceUpdateMode.OnPropertyChanged);

            // Advanced controls data bindings
            // Apply to All Windows - this is the actual property binding (main checkbox uses this as proxy)
            applyToAllWindowsCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.ApplyToAllWindows), false, DataSourceUpdateMode.OnPropertyChanged);

            // Hover timeout controls
            enableHoverTimeoutCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.EnableHoverTimeout), false, DataSourceUpdateMode.OnPropertyChanged);
            
            // Hover timeout numeric shows hardcoded value (read-only)
            hoverTimeoutNumeric.DataBindings.Add(
                nameof(NumericUpDown.Value), _viewModel, nameof(_viewModel.CurrentHoverTimeoutMs), false, DataSourceUpdateMode.Never);
            hoverTimeoutNumeric.ReadOnly = true; // Make it read-only since it's hardcoded

            // Multi-monitor controls
            enableWrappingCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.EnableWrapping), false, DataSourceUpdateMode.OnPropertyChanged);

            respectTaskbarAreasCheckBox.DataBindings.Add(
                nameof(CheckBox.Checked), _viewModel, nameof(_viewModel.RespectTaskbarAreas), false, DataSourceUpdateMode.OnPropertyChanged);

            // Show advanced options checkbox is handled by OnShowAdvancedCheckBoxChanged event
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup data bindings for simplified settings form");
            MessageBox.Show(
                "Failed to initialize some settings controls. Default values will be used.\n\n" +
                $"Error: {ex.Message}",
                "Initialization Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SetupEventHandlers()
    {
        // Subscribe to view model property changes
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Preset radio button handlers
        gentleRadioButton.CheckedChanged += OnPresetRadioButtonChanged;
        balancedRadioButton.CheckedChanged += OnPresetRadioButtonChanged;
        aggressiveRadioButton.CheckedChanged += OnPresetRadioButtonChanged;

        // Manual value change handlers
        proximityThresholdNumeric.ValueChanged += OnManualValueChanged;
        pushDistanceNumeric.ValueChanged += OnManualValueChanged;
    }

    private void SetupTooltips()
    {
        // Main controls tooltips
        _toolTip.SetToolTip(enableCursorPhobiaCheckBox, 
            "Enable or disable CursorPhobia window pushing behavior");

        // Preset tooltips
        _toolTip.SetToolTip(gentleRadioButton, 
            "Gentle: Large trigger distance (75px), moderate push distance (75px)");
        _toolTip.SetToolTip(balancedRadioButton, 
            "Balanced: Medium trigger distance (50px), standard push distance (100px) - Recommended");
        _toolTip.SetToolTip(aggressiveRadioButton, 
            "Aggressive: Small trigger distance (25px), strong push distance (150px)");

        // Fine-tuning tooltips (with help icon functionality)
        _toolTip.SetToolTip(proximityThresholdNumeric, 
            "Distance in pixels from cursor to window edge that triggers pushing behavior");
        _toolTip.SetToolTip(pushDistanceNumeric, 
            "Distance in pixels that windows are moved when pushed away from cursor");

        // Advanced options tooltip
        _toolTip.SetToolTip(showAdvancedCheckBox, 
            "Show additional settings for hover timeout, multi-monitor configuration, and more");

        // Advanced controls tooltips
        _toolTip.SetToolTip(applyToAllWindowsCheckBox,
            "When checked, CursorPhobia affects all windows instead of just the topmost window");

        _toolTip.SetToolTip(enableHoverTimeoutCheckBox,
            "When enabled, CursorPhobia stops pushing windows away after hovering for a specified time");
        
        _toolTip.SetToolTip(hoverTimeoutNumeric,
            $"Hover timeout duration (hardcoded to {CursorPhobia.Core.Models.HardcodedDefaults.HoverTimeoutMs}ms for optimal performance)");

        _toolTip.SetToolTip(enableWrappingCheckBox,
            "Allow windows to wrap around screen edges when pushed in multi-monitor setups");

        _toolTip.SetToolTip(respectTaskbarAreasCheckBox,
            "Prevent windows from being pushed into taskbar areas on multi-monitor setups");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isInitializing) return;

        // Update form title when unsaved changes state changes
        if (e.PropertyName == nameof(SettingsViewModel.HasUnsavedChanges))
        {
            UpdateFormTitle();
        }
        // Update preset selection when proximity or push values change
        else if (e.PropertyName == nameof(SettingsViewModel.ProximityThreshold) || 
                 e.PropertyName == nameof(SettingsViewModel.PushDistance))
        {
            UpdatePresetSelection();
        }
    }

    private void OnPresetRadioButtonChanged(object? sender, EventArgs e)
    {
        if (_isInitializing || sender is not RadioButton radioButton || !radioButton.Checked)
            return;

        var presetName = radioButton.Name switch
        {
            nameof(gentleRadioButton) => "gentle",
            nameof(balancedRadioButton) => "balanced",
            nameof(aggressiveRadioButton) => "aggressive",
            _ => null
        };

        if (presetName != null && Presets.TryGetValue(presetName, out var preset))
        {
            _isInitializing = true;
            try
            {
                _viewModel.ProximityThreshold = preset.ProximityThreshold;
                _viewModel.PushDistance = preset.PushDistance;
            }
            finally
            {
                _isInitializing = false;
            }
        }
    }

    private void OnManualValueChanged(object? sender, EventArgs e)
    {
        if (_isInitializing) return;
        
        // Clear preset selection when values are manually changed
        UpdatePresetSelection();
    }

    private void UpdatePresetSelection()
    {
        if (_isInitializing) return;

        _isInitializing = true;
        try
        {
            var currentValues = (_viewModel.ProximityThreshold, _viewModel.PushDistance);
            
            // Check if current values match any preset
            var matchingPreset = Presets.FirstOrDefault(p => 
                p.Value.ProximityThreshold == currentValues.ProximityThreshold &&
                p.Value.PushDistance == currentValues.PushDistance);

            // Update radio button selection
            gentleRadioButton.Checked = matchingPreset.Key == "gentle";
            balancedRadioButton.Checked = matchingPreset.Key == "balanced";
            aggressiveRadioButton.Checked = matchingPreset.Key == "aggressive";

            // If no preset matches, clear all selections
            if (matchingPreset.Key == null)
            {
                gentleRadioButton.Checked = false;
                balancedRadioButton.Checked = false;
                aggressiveRadioButton.Checked = false;
            }
        }
        finally
        {
            _isInitializing = false;
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

    // Event handlers for form buttons
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

    private void OnShowAdvancedCheckBoxChanged(object sender, EventArgs e)
    {
        try
        {
            // Toggle advanced options panel visibility
            AdvancedOptionsVisible = showAdvancedCheckBox.Checked;
            
            // Update form title to reflect advanced mode status
            UpdateFormTitle();
            
            _logger.LogDebug($"Advanced options panel {(showAdvancedCheckBox.Checked ? "shown" : "hidden")}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling advanced options panel");
            MessageBox.Show(
                $"An error occurred while toggling advanced options:\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            
            // Reset checkbox state on error
            showAdvancedCheckBox.Checked = !showAdvancedCheckBox.Checked;
        }
    }

    /// <summary>
    /// Displays a help dialog with the specified title and message
    /// </summary>
    /// <param name="title">The title of the help dialog</param>
    /// <param name="message">The help message to display</param>
    private void ShowHelp(string title, string message)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}