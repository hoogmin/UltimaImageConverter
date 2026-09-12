using ImageMagick;

namespace UltimaImageConverter.Core;

public class MagickImageConverter : IImageConverter
{
    static MagickImageConverter()
    {
        // Force C++ native libraries to load safely on a single thread
        MagickNET.Initialize();
    }

    public async Task<bool> ConvertAsync(string inputPath, string outputPath, ConversionOptions options, CancellationToken ct = default)
    {
        if (!File.Exists(inputPath)) return false;

        await Task.Run(() =>
        {
            // Abort if cancelled before we do the heavy lifting
            ct.ThrowIfCancellationRequested();

            using var image = new MagickImage(inputPath);
            image.AutoOrient();
            image.Quality = (uint)options.Quality;

            // Map the enum to MagickFormat
            image.Format = Enum.Parse<MagickFormat>(options.Format.ToString(), true);

            image.Write(outputPath);
        }, ct);

        return true;
    }
}
