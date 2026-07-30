using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using AllaganPocket.Emulation;

namespace AllaganPocket.Frontend;

internal enum FrontendPage : byte
{
    Library,
    Settings,
}

internal enum LibrarySection : byte
{
    All,
    Recent,
    Favorites,
    System,
}

internal sealed partial class EmulatorWindow : Window, IDisposable
{
    private const float MinimumWindowWidth = 760f;
    private const float MinimumWindowHeight = 480f;
    private const float MaximumWindowWidth = 3840f;
    private const float MaximumWindowHeight = 2160f;
    private const float DefaultWindowWidth = 1040f;
    private const float DefaultWindowHeight = 660f;

    private readonly Configuration configuration;
    private readonly string emulatorRoot;
    private readonly string coreDirectory;
    private readonly string iconDirectory;
    private readonly ITextureProvider textureProvider;
    private readonly RomLibrary library;
    private readonly EmulatorVideoTexture video;
    private readonly InputRouter input;
    private readonly FileDialogManager fileDialogs = new();
    private readonly object systemFilesGate = new();
    private IReadOnlyList<RomEntry> games = Array.Empty<RomEntry>();
    private RomEntry? selectedGame;
    private UiRect? selectedLibraryItemRect;
    private Vector2? selectedGameClickPosition;
    private bool preserveGameDetailsForCurrentClick;
    private RomEntry? activeGame;
    private EmulatorSession? session;
    private FrontendPage page = FrontendPage.Library;
    private LibrarySection section = LibrarySection.All;
    private string selectedSystemId = string.Empty;
    private string search = string.Empty;
    private string status = string.Empty;
    private bool listView;
    private bool gameplayVisible;
    private bool quickMenuVisible;
    private bool fastForwardLatched;
    private bool systemFilesInstalled;
    private bool quickKeyWasDown;
    private bool fastForwardToggleKeyWasDown;
    private bool addGamesPopupRequested;
    private bool fileDialogOpen;
    private bool windowSizeDirty;
    private float windowSizeSaveTimer;
    private int forceWindowSizeFrames;
    private Vector2 lastWindowLogicalSize;
    private Vector2 applicationWindowPos;
    private Vector2 applicationWindowSize;
    private string stateMessage = string.Empty;
    private float stateMessageSeconds;

