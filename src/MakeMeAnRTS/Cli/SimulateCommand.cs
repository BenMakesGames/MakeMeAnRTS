using MakeMeAnRTS.Features.Ai;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;
using MakeMeAnRTS.Features.World.Generation;

namespace MakeMeAnRTS.Cli;

/// <summary>
/// Runs a match with no window and no player, printing what each side owns as time passes.
/// </summary>
/// <remarks>
/// The simulation is deliberately free of any dependency on SDL, which makes this possible: a whole
/// match can be run in a second and its economy read as numbers. Watching wood climb - or fail to -
/// catches broken hauling, stuck pathing and a starving CPU far faster than playing the game does.
/// </remarks>
public static class SimulateCommand
{
    /// <summary>Fixed simulation step. Matches the rate the real game loop steps at.</summary>
    private const float StepSeconds = 1f / 20f;

    public static int Run(CommandLineArgs args)
    {
        var settings = new WorldGenSettings
        {
            Seed = args.Int("seed", 1),
            Width = args.Int("width", 176),
            Height = args.Int("height", 176),
        };

        var totalSeconds = args.Float("seconds", 300f);
        var reportInterval = args.Float("report", 60f);
        var verbose = args.HasFlag("verbose");

        // Letting the CPU play both sides turns this into a full self-play match, which is the fastest
        // way to find out whether the opponent can actually win a game rather than just gather wood.
        var cpuPlaysEveryone = args.HasFlag("cpu-vs-cpu");

        var state = MatchSetup.Create(settings);
        var runner = new MatchRunner(state);

        var commanders = state.Players
            .Where(player => cpuPlaysEveryone || !player.IsHuman)
            .Select(player => new CpuCommander(state, player.Index, CpuPlan.Standard, seed: settings.Seed + player.Index))
            .ToList();

        var commandersByPlayer = state.Players
            .Where(player => cpuPlaysEveryone || !player.IsHuman)
            .Select((player, index) => (player.Index, Commander: commanders[index]))
            .ToDictionary(entry => entry.Index, entry => entry.Commander);

        Console.WriteLine($"Simulating seed {state.Map.Seed} for {totalSeconds:0}s of match time.");
        Report(state, verbose, commandersByPlayer);

        var nextReport = reportInterval;

        while (state.ElapsedSeconds < totalSeconds && !state.IsOver)
        {
            foreach (var commander in commanders)
                commander.Update(StepSeconds);

            runner.Update(StepSeconds);

            if (state.ElapsedSeconds < nextReport)
                continue;

            Report(state, verbose, commandersByPlayer);
            nextReport += reportInterval;
        }

        if (state.WinnerIndex is { } winner)
            Console.WriteLine($"Match over at {state.ElapsedSeconds:0}s: {state.PlayerAt(winner).Name} wins.");

        return 0;
    }

    private static void Report(MatchState state, bool verbose, IReadOnlyDictionary<int, CpuCommander> commanders)
    {
        Console.WriteLine($"--- t={state.ElapsedSeconds,6:0}s ---");

        foreach (var player in state.Players)
        {
            var units = state.UnitsOf(player.Index).ToList();
            var byKind = string.Join(" ", UnitCatalog.All
                .Select(kind => (kind, count: units.Count(unit => unit.Kind == kind)))
                .Where(entry => entry.count > 0)
                .Select(entry => $"{entry.count}x{UnitCatalog.For(entry.kind).DisplayName}"));

            var buildings = string.Join(" ", state.BuildingsOf(player.Index)
                .GroupBy(building => building.MinedMineral is { } mineral ? $"{building.Kind}({mineral})" : building.Kind.ToString())
                .Select(group => $"{group.Count()}x{group.Key}{(group.Any(b => !b.IsComplete) ? "*" : "")}"));

            var stock = string.Join(" ", Resources.All.Select(resource => $"{resource.DisplayName()}={player.Resources[resource]}"));

            // Orders and carried loads are what expose a stalled economy: everything "gathering" while
            // wood sits still means the haul or the path is broken, not the gathering.
            var orders = string.Join(" ", units
                .GroupBy(unit => unit.Order.GetType().Name)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}={group.Count()}"));

            var carrying = units.Count(unit => unit.CarriedAmount > 0);
            var pathless = units.Count(unit => unit.Path.Count == 0 && unit.Order is not UnitOrder.Idle);

            Console.WriteLine($"  {player.Name,-4} {stock}");
            Console.WriteLine($"       units: {units.Count} ({byKind})");
            Console.WriteLine($"       orders: {orders}; carrying {carrying}; without a route {pathless}");
            Console.WriteLine($"       buildings: {(string.IsNullOrEmpty(buildings) ? "none" : buildings)}");

            // What the prospector has actually turned up, which is what limits where mines can go.
            var surveyed = state.Map.Tiles.Positions().Count(pos => player.Knowledge.IsSurveyed(pos));
            var richest = string.Join(" ", Minerals.All.Select(mineral =>
                $"{mineral}<={state.Map.Tiles.Positions().Max(pos => (int)player.Knowledge.KnownAbundance(mineral, pos))}"));

            var surveys = state.Signs.Count(sign => sign.OwnerIndex == player.Index);
            Console.WriteLine($"       surveyed: {surveyed} tiles over {surveys} surveys; best known {richest}");

            if (commanders.TryGetValue(player.Index, out var commander))
                Console.WriteLine($"       ai: {commander.Status()}");

            if (!verbose)
                continue;

            foreach (var unit in units)
            {
                var load = unit.CarriedAmount > 0 ? $" carrying {unit.CarriedAmount} {unit.CarriedResource}" : "";
                Console.WriteLine($"         #{unit.Id} {unit.Kind} at {unit.Tile} order={unit.Order} path={unit.Path.Count}{load} deliverTo={unit.DeliveryTargetId?.ToString() ?? "-"}");
            }
        }
    }
}
