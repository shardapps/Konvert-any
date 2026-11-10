using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Konvert;

internal static class ImageConverter
{
    public const long MaxPixels = 24_000_000;

    public static bool CanDecode(string input)
    {
        try
        {
            using var source = File.OpenRead(input);
            var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
            return decoder.Frames.Count > 0 && decoder.Frames[0].PixelWidth > 0 && decoder.Frames[0].PixelHeight > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or ArgumentException or InvalidOperationException or FormatException or COMException)
        {
            return false;
        }
    }

    public static void Convert(string input, string output, string target, ConversionSettings settings, CancellationToken token)
    {
        using var source = File.OpenRead(input);
        // OnDemand reads frame metadata before decoding. Reject large dimensions before allocating pixels.
        var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        var frame = decoder is IconBitmapDecoder
            ? decoder.Frames.MaxBy(item => (long)item.PixelWidth * item.PixelHeight)!
            : decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > MaxPixels) throw new InvalidDataException("Images are limited to 24 megapixels in this alpha.");
        token.ThrowIfCancellationRequested();

        if (target == "ico")
        {
            WriteIcon(output, frame, token);
            return;
        }
        var scale = settings.MaxImageEdge == 0
            ? 1
            : Math.Min(1.0, (double)settings.MaxImageEdge / Math.Max(frame.PixelWidth, frame.PixelHeight));
        var width = Math.Max(1, (int)Math.Round(frame.PixelWidth * scale));
        var height = Math.Max(1, (int)Math.Round(frame.PixelHeight * scale));
        var normalized = Render(frame, width, height, settings.Dpi, white: target is "jpg" or "pdf");
        token.ThrowIfCancellationRequested();
        BitmapEncoder encoder = target switch
        {
            "jpg" or "pdf" => new JpegBitmapEncoder { QualityLevel = settings.JpegQuality },
            "png" => new PngBitmapEncoder(),
            "bmp" => new BmpBitmapEncoder(),
            "tiff" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip },
            "gif" => new GifBitmapEncoder(),
            _ => throw new NotSupportedException("Unsupported image output.")
        };
        encoder.Frames.Add(BitmapFrame.Create(normalized));
        if (target == "pdf")
        {
            using var jpeg = new MemoryStream(); encoder.Save(jpeg);
            WriteImagePdf(output, jpeg, normalized.PixelWidth, normalized.PixelHeight, settings.Dpi);
        }
        else { using var destination = File.Create(output); encoder.Save(destination); }
        token.ThrowIfCancellationRequested();
    }

    private static RenderTargetBitmap Render(BitmapSource frame, int width, int height, int dpi, bool white = false, bool square = false)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // WPF units are 1/96 inch. Match logical size to the requested DPI.
            var bounds = new Rect(0, 0, width * 96.0 / dpi, height * 96.0 / dpi);
            if (white) context.DrawRectangle(Brushes.White, null, bounds);
            if (square)
            {
                var scale = Math.Min(bounds.Width / frame.PixelWidth, bounds.Height / frame.PixelHeight);
                var imageWidth = frame.PixelWidth * scale;
                var imageHeight = frame.PixelHeight * scale;
                bounds = new Rect((bounds.Width - imageWidth) / 2, (bounds.Height - imageHeight) / 2, imageWidth, imageHeight);
            }
            context.DrawImage(frame, bounds);
        }
        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static void WriteIcon(string output, BitmapSource frame, CancellationToken token)
    {
        int[] sizes = [16, 32, 48, 64, 128, 256];
        using var file = File.Create(output);
        using var writer = new BinaryWriter(file);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)sizes.Length);
        const int entrySize = 16;
        var directory = file.Position;
        file.Position += entrySize * sizes.Length;
        for (var index = 0; index < sizes.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var size = sizes[index];
            var start = file.Position;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Render(frame, size, size, 96, square: true)));
            using var png = new MemoryStream();
            encoder.Save(png);
            png.WriteTo(file);
            var end = file.Position;
            file.Position = directory + entrySize * index;
            var dimension = (byte)(size == 256 ? 0 : size);
            writer.Write(dimension);
            writer.Write(dimension);
            writer.Write((ushort)0); // Palette count and reserved byte.
            writer.Write((ushort)1); // Color planes.
            writer.Write((ushort)32);
            writer.Write((uint)(end - start));
            writer.Write((uint)start);
            file.Position = end;
        }
    }

    private static void WriteImagePdf(string output, MemoryStream jpeg, int width, int height, int dpi)
    {
        using var stream = File.Create(output);
        var offsets = new List<long> { 0 };
        void Write(string value) => stream.Write(Encoding.ASCII.GetBytes(value));
        void Object(int id, string value) { offsets.Add(stream.Position); Write($"{id} 0 obj\n{value}\nendobj\n"); }
        var w = (width * 72.0 / dpi).ToString("0.###", CultureInfo.InvariantCulture);
        var h = (height * 72.0 / dpi).ToString("0.###", CultureInfo.InvariantCulture);
        Write("%PDF-1.4\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
        offsets.Add(stream.Position);
        Write($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n");
        jpeg.Position = 0; jpeg.CopyTo(stream); Write("\nendstream\nendobj\n");
        var commands = $"q {w} 0 0 {h} 0 0 cm /Im0 Do Q\n";
        Object(5, $"<< /Length {Encoding.ASCII.GetByteCount(commands)} >>\nstream\n{commands}endstream");
        var xref = stream.Position;
        Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
