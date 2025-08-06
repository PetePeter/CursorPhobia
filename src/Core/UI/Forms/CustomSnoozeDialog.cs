using System.Windows.Forms;

namespace CursorPhobia.Core.UI.Forms;

/// <summary>
/// Dialog for custom snooze duration input
/// </summary>
public partial class CustomSnoozeDialog : Form
{
    /// <summary>
    /// Gets the selected snooze duration
    /// </summary>
    public TimeSpan SnoozeDuration { get; private set; }

    public CustomSnoozeDialog()
    {
        InitializeComponent();
    }

    private void OnOkButtonClick(object sender, EventArgs e)
    {
        var hours = (int)hoursNumeric.Value;
        var minutes = (int)minutesNumeric.Value;

        if (hours == 0 && minutes == 0)
        {
            MessageBox.Show("Please enter a valid duration.", "Invalid Duration",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SnoozeDuration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnCancelButtonClick(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}