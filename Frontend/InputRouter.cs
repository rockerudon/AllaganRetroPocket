using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Plugin.Services;
using AllaganPocket.Emulation;

namespace AllaganPocket.Frontend;

internal sealed class InputRouter : IDisposable
{
    private const float CaptureAxisThreshold = 0.70f;
    private const float AnalogDeadzone = 0.20f;

    private static readonly EmulatorButtons[] BindingOrder =
    {
        EmulatorButtons.Up, EmulatorButtons.Down, EmulatorButtons.Left, EmulatorButtons.Right,
        EmulatorButtons.A, EmulatorButtons.B, EmulatorButtons.X, EmulatorButtons.Y,
        EmulatorButtons.L, EmulatorButtons.R, EmulatorButtons.L2, EmulatorButtons.R2,
        EmulatorButtons.L3, EmulatorButtons.R3, EmulatorButtons.Start, EmulatorButtons.Select,
    };

    private static readonly GamepadButtons[] CapturableGamepadButtons =
    {
        GamepadButtons.DpadUp, GamepadButtons.DpadDown, GamepadButtons.DpadLeft, GamepadButtons.DpadRight,
        GamepadButtons.North, GamepadButtons.South, GamepadButtons.West, GamepadButtons.East,
        GamepadButtons.L1, GamepadButtons.L2, GamepadButtons.L3,
        GamepadButtons.R1, GamepadButtons.R2, GamepadButtons.R3,
        GamepadButtons.Start, GamepadButtons.Select,
    };

    private readonly IKeyState keyState;
    private readonly IGamepadState gamepadState;
    private readonly KeyboardInputCapture keyboard = new();
    private readonly Action<object, bool>? gamepadNavigationSetter;
    private readonly Func<object, bool>? gamepadNavigationGetter;
    private bool captured;
    private bool gamepadCaptureActive;
    private bool gamepadNavigationWasEnabled;
    private bool gamepadBlockWasEnabled;
    private bool gamepadUsesImGuiFallback;
    private bool reflectionWarningLogged;

