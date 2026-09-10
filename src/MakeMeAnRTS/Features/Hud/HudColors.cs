using SDL3;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Hud;

/// <summary>Colours shared by the HUD and by anything drawn over the world, so the two always agree.</summary>
public static class HudColors
{
    public static SDL.Color Resource(ResourceKind resource) => resource switch
    {
        ResourceKind.Wood => Palette.Rgb(146, 104, 62),
        ResourceKind.Stone => Palette.Rgb(196, 196, 206),
        ResourceKind.Iron => Palette.Rgb(206, 122, 88),
        ResourceKind.Gold => Palette.Rgb(240, 200, 70),
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unhandled resource kind."),
    };

    /// <summary>Green when healthy, sliding through amber to red as a thing gets closer to dying.</summary>
    public static SDL.Color HealthBar(float fraction) => fraction switch
    {
        > 0.6f => Palette.Rgb(96, 200, 96),
        > 0.3f => Palette.Rgb(226, 190, 70),
        _ => Palette.Rgb(220, 80, 70),
    };

    public static readonly SDL.Color PanelBackground = Palette.Rgba(18, 20, 26, 232);
    public static readonly SDL.Color PanelBorder = Palette.Rgb(64, 70, 84);
    public static readonly SDL.Color Text = Palette.Rgb(226, 230, 238);
    public static readonly SDL.Color DimText = Palette.Rgb(146, 154, 170);
    public static readonly SDL.Color Warning = Palette.Rgb(238, 150, 80);
    public static readonly SDL.Color Good = Palette.Rgb(130, 210, 130);
    public static readonly SDL.Color Accent = Palette.Rgb(112, 176, 232);
}
