using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using System.Reflection;

namespace AllaganPocket.Frontend;

internal static class FrontendTheme
{
    public static readonly Vector4 Window = new(0.012f, 0.012f, 0.014f, 1f);
    public static readonly Vector4 Sidebar = new(0.020f, 0.020f, 0.023f, 0.30f);
    public static readonly Vector4 Panel = new(0.030f, 0.030f, 0.034f, 0.14f);
    public static readonly Vector4 PanelRaised = new(0.045f, 0.045f, 0.050f, 0.58f);
    public static readonly Vector4 PanelHover = new(0.075f, 0.075f, 0.082f, 0.72f);
    public static readonly Vector4 Border = new(0.145f, 0.145f, 0.155f, 1f);
    public static readonly Vector4 Text = new(0.93f, 0.93f, 0.95f, 1f);
    public static readonly Vector4 Muted = new(0.58f, 0.58f, 0.62f, 1f);
    public static readonly Vector4 Accent = new(0.76f, 0.76f, 0.80f, 1f);
    public static readonly Vector4 AccentSoft = new(0.115f, 0.115f, 0.128f, 0.58f);
    public static readonly Vector4 AccentGold = new(0.88f, 0.68f, 0.26f, 1f);
    public static readonly Vector4 Success = new(0.34f, 0.72f, 0.46f, 1f);
    public static readonly Vector4 Warning = new(0.90f, 0.65f, 0.26f, 1f);
    public static readonly Vector4 Danger = new(0.88f, 0.30f, 0.34f, 1f);
    public static readonly Vector4 Overlay = new(0.005f, 0.005f, 0.007f, 0.86f);
    public static readonly Vector4 Dialog = new(0.020f, 0.020f, 0.024f, 0.98f);
    public static readonly Vector4 DialogPanel = new(0.032f, 0.032f, 0.038f, 0.98f);

    private const int ColorCount = 22;
    private const int VarCount = 8;
    private const int FileDialogColorCount = 28;
    private const int FileDialogVarCount = 7;
    private const int ModalLayoutVarCount = 3;

