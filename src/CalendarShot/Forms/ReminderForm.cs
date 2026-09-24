namespace CalendarShot.Forms;

internal enum ReminderChoice
{
    Capture,
    Snooze,
    Skip,
}

/// <summary>
/// Small always-on-top prompt shown at the reminder time. A window is used
/// instead of a toast notification because notifications can be suppressed
/// by Focus Assist and cannot offer several actions from a tray application.
/// </summary>
internal sealed class ReminderForm : Form
{
    public const int SnoozeMinutes = 15;

    public ReminderForm()
    {
        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(12);

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        layout.Controls.Add(new Label
        {
            Text = "Time to capture your calendar.",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
        });
        layout.Controls.Add(new Label
        {
            Text = "Open your calendar in month view (or the period you need),\n" +
                   "then click Capture.",
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 12),
        });

        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttons.Controls.Add(CreateButton("Capture", ReminderChoice.Capture));
        buttons.Controls.Add(CreateButton($"Snooze {SnoozeMinutes} min", ReminderChoice.Snooze));
        buttons.Controls.Add(CreateButton("Skip today", ReminderChoice.Skip));
        layout.Controls.Add(buttons);

        Controls.Add(layout);
    }

    /// <summary>Closing the window without choosing counts as skipping today.</summary>
    public ReminderChoice Choice { get; private set; } = ReminderChoice.Skip;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
    }

    private Button CreateButton(string text, ReminderChoice choice)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) =>
        {
            Choice = choice;
            DialogResult = DialogResult.OK;
            Close();
        };
        return button;
    }
}
