using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AllaganPocket.Emulation;

namespace AllaganPocket.Frontend;

internal sealed partial class EmulatorWindow
{
    private static readonly string[] VideoFilterLabels = { "Pixel", "Balanced", "Sharp", "Smooth" };
    private static readonly string[] GameplayScaleLabels = { "Fit to window", "Stretch", "Integer scale" };
    private static readonly string[] GameplayAspectLabels = { "Core provided", "4:3", "16:9" };

    private void DrawGameplay()
    {
        var active = session!;
        var game = activeGame!;
        var scale = ImGuiHelpers.GlobalScale;
        var windowFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
        ProcessQuickMenuShortcut();
        var fastForwardActive = ProcessFastForwardShortcut(windowFocused && !quickMenuVisible);

        var captureInput = windowFocused && !quickMenuVisible;
        input.SetCaptured(captureInput);
        var runCore = !quickMenuVisible && (!configuration.PauseWhenUnfocused || windowFocused);
        if (runCore)
        {
            var volume = configuration.AudioMuted || fastForwardActive && configuration.MuteFastForward
                ? 0f
                : configuration.Volume;
            active.Advance(ImGui.GetIO().DeltaTime,
                fastForwardActive ? configuration.FastForwardSpeed : 1f, volume);
        }

        var contentOrigin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail();
        var topHeight = configuration.ShowGameplayToolbar ? 56f * scale : 0f;
        if (topHeight > 0f) DrawGameplayTopBar(game, topHeight, windowFocused);
        var bodyMin = contentOrigin + new Vector2(0f, topHeight);
        var bodyMax = contentOrigin + available;
        var body = new UiRect(bodyMin, bodyMax);
        DrawGameplayBackground(body);
        var pointerRect = DrawGameVideo(active, body);
        var toolbarControlHovered = !configuration.ShowGameplayToolbar && DrawShowToolbarButton(body);

        if (!quickMenuVisible && captureInput && !toolbarControlHovered)
        {
            active.Input = input.Read(active.System, configuration.InputFor(active.System), pointerRect);
            input.SuppressGameInput();
        }
        else
        {
            active.Input = default;
        }

        if (!windowFocused && configuration.PauseWhenUnfocused && !quickMenuVisible)
        {
            DrawPausedOverlay(body, "Game paused — click this window to continue");
        }
        if (quickMenuVisible)
        {
            DrawQuickMenu(active, body);
        }
        DrawStateToast(body);
    }

    private void ProcessQuickMenuShortcut()
    {
        var bindingDown = input.IsBindingDown(configuration.QuickMenuBinding);
        if (bindingDown && !quickKeyWasDown)
        {
            quickMenuVisible = !quickMenuVisible;
        }
        quickKeyWasDown = bindingDown;
    }

    private bool ProcessFastForwardShortcut(bool shortcutAllowed)
    {
        var holdDown = input.IsBindingDown(configuration.FastForwardBinding);
        var toggleDown = input.IsBindingDown(configuration.FastForwardToggleBinding);
        if (!shortcutAllowed)
        {
            // Remember the current state so a button held while the window is inactive
            // does not toggle fast-forward when focus returns.
            fastForwardToggleKeyWasDown = toggleDown;
            return fastForwardLatched;
        }

        if (!quickMenuVisible && toggleDown && !fastForwardToggleKeyWasDown)
        {
            fastForwardLatched = !fastForwardLatched;
            ShowStateMessage(fastForwardLatched ? "Fast-forward on." : "Fast-forward off.", 1.5f);
        }

        fastForwardToggleKeyWasDown = toggleDown;
        return fastForwardLatched || holdDown;
    }

    private void DrawGameplayTopBar(RomEntry game, float height, bool focused)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.BeginChild("game-top", new Vector2(0f, height), false, ImGuiWindowFlags.NoScrollbar);
        var draw = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        draw.AddRectFilled(min, max, ImGui.GetColorU32(FrontendTheme.Sidebar));
        draw.AddLine(new Vector2(min.X, max.Y - 1f), new Vector2(max.X, max.Y - 1f),
            ImGui.GetColorU32(FrontendTheme.Border));

