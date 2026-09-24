using System.Globalization;
using CalendarShot.Capture;

namespace CalendarShot.Forms;

/// <summary>
/// Lets the user choose a monitor by clicking it. A dimmed overlay with a large
/// screen number covers every monitor; clicking one selects it and Esc cancels.
/// </summary>
internal static class ScreenPicker
{
    public static Task<Screen?> PickAsync()
    {
        IReadOnlyList<Screen> screens = ScreenCatalog.Ordered();
        var result = new TaskCompletionSource<Screen?>();
        var overlays = new List<Overlay>();

        void Finish(Screen? chosen)
        {
            if (!result.TrySetResult(chosen))
            {
                return;
            }

            foreach (Overlay overlay in overlays)
            {
                overlay.Close();
                overlay.Dispose();
            }
        }

        for (int i = 0; i < screens.Count; i++)
        {
            var overlay = new Overlay(screens[i], i + 1, screens.Count);
            overlay.Chosen += (_, _) => Finish(overlay.Target);
            overlay.Cancelled += (_, _) => Finish(null);
            overlays.Add(overlay);
            overlay.Show();
        }

        // Give keyboard focus to the overlay under the pointer so that Esc works at once.
        Screen current = ScreenCapture.ScreenUnderCursor();
        overlays.FirstOrDefault(o => o.Target.DeviceName == current.DeviceName)?.Activate();

        return result.Task;
    }

    private sealed class Overlay : Form
    {
        public Overlay(Screen target, int number, int count)
        {
            Target = target;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(20, 24, 33);
            ForeColor = Color.White;
            Opacity = 0.82;
            Cursor = Cursors.Hand;

            // Place the window on its monitor first; it is maximised there on load,
            // which also covers monitors with a different display scaling correctly.
            Location = new Point(target.Bounds.X + 20, target.Bounds.Y + 20);
            Size = new Size(200, 200);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var numberLabel = new Label
            {
                Text = number.ToString(CultureInfo.CurrentCulture),
                Font = new Font("Segoe UI", 96, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom,
            };
            var hint = new Label
            {
                Text = $"{ScreenCatalog.Describe(target)}\n" +
                       (count > 1 ? "Click the screen that shows your calendar" : "Click to capture this screen") + " · Esc to cancel",
                Font = new Font("Segoe UI", 16),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.Top,
                Margin = new Padding(0, 12, 0, 0),
            };

            layout.Controls.Add(numberLabel, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            layout.Controls.Add(new Panel(), 0, 2);
            Controls.Add(layout);

            foreach (Control control in new Control[] { this, layout, numberLabel, hint })
            {
                control.Click += (_, _) => Chosen?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? Chosen;

        public event EventHandler? Cancelled;

        public Screen Target { get; }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            WindowState = FormWindowState.Maximized;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
