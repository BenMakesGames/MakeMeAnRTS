using MakeMeAnRTS.Cli;

// Commands other than "play" exist to inspect the game from a terminal, with no display attached.
const string Usage = """
    MakeMeAnRTS

      play                       Play the game in a window (the default).
        --seed N                 Map seed. Omitted means a random map.
        --width N --height N     Map size in tiles.

      map-preview                Render a generated map to an image, without opening a window.
        --seed N                 Map seed.
        --width N --height N     Map size in tiles.
        --scale N                Pixels per tile in the output image.
        --rivers N               How many rivers to source.
        --forest F               Forest density, 0..1.
        --water F                Water level, 0..1.
        --overlay NAME           Draw a mineral heat map: stone, iron or gold.
        --tiles N                Zoom in on N tiles across instead of the whole map.
        --center-x N --center-y N  Where to centre a zoomed preview. Defaults to player 1's start.
        --out PATH               Output .bmp path.

      simulate                   Run a match with no window and print what each side owns.
        --seed N                 Map seed.
        --seconds N              How much match time to simulate.
        --report N               Seconds between reports.
        --cpu-vs-cpu             Let the CPU play both sides, for a full self-play match.
        --verbose                List every unit's tile, order and load in each report.

      font-sample                Render the built-in font's charset to an image.
        --out PATH               Output .bmp path.

    Convert any .bmp output to .png with: tools/bmp2png.py FILE
    """;

try
{
    var parsed = new CommandLineArgs(args);

    return (parsed.Command ?? "play") switch
    {
        "play" => PlayCommand.Run(parsed),
        "map-preview" => MapPreviewCommand.Run(parsed),
        "simulate" => SimulateCommand.Run(parsed),
        "font-sample" => FontSampleCommand.Run(parsed),
        "help" or "--help" => Print(Usage, 0),
        var unknown => Print($"Unknown command '{unknown}'.{Environment.NewLine}{Environment.NewLine}{Usage}", 2),
    };
}
catch (CommandLineException error)
{
    return Print($"{error.Message}{Environment.NewLine}{Environment.NewLine}{Usage}", 2);
}

static int Print(string message, int exitCode)
{
    var destination = exitCode == 0 ? Console.Out : Console.Error;
    destination.WriteLine(message);

    return exitCode;
}