    public EmulatorWindow(DirectoryInfo configDirectory, FileInfo assemblyLocation,
        ITextureProvider textures, IKeyState keyState, IGamepadState gamepadState,
        Configuration configuration)
        : base("Allagan Retro Pocket###AllaganPocket.Main",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        AllowPinning = false;
        AllowClickthrough = false;
        AllowBackgroundBlur = false;
        this.configuration = configuration;
        textureProvider = textures;
        emulatorRoot = Path.Combine(configDirectory.FullName, "Emulator");
        var assemblyDirectory = assemblyLocation.DirectoryName ?? string.Empty;
        coreDirectory = Path.Combine(assemblyDirectory, "Cores");
        iconDirectory = Path.Combine(assemblyDirectory, "Frontend", "Icons");
        library = new RomLibrary(emulatorRoot);
        video = new EmulatorVideoTexture(textures);
        input = new InputRouter(keyState, gamepadState);
        fileDialogs.AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings;
        listView = configuration.StartInListView;
        Size = new Vector2(configuration.WindowWidth, configuration.WindowHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight),
            MaximumSize = new Vector2(MaximumWindowWidth, MaximumWindowHeight),
        };
        lastWindowLogicalSize = Size ?? new Vector2(DefaultWindowWidth, DefaultWindowHeight);
        RefreshLibrary();
    }

    public void OpenSettings()
    {
        var system = activeGame?.System
            ?? EmulatorSystemCatalog.ById(selectedSystemId)
            ?? EmulatorSystemCatalog.All.First();
        selectedSystemId = system.Id;
        section = LibrarySection.System;
        LeaveGameplayForPage(FrontendPage.Settings);
    }

    public void OpenSupport() => OpenSupportPage();

    public override void PreDraw()
    {
        BgAlpha = configuration.WindowOpacity;
        FrontendTheme.Push(configuration.WindowOpacity);
    }

    public override void PostDraw()
    {
        FrontendTheme.Pop();
    }

    public override void Draw()
    {
        TrackWindowSize();
        if (gameplayVisible && session is not null)
        {
            DrawGameplay();
        }
        else
        {
            DrawApplication();
        }

        if (fileDialogOpen)
            DrawFileDialog();
    }

    public override void OnClose()
    {
        input.SetCaptured(false);
        SaveAutoState();
        SaveWindowSizeNow();
    }

    private void DrawFileDialog()
    {
        // The main frontend intentionally uses large spacing and zero window padding.
        // Dalamud's picker has a compact, fixed-width footer, so draw it under an
        // isolated style to keep its path field and Ok/Cancel buttons inside the window.
        FrontendTheme.Pop();
        try
        {
            var scale = ImGuiHelpers.GlobalScale;
            FrontendTheme.PushFileDialogSurface(scale);
            try
            {
                var viewport = ImGui.GetMainViewport();
                var margin = new Vector2(20f, 20f) * scale;
                var usableSize = Vector2.Max(new Vector2(1f), viewport.WorkSize - margin * 2f);
                var maximumSize = Vector2.Min(new Vector2(900f, 620f) * scale, usableSize);
                var minimumSize = Vector2.Min(new Vector2(620f, 410f) * scale, maximumSize);

                ImGui.SetNextWindowPos(
                    viewport.WorkPos + viewport.WorkSize * 0.5f,
                    ImGuiCond.Appearing,
                    new Vector2(0.5f, 0.5f));
                ImGui.SetNextWindowSizeConstraints(minimumSize, maximumSize);
                using var palette = FrontendTheme.ApplyFileDialogPalette();
                fileDialogs.Draw();
            }
            finally
            {
                FrontendTheme.PopFileDialogSurface();
            }
        }
        finally
        {
            // PreDraw/PostDraw own one frontend theme frame. Restore it so PostDraw
            // can pop the exact stack it expects.
            FrontendTheme.Push(configuration.WindowOpacity);
        }
    }

    private void DrawApplication()
    {
        applicationWindowPos = ImGui.GetWindowPos();
        applicationWindowSize = ImGui.GetWindowSize();
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail();
        var sidebarWidth = MathF.Min(configuration.SidebarWidth * scale,
            MathF.Max(280f * scale, available.X * 0.44f));
        ImGui.BeginChild("sidebar", new Vector2(sidebarWidth, available.Y), false,
            ImGuiWindowFlags.NoScrollbar);
        DrawSidebar();
        ImGui.EndChild();
        ImGui.SameLine(0f, 0f);
        ImGui.BeginChild("content", new Vector2(0f, available.Y), false);
        switch (page)
        {
            case FrontendPage.Settings:
                DrawSettings();
                break;
            default:
                DrawLibrary();
                break;
        }
        ImGui.EndChild();
        DrawAddGamesPopup();
    }

    private void DrawSidebar()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var draw = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        draw.AddRectFilled(min, max, ImGui.GetColorU32(FrontendTheme.ScaleAlpha(FrontendTheme.Sidebar, configuration.WindowOpacity)));
        draw.AddLine(new Vector2(max.X - 1f, min.Y), new Vector2(max.X - 1f, max.Y),
            ImGui.GetColorU32(FrontendTheme.Border));

        ImGui.SetCursorPos(new Vector2(18f, 14f) * scale);
        ImGui.TextColored(FrontendTheme.Muted, "LIBRARY");

        var supportWidth = MathF.Min(118f * scale, MathF.Max(96f * scale, ImGui.GetWindowWidth() - 108f * scale));
        ImGui.SetCursorPos(new Vector2(ImGui.GetWindowWidth() - supportWidth - 12f * scale, 8f * scale));
        DrawSupportDeveloperButton(supportWidth, 30f * scale);

        ImGui.SetCursorPos(new Vector2(0f, 44f * scale));
        DrawLibrarySidebarButton("All games", LibrarySection.All);
        DrawLibrarySidebarButton("Recently played", LibrarySection.Recent);
        DrawLibrarySidebarButton("Favorites", LibrarySection.Favorites);

        ImGui.Dummy(new Vector2(1f, 10f) * scale);
        ImGui.SetCursorPosX(18f * scale);
        ImGui.TextColored(FrontendTheme.Muted, "SYSTEMS");

        var resumeHeight = session is null ? 0f : 50f * scale;
        var systemsHeight = MathF.Max(90f * scale, ImGui.GetContentRegionAvail().Y - resumeHeight - 8f * scale);
        ImGui.BeginChild("systems-scroll", new Vector2(0f, systemsHeight), false);
        foreach (var system in EmulatorSystemCatalog.All.OrderBy(static system => system.Name,
                     StringComparer.CurrentCultureIgnoreCase))
        {
            DrawSystemSidebarButton(system);
        }
        ImGui.EndChild();

        if (session is not null)
        {
            ImGui.SetCursorPosX(12f * scale);
            ImGui.PushStyleColor(ImGuiCol.Button, FrontendTheme.AccentSoft);
            if (ImGui.Button("Resume game", new Vector2(ImGui.GetContentRegionAvail().X - 12f * scale,
                    38f * scale)))
            {
                ResumeGame();
            }
            ImGui.PopStyleColor();
        }
    }

    private void DrawSupportDeveloperButton(float width, float height)
    {
        var darkText = new Vector4(0.075f, 0.060f, 0.025f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Text, darkText);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.82f, 0.61f, 0.18f, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, FrontendTheme.AccentGold);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.72f, 0.50f, 0.12f, 1f));
        if (ImGui.Button("Support ♥", new Vector2(width, height)))
        {
            OpenSupportPage();
        }
        ImGui.PopStyleColor(4);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Buy me a coffee");
        }
    }

    private void DrawLibrarySidebarButton(string label, LibrarySection target)
    {
        var selected = page == FrontendPage.Library && section == target;
        if (!DrawSidebarRow($"library-{target}", label, selected, null))
        {
            return;
        }

        page = FrontendPage.Library;
        section = target;
        selectedSystemId = string.Empty;
        selectedGame = null;
        selectedGameClickPosition = null;
    }

    private void DrawSystemSidebarButton(EmulatorSystemDefinition system)
    {
        var selected = section == LibrarySection.System &&
            string.Equals(selectedSystemId, system.Id, StringComparison.OrdinalIgnoreCase) &&
            page is FrontendPage.Settings or FrontendPage.Library;
        if (!DrawSidebarRow($"system-{system.Id}", system.Name, selected, system))
        {
            return;
        }

        page = FrontendPage.Library;
        section = LibrarySection.System;
        selectedSystemId = system.Id;
        selectedGame = null;
        selectedGameClickPosition = null;
    }

    private bool DrawSidebarRow(string id, string label, bool selected, EmulatorSystemDefinition? system)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var height = 44f * scale;
        var position = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();

        var draw = ImGui.GetWindowDrawList();
        if (selected || hovered)
        {
            draw.AddRectFilled(position, position + new Vector2(width, height),
                ImGui.GetColorU32(selected ? FrontendTheme.AccentSoft : FrontendTheme.PanelHover));
        }

        var textX = 18f * scale;
        if (system is not null)
        {
            var color = FrontendTheme.SystemColor(system.Id);
            draw.AddRectFilled(position, position + new Vector2(3f * scale, height),
                ImGui.GetColorU32(selected ? color : FrontendTheme.WithAlpha(color, 0.62f)));
            // Sidebar assets are authored at 32 x 32. Draw them at their native
            // size at 100% UI scale instead of downsampling them to 24 x 24.
            // Rounding avoids half-pixel placement at fractional Dalamud scales.
            var iconPixels = MathF.Round(32f * scale);
            var iconSize = new Vector2(iconPixels, iconPixels);
            var iconPosition = new Vector2(
                MathF.Round(position.X + 14f * scale),
                MathF.Round(position.Y + (height - iconPixels) * 0.5f));
            if (DrawSystemIconAt(system, iconPosition, iconSize))
            {
                textX = 54f * scale;
            }
        }
        else if (selected)
        {
            draw.AddRectFilled(position, position + new Vector2(3f * scale, height),
                ImGui.GetColorU32(FrontendTheme.Accent));
        }

        var textSize = ImGui.CalcTextSize(label);
        draw.AddText(position + new Vector2(textX, (height - textSize.Y) * 0.5f),
            ImGui.GetColorU32(selected ? FrontendTheme.Text : FrontendTheme.Muted), label);
        if (hovered && textSize.X > width - textX - 14f * scale)
        {
            ImGui.SetTooltip(label);
        }
        return clicked;
    }

    private void LeaveGameplayForPage(FrontendPage target)
    {
        gameplayVisible = false;
        quickMenuVisible = false;
        input.SetCaptured(false);
        page = target;
    }

    private void RefreshLibrary()
    {
        games = library.Scan(configuration.RomFolders, configuration.RomFiles);
        if (selectedGame is not null)
        {
            selectedGame = games.FirstOrDefault(game => string.Equals(game.Path, selectedGame.Path,
                StringComparison.OrdinalIgnoreCase));
        }

        status = games.Count == 0
            ? "No games found. Add game files or a ROM folder to get started."
            : $"{games.Count} game(s) found";
    }

    private void TrackWindowSize()
    {
        if (forceWindowSizeFrames > 0)
        {
            forceWindowSizeFrames--;
            if (forceWindowSizeFrames == 0)
            {
                Size = null;
                SizeCondition = ImGuiCond.FirstUseEver;
            }
        }
        if (ImGui.IsWindowCollapsed()) return;
        var scale = MathF.Max(0.01f, ImGuiHelpers.GlobalScale);
        var logical = ImGui.GetWindowSize() / scale;
        logical.X = Math.Clamp(logical.X, MinimumWindowWidth, MaximumWindowWidth);
        logical.Y = Math.Clamp(logical.Y, MinimumWindowHeight, MaximumWindowHeight);

        if (Vector2.DistanceSquared(logical, lastWindowLogicalSize) > 1f)
        {
            lastWindowLogicalSize = logical;
            windowSizeDirty = true;
            windowSizeSaveTimer = 0.65f;
            return;
        }

        if (!windowSizeDirty) return;
        windowSizeSaveTimer -= MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (windowSizeSaveTimer <= 0f)
        {
            SaveWindowSizeNow();
        }
    }

    private void SaveWindowSizeNow()
    {
        if (!windowSizeDirty) return;
        configuration.WindowWidth = lastWindowLogicalSize.X;
        configuration.WindowHeight = lastWindowLogicalSize.Y;
        configuration.Save();
        windowSizeDirty = false;
        windowSizeSaveTimer = 0f;
    }

    private void ResetWindowSize()
    {
        configuration.WindowWidth = DefaultWindowWidth;
        configuration.WindowHeight = DefaultWindowHeight;
        configuration.Save();
        Size = new Vector2(configuration.WindowWidth, configuration.WindowHeight);
        SizeCondition = ImGuiCond.Always;
        forceWindowSizeFrames = 2;
        lastWindowLogicalSize = Size.Value;
        windowSizeDirty = false;
    }

    private void OpenSupportPage()
    {
        try
        {
            Dalamud.Utility.Util.OpenLink(Plugin.SupportUrl);
            status = "The Buy Me a Coffee page was opened in your browser.";
        }
        catch (Exception exception)
        {
            ImGui.SetClipboardText(Plugin.SupportUrl);
            status = "The browser could not be opened. The Buy Me a Coffee address was copied.";
            EmulatorLog.Warning($"[Allagan Retro Pocket] Could not open the support link: {exception.Message}");
        }
    }

    public void Dispose()
    {
        coreOptionLoadCancellation.Cancel();
        fileDialogOpen = false;
        fileDialogs.Reset();
        SaveWindowSizeNow();
        StopGame();
        input.Dispose();
        video.Dispose();
    }
}
