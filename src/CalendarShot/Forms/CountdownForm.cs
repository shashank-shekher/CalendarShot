using System.Globalization;

namespace CalendarShot.Forms;

/// <summary>
/// Gives the user a few seconds to bring the calendar to the front before the
/// screen is captured.
/// </summary>
internal sealed class CountdownForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly Label _message = new() { AutoSize = true };
    private readonly Screen _screen;
    private int _remaining;

    public CountdownForm(int seconds, Screen screen)
    {
        _remaining = seconds;
        _screen = screen;

        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(12);

        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        layout.Controls.Add(_message);
        layout.Controls.Add(cancel);
        Controls.Add(layout);
        CancelButton = cancel;

        _timer.Tick += (_, _) => Tick();
        UpdateMessage();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Shown on the screen being captured; it is hidden before the capture is taken.
        Rectangle area = _screen.WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Tick()
    {
        _remaining--;
        if (_remaining <= 0)
        {
            _timer.Stop();
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        UpdateMessage();
    }

    private void UpdateMessage() =>
        _message.Text = string.Format(
            CultureInfo.CurrentCulture,
            "Bring your calendar to the front on this screen.\nCapturing in {0} second{1}…",
            _remaining,
            _remaining == 1 ? string.Empty : "s");
}
