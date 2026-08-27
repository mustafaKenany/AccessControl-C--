using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AccessControlPro.WPF.Converters;

/// <summary>
/// Binds a member's JPEG <c>byte[]</c> (PhotoData) straight to an Image/ImageBrush Source.
/// Returns null for empty/invalid data so the XAML fallback (a User icon) shows instead.
/// The bitmap is decoded once, frozen, and cached-on-load so it survives the stream being
/// disposed and is safe to hand to the UI thread. Only used for the SMALL "expiring soon"
/// list (a handful of photos) — never for the whole roster (that path stays photo-free to
/// avoid the 32-bit OOM leak).
/// </summary>
public class BytesToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            return null;

        try
        {
            var image = new BitmapImage();
            using var ms = new MemoryStream(bytes);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            // Decode small — these are thumbnail avatars, no need for full-res in memory.
            image.DecodePixelWidth = 120;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null; // corrupt/partial image data — fall back to the icon
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
