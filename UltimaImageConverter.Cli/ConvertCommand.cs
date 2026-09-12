using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using UltimaImageConverter.Core;

namespace UltimaImageConverter.Cli;

public class ConvertSettings : CommandSettings
{
    [CommandArgument(0, "<INPUT_PATH>")]
    [Description("The path to the input image file or directory.")]
    public string InputPath { get; set; } = string.Empty;

    [CommandOption("-f|--format")]
    [Description("The target format (supported: png, jpeg, webp, avif, tiff, bmp, gif).")]
    [DefaultValue(OutputFormat.Png)]
    public OutputFormat Format { get; set; }

    [CommandOption("-q|--quality")]
    [Description("Image quality (1-100).")]
    [DefaultValue(90)]
    public int Quality { get; set; }

    [CommandOption("-s|--silent")]
    [Description("Suppress all console output (useful for scripts).")]
    [DefaultValue(false)]
    public bool Silent { get; set; }

    [CommandOption("-l|--log")]
    [Description("Generate a conversion_log.txt file in the uic install directory.")]
    [DefaultValue(false)]
    public bool Log { get; set; }
}

public class ConvertCommand : AsyncCommand<ConvertSettings>
{
    private readonly IImageConverter _converter;

    // Dependency injection automatically provides the converter here
    public ConvertCommand(IImageConverter converter)
    {
        _converter = converter;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, ConvertSettings settings, CancellationToken token)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

        ConsoleCancelEventHandler cancelHandler = (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            var isDirectory = Directory.Exists(settings.InputPath);
            var isFile = File.Exists(settings.InputPath);

            if (!isDirectory && !isFile)
            {
                if (!settings.Silent) AnsiConsole.MarkupLine("[red]Error:[/] The specified input path does not exist.");
                return 1;
            }

            // Get single file or filter directory by image extensions
            var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".heic", ".heif", ".avif", ".webp", ".jpg", ".jpeg", ".png", ".tiff", ".bmp"
            };

            var filesToProcess = isDirectory ? Directory.GetFiles(settings.InputPath)
                .Where(f => validExtensions.Contains(Path.GetExtension(f)))
                .ToList()
                : new List<string> { settings.InputPath };

            if (filesToProcess.Count <= 0)
            {
                if (!settings.Silent) AnsiConsole.MarkupLine("[yellow]No supported images found to convert.[/]");
                return 0;
            }

            if (!settings.Silent)
                AnsiConsole.MarkupLine($"[cyan]Converting {filesToProcess.Count} image(s) to {settings.Format}... Ctrl-C to abort the operation.[/]");

            var options = new ConversionOptions(settings.Format, settings.Quality);
            var successCount = 0;

            // Initialize the logger if requested
            StreamWriter? logWriter = null;

            if (settings.Log)
            {
                var targetDir = isDirectory ? settings.InputPath : Path.GetDirectoryName(settings.InputPath)!;
                var logPath = Path.Combine(AppContext.BaseDirectory, "conversion_log.txt");

                logWriter = new StreamWriter(logPath, append: false);

                await logWriter.WriteLineAsync($"--- UIC Conversion Log ---");
                await logWriter.WriteLineAsync($"Started at: {DateTime.Now}");
                await logWriter.WriteLineAsync($"Directory/Folder: {targetDir}");
                await logWriter.WriteLineAsync($"Target Format: {settings.Format}");
                await logWriter.WriteLineAsync($"---------------------------\n");
            }

            // Ensure the logger flushes and closes properly upon exit of the method
            await using (logWriter)
            {
                // Was originally going to be multithreaded but decided to keep it simple.
                async Task RunBatchAsync(ProgressTask? task = null)
                {
                    foreach (var file in filesToProcess)
                    {
                        if (cts.Token.IsCancellationRequested) break;

                        var outputFilename = Path.ChangeExtension(file, settings.Format.ToString().ToLowerInvariant());

                        try
                        {
                            var success = await _converter.ConvertAsync(file, outputFilename, options, cts.Token);

                            if (success) 
                            { 
                                successCount += 1;
                                if (logWriter is not null) await logWriter.WriteLineAsync($"[SUCCESS] {Path.GetFileName(file)} -> {Path.GetFileName(outputFilename)}");
                            }
                            else
                            {
                                if (logWriter is not null) await logWriter.WriteLineAsync($"[FAILED]  {Path.GetFileName(file)}");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            if (logWriter is not null) await logWriter.WriteLineAsync($"\n[ABORTED] Operation cancelled by user.");
                            break; // Caught Ctrl-C mid-file
                        }

                        task?.Increment(1);
                    }
                }

                if (settings.Silent)
                {
                    await RunBatchAsync();
                }
                else
                {
                    await AnsiConsole.Progress()
                        .StartAsync(async ctx =>
                        {
                            var task = ctx.AddTask($"[green]Processing[/]", maxValue: filesToProcess.Count);
                            await RunBatchAsync(task);
                        });
                }

                if (logWriter is not null)
                {
                    await logWriter.WriteLineAsync("\n---------------------------");
                    await logWriter.WriteLineAsync($"Finished: {successCount}/{filesToProcess.Count} converted successfully.");
                }
            }   

            if (cts.Token.IsCancellationRequested)
            {
                if (!settings.Silent) AnsiConsole.MarkupLine($"\n[yellow]Aborted![/] Converted {successCount}/{filesToProcess.Count} files before stopping.");
                return 130; // POSIX Standard Code for Ctrl-C termination. See: https://www.ditig.com/linux-exit-status-codes
            }

            if (!settings.Silent) AnsiConsole.MarkupLine($"[green]Done![/] Successfully converted {successCount}/{filesToProcess.Count} files.");
            return successCount == filesToProcess.Count ? 0 : 1;
        }
        finally
        {
            // Clean up event handler to prevent memory leaks.
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
