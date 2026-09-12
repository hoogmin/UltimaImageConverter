using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using UltimaImageConverter.Cli;
using UltimaImageConverter.Core;

// Setup dependency injection
var services = new ServiceCollection();
services.AddSingleton<IImageConverter, MagickImageConverter>();

var registrar = new TypeRegistrar(services);

// Configure the CLI app
var app = new CommandApp<ConvertCommand>(registrar);

app.Configure(config =>
{
    config.SetApplicationName("uic");
    config.SetApplicationVersion("1.0.0");

    // Add examples that will appear at the bottom of the help screen
    config.AddExample(["\"C:\\images\\photo.heic\"", "-f", "jpeg", "-q", "85"]);
    config.AddExample(["\"C:\\images\"", "-f", "webp", "-s", "--log"]);

    // Register our subcommands
    config.AddCommand<LicenseCommand>("license");
});

return app.Run(args);