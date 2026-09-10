using SDL3;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Prospecting;
using MakeMeAnRTS.Features.Vision;

namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// One side in a match: its stockpile, what it has surveyed, and what it can see.
/// </summary>
/// <remarks>
/// There are no factions, so a player carries no rules of its own - only state. The human player and
/// the CPU differ solely in who issues their orders.
/// </remarks>
public sealed class Player
{
    public int Index { get; }
    public string Name { get; }
    public SDL.Color Color { get; }
    public bool IsHuman { get; }

    public ResourceStore Resources { get; } = new();
    public MineralKnowledge Knowledge { get; }
    public PlayerVision Vision { get; }

    /// <summary>Set once a player has lost every building. Defeated players are left on the map, inert.</summary>
    public bool IsDefeated { get; set; }

    public Player(int index, string name, SDL.Color color, bool isHuman, int mapWidth, int mapHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Index = index;
        Name = name;
        Color = color;
        IsHuman = isHuman;
        Knowledge = new MineralKnowledge(mapWidth, mapHeight);
        Vision = new PlayerVision(mapWidth, mapHeight);
    }

    /// <summary>Player colours, indexed by player number. Blue for the human, red for the opponent.</summary>
    public static readonly SDL.Color[] Colors =
    [
        Palette.Rgb(86, 156, 240),
        Palette.Rgb(226, 88, 78),
        Palette.Rgb(120, 208, 110),
        Palette.Rgb(224, 190, 82),
    ];
}
