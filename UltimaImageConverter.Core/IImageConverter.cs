namespace UltimaImageConverter.Core;

public enum OutputFormat
{
    Png,
    Jpeg,
    Webp,
    Avif,
    Tiff,
    Bmp,
    Gif
}

public record ConversionOptions(OutputFormat Format, int Quality = 90, bool Overwrite = false);

public interface IImageConverter
{
    Task<bool> ConvertAsync(string inputPath, string outputPath, ConversionOptions options, CancellationToken cancellationToken = default);
}
