using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Foxnet.Desktop.Services;

internal static class AvatarImages
{
    public static byte[] Import(string fileName, int sizeLimit)
    {
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is 0 || stream.Length > sizeLimit)
            throw new InvalidDataException("Выберите изображение размером до 8 МБ.");

        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        if (decoder is not (PngBitmapDecoder or JpegBitmapDecoder or BmpBitmapDecoder or GifBitmapDecoder or TiffBitmapDecoder))
            throw new InvalidDataException("Поддерживаются изображения PNG, JPEG, BMP, GIF и TIFF.");

        var first = decoder.Frames[0];
        if (first.PixelWidth is < 1 or > 4096 || first.PixelHeight is < 1 or > 4096)
            throw new InvalidDataException("Ширина и высота аватара должны быть не больше 4096 пикселей.");

        var scale = Math.Min(1, 320.0 / Math.Max(first.PixelWidth, first.PixelHeight));
        BitmapSource frame = scale < 1 ? new TransformedBitmap(first, new ScaleTransform(scale, scale)) : first;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    public static ImageSource? Load(byte[]? content)
    {
        if (content == null)
            return null;

        using var stream = new MemoryStream(content, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
