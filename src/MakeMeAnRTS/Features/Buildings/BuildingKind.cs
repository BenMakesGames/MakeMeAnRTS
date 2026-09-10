namespace MakeMeAnRTS.Features.Buildings;

public enum BuildingKind : byte
{
    /// <summary>The one building a player starts with. Trains civilians and accepts every resource.</summary>
    HomeBase,

    /// <summary>A forward drop-off for wood, so woodcutters do not have to walk home every load.</summary>
    Lumberjack,

    /// <summary>Extracts one mineral from the ground beneath it. Needs workers assigned to do anything.</summary>
    Mine,

    /// <summary>Trains soldiers.</summary>
    Barracks,

    /// <summary>Trains archers.</summary>
    ArcheryRange,
}
