using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Ai;

/// <summary>
/// Plays one side. Scouts, prospects, builds an economy, trains an army and attacks with it.
/// </summary>
/// <remarks>
/// Every action goes through <see cref="MatchCommands"/> and every decision reads only this player's
/// own <see cref="Player.Knowledge"/> and <see cref="Player.Vision"/>. That is deliberate: the CPU is
/// structurally incapable of mining ground it has not surveyed or marching on a base it has not found,
/// so it has to play the same game the player does rather than a privileged version of it.
/// </remarks>
public sealed class CpuCommander
{
    private readonly MatchState _state;
    private readonly MatchCommands _commands;
    private readonly CpuPlan _plan;
    private readonly Rng _rng;

    private readonly int _playerIndex;

    /// <summary>
    /// Survey spots that were ordered but never actually read, so the prospector stops retrying ground
    /// it cannot reach. Bounded and oldest-first: a permanent blacklist eventually rules out the whole
    /// map and leaves the prospector idle for the rest of the match.
    /// </summary>
    private readonly Queue<GridPos> _unreachableSpots = [];

    /// <summary>The spot the prospector was last sent to, so its success can be checked afterwards.</summary>
    private GridPos? _pendingSurveySpot;

    /// <summary>Exploration targets that turned out to be unreachable, remembered the same bounded way.</summary>
    private readonly Queue<GridPos> _unreachableTargets = [];

    /// <summary>Where the army is currently marching to look for the enemy.</summary>
    private GridPos? _armyRally;

    /// <summary>
    /// Where an enemy was last seen.
    /// </summary>
    /// <remarks>
    /// The single most useful thing the CPU can remember. Enemies come from their base, so pushing
    /// towards the last contact finds it; without this the army would wander off towards whatever
    /// unexplored corner happened to be nearest and never follow up a fight it just had.
    /// </remarks>
    private GridPos? _lastEnemyContact;

    private float _thinkTimer;
    private int _militaryCycle;

    /// <summary>The building the CPU is currently saving up for, so its mining stays aimed at it.</summary>
    private BuildingKind? _savingFor;
    private bool _isAttacking;

    public CpuCommander(MatchState state, int playerIndex, CpuPlan plan, int seed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);

