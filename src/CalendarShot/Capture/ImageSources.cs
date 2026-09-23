using System.Drawing.Imaging;

namespace CalendarShot.Capture;

/// <summary>
/// Loads screenshots supplied by the user, with limits that protect against
/// oversized or malformed files.
/// </summary>
internal static class ImageSources
{
    public const string FileDialogFilter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

    private const long MaxFileBytes = 50L * 1024 * 1024;
    private const int MaxDimension = 16384;

    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public static Bitmap LoadFromFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("The selected image no longer exists.", path);
        }

        if (!AllowedExtensions.Contains(info.Extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only PNG, JPEG and BMP images are supported.");
        }

        if (info.Length > MaxFileBytes)
        {
            throw new InvalidDataException("The image is larger than 50 MB.");
        }

        // Read into memory first so the file is not locked while the image is in use.
        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return Normalize(image);
    }

    public static Bitmap? LoadFromClipboard()
    {
        if (!Clipboard.ContainsImage())
        {
            return null;
        }

        using Image? image = Clipboard.GetImage();
        return image is null ? null : Normalize(image);
    }

    private static Bitmap Normalize(Image image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Width > MaxDimension || image.Height > MaxDimension)
        {
            throw new InvalidDataException($"Images must be between 1 and {MaxDimension} pixels on each side.");
        }

        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        return bitmap;
    }
}
