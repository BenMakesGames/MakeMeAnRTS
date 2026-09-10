using SDL3;
using MakeMeAnRTS.Core;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.World;
using MakeMeAnRTS.Features.World.Generation;

namespace MakeMeAnRTS.Cli;

/// <summary>
/// Renders a whole generated map to an image without opening a window.
/// </summary>
/// <remarks>
/// This is the main tool for judging world generation. Tweaking a threshold and eyeballing twenty seeds
/// catches things - rivers that never reach water, forests that swallow the map - that no assertion
/// would have thought to check.
/// </remarks>
public static class MapPreviewCommand
{
    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", 1),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
            RiverCount = args.Int("rivers", 10),
            ForestDensity = args.Float("forest", 0.45f),
            WaterLevel = args.Float("water", 0.30f),
        };

        var outputPath = args.String("out", $"screenshots/map-{settings.Seed}.bmp");
        var pixelsPerTile = args.Int("scale", 4);
        var overlay = args.Enum<MineralKind>("overlay");

        // --tiles zooms in on part of the map, which is how tree, bridge and unit art gets checked at
        // the size a player actually sees it.
        var visibleTiles = args.Int("tiles", 0);

        var map = WorldGenerator.Generate(settings);

        var tilesAcross = visibleTiles > 0 ? Math.Min(visibleTiles, map.Width) : map.Width;
        var width = tilesAcross * pixelsPerTile;
        var height = tilesAcross * pixelsPerTile;

        using var platform = Platform.Create("Map preview", width, height, PlatformMode.Offscreen);
        using var font = BitmapFont.Create(platform.Renderer);

        var renderer = new Renderer2D(platform.Renderer, font);
        var camera = new Camera2D(map.Width, map.Height, new SDL.Rect { X = 0, Y = 0, W = width, H = height });
        camera.FitToMap();

        if (visibleTiles > 0)
        {
            camera.SetZoom(pixelsPerTile);
            camera.CenterOn(new Vec2(args.Float("center-x", map.StartPositions[0].X), args.Float("center-y", map.StartPositions[0].Y)));
        }

        var worldRenderer = new WorldRenderer(map);

        renderer.Clear(Palette.Rgb(12, 14, 18));
        worldRenderer.DrawTerrain(renderer, camera);

        if (overlay is not null)
            worldRenderer.DrawMineralHeatMap(renderer, camera, overlay.Value, map.MineralMap(overlay.Value));

        DrawStartPositions(renderer, camera, map);
        DrawCaption(renderer, map, settings, overlay);

        Screenshot.Save(platform.Renderer, outputPath);

        Console.WriteLine($"Wrote {outputPath} ({width}x{height}) for seed {map.Seed}.");
        Console.WriteLine(MapStatistics.Summarize(map));
        Console.WriteLine($"rivers: {string.Join(", ", WorldGenerator.LastRiverOutcomes.GroupBy(outcome => outcome).Select(group => $"{group.Key}={group.Count()}"))}");

        return 0;
    }

    private static void DrawStartPositions(Renderer2D renderer, Camera2D camera, WorldMap map)
    {
        for (var player = 0; player < map.StartPositions.Count; player++)
        {
            var screen = camera.WorldToScreen(map.StartPositions[player].Center);
            var color = player == 0 ? Palette.Rgb(90, 170, 255) : Palette.Rgb(230, 90, 90);

            renderer.DrawRectThick(screen.X - 8, screen.Y - 8, 16, 16, color, 2);
            renderer.DrawTextShadowed($"P{player + 1}", screen.X + 10, screen.Y - 3, color);
        }
    }

    private static void DrawCaption(Renderer2D renderer, WorldMap map, WorldGenSettings settings, MineralKind? overlay)
    {
        var caption = $"seed {map.Seed}  {map.Width}x{map.Height}  rivers {settings.RiverCount}";
        if (overlay is not null)
            caption += $"  overlay {overlay.Value.DisplayName()}";

        renderer.FillRect(0, 0, renderer.Font.MeasureWidth(caption) + 8, 13, Palette.Rgba(0, 0, 0, 170));
        renderer.DrawText(caption, 4, 4, Palette.White);
    }
}
