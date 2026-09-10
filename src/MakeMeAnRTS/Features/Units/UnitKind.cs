namespace MakeMeAnRTS.Features.Units;

public enum UnitKind : byte
{
    /// <summary>Builds, cuts wood, and works mines. The backbone of an economy.</summary>
    Citizen,

    /// <summary>Fast and far-sighted, but cannot work. Exists to find things.</summary>
    Scout,

    /// <summary>Surveys the ground for minerals and plants a sign recording what it found.</summary>
    Prospector,

    /// <summary>Melee fighter from the barracks.</summary>
    Soldier,

    /// <summary>Ranged fighter from the archery range.</summary>
    Archer,
}
