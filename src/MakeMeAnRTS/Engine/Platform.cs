using SDL3;

namespace MakeMeAnRTS.Engine;

/// <summary>
/// Owns the SDL subsystems, window and renderer for the whole process.
/// </summary>
/// <remarks>
/// <see cref="PlatformMode.Offscreen"/> exists so the game can be driven and screenshotted with no
/// display attached (CI, containers, automated visual checks). Everything above this class is
/// identical in both modes, which is what makes those screenshots trustworthy.
/// </remarks>
public sealed class Platform : IDisposable
{
    private bool _disposed;

    public IntPtr Window { get; }
    public IntPtr Renderer { get; }
    public PlatformMode Mode { get; }

    private Platform(IntPtr window, IntPtr renderer, PlatformMode mode)
    {
        Window = window;
        Renderer = renderer;
        Mode = mode;
    }

    public static Platform Create(string title, int width, int height, PlatformMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        if (mode == PlatformMode.Offscreen)
            SDL.SetHint(SDL.Hints.VideoDriver, "dummy");

        // Audio is requested but not required: a machine with no sound device should still play.
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events))
            throw new PlatformException($"SDL_Init failed: {SDL.GetError()}");

        var windowFlags = mode == PlatformMode.Windowed ? SDL.WindowFlags.Resizable : SDL.WindowFlags.Hidden;

        var window = SDL.CreateWindow(title, width, height, windowFlags);
        if (window == IntPtr.Zero)
        {
            SDL.Quit();
            throw new PlatformException($"SDL_CreateWindow failed: {SDL.GetError()}");
        }

        // The software renderer is deterministic and needs no GPU, which is what offscreen runs want.
        var rendererName = mode == PlatformMode.Offscreen ? "software" : null;

        var renderer = SDL.CreateRenderer(window, rendererName!);
        if (renderer == IntPtr.Zero)
        {
            SDL.DestroyWindow(window);
            SDL.Quit();
            throw new PlatformException($"SDL_CreateRenderer failed: {SDL.GetError()}");
        }

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.Blend);

        if (mode == PlatformMode.Windowed)
            SDL.SetRenderVSync(renderer, 1);

        return new Platform(window, renderer, mode);
    }

    /// <summary>Current drawable size in pixels, which changes when the user resizes the window.</summary>
    public (int Width, int Height) GetOutputSize()
    {
        if (!SDL.GetRenderOutputSize(Renderer, out var width, out var height))
            throw new PlatformException($"SDL_GetRenderOutputSize failed: {SDL.GetError()}");

        return (width, height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        SDL.DestroyRenderer(Renderer);
        SDL.DestroyWindow(Window);
        SDL.Quit();
    }
}

public enum PlatformMode
{
    /// <summary>A real, visible window for a human to play in.</summary>
    Windowed,

    /// <summary>Hidden window on the dummy video driver, for headless runs and screenshot tests.</summary>
    Offscreen,
}

public sealed class PlatformException(string message) : Exception(message);
