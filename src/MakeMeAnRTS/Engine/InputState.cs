using SDL3;
using MakeMeAnRTS.Core;

namespace MakeMeAnRTS.Engine;

/// <summary>
/// A per-frame snapshot of keyboard and mouse. Held state comes from SDL's key array; presses and
/// releases are edges computed against the previous frame, so game code never has to track them.
/// </summary>
public sealed class InputState
{
    private const int MouseButtonCount = 6;

    private readonly HashSet<SDL.Scancode> _down = [];
    private readonly HashSet<SDL.Scancode> _pressed = [];
    private readonly HashSet<SDL.Scancode> _released = [];
    private readonly bool[] _mouseDown = new bool[MouseButtonCount];
    private readonly bool[] _mousePressed = new bool[MouseButtonCount];
    private readonly bool[] _mouseReleased = new bool[MouseButtonCount];

    public Vec2 MousePosition { get; private set; }
    public Vec2 MouseDelta { get; private set; }

    /// <summary>Accumulated wheel ticks this frame; positive is scroll-up (zoom in).</summary>
    public float WheelDelta { get; private set; }

    public bool QuitRequested { get; private set; }

    public bool IsDown(SDL.Scancode key) => _down.Contains(key);
    public bool WasPressed(SDL.Scancode key) => _pressed.Contains(key);
    public bool WasReleased(SDL.Scancode key) => _released.Contains(key);

    public bool IsShiftDown => IsDown(SDL.Scancode.LShift) || IsDown(SDL.Scancode.RShift);
    public bool IsCtrlDown => IsDown(SDL.Scancode.LCtrl) || IsDown(SDL.Scancode.RCtrl);

    public bool IsMouseDown(MouseButton button) => _mouseDown[(int)button];
    public bool WasMousePressed(MouseButton button) => _mousePressed[(int)button];
    public bool WasMouseReleased(MouseButton button) => _mouseReleased[(int)button];

    /// <summary>Drains the SDL event queue and rebuilds this frame's edges. Call exactly once per frame.</summary>
    public void BeginFrame()
    {
        _pressed.Clear();
        _released.Clear();
        Array.Clear(_mousePressed);
        Array.Clear(_mouseReleased);
        WheelDelta = 0f;
        MouseDelta = Vec2.Zero;

        while (SDL.PollEvent(out var sdlEvent))
            Handle(sdlEvent);
    }

    private void Handle(SDL.Event sdlEvent)
    {
        switch ((SDL.EventType)sdlEvent.Type)
        {
            case SDL.EventType.Quit:
                QuitRequested = true;
                break;

            case SDL.EventType.KeyDown:
                // Auto-repeat must not read as a fresh press, or held keys fire orders every frame.
                if (!sdlEvent.Key.Repeat)
                {
                    _down.Add(sdlEvent.Key.Scancode);
                    _pressed.Add(sdlEvent.Key.Scancode);
                }
                break;

            case SDL.EventType.KeyUp:
                _down.Remove(sdlEvent.Key.Scancode);
                _released.Add(sdlEvent.Key.Scancode);
                break;

            case SDL.EventType.MouseMotion:
                var moved = new Vec2(sdlEvent.Motion.X, sdlEvent.Motion.Y);
                MouseDelta += new Vec2(sdlEvent.Motion.XRel, sdlEvent.Motion.YRel);
                MousePosition = moved;
                break;

            case SDL.EventType.MouseButtonDown:
                MousePosition = new Vec2(sdlEvent.Button.X, sdlEvent.Button.Y);
                if (TryMapButton(sdlEvent.Button.Button, out var pressedButton))
                {
                    _mouseDown[(int)pressedButton] = true;
                    _mousePressed[(int)pressedButton] = true;
                }
                break;

            case SDL.EventType.MouseButtonUp:
                MousePosition = new Vec2(sdlEvent.Button.X, sdlEvent.Button.Y);
                if (TryMapButton(sdlEvent.Button.Button, out var releasedButton))
                {
                    _mouseDown[(int)releasedButton] = false;
                    _mouseReleased[(int)releasedButton] = true;
                }
                break;

            case SDL.EventType.MouseWheel:
                WheelDelta += sdlEvent.Wheel.Y;
                break;
        }
    }

    /// <summary>Injects a synthetic key press, used by scripted/headless runs to drive the real input path.</summary>
    public void InjectKeyPress(SDL.Scancode key)
    {
        _down.Add(key);
        _pressed.Add(key);
    }

    public void InjectKeyRelease(SDL.Scancode key)
    {
        _down.Remove(key);
        _released.Add(key);
    }

    public void InjectMousePosition(Vec2 position) => MousePosition = position;

    public void InjectMouseClick(MouseButton button)
    {
        _mouseDown[(int)button] = true;
        _mousePressed[(int)button] = true;
    }

    public void InjectMouseRelease(MouseButton button)
    {
        _mouseDown[(int)button] = false;
        _mouseReleased[(int)button] = true;
    }

    private static bool TryMapButton(byte sdlButton, out MouseButton button)
    {
        switch (sdlButton)
        {
            case 1: button = MouseButton.Left; return true;
            case 2: button = MouseButton.Middle; return true;
            case 3: button = MouseButton.Right; return true;
            default: button = MouseButton.Left; return false;
        }
    }
}

public enum MouseButton
{
    Left = 0,
    Middle = 1,
    Right = 2,
}
