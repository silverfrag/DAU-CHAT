using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DauChat.Client.UI.Infrastructure;

/// <summary>
/// Tạo lớp vi tinh thể kính nhám (crystal satin micro-frost) siêu nhẹ chạy trên GPU
/// giúp bề mặt kính có độ mịn cao cấp, không bị đục bụi và giữ trọn độ trong của màu sắc.
/// </summary>
internal static class FrostedTextureGenerator
{
    public static ImageBrush CreateFrostedGrainBrush(double opacity = 0.032, bool isDarkSurface = false)
    {
        const int size = 128;
        const int stride = size * 4;
        var pixels = new byte[size * stride];

        uint seed = isDarkSurface ? 0x9E37_79B9u : 0x5EED_DA05u;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;

            var v = (byte)(seed & 0xFF);
            if (isDarkSurface)
            {
                // Hạt sáng lạnh Arctic Ice (#E8EFF3) siêu mịn trên nền kính xanh đậm
                var alpha = (byte)(v > 140 ? (v - 140) * 2 : 0);
                pixels[i] = 243;     // B
                pixels[i + 1] = 239; // G
                pixels[i + 2] = 232; // R
                pixels[i + 3] = alpha;
            }
            else
            {
                // Vi tinh thể sáng (trắng băng) xen điểm sâu xanh lam cực mảnh — không dùng đen đục
                if (v > 148)
                {
                    pixels[i] = 255;
                    pixels[i + 1] = 255;
                    pixels[i + 2] = 255;
                    pixels[i + 3] = (byte)((v - 148) * 2);
                }
                else if (v < 48)
                {
                    pixels[i] = 107; // #2E4F6B (Arctic Harbor Blue)
                    pixels[i + 1] = 79;
                    pixels[i + 2] = 46;
                    pixels[i + 3] = (byte)((48 - v) * 2);
                }
            }
        }

        var bitmap = BitmapSource.Create(
            size,
            size,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        bitmap.Freeze();

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }
}
