using MakeMeAnRTS.Engine;

namespace MakeMeAnRTS.Cli;

/// <summary>Renders the whole printable charset to an image, to check the in-code font by eye.</summary>
public static class FontSampleCommand
{
    public static int Run(CommandLineArgs args)
    {
        var outputPath = args.String("out", "screenshots/font-sample.bmp");

        using var platform = Platform.Create("Font sample", 480, 200, PlatformMode.Offscreen);
        using var font = BitmapFont.Create(platform.Renderer);

        var renderer = new Renderer2D(platform.Renderer, font);
        renderer.Clear(Palette.Rgb(24, 28, 36));

        string[] lines =
        [
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "abcdefghijklmnopqrstuvwxyz",
            "0123456789 !\"#$%&'()*+,-./",
            ":;<=>?@[\\]^_`{|}~",
        ];

        for (var line = 0; line < lines.Length; line++)
            renderer.DrawText(lines[line], 8, 8 + line * 16, Palette.White, 2);

        renderer.DrawText("scale 1: The quick brown fox jumps over the lazy dog.", 8, 84, Palette.Rgb(180, 220, 160));
        renderer.DrawText("Wood 120  Stone 45  Iron 8  Gold 0", 8, 100, Palette.Rgb(240, 200, 90), 2);
        renderer.DrawTextShadowed("shadowed label over terrain", 8, 130, Palette.White, 2);

        Screenshot.Save(platform.Renderer, outputPath);
        Console.WriteLine($"Wrote {outputPath}.");

        return 0;
    }
}
