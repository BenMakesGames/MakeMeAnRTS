using MakeMeAnRTS.Engine;

using var platform = Platform.Create("MakeMeAnRTS - font sample", 480, 200, PlatformMode.Offscreen);
using var font = BitmapFont.Create(platform.Renderer);

var renderer = new Renderer2D(platform.Renderer, font);
renderer.Clear(Palette.Rgb(24, 28, 36));

var lines = new[]
{
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ",
    "abcdefghijklmnopqrstuvwxyz",
    "0123456789 !\"#$%&'()*+,-./",
    ":;<=>?@[\\]^_`{|}~",
    "Wood 120  Stone 45  Iron 8",
};

for (var i = 0; i < lines.Length; i++)
    renderer.DrawText(lines[i], 8, 8 + i * 16, Palette.White, 2);

renderer.DrawText("scale 1: The quick brown fox jumps over the lazy dog.", 8, 100, Palette.Rgb(180, 220, 160));
renderer.DrawText("scale 3: 1234", 8, 120, Palette.Rgb(240, 200, 90), 3);

Screenshot.Save(platform.Renderer, "/tmp/claude-0/font-sample.bmp");
Console.WriteLine("wrote /tmp/claude-0/font-sample.bmp");
