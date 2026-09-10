using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.PlayerControl;

/// <summary>
/// The "where should this go?" state between choosing a building and clicking to place it.
/// </summary>
/// <remarks>
/// Holds the reason a spot is refused, not just whether it is, so the player is told "survey this
/// ground for minerals first" while hovering rather than after a failed click.
/// </remarks>
public sealed class PlacementPreview
{
    public BuildingKind Kind { get; }
    public int Size { get; }

    /// <summary>Top-left tile of the footprint under the cursor.</summary>
    public GridPos Origin { get; private set; }

    public bool IsValid { get; private set; }

    /// <summary>Why the current spot will not work, or null when it will.</summary>
    public string? Problem { get; private set; }

    public PlacementPreview(BuildingKind kind)
    {
        Kind = kind;
        Size = BuildingCatalog.For(kind).Size;
    }

    /// <summary>Centres the footprint on the hovered tile and rechecks whether it can go there.</summary>
    public void MoveTo(GridPos hovered, MatchState state, Player player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);

        Origin = new GridPos(hovered.X - Size / 2, hovered.Y - Size / 2);

        var stats = BuildingCatalog.For(Kind);

        if (!state.Map.IsFootprintBuildable(Origin, Size))
        {
            (IsValid, Problem) = (false, "Blocked ground.");
            return;
        }

        if (!state.Map.FootprintApproaches(Origin, Size).Any())
        {
            (IsValid, Problem) = (false, "Nothing could reach it.");
            return;
        }

        if (stats.RequiresMineral && new MatchCommands(state).BestKnownMineral(player, Origin, Size) is null)
        {
            (IsValid, Problem) = (false, "Survey this ground for minerals first.");
            return;
        }

        if (!player.Resources.CanAfford(stats.BuildCost))
        {
            (IsValid, Problem) = (false, $"Need {stats.BuildCost}.");
            return;
        }

        (IsValid, Problem) = (true, null);
    }
}
