using MakeMeAnRTS.Core;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.World;

namespace MakeMeAnRTS.Features.Buildings;

/// <summary>
/// One building, from the moment its foundation is placed to the moment it is destroyed.
/// </summary>
/// <remarks>
/// A foundation is the same object as the finished building, with <see cref="IsComplete"/> false. That
/// means it occupies its tiles, can be attacked, and can be selected from the moment it is placed - all
/// behaviour a player expects and none of it needing a second type.
/// </remarks>
public sealed class Building
{
    private readonly List<int> _assignedWorkerIds = [];
    private readonly Queue<UnitKind> _trainingQueue = [];

    public int Id { get; }
    public BuildingKind Kind { get; }
    public int OwnerIndex { get; }
    public BuildingStats Stats { get; }

    /// <summary>Top-left tile of the footprint.</summary>
    public GridPos Origin { get; }

    public float Health { get; set; }

    /// <summary>Worker-seconds of labour done so far. A building is finished at <see cref="BuildingStats.BuildSeconds"/>.</summary>
    public float BuildProgress { get; private set; }

    public bool IsComplete { get; private set; }

    /// <summary>The mineral a mine extracts, fixed when it is placed. Null for every other kind.</summary>
    public MineralKind? MinedMineral { get; }

    /// <summary>Workers assigned to produce here. Only mines accept any.</summary>
    public IReadOnlyList<int> AssignedWorkerIds => _assignedWorkerIds;

    public IReadOnlyCollection<UnitKind> TrainingQueue => _trainingQueue;

    /// <summary>Seconds of training done on the unit at the head of the queue.</summary>
    public float TrainingElapsed { get; set; }

    public bool IsAlive => Health > 0f;

    public Building(int id, BuildingKind kind, int ownerIndex, GridPos origin, MineralKind? minedMineral, bool startCompleted)
    {
        Id = id;
        Kind = kind;
        OwnerIndex = ownerIndex;
        Stats = BuildingCatalog.For(kind);
        Origin = origin;
        MinedMineral = minedMineral;

        if (Stats.RequiresMineral && minedMineral is null)
            throw new ArgumentNullException(nameof(minedMineral), $"A {Stats.DisplayName} must be placed on a known mineral.");

        IsComplete = startCompleted;
        BuildProgress = startCompleted ? Stats.BuildSeconds : 0f;

        // A foundation starts as a shell and gains health as it is built, so an unfinished building is fragile.
        Health = startCompleted ? Stats.MaxHealth : Stats.MaxHealth * 0.1f;
    }

    /// <summary>Centre of the footprint in world space, which is what units aim at and the HUD labels.</summary>
    public Vec2 Center => new(Origin.X + Stats.Size / 2f, Origin.Y + Stats.Size / 2f);

    public IEnumerable<GridPos> FootprintTiles()
    {
        for (var dy = 0; dy < Stats.Size; dy++)
            for (var dx = 0; dx < Stats.Size; dx++)
                yield return new GridPos(Origin.X + dx, Origin.Y + dy);
    }

    public bool CoversTile(GridPos pos) =>
        pos.X >= Origin.X && pos.X < Origin.X + Stats.Size &&
        pos.Y >= Origin.Y && pos.Y < Origin.Y + Stats.Size;

    /// <summary>
    /// Adds building labour and finishes the building once it has had enough.
    /// </summary>
    /// <returns>True on the update that completes it, so callers can react once.</returns>
    public bool AddBuildProgress(float workerSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(workerSeconds);

        if (IsComplete)
            return false;

        BuildProgress = MathF.Min(Stats.BuildSeconds, BuildProgress + workerSeconds);

        // Health tracks progress, so a half-built structure is half as tough.
        Health = MathF.Max(Health, Stats.MaxHealth * (0.1f + 0.9f * BuildProgress / Stats.BuildSeconds));

        if (BuildProgress < Stats.BuildSeconds)
            return false;

        IsComplete = true;
        return true;
    }

    public float BuildFraction => Stats.BuildSeconds <= 0f ? 1f : BuildProgress / Stats.BuildSeconds;

    public bool HasWorkerSlot => _assignedWorkerIds.Count < Stats.MaxWorkers;

    /// <summary>Assigns a worker if there is room. Assigning the same worker twice is a no-op.</summary>
    public bool TryAssignWorker(int unitId)
    {
        if (_assignedWorkerIds.Contains(unitId))
            return true;

        if (!HasWorkerSlot)
            return false;

        _assignedWorkerIds.Add(unitId);
        return true;
    }

    public void RemoveWorker(int unitId) => _assignedWorkerIds.Remove(unitId);

    public void EnqueueTraining(UnitKind kind) => _trainingQueue.Enqueue(kind);

    public UnitKind? PeekTraining() => _trainingQueue.Count > 0 ? _trainingQueue.Peek() : null;

    public UnitKind DequeueTraining()
    {
        TrainingElapsed = 0f;
        return _trainingQueue.Dequeue();
    }

    /// <summary>Cancels the most recently queued unit, so a misclick is undoable.</summary>
    public UnitKind? CancelLastQueued()
    {
        if (_trainingQueue.Count == 0)
            return null;

        var remaining = _trainingQueue.ToList();
        var cancelled = remaining[^1];
        remaining.RemoveAt(remaining.Count - 1);

        _trainingQueue.Clear();
        foreach (var kind in remaining)
            _trainingQueue.Enqueue(kind);

        return cancelled;
    }

    public void TakeDamage(float amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Health = MathF.Max(0f, Health - amount);
    }
}
