using System.Globalization;
using CalendarShot.Core.Configuration;
using CalendarShot.Ocr;

namespace CalendarShot.Forms;

/// <summary>
/// Collects and validates all user settings. Shown on first run and from the tray menu.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox _exportFolder = new() { Width = 360 };
    private readonly CheckBox _remindersEnabled = new() { Text = "Remind me to capture my calendar", AutoSize = true };
    private readonly DateTimePicker _reminderTime = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern,
        ShowUpDown = true,
        Width = 120,
    };
    private readonly FlowLayoutPanel _days = new() { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
    private readonly NumericUpDown _captureDelay = new() { Minimum = AppSettingsValidator.MinCaptureDelay, Maximum = AppSettingsValidator.MaxCaptureDelay, Width = 70 };
    private readonly NumericUpDown _eventMinutes = new() { Minimum = AppSettingsValidator.MinEventMinutes, Maximum = AppSettingsValidator.MaxEventMinutes, Increment = 5, Width = 70 };
    private readonly ComboBox _ocrLanguage = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly CheckBox _startWithWindows = new() { Text = "Start CalendarShot when I sign in to Windows", AutoSize = true };
    private readonly CheckBox _keepScreenshots = new() { Text = "Keep captured screenshots (for troubleshooting)", AutoSize = true };

    public SettingsForm(AppSettings settings, string storageFolder, bool firstRun)
    {
        _settings = settings.Clone();

        Text = firstRun ? $"Welcome to {AppInfo.Name}" : $"{AppInfo.Name} Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = SystemFonts.MessageBoxFont ?? Font;
        Padding = new Padding(12);
        ShowInTaskbar = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        if (firstRun)
        {
            var intro = new Label
            {
                Text = "CalendarShot reminds you to take a screenshot of your calendar and turns it into\n" +
                       "monthly .ics files you can import anywhere. Please review these settings.",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
            };
            layout.Controls.Add(intro);
            layout.SetColumnSpan(intro, 2);
        }

        var browse = new Button { Text = "Browse…", AutoSize = true };
        browse.Click += (_, _) => BrowseForFolder();
        var folderRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        folderRow.Controls.Add(_exportFolder);
        folderRow.Controls.Add(browse);
        AddRow(layout, "Save .ics files to:", folderRow);

        layout.Controls.Add(_remindersEnabled);
        layout.SetColumnSpan(_remindersEnabled, 2);
        _remindersEnabled.CheckedChanged += (_, _) => UpdateReminderControls();

        AddRow(layout, "Reminder time:", _reminderTime);

        foreach (DayOfWeek day in OrderedWeekdays())
        {
            _days.Controls.Add(new CheckBox
            {
                Text = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day),
                Tag = day,
                AutoSize = true,
                Checked = _settings.ReminderDays.Contains(day),
            });
        }

        AddRow(layout, "On these days:", _days);
        AddRow(layout, "Capture delay (seconds):", _captureDelay);
        AddRow(layout, "Default event length (minutes):", _eventMinutes);
        AddRow(layout, "Text recognition language:", _ocrLanguage);

        layout.Controls.Add(_startWithWindows);
        layout.SetColumnSpan(_startWithWindows, 2);
        layout.Controls.Add(_keepScreenshots);
        layout.SetColumnSpan(_keepScreenshots, 2);

        var storageNote = new Label
        {
            Text = $"Settings and history are stored in {storageFolder}",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 12, 0, 0),
        };
        layout.Controls.Add(storageNote);
        layout.SetColumnSpan(storageNote, 2);

        var save = new Button { Text = firstRun ? "Save and start" : "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;

        LoadValues();
    }

    /// <summary>The saved settings; valid only when the dialog result is OK.</summary>
    public AppSettings Result => _settings;

    private static IEnumerable<DayOfWeek> OrderedWeekdays()
    {
        DayOfWeek first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        return Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)first + i) % 7));
    }

    private static void AddRow(TableLayoutPanel layout, string caption, Control control)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6) });
        control.Anchor = AnchorStyles.Left;
        layout.Controls.Add(control);
    }

    private void LoadValues()
    {
        _exportFolder.Text = _settings.ExportFolder;
        _remindersEnabled.Checked = _settings.RemindersEnabled;
        _reminderTime.Value = DateTime.Today.Add(_settings.ReminderTime.ToTimeSpan());
        _captureDelay.Value = Math.Clamp(_settings.CaptureDelaySeconds, AppSettingsValidator.MinCaptureDelay, AppSettingsValidator.MaxCaptureDelay);
        _eventMinutes.Value = Math.Clamp(_settings.DefaultEventMinutes, AppSettingsValidator.MinEventMinutes, AppSettingsValidator.MaxEventMinutes);
        _startWithWindows.Checked = _settings.StartWithWindows;
        _keepScreenshots.Checked = _settings.KeepScreenshots;

        _ocrLanguage.Items.Add(new LanguageItem(string.Empty, "Automatic (Windows display language)"));
        foreach ((string tag, string name) in WindowsOcrService.AvailableLanguages())
        {
            _ocrLanguage.Items.Add(new LanguageItem(tag, name));
        }

        _ocrLanguage.SelectedItem = _ocrLanguage.Items.Cast<LanguageItem>()
            .FirstOrDefault(i => string.Equals(i.Tag, _settings.OcrLanguage, StringComparison.OrdinalIgnoreCase))
            ?? _ocrLanguage.Items[0];

        UpdateReminderControls();
    }

    private void UpdateReminderControls()
    {
        _reminderTime.Enabled = _remindersEnabled.Checked;
        _days.Enabled = _remindersEnabled.Checked;
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where the monthly calendar files are saved",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (Directory.Exists(_exportFolder.Text))
        {
            dialog.InitialDirectory = _exportFolder.Text;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _exportFolder.Text = dialog.SelectedPath;
        }
    }

    private void Save()
    {
        _settings.ExportFolder = _exportFolder.Text.Trim();
        _settings.RemindersEnabled = _remindersEnabled.Checked;
        _settings.ReminderTime = new TimeOnly(_reminderTime.Value.Hour, _reminderTime.Value.Minute);
        _settings.ReminderDays = _days.Controls.OfType<CheckBox>().Where(c => c.Checked).Select(c => (DayOfWeek)c.Tag!).ToList();
        _settings.CaptureDelaySeconds = (int)_captureDelay.Value;
        _settings.DefaultEventMinutes = (int)_eventMinutes.Value;
        _settings.OcrLanguage = (_ocrLanguage.SelectedItem as LanguageItem)?.Tag ?? string.Empty;
        _settings.StartWithWindows = _startWithWindows.Checked;
        _settings.KeepScreenshots = _keepScreenshots.Checked;

        IReadOnlyList<string> errors = AppSettingsValidator.Validate(_settings);
        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, errors), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(_settings.ExportFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            MessageBox.Show(this, $"The export folder could not be created: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record LanguageItem(string Tag, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
