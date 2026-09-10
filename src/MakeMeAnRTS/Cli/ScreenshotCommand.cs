using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Ai;
using MakeMeAnRTS.Features.World;
using MakeMeAnRTS.Features.World.Generation;
using MakeMeAnRTS.Game;

namespace MakeMeAnRTS.Cli;

/// <summary>
/// Plays a match forward for a while with nobody watching, then saves a picture of the real game view.
/// </summary>
/// <remarks>
/// The same <see cref="GameScreen"/> a player would see, drawn through an offscreen renderer. That is
/// what makes the picture worth anything: it is not a diagram of the game, it is the game. Being able
/// to look at minute twelve of a match without sitting through eleven of them is the difference between
/// checking the HUD once and checking it every time it changes.
/// </remarks>
public static class ScreenshotCommand
{
    /// <summary>Fixed step used to fast-forward. Matches the simulation rate the real loop settles at.</summary>
    private const float StepSeconds = 1f / 20f;

    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", 1),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
        };

        var windowWidth = args.Int("window-width", 1280);
        var windowHeight = args.Int("window-height", 800);
        var fastForward = args.Float("seconds", 0f);
        var zoom = args.Float("zoom", 0f);
        var outputPath = args.String("out", $"screenshots/game-{settings.Seed}.bmp");

        using var platform = Platform.Create("MakeMeAnRTS", windowWidth, windowHeight, PlatformMode.Offscreen);
        using var font = BitmapFont.Create(platform.Renderer);

        var renderer = new Renderer2D(platform.Renderer, font);
        var screen = new GameScreen(settings, windowWidth, windowHeight) { RevealEverything = args.HasFlag("reveal") };

        // The human seat has nobody at it here, so give it a CPU too; otherwise the picture is of four
        // citizens chopping wood forever while the opponent builds an empire, which shows nothing.
        var commanders = args.HasFlag("no-autoplay")
            ? []
            : screen.State.Players
                .Where(player => player.IsHuman)
                .Select(player => new CpuCommander(screen.State, player.Index, CpuPlan.Standard, seed: settings.Seed + 500))
                .ToList();

        var steps = (int)(fastForward / StepSeconds);
        var input = new InputState();

        for (var step = 0; step < steps; step++)
        {
            foreach (var commander in commanders)
                commander.Update(StepSeconds);

            screen.Update(input, StepSeconds);
        }

        if (zoom > 0f)
            screen.Camera.SetZoom(zoom);

        if (args.Enum<MineralKind>("overlay") is { } overlay)
            CycleOverlayTo(screen, overlay);

        if (args.HasFlag("debug"))
            PressDebugOverlay(screen, input);

        if (args.HasFlag("select-all"))
            SelectEverything(screen);

        screen.Draw(renderer);
        Screenshot.Save(platform.Renderer, outputPath);

        Console.WriteLine($"Wrote {outputPath} at t={screen.State.ElapsedSeconds:0}s of seed {screen.State.Map.Seed}.");

        return 0;
    }

    /// <summary>Presses Tab until the wanted heat map is showing, driving the real input path.</summary>
    private static void CycleOverlayTo(GameScreen screen, MineralKind wanted)
    {
        for (var attempt = 0; attempt < 4 && screen.Controller.MineralOverlay != wanted; attempt++)
            Press(screen, SDL3.SDL.Scancode.Tab);
    }

    private static void PressDebugOverlay(GameScreen screen, InputState input) => Press(screen, SDL3.SDL.Scancode.F1);

    /// <summary>Selects the human player's units, to show the selection panel and build menu populated.</summary>
    private static void SelectEverything(GameScreen screen)
    {
        var units = screen.State.UnitsOf(0).ToList();
        if (units.Count > 0)
            screen.Controller.Selection.SelectUnits(units, add: false);
    }

    private static void Press(GameScreen screen, SDL3.SDL.Scancode key)
    {
        var input = new InputState();
        input.InjectKeyPress(key);

        // A zero-length update, so the keypress is handled without the match moving on.
        screen.Update(input, 0f);
    }
}