    public InputRouter(IKeyState keyState, IGamepadState gamepadState)
    {
        this.keyState = keyState;
        this.gamepadState = gamepadState;
        var property = gamepadState.GetType().GetProperty("NavEnableGamepad",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        gamepadNavigationSetter = CreateSetter(property);
        gamepadNavigationGetter = CreateGetter(property);
    }

    public bool IsPhysicalKeyDown(int virtualKey) =>
        GetForegroundWindow() == ProcessWindow() && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public bool IsBindingDown(InputActionBinding binding) => BindingStrength(binding) > 0.5f;

    public bool IsChordDown(InputChord chord) => ChordStrength(chord) > 0.5f;

    public IReadOnlyList<InputBindingToken> ReadPressedBindingTokens()
    {
        var result = new List<InputBindingToken>();
        foreach (var key in EmulatorKeyCatalog.SupportedKeys)
        {
            if (IsPhysicalKeyDown(key)) result.Add(InputBindingToken.Keyboard(key));
        }
        foreach (var button in CapturableGamepadButtons)
        {
            if (gamepadState.Raw(button) > 0.5f) result.Add(InputBindingToken.Gamepad(button));
        }

        var left = gamepadState.LeftStick;
        var right = gamepadState.RightStick;
        AddPressedAxis(result, left.X, GamepadAxisDirection.LeftStickLeft,
            GamepadAxisDirection.LeftStickRight);
        AddPressedAxis(result, left.Y, GamepadAxisDirection.LeftStickDown,
            GamepadAxisDirection.LeftStickUp);
        AddPressedAxis(result, right.X, GamepadAxisDirection.RightStickLeft,
            GamepadAxisDirection.RightStickRight);
        AddPressedAxis(result, right.Y, GamepadAxisDirection.RightStickDown,
            GamepadAxisDirection.RightStickUp);
        return result;
    }

    public static string BindingName(InputActionBinding binding,
        ControllerAutoMapPreset preset = ControllerAutoMapPreset.Automatic)
    {
        var primary = binding.Primary.IsEmpty ? string.Empty : ChordName(binding.Primary, preset);
        var secondary = binding.Secondary.IsEmpty ? string.Empty : ChordName(binding.Secondary, preset);
        if (string.IsNullOrEmpty(primary)) return string.IsNullOrEmpty(secondary) ? "Not bound" : secondary;
        return string.IsNullOrEmpty(secondary) ? primary : $"{primary}  /  {secondary}";
    }

    public static string ChordName(InputChord chord,
        ControllerAutoMapPreset preset = ControllerAutoMapPreset.Automatic) =>
        chord is null || chord.Inputs.Count == 0
            ? "Not bound"
            : string.Join(" + ", chord.Inputs.Select(token => TokenName(token, preset)));

    public static string TokenName(InputBindingToken token,
        ControllerAutoMapPreset preset = ControllerAutoMapPreset.Automatic)
    {
        if (token.Device == InputBindingDevice.Keyboard) return EmulatorKeyCatalog.Name(token.Code);
        if (token.Device == InputBindingDevice.GamepadAxis)
        {
            return (GamepadAxisDirection)token.Code switch
            {
                GamepadAxisDirection.LeftStickLeft => "Pad Left Stick Left",
                GamepadAxisDirection.LeftStickRight => "Pad Left Stick Right",
                GamepadAxisDirection.LeftStickUp => "Pad Left Stick Up",
                GamepadAxisDirection.LeftStickDown => "Pad Left Stick Down",
                GamepadAxisDirection.RightStickLeft => "Pad Right Stick Left",
                GamepadAxisDirection.RightStickRight => "Pad Right Stick Right",
                GamepadAxisDirection.RightStickUp => "Pad Right Stick Up",
                GamepadAxisDirection.RightStickDown => "Pad Right Stick Down",
                _ => "Pad Stick",
            };
        }

        return GamepadButtonName((GamepadButtons)token.Code, preset);
    }

    private static string GamepadButtonName(GamepadButtons button, ControllerAutoMapPreset preset)
    {
        if (preset == ControllerAutoMapPreset.XboxXInput)
        {
            return button switch
            {
                GamepadButtons.North => "Pad Y",
                GamepadButtons.South => "Pad A",
                GamepadButtons.West => "Pad X",
                GamepadButtons.East => "Pad B",
                GamepadButtons.L1 => "Pad LB",
                GamepadButtons.L2 => "Pad LT",
                GamepadButtons.L3 => "Pad Left Stick",
                GamepadButtons.R1 => "Pad RB",
                GamepadButtons.R2 => "Pad RT",
                GamepadButtons.R3 => "Pad Right Stick",
                GamepadButtons.Start => "Pad Menu",
                GamepadButtons.Select => "Pad View",
                _ => CommonGamepadButtonName(button),
            };
        }

        if (preset is ControllerAutoMapPreset.DualShock4 or ControllerAutoMapPreset.DualSense)
        {
            return button switch
            {
                GamepadButtons.North => "Pad Triangle",
                GamepadButtons.South => "Pad Cross",
                GamepadButtons.West => "Pad Square",
                GamepadButtons.East => "Pad Circle",
                GamepadButtons.Start => "Pad Options",
                GamepadButtons.Select => preset == ControllerAutoMapPreset.DualSense
                    ? "Pad Create"
                    : "Pad Share",
                _ => CommonGamepadButtonName(button),
            };
        }

        if (preset == ControllerAutoMapPreset.NintendoSwitchPro)
        {
            return button switch
            {
                GamepadButtons.North => "Pad X",
                GamepadButtons.South => "Pad B",
                GamepadButtons.West => "Pad Y",
                GamepadButtons.East => "Pad A",
                GamepadButtons.L1 => "Pad L",
                GamepadButtons.L2 => "Pad ZL",
                GamepadButtons.L3 => "Pad Left Stick",
                GamepadButtons.R1 => "Pad R",
                GamepadButtons.R2 => "Pad ZR",
                GamepadButtons.R3 => "Pad Right Stick",
                GamepadButtons.Start => "Pad Plus",
                GamepadButtons.Select => "Pad Minus",
                _ => CommonGamepadButtonName(button),
            };
        }

        return CommonGamepadButtonName(button);
    }

    private static string CommonGamepadButtonName(GamepadButtons button) => button switch
    {
        GamepadButtons.DpadUp => "Pad D-pad Up",
        GamepadButtons.DpadDown => "Pad D-pad Down",
        GamepadButtons.DpadLeft => "Pad D-pad Left",
        GamepadButtons.DpadRight => "Pad D-pad Right",
        GamepadButtons.North => "Pad North",
        GamepadButtons.South => "Pad South",
        GamepadButtons.West => "Pad West",
        GamepadButtons.East => "Pad East",
        GamepadButtons.L1 => "Pad L1",
        GamepadButtons.L2 => "Pad L2",
        GamepadButtons.L3 => "Pad L3",
        GamepadButtons.R1 => "Pad R1",
        GamepadButtons.R2 => "Pad R2",
        GamepadButtons.R3 => "Pad R3",
        GamepadButtons.Start => "Pad Start",
        GamepadButtons.Select => "Pad Select",
        _ => $"Pad {button}",
    };

    public void SetCaptured(bool value)
    {
        if (captured == value) return;
        captured = value;
        keyboard.SetCaptured(value);
        if (value)
        {
            var io = ImGui.GetIO();
            gamepadCaptureActive = true;
            gamepadUsesImGuiFallback = gamepadNavigationSetter is null;
            if (gamepadUsesImGuiFallback)
            {
                gamepadNavigationWasEnabled = (io.ConfigFlags & ImGuiConfigFlags.NavEnableGamepad) != 0;
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
            }
            else
            {
                gamepadBlockWasEnabled = ReadGamepadCapture();
                WriteGamepadCapture(true);
            }
            keyState.ClearAll();
        }
        else
        {
            RestoreGamepadNavigation();
        }
    }

    public EmulatorInputState Read(EmulatorSystemDefinition system, InputBindings bindings, UiRect pointerRect)
    {
        var buttons = BoundButtons(bindings);
        var physicalLeft = gamepadState.LeftStick;
        var stickDirections = system.InputProfile is not EmulatorInputProfile.Nintendo64 and
            not EmulatorInputProfile.PlayStation and not EmulatorInputProfile.PlayStationPortable;
        if (stickDirections)
        {
            if (physicalLeft.Y > 0.5f) buttons |= EmulatorButtons.Up;
            if (physicalLeft.Y < -0.5f) buttons |= EmulatorButtons.Down;
            if (physicalLeft.X < -0.5f) buttons |= EmulatorButtons.Left;
            if (physicalLeft.X > 0.5f) buttons |= EmulatorButtons.Right;
        }

        if (system.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            var pointer = ReadPointer(pointerRect);
            // melonDS DS uses the Libretro right analog axes for its virtual touch cursor.
            // Read the frontend mappings instead of the physical stick directly so players
            // can remap the cursor to another stick, controller, or keyboard keys.
            var touchJoystick = ReadMappedStick(bindings, "RightStick");
            return new EmulatorInputState(buttons,
                RightX: ToAnalog(touchJoystick.X), RightY: ToAnalog(-touchJoystick.Y),
                PointerX: pointer.X, PointerY: pointer.Y, PointerPressed: pointer.Pressed);
        }

        if (system.InputProfile is not EmulatorInputProfile.Nintendo64 and
            not EmulatorInputProfile.PlayStation and not EmulatorInputProfile.PlayStationPortable)
        {
            return new EmulatorInputState(buttons);
        }

        var left = ReadMappedStick(bindings, "LeftStick");
        var right = system.InputProfile == EmulatorInputProfile.PlayStation
            ? ReadMappedStick(bindings, "RightStick")
            : Vector2.Zero;

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            var logical = buttons;
            buttons &= ~(EmulatorButtons.A | EmulatorButtons.B);
            if ((logical & EmulatorButtons.A) != 0) buttons |= EmulatorButtons.B;
            if ((logical & EmulatorButtons.B) != 0) buttons |= EmulatorButtons.Y;
            if (MathF.Abs(left.X) < AnalogDeadzone)
                left.X = (logical & EmulatorButtons.Left) != 0 ? -1f :
                    (logical & EmulatorButtons.Right) != 0 ? 1f : 0f;
            if (MathF.Abs(left.Y) < AnalogDeadzone)
                left.Y = (logical & EmulatorButtons.Up) != 0 ? 1f :
                    (logical & EmulatorButtons.Down) != 0 ? -1f : 0f;
            buttons &= ~(EmulatorButtons.Up | EmulatorButtons.Down | EmulatorButtons.Left | EmulatorButtons.Right);

            if (IsBindingDown(bindings.For("CUp"))) right.Y -= 1f;
            if (IsBindingDown(bindings.For("CDown"))) right.Y += 1f;
            if (IsBindingDown(bindings.For("CLeft"))) right.X -= 1f;
            if (IsBindingDown(bindings.For("CRight"))) right.X += 1f;
            right.X = Math.Clamp(right.X, -1f, 1f);
            right.Y = Math.Clamp(right.Y, -1f, 1f);
        }

        return new EmulatorInputState(buttons, ToAnalog(left.X), ToAnalog(-left.Y),
            ToAnalog(right.X), ToAnalog(-right.Y));
    }

