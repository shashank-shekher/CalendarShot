using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using CalendarShot.Capture;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Configuration;
using CalendarShot.Core.Export;
using CalendarShot.Core.Parsing;
using CalendarShot.Core.Recognition;
using CalendarShot.Core.Storage;
using CalendarShot.Forms;
using CalendarShot.Ocr;
using CalendarShot.Platform;

namespace CalendarShot.Tray;

/// <summary>
/// The application lives in the notification area. This class owns the tray
/// icon, the reminder timer and the capture → recognise → review → export flow.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan ReminderCheckInterval = TimeSpan.FromSeconds(30);

    private readonly StorageLayout _layout;
    private readonly FileLog _log;
    private readonly JsonFileStore<AppSettings> _settingsStore;
    private readonly JsonFileStore<AppState> _stateStore;

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _reminderTimer;
    private readonly SynchronizationContext _ui;

    private AppSettings _settings = new();
    private AppState _state = new();
    private DateTime? _snoozedUntil;
    private bool _busy;
    private bool _reminderOpen;
    private string? _lastExportFolder;
    private HomeForm? _home;

    public TrayApplicationContext(StorageLayout layout, FileLog log, SingleInstance instance, bool autoStarted)
    {
        _layout = layout;
        _log = log;
        _settingsStore = new JsonFileStore<AppSettings>(layout.SettingsFile, log);
        _stateStore = new JsonFileStore<AppState>(layout.StateFile, log);

        // A control must exist before the WinForms synchronisation context is installed.
        using (new Control())
        {
            _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        }

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = AppInfo.Name,
            ContextMenuStrip = BuildMenu(),
        };
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowHome();
            }
        };
        _trayIcon.BalloonTipClicked += (_, _) => OpenExportFolder();

        _reminderTimer = new System.Windows.Forms.Timer { Interval = (int)ReminderCheckInterval.TotalMilliseconds };
        _reminderTimer.Tick += (_, _) => CheckReminder();

        // Starting CalendarShot again (Start menu, shortcut) opens the main window.
        instance.OnActivationRequested(() => _ui.Post(_ => ShowHome(), null));

        // Start once the message loop is running so dialogs behave normally.
        _ui.Post(_ => Start(autoStarted), null);
    }

    private enum CaptureSource
    {
        Screen,
        File,
        Clipboard,
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reminderTimer.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void Start(bool autoStarted)
    {
        AppSettings? loaded = _settingsStore.Load();
        _state = _stateStore.Load() ?? new AppState();

        if (loaded is null)
        {
            var defaults = new AppSettings { ExportFolder = KnownFolders.Downloads };
            if (!ShowSettingsDialog(defaults, firstRun: true, out AppSettings? chosen))
            {
                _log.Info("First-run setup was cancelled.");
                ExitThread();
                return;
            }

            loaded = chosen;
        }
        else if (AppSettingsValidator.Validate(loaded).Count > 0)
        {
            _log.Warn("Stored settings are incomplete; asking the user to review them.");
            if (ShowSettingsDialog(loaded, firstRun: false, out AppSettings? repaired))
            {
                loaded = repaired;
            }
        }

        ApplySettings(loaded);
        _trayIcon.Visible = true;
        _reminderTimer.Start();

        CheckReminder();

        // Started by hand rather than at sign-in: show the main window so the
        // user sees that CalendarShot is running and can capture straight away.
        if (!autoStarted)
        {
            ShowHome();
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open CalendarShot", null, (_, _) => ShowHome())
        {
            Font = new Font(menu.Font, FontStyle.Bold),
        };
        menu.Items.Add(open);
        menu.Items.Add("Capture now", null, (_, _) => StartCapture(CaptureSource.Screen));
        menu.Items.Add("Import screenshot from file…", null, (_, _) => StartCapture(CaptureSource.File));
        menu.Items.Add("Import screenshot from clipboard", null, (_, _) => StartCapture(CaptureSource.Clipboard));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open export folder", null, (_, _) => OpenFolder(_settings.ExportFolder));
        menu.Items.Add("Open data folder", null, (_, _) => OpenFolder(_layout.RootDirectory));
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings(firstRun: false));
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowHome()
    {
        if (_busy)
        {
            return;
        }

        if (_home is { IsDisposed: false })
        {
            _home.Activate();
            return;
        }

        _home = new HomeForm(DescribeScreenChoice(), DescribeNextReminder());
        _home.ActionRequested += (_, action) => _ui.Post(_ => RunHomeAction(action), null);
        _home.FormClosed += (sender, _) =>
        {
            ((Form)sender!).Dispose();
            _home = null;
        };
        _home.Show();
        _home.Activate();
    }

    private void RunHomeAction(HomeAction action)
    {
        switch (action)
        {
            case HomeAction.CaptureScreen:
                StartCapture(CaptureSource.Screen);
                break;
            case HomeAction.ImportFile:
                StartCapture(CaptureSource.File);
                break;
            case HomeAction.ImportClipboard:
                StartCapture(CaptureSource.Clipboard);
                break;
            case HomeAction.OpenExportFolder:
                OpenFolder(_settings.ExportFolder);
                break;
            case HomeAction.Settings:
                ShowSettings(firstRun: false);
                break;
        }
    }

    private string DescribeScreenChoice()
    {
        if (Screen.AllScreens.Length == 1)
        {
            return "Captures your screen.";
        }

        return _settings.CaptureScreen switch
        {
            CaptureScreenMode.UnderPointer => "Captures the screen under the mouse pointer.",
            CaptureScreenMode.Specific when ScreenCatalog.FindByDevice(_settings.CaptureScreenDevice) is Screen screen =>
                $"Captures {ScreenCatalog.Describe(screen)}.",
            _ => "You choose the screen when capturing.",
        };
    }

    private string DescribeNextReminder()
    {
        DateTime now = DateTime.Now;
        DateTime? next = ReminderPolicy.NextReminder(now, _settings, _state.LastReminderDate, _snoozedUntil);
        if (next is not DateTime at)
        {
            return "Reminders are off.";
        }

        if (at <= now)
        {
            return "A capture reminder is due now.";
        }

        string day = at.Date == now.Date ? "today"
            : at.Date == now.Date.AddDays(1) ? "tomorrow"
            : at.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
        return $"Next reminder: {day} at {at.ToString("t", CultureInfo.CurrentCulture)}.";
    }

    private void ShowSettings(bool firstRun)
    {
        if (_busy)
        {
            return;
        }

        if (ShowSettingsDialog(_settings, firstRun, out AppSettings? updated))
        {
            ApplySettings(updated);
        }
    }

    private bool ShowSettingsDialog(AppSettings current, bool firstRun, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AppSettings? result)
    {
        using var form = new SettingsForm(current, _layout.RootDirectory, firstRun);
        if (form.ShowDialog() == DialogResult.OK)
        {
            result = form.Result;
            _settingsStore.Save(result);
            _log.Info("Settings saved.");
            return true;
        }

        result = null;
        return false;
    }

    private void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        try
        {
            StartupRegistration.Apply(settings.StartWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _log.Error("Could not update the Windows startup entry.", ex);
        }
    }

    private void CheckReminder()
    {
        if (_busy || _reminderOpen || !ReminderPolicy.IsDue(DateTime.Now, _settings, _state.LastReminderDate, _snoozedUntil))
        {
            return;
        }

        _reminderOpen = true;
        ReminderChoice choice;
        try
        {
            using var form = new ReminderForm();
            form.ShowDialog();
            choice = form.Choice;
        }
        finally
        {
            _reminderOpen = false;
        }

        if (choice == ReminderChoice.Snooze)
        {
            _snoozedUntil = DateTime.Now.AddMinutes(ReminderForm.SnoozeMinutes);
            return;
        }

        _snoozedUntil = null;
        _state.LastReminderDate = DateOnly.FromDateTime(DateTime.Now);
        _stateStore.Save(_state);

        if (choice == ReminderChoice.Capture)
        {
            StartCapture(CaptureSource.Screen);
        }
    }

    private async void StartCapture(CaptureSource source)
    {
        // async void is limited to this UI entry point; all failures are handled here.
        if (_busy)
        {
            return;
        }

        _busy = true;

        // Never let the main window end up in the screenshot.
        _home?.Close();
        try
        {
            await RunCaptureAsync(source);
        }
        catch (Exception ex)
        {
            _log.Error("Capture failed.", ex);
            MessageBox.Show(
                $"The calendar could not be processed.\n\n{ex.Message}",
                AppInfo.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RunCaptureAsync(CaptureSource source)
    {
        using Bitmap? image = await AcquireImageAsync(source);
        if (image is null)
        {
            return;
        }

        _trayIcon.Text = $"{AppInfo.Name} – reading screenshot…";
        OcrPage page;
        try
        {
            page = await WindowsOcrService.RecognizeAsync(image, _settings.OcrLanguage, CancellationToken.None);
        }
        finally
        {
            _trayIcon.Text = AppInfo.Name;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        var parser = new CalendarScreenshotParser(CreateVocabulary());
        ParseOutcome outcome = parser.Parse(page, today);
        _log.Info(outcome.Success
            ? $"Recognised {outcome.Result!.Layout} layout with {outcome.Result.Events.Count} event(s)."
            : "No calendar layout recognised.");

        if (_settings.KeepScreenshots)
        {
            SaveScreenshot(image);
        }

        ImportBatch? batch;
        using (var review = new ReviewForm(image, outcome.Result, outcome.Error))
        {
            if (review.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            batch = review.Batch;
        }

        if (batch is null)
        {
            return;
        }

        var exporter = new CalendarExporter(
            new MonthlyEventStore(_layout.DataDirectory, _log),
            new IcsCalendarWriter(TimeZoneInfo.Local, TimeSpan.FromMinutes(_settings.DefaultEventMinutes), AppInfo.Version),
            TimeProvider.System);

        ExportSummary summary = exporter.Export(batch, _settings.ExportFolder);
        _log.Info($"Exported {summary.ImportedEvents} event(s) into {summary.Files.Count} monthly file(s).");

        _lastExportFolder = _settings.ExportFolder;
        string files = string.Join(", ", summary.Files.Select(Path.GetFileName));
        _trayIcon.ShowBalloonTip(
            5000,
            "Calendar exported",
            string.Format(CultureInfo.CurrentCulture, "{0} event(s) saved to {1}. Click to open the folder.", summary.ImportedEvents, files),
            ToolTipIcon.Info);
    }

    private async Task<Bitmap?> AcquireImageAsync(CaptureSource source)
    {
        switch (source)
        {
            case CaptureSource.Screen:
                Screen? target = await ChooseScreenAsync();
                if (target is null)
                {
                    return null;
                }

                if (_settings.CaptureDelaySeconds > 0)
                {
                    using var countdown = new CountdownForm(_settings.CaptureDelaySeconds, target);
                    if (countdown.ShowDialog() != DialogResult.OK)
                    {
                        return null;
                    }
                }

                // Give the picker and countdown windows time to disappear from the screen.
                await Task.Delay(300);

                if (_settings.CaptureScreen == CaptureScreenMode.UnderPointer && Screen.AllScreens.Length > 1)
                {
                    target = ScreenCapture.ScreenUnderCursor();
                }

                _log.Info($"Capturing a {target.Bounds.Width}x{target.Bounds.Height} screen.");
                return ScreenCapture.Capture(target);

            case CaptureSource.File:
                using (var dialog = new OpenFileDialog { Filter = ImageSources.FileDialogFilter, Title = "Choose a calendar screenshot" })
                {
                    return dialog.ShowDialog() == DialogResult.OK ? ImageSources.LoadFromFile(dialog.FileName) : null;
                }

            case CaptureSource.Clipboard:
                Bitmap? pasted = ImageSources.LoadFromClipboard();
                if (pasted is null)
                {
                    MessageBox.Show(
                        "The clipboard does not contain an image. Take a screenshot (for example with Windows + Shift + S) and try again.",
                        AppInfo.Name,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return pasted;

            default:
                return null;
        }
    }

    /// <summary>
    /// The monitor to capture according to the settings, or <see langword="null"/>
    /// when the user cancelled the choice.
    /// </summary>
    private async Task<Screen?> ChooseScreenAsync()
    {
        Screen[] screens = Screen.AllScreens;
        if (screens.Length == 1)
        {
            return screens[0];
        }

        switch (_settings.CaptureScreen)
        {
            case CaptureScreenMode.UnderPointer:
                return ScreenCapture.ScreenUnderCursor();

            case CaptureScreenMode.Specific:
                if (ScreenCatalog.FindByDevice(_settings.CaptureScreenDevice) is Screen configured)
                {
                    return configured;
                }

                _log.Warn("The configured screen is not connected; asking the user instead.");
                break;
        }

        return await ScreenPicker.PickAsync();
    }

    private DateVocabulary CreateVocabulary()
    {
        var cultures = new List<CultureInfo?> { CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture };
        if (!string.IsNullOrEmpty(_settings.OcrLanguage))
        {
            try
            {
                cultures.Add(CultureInfo.GetCultureInfo(_settings.OcrLanguage));
            }
            catch (CultureNotFoundException)
            {
                _log.Warn("The configured OCR language has no matching culture; month names default to English.");
            }
        }

        return DateVocabulary.Create([.. cultures]);
    }

    private void SaveScreenshot(Bitmap image)
    {
        try
        {
            Directory.CreateDirectory(_layout.ScreenshotDirectory);
            string name = $"capture-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
            image.Save(Path.Combine(_layout.ScreenshotDirectory, name), ImageFormat.Png);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            _log.Error("Could not save the screenshot.", ex);
        }
    }

    private void OpenExportFolder() => OpenFolder(_lastExportFolder ?? _settings.ExportFolder);

    private static void OpenFolder(string path)
    {
        // Only ever hand an existing directory to the shell, never an arbitrary string.
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show("The folder does not exist yet.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(path),
            UseShellExecute = true,
            Verb = "open",
        });
    }

    private static void ShowAbout()
    {
        string text = $"{AppInfo.Name} {AppInfo.Version}\n\n" +
            "Turns calendar screenshots into monthly .ics files.\n" +
            "All processing happens on this computer.\n\n" +
            "Released under the MIT License.";
        if (!string.IsNullOrEmpty(AppInfo.RepositoryUrl))
        {
            text += $"\n{AppInfo.RepositoryUrl}";
        }

        MessageBox.Show(text, $"About {AppInfo.Name}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
