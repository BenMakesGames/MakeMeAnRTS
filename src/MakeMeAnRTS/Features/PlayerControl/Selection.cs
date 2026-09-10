using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;

namespace MakeMeAnRTS.Features.PlayerControl;

/// <summary>
/// What the player currently has selected.
/// </summary>
/// <remarks>
/// Units and buildings are kept apart rather than in one list, because almost everything the HUD and
/// the order code want to ask is about one or the other. Ids rather than references, so a selection
/// containing something that has since died is merely stale, not a crash waiting to happen.
/// </remarks>
public sealed class Selection
{
    private readonly HashSet<int> _unitIds = [];

    public IReadOnlySet<int> UnitIds => _unitIds;

    /// <summary>The selected building. Selecting a building clears any unit selection and vice versa.</summary>
    public int? BuildingId { get; private set; }

    public bool IsEmpty => _unitIds.Count == 0 && BuildingId is null;

    public void Clear()
    {
        _unitIds.Clear();
        BuildingId = null;
    }

    public void SelectUnits(IEnumerable<Unit> units, bool add)
    {
        ArgumentNullException.ThrowIfNull(units);

        if (!add)
            Clear();

        BuildingId = null;

        foreach (var unit in units)
            _unitIds.Add(unit.Id);
    }

    public void SelectBuilding(Building building)
    {
        ArgumentNullException.ThrowIfNull(building);

        Clear();
        BuildingId = building.Id;
    }

    /// <summary>Drops anything that has died, so the HUD never shows a phantom.</summary>
    public void Prune(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _unitIds.RemoveWhere(id => state.FindUnit(id) is not { IsAlive: true });

        if (BuildingId is { } id && state.FindBuilding(id) is not { IsAlive: true })
            BuildingId = null;
    }

    /// <summary>The live units in the selection, in a stable order.</summary>
    public List<Unit> ResolveUnits(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return _unitIds
            .Select(state.FindUnit)
            .Where(unit => unit is { IsAlive: true })
            .Select(unit => unit!)
            .OrderBy(unit => unit.Id)
            .ToList();
    }

    public Building? ResolveBuilding(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return BuildingId is { } id ? state.FindBuilding(id) : null;
    }

    /// <summary>The selected buildings, as a set, for the renderer to check membership against.</summary>
    public IReadOnlySet<int> BuildingIds => BuildingId is { } id ? new HashSet<int> { id } : EmptyIds;

    private static readonly HashSet<int> EmptyIds = [];
}
