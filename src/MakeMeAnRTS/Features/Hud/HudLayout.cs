using SDL3;

namespace MakeMeAnRTS.Features.Hud;

/// <summary>
/// Where every panel sits for a given window size, and what is left over for the world.
/// </summary>
/// <remarks>
/// Computed in one place each frame rather than scattered through drawing code, because the camera,
/// the mouse hit-testing and the renderer all have to agree on exactly where the world view ends. When
/// they disagree, clicks land somewhere other than where the player aimed.
/// </remarks>
public readonly record struct HudLayout
{
    public SDL.Rect TopBar { get; private init; }
    public SDL.Rect BottomBar { get; private init; }
    public SDL.Rect Minimap { get; private init; }
    public SDL.Rect SelectionPanel { get; private init; }
    public SDL.Rect CommandPanel { get; private init; }

    /// <summary>The part of the window the world is drawn into.</summary>
    public SDL.Rect Viewport { get; private init; }

    public const int TopBarHeight = 26;
    public const int BottomBarHeight = 132;
    public const int Padding = 6;

    public static HudLayout For(int windowWidth, int windowHeight)
    {
        var bottomTop = windowHeight - BottomBarHeight;
        var minimapSize = BottomBarHeight - Padding * 2;
        var commandWidth = Math.Min(300, windowWidth / 3);

        var selectionX = Padding * 2 + minimapSize;
        var selectionWidth = Math.Max(80, windowWidth - selectionX - commandWidth - Padding * 2);

        return new HudLayout
        {
            TopBar = new SDL.Rect { X = 0, Y = 0, W = windowWidth, H = TopBarHeight },
            BottomBar = new SDL.Rect { X = 0, Y = bottomTop, W = windowWidth, H = BottomBarHeight },
            Minimap = new SDL.Rect { X = Padding, Y = bottomTop + Padding, W = minimapSize, H = minimapSize },
            SelectionPanel = new SDL.Rect { X = selectionX, Y = bottomTop + Padding, W = selectionWidth, H = minimapSize },
            CommandPanel = new SDL.Rect { X = windowWidth - commandWidth - Padding, Y = bottomTop + Padding, W = commandWidth, H = minimapSize },

            // The world fills the gap between the bars; panels are opaque and drawn over it.
            Viewport = new SDL.Rect { X = 0, Y = TopBarHeight, W = windowWidth, H = Math.Max(1, bottomTop - TopBarHeight) },
        };
    }
}