    public void SuppressGameInput()
    {
        if (!captured) return;
        var io = ImGui.GetIO();
        io.WantCaptureKeyboard = true;
        ImGui.SetNextFrameWantCaptureKeyboard(true);
        keyState.ClearAll();
        if (gamepadNavigationSetter is null) io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad;
    }

    private EmulatorButtons BoundButtons(InputBindings bindings)
    {
        var result = EmulatorButtons.None;
        foreach (var button in BindingOrder)
        {
            if (IsBindingDown(bindings.For(button))) result |= button;
        }
        return result;
    }

    private Vector2 ReadMappedStick(InputBindings bindings, string prefix)
    {
        var left = BindingStrength(bindings.For($"{prefix}Left"));
        var right = BindingStrength(bindings.For($"{prefix}Right"));
        var up = BindingStrength(bindings.For($"{prefix}Up"));
        var down = BindingStrength(bindings.For($"{prefix}Down"));
        return new Vector2(Math.Clamp(right - left, -1f, 1f), Math.Clamp(up - down, -1f, 1f));
    }

    private float BindingStrength(InputActionBinding binding) =>
        MathF.Max(ChordStrength(binding.Primary), ChordStrength(binding.Secondary));

    private float ChordStrength(InputChord chord)
    {
        if (chord is null || chord.Inputs.Count == 0) return 0f;
        var strength = 1f;
        foreach (var token in chord.Inputs)
        {
            strength = MathF.Min(strength, TokenStrength(token));
            if (strength <= 0f) return 0f;
        }
        return strength;
    }

