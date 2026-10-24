using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Konvert;

internal static class ImageConverter
{
    public const long MaxPixels = 24_000_000;

    public static void Convert(string input, string output, string target, ConversionSettings settings, CancellationToken token)
    {
        using var source = File.OpenRead(input);
        // OnDemand reads frame metadata before decoding. Reject large dimensions before allocating pixels.
        var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > MaxPixels) throw new InvalidDataException("Images are limited to 24 megapixels in this alpha.");
        token.ThrowIfCancellationRequested();
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            // WPF units are 1/96 inch. Match logical size to requested DPI to retain pixel dimensions.
            var bounds = new Rect(0, 0, frame.PixelWidth * 96.0 / settings.Dpi, frame.PixelHeight * 96.0 / settings.Dpi);
            if (target is "jpg" or "pdf")
                context.DrawRectangle(Brushes.White, null, bounds);
            context.DrawImage(frame, bounds);
        }
        // Render once, avoiding a large managed pixel buffer and a second BitmapSource copy.
        var normalized = new RenderTargetBitmap(frame.PixelWidth, frame.PixelHeight, settings.Dpi, settings.Dpi, PixelFormats.Pbgra32);
        normalized.Render(visual);
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
