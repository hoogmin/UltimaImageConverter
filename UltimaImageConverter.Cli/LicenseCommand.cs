using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace UltimaImageConverter.Cli;

[Description("Displays license, author, and support information.")]
public class LicenseCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken ct)
    {
        AnsiConsole.Write(new Rule("[cyan]Ultima Image Converter (UIC)[/]").LeftJustified());

        AnsiConsole.MarkupLine("[bold]Author:[/] Javier Martinez / @hoogmin");
        AnsiConsole.MarkupLine("[bold]License:[/] BSD-3-Clause\n");

        AnsiConsole.MarkupLine("This tool is fully open-source. If it saves you time,");
        AnsiConsole.MarkupLine("consider supporting its continued development.");

        AnsiConsole.Write(new Rule("[grey]Third-Party Licenses[/]").LeftJustified());
        AnsiConsole.MarkupLine("[grey]- Spectre.Console (MIT)[/]");
        AnsiConsole.MarkupLine("[grey]- Magick.NET (Apache-2.0)[/]");
        AnsiConsole.MarkupLine("[grey]- ImageMagick (ImageMagick License)[/]");

        return 0;
    }
}
