using System.Diagnostics;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Ai;
using MakeMeAnRTS.Features.World.Generation;
using MakeMeAnRTS.Game;

namespace MakeMeAnRTS.Cli;

/// <summary>
/// Times the simulation and the renderer separately, on a match fast-forwarded to a chosen point.
/// </summary>
/// <remarks>
/// Frame cost is invisible in a screenshot and easy to guess wrong about, and the interesting case is
/// never the opening minute - it is twenty minutes in with three hundred units on the map. This runs
/// that state headlessly and reports where the time actually goes.
/// </remarks>
public static class BenchmarkCommand
{
    private const float StepSeconds = 1f / 20f;

    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", 1),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
        };

        var fastForward = args.Float("seconds", 600f);
        var frames = args.Int("frames", 120);

        using var platform = Platform.Create("Benchmark", 1280, 800, PlatformMode.Offscreen);
        using var font = BitmapFont.Create(platform.Renderer);
        using var audio = AudioDevice.Open();

        var renderer = new Renderer2D(platform.Renderer, font);
        using var screen = new GameScreen(settings, 1280, 800, audio, renderer);

        var commanders = screen.State.Players
            .Where(player => player.IsHuman)
            .Select(player => new CpuCommander(screen.State, player.Index, CpuPlan.Standard, seed: settings.Seed + 500))
            .ToList();

        var input = new InputState();

        for (var step = 0; step < (int)(fastForward / StepSeconds); step++)
        {
            foreach (var commander in commanders)
                commander.Update(StepSeconds);

            screen.Update(input, StepSeconds);
        }

        Console.WriteLine($"Seed {screen.State.Map.Seed} at t={screen.State.ElapsedSeconds:0}s: " +
                          $"{screen.State.Units.Count} units, {screen.State.Buildings.Count} buildings, {screen.State.Signs.Count} signs.");

        var updateMs = TimeAverage(frames, () => screen.Update(input, 1f / 60f));
        var drawMs = TimeAverage(frames, () => screen.Draw(renderer));

        Console.WriteLine($"update {updateMs:0.00} ms/frame");
        Console.WriteLine($"draw   {drawMs:0.00} ms/frame");
        Console.WriteLine($"total  {updateMs + drawMs:0.00} ms/frame  ({1000f / MathF.Max(0.001f, updateMs + drawMs):0} fps)");

        // The software renderer used offscreen is slower than the GPU one a player gets, so this is a
        // pessimistic figure rather than an optimistic one.
        Console.WriteLine("(offscreen software renderer; a windowed game uses the GPU and draws faster)");

        return 0;
    }

    private static float TimeAverage(int frames, Action work)
    {
        // One untimed pass first, so first-call JIT and lazy allocation do not land in the average.
        work();

        var stopwatch = Stopwatch.StartNew();

        for (var frame = 0; frame < frames; frame++)
            work();

        return (float)stopwatch.Elapsed.TotalMilliseconds / frames;
    }
}
