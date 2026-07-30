using Dalamud.Game.ClientState.GamePad;

namespace AllaganPocket.Emulation;

/// <summary>
/// Applies a RetroPad-style positional controller layout to Allagan Retro Pocket's
/// per-system bindings. Dalamud exposes controller inputs as normalized button
/// positions and axes, so the same reliable layout works for XInput, supported
/// DirectInput devices, PlayStation controllers and Nintendo-style controllers.
/// </summary>
internal static class ControllerAutoMapper
{
    public static readonly string[] PresetLabels =
    {
        "Automatic (FFXIV / Dalamud)",
        "Xbox / XInput",
        "DualShock 4",
        "DualSense",
        "Nintendo Switch Pro",
        "Generic DirectInput",
    };

    public static string Label(ControllerAutoMapPreset preset)
    {
        var index = (int)preset;
        return index >= 0 && index < PresetLabels.Length
            ? PresetLabels[index]
            : PresetLabels[0];
    }

    public static void Apply(EmulatorSystemDefinition system, InputBindings bindings)
    {
        bindings.Normalize();

        SetButton(bindings, EmulatorButtons.Up, GamepadButtons.DpadUp);
        SetButton(bindings, EmulatorButtons.Down, GamepadButtons.DpadDown);
        SetButton(bindings, EmulatorButtons.Left, GamepadButtons.DpadLeft);
        SetButton(bindings, EmulatorButtons.Right, GamepadButtons.DpadRight);

        // RetroPad uses positions rather than the letters printed on a specific pad.
        SetButton(bindings, EmulatorButtons.A, GamepadButtons.East);
        SetButton(bindings, EmulatorButtons.B, GamepadButtons.South);
        SetButton(bindings, EmulatorButtons.X, GamepadButtons.North);
        SetButton(bindings, EmulatorButtons.Y, GamepadButtons.West);

        SetButton(bindings, EmulatorButtons.L, GamepadButtons.L1);
        SetButton(bindings, EmulatorButtons.R, GamepadButtons.R1);
        SetButton(bindings, EmulatorButtons.L2, GamepadButtons.L2);
        SetButton(bindings, EmulatorButtons.R2, GamepadButtons.R2);
        SetButton(bindings, EmulatorButtons.L3, GamepadButtons.L3);
        SetButton(bindings, EmulatorButtons.R3, GamepadButtons.R3);
        SetButton(bindings, EmulatorButtons.Start, GamepadButtons.Start);
        SetButton(bindings, EmulatorButtons.Select, GamepadButtons.Select);

        SetAxis(bindings, "LeftStickLeft", GamepadAxisDirection.LeftStickLeft);
        SetAxis(bindings, "LeftStickRight", GamepadAxisDirection.LeftStickRight);
        SetAxis(bindings, "LeftStickUp", GamepadAxisDirection.LeftStickUp);
        SetAxis(bindings, "LeftStickDown", GamepadAxisDirection.LeftStickDown);
        SetAxis(bindings, "RightStickLeft", GamepadAxisDirection.RightStickLeft);
        SetAxis(bindings, "RightStickRight", GamepadAxisDirection.RightStickRight);
        SetAxis(bindings, "RightStickUp", GamepadAxisDirection.RightStickUp);
        SetAxis(bindings, "RightStickDown", GamepadAxisDirection.RightStickDown);

        // Nintendo 64 C-buttons use the right stick by default. The same right-stick
        // mapping is also used by the Nintendo DS virtual touch cursor.
        SetAxis(bindings, "CUp", GamepadAxisDirection.RightStickUp);
        SetAxis(bindings, "CDown", GamepadAxisDirection.RightStickDown);
        SetAxis(bindings, "CLeft", GamepadAxisDirection.RightStickLeft);
        SetAxis(bindings, "CRight", GamepadAxisDirection.RightStickRight);

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            // The frontend translates these original N64 labels to RetroPad B/Y for
            // Mupen64Plus. Match RetroArch's natural layout: A on the bottom face
            // button and B on the left face button.
            SetButton(bindings, EmulatorButtons.A, GamepadButtons.South);
            SetButton(bindings, EmulatorButtons.B, GamepadButtons.West);
        }
    }

    private static void SetButton(InputBindings bindings, EmulatorButtons action, GamepadButtons button) =>
        SetSecondary(bindings.For(action), InputChord.FromGamepad(button));

    private static void SetAxis(InputBindings bindings, string action, GamepadAxisDirection direction) =>
        SetSecondary(bindings.For(action), InputChord.FromGamepadAxis(direction));

    private static void SetSecondary(InputActionBinding binding, InputChord chord)
    {
        binding.Secondary = chord;
        binding.Normalize();
    }
}