        var libraryWidth = 96f * scale;
        var hideWidth = 78f * scale;
        var menuWidth = 106f * scale;
        var statusText = focused
            ? "● Ready to play"
            : configuration.PauseWhenUnfocused
                ? "● Click to resume"
                : "● Click for controls";
        var statusWidth = ImGui.CalcTextSize(statusText).X;
        var rightStart = ImGui.GetWindowWidth() - 12f * scale - menuWidth - 6f * scale - hideWidth;

        ImGui.SetCursorPos(new Vector2(12f, 10f) * scale);
        if (ImGui.Button("Library", new Vector2(libraryWidth, 36f * scale))) PauseGame();

        var titleStart = 12f * scale + libraryWidth + 12f * scale;
        var statusStart = rightStart - 12f * scale - statusWidth;
        var showStatus = statusStart >= titleStart + 80f * scale;
        var titleEnd = showStatus ? statusStart - 12f * scale : rightStart - 12f * scale;
        var titleWidth = MathF.Max(36f * scale, titleEnd - titleStart);
        var title = FitTextToWidth(game.Title, titleWidth, out var titleTruncated);
        var textY = (height - ImGui.CalcTextSize("Ag").Y) * 0.5f;
        ImGui.SetCursorPos(new Vector2(titleStart, textY));
        ImGui.TextColored(FrontendTheme.SystemColor(game.System.Id), title);
        if (titleTruncated && ImGui.IsItemHovered()) ImGui.SetTooltip(game.Title);

        if (showStatus)
        {
            ImGui.SetCursorPos(new Vector2(statusStart, textY));
            ImGui.TextColored(focused ? FrontendTheme.Success : FrontendTheme.Warning, statusText);
        }

