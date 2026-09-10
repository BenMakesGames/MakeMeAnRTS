using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Units;

namespace MakeMeAnRTS.Features.Match;

/// <summary>
/// Drives one simulation step: units, buildings, casualties, sight, and the win check.
/// </summary>
/// <remarks>
/// Stepping order matters and is fixed here: act, then remove the dead, then recompute sight, then
/// check for a winner. Recomputing sight after casualties means a destroyed watchtower stops seeing on
/// the same update it dies, rather than a frame later.
///
/// The simulation is pure state: it does not touch SDL, and nothing here reads input or draws. That is
/// what lets a whole match be run headlessly, at any speed, to check that the economy and the CPU
/// actually work.
/// </remarks>
public sealed class MatchRunner
{
    private readonly MatchState _state;
    private readonly UnitUpdater _units;
    private readonly BuildingUpdater _buildings;

    public MatchRunner(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _state = state;
        _units = new UnitUpdater(state);
        _buildings = new BuildingUpdater(state);
    }

    public void Update(float deltaSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deltaSeconds);

        if (_state.IsOver)
            return;

        _state.ElapsedSeconds += deltaSeconds;

        _units.Update(deltaSeconds);
        _buildings.Update(deltaSeconds);

        RemoveCasualties();
        UpdateVision();
        CheckForWinner();
    }

    private void RemoveCasualties()
    {
        foreach (var unit in _state.Units.Where(unit => !unit.IsAlive).ToList())
            _state.RemoveUnit(unit);

        foreach (var building in _state.Buildings.Where(building => !building.IsAlive).ToList())
            _state.RemoveBuilding(building);
    }

    private void UpdateVision()
    {
        foreach (var player in _state.Players)
            player.Vision.BeginUpdate();

        foreach (var unit in _state.Units)
            _state.PlayerAt(unit.OwnerIndex).Vision.Reveal(unit.Tile, unit.Stats.VisionRadius);

        foreach (var building in _state.Buildings)
        {
            // Sight is centred on the footprint, so a big building does not see from its corner.
            var center = building.Center.ToTile();
            _state.PlayerAt(building.OwnerIndex).Vision.Reveal(center, building.Stats.VisionRadius);
        }
    }

    /// <summary>
    /// A side that can no longer recover is out; when one side remains, it has won.
    /// </summary>
    /// <remarks>
    /// Losing every building is not enough on its own - a player with a citizen alive can rebuild, and
    /// ending the match while they can still act would be wrong. Losing every building *and* every
    /// citizen is different: nothing is left that could ever put a building back, so whatever survives
    /// is running away rather than playing. Requiring literally nothing to remain meant a single fleeing
    /// scout kept a decided match open indefinitely.
    /// </remarks>
    private void CheckForWinner()
    {
        foreach (var player in _state.Players)
        {
            if (player.IsDefeated)
                continue;

            var canRebuild = _state.UnitsOf(player.Index).Any(unit => unit.Stats.CanBuild);

            if (!_state.BuildingsOf(player.Index).Any() && !canRebuild)
                player.IsDefeated = true;
        }

        var survivors = _state.Players.Where(player => !player.IsDefeated).ToList();
        if (survivors.Count == 1)
            _state.WinnerIndex = survivors[0].Index;
    }
}
