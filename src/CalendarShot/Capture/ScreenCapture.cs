using System.Drawing.Imaging;

namespace CalendarShot.Capture;

internal static class ScreenCapture
{
    /// <summary>
    /// Captures one whole monitor. The process is per-monitor DPI aware, so the
    /// bounds are physical pixels and the image is captured at native resolution.
    /// </summary>
    /// <remarks>
    /// Only what is currently displayed can be captured. Windows does not render
    /// inactive virtual desktops, so the calendar must be on the desktop in view.
    /// </remarks>
    public static Bitmap Capture(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        Rectangle bounds = screen.Bounds;

        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static Screen ScreenUnderCursor() => Screen.FromPoint(Cursor.Position);
}
