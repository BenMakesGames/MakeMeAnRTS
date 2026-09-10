using SDL3;
using MakeMeAnRTS.Engine;
using MakeMeAnRTS.Features.Ai;
using MakeMeAnRTS.Features.Audio;
using MakeMeAnRTS.Features.Buildings;
using MakeMeAnRTS.Features.Camera;
using MakeMeAnRTS.Features.Debug;
using MakeMeAnRTS.Features.Hud;
using MakeMeAnRTS.Features.Match;
using MakeMeAnRTS.Features.PlayerControl;
using MakeMeAnRTS.Features.Prospecting;
using MakeMeAnRTS.Features.Units;
using MakeMeAnRTS.Features.Vision;
using MakeMeAnRTS.Features.World;
using MakeMeAnRTS.Features.World.Generation;

namespace MakeMeAnRTS.Game;

/// <summary>
/// One match, wired together: simulation, opponents, camera, input and everything that draws.
/// </summary>
/// <remarks>
/// The composition root. Features know nothing about each other's rendering or input; this is the only
/// place that knows the order they go in. Update and Draw are separate and Draw changes no state, which
/// is what lets a screenshot be taken of any moment without disturbing the match.
/// </remarks>
public sealed class GameScreen : IDisposable
{
    /// <summary>The human player is always seat 0.</summary>
    private const int HumanPlayerIndex = 0;

    private readonly MatchState _state;
    private readonly MatchRunner _runner;
    private readonly List<CpuCommander> _opponents = [];

    private readonly Camera2D _camera;
    private readonly PlayerController _controller;

    private readonly WorldRenderer _worldRenderer;
    private readonly UnitRenderer _unitRenderer;
    private readonly BuildingRenderer _buildingRenderer;
    private readonly SignRenderer _signRenderer;
    private readonly FogRenderer _fogRenderer;
    private readonly SelectionRenderer _selectionRenderer;
    private readonly MinimapRenderer _minimapRenderer;
    private readonly HudRenderer _hudRenderer;
    private readonly DebugOverlay _debugOverlay;
    private readonly SoundBoard _sound;

    private HudLayout _layout;
    private int _lastMessageRevision;

    /// <summary>The last step's length, so drawing can drive time-based refreshes without its own clock.</summary>
    private float _lastDeltaSeconds;

    public MatchState State => _state;
    public Camera2D Camera => _camera;
    public PlayerController Controller => _controller;

    /// <summary>Reveals the whole map. For the debug overlay and for looking at a finished match.</summary>
    public bool RevealEverything { get; set; }

    /// <param name="renderer">
    /// Needed up front because the minimap keeps a texture, which cannot exist without a renderer.
    /// </param>
    public GameScreen(WorldGenSettings worldSettings, int windowWidth, int windowHeight, AudioDevice audio, Renderer2D renderer)
    {
        ArgumentNullException.ThrowIfNull(worldSettings);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(renderer);

        _state = MatchSetup.Create(worldSettings);
        _runner = new MatchRunner(_state);

        foreach (var player in _state.Players.Where(player => !player.IsHuman))
            _opponents.Add(new CpuCommander(_state, player.Index, CpuPlan.Standard, seed: _state.Map.Seed + player.Index));

        _layout = HudLayout.For(windowWidth, windowHeight);

        _camera = new Camera2D(_state.Map.Width, _state.Map.Height, _layout.Viewport);
        _controller = new PlayerController(_state, _camera, HumanPlayerIndex);

        _worldRenderer = new WorldRenderer(_state.Map);
        _unitRenderer = new UnitRenderer(_state);
        _buildingRenderer = new BuildingRenderer(_state);
        _signRenderer = new SignRenderer();
        _fogRenderer = new FogRenderer();
        _selectionRenderer = new SelectionRenderer(_state);
        _minimapRenderer = new MinimapRenderer(renderer.Handle, _state, HumanPlayerIndex);
        _hudRenderer = new HudRenderer(_state, _minimapRenderer, HumanPlayerIndex);
        _debugOverlay = new DebugOverlay(_state);
        _sound = new SoundBoard(audio, _state, HumanPlayerIndex);

        StartLookingAtHome();
    }

    private Player Human => _state.PlayerAt(HumanPlayerIndex);

    public void Resize(int windowWidth, int windowHeight)
    {
        _layout = HudLayout.For(windowWidth, windowHeight);
        _camera.Viewport = _layout.Viewport;
    }

    public void Update(InputState input, float deltaSeconds)
    {
        ArgumentNullException.ThrowIfNull(input);

        _lastDeltaSeconds = deltaSeconds;
        _controller.Update(input, deltaSeconds, _layout.Minimap);
        _camera.Update(input, deltaSeconds);

        foreach (var opponent in _opponents)
            opponent.Update(deltaSeconds);

        _runner.Update(deltaSeconds);

        // Sound reads the match after it has been stepped, so it reacts to this frame's state.
        _sound.Update(deltaSeconds);

        if (_controller.MessageRevision != _lastMessageRevision)
        {
            _lastMessageRevision = _controller.MessageRevision;

            if (_controller.MessageIsProblem)
                _sound.Play(GameSound.Denied);
        }
    }

    public void Draw(Renderer2D renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.Clear(Palette.Rgb(10, 12, 16));
        renderer.SetClip(_layout.Viewport);

        var vision = ViewerVision();

        _worldRenderer.DrawTerrain(renderer, _camera);

        // The heat map draws what this player has surveyed, never the true map: the prospector exists
        // to fill it in, and handing over the answer would delete the unit's reason to be.
        if (_controller.MineralOverlay is { } mineral)
            _worldRenderer.DrawMineralHeatMap(renderer, _camera, mineral, Human.Knowledge.KnownMap(mineral));

        _signRenderer.Draw(renderer, _camera, _state.Signs, HumanPlayerIndex);
        _buildingRenderer.Draw(renderer, _camera, vision, HumanPlayerIndex, _controller.Selection.BuildingIds);
        _unitRenderer.Draw(renderer, _camera, vision, HumanPlayerIndex, _controller.Selection.UnitIds);

        _fogRenderer.Draw(renderer, _camera, vision);
        _selectionRenderer.Draw(renderer, _camera, _controller, Human);

        if (_controller.ShowDebugOverlay)
            _debugOverlay.Draw(renderer, _camera, HumanPlayerIndex);

        renderer.SetClip(null);
        _hudRenderer.Draw(renderer, _camera, _controller, _layout, _lastDeltaSeconds);
    }

    public void Dispose() => _minimapRenderer.Dispose();

    /// <summary>
    /// The sight this frame is drawn through. Revealing everything hands over a full-map view, which is
    /// only ever used for debugging and for reviewing a match that is already decided.
    /// </summary>
    private PlayerVision ViewerVision()
    {
        if (!RevealEverything)
            return Human.Vision;

        Human.Vision.RevealAll();
        return Human.Vision;
    }

    private void StartLookingAtHome()
    {
        var home = _state.BuildingsOf(HumanPlayerIndex).FirstOrDefault(building => building.Kind == BuildingKind.HomeBase);

        if (home is not null)
            _camera.CenterOn(home.Center);
    }
}
