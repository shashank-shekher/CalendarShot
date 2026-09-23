using System.Drawing.Imaging;

namespace CalendarShot.Capture;

internal static class ScreenCapture
{
    /// <summary>
    /// Captures the full monitor under the mouse pointer. The process is
    /// per-monitor DPI aware, so the bounds are physical pixels and the image
    /// is captured at native resolution.
    /// </summary>
    public static Bitmap CaptureScreenUnderCursor()
    {
        Screen screen = Screen.FromPoint(Cursor.Position);
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
}
