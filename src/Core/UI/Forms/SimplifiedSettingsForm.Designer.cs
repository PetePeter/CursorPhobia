using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.UI.Forms
{
    partial class SimplifiedSettingsForm
    {
        // Main Enable/Disable Control
        private CheckBox enableCursorPhobiaCheckBox;
        
        // Quick Setup Preset Controls
        private GroupBox quickSetupGroupBox;
        private RadioButton gentleRadioButton;
        private RadioButton balancedRadioButton;
        private RadioButton aggressiveRadioButton;
        
        // Fine Tuning Controls
        private GroupBox fineTuningGroupBox;
        private Label proximityThresholdLabel;
        private NumericUpDown proximityThresholdNumeric;
        private Label proximityThresholdUnitsLabel;
        private Button proximityThresholdHelpButton;
        private Label pushDistanceLabel;
        private NumericUpDown pushDistanceNumeric;
        private Label pushDistanceUnitsLabel;
        private Button pushDistanceHelpButton;
        
        // Advanced Options Toggle
        private CheckBox showAdvancedCheckBox;
        
        // Advanced Options Panel (Collapsible)
        private Panel advancedOptionsPanel;
        private GroupBox hoverTimeoutGroupBox;
        private CheckBox enableHoverTimeoutCheckBox;
        private Label hoverTimeoutLabel;
        private NumericUpDown hoverTimeoutNumeric;
        private Label hoverTimeoutUnitsLabel;
        private CheckBox applyToAllWindowsCheckBox;
        private GroupBox multiMonitorGroupBox;
        private CheckBox enableWrappingCheckBox;
        private CheckBox respectTaskbarAreasCheckBox;
        
        // Form Buttons
        private Button okButton;
        private Button cancelButton;
        private Button applyButton;

        private void InitializeComponent()
        {
            SuspendLayout();
            
            // Form properties - compact size for simplified interface
            Text = "CursorPhobia Settings";
            Size = new Size(420, 360);
            MinimumSize = new Size(400, 340);
            MaximumSize = new Size(500, 600); // Increased to allow for advanced panel
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Font = new Font("Segoe UI", 9F);
            
            InitializeMainControls();
            InitializeQuickSetupControls();
            InitializeFineTuningControls();
            InitializeAdvancedOptionsToggle();
            InitializeAdvancedOptionsPanel();
            InitializeFormButtons();
            
            // Add all controls to form
            Controls.AddRange(new Control[] {
                enableCursorPhobiaCheckBox,
                quickSetupGroupBox,
                fineTuningGroupBox,
                showAdvancedCheckBox,
                advancedOptionsPanel,
                okButton,
                cancelButton,
                applyButton
            });
            
            // Set tab order for proper navigation
            SetTabOrder();
            
            ResumeLayout(false);
        }

        private void InitializeMainControls()
        {
            // Main enable/disable checkbox
            enableCursorPhobiaCheckBox = new CheckBox
            {
                Text = "&Enable CursorPhobia",
                Location = new Point(20, 20),
                Size = new Size(200, 24),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true,
                Checked = true
            };
        }

        private void InitializeQuickSetupControls()
        {
            // Quick Setup group box
            quickSetupGroupBox = new GroupBox
            {
                Text = "Quick Setup:",
                Location = new Point(20, 55),
                Size = new Size(360, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            // Gentle preset radio button
            gentleRadioButton = new RadioButton
            {
                Text = "&Gentle",
                Location = new Point(20, 25),
                Size = new Size(80, 24),
                UseVisualStyleBackColor = true
            };

            // Balanced preset radio button (default selection)
            balancedRadioButton = new RadioButton
            {
                Text = "&Balanced",
                Location = new Point(120, 25),
                Size = new Size(80, 24),
                UseVisualStyleBackColor = true,
                Checked = true
            };

            // Aggressive preset radio button
            aggressiveRadioButton = new RadioButton
            {
                Text = "&Aggressive",
                Location = new Point(220, 25),
                Size = new Size(80, 24),
                UseVisualStyleBackColor = true
            };

            // Add radio buttons to group box
            quickSetupGroupBox.Controls.AddRange(new Control[] {
                gentleRadioButton,
                balancedRadioButton,
                aggressiveRadioButton
            });
        }

        private void InitializeFineTuningControls()
        {
            // Fine Tuning group box
            fineTuningGroupBox = new GroupBox
            {
                Text = "Fine Tuning:",
                Location = new Point(20, 150),
                Size = new Size(360, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            // Proximity Threshold (Trigger Distance) controls
            proximityThresholdLabel = new Label
            {
                Text = "&Trigger Distance:",
                Location = new Point(15, 28),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            proximityThresholdNumeric = new NumericUpDown
            {
                Location = new Point(125, 25),
                Size = new Size(60, 23),
                Minimum = 10,
                Maximum = 200,
                Value = 50,
                TextAlign = HorizontalAlignment.Center
            };

            proximityThresholdUnitsLabel = new Label
            {
                Text = "pixels",
                Location = new Point(195, 28),
                Size = new Size(40, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            proximityThresholdHelpButton = new Button
            {
                Text = "?",
                Location = new Point(240, 25),
                Size = new Size(23, 23),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                UseVisualStyleBackColor = true,
                FlatStyle = FlatStyle.System
            };

            // Push Distance controls
            pushDistanceLabel = new Label
            {
                Text = "&Push Distance:",
                Location = new Point(15, 53),
                Size = new Size(100, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            pushDistanceNumeric = new NumericUpDown
            {
                Location = new Point(125, 50),
                Size = new Size(60, 23),
                Minimum = 25,
                Maximum = 500,
                Value = 100,
                TextAlign = HorizontalAlignment.Center
            };

            pushDistanceUnitsLabel = new Label
            {
                Text = "pixels",
                Location = new Point(195, 53),
                Size = new Size(40, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            pushDistanceHelpButton = new Button
            {
                Text = "?",
                Location = new Point(240, 50),
                Size = new Size(23, 23),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                UseVisualStyleBackColor = true,
                FlatStyle = FlatStyle.System
            };

            // Add controls to fine tuning group box
            fineTuningGroupBox.Controls.AddRange(new Control[] {
                proximityThresholdLabel,
                proximityThresholdNumeric,
                proximityThresholdUnitsLabel,
                proximityThresholdHelpButton,
                pushDistanceLabel,
                pushDistanceNumeric,
                pushDistanceUnitsLabel,
                pushDistanceHelpButton
            });

            // Set up help button event handlers
            proximityThresholdHelpButton.Click += (s, e) => ShowHelp(
                "Trigger Distance",
                "The distance in pixels from the cursor to a window edge that triggers the pushing behavior.\n\n" +
                "• Lower values (30px) = More sensitive, triggers closer to windows\n" +
                "• Higher values (75px) = Less sensitive, triggers further from windows\n\n" +
                "Recommended: 50px for balanced responsiveness.");

            pushDistanceHelpButton.Click += (s, e) => ShowHelp(
                "Push Distance", 
                "The distance in pixels that windows are moved away from the cursor.\n\n" +
                "• Lower values (75px) = Gentle movement, keeps windows nearby\n" +
                "• Higher values (150px) = Strong movement, pushes windows far away\n\n" +
                "Recommended: 100px for effective window clearing.");
        }

        private void InitializeAdvancedOptionsToggle()
        {
            // Show Advanced Options checkbox
            showAdvancedCheckBox = new CheckBox
            {
                Text = "Show &Advanced Options",
                Location = new Point(20, 250),
                Size = new Size(200, 24),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true,
                Checked = false
            };

            // Event handler for advanced options toggle
            showAdvancedCheckBox.CheckedChanged += OnShowAdvancedCheckBoxChanged;
        }

        private void InitializeAdvancedOptionsPanel()
        {
            // Advanced Options Panel (hidden by default)
            advancedOptionsPanel = new Panel
            {
                Location = new Point(20, 280),
                Size = new Size(360, 180),
                Visible = false,
                BorderStyle = BorderStyle.None
            };

            // Apply to All Windows checkbox
            applyToAllWindowsCheckBox = new CheckBox
            {
                Text = "Apply to &All Windows (not just topmost)",
                Location = new Point(0, 5),
                Size = new Size(340, 24),
                UseVisualStyleBackColor = true
            };

            // Hover Timeout group box
            hoverTimeoutGroupBox = new GroupBox
            {
                Text = "Hover Timeout:",
                Location = new Point(0, 35),
                Size = new Size(360, 70),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            enableHoverTimeoutCheckBox = new CheckBox
            {
                Text = "&Enable hover timeout",
                Location = new Point(15, 25),
                Size = new Size(160, 24),
                UseVisualStyleBackColor = true
            };

            hoverTimeoutLabel = new Label
            {
                Text = "Timeout:",
                Location = new Point(200, 28),
                Size = new Size(55, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            hoverTimeoutNumeric = new NumericUpDown
            {
                Location = new Point(260, 25),
                Size = new Size(60, 23),
                Minimum = 100,
                Maximum = 10000,
                Value = 2000,
                Increment = 100,
                TextAlign = HorizontalAlignment.Center
            };

            hoverTimeoutUnitsLabel = new Label
            {
                Text = "ms",
                Location = new Point(325, 28),
                Size = new Size(25, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            hoverTimeoutGroupBox.Controls.AddRange(new Control[] {
                enableHoverTimeoutCheckBox,
                hoverTimeoutLabel,
                hoverTimeoutNumeric,
                hoverTimeoutUnitsLabel
            });

            // Multi-Monitor group box
            multiMonitorGroupBox = new GroupBox
            {
                Text = "Multi-Monitor:",
                Location = new Point(0, 110),
                Size = new Size(360, 65),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            enableWrappingCheckBox = new CheckBox
            {
                Text = "Enable edge &wrapping",
                Location = new Point(15, 25),
                Size = new Size(160, 24),
                UseVisualStyleBackColor = true
            };

            respectTaskbarAreasCheckBox = new CheckBox
            {
                Text = "Respect &taskbar areas",
                Location = new Point(190, 25),
                Size = new Size(160, 24),
                UseVisualStyleBackColor = true
            };

            multiMonitorGroupBox.Controls.AddRange(new Control[] {
                enableWrappingCheckBox,
                respectTaskbarAreasCheckBox
            });

            // Add controls to advanced panel
            advancedOptionsPanel.Controls.AddRange(new Control[] {
                applyToAllWindowsCheckBox,
                hoverTimeoutGroupBox,
                multiMonitorGroupBox
            });
        }

        private void InitializeFormButtons()
        {
            // OK Button - position will be updated based on advanced panel visibility
            okButton = new Button
            {
                Text = "&OK",
                Location = new Point(140, 290),
                Size = new Size(75, 30),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.OK
            };
            okButton.Click += OnOkButtonClick;

            // Cancel Button
            cancelButton = new Button
            {
                Text = "&Cancel",
                Location = new Point(225, 290),
                Size = new Size(75, 30),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.Cancel
            };
            cancelButton.Click += OnCancelButtonClick;

            // Apply Button
            applyButton = new Button
            {
                Text = "&Apply",
                Location = new Point(310, 290),
                Size = new Size(75, 30),
                UseVisualStyleBackColor = true
            };
            applyButton.Click += OnApplyButtonClick;

            // Set default and cancel buttons
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void SetTabOrder()
        {
            // Set proper tab order for accessibility
            int tabIndex = 0;
            
            enableCursorPhobiaCheckBox.TabIndex = tabIndex++;
            gentleRadioButton.TabIndex = tabIndex++;
            balancedRadioButton.TabIndex = tabIndex++;
            aggressiveRadioButton.TabIndex = tabIndex++;
            proximityThresholdNumeric.TabIndex = tabIndex++;
            proximityThresholdHelpButton.TabIndex = tabIndex++;
            pushDistanceNumeric.TabIndex = tabIndex++;
            pushDistanceHelpButton.TabIndex = tabIndex++;
            showAdvancedCheckBox.TabIndex = tabIndex++;
            
            // Advanced controls tab order (will be updated when panel is shown)
            applyToAllWindowsCheckBox.TabIndex = tabIndex++;
            enableHoverTimeoutCheckBox.TabIndex = tabIndex++;
            hoverTimeoutNumeric.TabIndex = tabIndex++;
            enableWrappingCheckBox.TabIndex = tabIndex++;
            respectTaskbarAreasCheckBox.TabIndex = tabIndex++;
            
            okButton.TabIndex = tabIndex++;
            cancelButton.TabIndex = tabIndex++;
            applyButton.TabIndex = tabIndex++;
        }

        /// <summary>
        /// Updates the form layout when advanced options are toggled
        /// </summary>
        private void UpdateFormLayout(bool showAdvanced)
        {
            const int compactHeight = 360;
            const int expandedHeight = 540;
            const int compactButtonY = 290;
            const int expandedButtonY = 470;

            if (showAdvanced)
            {
                // Show advanced panel and expand form
                advancedOptionsPanel.Visible = true;
                Size = new Size(Width, expandedHeight);
                
                // Move buttons down
                okButton.Location = new Point(okButton.Location.X, expandedButtonY);
                cancelButton.Location = new Point(cancelButton.Location.X, expandedButtonY);
                applyButton.Location = new Point(applyButton.Location.X, expandedButtonY);
            }
            else
            {
                // Hide advanced panel and contract form
                advancedOptionsPanel.Visible = false;
                Size = new Size(Width, compactHeight);
                
                // Move buttons up
                okButton.Location = new Point(okButton.Location.X, compactButtonY);
                cancelButton.Location = new Point(cancelButton.Location.X, compactButtonY);
                applyButton.Location = new Point(applyButton.Location.X, compactButtonY);
            }
        }

        /// <summary>
        /// Gets or sets the visibility of the advanced options panel
        /// </summary>
        public bool AdvancedOptionsVisible
        {
            get => advancedOptionsPanel.Visible;
            set
            {
                if (advancedOptionsPanel.Visible != value)
                {
                    UpdateFormLayout(value);
                }
            }
        }
    }
}