    private float TokenStrength(InputBindingToken token)
    {
        if (token.Device == InputBindingDevice.Keyboard)
            return (captured ? keyboard.IsKeyDown(token.Code) : IsPhysicalKeyDown(token.Code)) ? 1f : 0f;
        if (token.Device == InputBindingDevice.GamepadAxis)
            return AxisStrength((GamepadAxisDirection)token.Code);
        var button = (GamepadButtons)token.Code;
        return button == GamepadButtons.None ? 0f : Math.Clamp(gamepadState.Raw(button), 0f, 1f);
    }

    private float AxisStrength(GamepadAxisDirection direction)
    {
        var left = gamepadState.LeftStick;
        var right = gamepadState.RightStick;
        var value = direction switch
        {
            GamepadAxisDirection.LeftStickLeft => -left.X,
            GamepadAxisDirection.LeftStickRight => left.X,
            GamepadAxisDirection.LeftStickUp => left.Y,
            GamepadAxisDirection.LeftStickDown => -left.Y,
            GamepadAxisDirection.RightStickLeft => -right.X,
            GamepadAxisDirection.RightStickRight => right.X,
            GamepadAxisDirection.RightStickUp => right.Y,
            GamepadAxisDirection.RightStickDown => -right.Y,
            _ => 0f,
        };
        if (value <= AnalogDeadzone) return 0f;
        return Math.Clamp((value - AnalogDeadzone) / (1f - AnalogDeadzone), 0f, 1f);
    }

