using SDL3;
using MakeMeAnRTS.Engine;

namespace MakeMeAnRTS.Game;

/// <summary>
/// The frame loop: read input, step the match, draw, repeat.
/// </summary>
/// <remarks>
/// Frame time is clamped rather than passed through raw. A stall - a window drag, a breakpoint, the
/// machine going to sleep - would otherwise arrive as a single enormous step and teleport every unit
/// through walls. Losing a moment of simulated time is much cheaper than that.
/// </remarks>
public sealed class GameLoop
{
    /// <summary>Longest step the simulation will take in one go, in seconds.</summary>
    private const float MaxStepSeconds = 1f / 15f;

    private readonly Platform _platform;
    private readonly Renderer2D _renderer;
    private readonly GameScreen _screen;

    public GameLoop(Platform platform, Renderer2D renderer, GameScreen screen)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(screen);

        _platform = platform;
        _renderer = renderer;
        _screen = screen;
    }

    public void Run()
    {
        var input = new InputState();
        var frequency = (double)SDL.GetPerformanceFrequency();
        var previousTicks = SDL.GetPerformanceCounter();

        var (lastWidth, lastHeight) = _platform.GetOutputSize();

        while (true)
        {
            input.BeginFrame();

            if (input.QuitRequested)
                return;

            // Escape closes a finished match; during play it cancels whatever the player was doing.
            if (_screen.State.IsOver && input.WasPressed(SDL.Scancode.Escape))
                return;

            var nowTicks = SDL.GetPerformanceCounter();
            var deltaSeconds = (float)((nowTicks - previousTicks) / frequency);
            previousTicks = nowTicks;

            var (width, height) = _platform.GetOutputSize();
            if (width != lastWidth || height != lastHeight)
            {
                _screen.Resize(width, height);
                (lastWidth, lastHeight) = (width, height);
            }

            _screen.Update(input, MathF.Min(deltaSeconds, MaxStepSeconds));
            _screen.Draw(_renderer);
            _renderer.Present();
        }
    }
}
