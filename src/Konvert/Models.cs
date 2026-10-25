global using System.IO;

namespace Konvert;

public sealed record ConversionTarget(string Extension, string Label, string Detail, string Engine);
public sealed record ConversionOption(ConversionTarget Target, bool Available);

public sealed record ConversionSettings
{
    public int JpegQuality { get; init; } = 90;
    public int Dpi { get; init; } = 144;
    public string? OutputDirectory { get; init; }
    public string? LibreOfficePath { get; init; }
    public string? FfmpegPath { get; init; }
    public bool ReducedMotion { get; init; }
    public string Theme { get; init; } = "glass";

    public ConversionSettings Validate() => this with
    {
        JpegQuality = Math.Clamp(JpegQuality, 10, 100),
        Dpi = Math.Clamp(Dpi, 72, 600),
        Theme = ThemeManager.Themes.Any(theme => theme.Id == Theme) ? Theme : "glass",
        OutputDirectory = string.IsNullOrWhiteSpace(OutputDirectory) ? null : Path.GetFullPath(OutputDirectory)
    };
}

public sealed record ConversionResult(string OutputPath, TimeSpan Elapsed, long OutputBytes);