    private static void AddPressedAxis(ICollection<InputBindingToken> result, float value,
        GamepadAxisDirection negative, GamepadAxisDirection positive)
    {
        if (value <= -CaptureAxisThreshold) result.Add(InputBindingToken.GamepadAxis(negative));
        else if (value >= CaptureAxisThreshold) result.Add(InputBindingToken.GamepadAxis(positive));
    }

    private static (short X, short Y, bool Pressed) ReadPointer(UiRect rect)
    {
        if (rect.Width <= 1f || rect.Height <= 1f) return default;
        var mouse = ImGui.GetMousePos();
        var inside = ImGui.IsMouseHoveringRect(rect.Min, rect.Max, false);
        var pressed = inside && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var x = Math.Clamp((mouse.X - rect.Min.X) / rect.Width, 0f, 1f);
        var y = Math.Clamp((mouse.Y - rect.Min.Y) / rect.Height, 0f, 1f);
        return (ToPointer(x), ToPointer(y), pressed);
    }

    private static short ToPointer(float normalized) =>
        (short)MathF.Round(Math.Clamp(normalized, 0f, 1f) * 65534f - 32767f);
    private static short ToAnalog(float value) =>
        (short)MathF.Round(Math.Clamp(value, -1f, 1f) * short.MaxValue);

    private bool ReadGamepadCapture()
    {
        if (gamepadNavigationGetter is null) return false;
        try { return gamepadNavigationGetter(gamepadState); }
        catch (Exception exception) { LogReflectionFailure(exception); return false; }
    }

    private void WriteGamepadCapture(bool value)
    {
        if (gamepadNavigationSetter is null) return;
        try { gamepadNavigationSetter(gamepadState, value); }
        catch (Exception exception) { LogReflectionFailure(exception); }
    }

    private void RestoreGamepadNavigation()
    {
        if (!gamepadCaptureActive) return;
        if (gamepadUsesImGuiFallback)
        {
            if (!gamepadNavigationWasEnabled) ImGui.GetIO().ConfigFlags &= ~ImGuiConfigFlags.NavEnableGamepad;
        }
        else
        {
            WriteGamepadCapture(gamepadBlockWasEnabled);
        }
        gamepadCaptureActive = false;
        gamepadNavigationWasEnabled = false;
        gamepadBlockWasEnabled = false;
        gamepadUsesImGuiFallback = false;
    }

    private void LogReflectionFailure(Exception exception)
    {
        if (reflectionWarningLogged) return;
        reflectionWarningLogged = true;
        EmulatorLog.Warning($"[Allagan Retro Pocket] Gamepad capture unavailable: {exception.Message}");
    }

    private static Action<object, bool>? CreateSetter(PropertyInfo? property)
    {
        var method = property?.SetMethod;
        if (method?.DeclaringType is null) return null;
        try
        {
            var factory = typeof(InputRouter).GetMethod(nameof(CreateTypedSetter), BindingFlags.Static | BindingFlags.NonPublic);
            return (Action<object, bool>?)factory?.MakeGenericMethod(method.DeclaringType).Invoke(null, new object[] { method });
        }
        catch { return null; }
    }

    private static Action<object, bool> CreateTypedSetter<T>(MethodInfo method)
    {
        var setter = (Action<T, bool>)Delegate.CreateDelegate(typeof(Action<T, bool>), method);
        return (target, value) => setter((T)target, value);
    }

    private static Func<object, bool>? CreateGetter(PropertyInfo? property)
    {
        var method = property?.GetMethod;
        if (method?.DeclaringType is null) return null;
        try
        {
            var factory = typeof(InputRouter).GetMethod(nameof(CreateTypedGetter), BindingFlags.Static | BindingFlags.NonPublic);
            return (Func<object, bool>?)factory?.MakeGenericMethod(method.DeclaringType).Invoke(null, new object[] { method });
        }
        catch { return null; }
    }

    private static Func<object, bool> CreateTypedGetter<T>(MethodInfo method)
    {
        var getter = (Func<T, bool>)Delegate.CreateDelegate(typeof(Func<T, bool>), method);
        return target => getter((T)target);
    }

    public void Dispose()
    {
        SetCaptured(false);
        keyboard.Dispose();
    }

    private static nint ProcessWindow() => System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
