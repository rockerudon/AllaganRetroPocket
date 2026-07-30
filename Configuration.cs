using Dalamud.Configuration;
using Dalamud.Game.ClientState.GamePad;
using AllaganPocket.Emulation;

namespace AllaganPocket;

internal enum GameplayScaleMode : byte
{
    Fit,
    Stretch,
    Integer,
}

internal enum GameplayAspectMode : byte
{
    Core,
    FourThree,
    SixteenNine,
}

internal enum FastForwardActivationMode : byte
{
    Hold,
    Toggle,
}

internal enum ControllerAutoMapPreset : byte
{
    Automatic,
    XboxXInput,
    DualShock4,
    DualSense,
    NintendoSwitchPro,
    GenericDirectInput,
}

[Serializable]
internal sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 14;
    public List<string> RomFolders { get; set; } = new();
    public List<RomFileRecord> RomFiles { get; set; } = new();
    public List<string> Favorites { get; set; } = new();
    public List<RecentGameRecord> RecentGames { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> CoreOptions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public EmulatorVideoFilter VideoFilter { get; set; } = EmulatorVideoFilter.Pixel;
    public float Volume { get; set; } = 0.65f;
    public bool AudioMuted { get; set; }
    public bool MuteFastForward { get; set; } = true;
    public int AudioLatencyMs { get; set; } = 90;
    public int FastForwardSpeed { get; set; } = 3;
    public FastForwardActivationMode FastForwardMode { get; set; } = FastForwardActivationMode.Hold;
    public bool AutoSaveState { get; set; } = true;
    public bool AutoLoadState { get; set; } = true;
    public int SaveStateSlot { get; set; } = 1;
    public bool ProtectSaveMemoryOnStateLoad { get; set; } = true;
    public bool StartInListView { get; set; }
    public int QuickMenuKey { get; set; } = 0x70;
    public int FastForwardKey { get; set; } = 0x09;
    public InputActionBinding QuickMenuBinding { get; set; } = new()
    {
        Primary = InputChord.FromKeyboard(0x70),
        Secondary = InputChord.FromGamepad(GamepadButtons.Start, GamepadButtons.Select),
    };
    public InputActionBinding FastForwardBinding { get; set; } = new()
    {
        Primary = InputChord.FromKeyboard(0x09),
    };
    public InputActionBinding FastForwardToggleBinding { get; set; } = new();
    public ControllerAutoMapPreset ControllerPreset { get; set; } = ControllerAutoMapPreset.Automatic;
    public InputBindings Input { get; set; } = new();
    public Dictionary<string, InputBindings> CoreInputs { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ControllerTypes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public float WindowWidth { get; set; } = 1040f;
    public float WindowOpacity { get; set; } = 0.90f;
    public float WindowHeight { get; set; } = 660f;
    public float SidebarWidth { get; set; } = 320f;
    public float DetailsPanelWidth { get; set; } = 310f;
    public float LibraryCardScale { get; set; } = 1f;
    public GameplayScaleMode GameplayScale { get; set; } = GameplayScaleMode.Fit;
    public GameplayAspectMode GameplayAspect { get; set; } = GameplayAspectMode.Core;
    public bool PauseWhenUnfocused { get; set; } = true;
    public bool ShowGameplayToolbar { get; set; } = true;

    public void Normalize()
    {
        if (Version < 3)
        {
            if (MathF.Abs(WindowWidth - 1120f) < 1f && MathF.Abs(WindowHeight - 720f) < 1f)
            {
                WindowWidth = 1040f;
                WindowHeight = 660f;
            }
            if (MathF.Abs(SidebarWidth - 220f) < 1f)
            {
                SidebarWidth = 300f;
            }
            Version = 3;
        }

        if (Version < 4)
        {
            if (AudioLatencyMs <= 0) AudioLatencyMs = 90;
            if (WindowOpacity <= 0f) WindowOpacity = 0.90f;
            Version = 4;
        }

        if (Version < 5)
        {
            QuickMenuBinding ??= new InputActionBinding();
            FastForwardBinding ??= new InputActionBinding();
            QuickMenuBinding.Normalize();
            FastForwardBinding.Normalize();
            if (QuickMenuBinding.Primary.IsEmpty)
                QuickMenuBinding.Primary = InputChord.FromKeyboard(QuickMenuKey);
            if (QuickMenuBinding.Secondary.IsEmpty)
                QuickMenuBinding.Secondary = InputChord.FromGamepad(GamepadButtons.Start, GamepadButtons.Select);
            if (FastForwardBinding.Primary.IsEmpty)
                FastForwardBinding.Primary = InputChord.FromKeyboard(FastForwardKey);
            Version = 5;
        }

        if (Version < 6)
        {
            // 0.90 was the old default and looked almost opaque once the internal
            // panels were composited over the root background.
            if (MathF.Abs(WindowOpacity - 0.90f) < 0.001f) WindowOpacity = 0.78f;
            Version = 6;
        }

        if (Version < 7)
        {
            ControllerTypes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ControllerTypes.TryAdd("ps1", "dualshock");
            Version = 7;
        }

        if (Version < 8)
        {
            SaveStateSlot = Math.Clamp(SaveStateSlot, 1, 10);
            Version = 8;
        }

        if (Version < 9)
        {
            RomFiles ??= new List<RomFileRecord>();
            // The previous default was intentionally translucent, but made dialogs and
            // other Dalamud windows show through too strongly. Preserve custom values
            // while moving users on the old default to the more readable value.
            if (MathF.Abs(WindowOpacity - 0.78f) < 0.001f) WindowOpacity = 0.90f;
            Version = 9;
        }

        if (Version < 10)
        {
            // v10 only changes library presentation. Keep the user's existing Grid/List choice.
            Version = 10;
        }

        if (Version < 11)
        {
            // Give long console names a little more room without changing custom widths.
            if (MathF.Abs(SidebarWidth - 300f) < 0.001f) SidebarWidth = 320f;
            Version = 11;
        }

        if (Version < 12)
        {
            // v12 exposes the Nintendo DS core's existing right-stick touch controls
            // with friendly labels. The old cursor default only showed while pressing,
            // which made aiming with a stick difficult, so migrate that previous default
            // to the short visibility timeout used by the new controller flow.
            if (CoreOptions is not null &&
                CoreOptions.TryGetValue("nds", out var ndsOptions) && ndsOptions is not null &&
                ndsOptions.TryGetValue("melonds_show_cursor", out var cursorMode) &&
                string.Equals(cursorMode, "touching", StringComparison.OrdinalIgnoreCase))
            {
                ndsOptions["melonds_show_cursor"] = "timeout";
            }
            Version = 12;
        }

        if (Version < 13)
        {
            FastForwardBinding ??= new InputActionBinding();
            FastForwardToggleBinding ??= new InputActionBinding();
            FastForwardBinding.Normalize();
            FastForwardToggleBinding.Normalize();

            // v11 used one shortcut plus a Hold/Toggle behavior selector. Preserve a
            // toggle user's shortcut by moving it to the new dedicated toggle binding.
            if (FastForwardMode == FastForwardActivationMode.Toggle &&
                FastForwardToggleBinding.Primary.IsEmpty &&
                FastForwardToggleBinding.Secondary.IsEmpty)
            {
                FastForwardToggleBinding = FastForwardBinding.Clone();
                FastForwardBinding = new InputActionBinding();
            }
            FastForwardMode = FastForwardActivationMode.Hold;
            Version = 13;
        }

        if (Version < 14)
        {
            // v14 reorganizes existing save-state resume controls under Storage & media
            // and adds BIOS management. No user setting needs conversion.
            Version = 14;
        }

        RomFolders ??= new List<string>();
        RomFiles ??= new List<RomFileRecord>();
        Favorites ??= new List<string>();
        RecentGames ??= new List<RecentGameRecord>();
        RomFolders = RomFolders
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        RomFiles = RomFiles
            .Where(static record => record is not null && !string.IsNullOrWhiteSpace(record.Path))
            .Select(static record => new RomFileRecord
            {
                Path = NormalizePath(record.Path),
                SystemId = record.SystemId?.Trim() ?? string.Empty,
            })
            .GroupBy(static record => record.Path, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        CoreOptions ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Input ??= new InputBindings();
        CoreInputs ??= new Dictionary<string, InputBindings>(StringComparer.OrdinalIgnoreCase);
        ControllerTypes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        QuickMenuBinding ??= new InputActionBinding();
        FastForwardBinding ??= new InputActionBinding();
        FastForwardToggleBinding ??= new InputActionBinding();
        QuickMenuBinding.Normalize();
        FastForwardBinding.Normalize();
        FastForwardToggleBinding.Normalize();
        Input.Normalize();
        CoreOptions = CoreOptions
            .Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key,
                static pair => new Dictionary<string, string>(pair.Value, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);
        CoreInputs = CoreInputs
            .Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        ControllerTypes = ControllerTypes
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value,
                StringComparer.OrdinalIgnoreCase);
        ControllerTypes.TryAdd("ps1", "dualshock");
        foreach (var bindings in CoreInputs.Values) bindings.Normalize();
        Volume = Math.Clamp(Volume, 0f, 1f);
        WindowOpacity = Math.Clamp(WindowOpacity, 0.20f, 1f);
        FastForwardSpeed = Math.Clamp(FastForwardSpeed, 2, 8);
        AudioLatencyMs = Math.Clamp(AudioLatencyMs, 30, 250);
        SaveStateSlot = Math.Clamp(SaveStateSlot, 1, 10);
        WindowWidth = Math.Clamp(WindowWidth, 760f, 3840f);
        WindowHeight = Math.Clamp(WindowHeight, 480f, 2160f);
        SidebarWidth = Math.Clamp(SidebarWidth, 270f, 380f);
        DetailsPanelWidth = Math.Clamp(DetailsPanelWidth, 270f, 480f);
        LibraryCardScale = Math.Clamp(LibraryCardScale, 0.8f, 1.5f);
        if (!Enum.IsDefined(typeof(GameplayScaleMode), GameplayScale)) GameplayScale = GameplayScaleMode.Fit;
        if (!Enum.IsDefined(typeof(GameplayAspectMode), GameplayAspect)) GameplayAspect = GameplayAspectMode.Core;
        if (!Enum.IsDefined(typeof(FastForwardActivationMode), FastForwardMode))
            FastForwardMode = FastForwardActivationMode.Hold;
        if (!Enum.IsDefined(typeof(ControllerAutoMapPreset), ControllerPreset))
            ControllerPreset = ControllerAutoMapPreset.Automatic;
        foreach (var system in EmulatorSystemCatalog.All)
        {
            _ = OptionsFor(system);
            _ = InputFor(system);
        }
    }

    public Dictionary<string, string> OptionsFor(EmulatorSystemDefinition system)
    {
        if (!CoreOptions.TryGetValue(system.Id, out var options))
        {
            options = new Dictionary<string, string>(StringComparer.Ordinal);
            CoreOptions[system.Id] = options;
        }

        foreach (var pair in system.DefaultCoreOptions)
        {
            options.TryAdd(pair.Key, pair.Value);
        }

        return options;
    }

    public InputBindings InputFor(EmulatorSystemDefinition system)
    {
        if (!CoreInputs.TryGetValue(system.Id, out var bindings))
        {
            bindings = Input.Clone();
            CoreInputs[system.Id] = bindings;
        }

        return bindings;
    }

    public string ControllerTypeFor(EmulatorSystemDefinition system)
    {
        if (ControllerTypes.TryGetValue(system.Id, out var controllerType) &&
            !string.IsNullOrWhiteSpace(controllerType))
        {
            return controllerType;
        }

        return system.InputProfile == EmulatorInputProfile.PlayStation ? "dualshock" : "standard";
    }

    public bool UsesAnalogController(EmulatorSystemDefinition system) =>
        system.InputProfile == EmulatorInputProfile.PlayStation &&
        string.Equals(ControllerTypeFor(system), "dualshock", StringComparison.OrdinalIgnoreCase);

    public bool IsFavorite(string path) =>
        Favorites.Contains(NormalizePath(path), StringComparer.OrdinalIgnoreCase);

    public void SetFavorite(string path, bool favorite)
    {
        var normalized = NormalizePath(path);
        Favorites.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        if (favorite)
        {
            Favorites.Add(normalized);
        }
    }

    public void AddRecent(RomEntry game)
    {
        var normalized = NormalizePath(game.Path);
        RecentGames.RemoveAll(item => string.Equals(item.Path, normalized, StringComparison.OrdinalIgnoreCase));
        RecentGames.Insert(0, new RecentGameRecord
        {
            Path = normalized,
            SystemId = game.System.Id,
            LastPlayedUtc = DateTime.UtcNow,
        });
        if (RecentGames.Count > 30)
        {
            RecentGames.RemoveRange(30, RecentGames.Count - 30);
        }
    }

    public DateTime? LastPlayed(string path)
    {
        var normalized = NormalizePath(path);
        return RecentGames.FirstOrDefault(item =>
            string.Equals(item.Path, normalized, StringComparison.OrdinalIgnoreCase))?.LastPlayedUtc;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}


[Serializable]
internal sealed class RomFileRecord
{
    public string Path { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
}

[Serializable]
internal sealed class RecentGameRecord
{
    public string Path { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public DateTime LastPlayedUtc { get; set; }
}

[Serializable]
internal sealed class InputBindings
{
    // Legacy fields are retained for automatic migration from v4 configurations.
    public int KeyUp { get; set; } = 0x26;
    public int KeyDown { get; set; } = 0x28;
    public int KeyLeft { get; set; } = 0x25;
    public int KeyRight { get; set; } = 0x27;
    public int KeyA { get; set; } = 0x58;
    public int KeyB { get; set; } = 0x5A;
    public int KeyX { get; set; } = 0x43;
    public int KeyY { get; set; } = 0x56;
    public int KeyL { get; set; } = 0x41;
    public int KeyR { get; set; } = 0x53;
    public int KeyL2 { get; set; } = 0x51;
    public int KeyR2 { get; set; } = 0x57;
    public int KeyL3 { get; set; } = 0x44;
    public int KeyR3 { get; set; } = 0x46;
    public int KeyStart { get; set; } = 0x0D;
    public int KeySelect { get; set; } = 0x08;
    public int KeyCUp { get; set; } = 0x49;
    public int KeyCDown { get; set; } = 0x4B;
    public int KeyCLeft { get; set; } = 0x4A;
    public int KeyCRight { get; set; } = 0x4C;

    public Dictionary<string, InputActionBinding> Actions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        Actions ??= new Dictionary<string, InputActionBinding>(StringComparer.OrdinalIgnoreCase);
        Actions = Actions
            .Where(static pair => pair.Value is not null)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var action in Actions.Values) action.Normalize();

        // Add defaults only for actions that are missing entirely. An existing empty
        // action is intentional: it means the user cleared that binding.
        Ensure(EmulatorButtons.Up, KeyUp, GamepadButtons.DpadUp);
        Ensure(EmulatorButtons.Down, KeyDown, GamepadButtons.DpadDown);
        Ensure(EmulatorButtons.Left, KeyLeft, GamepadButtons.DpadLeft);
        Ensure(EmulatorButtons.Right, KeyRight, GamepadButtons.DpadRight);
        Ensure(EmulatorButtons.A, KeyA, GamepadButtons.East);
        Ensure(EmulatorButtons.B, KeyB, GamepadButtons.South);
        Ensure(EmulatorButtons.X, KeyX, GamepadButtons.North);
        Ensure(EmulatorButtons.Y, KeyY, GamepadButtons.West);
        Ensure(EmulatorButtons.L, KeyL, GamepadButtons.L1);
        Ensure(EmulatorButtons.R, KeyR, GamepadButtons.R1);
        Ensure(EmulatorButtons.L2, KeyL2, GamepadButtons.L2);
        Ensure(EmulatorButtons.R2, KeyR2, GamepadButtons.R2);
        Ensure(EmulatorButtons.L3, KeyL3, GamepadButtons.L3);
        Ensure(EmulatorButtons.R3, KeyR3, GamepadButtons.R3);
        Ensure(EmulatorButtons.Start, KeyStart, GamepadButtons.Start);
        Ensure(EmulatorButtons.Select, KeySelect, GamepadButtons.Select);
        Ensure("CUp", KeyCUp, GamepadAxisDirection.RightStickUp);
        Ensure("CDown", KeyCDown, GamepadAxisDirection.RightStickDown);
        Ensure("CLeft", KeyCLeft, GamepadAxisDirection.RightStickLeft);
        Ensure("CRight", KeyCRight, GamepadAxisDirection.RightStickRight);
        EnsureAxis("LeftStickUp", GamepadAxisDirection.LeftStickUp);
        EnsureAxis("LeftStickDown", GamepadAxisDirection.LeftStickDown);
        EnsureAxis("LeftStickLeft", GamepadAxisDirection.LeftStickLeft);
        EnsureAxis("LeftStickRight", GamepadAxisDirection.LeftStickRight);
        EnsureAxis("RightStickUp", GamepadAxisDirection.RightStickUp);
        EnsureAxis("RightStickDown", GamepadAxisDirection.RightStickDown);
        EnsureAxis("RightStickLeft", GamepadAxisDirection.RightStickLeft);
        EnsureAxis("RightStickRight", GamepadAxisDirection.RightStickRight);
    }

    public InputActionBinding For(EmulatorButtons button) => For(button.ToString());

    public InputActionBinding For(string id)
    {
        if (!Actions.TryGetValue(id, out var action))
        {
            action = new InputActionBinding();
            Actions[id] = action;
        }
        action.Normalize();
        return action;
    }

    public void Set(string id, InputActionBinding binding)
    {
        binding.Normalize();
        Actions[id] = binding;
    }

    public InputBindings Clone()
    {
        var clone = new InputBindings
        {
            KeyUp = KeyUp, KeyDown = KeyDown, KeyLeft = KeyLeft, KeyRight = KeyRight,
            KeyA = KeyA, KeyB = KeyB, KeyX = KeyX, KeyY = KeyY,
            KeyL = KeyL, KeyR = KeyR, KeyL2 = KeyL2, KeyR2 = KeyR2,
            KeyL3 = KeyL3, KeyR3 = KeyR3, KeyStart = KeyStart, KeySelect = KeySelect,
            KeyCUp = KeyCUp, KeyCDown = KeyCDown, KeyCLeft = KeyCLeft, KeyCRight = KeyCRight,
            Actions = Actions.ToDictionary(static pair => pair.Key, static pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase),
        };
        clone.Normalize();
        return clone;
    }

    private void Ensure(EmulatorButtons button, int legacyKey, GamepadButtons gamepad) =>
        Ensure(button.ToString(), legacyKey, gamepad);

    private void Ensure(string id, int legacyKey, GamepadButtons? gamepad = null) =>
        Ensure(id, legacyKey, gamepad, null);

    private void Ensure(string id, int legacyKey, GamepadAxisDirection gamepadAxis) =>
        Ensure(id, legacyKey, null, gamepadAxis);

    private void Ensure(string id, int legacyKey, GamepadButtons? gamepad,
        GamepadAxisDirection? gamepadAxis)
    {
        if (Actions.ContainsKey(id)) return;
        Actions[id] = new InputActionBinding
        {
            Primary = InputChord.FromKeyboard(legacyKey),
            Secondary = gamepad is { } button
                ? InputChord.FromGamepad(button)
                : gamepadAxis is { } direction
                    ? InputChord.FromGamepadAxis(direction)
                    : new InputChord(),
        };
    }

    private void EnsureAxis(string id, GamepadAxisDirection direction)
    {
        if (Actions.ContainsKey(id)) return;
        Actions[id] = new InputActionBinding
        {
            Secondary = InputChord.FromGamepadAxis(direction),
        };
    }
}
