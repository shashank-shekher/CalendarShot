using System.Globalization;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Parsing;

namespace CalendarShot.Forms;

/// <summary>
/// Shows the screenshot next to the recognised events so the user can
/// correct them before anything is written.
/// </summary>
internal sealed class ReviewForm : Form
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";
    private const int MaxReplaceDays = 62;

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersWidth = 28,
    };

    private readonly CheckBox _replace = new() { AutoSize = true };
    private readonly DateTimePicker _replaceFrom = new() { Format = DateTimePickerFormat.Short, Width = 110 };
    private readonly DateTimePicker _replaceTo = new() { Format = DateTimePickerFormat.Short, Width = 110 };

    public ReviewForm(Image screenshot, RecognitionResult? result, string? problem)
    {
        Text = $"{AppInfo.Name} – Review events";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 760);
        MinimumSize = new Size(900, 500);
        Font = SystemFonts.MessageBoxFont ?? Font;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 560 };
        split.Panel1.Controls.Add(new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = screenshot,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = SystemColors.ControlDark,
        });

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        right.Controls.Add(BuildSummary(result, problem));
        right.Controls.Add(BuildReplaceRow(result));
        right.Controls.Add(_grid);
        right.Controls.Add(BuildButtons());
        split.Panel2.Controls.Add(right);
        Controls.Add(split);

        BuildColumns();
        if (result is not null)
        {
            foreach (CalendarEvent e in result.Events)
            {
                AddRow(e);
            }
        }
    }

    /// <summary>The reviewed events; set when the dialog result is OK.</summary>
    public ImportBatch? Batch { get; private set; }

    private static TextBox BuildSummary(RecognitionResult? result, string? problem)
    {
        var lines = new List<string>();
        if (result is null)
        {
            lines.Add(problem ?? "No events could be recognised.");
            lines.Add("You can add events manually with the Add button.");
        }
        else
        {
            string layout = result.Layout switch
            {
                CalendarLayout.MonthGrid => "Month view",
                CalendarLayout.TimeGrid => "Day/week view",
                _ => "Agenda view",
            };

            lines.Add(string.Format(
                CultureInfo.CurrentCulture,
                "{0} detected, {1:d} – {2:d}. {3} event(s) found. Untick or correct anything that was misread.",
                layout,
                result.CoveredFrom.ToDateTime(TimeOnly.MinValue),
                result.CoveredTo.ToDateTime(TimeOnly.MinValue),
                result.Events.Count));
            lines.AddRange(result.Warnings);
        }

        lines.Add($"Dates use {DateFormat}; times use 24-hour {TimeFormat}. Leave Start empty for an all-day event.");

        return new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Height = 96,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine, lines),
            TabStop = false,
        };
    }

    private FlowLayoutPanel BuildReplaceRow(RecognitionResult? result)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6) };
        _replace.Text = "Replace previously exported events from";
        _replace.Checked = result is not null;
        _replaceFrom.Value = (result?.CoveredFrom ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        _replaceTo.Value = (result?.CoveredTo ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);
        _replace.CheckedChanged += (_, _) => _replaceFrom.Enabled = _replaceTo.Enabled = _replace.Checked;
        _replaceFrom.Enabled = _replaceTo.Enabled = _replace.Checked;

        row.Controls.Add(_replace);
        row.Controls.Add(_replaceFrom);
        row.Controls.Add(new Label { Text = "to", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(4, 6, 4, 0) });
        row.Controls.Add(_replaceTo);

        var help = new ToolTip();
        help.SetToolTip(_replace, "Events that were removed from your calendar disappear from the exported file too. " +
            "Untick to only add events.");
        return row;
    }

    private FlowLayoutPanel BuildButtons()
    {
        var add = new Button { Text = "Add", AutoSize = true };
        var remove = new Button { Text = "Remove selected", AutoSize = true };
        var export = new Button { Text = "Export", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };

        add.Click += (_, _) =>
        {
            AddRow(new CalendarEvent(DateOnly.FromDateTime(_replaceFrom.Value), null, null, string.Empty));
            _grid.CurrentCell = _grid.Rows[^1].Cells["Title"];
            _grid.BeginEdit(true);
        };
        remove.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                _grid.Rows.Remove(row);
            }
        };
        export.Click += (_, _) => TryExport();

        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        panel.Controls.Add(cancel);
        panel.Controls.Add(export);
        panel.Controls.Add(remove);
        panel.Controls.Add(add);
        CancelButton = cancel;
        return panel;
    }

    private void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Include", HeaderText = "Use", Width = 40 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", Width = 95 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Start", HeaderText = "Start", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "End", HeaderText = "End", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", Width = 260, MaxInputLength = EventTextParser.MaxTitleLength });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Notes",
            HeaderText = "Notes",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MaxInputLength = 1000,
        });
    }

    private void AddRow(CalendarEvent e)
    {
        _grid.Rows.Add(
            true,
            e.Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            e.Start?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty,
            e.End?.ToString(TimeFormat, CultureInfo.InvariantCulture) ?? string.Empty,
            e.Title,
            e.Notes ?? string.Empty);
    }

    private void TryExport()
    {
        _grid.EndEdit();
        var events = new List<CalendarEvent>();
        bool valid = true;

        foreach (DataGridViewRow row in _grid.Rows)
        {
            row.ErrorText = string.Empty;
            if (row.Cells["Include"].Value is not true)
            {
                continue;
            }

            string? error = TryReadRow(row, out CalendarEvent? e);
            if (error is not null)
            {
                row.ErrorText = error;
                valid = false;
            }
            else
            {
                events.Add(e!);
            }
        }

        if (!valid)
        {
            MessageBox.Show(this, "Some rows need attention. Hover over the warning icons for details.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DateOnly? from = null;
        DateOnly? to = null;
        if (_replace.Checked)
        {
            from = DateOnly.FromDateTime(_replaceFrom.Value);
            to = DateOnly.FromDateTime(_replaceTo.Value);
            if (from > to || to.Value.DayNumber - from.Value.DayNumber > MaxReplaceDays)
            {
                MessageBox.Show(this, $"The replace range must run forwards and cover at most {MaxReplaceDays} days.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (events.Count == 0 && !_replace.Checked)
        {
            MessageBox.Show(this, "There is nothing to export.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Batch = new ImportBatch(events, from, to);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string? TryReadRow(DataGridViewRow row, out CalendarEvent? calendarEvent)
    {
        calendarEvent = null;
        string Cell(string name) => (row.Cells[name].Value as string ?? string.Empty).Trim();

        if (!DateOnly.TryParseExact(Cell("Date"), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return $"Enter the date as {DateFormat}.";
        }

        TimeOnly? start = null;
        TimeOnly? end = null;
        if (Cell("Start").Length > 0)
        {
            if (!EventTextParser.TryParseTime(Cell("Start"), out TimeOnly s))
            {
                return $"Enter the start time as {TimeFormat}, or leave it empty for an all-day event.";
            }

            start = s;
        }

        if (Cell("End").Length > 0)
        {
            if (start is null)
            {
                return "An end time needs a start time.";
            }

            if (!EventTextParser.TryParseTime(Cell("End"), out TimeOnly e))
            {
                return $"Enter the end time as {TimeFormat}.";
            }

            end = e;
        }

        string title = Cell("Title");
        if (title.Length == 0)
        {
            return "Enter a title.";
        }

        string notes = Cell("Notes");
        calendarEvent = new CalendarEvent(date, start, end, title, notes.Length > 0 ? notes : null);
        return null;
    }
}