        _state = state;
        _commands = new MatchCommands(state);
        _plan = plan;
        _playerIndex = playerIndex;
        _rng = new Rng(seed);
    }

    private Player Player => _state.PlayerAt(_playerIndex);

    public void Update(float deltaSeconds)
    {
        if (_state.IsOver || Player.IsDefeated)
            return;

        // Thinking on a timer rather than every frame: decisions are cheap, but scanning every unit and
        // searching for build sites sixty times a second is not, and the CPU gains nothing from it.
        _thinkTimer -= deltaSeconds;
        if (_thinkTimer > 0f)
            return;

        _thinkTimer = _plan.ThinkInterval;

        var home = _state.BuildingsOf(_playerIndex).FirstOrDefault(building => building.Kind == BuildingKind.HomeBase);
        if (home is null)
        {
            // Base gone: there is no economy left to run, so everything that can fight, fights.
            SendEveryoneToFight();
            return;
        }

        NoteEnemyContact();
        DirectScout(home);
        DirectProspector(home);
        AssignIdleCitizens(home);
        TrainUnits(home);
        AdvanceBuildOrder(home);
        ManageArmy();
    }

    /// <summary>Remembers where the enemy currently is, if any of them can be seen.</summary>
    private void NoteEnemyContact()
    {
        foreach (var enemy in _state.Units.Where(unit => unit.OwnerIndex != _playerIndex && unit.IsAlive))
        {
            if (Player.Vision.IsVisible(enemy.Tile))
            {
                _lastEnemyContact = enemy.Tile;
                return;
            }
        }

        foreach (var enemy in _state.Buildings.Where(building => building.OwnerIndex != _playerIndex && building.IsAlive))
        {
            if (Player.Vision.IsExplored(enemy.Center.ToTile()))
            {
                _lastEnemyContact = enemy.Center.ToTile();
                return;
            }
        }
    }

    /// <summary>Walks the scout to somewhere it has not been, which is how the CPU finds anything at all.</summary>
    private void DirectScout(Building home)
    {
        foreach (var scout in _state.UnitsOf(_playerIndex).Where(unit => unit.Kind == UnitKind.Scout))
        {
            if (scout.Order is not UnitOrder.Idle)
                continue;

            var target = FindExplorationTarget(scout.Tile, minRadius: 10);
            if (target is not null)
                _commands.OrderMove([scout], target.Value);
        }
    }

    /// <summary>How many failed survey spots to remember before letting the oldest be tried again.</summary>
    private const int UnreachableMemory = 32;

    /// <summary>Sends the prospector to survey ground nobody has read yet, working outwards from home.</summary>
    private void DirectProspector(Building home)
    {
        var prospector = _state.UnitsOf(_playerIndex).FirstOrDefault(unit => unit.Stats.CanSurvey);

        if (prospector is null || prospector.Order is not UnitOrder.Idle)
            return;

        // Idle with the last target still unread means it never got there. Remember that and move on.
        if (_pendingSurveySpot is { } previous)
        {
            if (!Player.Knowledge.IsSurveyed(previous))
            {
                _unreachableSpots.Enqueue(previous);

                while (_unreachableSpots.Count > UnreachableMemory)
                    _unreachableSpots.Dequeue();
            }

            _pendingSurveySpot = null;
        }

        var target = FindSurveyTarget(home.Center.ToTile());
        if (target is null)
            return;

        _pendingSurveySpot = target;
        _commands.OrderSurvey([prospector], target.Value);
    }

    /// <summary>
    /// Puts idle citizens to work, keeping enough on wood and sending the rest into mines.
    /// </summary>
    private void AssignIdleCitizens(Building home)
    {
        var citizens = _state.UnitsOf(_playerIndex).Where(unit => unit.Kind == UnitKind.Citizen).ToList();
        var woodcutters = citizens.Count(unit => unit.Order is UnitOrder.GatherWood);

        var claimedTrees = citizens
            .Select(unit => unit.Order)
            .OfType<UnitOrder.GatherWood>()
            .Select(order => order.Tree)
            .ToHashSet();

        foreach (var citizen in citizens.Where(unit => unit.Order is UnitOrder.Idle))
        {
            // Unfinished buildings come first: a half-built barracks trains nothing.
            var site = _state.BuildingsOf(_playerIndex).FirstOrDefault(building => !building.IsComplete);
            if (site is not null)
            {
                citizen.GiveOrder(new UnitOrder.BuildStructure(site.Id));
                continue;
            }

            var mine = _state.BuildingsOf(_playerIndex)
                .FirstOrDefault(building =>
                    building.Kind == BuildingKind.Mine &&
                    building.IsComplete &&
                    building.HasWorkerSlot &&
                    !_state.IsMineExhausted(building));

            if (woodcutters >= _plan.WoodcuttersWanted && mine is not null)
            {
                citizen.GiveOrder(new UnitOrder.WorkMine(mine.Id));
                continue;
            }

            var trees = _state.FindNearestTrees(citizen.Tile, skip: claimedTrees)
                ?? _state.FindNearestTrees(home.Center.ToTile(), skip: claimedTrees);

            if (trees is not null)
            {
                claimedTrees.Add(trees.Value);
                citizen.GiveOrder(new UnitOrder.GatherWood(trees.Value));
                woodcutters++;
                continue;
            }

            // No wood left anywhere in reach: a mine slot beats standing still.
            if (mine is not null)
                citizen.GiveOrder(new UnitOrder.WorkMine(mine.Id));
        }
    }

    private void TrainUnits(Building home)
    {
        var citizens = _state.UnitsOf(_playerIndex).Count(unit => unit.Kind == UnitKind.Citizen);

        // Only queue one at a time, so an early surplus is not spent entirely on villagers.
        if (citizens < _plan.TargetCitizens && home.TrainingQueue.Count == 0)
            _commands.TryQueueTraining(home, UnitKind.Citizen, out _);

        // Replace a lost prospector: without one, no new ground can ever be mined.
        var hasProspector = _state.UnitsOf(_playerIndex).Any(unit => unit.Stats.CanSurvey);
        if (!hasProspector && home.TrainingQueue.Count == 0)
            _commands.TryQueueTraining(home, UnitKind.Prospector, out _);

        foreach (var barracks in _state.BuildingsOf(_playerIndex).Where(IsMilitaryBuilding))
        {
            if (barracks.TrainingQueue.Count >= 2)
                continue;

            var kind = _plan.MilitaryMix[_militaryCycle % _plan.MilitaryMix.Count];

            // Cycle on regardless: if this building cannot make that kind, the next one probably can.
            if (UnitCatalog.For(kind).TrainedAt == barracks.Kind && _commands.TryQueueTraining(barracks, kind, out _))
                _militaryCycle++;
            else if (UnitCatalog.TrainedAt(barracks.Kind).FirstOrDefault() is { } fallback)
                _commands.TryQueueTraining(barracks, fallback, out _);
        }
    }

    private static bool IsMilitaryBuilding(Building building) =>
        building.IsComplete && building.Kind is BuildingKind.Barracks or BuildingKind.ArcheryRange;

    /// <summary>
    /// Picks the most useful thing to build right now, if it can be afforded and placed.
    /// </summary>
    /// <remarks>
    /// A standing set of targets rather than a fixed opening list. Mines exhaust their footprint, so a
    /// scripted build order runs out and leaves the CPU sitting on a pile of wood with nothing to spend
    /// it on. Re-deciding every think keeps it replacing worked-out mines for the rest of the match.
    /// </remarks>
    private void AdvanceBuildOrder(Building home)
    {
        // One at a time: several foundations at once would split the builders and finish none of them.
        if (_state.BuildingsOf(_playerIndex).Any(building => !building.IsComplete))
            return;

        if (NextBuildingWanted() is not { } kind)
        {
            _savingFor = null;
            return;
        }

        // Remembered so mine choice can aim at whatever this building is short of.
        _savingFor = kind;

        var stats = BuildingCatalog.For(kind);
        if (!Player.Resources.CanAfford(stats.BuildCost))
            return;

        MineralKind? wantedMineral = null;
        GridPos? site;

        if (kind == BuildingKind.Mine)
        {
            var choice = FindMineSite();

            // Nothing surveyed worth mining yet: wait for the prospector rather than skipping the step.
            if (choice is null)
                return;

            (site, wantedMineral) = (choice.Value.Origin, choice.Value.Mineral);
        }
        else
        {
            site = FindBuildSite(home.Center.ToTile(), stats.Size);
        }

        if (site is null)
            return;

        var builders = _state.UnitsOf(_playerIndex)
            .Where(unit => unit.Stats.CanBuild)
            .OrderBy(unit => Vec2.DistanceSquared(unit.Position, site.Value.Center))
            .Take(kind == BuildingKind.HomeBase ? 4 : 2)
            .ToList();

        if (builders.Count == 0)
            return;

        _commands.TryPlaceBuilding(_playerIndex, kind, site.Value, builders, out _, out _, wantedMineral);
    }

    /// <summary>
    /// The next building worth having, in priority order. Null when the CPU has everything it wants.
    /// </summary>
    private BuildingKind? NextBuildingWanted()
    {
        var mine = CountOf(BuildingKind.Mine, countExhaustedMines: false);
        var lumberjack = CountOf(BuildingKind.Lumberjack);
        var barracks = CountOf(BuildingKind.Barracks);
        var archery = CountOf(BuildingKind.ArcheryRange);

        // One lumberjack early pays for itself faster than anything else on this list.
        if (lumberjack == 0)
            return BuildingKind.Lumberjack;

        // Then ore, because everything military is gated on it.
        if (mine < _plan.TargetMines)
            return BuildingKind.Mine;

        if (barracks == 0)
            return BuildingKind.Barracks;

        if (lumberjack < _plan.TargetLumberjacks)
            return BuildingKind.Lumberjack;

        if (archery < _plan.TargetArcheryRanges)
            return BuildingKind.ArcheryRange;

        if (barracks < _plan.TargetBarracks)
            return BuildingKind.Barracks;

        return null;
    }

    private int CountOf(BuildingKind kind, bool countExhaustedMines = true) =>
        _state.BuildingsOf(_playerIndex).Count(building =>
            building.Kind == kind && (countExhaustedMines || !_state.IsMineExhausted(building)));

    /// <summary>Gathers an army, then throws it at the nearest enemy building it has actually seen.</summary>
    private void ManageArmy()
    {
        var army = _state.UnitsOf(_playerIndex).Where(unit => unit.Stats.CanFight && unit.Kind is not UnitKind.Citizen and not UnitKind.Scout).ToList();

        if (army.Count < _plan.AttackArmySize && !_isAttacking)
            return;

        // Once committed, keep pushing: retreating to re-gather just feeds the enemy the army piecemeal.
        _isAttacking = true;

        var target = FindKnownEnemyBuilding();

        if (target is null)
        {
            AdvanceOnEnemy(army);
            return;
        }

        _armyRally = null;

        foreach (var soldier in army.Where(unit => unit.Order is UnitOrder.Idle))
            soldier.GiveOrder(new UnitOrder.AttackBuilding(target.Id));

        EngageNearbyEnemies(army);

        // Back below half strength: stop feeding units in and rebuild before trying again.
        if (army.Count < _plan.AttackArmySize / 2)
            _isAttacking = false;
    }

    /// <summary>
    /// Moves the army towards the enemy when nothing of theirs is in sight yet: to the last place one was
    /// seen if there is one, otherwise onto unexplored ground.
    /// </summary>
    /// <remarks>
    /// One shared destination rather than one per soldier: an army that arrives together wins fights an
    /// army that trickles in loses, and it costs a single search per think instead of one per unit.
    /// </remarks>
    private void AdvanceOnEnemy(IReadOnlyList<Unit> army)
    {
        if (army.Count == 0)
            return;

        var leader = army[0];
        var everyoneStopped = army.All(unit => unit.Order is UnitOrder.Idle);

        // Reached it, or could not get there: either way this target is spent.
        if (_armyRally is { } rally && (Player.Vision.IsExplored(rally) || everyoneStopped))
        {
            if (!Player.Vision.IsExplored(rally))
                RememberUnreachable(rally);

            _armyRally = null;
        }

        // Following up a contact beats exploring: the enemy came from their base, so that is the way to it.
        _armyRally ??= _lastEnemyContact is { } contact && GridPos.StepDistance(contact, leader.Tile) > 4
            ? contact
            : FindExplorationTarget(leader.Tile, minRadius: 12);

        if (_armyRally is not { } destination)
            return;

        foreach (var soldier in army.Where(unit => unit.Order is UnitOrder.Idle))
            _commands.OrderMove([soldier], destination);

        EngageNearbyEnemies(army);
    }

    /// <summary>
    /// Turns a marching soldier onto any enemy that comes into its own line of sight.
    /// </summary>
    /// <remarks>
    /// This is what makes the CPU's advance an attack-move rather than a parade: a plain move order walks
    /// past enemies without swinging, and an army that ignores everything on the way to a waypoint gets
    /// picked apart by whatever it walked past.
    /// </remarks>
    private void EngageNearbyEnemies(IReadOnlyList<Unit> army)
    {
        var enemies = _state.Units.Where(unit => unit.OwnerIndex != _playerIndex && unit.IsAlive).ToList();
        if (enemies.Count == 0)
            return;

        foreach (var soldier in army)
        {
            if (soldier.Order is not UnitOrder.Move)
                continue;

            var range = (float)(soldier.Stats.VisionRadius * soldier.Stats.VisionRadius);
            var nearest = enemies
                .Where(enemy => Vec2.DistanceSquared(enemy.Position, soldier.Position) <= range)
                .MinBy(enemy => Vec2.DistanceSquared(enemy.Position, soldier.Position));

            if (nearest is not null)
                soldier.GiveOrder(new UnitOrder.AttackUnit(nearest.Id));
        }
    }

    private void RememberUnreachable(GridPos target)
    {
        _unreachableTargets.Enqueue(target);

        while (_unreachableTargets.Count > UnreachableMemory)
            _unreachableTargets.Dequeue();
    }

    private void SendEveryoneToFight()
    {
        var target = FindKnownEnemyBuilding();
        if (target is null)
            return;

        foreach (var unit in _state.UnitsOf(_playerIndex).Where(unit => unit.Stats.CanFight && unit.Order is UnitOrder.Idle))
            unit.GiveOrder(new UnitOrder.AttackBuilding(target.Id));
    }

    /// <summary>
    /// The nearest enemy building on ground this player has explored.
    /// </summary>
    /// <remarks>
    /// Gated on exploration on purpose. An opponent that marches straight at a base it has never seen is
    /// both unfair and boring, and it would make the scout pointless for the CPU as well as the player.
    /// </remarks>
    private Building? FindKnownEnemyBuilding()
    {
        var home = _state.BuildingsOf(_playerIndex).FirstOrDefault();
        var from = home?.Center ?? _state.UnitsOf(_playerIndex).FirstOrDefault()?.Position ?? Vec2.Zero;

        return _state.Buildings
            .Where(building => building.OwnerIndex != _playerIndex && building.IsAlive)
            .Where(building => Player.Vision.IsExplored(building.Center.ToTile()))
            .MinBy(building => Vec2.DistanceSquared(from, building.Center));
    }

    /// <summary>A clear footprint near home, with a one-tile gap so buildings do not wall each other in.</summary>
    private GridPos? FindBuildSite(GridPos near, int size)
    {
        for (var radius = 3; radius < 26; radius++)
        {
            // Random start angle each ring, so repeated buildings do not stack along one edge.
            var offset = _rng.NextInt(radius * 8);

            for (var step = 0; step < radius * 8; step++)
                if (RingTile(near, radius, (step + offset) % (radius * 8)) is { } candidate && IsGoodSite(candidate, size))
                    return candidate;
        }

        return null;
    }

    private bool IsGoodSite(GridPos origin, int size)
    {
        if (!_state.Map.IsFootprintBuildable(origin, size))
            return false;

        // A building nobody can walk up to is worse than no building at all.
        return _state.Map.FootprintApproaches(origin, size).Any();
    }

    /// <summary>Abundance a deposit must reach before it is worth putting a mine on.</summary>
    private const int WorthMiningAbundance = 40;

    /// <summary>
    /// Picks where to mine and what to mine there, weighted by what this player is actually short of.
    /// </summary>
    /// <remarks>
    /// Scoring purely on richness sent every mine onto stone, because stone is the most common mineral
    /// on any map. The CPU then sat on a thousand stone with ten iron, unable to train a single soldier.
    /// Weighting by shortage is what makes it mine the thing it needs rather than the thing it can see
    /// most of.
    /// </remarks>
    private (GridPos Origin, MineralKind Mineral)? FindMineSite()
    {
        var size = BuildingCatalog.For(BuildingKind.Mine).Size;
        var home = _state.BuildingsOf(_playerIndex).FirstOrDefault(building => building.Kind == BuildingKind.HomeBase);
        var from = home?.Center ?? Vec2.Zero;

        (GridPos Origin, MineralKind Mineral)? best = null;
        var bestScore = 0f;

        foreach (var pos in _state.Map.Tiles.Positions())
        {
            if (!Player.Knowledge.IsSurveyed(pos) || !IsGoodSite(pos, size))
                continue;

            // A long walk costs a mine most of its value, however rich it is.
            var distancePenalty = 1f + Vec2.Distance(from, pos.Center) * 0.05f;

            foreach (var mineral in Minerals.All)
            {
                var abundance = _commands.KnownAbundanceUnder(Player, pos, size, mineral);
                if (abundance < WorthMiningAbundance)
                    continue;

                var score = abundance * ShortageWeight(mineral) / distancePenalty;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = (pos, mineral);
            }
        }

        return best;
    }

    /// <summary>
    /// How badly this player wants more of a mineral: high when the stockpile is empty, falling away as
    /// it fills, and boosted for minerals that are already blocking something the CPU wants to build.
    /// </summary>
    private float ShortageWeight(MineralKind mineral)
    {
        var resource = mineral.ToResource();
        var stock = Player.Resources[resource];

        var weight = 100f / (100f + stock);

        // Whatever the CPU is saving for is the real bottleneck, if it cannot pay for it yet.
        if (_savingFor is { } wanted && BuildingCatalog.For(wanted).BuildCost[resource] > stock)
            weight *= 3f;

        // Likewise for the army: no iron means no soldiers, however much stone is piled up.
        var militaryNeed = _plan.MilitaryMix.Sum(kind => UnitCatalog.For(kind).TrainCost[resource]);
        if (militaryNeed > 0 && stock < militaryNeed * 2)
            weight *= 2.5f;

        return weight;
    }

    private GridPos? FindSurveyTarget(GridPos home)
    {
        for (var radius = 6; radius < 60; radius += 4)
        {
            var offset = _rng.NextInt(radius * 8);

            for (var step = 0; step < radius * 8; step++)
            {
                if (RingTile(home, radius, (step + offset) % (radius * 8)) is not { } candidate)
                    continue;

                // Unsurveyed ground is its own spacing rule: a completed survey marks a whole disc read,
                // so the nearest unsurveyed tile is always just outside the last one.
                if (!_state.Map.IsWalkable(candidate) || Player.Knowledge.IsSurveyed(candidate))
                    continue;

                if (_unreachableSpots.Any(spot => GridPos.StepDistance(spot, candidate) < 6))
                    continue;

                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The nearest walkable tile this player has never seen.
    /// </summary>
    /// <remarks>
    /// A straight scan of the map rather than random sampling around a point. Sampling worked while
    /// there was unexplored ground nearby and then quietly stopped finding anything, which left armies
    /// standing at home for the rest of the match with the enemy never found.
    /// </remarks>
    private GridPos? FindExplorationTarget(GridPos from, int minRadius)
    {
        GridPos? best = null;
        var bestDistance = float.MaxValue;

        foreach (var pos in _state.Map.Tiles.Positions())
        {
            if (Player.Vision.IsExplored(pos) || !_state.Map.IsWalkable(pos))
                continue;

            var distance = Vec2.DistanceSquared(from.Center, pos.Center);
            if (distance >= bestDistance || distance < minRadius * minRadius)
                continue;

            if (_unreachableTargets.Any(spot => GridPos.StepDistance(spot, pos) < 8))
                continue;

            bestDistance = distance;
            best = pos;
        }

        return best;
    }

    /// <summary>A tile on the square ring of the given radius, indexed clockwise from the top-left.</summary>
    private GridPos? RingTile(GridPos center, int radius, int step)
    {
        var perimeter = radius * 8;
        if (perimeter == 0)
            return center;

        var side = step / (radius * 2);
        var along = step % (radius * 2) - radius;

        var candidate = side switch
        {
            0 => new GridPos(center.X + along, center.Y - radius),
            1 => new GridPos(center.X + radius, center.Y + along),
            2 => new GridPos(center.X - along, center.Y + radius),
            _ => new GridPos(center.X - radius, center.Y - along),
        };

        return _state.Map.IsInBounds(candidate) ? candidate : null;
    }
}
