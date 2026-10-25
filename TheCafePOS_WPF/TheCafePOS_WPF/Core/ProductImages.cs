using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace TheCafePOS_WPF.Core;

// Product photos are optional files next to the executable; a missing or broken file simply shows no photo.
public static class ProductImages
{
    public static BitmapImage? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
            if (!File.Exists(fullPath)) return null;
            using var stream = File.OpenRead(fullPath);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 400;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException)
        {
            return null; // A missing or invalid photo must not prevent sales.
        }
    }
}
