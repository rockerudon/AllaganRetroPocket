namespace AllaganPocket.Emulation;

internal enum EmulatorInputProfile : byte
{
    Standard,
    SegaSixButton,
    PcEngine,
    NeoGeo,
    NeoGeoPocket,
    WonderSwan,
    PlayStation,
    Nintendo64,
    NintendoDs,
    PlayStationPortable,
}

internal sealed record EmulatorFirmwareDefinition(string FileName, string Description, bool Required, bool ManagedByPlugin = false);

internal sealed record EmulatorSystemDefinition(
    string Id,
    string Name,
    string ShortName,
    string CoreFileName,
    EmulatorButtons Controls,
    params string[] Extensions)
{
    public EmulatorInputProfile InputProfile { get; init; } = EmulatorInputProfile.Standard;
    public bool DiscBased { get; init; }
    public string Description { get; init; } = string.Empty;
    public string SaveDescription { get; init; } = "Battery-backed cartridge save";
    public IReadOnlyList<EmulatorFirmwareDefinition> Firmware { get; init; } =
        Array.Empty<EmulatorFirmwareDefinition>();
    public IReadOnlyDictionary<string, string> DefaultCoreOptions { get; init; } =
        new Dictionary<string, string>();

    public bool Supports(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}

internal sealed record RomEntry(string Path, EmulatorSystemDefinition System)
{
    public string Title => global::System.IO.Path.GetFileNameWithoutExtension(Path);
}

internal static class EmulatorSystemCatalog
{
    private const EmulatorButtons Directions = EmulatorButtons.Up | EmulatorButtons.Down |
        EmulatorButtons.Left | EmulatorButtons.Right;
    private const EmulatorButtons TwoButtonPad = Directions | EmulatorButtons.A | EmulatorButtons.B |
        EmulatorButtons.Start | EmulatorButtons.Select;
    private const EmulatorButtons SixButtonPad = TwoButtonPad | EmulatorButtons.X | EmulatorButtons.Y |
        EmulatorButtons.L | EmulatorButtons.R;
    private const EmulatorButtons PlayStationPad = SixButtonPad | EmulatorButtons.L2 | EmulatorButtons.R2 |
        EmulatorButtons.L3 | EmulatorButtons.R3;
    private const EmulatorButtons Nintendo64Pad = Directions | EmulatorButtons.A | EmulatorButtons.B |
        EmulatorButtons.L | EmulatorButtons.R | EmulatorButtons.L2 | EmulatorButtons.Start;
    private const EmulatorButtons NintendoDsPad = SixButtonPad | EmulatorButtons.L2 | EmulatorButtons.R2 |
        EmulatorButtons.L3 | EmulatorButtons.R3;

    public static readonly EmulatorSystemDefinition GameBoy = new(
        "gb", "Game Boy Color", "Game Boy Color", "sameboy_libretro.dll", TwoButtonPad, ".gb", ".gbc")
    {
        Description = "Game Boy and Game Boy Color through SameBoy.",
    };

    public static readonly EmulatorSystemDefinition GameBoyAdvance = new(
        "gba", "Game Boy Advance", "Game Boy Advance", "gpsp_libretro.dll",
        TwoButtonPad | EmulatorButtons.L | EmulatorButtons.R, ".gba")
    {
        Description = "Game Boy Advance through gpSP.",
        Firmware = new[] { new EmulatorFirmwareDefinition("gba_bios.bin", "Game Boy Advance BIOS", false), },
    };

    public static readonly EmulatorSystemDefinition Nes = new(
        "nes", "Nintendo Entertainment System", "Nintendo Entertainment System", "nestopia_libretro.dll", TwoButtonPad,
        ".nes", ".unf", ".unif")
    {
        Description = "Nintendo Entertainment System and Famicom through Nestopia.",
    };

    public static readonly EmulatorSystemDefinition Snes = new(
        "snes", "Super Nintendo Entertainment System", "Super Nintendo Entertainment System", "bsnes_libretro.dll", SixButtonPad,
        ".sfc", ".smc", ".fig", ".swc")
    {
        Description = "Super Nintendo and Super Famicom through bsnes.",
    };

    public static readonly EmulatorSystemDefinition MegaDrive = new(
        "megadrive", "Sega Mega Drive", "Sega Mega Drive", "blastem_libretro.dll", SixButtonPad,
        ".md", ".gen", ".smd", ".68k", ".sgd")
    {
        InputProfile = EmulatorInputProfile.SegaSixButton,
        Description = "Sega Mega Drive and Genesis through BlastEm.",
    };

    public static readonly EmulatorSystemDefinition SegaCd = new(
        "segacd", "Sega CD", "Sega CD", "clownmdemu_libretro.dll", SixButtonPad,
        ".cue", ".iso", ".chd")
    {
        InputProfile = EmulatorInputProfile.SegaSixButton,
        DiscBased = true,
        Description = "Sega CD and Mega-CD through ClownMDEmu.",
        SaveDescription = "Backup RAM",
    };

    public static readonly EmulatorSystemDefinition Sega8Bit = new(
        "sega8", "Sega Master System", "Sega Master System", "smsplus_libretro.dll", TwoButtonPad,
        ".sms", ".gg", ".rom")
    {
        Description = "Sega Master System and Game Gear through SMS Plus GX.",
    };

    public static readonly EmulatorSystemDefinition PcEngine = new(
        "pcengine", "PC Engine", "PC Engine", "geargrafx_libretro.dll", SixButtonPad,
        ".pce", ".sgx", ".hes", ".cue", ".chd")
    {
        InputProfile = EmulatorInputProfile.PcEngine,
        DiscBased = true,
        Description = "PC Engine, TurboGrafx-16, SuperGrafx and CD-ROM² through Geargrafx.",
        SaveDescription = "Backup RAM / MB128 memory",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("syscard3.pce", "Super CD-ROM² System Card 3", true),
            new EmulatorFirmwareDefinition("syscard2.pce", "CD-ROM² System Card 2", false),
        },
        DefaultCoreOptions = Options(("geargrafx_deterministic_netplay", "Enabled")),
    };

    public static readonly EmulatorSystemDefinition NeoGeo = new(
        "neogeo", "Neo Geo", "Neo Geo", "geolith_libretro.dll", SixButtonPad, ".neo")
    {
        InputProfile = EmulatorInputProfile.NeoGeo,
        Description = "Neo Geo AES and MVS through Geolith.",
        SaveDescription = "Memory card / NVRAM",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("aes.zip", "Neo Geo AES BIOS", true),
            new EmulatorFirmwareDefinition("neogeo.zip", "Neo Geo MVS or UniBIOS", true),
        },
    };

    public static readonly EmulatorSystemDefinition NeoGeoPocket = new(
        "ngp", "Neo Geo Pocket Color", "Neo Geo Pocket Color", "mednafen_ngp_libretro.dll",
        Directions | EmulatorButtons.A | EmulatorButtons.B | EmulatorButtons.Y, ".ngp", ".ngc")
    {
        InputProfile = EmulatorInputProfile.NeoGeoPocket,
        Description = "Neo Geo Pocket and Neo Geo Pocket Color through Beetle NeoPop.",
    };

    public static readonly EmulatorSystemDefinition WonderSwan = new(
        "wonderswan", "WonderSwan Color", "WonderSwan Color", "mednafen_wswan_libretro.dll",
        Directions | EmulatorButtons.A | EmulatorButtons.B | EmulatorButtons.X | EmulatorButtons.Y |
        EmulatorButtons.L | EmulatorButtons.R | EmulatorButtons.L2 | EmulatorButtons.Select,
        ".ws", ".wsc", ".pc2")
    {
        InputProfile = EmulatorInputProfile.WonderSwan,
        Description = "WonderSwan and WonderSwan Color through Beetle Cygne.",
    };

    public static readonly EmulatorSystemDefinition PlayStation = new(
        "ps1", "Sony PlayStation", "Sony PlayStation", "pcsx_rearmed_libretro.dll", PlayStationPad,
        ".cue", ".chd", ".pbp", ".m3u", ".toc", ".img", ".mdf", ".iso", ".exe")
    {
        InputProfile = EmulatorInputProfile.PlayStation,
        DiscBased = true,
        Description = "Sony PlayStation with software rendering and multi-disc support.",
        SaveDescription = "Memory card 1 (per game) and optional memory card 2",
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("scph5501.bin", "Recommended PlayStation BIOS", false),
            new EmulatorFirmwareDefinition("scph1001.bin", "Alternative PlayStation BIOS", false),
        },
    };

    public static readonly EmulatorSystemDefinition Nintendo64 = new(
        "n64", "Nintendo 64", "Nintendo 64", "mupen64plus_next_libretro.dll", Nintendo64Pad,
        ".n64", ".v64", ".z64", ".u1")
    {
        InputProfile = EmulatorInputProfile.Nintendo64,
        Description = "Nintendo 64 with the Angrylion software renderer.",
        SaveDescription = "Cartridge save / Controller Pak",
        DefaultCoreOptions = Options(
            ("mupen64plus-rdp-plugin", "angrylion"),
            ("mupen64plus-rsp-plugin", "parallel"),
            ("mupen64plus-pak1", "memory")),
    };

    public static readonly EmulatorSystemDefinition NintendoDs = new(
        "nds", "Nintendo DS", "Nintendo DS", "melondsds_libretro.dll", NintendoDsPad, ".nds", ".ids")
    {
        InputProfile = EmulatorInputProfile.NintendoDs,
        Description = "Nintendo DS through melonDS DS with mouse and controller touch-screen input.",
        DefaultCoreOptions = Options(
            ("melonds_console_mode", "ds"),
            ("melonds_boot_mode", "direct"),
            ("melonds_sysfile_mode", "builtin"),
            ("melonds_render_mode", "software"),
            ("melonds_threaded_renderer", "enabled"),
            ("melonds_touch_mode", "auto"),
            ("melonds_show_cursor", "timeout"),
            ("melonds_mic_input", "blow"),
            ("melonds_mic_input_active", "hold"),
            ("melonds_network_mode", "disabled"),
            ("melonds_number_of_screen_layouts", "1"),
            ("melonds_screen_layout1", "top-bottom"),
            ("melonds_screen_layout2", "top-bottom")),
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition("bios7.bin", "Nintendo DS ARM7 BIOS", false),
            new EmulatorFirmwareDefinition("bios9.bin", "Nintendo DS ARM9 BIOS", false),
            new EmulatorFirmwareDefinition("firmware.bin", "Nintendo DS firmware", false),
        },
    };

    public static readonly EmulatorSystemDefinition PlayStationPortable = new(
        "psp", "PlayStation Portable", "PlayStation Portable", "ppsspp_libretro.dll", SixButtonPad,
        ".iso", ".cso", ".chd", ".pbp", ".elf", ".prx")
    {
        InputProfile = EmulatorInputProfile.PlayStationPortable,
        DiscBased = true,
        Description = "PlayStation Portable through PPSSPP with software rendering.",
        SaveDescription = "Memory Stick save data",
        DefaultCoreOptions = Options(
            ("ppsspp_backend", "none"),
            ("ppsspp_software_rendering", "enabled"),
            ("ppsspp_internal_resolution", "480x272"),
            ("ppsspp_frameskip", "disabled"),
            ("ppsspp_auto_frameskip", "disabled"),
            ("ppsspp_memstick_inserted", "enabled"),
            ("ppsspp_language", "Automatic"),
            ("ppsspp_enable_wlan", "disabled"),
            ("ppsspp_enable_builtin_pro_ad_hoc_server", "disabled"),
            ("ppsspp_change_pro_ad_hoc_server_address", "socom.cc")),
        Firmware = new[]
        {
            new EmulatorFirmwareDefinition(Path.Combine("PPSSPP", "flash0"), "PPSSPP system assets", true, true),
            new EmulatorFirmwareDefinition(Path.Combine("PPSSPP", "font_atlas.zim"), "PPSSPP font atlas", true, true),
        },
    };

    public static IReadOnlyList<EmulatorSystemDefinition> All { get; } = new[]
    {
        GameBoy, GameBoyAdvance, Nes, Snes, MegaDrive, SegaCd, Sega8Bit,
        PcEngine, NeoGeo, NeoGeoPocket, WonderSwan, PlayStation, Nintendo64, NintendoDs,
        PlayStationPortable,
    };

    public static EmulatorSystemDefinition? ById(string id) =>
        All.FirstOrDefault(system => string.Equals(system.Id, id, StringComparison.OrdinalIgnoreCase));

    public static EmulatorSystemDefinition? Resolve(string path)
    {
        var candidates = All.Where(system => system.Supports(path)).ToArray();
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        var hint = Path.GetDirectoryName(path) ?? string.Empty;
        foreach (var system in candidates)
        {
            if (hint.Contains(system.Id, StringComparison.OrdinalIgnoreCase) ||
                hint.Contains(system.ShortName.Replace("/", string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                return system;
            }
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".bin", StringComparison.OrdinalIgnoreCase) ? ResolveBinary(path) : null;
    }

    public static EmulatorSystemDefinition? ResolveWithFolderHint(string path)
    {
        var directory = Path.GetDirectoryName(path);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var name = Path.GetFileName(directory);
            var byFolder = All.FirstOrDefault(system =>
                string.Equals(name, system.Id, StringComparison.OrdinalIgnoreCase));
            if (byFolder is not null && byFolder.Supports(path))
            {
                return byFolder;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return Resolve(path);
    }

    private static IReadOnlyDictionary<string, string> Options(params (string Key, string Value)[] values) =>
        values.ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);

    private static EmulatorSystemDefinition? ResolveBinary(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[0x8000];
            var read = stream.Read(header);
            var data = header[..read];
            if (HasAscii(data, 0x100, "SEGA")) return MegaDrive;
            if (HasAscii(data, 0x1ff0, "TMR SEGA") || HasAscii(data, 0x3ff0, "TMR SEGA") ||
                HasAscii(data, 0x7ff0, "TMR SEGA")) return Sega8Bit;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EmulatorLog.Warning($"[Allagan Retro Pocket] Could not identify '{path}': {exception.Message}");
        }

        return null;
    }

    private static bool HasAscii(ReadOnlySpan<byte> data, int offset, string value)
    {
        if (offset < 0 || offset + value.Length > data.Length) return false;
        for (var index = 0; index < value.Length; index++)
        {
            if (data[offset + index] != value[index]) return false;
        }

        return true;
    }
}
