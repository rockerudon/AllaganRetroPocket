using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using AllaganPocket.Emulation;

namespace AllaganPocket.Frontend;

internal sealed partial class EmulatorWindow
{
    private static string? IconFileFor(string systemId) => systemId switch
    {
        "gb" => "GBC_1.png",
        "gba" => "GBA.png",
        "nes" => "NES.png",
        "snes" => "SNES.png",
        "megadrive" => "MD_GEN.png",
        "segacd" => "MD_GEN.png",
        "sega8" => "SMS.png",
        "pcengine" => "PCE.png",
        "neogeo" => "MAME.png",
        "ngp" => "NGPC.png",
        "wonderswan" => "WS.png",
        "ps1" => "PS1.png",
        "n64" => "N64.png",
        "nds" => "NDS.png",
        "psp" => "PSP.png",
        _ => null,
    };

    private bool DrawSystemIcon(EmulatorSystemDefinition system, Vector2 size)
    {
        var file = IconFileFor(system.Id);
        if (file is null)
        {
            return false;
        }

        var path = Path.Combine(iconDirectory, file);
        if (!File.Exists(path))
        {
            return false;
        }

        var wrap = textureProvider.GetFromFile(path).GetWrapOrDefault();
        if (wrap is null)
        {
            return false;
        }

        ImGui.Image(wrap.Handle, size);
        return true;
    }

    private bool DrawSystemIconAt(EmulatorSystemDefinition system, Vector2 min, Vector2 size)
    {
        var file = IconFileFor(system.Id);
        if (file is null)
        {
            return false;
        }

        var path = Path.Combine(iconDirectory, file);
        if (!File.Exists(path))
        {
            return false;
        }

        var wrap = textureProvider.GetFromFile(path).GetWrapOrDefault();
        if (wrap is null)
        {
            return false;
        }

        var snappedMin = new Vector2(MathF.Round(min.X), MathF.Round(min.Y));
        var snappedSize = new Vector2(MathF.Round(size.X), MathF.Round(size.Y));
        ImGui.GetWindowDrawList().AddImage(wrap.Handle, snappedMin, snappedMin + snappedSize);
        return true;
    }
}