        ImGui.SetCursorPos(new Vector2(rightStart, 10f * scale));
        if (ImGui.Button("Hide bar", new Vector2(hideWidth, 36f * scale)))
        {
            configuration.ShowGameplayToolbar = false;
            configuration.Save();
        }
        ImGui.SameLine(0f, 6f * scale);
        if (ImGui.Button(quickMenuVisible ? "Resume" : "Quick menu", new Vector2(menuWidth, 36f * scale)))
        {
            quickMenuVisible = !quickMenuVisible;
        }
        ImGui.EndChild();
    }

    private void DrawGameplayBackground(UiRect body)
    {
        var draw = ImGui.GetWindowDrawList();
        // Keep the gameplay canvas translucent as well. The video frame itself remains
        // opaque, while letterboxing and unused space preserve the configured window alpha.
        draw.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(
            FrontendTheme.WithAlpha(FrontendTheme.Window, 0.28f)));
        var center = body.Center;
        var radius = MathF.Max(body.Width, body.Height) * 0.58f;
        draw.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.055f, 0.055f, 0.060f, 0.32f)), 64);
    }

    private UiRect DrawGameVideo(EmulatorSession active, UiRect body)
    {
        var padding = 18f * ImGuiHelpers.GlobalScale;
        var content = new UiRect(body.Min + new Vector2(padding), body.Max - new Vector2(padding));
        if (active.System.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            return DrawNintendoDs(active, content);
        }

        var aspect = ResolveConfiguredAspect(active);
        var image = ResolveVideoRect(content, aspect, active.VideoWidth, active.VideoHeight);
        active.UploadVideoFrame(video, configuration.VideoFilter,
            Math.Max(1, (int)MathF.Round(image.Width)), Math.Max(1, (int)MathF.Round(image.Height)));
        DrawVideoSurface(image, Vector2.Zero, Vector2.One, active);
        return image;
    }

    private UiRect DrawNintendoDs(EmulatorSession active, UiRect content)
    {
        // melonDS DS composes its screens according to the selected core layout.
        // Display the complete framebuffer instead of assuming a permanent top/bottom split.
        var aspect = active.VideoWidth > 0 && active.VideoHeight > 0
            ? active.VideoWidth / (float)active.VideoHeight
            : ResolveNintendoDsFallbackAspect(active.System);
        var image = ResolveVideoRect(content, aspect, active.VideoWidth, active.VideoHeight);
        active.UploadVideoFrame(video, configuration.VideoFilter,
            Math.Max(1, (int)MathF.Round(image.Width)), Math.Max(1, (int)MathF.Round(image.Height)));
        DrawVideoSurface(image, Vector2.Zero, Vector2.One, active);
        return image;
    }

    private float ResolveNintendoDsFallbackAspect(EmulatorSystemDefinition system)
    {
        var options = configuration.OptionsFor(system);
        var layout = options.TryGetValue("melonds_screen_layout1", out var configured)
            ? configured.ToLowerInvariant()
            : "top-bottom";
        if (layout is "top" or "bottom") return 4f / 3f;
        if (layout.Contains("left-right", StringComparison.Ordinal) ||
            layout.Contains("right-left", StringComparison.Ordinal) ||
            layout.Contains("hybrid", StringComparison.Ordinal))
        {
            return 8f / 3f;
        }
        return 2f / 3f;
    }

    private void DrawVideoSurface(UiRect rect, Vector2 uvMin, Vector2 uvMax, EmulatorSession active)
    {
        var draw = ImGui.GetWindowDrawList();
        var shadow = new Vector2(8f, 10f) * ImGuiHelpers.GlobalScale;
        draw.AddRectFilled(rect.Min + shadow, rect.Max + shadow,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 0f);
        draw.AddRectFilled(rect.Min - new Vector2(4f), rect.Max + new Vector2(4f),
            ImGui.GetColorU32(new Vector4(0.008f, 0.010f, 0.014f, 1f)), 0f);
        draw.AddRect(rect.Min - new Vector2(4f), rect.Max + new Vector2(4f),
            ImGui.GetColorU32(FrontendTheme.Border), 0f);
        var wrap = video.Wrap;
        if (wrap is not null && active.VideoWidth > 0 && active.VideoHeight > 0)
        {
            draw.AddImage(wrap.Handle, rect.Min, rect.Max, uvMin, uvMax, 0xFFFFFFFFu);
        }
        else
        {
            var text = "Starting core...";
            var size = ImGui.CalcTextSize(text);
            draw.AddText(rect.Center - size * 0.5f, ImGui.GetColorU32(FrontendTheme.Muted), text);
        }
    }

    private void DrawQuickMenu(EmulatorSession active, UiRect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(FrontendTheme.Overlay));
        var width = MathF.Min(410f * scale, body.Width - 32f * scale);
        var height = MathF.Max(220f * scale, body.Height - 32f * scale);
        ImGui.SetCursorScreenPos(new Vector2(body.Max.X - width - 16f * scale, body.Min.Y + 16f * scale));
        ImGui.BeginChild("quick-menu", new Vector2(width, height), true);
        ImGui.SetCursorPos(new Vector2(16f, 14f) * scale);
        ImGui.TextUnformatted("Quick menu");
        ImGui.TextColored(FrontendTheme.Muted,
            $"{active.CoreName} • {InputRouter.ChordName(configuration.QuickMenuBinding.Primary, configuration.ControllerPreset)} to close");
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Button, FrontendTheme.AccentSoft);
        if (ImGui.Button("Resume", new Vector2(-1f, 38f * scale))) quickMenuVisible = false;
        ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.TextColored(FrontendTheme.Accent, "Save states");
        var stateSlot = configuration.SaveStateSlot;
        ImGui.TextUnformatted($"Slot {stateSlot}");
        ImGui.SameLine();
        if (ImGui.SmallButton("-") && stateSlot > 1)
        {
            configuration.SaveStateSlot = stateSlot - 1;
            configuration.Save();
            stateSlot = configuration.SaveStateSlot;
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("+") && stateSlot < 10)
        {
            configuration.SaveStateSlot = stateSlot + 1;
            configuration.Save();
            stateSlot = configuration.SaveStateSlot;
        }
        if (ImGui.Button("Save state", new Vector2(-1f, 34f * scale))) SaveManualState();
        var canLoad = active.HasState(stateSlot);
        ImGui.BeginDisabled(!canLoad);
        if (ImGui.Button("Load state", new Vector2(-1f, 34f * scale))) LoadManualState();
        ImGui.EndDisabled();

        if (active.DiskCount > 1)
        {
            ImGui.Separator();
            ImGui.TextColored(FrontendTheme.Accent, "Media");
            var disk = Math.Clamp(active.DiskIndex, 0, active.DiskCount - 1);
            var discLabels = Enumerable.Range(1, active.DiskCount)
                .Select(static number => $"Disc {number}")
                .ToArray();
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.Combo("##current-disc", ref disk, discLabels, discLabels.Length))
            {
                try
                {
                    active.SetDiskIndex(disk);
                    ShowStateMessage($"Disc {disk + 1} selected.");
                }
                catch (Exception exception)
                {
                    ShowStateMessage(exception.Message, 5f);
                }
            }
        }

        ImGui.Separator();
        ImGui.TextColored(FrontendTheme.Accent, "Display");
        var scaleMode = (int)configuration.GameplayScale;
        if (ImGui.Combo("Image scaling", ref scaleMode, GameplayScaleLabels, GameplayScaleLabels.Length))
        {
            configuration.GameplayScale = (GameplayScaleMode)scaleMode;
            configuration.Save();
        }
        var aspectMode = (int)configuration.GameplayAspect;
        if (ImGui.Combo("Aspect ratio", ref aspectMode, GameplayAspectLabels, GameplayAspectLabels.Length))
        {
            configuration.GameplayAspect = (GameplayAspectMode)aspectMode;
            configuration.Save();
        }
        var filter = (int)configuration.VideoFilter;
        if (ImGui.Combo("Filter", ref filter, VideoFilterLabels, VideoFilterLabels.Length))
        {
            configuration.VideoFilter = (EmulatorVideoFilter)filter;
            configuration.Save();
        }

        var muted = configuration.AudioMuted;
        if (ImGui.Checkbox("Mute audio", ref muted))
        {
            configuration.AudioMuted = muted;
            configuration.Save();
        }
        var volumePercent = (int)MathF.Round(configuration.Volume * 100f);
        ImGui.BeginDisabled(configuration.AudioMuted);
        if (ImGui.SliderInt("Volume", ref volumePercent, 0, 100, "%d%%"))
        {
            configuration.Volume = volumePercent / 100f;
            configuration.Save();
        }
        ImGui.EndDisabled();
        var fast = fastForwardLatched;
        if (ImGui.Checkbox("Fast-forward", ref fast)) fastForwardLatched = fast;

        ImGui.Separator();
        if (ImGui.Button("Restart game", new Vector2(-1f, 34f * scale)) && activeGame is not null)
        {
            var game = activeGame;
            StartGame(game);
            ImGui.EndChild();
            return;
        }
        ImGui.PushStyleColor(ImGuiCol.Button, FrontendTheme.Danger);
        if (ImGui.Button("Close game", new Vector2(-1f, 34f * scale))) StopGame();
        ImGui.PopStyleColor();
        ImGui.EndChild();
    }

    private void DrawPausedOverlay(UiRect body, string text)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(body.Min, body.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f)));
        var size = ImGui.CalcTextSize(text);
        var padding = new Vector2(18f, 12f) * ImGuiHelpers.GlobalScale;
        var min = body.Center - (size + padding * 2f) * 0.5f;
        draw.AddRectFilled(min, min + size + padding * 2f, ImGui.GetColorU32(FrontendTheme.PanelRaised), 0f);
        draw.AddText(min + padding, ImGui.GetColorU32(FrontendTheme.Text), text);
    }

    private bool DrawShowToolbarButton(UiRect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var buttonSize = new Vector2(92f, 30f) * scale;
        ImGui.SetCursorScreenPos(new Vector2(
            body.Max.X - buttonSize.X - 12f * scale,
            body.Min.Y + 12f * scale));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.04f, 0.04f, 0.05f, 0.78f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, FrontendTheme.PanelHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, FrontendTheme.AccentSoft);
        if (ImGui.Button("Show bar", buttonSize))
        {
            configuration.ShowGameplayToolbar = true;
            configuration.Save();
        }
        var hovered = ImGui.IsItemHovered() || ImGui.IsItemActive();
        ImGui.PopStyleColor(3);
        if (hovered)
        {
            ImGui.SetTooltip($"Show Library, game title and Quick menu\n{InputRouter.ChordName(configuration.QuickMenuBinding.Primary, configuration.ControllerPreset)} opens the Quick menu");
        }
        return hovered;
    }

    private void StartGame(RomEntry game)
    {
        if (IsCoreOptionDiscoveryRunning(game.System))
        {
            status = "Core settings are still loading. Start the game after loading finishes.";
            return;
        }

        StopGame();
        try
        {
            EnsureSystemFiles();
            var corePath = Path.Combine(coreDirectory, game.System.CoreFileName);
            if (!File.Exists(corePath)) throw new FileNotFoundException("Libretro core not found.", corePath);
            var options = configuration.OptionsFor(game.System);
            session = new EmulatorSession(corePath, game.System, game.Path, emulatorRoot, options,
                preserveSaveMemoryOnStateLoad: configuration.ProtectSaveMemoryOnStateLoad,
                audioLatencyMs: configuration.AudioLatencyMs,
                analogController: configuration.UsesAnalogController(game.System));
            activeGame = game;
            if (configuration.AutoLoadState && session.HasAutoState)
            {
                try
                {
                    session.LoadAutoState();
                }
                catch (Exception exception)
                {
                    EmulatorLog.Warning($"[Allagan Retro Pocket] Auto-load failed: {exception.Message}");
                }
            }
            configuration.AddRecent(game);
            configuration.Save();
            gameplayVisible = true;
            quickMenuVisible = false;
            fastForwardLatched = false;
            fastForwardToggleKeyWasDown = false;
            stateMessage = string.Empty;
            stateMessageSeconds = 0f;
        }
        catch (Exception exception)
        {
            status = $"Failed to start: {exception.Message}";
            EmulatorLog.Error($"[Allagan Retro Pocket] {exception}");
            session?.Dispose();
            session = null;
            activeGame = null;
            gameplayVisible = false;
        }
    }

    private void ResumeGame()
    {
        if (session is null) return;
        gameplayVisible = true;
        quickMenuVisible = false;
        stateMessage = string.Empty;
        stateMessageSeconds = 0f;
    }

    private void PauseGame()
    {
        SaveAutoState();
        input.SetCaptured(false);
        gameplayVisible = false;
        quickMenuVisible = false;
        page = FrontendPage.Library;
        section = LibrarySection.Recent;
    }

    private void StopGame()
    {
        input.SetCaptured(false);
        SaveAutoState();
        session?.Dispose();
        session = null;
        activeGame = null;
        gameplayVisible = false;
        quickMenuVisible = false;
        fastForwardLatched = false;
        fastForwardToggleKeyWasDown = false;
    }

    private void EnsureSystemFiles()
    {
        lock (systemFilesGate)
        {
            if (systemFilesInstalled)
            {
                return;
            }

            BundledSystemFiles.Install(coreDirectory, emulatorRoot);
            systemFilesInstalled = true;
        }
    }

    private void SaveAutoState()
    {
        if (session is null || !configuration.AutoSaveState) return;
        try
        {
            session.SaveAutoState(true);
        }
        catch (Exception exception)
        {
            EmulatorLog.Warning($"[Allagan Retro Pocket] Auto-save failed: {exception.Message}");
        }
    }

    private void SaveManualState()
    {
        if (session is null) return;
        try
        {
            var slot = configuration.SaveStateSlot;
            session.SaveState(slot);
            ShowStateMessage($"State saved to slot {slot}.");
        }
        catch (Exception exception)
        {
            ShowStateMessage($"Save failed: {exception.Message}", 5f);
        }
    }

    private void LoadManualState()
    {
        if (session is null) return;
        try
        {
            var slot = configuration.SaveStateSlot;
            session.LoadState(slot);
            ShowStateMessage($"State loaded from slot {slot}.");
        }
        catch (Exception exception)
        {
            ShowStateMessage($"Load failed: {exception.Message}", 5f);
        }
    }

    private void ShowStateMessage(string message, float seconds = 3f)
    {
        stateMessage = message;
        stateMessageSeconds = seconds;
    }

    private void DrawStateToast(UiRect body)
    {
        if (string.IsNullOrWhiteSpace(stateMessage) || stateMessageSeconds <= 0f) return;
        stateMessageSeconds -= MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
        if (stateMessageSeconds <= 0f)
        {
            stateMessage = string.Empty;
            return;
        }

        var size = ImGui.CalcTextSize(stateMessage) + new Vector2(24f, 16f) * ImGuiHelpers.GlobalScale;
        var min = new Vector2(body.Center.X - size.X * 0.5f, body.Max.Y - size.Y - 18f * ImGuiHelpers.GlobalScale);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, min + size, ImGui.GetColorU32(FrontendTheme.PanelRaised), 0f);
        draw.AddRect(min, min + size, ImGui.GetColorU32(FrontendTheme.Border), 0f);
        draw.AddText(min + new Vector2(12f, 8f) * ImGuiHelpers.GlobalScale,
            ImGui.GetColorU32(FrontendTheme.Text), stateMessage);
    }

    private float ResolveConfiguredAspect(EmulatorSession active) => configuration.GameplayAspect switch
    {
        GameplayAspectMode.FourThree => 4f / 3f,
        GameplayAspectMode.SixteenNine => 16f / 9f,
        _ => ResolveAspect(active.VideoWidth, active.VideoHeight, active.VideoAspectRatio),
    };

    private UiRect ResolveVideoRect(UiRect bounds, float aspect, int sourceWidth, int sourceHeight)
    {
        if (configuration.GameplayScale == GameplayScaleMode.Stretch)
        {
            return bounds;
        }
        if (configuration.GameplayScale == GameplayScaleMode.Integer && sourceWidth > 0 && sourceHeight > 0)
        {
            var logicalWidth = sourceHeight * aspect;
            var multiplier = MathF.Floor(MathF.Min(bounds.Width / logicalWidth, bounds.Height / sourceHeight));
            if (multiplier >= 1f)
            {
                var size = new Vector2(logicalWidth * multiplier, sourceHeight * multiplier);
                return new UiRect(bounds.Center - size * 0.5f, bounds.Center + size * 0.5f);
            }
        }
        return FitAspect(bounds, aspect);
    }

    private static float ResolveAspect(int width, int height, float aspect)
    {
        if (aspect > 0.1f && float.IsFinite(aspect)) return aspect;
        return width > 0 && height > 0 ? width / (float)height : 4f / 3f;
    }

    private static UiRect FitAspect(UiRect bounds, float aspect)
    {
        var width = bounds.Width;
        var height = width / MathF.Max(0.1f, aspect);
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = height * aspect;
        }
        var size = new Vector2(MathF.Max(1f, width), MathF.Max(1f, height));
        return new UiRect(bounds.Center - size * 0.5f, bounds.Center + size * 0.5f);
    }
}
