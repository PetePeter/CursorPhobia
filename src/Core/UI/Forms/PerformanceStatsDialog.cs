using System.Windows.Forms;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.UI.Forms;

/// <summary>
/// Dialog for displaying engine performance statistics
/// </summary>
public partial class PerformanceStatsDialog : Form
{
    private readonly ICursorPhobiaEngine _engine;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public PerformanceStatsDialog(ICursorPhobiaEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        InitializeComponent();

        // Setup refresh timer
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000, // Update every second
            Enabled = true
        };
        _refreshTimer.Tick += OnRefreshTimerTick;

        // Initial update
        UpdateStats();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        UpdateStats();
    }

    private void UpdateStats()
    {
        try
        {
            var stats = _engine.GetPerformanceStats();

            // Update labels
            engineStatusLabel.Text = stats.IsRunning ? "Running" : "Stopped";
            engineStatusLabel.ForeColor = stats.IsRunning ? Color.Green : Color.Red;

            uptimeLabel.Text = FormatTimeSpan(TimeSpan.FromMilliseconds(stats.UptimeMs));
            updateCountLabel.Text = stats.UpdateCount.ToString("N0");
            avgUpdateTimeLabel.Text = $"{stats.AverageUpdateTimeMs:F2} ms";
            trackedWindowsLabel.Text = stats.TrackedWindowCount.ToString();
            configuredIntervalLabel.Text = $"{stats.ConfiguredUpdateIntervalMs} ms";
            updatesPerSecondLabel.Text = $"{stats.UpdatesPerSecond:F1}";
            estimatedCpuLabel.Text = $"{stats.EstimatedCpuUsagePercent:F1}%";

            successfulUpdatesLabel.Text = stats.SuccessfulUpdates.ToString("N0");
            failedUpdatesLabel.Text = stats.FailedUpdates.ToString("N0");
            totalUpdatesLabel.Text = stats.TotalUpdates.ToString("N0");

            var successRate = stats.TotalUpdates > 0 ?
                (double)stats.SuccessfulUpdates / stats.TotalUpdates * 100 : 0;
            successRateLabel.Text = $"{successRate:F1}%";
        }
        catch (Exception ex)
        {
            // Handle any errors gracefully
            engineStatusLabel.Text = $"Error: {ex.Message}";
            engineStatusLabel.ForeColor = Color.Red;
        }
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        return $"{timeSpan.Seconds}s";
    }

    private void OnCloseButtonClick(object sender, EventArgs e)
    {
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        base.OnFormClosing(e);
    }
}