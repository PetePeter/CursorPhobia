using System.Linq;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.UI.Forms
{
    partial class SettingsForm
    {
        private TabControl tabControl;
        private TabPage generalTabPage;
        private TabPage behaviorTabPage;
        private TabPage multiMonitorTabPage;
        private TabPage advancedTabPage;
        
        // General Tab Controls
        private CheckBox enableCtrlOverrideCheckBox;
        private CheckBox applyToAllWindowsCheckBox;
        private CheckBox startWithWindowsCheckBox;
        
        // Behavior Tab Controls
        private Label proximityThresholdLabel;
        private NumericUpDown proximityThresholdNumeric;
        private Label pushDistanceLabel;
        private NumericUpDown pushDistanceNumeric;
        private CheckBox enableAnimationsCheckBox;
        private Label animationDurationLabel;
        private NumericUpDown animationDurationNumeric;
        private Label animationEasingLabel;
        private ComboBox animationEasingComboBox;
        private CheckBox enableHoverTimeoutCheckBox;
        private NumericUpDown hoverTimeoutNumeric;
        private Label hoverTimeoutLabel;
        private Panel previewPanel;
        
        // Multi-Monitor Tab Controls
        private CheckBox enableWrappingCheckBox;
        private ComboBox wrapPreferenceComboBox;
        private Label wrapPreferenceLabel;
        private CheckBox respectTaskbarAreasCheckBox;
        
        // Per-Monitor Settings Controls
        private ListBox monitorListBox;
        private Label monitorListLabel;
        private Panel perMonitorSettingsPanel;
        private Label selectedMonitorLabel;
        private CheckBox perMonitorEnabledCheckBox;
        
        // Advanced Tab Controls
        private NumericUpDown updateIntervalNumeric;
        private Label updateIntervalLabel;
        private NumericUpDown maxUpdateIntervalNumeric;
        private Label maxUpdateIntervalLabel;
        private NumericUpDown screenEdgeBufferNumeric;
        private Label screenEdgeBufferLabel;
        private Button exportButton;
        private Button importButton;
        private Button resetButton;
        private ComboBox presetComboBox;
        private Label presetLabel;
        
        // Form Buttons
        private Button okButton;
        private Button cancelButton;
        private Button applyButton;


        private void InitializeComponent()
        {
            SuspendLayout();
            
            // Form properties
            Text = "CursorPhobia Settings";
            Size = new Size(600, 500);
            MinimumSize = new Size(550, 450);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            
            // Tab Control
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(8),
                Padding = new Point(8, 6)
            };
            
            // Initialize tab pages
            InitializeGeneralTab();
            InitializeBehaviorTab();
            InitializeMultiMonitorTab();
            InitializeAdvancedTab();
            
            // Add tabs to control
            tabControl.TabPages.AddRange(new TabPage[] {
                generalTabPage,
                behaviorTabPage,
                multiMonitorTabPage,
                advancedTabPage
            });
            
            // Initialize form buttons
            InitializeFormButtons();
            
            // Add controls to form
            Controls.Add(tabControl);
            
            ResumeLayout(false);
        }

        private void InitializeGeneralTab()
        {
            generalTabPage = new TabPage("General")
            {
                Padding = new Padding(12),
                UseVisualStyleBackColor = true
            };

            // Enable CTRL Override
            enableCtrlOverrideCheckBox = new CheckBox
            {
                Text = "Enable CTRL key override (temporarily disable when CTRL is held)",
                Location = new Point(12, 12),
                Size = new Size(400, 20),
                UseVisualStyleBackColor = true
            };

            // Apply to All Windows
            applyToAllWindowsCheckBox = new CheckBox
            {
                Text = "Apply to all windows (not just topmost)",
                Location = new Point(12, 42),
                Size = new Size(400, 20),
                UseVisualStyleBackColor = true
            };

            // Start with Windows
            startWithWindowsCheckBox = new CheckBox
            {
                Text = "Start with Windows (auto-start on system boot)",
                Location = new Point(12, 72),
                Size = new Size(400, 20),
                UseVisualStyleBackColor = true
            };

            // Add help text
            var generalHelpLabel = new Label
            {
                Text = "General settings control the basic behavior of CursorPhobia.\n\n" +
                       "• CTRL Override: Hold CTRL to temporarily disable window pushing\n" +
                       "• All Windows: Apply to all windows, not just the active window\n" +
                       "• Start with Windows: Automatically start CursorPhobia when Windows starts",
                Location = new Point(12, 110),
                Size = new Size(550, 80),
                ForeColor = SystemColors.GrayText
            };

            generalTabPage.Controls.AddRange(new Control[] {
                enableCtrlOverrideCheckBox,
                applyToAllWindowsCheckBox,
                startWithWindowsCheckBox,
                generalHelpLabel
            });
        }

        private void InitializeBehaviorTab()
        {
            behaviorTabPage = new TabPage("Behavior")
            {
                Padding = new Padding(12),
                UseVisualStyleBackColor = true
            };

            int yPos = 12;

            // Proximity Threshold
            proximityThresholdLabel = new Label
            {
                Text = "Proximity Threshold (pixels):",
                Location = new Point(12, yPos),
                Size = new Size(150, 20)
            };

            proximityThresholdNumeric = new NumericUpDown
            {
                Location = new Point(170, yPos),
                Size = new Size(100, 20),
                Minimum = 1,
                Maximum = 500,
                DecimalPlaces = 0
            };

            yPos += 30;

            // Push Distance
            pushDistanceLabel = new Label
            {
                Text = "Push Distance (pixels):",
                Location = new Point(12, yPos),
                Size = new Size(150, 20)
            };

            pushDistanceNumeric = new NumericUpDown
            {
                Location = new Point(170, yPos),
                Size = new Size(100, 20),
                Minimum = 1,
                Maximum = 1000,
                DecimalPlaces = 0
            };

            yPos += 30;

            // Enable Animations
            enableAnimationsCheckBox = new CheckBox
            {
                Text = "Enable smooth animations",
                Location = new Point(12, yPos),
                Size = new Size(200, 20),
                UseVisualStyleBackColor = true
            };

            yPos += 30;

            // Animation Duration
            animationDurationLabel = new Label
            {
                Text = "Animation Duration (ms):",
                Location = new Point(12, yPos),
                Size = new Size(150, 20)
            };

            animationDurationNumeric = new NumericUpDown
            {
                Location = new Point(170, yPos),
                Size = new Size(100, 20),
                Minimum = 0,
                Maximum = 2000,
                DecimalPlaces = 0
            };

            yPos += 30;

            // Animation Easing
            animationEasingLabel = new Label
            {
                Text = "Animation Easing:",
                Location = new Point(12, yPos),
                Size = new Size(150, 20)
            };

            animationEasingComboBox = new ComboBox
            {
                Location = new Point(170, yPos),
                Size = new Size(120, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            animationEasingComboBox.Items.AddRange(Enum.GetValues(typeof(AnimationEasing)).Cast<object>().ToArray());

            yPos += 30;

            // Hover Timeout
            enableHoverTimeoutCheckBox = new CheckBox
            {
                Text = "Stop pushing after hovering:",
                Location = new Point(12, yPos),
                Size = new Size(180, 20),
                UseVisualStyleBackColor = true
            };

            hoverTimeoutNumeric = new NumericUpDown
            {
                Location = new Point(200, yPos),
                Size = new Size(70, 20),
                Minimum = 100,
                Maximum = 30000,
                DecimalPlaces = 0,
                Increment = 500
            };

            hoverTimeoutLabel = new Label
            {
                Text = "ms",
                Location = new Point(275, yPos),
                Size = new Size(30, 20)
            };

            yPos += 35;

            // Preview Panel
            previewPanel = new Panel
            {
                Location = new Point(12, yPos),
                Size = new Size(550, 80),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            var previewLabel = new Label
            {
                Text = "Preview Area (shows proximity and push distances)",
                Location = new Point(4, 4),
                Size = new Size(300, 16),
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8)
            };
            previewPanel.Controls.Add(previewLabel);

            behaviorTabPage.Controls.AddRange(new Control[] {
                proximityThresholdLabel, proximityThresholdNumeric,
                pushDistanceLabel, pushDistanceNumeric,
                enableAnimationsCheckBox,
                animationDurationLabel, animationDurationNumeric,
                animationEasingLabel, animationEasingComboBox,
                enableHoverTimeoutCheckBox, hoverTimeoutNumeric, hoverTimeoutLabel,
                previewPanel
            });
        }

        private void InitializeMultiMonitorTab()
        {
            multiMonitorTabPage = new TabPage("Multi-Monitor")
            {
                Padding = new Padding(12),
                UseVisualStyleBackColor = true
            };

            int yPos = 12;

            // Enable Wrapping
            enableWrappingCheckBox = new CheckBox
            {
                Text = "Enable edge wrapping to adjacent monitors",
                Location = new Point(12, yPos),
                Size = new Size(300, 20),
                UseVisualStyleBackColor = true
            };

            yPos += 35;

            // Wrap Preference
            wrapPreferenceLabel = new Label
            {
                Text = "Wrap Behavior:",
                Location = new Point(12, yPos),
                Size = new Size(100, 20)
            };

            wrapPreferenceComboBox = new ComboBox
            {
                Location = new Point(120, yPos),
                Size = new Size(150, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            wrapPreferenceComboBox.Items.AddRange(Enum.GetValues(typeof(WrapPreference)).Cast<object>().ToArray());

            yPos += 35;

            // Respect Taskbar Areas
            respectTaskbarAreasCheckBox = new CheckBox
            {
                Text = "Respect taskbar and dock areas",
                Location = new Point(12, yPos),
                Size = new Size(300, 20),
                UseVisualStyleBackColor = true
            };

            yPos += 50;

            // Per-Monitor Settings Section
            var perMonitorLabel = new Label
            {
                Text = "Per-Monitor Settings:",
                Location = new Point(12, yPos),
                Size = new Size(200, 20),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };

            yPos += 30;

            // Monitor List
            monitorListLabel = new Label
            {
                Text = "Select Monitor:",
                Location = new Point(12, yPos),
                Size = new Size(100, 20)
            };

            monitorListBox = new ListBox
            {
                Location = new Point(12, yPos + 25),
                Size = new Size(250, 120),
                SelectionMode = SelectionMode.One
            };

            // Per-Monitor Settings Panel
            perMonitorSettingsPanel = new Panel
            {
                Location = new Point(280, yPos),
                Size = new Size(300, 120),
                BorderStyle = BorderStyle.FixedSingle
            };

            selectedMonitorLabel = new Label
            {
                Text = "No monitor selected",
                Location = new Point(8, 8),
                Size = new Size(280, 20),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
            };

            perMonitorEnabledCheckBox = new CheckBox
            {
                Text = "Enable CursorPhobia for this monitor",
                Location = new Point(8, 35),
                Size = new Size(250, 20),
                UseVisualStyleBackColor = true
            };

            var globalSettingsNote = new Label
            {
                Text = "Global settings from Behavior tab will be used for all enabled monitors.",
                Location = new Point(8, 65),
                Size = new Size(280, 30),
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8)
            };

            // Add controls to per-monitor settings panel
            perMonitorSettingsPanel.Controls.AddRange(new Control[] {
                selectedMonitorLabel,
                perMonitorEnabledCheckBox,
                globalSettingsNote
            });

            yPos += 140;

            // Help text
            var multiMonitorHelpLabel = new Label
            {
                Text = "Multi-monitor settings control how windows behave across multiple displays.\n\n" +
                       "• Edge Wrapping: Windows pushed to screen edges wrap to adjacent monitors\n" +
                       "• Per-Monitor: Configure different behavior for each display\n" +
                       "• DPI-Aware: Settings automatically scale with monitor DPI",
                Location = new Point(12, yPos),
                Size = new Size(550, 60),
                ForeColor = SystemColors.GrayText
            };

            multiMonitorTabPage.Controls.AddRange(new Control[] {
                enableWrappingCheckBox,
                wrapPreferenceLabel, wrapPreferenceComboBox,
                respectTaskbarAreasCheckBox,
                perMonitorLabel,
                monitorListLabel, monitorListBox,
                perMonitorSettingsPanel,
                multiMonitorHelpLabel
            });
        }

        private void InitializeAdvancedTab()
        {
            advancedTabPage = new TabPage("Advanced")
            {
                Padding = new Padding(12),
                UseVisualStyleBackColor = true
            };

            int yPos = 12;

            // Performance Settings Group
            var performanceGroupBox = new GroupBox
            {
                Text = "Performance Settings",
                Location = new Point(12, yPos),
                Size = new Size(550, 120)
            };

            // Update Interval
            updateIntervalLabel = new Label
            {
                Text = "Update Interval (ms):",
                Location = new Point(12, 25),
                Size = new Size(120, 20)
            };

            updateIntervalNumeric = new NumericUpDown
            {
                Location = new Point(140, 25),
                Size = new Size(70, 20),
                Minimum = 1,
                Maximum = 100,
                DecimalPlaces = 0
            };

            var updateIntervalHelpLabel = new Label
            {
                Text = "Lower = more responsive, higher = better performance",
                Location = new Point(220, 25),
                Size = new Size(300, 20),
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8)
            };

            // Max Update Interval
            maxUpdateIntervalLabel = new Label
            {
                Text = "Max Update Interval (ms):",
                Location = new Point(12, 50),
                Size = new Size(120, 20)
            };

            maxUpdateIntervalNumeric = new NumericUpDown
            {
                Location = new Point(140, 50),
                Size = new Size(70, 20),
                Minimum = 10,
                Maximum = 1000,
                DecimalPlaces = 0
            };

            var maxUpdateIntervalHelpLabel = new Label
            {
                Text = "Fallback interval when system is busy",
                Location = new Point(220, 50),
                Size = new Size(300, 20),
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8)
            };

            // Screen Edge Buffer
            screenEdgeBufferLabel = new Label
            {
                Text = "Screen Edge Buffer (px):",
                Location = new Point(12, 75),
                Size = new Size(120, 20)
            };

            screenEdgeBufferNumeric = new NumericUpDown
            {
                Location = new Point(140, 75),
                Size = new Size(70, 20),
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 0
            };

            var screenEdgeBufferHelpLabel = new Label
            {
                Text = "Minimum distance from screen edges",
                Location = new Point(220, 75),
                Size = new Size(300, 20),
                ForeColor = SystemColors.GrayText,
                Font = new Font(Font.FontFamily, 8)
            };

            performanceGroupBox.Controls.AddRange(new Control[] {
                updateIntervalLabel, updateIntervalNumeric, updateIntervalHelpLabel,
                maxUpdateIntervalLabel, maxUpdateIntervalNumeric, maxUpdateIntervalHelpLabel,
                screenEdgeBufferLabel, screenEdgeBufferNumeric, screenEdgeBufferHelpLabel
            });

            yPos += 130;

            // Configuration Management Group
            var configGroupBox = new GroupBox
            {
                Text = "Configuration Management",
                Location = new Point(12, yPos),
                Size = new Size(550, 80)
            };

            // Preset Selection
            presetLabel = new Label
            {
                Text = "Presets:",
                Location = new Point(12, 25),
                Size = new Size(60, 20)
            };

            presetComboBox = new ComboBox
            {
                Location = new Point(80, 25),
                Size = new Size(120, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            presetComboBox.Items.AddRange(new[] { "Default", "Performance", "Responsive" });

            // Export/Import/Reset buttons
            exportButton = new Button
            {
                Text = "Export...",
                Location = new Point(220, 24),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true
            };

            importButton = new Button
            {
                Text = "Import...",
                Location = new Point(305, 24),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true
            };

            resetButton = new Button
            {
                Text = "Reset",
                Location = new Point(390, 24),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true
            };

            configGroupBox.Controls.AddRange(new Control[] {
                presetLabel, presetComboBox,
                exportButton, importButton, resetButton
            });

            advancedTabPage.Controls.AddRange(new Control[] {
                performanceGroupBox,
                configGroupBox
            });
        }

        private void InitializeFormButtons()
        {
            var buttonPanel = new Panel
            {
                Height = 45,
                Dock = DockStyle.Bottom,
                Padding = new Padding(8)
            };

            okButton = new Button
            {
                Text = "OK",
                Size = new Size(75, 23),
                Location = new Point(350, 11),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.OK
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(75, 23),
                Location = new Point(435, 11),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.Cancel
            };

            applyButton = new Button
            {
                Text = "Apply",
                Size = new Size(75, 23),
                Location = new Point(520, 11),
                UseVisualStyleBackColor = true
            };

            buttonPanel.Controls.AddRange(new Control[] {
                okButton, cancelButton, applyButton
            });

            Controls.Add(buttonPanel);

            // Set dialog buttons
            AcceptButton = okButton;
            CancelButton = cancelButton;

            // Set up event handlers
            okButton.Click += OnOkButtonClick;
            cancelButton.Click += OnCancelButtonClick;
            applyButton.Click += OnApplyButtonClick;
            exportButton.Click += OnExportButtonClick;
            importButton.Click += OnImportButtonClick;
            resetButton.Click += OnResetButtonClick;
        }


    }
}