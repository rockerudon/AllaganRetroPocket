using Dalamud.Game.ClientState.GamePad;

namespace AllaganPocket.Emulation;

internal enum InputBindingDevice : byte
{
    Keyboard,
    Gamepad,
    GamepadAxis,
}

internal enum GamepadAxisDirection : byte
{
    LeftStickLeft = 1,
    LeftStickRight,
    LeftStickUp,
    LeftStickDown,
    RightStickLeft,
    RightStickRight,
    RightStickUp,
    RightStickDown,
}

[Serializable]
internal sealed class InputBindingToken
{
    public InputBindingDevice Device { get; set; }
    public int Code { get; set; }

    public InputBindingToken Clone() => new() { Device = Device, Code = Code };

    public static InputBindingToken Keyboard(int virtualKey) =>
        new() { Device = InputBindingDevice.Keyboard, Code = virtualKey };

    public static InputBindingToken Gamepad(GamepadButtons button) =>
        new() { Device = InputBindingDevice.Gamepad, Code = (int)button };

    public static InputBindingToken GamepadAxis(GamepadAxisDirection direction) =>
        new() { Device = InputBindingDevice.GamepadAxis, Code = (int)direction };
}

[Serializable]
internal sealed class InputChord
{
    public List<InputBindingToken> Inputs { get; set; } = new();

    public bool IsEmpty => Inputs.Count == 0;

    public void Normalize()
    {
        Inputs ??= new List<InputBindingToken>();
        Inputs = Inputs
            .Where(static token => token is not null && token.Code != 0)
            .GroupBy(static token => (token.Device, token.Code))
            .Select(static group => group.First())
            .Take(3)
            .ToList();
    }

    public InputChord Clone() => new()
    {
        Inputs = Inputs.Select(static token => token.Clone()).ToList(),
    };

    public static InputChord FromKeyboard(int virtualKey) => new()
    {
        Inputs = new List<InputBindingToken> { InputBindingToken.Keyboard(virtualKey) },
    };

    public static InputChord FromGamepad(params GamepadButtons[] buttons) => new()
    {
        Inputs = buttons.Take(3).Select(InputBindingToken.Gamepad).ToList(),
    };

    public static InputChord FromGamepadAxis(GamepadAxisDirection direction) => new()
    {
        Inputs = new List<InputBindingToken> { InputBindingToken.GamepadAxis(direction) },
    };
}

[Serializable]
internal sealed class InputActionBinding
{
    public InputChord Primary { get; set; } = new();
    public InputChord Secondary { get; set; } = new();

    public void Normalize()
    {
        Primary ??= new InputChord();
        Secondary ??= new InputChord();
        Primary.Normalize();
        Secondary.Normalize();
    }

    public InputActionBinding Clone() => new()
    {
        Primary = Primary.Clone(),
        Secondary = Secondary.Clone(),
    };
}
