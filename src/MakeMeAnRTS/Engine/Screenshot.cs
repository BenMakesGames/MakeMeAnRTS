using SDL3;

namespace MakeMeAnRTS.Engine;

/// <summary>Saves whatever the renderer last drew to a BMP, the debugging eye for headless runs.</summary>
public static class Screenshot
{
    /// <summary>
    /// Call after drawing and before <c>RenderPresent</c>; the back buffer is what gets read.
    /// </summary>
    public static void Save(IntPtr renderer, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var surface = SDL.RenderReadPixels(renderer, null);
        if (surface == IntPtr.Zero)
            throw new PlatformException($"SDL_RenderReadPixels failed: {SDL.GetError()}");

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (!Image.SaveBMP(surface, path))
                throw new PlatformException($"IMG_SaveBMP failed: {SDL.GetError()}");
        }
        finally
        {
            SDL.DestroySurface(surface);
        }
    }
}
