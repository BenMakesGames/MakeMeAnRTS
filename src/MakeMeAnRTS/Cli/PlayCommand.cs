using MakeMeAnRTS.Features.World.Generation;

namespace MakeMeAnRTS.Cli;

/// <summary>Starts a normal game in a window.</summary>
public static class PlayCommand
{
    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", Random.Shared.Next(1, int.MaxValue)),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
        };

        // TODO: hand off to the game loop once units, buildings and the HUD exist.
        var map = WorldGenerator.Generate(settings);
        Console.WriteLine($"Generated map seed {map.Seed} ({map.Width}x{map.Height}). The playable game is not wired up yet.");

        return 0;
    }
}
