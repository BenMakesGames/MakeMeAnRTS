using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.World.Generation;
using MakeMeAnRTS.Game;

namespace MakeMeAnRTS.Cli;

/// <summary>Starts a normal game in a window.</summary>
public static class PlayCommand
{
    private const int DefaultWindowWidth = 1280;
    private const int DefaultWindowHeight = 800;

    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", Random.Shared.Next(1, int.MaxValue)),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
        };

        var windowWidth = args.Int("window-width", DefaultWindowWidth);
        var windowHeight = args.Int("window-height", DefaultWindowHeight);

        using var platform = Platform.Create("MakeMeAnRTS", windowWidth, windowHeight, PlatformMode.Windowed);
        using var font = BitmapFont.Create(platform.Renderer);

        using var audio = AudioDevice.Open();

        var renderer = new Renderer2D(platform.Renderer, font);
        using var screen = new GameScreen(settings, windowWidth, windowHeight, audio, renderer);

        if (!audio.IsAvailable)
            Console.WriteLine("No audio device available; playing without sound.");

        Console.WriteLine($"Map seed {screen.State.Map.Seed}. Replay it with: play --seed {screen.State.Map.Seed}");

        new GameLoop(platform, renderer, screen).Run();

        return 0;
    }
}
