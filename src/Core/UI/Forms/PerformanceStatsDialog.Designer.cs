namespace CursorPhobia.Core.UI.Forms
{
    partial class PerformanceStatsDialog
    {
        private System.ComponentModel.IContainer components = null;
        private Label engineStatusLabel;
        private Label uptimeLabel;
        private Label updateCountLabel;
        private Label avgUpdateTimeLabel;
        private Label trackedWindowsLabel;
        private Label configuredIntervalLabel;
        private Label updatesPerSecondLabel;
        private Label estimatedCpuLabel;
        private Label successfulUpdatesLabel;
        private Label failedUpdatesLabel;
        private Label totalUpdatesLabel;
        private Label successRateLabel;
        private Button closeButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            
            // Form properties
            Text = "CursorPhobia Performance Statistics";
            Size = new Size(450, 400);
            MinimumSize = new Size(400, 350);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            
            int yPos = 20;
            const int labelHeight = 20;
            const int spacing = 25;
            
            // Engine Status
            CreateStatRow("Engine Status:", out engineStatusLabel, ref yPos, spacing);
            
            // Uptime
            CreateStatRow("Uptime:", out uptimeLabel, ref yPos, spacing);
            
            // Update Count
            CreateStatRow("Total Updates:", out updateCountLabel, ref yPos, spacing);
            
            // Average Update Time
            CreateStatRow("Avg Update Time:", out avgUpdateTimeLabel, ref yPos, spacing);
            
            // Tracked Windows
            CreateStatRow("Tracked Windows:", out trackedWindowsLabel, ref yPos, spacing);
            
            // Configured Interval
            CreateStatRow("Update Interval:", out configuredIntervalLabel, ref yPos, spacing);
            
            // Updates Per Second
            CreateStatRow("Updates/Second:", out updatesPerSecondLabel, ref yPos, spacing);
            
            // Estimated CPU Usage
            CreateStatRow("Est. CPU Usage:", out estimatedCpuLabel, ref yPos, spacing);
            
            // Add separator
            yPos += 10;
            var separator = new Label
            {
                Text = "Update Statistics:",
                Location = new Point(20, yPos),
                Size = new Size(150, labelHeight),
                Font = new Font(Font, FontStyle.Bold)
            };
            Controls.Add(separator);
            yPos += spacing;
            
            // Successful Updates
            CreateStatRow("Successful:", out successfulUpdatesLabel, ref yPos, spacing);
            
            // Failed Updates
            CreateStatRow("Failed:", out failedUpdatesLabel, ref yPos, spacing);
            
            // Total Updates
            CreateStatRow("Total:", out totalUpdatesLabel, ref yPos, spacing);
            
            // Success Rate
            CreateStatRow("Success Rate:", out successRateLabel, ref yPos, spacing);
            
            // Close button
            closeButton = new Button
            {
                Text = "Close",
                Location = new Point(350, yPos + 10),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.OK
            };
            closeButton.Click += OnCloseButtonClick;
            Controls.Add(closeButton);
            
            // Set default button
            AcceptButton = closeButton;
            CancelButton = closeButton;
            
            ResumeLayout(false);
        }
        
        private void CreateStatRow(string labelText, out Label valueLabel, ref int yPos, int spacing)
        {
            var nameLabel = new Label
            {
                Text = labelText,
                Location = new Point(20, yPos), 
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            valueLabel = new Label
            {
                Text = "Loading...",
                Location = new Point(180, yPos),
                Size = new Size(200, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font, FontStyle.Bold)
            };
            
            Controls.AddRange(new Control[] { nameLabel, valueLabel });
            yPos += spacing;
        }
    }
}