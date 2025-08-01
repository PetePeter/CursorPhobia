namespace CursorPhobia.Core.UI.Forms
{
    partial class CustomSnoozeDialog
    {
        private System.ComponentModel.IContainer components = null;
        private NumericUpDown hoursNumeric;
        private NumericUpDown minutesNumeric;
        private Label hoursLabel;
        private Label minutesLabel;
        private Button okButton;
        private Button cancelButton;

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
            Text = "Custom Snooze Duration";
            Size = new Size(300, 180);
            MinimumSize = new Size(280, 160);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            
            // Hours label
            hoursLabel = new Label
            {
                Text = "Hours:",
                Location = new Point(20, 30),
                Size = new Size(50, 20)
            };
            
            // Hours numeric
            hoursNumeric = new NumericUpDown
            {
                Location = new Point(80, 28),
                Size = new Size(60, 20),
                Minimum = 0,
                Maximum = 24,
                Value = 0
            };
            
            // Minutes label
            minutesLabel = new Label
            {
                Text = "Minutes:",
                Location = new Point(20, 60),
                Size = new Size(50, 20)
            };
            
            // Minutes numeric
            minutesNumeric = new NumericUpDown
            {
                Location = new Point(80, 58),
                Size = new Size(60, 20),
                Minimum = 0,
                Maximum = 59,
                Value = 30,
                Increment = 5
            };
            
            // OK button
            okButton = new Button
            {
                Text = "OK",
                Location = new Point(115, 100),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.OK
            };
            okButton.Click += OnOkButtonClick;
            
            // Cancel button
            cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(200, 100),
                Size = new Size(75, 23),
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.Cancel
            };
            cancelButton.Click += OnCancelButtonClick;
            
            // Add controls to form
            Controls.AddRange(new Control[] {
                hoursLabel, hoursNumeric,
                minutesLabel, minutesNumeric,
                okButton, cancelButton
            });
            
            // Set default button
            AcceptButton = okButton;
            CancelButton = cancelButton;
            
            ResumeLayout(false);
        }
    }
}