    public static void Push(float opacity)
    {
        opacity = Math.Clamp(opacity, 0.55f, 1f);
        // BgAlpha on EmulatorWindow is the single source of truth for the root surface.
        // Keeping WindowBg opaque here lets SetNextWindowBgAlpha apply the requested value
        // without any second alpha transform.
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Window);
        // Child windows used to paint another opaque black layer over the root window.
        // Keep them transparent so WindowOpacity controls the whole plugin consistently.
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Dialog);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, ScaleAlpha(PanelRaised, opacity));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ScaleAlpha(PanelHover, opacity));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ScaleAlpha(AccentSoft, opacity));
        ImGui.PushStyleColor(ImGuiCol.Header, ScaleAlpha(AccentSoft, opacity));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ScaleAlpha(PanelHover, opacity));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, ScaleAlpha(WithAlpha(Sidebar, 0.76f), opacity));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, ScaleAlpha(PanelHover, opacity));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, ScaleAlpha(AccentSoft, opacity));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, AccentGold);
        ImGui.PushStyleColor(ImGuiCol.Separator, Border);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, WithAlpha(Accent, 0.22f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, WithAlpha(Accent, 0.58f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, Accent);
        ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0f, 0f, 0f, 0.72f));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 9f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(12f, 9f));
    }

    public static void Pop()
    {
        ImGui.PopStyleVar(VarCount);
        ImGui.PopStyleColor(ColorCount);
    }

    public static void PushModalLayout(float scale)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 14f) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 10f) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10f, 7f) * scale);
    }

    public static void PopModalLayout() => ImGui.PopStyleVar(ModalLayoutVarCount);

    public static void PushFileDialogSurface(float scale)
    {
        // The built-in picker assumes ImGui's compact default spacing. Reusing the
        // main plugin's large padding makes its fixed footer overflow and clips Ok/Cancel.
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Dialog);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, DialogPanel);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Dialog);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, PanelRaised);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.Header, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.055f, 0.055f, 0.064f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, PanelHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.TableHeaderBg, new Vector4(0.12f, 0.12f, 0.13f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBg, new Vector4(0.018f, 0.018f, 0.022f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TableRowBgAlt, new Vector4(0.034f, 0.034f, 0.040f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.NavHighlight, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, AccentGold);
        ImGui.PushStyleColor(ImGuiCol.Separator, Border);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0.012f, 0.012f, 0.015f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new Vector4(0.22f, 0.22f, 0.24f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new Vector4(0.32f, 0.32f, 0.35f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, WithAlpha(Accent, 0.18f));
        ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, new Vector4(0f, 0f, 0f, 0.78f));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 8f) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 4f) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(5f, 3f) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f, 3f) * scale);
    }

    public static void PopFileDialogSurface()
    {
        ImGui.PopStyleVar(FileDialogVarCount);
        ImGui.PopStyleColor(FileDialogColorCount);
    }

    public static IDisposable ApplyFileDialogPalette() => new FileDialogPaletteScope();

    private sealed class FileDialogPaletteScope : IDisposable
    {
        private static readonly FieldInfo[]? PaletteFields = ResolvePaletteFields();
        private static object? themedIconMap;
        private object?[]? originalValues;

        public FileDialogPaletteScope()
        {
            var fields = PaletteFields;
            if (fields is null) return;

            try
            {
                originalValues = new object?[fields.Length];
                for (var index = 0; index < fields.Length; index++)
                    originalValues[index] = fields[index].GetValue(null);

                fields[0].SetValue(null, PanelRaised);
                fields[1].SetValue(null, Text);
                fields[2].SetValue(null, AccentGold);
                fields[3].SetValue(null, Text);
                fields[4].SetValue(null, Muted);
                fields[5].SetValue(null, Text);
                fields[6].SetValue(null, Text);

                // Reuse the themed extension-icon cache after its first build. The original
                // static cache is restored on Dispose, so other plugins are not affected.
                fields[7].SetValue(null, themedIconMap);
            }
            catch
            {
                Restore();
            }
        }

        public void Dispose() => Restore();

        private void Restore()
        {
            var fields = PaletteFields;
            var values = originalValues;
            originalValues = null;
            if (fields is null || values is null) return;

            try
            {
                themedIconMap = fields[7].GetValue(null) ?? themedIconMap;
            }
            catch
            {
                // Optional cache only.
            }

            for (var index = fields.Length - 1; index >= 0; index--)
            {
                try
                {
                    fields[index].SetValue(null, values[index]);
                }
                catch
                {
                    // A future Dalamud implementation may make these fields immutable.
                    // Never let the optional palette override break the file picker.
                }
            }
        }

        private static FieldInfo[]? ResolvePaletteFields()
        {
            try
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                var type = typeof(FileDialog);
                string[] names =
                [
                    "pathDecompColor",
                    "selectedTextColor",
                    "dirTextColor",
                    "codeTextColor",
                    "miscTextColor",
                    "imageTextColor",
                    "standardTextColor",
                    "iconMap",
                ];

                return names.Select(name => type.GetField(name, flags)
                    ?? throw new MissingFieldException(type.FullName, name)).ToArray();
            }
            catch
            {
                return null;
            }
        }
    }

    public static Vector4 WithAlpha(Vector4 value, float alpha) =>
        new(value.X, value.Y, value.Z, alpha);

    public static Vector4 ScaleAlpha(Vector4 value, float opacity) =>
        new(value.X, value.Y, value.Z, value.W * Math.Clamp(opacity, 0f, 1f));

    public static Vector4 SystemColor(string id) => id switch
    {
        "gb" => new Vector4(0.43f, 0.63f, 0.39f, 1f),
        "gba" => new Vector4(0.45f, 0.38f, 0.80f, 1f),
        "nes" => new Vector4(0.74f, 0.27f, 0.28f, 1f),
        "snes" => new Vector4(0.56f, 0.43f, 0.78f, 1f),
        "megadrive" => new Vector4(0.76f, 0.22f, 0.28f, 1f),
        "segacd" => new Vector4(0.28f, 0.39f, 0.76f, 1f),
        "sega8" => new Vector4(0.25f, 0.47f, 0.78f, 1f),
        "pcengine" => new Vector4(0.82f, 0.42f, 0.25f, 1f),
        "neogeo" => new Vector4(0.78f, 0.62f, 0.22f, 1f),
        "ngp" => new Vector4(0.32f, 0.69f, 0.67f, 1f),
        "wonderswan" => new Vector4(0.36f, 0.67f, 0.74f, 1f),
        "ps1" => new Vector4(0.27f, 0.49f, 0.86f, 1f),
        "n64" => new Vector4(0.90f, 0.68f, 0.18f, 1f),
        "nds" => new Vector4(0.33f, 0.67f, 0.91f, 1f),
        "psp" => new Vector4(0.23f, 0.39f, 0.73f, 1f),
        _ => Accent,
    };
}
