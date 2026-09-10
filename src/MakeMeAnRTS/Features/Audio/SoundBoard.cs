using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Match;

namespace MakeMeAnRTS.Features.Audio;

/// <summary>The things the game makes a noise about.</summary>
public enum GameSound
{
    /// <summary>A building finished.</summary>
    BuildingComplete,

    /// <summary>A unit came out of a building.</summary>
    UnitReady,

    /// <summary>A survey was taken and a sign planted.</summary>
    SurveyComplete,

    /// <summary>Something of the player's is being hurt.</summary>
    UnderAttack,

    /// <summary>An order was refused - bad placement, cannot afford it.</summary>
    Denied,

    /// <summary>The match ended.</summary>
    MatchOver,
}

/// <summary>
/// Watches the match for things worth hearing and plays them.
/// </summary>
/// <remarks>
/// Written as an observer that diffs the match each frame rather than as events raised by the
/// simulation. That keeps the rules free of any knowledge that sound exists - the simulation still runs
/// identically with no audio device, in the headless tools, and under the CPU-versus-CPU harness.
///
/// Everything is rate limited. Forty soldiers landing blows would otherwise fire forty identical
/// effects in one frame, which is noise in both senses.
/// </remarks>
public sealed class SoundBoard
{
    private readonly AudioDevice _audio;
    private readonly MatchState _state;
    private readonly int _playerIndex;

    private readonly Dictionary<GameSound, IntPtr> _sounds = [];
    private readonly Dictionary<GameSound, float> _cooldowns = [];

    private readonly HashSet<int> _knownCompleteBuildings = [];
    private int _knownUnitCount;
    private int _knownSignCount;
    private float _knownFriendlyHealth;
    private bool _announcedOutcome;

    /// <summary>Seconds before the same sound may play again.</summary>
    private static readonly Dictionary<GameSound, float> MinimumGap = new()
    {
        [GameSound.BuildingComplete] = 0.4f,
        [GameSound.UnitReady] = 0.4f,
        [GameSound.SurveyComplete] = 0.4f,
        [GameSound.UnderAttack] = 4f,
        [GameSound.Denied] = 0.5f,
        [GameSound.MatchOver] = 60f,
    };

    public SoundBoard(AudioDevice audio, MatchState state, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(state);

        _audio = audio;
        _state = state;
        _playerIndex = playerIndex;

        Build();
        Resync();
    }

    private void Build()
    {
        // A rising two-note figure: something finished and it was a good thing.
        _sounds[GameSound.BuildingComplete] = _audio.CreateSound(SoundSynth.Sequence(
            SoundSynth.Tone(392f, 392f, 0.09f),
            SoundSynth.Tone(587f, 587f, 0.16f)));

        _sounds[GameSound.UnitReady] = _audio.CreateSound(SoundSynth.Tone(523f, 659f, 0.13f, 0.2f));

        // Three quick rising pips: a discovery, not an alarm.
        _sounds[GameSound.SurveyComplete] = _audio.CreateSound(SoundSynth.Sequence(
            SoundSynth.Tone(784f, 784f, 0.05f, 0.18f),
            SoundSynth.Tone(988f, 988f, 0.05f, 0.18f),
            SoundSynth.Tone(1319f, 1319f, 0.10f, 0.18f)));

        // Low, dissonant and thuddy, so it reads as bad news without needing to be loud.
        _sounds[GameSound.UnderAttack] = _audio.CreateSound(SoundSynth.Layer(
            SoundSynth.Tone(196f, 165f, 0.35f, 0.2f),
            SoundSynth.NoiseBurst(0.35f, 0.12f)));

        _sounds[GameSound.Denied] = _audio.CreateSound(SoundSynth.Tone(220f, 165f, 0.14f, 0.18f));

        _sounds[GameSound.MatchOver] = _audio.CreateSound(SoundSynth.Sequence(
            SoundSynth.Tone(523f, 523f, 0.14f),
            SoundSynth.Tone(659f, 659f, 0.14f),
            SoundSynth.Tone(784f, 784f, 0.36f)));
    }

    /// <summary>Plays a sound directly, for things the interface knows about but the match does not.</summary>
    public void Play(GameSound sound)
    {
        if (_cooldowns.GetValueOrDefault(sound) > 0f)
            return;

        _cooldowns[sound] = MinimumGap.GetValueOrDefault(sound, 0.2f);
        _audio.Play(_sounds.GetValueOrDefault(sound));
    }

    public void Update(float deltaSeconds)
    {
        foreach (var sound in _cooldowns.Keys.ToList())
            _cooldowns[sound] = MathF.Max(0f, _cooldowns[sound] - deltaSeconds);

        foreach (var building in _state.BuildingsOf(_playerIndex))
            if (building.IsComplete && _knownCompleteBuildings.Add(building.Id))
                Play(GameSound.BuildingComplete);

        var units = _state.UnitsOf(_playerIndex).Count();
        if (units > _knownUnitCount)
            Play(GameSound.UnitReady);

        _knownUnitCount = units;

        var signs = _state.Signs.Count(sign => sign.OwnerIndex == _playerIndex);
        if (signs > _knownSignCount)
            Play(GameSound.SurveyComplete);

        _knownSignCount = signs;

        // Total health falling is the cheapest reliable "something of mine is being hurt" signal, and it
        // catches buildings being demolished as well as units being shot.
        var health = FriendlyHealth();
        if (health < _knownFriendlyHealth - 1f)
            Play(GameSound.UnderAttack);

        _knownFriendlyHealth = health;

        if (_state.IsOver && !_announcedOutcome)
        {
            _announcedOutcome = true;
            Play(GameSound.MatchOver);
        }
    }

    /// <summary>Takes a baseline of the match so the opening pieces do not all announce themselves.</summary>
    private void Resync()
    {
        foreach (var building in _state.BuildingsOf(_playerIndex).Where(building => building.IsComplete))
            _knownCompleteBuildings.Add(building.Id);

        _knownUnitCount = _state.UnitsOf(_playerIndex).Count();
        _knownSignCount = _state.Signs.Count(sign => sign.OwnerIndex == _playerIndex);
        _knownFriendlyHealth = FriendlyHealth();
    }

    private float FriendlyHealth() =>
        _state.UnitsOf(_playerIndex).Sum(unit => unit.Health) +
        _state.BuildingsOf(_playerIndex).Sum(building => building.Health);
}
