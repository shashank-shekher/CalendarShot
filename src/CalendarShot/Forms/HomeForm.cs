namespace CalendarShot.Forms;

internal enum HomeAction
{
    CaptureScreen,
    ImportFile,
    ImportClipboard,
    OpenExportFolder,
    Settings,
}

/// <summary>
/// Small main window with the everyday actions. Opened by clicking the tray
/// icon or by starting CalendarShot again while it is already running.
/// </summary>
internal sealed class HomeForm : Form
{
    public HomeForm(string screenSummary, string reminderSummary)
    {
        Text = AppInfo.Name;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(16);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };

        var capture = new Button
        {
            Text = "Capture now",
            Font = new Font(Font.FontFamily, Font.Size * 1.4f, FontStyle.Bold),
            AutoSize = true,
            MinimumSize = new Size(320, 56),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        capture.Click += (_, _) => Request(HomeAction.CaptureScreen);
        layout.Controls.Add(capture);

        var screenLine = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 12) };
        screenLine.Controls.Add(new Label { Text = screenSummary, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 3, 6, 0) });
        var change = new LinkLabel { Text = "Change", AutoSize = true, Margin = new Padding(0, 3, 0, 0) };
        change.LinkClicked += (_, _) => Request(HomeAction.Settings);
        screenLine.Controls.Add(change);
        layout.Controls.Add(screenLine);

        var imports = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        imports.Controls.Add(CreateButton("Import from file…", HomeAction.ImportFile));
        imports.Controls.Add(CreateButton("Import from clipboard", HomeAction.ImportClipboard));
        layout.Controls.Add(imports);

        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
        links.Controls.Add(CreateLink("Open export folder", HomeAction.OpenExportFolder));
        links.Controls.Add(CreateLink("Settings", HomeAction.Settings));
        layout.Controls.Add(links);

        layout.Controls.Add(new Label
        {
            Text = reminderSummary,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 0),
        });

        Controls.Add(layout);
        AcceptButton = capture;
    }

    /// <summary>Raised after the window has closed, so it never appears in a capture.</summary>
    public event EventHandler<HomeAction>? ActionRequested;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Near the notification area, like other tray applications.
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Request(HomeAction action)
    {
        Close();
        ActionRequested?.Invoke(this, action);
    }

    private Button CreateButton(string text, HomeAction action)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += (_, _) => Request(action);
        return button;
    }

    private LinkLabel CreateLink(string text, HomeAction action)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
        link.LinkClicked += (_, _) => Request(action);
        return link;
    }
}
