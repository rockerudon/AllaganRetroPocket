using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AllaganPocket.Emulation;
using AllaganPocket.Emulation.Libretro;

namespace AllaganPocket.Frontend;

internal enum CoreSettingsSection : byte
{
    CoreOptions,
    Input,
    Video,
    AudioAndSpeed,
    StorageAndMedia,
    Library,
}

internal sealed partial class EmulatorWindow
{
    private static readonly string[] CoreSettingsSectionLabels =
    {
        "Core options", "Input", "Video", "Audio & speed", "Storage & media", "Library",
    };

    private CoreSettingsSection coreSettingsSection = CoreSettingsSection.CoreOptions;
    private readonly Dictionary<string, IReadOnlyList<LibretroCoreOptionDefinition>> coreOptionCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<LibretroCoreOptionDefinition>> importantCoreOptionCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<CoreOptionLoadResult>> coreOptionLoads =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> coreOptionErrors = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim coreOptionLoadGate = new(1, 1);
    private readonly CancellationTokenSource coreOptionLoadCancellation = new();
    private string coreOptionSearch = string.Empty;
    private bool bindingPopupRequested;
    private bool bindingCaptureOpen;
    private string bindingCaptureLabel = string.Empty;
    private InputChord? bindingCaptureTarget;
    private List<InputBindingToken> bindingCaptureTokens = new();
    private bool bindingCaptureArmed;
    private bool controllerAutoMapPopupRequested;
    private bool controllerAutoMapPopupOpen;
    private bool controllerAutoMapAllSystems = true;
    private int controllerAutoMapPresetIndex;
    private string controllerAutoMapMessage = string.Empty;
    private EmulatorSystemDefinition? firmwareRemovalSystem;
    private EmulatorFirmwareDefinition? firmwareRemovalTarget;
    private bool firmwareRemovalPopupRequested;
    private bool firmwareRemovalPopupOpen;
    private string firmwareOperationMessage = string.Empty;
    private string firmwareOperationSystemId = string.Empty;
    private string storageOperationMessage = string.Empty;
    private string storageOperationSystemId = string.Empty;
    private string lastFirmwareImportDirectory = string.Empty;

    private void DrawSettings()
    {
        var system = EmulatorSystemCatalog.ById(selectedSystemId) ?? EmulatorSystemCatalog.All.First();
        selectedSystemId = system.Id;
        section = LibrarySection.System;

        DrawCoreSettingsHeader(system);

        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail();
        var compactNavigation = available.X < 620f * scale;
        if (compactNavigation)
        {
            ImGui.SetCursorPosX(18f * scale);
            ImGui.SetNextItemWidth(MathF.Max(220f * scale, available.X - 36f * scale));
            var selectedSection = (int)coreSettingsSection;
            if (ImGui.Combo("##settings-section", ref selectedSection,
                    CoreSettingsSectionLabels, CoreSettingsSectionLabels.Length))
            {
                coreSettingsSection = (CoreSettingsSection)selectedSection;
            }
            ImGui.Dummy(new Vector2(1f, 6f * scale));

            var compactContentHeight = ImGui.GetContentRegionAvail().Y;
            ImGui.BeginChild("core-settings-content", new Vector2(0f, compactContentHeight), false);
            ImGui.SetCursorPos(new Vector2(20f, 14f) * scale);
            ImGui.BeginGroup();
            DrawSelectedSettingsPage(system);
            ImGui.Dummy(new Vector2(1f, 24f * scale));
            ImGui.EndGroup();
            ImGui.EndChild();
            DrawBindingCapturePopup();
            DrawControllerAutoMapPopup(system);
            DrawFirmwareRemovalPopup();
            return;
        }

        var navigationWidth = MathF.Min(182f * scale,
            MathF.Max(158f * scale, available.X * 0.28f));
        ImGui.BeginChild("core-settings-navigation", new Vector2(navigationWidth, available.Y), true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        DrawCoreSettingsNavigation();
        ImGui.EndChild();

        ImGui.SameLine(0f, 0f);
        ImGui.BeginChild("core-settings-content", new Vector2(0f, available.Y), false);
        ImGui.SetCursorPos(new Vector2(24f, 18f) * scale);
        ImGui.BeginGroup();
        DrawSelectedSettingsPage(system);
        ImGui.Dummy(new Vector2(1f, 24f * scale));
        ImGui.EndGroup();
        ImGui.EndChild();
        DrawBindingCapturePopup();
        DrawControllerAutoMapPopup(system);
        DrawFirmwareRemovalPopup();
    }

    private void DrawSelectedSettingsPage(EmulatorSystemDefinition system)
    {
        switch (coreSettingsSection)
        {
            case CoreSettingsSection.CoreOptions:
                DrawCoreOptionsPage(system);
                break;
            case CoreSettingsSection.Input:
                DrawInputSettingsPage(system);
                break;
            case CoreSettingsSection.Video:
                DrawVideoSettingsPage();
                break;
            case CoreSettingsSection.AudioAndSpeed:
                DrawAudioAndSpeedSettingsPage();
                break;
            case CoreSettingsSection.StorageAndMedia:
                DrawStorageAndMediaSettingsPage(system);
                break;
            case CoreSettingsSection.Library:
                DrawLibrarySettingsPage();
                break;
            default:
                DrawCoreOptionsPage(system);
                break;
        }
    }

    private void DrawCoreSettingsHeader(EmulatorSystemDefinition system)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var compact = ImGui.GetContentRegionAvail().X < 620f * scale;
        var headerHeight = compact ? 112f : 78f;
        ImGui.BeginChild("core-settings-header", new Vector2(0f, headerHeight * scale), false,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.SetCursorPos(new Vector2(24f, 14f) * scale);
        var iconSize = new Vector2(34f, 34f) * scale;
        if (DrawSystemIcon(system, iconSize))
        {
            ImGui.SameLine(0f, 12f * scale);
        }

        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(compact ? ImGui.GetWindowWidth() - 24f * scale : 0f);
        ImGui.TextWrapped(system.Name);
        ImGui.PopTextWrapPos();
        ImGui.TextColored(FrontendTheme.Muted, "Settings");
        ImGui.EndGroup();

        var buttonWidth = 126f * scale;
        ImGui.SetCursorPos(compact
            ? new Vector2(24f * scale, 67f * scale)
            : new Vector2(ImGui.GetWindowWidth() - buttonWidth - 18f * scale, 18f * scale));
        if (ImGui.Button("←  Library", new Vector2(buttonWidth, 34f * scale)))
        {
            page = FrontendPage.Library;
            section = LibrarySection.System;
            selectedGame = null;
        }

        ImGui.SetCursorPos(new Vector2(24f, compact ? 101f : 61f) * scale);
        ImGui.Separator();
        ImGui.EndChild();
    }

    private void DrawCoreSettingsNavigation()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPos(new Vector2(14f, 14f) * scale);
        ImGui.TextColored(FrontendTheme.Muted, "SETTINGS");
        ImGui.Dummy(new Vector2(1f, 5f * scale));
        DrawSettingsNavigationRow("Core options", CoreSettingsSection.CoreOptions);
        DrawSettingsNavigationRow("Input", CoreSettingsSection.Input);
        DrawSettingsNavigationRow("Video", CoreSettingsSection.Video);
        DrawSettingsNavigationRow("Audio & speed", CoreSettingsSection.AudioAndSpeed);
        DrawSettingsNavigationRow("Storage & media", CoreSettingsSection.StorageAndMedia);
        DrawSettingsNavigationRow("Library", CoreSettingsSection.Library);
    }

    private void DrawSettingsNavigationRow(string label, CoreSettingsSection target)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var selected = coreSettingsSection == target;
        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(ImGui.GetContentRegionAvail().X, 36f * scale);
        ImGui.InvisibleButton($"settings-nav-{target}", size);
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            coreSettingsSection = target;
        }

        var draw = ImGui.GetWindowDrawList();
        if (selected || hovered)
        {
            draw.AddRectFilled(position, position + size,
                ImGui.GetColorU32(selected ? FrontendTheme.AccentSoft : FrontendTheme.PanelHover));
        }
        if (selected)
        {
            draw.AddRectFilled(position, position + new Vector2(3f * scale, size.Y),
                ImGui.GetColorU32(FrontendTheme.Accent));
        }
        var textSize = ImGui.CalcTextSize(label);
        draw.AddText(position + new Vector2(12f * scale, (size.Y - textSize.Y) * 0.5f),
            ImGui.GetColorU32(selected ? FrontendTheme.Text : FrontendTheme.Muted), label);
    }

    private static void DrawSettingsPageTitle(string title, string description)
    {
        ImGui.TextUnformatted(title);
        DrawMutedWrapped(description);
        ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(1f, 10f * ImGuiHelpers.GlobalScale));
    }

    private static void DrawMutedWrapped(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Muted);
        ImGui.PushTextWrapPos(0f);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
    }

    private static void DrawColoredWrapped(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.PushTextWrapPos(0f);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
    }

    private static void DrawSettingsSection(string title, Action content)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TextColored(FrontendTheme.Accent, title);
        ImGui.Dummy(new Vector2(1f, 2f * scale));
        content();
        ImGui.Dummy(new Vector2(1f, 10f * scale));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(1f, 12f * scale));
    }

    private void DrawFirmwareSettings(EmulatorSystemDefinition system)
    {
        if (system.Firmware.Count == 0)
        {
            DrawMutedWrapped("This core does not require additional BIOS or firmware files.");
            return;
        }

        DrawMutedWrapped("Imported files are copied into Allagan Retro Pocket's user data folder. Your original file is never moved or deleted.");
        if (string.Equals(firmwareOperationSystemId, system.Id, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(firmwareOperationMessage))
        {
            ImGui.Dummy(new Vector2(1f, 4f * ImGuiHelpers.GlobalScale));
            DrawColoredWrapped(FrontendTheme.Accent, firmwareOperationMessage);
        }

        var scale = ImGuiHelpers.GlobalScale;
        foreach (var firmware in system.Firmware)
        {
            ImGui.PushID(firmware.FileName);
            var path = ResolveFirmwareTargetPath(firmware);
            var installed = File.Exists(path) || Directory.Exists(path);
            var statusColor = installed
                ? FrontendTheme.Success
                : firmware.Required ? FrontendTheme.Danger : FrontendTheme.Warning;
            var statusText = installed ? "Installed" : firmware.Required ? "Required" : "Optional";

            ImGui.Dummy(new Vector2(1f, 5f * scale));
            ImGui.TextColored(statusColor, statusText);
            ImGui.SameLine(0f, 10f * scale);
            ImGui.TextWrapped(firmware.FileName);
            DrawMutedWrapped(firmware.Description);

            if (firmware.ManagedByPlugin)
            {
                DrawMutedWrapped("Provided and maintained by Allagan Retro Pocket.");
            }
            else
            {
                var actionWidth = 112f * scale;
                if (ImGui.Button(installed ? "Replace" : "Import", new Vector2(actionWidth, 0f)))
                {
                    OpenFirmwareImportDialog(system, firmware);
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(installed
                        ? "Choose another file and replace the copy stored by Allagan Retro Pocket."
                        : "Choose a file to copy into Allagan Retro Pocket's system folder.");
                }

                if (installed)
                {
                    ImGui.SameLine();
                    if (ImGui.Button("Remove", new Vector2(actionWidth, 0f)))
                    {
                        firmwareRemovalSystem = system;
                        firmwareRemovalTarget = firmware;
                        firmwareRemovalPopupOpen = true;
                        firmwareRemovalPopupRequested = true;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Remove only Allagan Retro Pocket's imported copy. The original file is not affected.");
                }
            }

            if (installed && File.Exists(path))
            {
                try
                {
                    var size = new FileInfo(path).Length;
                    DrawMutedWrapped($"Stored file: {FormatFileSize(size)}");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    EmulatorLog.Warning($"[Allagan Retro Pocket] Could not inspect firmware '{path}': {exception.Message}");
                }
            }

            ImGui.Dummy(new Vector2(1f, 8f * scale));
            ImGui.Separator();
            ImGui.PopID();
        }
    }

    private void OpenFirmwareImportDialog(EmulatorSystemDefinition system,
        EmulatorFirmwareDefinition firmware)
    {
        if (firmware.ManagedByPlugin)
        {
            SetFirmwareOperationMessage(system, "This system file is maintained by Allagan Retro Pocket.");
            return;
        }

        var expectedName = Path.GetFileName(firmware.FileName);
        var extension = Path.GetExtension(expectedName);
        var filter = string.IsNullOrWhiteSpace(extension)
            ? "System files{.*}"
            : $"{firmware.Description}{{{extension}}}";
        var startPath = Directory.Exists(lastFirmwareImportDirectory)
            ? lastFirmwareImportDirectory
            : DefaultFirmwareImportDirectory();

        fileDialogOpen = true;
        fileDialogs.OpenFileDialog($"Import {firmware.Description}", filter, (accepted, paths) =>
        {
            fileDialogOpen = false;
            if (!accepted || paths.Count == 0) return;
            ImportFirmwareFile(system, firmware, paths[0]);
        }, 1, startPath, true);
    }

    private void ImportFirmwareFile(EmulatorSystemDefinition system,
        EmulatorFirmwareDefinition firmware, string sourcePath)
    {
        const long maximumFirmwareBytes = 64L * 1024L * 1024L;
        string? temporaryPath = null;
        try
        {
            var source = Path.GetFullPath(sourcePath);
            if (!File.Exists(source))
                throw new FileNotFoundException("The selected file no longer exists.", source);

            var attributes = File.GetAttributes(source);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked files cannot be imported.");

            var info = new FileInfo(source);
            if (info.Length <= 0)
                throw new InvalidDataException("The selected file is empty.");
            if (info.Length > maximumFirmwareBytes)
                throw new InvalidDataException("The selected file is too large to be a supported BIOS or system file.");

            var expectedExtension = Path.GetExtension(firmware.FileName);
            if (!string.IsNullOrWhiteSpace(expectedExtension) &&
                !string.Equals(info.Extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Choose a {expectedExtension} file for this entry.");
            }

            var target = ResolveFirmwareTargetPath(firmware);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                SetFirmwareOperationMessage(system, $"{firmware.FileName} is already installed.");
                return;
            }

            var targetDirectory = Path.GetDirectoryName(target)
                ?? throw new InvalidDataException("The destination folder is invalid.");
            Directory.CreateDirectory(targetDirectory);
            temporaryPath = Path.Combine(targetDirectory,
                $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.importing");

            using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                       64 * 1024, FileOptions.SequentialScan))
            using (var destinationStream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                sourceStream.CopyTo(destinationStream);
                destinationStream.Flush(true);
            }

            File.Move(temporaryPath, target, true);
            temporaryPath = null;
            lastFirmwareImportDirectory = Path.GetDirectoryName(source) ?? string.Empty;
            SetFirmwareOperationMessage(system, $"Imported {firmware.Description} as {firmware.FileName}. Restart the game to use it.");
            InvalidateCoreOptionCache(system);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidDataException or NotSupportedException)
        {
            SetFirmwareOperationMessage(system, $"Import failed: {exception.Message}");
            EmulatorLog.Warning($"[Allagan Retro Pocket] Firmware import failed: {exception.Message}");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryPath))
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    EmulatorLog.Warning($"[Allagan Retro Pocket] Could not remove temporary firmware file: {exception.Message}");
                }
            }
        }
    }

    private void DrawFirmwareRemovalPopup()
    {
        if (firmwareRemovalPopupRequested)
        {
            ImGui.OpenPopup("Remove system file");
            firmwareRemovalPopupRequested = false;
        }

        if (!firmwareRemovalPopupOpen || firmwareRemovalSystem is null || firmwareRemovalTarget is null)
            return;

        var popupCenter = applicationWindowPos + applicationWindowSize * 0.5f;
        ImGui.SetNextWindowPos(popupCenter, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (!ImGui.BeginPopupModal("Remove system file", ref firmwareRemovalPopupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            if (!firmwareRemovalPopupOpen) ClearFirmwareRemovalRequest();
            return;
        }

        ImGui.TextUnformatted("Remove this imported file?");
        DrawMutedWrapped(firmwareRemovalTarget.FileName);
        DrawMutedWrapped("Only the copy inside Allagan Retro Pocket is deleted. The original file you imported is not changed.");
        ImGui.Dummy(new Vector2(1f, 8f * ImGuiHelpers.GlobalScale));

        var actionWidth = 112f * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleColor(ImGuiCol.Button, FrontendTheme.Danger);
        if (ImGui.Button("Remove", new Vector2(actionWidth, 0f)))
        {
            RemoveFirmwareFile(firmwareRemovalSystem, firmwareRemovalTarget);
            ImGui.CloseCurrentPopup();
            ClearFirmwareRemovalRequest();
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(actionWidth, 0f)))
        {
            ImGui.CloseCurrentPopup();
            ClearFirmwareRemovalRequest();
        }
        ImGui.EndPopup();
    }

    private void RemoveFirmwareFile(EmulatorSystemDefinition system,
        EmulatorFirmwareDefinition firmware)
    {
        try
        {
            if (firmware.ManagedByPlugin)
                throw new InvalidOperationException("This file is maintained by Allagan Retro Pocket.");

            var target = ResolveFirmwareTargetPath(firmware);
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            else if (Directory.Exists(target))
            {
                throw new InvalidOperationException("Managed folders cannot be removed from this screen.");
            }

            SetFirmwareOperationMessage(system, $"Removed {firmware.FileName}. Restart the game before importing a replacement.");
            InvalidateCoreOptionCache(system);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
        {
            SetFirmwareOperationMessage(system, $"Remove failed: {exception.Message}");
            EmulatorLog.Warning($"[Allagan Retro Pocket] Firmware removal failed: {exception.Message}");
        }
    }

    private string ResolveFirmwareTargetPath(EmulatorFirmwareDefinition firmware)
    {
        var root = Path.GetFullPath(Path.Combine(emulatorRoot, "system"));
        var target = Path.GetFullPath(Path.Combine(root, firmware.FileName));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The system-file destination is outside the plugin data folder.");
        return target;
    }

    private void SetFirmwareOperationMessage(EmulatorSystemDefinition system, string message)
    {
        firmwareOperationSystemId = system.Id;
        firmwareOperationMessage = message;
    }

    private void InvalidateCoreOptionCache(EmulatorSystemDefinition system)
    {
        coreOptionCache.Remove(system.Id);
        importantCoreOptionCache.Remove(system.Id);
        coreOptionErrors.Remove(system.Id);
    }

    private void ClearFirmwareRemovalRequest()
    {
        firmwareRemovalPopupOpen = false;
        firmwareRemovalSystem = null;
        firmwareRemovalTarget = null;
    }

    private static string DefaultFirmwareImportDirectory()
    {
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads)) return downloads;
        const string preferredDrive = @"C:\";
        return Directory.Exists(preferredDrive) ? preferredDrive : Environment.CurrentDirectory;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024L * 1024L) return $"{bytes / (1024d * 1024d):0.##} MiB";
        if (bytes >= 1024L) return $"{bytes / 1024d:0.##} KiB";
        return $"{bytes} bytes";
    }

    private void DrawCoreOptionsPage(EmulatorSystemDefinition system)
    {
        DrawSettingsPageTitle("Core options",
            "Only the settings most useful for playing are shown here. Everything else remains available under Advanced settings.");
        DrawMutedWrapped("Changes are applied the next time a game using this core starts.");
        ImGui.Dummy(new Vector2(1f, 8f * ImGuiHelpers.GlobalScale));

        var definitions = LoadCoreOptionDefinitions(system, false, out var loading);
        if (coreOptionErrors.TryGetValue(system.Id, out var error) && !string.IsNullOrWhiteSpace(error))
        {
            DrawColoredWrapped(FrontendTheme.Warning, error);
            ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
        }

        DrawSettingsSection("BIOS & system files", () => DrawFirmwareSettings(system));

        if (loading)
        {
            DrawSettingsSection("Recommended settings", DrawCoreOptionsLoading);
            return;
        }

        var visibleDefinitions = definitions
            .Where(static option => option.Visible && !IsStorageCoreOption(option))
            .ToArray();
        if (!importantCoreOptionCache.TryGetValue(system.Id, out var important))
        {
            important = GetImportantCoreOptions(system, visibleDefinitions);
            importantCoreOptionCache[system.Id] = important;
        }
        var importantKeys = important.Select(static option => option.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (system.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            DrawSettingsSection("Screen preview", () => DrawNintendoDsLayoutPreview(system));
        }

        DrawSettingsSection("Recommended settings", () =>
        {
            if (important.Count == 0)
            {
                DrawMutedWrapped("This core is already configured for general use. You can start playing without changing anything.");
                return;
            }

            foreach (var option in important)
            {
                DrawCoreOption(system, option, false);
            }
        });

        var scale = ImGuiHelpers.GlobalScale;
        if (ImGui.CollapsingHeader("Advanced settings##core-advanced"))
        {
            DrawMutedWrapped("These options are intended for troubleshooting, unusual games and experienced users.");
            ImGui.Dummy(new Vector2(1f, 6f * scale));

            var actionWidth = 100f * scale;
            ImGui.SetNextItemWidth(MathF.Max(180f * scale,
                ImGui.GetContentRegionAvail().X - actionWidth - 10f * scale));
            ImGui.InputTextWithHint("##core-option-search", "Search advanced settings...",
                ref coreOptionSearch, 128);
            ImGui.SameLine();
            if (ImGui.Button("Refresh", new Vector2(actionWidth, 0f)))
            {
                _ = LoadCoreOptionDefinitions(system, true, out _);
            }
            ImGui.Dummy(new Vector2(1f, 8f * scale));

            var advanced = visibleDefinitions
                .Where(option => !importantKeys.Contains(option.Key))
                .Where(option => string.IsNullOrWhiteSpace(coreOptionSearch) ||
                    option.Description.Contains(coreOptionSearch, StringComparison.CurrentCultureIgnoreCase) ||
                    option.Key.Contains(coreOptionSearch, StringComparison.OrdinalIgnoreCase) ||
                    option.Info.Contains(coreOptionSearch, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(static option => option.CategoryDescription, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static option => option.Description, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            if (advanced.Length == 0)
            {
                DrawMutedWrapped(string.IsNullOrWhiteSpace(coreOptionSearch)
                    ? "This core has no additional advanced options."
                    : "No advanced settings match the search.");
            }
            else
            {
                foreach (var group in advanced.GroupBy(option =>
                             string.IsNullOrWhiteSpace(option.CategoryDescription)
                                 ? "Core"
                                 : option.CategoryDescription))
                {
                    DrawSettingsSection(group.Key, () =>
                    {
                        foreach (var option in group) DrawCoreOption(system, option, true);
                    });
                }
            }
        }

        ImGui.Dummy(new Vector2(1f, 12f * scale));
        if (ImGui.Button("Restore core defaults", new Vector2(180f * scale, 34f * scale)))
        {
            var options = configuration.OptionsFor(system);
            options.Clear();
            foreach (var definition in definitions)
            {
                if (!string.IsNullOrWhiteSpace(definition.DefaultValue))
                {
                    options[definition.Key] = definition.DefaultValue;
                }
            }
            foreach (var pair in system.DefaultCoreOptions)
            {
                options[pair.Key] = pair.Value;
            }
            configuration.Save();
        }
    }

    private void DrawCoreOption(EmulatorSystemDefinition system,
        LibretroCoreOptionDefinition option, bool advanced)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var options = configuration.OptionsFor(system);
        var currentValue = options.TryGetValue(option.Key, out var configured)
            ? configured
            : option.DefaultValue;
        var currentIndex = option.Choices
            .Select(static choice => choice.Value)
            .ToList()
            .FindIndex(value => string.Equals(value, currentValue, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0) currentIndex = 0;

        ImGui.PushID(option.Key);
        ImGui.TextWrapped(advanced ? option.Description : FriendlyCoreOptionLabel(option));
        var help = advanced ? option.Info : FriendlyCoreOptionHelp(option);
        if (!string.IsNullOrWhiteSpace(help))
        {
            DrawMutedWrapped(help);
        }
        if (option.Choices.Count > 0)
        {
            ImGui.SetNextItemWidth(MathF.Min(420f * scale, ImGui.GetContentRegionAvail().X));
            var labels = option.Choices.Select(static choice => choice.Label).ToArray();
            if (ImGui.Combo("##value", ref currentIndex, labels, labels.Length))
            {
                options[option.Key] = option.Choices[currentIndex].Value;
                configuration.Save();
            }
        }
        else
        {
            DrawMutedWrapped(string.IsNullOrWhiteSpace(currentValue)
                ? "The core did not provide a selectable value list."
                : currentValue);
        }
        if (advanced) DrawMutedWrapped(option.Key);
        ImGui.Dummy(new Vector2(1f, 8f * scale));
        ImGui.PopID();
    }

    private static IReadOnlyList<LibretroCoreOptionDefinition> GetImportantCoreOptions(
        EmulatorSystemDefinition system, IReadOnlyList<LibretroCoreOptionDefinition> definitions)
    {
        var selected = new List<LibretroCoreOptionDefinition>();
        var selectedKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in ImportantCoreOptionTerms(system))
        {
            var option = definitions
                .Where(candidate => !selectedKeys.Contains(candidate.Key) &&
                    CoreOptionMatches(candidate, term))
                .OrderBy(candidate => CoreOptionMatchScore(candidate, term))
                .ThenBy(static candidate => candidate.Key.Length)
                .ThenBy(static candidate => candidate.Description, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            if (option is null) continue;
            selected.Add(option);
            selectedKeys.Add(option.Key);
            if (selected.Count >= (system.InputProfile == EmulatorInputProfile.NintendoDs ? 8 : 6)) break;
        }
        return selected;
    }

    private static int CoreOptionMatchScore(LibretroCoreOptionDefinition option, string term)
    {
        var normalizedTerm = term.Replace(" ", "_", StringComparison.Ordinal);
        if (string.Equals(option.Key, term, StringComparison.OrdinalIgnoreCase) ||
            option.Key.EndsWith($"_{normalizedTerm}", StringComparison.OrdinalIgnoreCase)) return 0;
        if (option.Key.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase)) return 1;
        if (option.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase)) return 2;
        return 3;
    }

    private static string[] ImportantCoreOptionTerms(EmulatorSystemDefinition system) => system.Id switch
    {
        "gb" => new[] { "model", "color correction", "color_correction", "palette", "bootrom" },
        "gba" => new[] { "bios", "color correction", "color_correction", "frameskip" },
        "nes" => new[] { "region", "palette", "overscan" },
        "snes" => new[] { "region", "overscan", "hires" },
        "megadrive" => new[] { "region", "aspect", "overscan" },
        "segacd" => new[] { "region", "bios" },
        "sega8" => new[] { "region", "hardware", "fm sound", "fm_sound" },
        "pcengine" => new[] { "system card", "system_card", "region", "aspect" },
        "neogeo" => new[] { "bios", "region", "console mode", "system mode" },
        "ngp" => new[] { "language", "color" },
        "wonderswan" => new[] { "rotation", "color" },
        "ps1" => new[] { "region", "bios", "frameskip", "dithering", "clock" },
        "n64" => new[] { "resolution", "aspect", "framerate" },
        "nds" => new[] { "screen_layout1", "mic_input", "boot_mode", "sysfile_mode" },
        "psp" => new[] { "internal_resolution", "frameskip", "language", "memstick" },
        _ => new[] { "region", "bios", "model" },
    };

    private static bool CoreOptionMatches(LibretroCoreOptionDefinition option, string term) =>
        option.Key.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        option.Description.Contains(term, StringComparison.CurrentCultureIgnoreCase);


    private static string FriendlyCoreOptionLabel(LibretroCoreOptionDefinition option)
    {
        var text = $"{option.Key} {option.Description}";
        if (text.Contains("screen_layout", StringComparison.OrdinalIgnoreCase)) return "Screen layout";
        if (text.Contains("touch_mode", StringComparison.OrdinalIgnoreCase)) return "Touch controls";
        if (text.Contains("show_cursor", StringComparison.OrdinalIgnoreCase)) return "Stylus cursor";
        if (text.Contains("mic_input", StringComparison.OrdinalIgnoreCase)) return "Microphone";
        if (text.Contains("memcard2", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("second memory card", StringComparison.OrdinalIgnoreCase)) return "Second memory card";
        if (text.Contains("memory card", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memory_card", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memcard", StringComparison.OrdinalIgnoreCase)) return "Memory card";
        if (text.Contains("memstick", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memory stick", StringComparison.OrdinalIgnoreCase)) return "Memory Stick";
        if (text.Contains("pak1", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("controller pak", StringComparison.OrdinalIgnoreCase)) return "Controller accessory";
        if (text.Contains("internal_resolution", StringComparison.OrdinalIgnoreCase)) return "Internal resolution";
        if (text.Contains("frameskip", StringComparison.OrdinalIgnoreCase)) return "Frameskip";
        if (text.Contains("bios", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("sysfile", StringComparison.OrdinalIgnoreCase)) return "BIOS mode";
        if (text.Contains("region", StringComparison.OrdinalIgnoreCase)) return "Region";
        if (text.Contains("overscan", StringComparison.OrdinalIgnoreCase)) return "Screen borders";
        if (text.Contains("palette", StringComparison.OrdinalIgnoreCase)) return "Color palette";
        if (text.Contains("color correction", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("color_correction", StringComparison.OrdinalIgnoreCase)) return "Color correction";
        if (text.Contains("language", StringComparison.OrdinalIgnoreCase)) return "System language";
        if (text.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("hardware", StringComparison.OrdinalIgnoreCase)) return "Console model";
        if (text.Contains("aspect", StringComparison.OrdinalIgnoreCase)) return "Aspect ratio";
        if (text.Contains("dithering", StringComparison.OrdinalIgnoreCase)) return "Color dithering";
        return option.Description;
    }

    private static string FriendlyCoreOptionHelp(LibretroCoreOptionDefinition option)
    {
        var text = $"{option.Key} {option.Description}";
        if (text.Contains("memcard2", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("second memory card", StringComparison.OrdinalIgnoreCase))
            return "Enables the second PlayStation memory-card slot. This card is shared by PlayStation games.";
        if (text.Contains("memory card", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memory_card", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memcard", StringComparison.OrdinalIgnoreCase))
            return "Controls memory-card support provided by this emulator core.";
        if (text.Contains("memstick", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("memory stick", StringComparison.OrdinalIgnoreCase))
            return "Controls the emulated removable storage used by this console.";
        if (text.Contains("pak1", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("controller pak", StringComparison.OrdinalIgnoreCase))
            return "Choose the accessory inserted into the first controller.";
        if (text.Contains("region", StringComparison.OrdinalIgnoreCase))
            return "Automatic is recommended unless a game runs at the wrong speed or uses the wrong video standard.";
        if (text.Contains("bios", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("sysfile", StringComparison.OrdinalIgnoreCase))
            return "Use the default unless a specific game requires original system files.";
        if (text.Contains("screen_layout", StringComparison.OrdinalIgnoreCase))
            return "Choose how the two Nintendo DS screens are arranged inside the plugin window.";
        if (text.Contains("touch", StringComparison.OrdinalIgnoreCase))
            return "Controls how the Nintendo DS touch screen receives input.";
        if (text.Contains("cursor", StringComparison.OrdinalIgnoreCase))
            return "Controls when the Nintendo DS stylus cursor is visible.";
        if (text.Contains("mic", StringComparison.OrdinalIgnoreCase))
            return "Some Nintendo DS games require microphone input to continue.";
        if (text.Contains("resolution", StringComparison.OrdinalIgnoreCase))
            return "Higher values look sharper but can reduce performance.";
        if (text.Contains("frameskip", StringComparison.OrdinalIgnoreCase))
            return "Leave disabled unless the game is too slow on your computer.";
        if (text.Contains("overscan", StringComparison.OrdinalIgnoreCase))
            return "Hides or shows the unused border area found on some older games.";
        if (text.Contains("palette", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("color", StringComparison.OrdinalIgnoreCase))
            return "Changes how the original console colors are displayed.";
        if (text.Contains("language", StringComparison.OrdinalIgnoreCase))
            return "Selects the language reported by the emulated system.";
        return string.Empty;
    }

    private void DrawNintendoDsLayoutPreview(EmulatorSystemDefinition system)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var options = configuration.OptionsFor(system);
        var layout = options.TryGetValue("melonds_screen_layout1", out var configured)
            ? configured
            : "top-bottom";
        var availableWidth = MathF.Min(430f * scale, ImGui.GetContentRegionAvail().X);
        var previewHeight = 126f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + new Vector2(availableWidth, previewHeight),
            ImGui.GetColorU32(FrontendTheme.Panel), 0f);
        draw.AddRect(origin, origin + new Vector2(availableWidth, previewHeight),
            ImGui.GetColorU32(FrontendTheme.Border), 0f);

        var innerMin = origin + new Vector2(16f, 14f) * scale;
        var innerMax = origin + new Vector2(availableWidth / scale - 16f, previewHeight / scale - 14f) * scale;
        var gap = 8f * scale;
        var lower = layout.ToLowerInvariant();
        if (lower.Contains("left-right") || lower.Contains("right-left") || lower.Contains("hybrid"))
        {
            var width = (innerMax.X - innerMin.X - gap) * 0.5f;
            var touchFirst = lower.Contains("right-left", StringComparison.Ordinal) ||
                lower.Contains("hybrid-bottom", StringComparison.Ordinal);
            DrawDsPreviewScreen(innerMin, new Vector2(innerMin.X + width, innerMax.Y),
                touchFirst ? "TOUCH" : "TOP");
            DrawDsPreviewScreen(new Vector2(innerMin.X + width + gap, innerMin.Y), innerMax,
                touchFirst ? "TOP" : "TOUCH");
        }
        else if (lower is "top" or "bottom")
        {
            DrawDsPreviewScreen(innerMin, innerMax, lower == "top" ? "TOP" : "TOUCH");
        }
        else
        {
            var height = (innerMax.Y - innerMin.Y - gap) * 0.5f;
            var touchFirst = lower is "bottom-top" or "upside-down";
            DrawDsPreviewScreen(innerMin, new Vector2(innerMax.X, innerMin.Y + height),
                touchFirst ? "TOUCH" : "TOP");
            DrawDsPreviewScreen(new Vector2(innerMin.X, innerMin.Y + height + gap), innerMax,
                touchFirst ? "TOP" : "TOUCH");
        }
        ImGui.Dummy(new Vector2(availableWidth, previewHeight));
        DrawMutedWrapped($"Current layout: {layout}");
        ImGui.Dummy(new Vector2(1f, 8f * scale));
    }

    private static void DrawDsPreviewScreen(Vector2 min, Vector2 max, string label)
    {
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, ImGui.GetColorU32(FrontendTheme.PanelRaised), 0f);
        draw.AddRect(min, max, ImGui.GetColorU32(FrontendTheme.Accent), 0f);
        var textSize = ImGui.CalcTextSize(label);
        draw.AddText(min + (max - min - textSize) * 0.5f, ImGui.GetColorU32(FrontendTheme.Muted), label);
    }

    private IReadOnlyList<LibretroCoreOptionDefinition> LoadCoreOptionDefinitions(
        EmulatorSystemDefinition system, bool refresh, out bool loading)
    {
        loading = false;

        if (session is not null && string.Equals(session.System.CoreFileName, system.CoreFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!refresh && coreOptionCache.TryGetValue(system.Id, out var activeCached))
            {
                return activeCached;
            }

            var activeDefinitions = MergeSystemOptionDefinitions(system, session.CoreOptionDefinitions);
            coreOptionCache[system.Id] = activeDefinitions;
            importantCoreOptionCache.Remove(system.Id);
            coreOptionErrors.Remove(system.Id);
            return activeDefinitions;
        }

        if (refresh)
        {
            coreOptionCache.Remove(system.Id);
            importantCoreOptionCache.Remove(system.Id);
            coreOptionErrors.Remove(system.Id);
        }
        else if (coreOptionCache.TryGetValue(system.Id, out var cached))
        {
            return cached;
        }

        if (coreOptionLoads.TryGetValue(system.Id, out var pending))
        {
            if (!pending.IsCompleted)
            {
                loading = true;
                return Array.Empty<LibretroCoreOptionDefinition>();
            }

            coreOptionLoads.Remove(system.Id);
            var result = pending.GetAwaiter().GetResult();
            if (result.Cancelled)
            {
                return Array.Empty<LibretroCoreOptionDefinition>();
            }

            var definitions = MergeSystemOptionDefinitions(system, result.Definitions);
            coreOptionCache[system.Id] = definitions;
            importantCoreOptionCache.Remove(system.Id);
            if (string.IsNullOrWhiteSpace(result.Error))
            {
                coreOptionErrors.Remove(system.Id);
            }
            else
            {
                coreOptionErrors[system.Id] = result.Error;
            }

            return definitions;
        }

        var corePath = Path.GetFullPath(Path.Combine(coreDirectory, system.CoreFileName));
        if (!File.Exists(corePath))
        {
            coreOptionErrors[system.Id] = "Core options cannot be read because the core file is missing.";
            var empty = MergeSystemOptionDefinitions(system, Array.Empty<LibretroCoreOptionDefinition>());
            coreOptionCache[system.Id] = empty;
            return empty;
        }

        var systemDirectory = Path.Combine(emulatorRoot, "system");
        var saveDirectory = Path.Combine(emulatorRoot, "saves", system.Id);
        var optionsSnapshot = new Dictionary<string, string>(
            configuration.OptionsFor(system), StringComparer.Ordinal);
        var analogController = configuration.UsesAnalogController(system);
        var preserveSaveMemory = configuration.ProtectSaveMemoryOnStateLoad;
        var token = coreOptionLoadCancellation.Token;

        pending = Task.Run(async () =>
        {
            try
            {
                await coreOptionLoadGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    EnsureSystemFiles();
                    using var core = new LibretroCore(corePath, systemDirectory, saveDirectory,
                        enableAudio: false,
                        coreOptions: optionsSnapshot,
                        analogController: analogController,
                        preserveSaveRamOnStateLoad: preserveSaveMemory);
                    var definitions = core.CoreOptionDefinitions.Values
                        .OrderBy(static option => option.CategoryDescription,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(static option => option.Description,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                    return new CoreOptionLoadResult(definitions, null, false);
                }
                finally
                {
                    coreOptionLoadGate.Release();
                }
            }
            catch (OperationCanceledException)
            {
                return new CoreOptionLoadResult(Array.Empty<LibretroCoreOptionDefinition>(), null, true);
            }
            catch (Exception exception)
            {
                EmulatorLog.Warning(
                    $"[Allagan Retro Pocket] Core option discovery failed for {system.Id}: {exception.Message}");
                return new CoreOptionLoadResult(
                    Array.Empty<LibretroCoreOptionDefinition>(),
                    $"Core option discovery failed: {exception.Message}",
                    false);
            }
        });

        coreOptionLoads[system.Id] = pending;
        loading = true;
        return Array.Empty<LibretroCoreOptionDefinition>();
    }

    private bool IsCoreOptionDiscoveryRunning(EmulatorSystemDefinition system)
    {
        foreach (var (systemId, task) in coreOptionLoads)
        {
            if (task.IsCompleted)
            {
                continue;
            }

            var pendingSystem = EmulatorSystemCatalog.ById(systemId);
            if (pendingSystem is not null && string.Equals(pendingSystem.CoreFileName, system.CoreFileName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawCoreOptionsLoading()
    {
        var dots = (int)(ImGui.GetTime() * 2.5) % 4;
        ImGui.TextUnformatted($"Loading core settings{new string('.', dots)}");
        DrawMutedWrapped("The core is being inspected in the background. You can keep using Final Fantasy XIV.");
    }

    private sealed record CoreOptionLoadResult(
        IReadOnlyList<LibretroCoreOptionDefinition> Definitions,
        string? Error,
        bool Cancelled);

    private static IReadOnlyList<LibretroCoreOptionDefinition> MergeSystemOptionDefinitions(
        EmulatorSystemDefinition system, IReadOnlyList<LibretroCoreOptionDefinition> discovered)
    {
        var merged = discovered.ToDictionary(static option => option.Key, StringComparer.Ordinal);
        foreach (var fallback in FallbackCoreOptions(system))
        {
            if (!merged.TryGetValue(fallback.Key, out var existing))
            {
                merged[fallback.Key] = fallback;
                continue;
            }

            // Old cores may register a variable without a parsed value list. Keep the
            // core's wording while supplying the known finite choices for this system.
            if (existing.Choices.Count == 0 && fallback.Choices.Count > 0)
            {
                merged[fallback.Key] = existing with
                {
                    Choices = fallback.Choices,
                    DefaultValue = string.IsNullOrWhiteSpace(existing.DefaultValue)
                        ? fallback.DefaultValue
                        : existing.DefaultValue,
                };
            }
        }

        return merged.Values
            .OrderBy(static option => option.CategoryDescription, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static option => option.Description, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<LibretroCoreOptionDefinition> FallbackCoreOptions(
        EmulatorSystemDefinition system)
    {
        if (system.InputProfile != EmulatorInputProfile.NintendoDs) yield break;

        yield return FallbackOption(
            "melonds_number_of_screen_layouts",
            "Number of screen layouts",
            "Choose how many layouts can be cycled with the Next Screen Layout input.",
            "1",
            Enumerable.Range(1, 8).Select(number => Choice(number.ToString(), number.ToString())).ToArray());
        yield return FallbackOption(
            "melonds_screen_layout1",
            "Screen layout 1",
            "Arrangement and orientation of the Nintendo DS screens.",
            "top-bottom",
            NintendoDsLayoutChoices);
        yield return FallbackOption(
            "melonds_screen_layout2",
            "Screen layout 2",
            "Second arrangement used when more than one screen layout is enabled.",
            "top-bottom",
            NintendoDsLayoutChoices);
        yield return FallbackOption(
            "melonds_touch_mode",
            "Touch input mode",
            "Select automatic touch handling, direct pointer input or right-stick touch control.",
            "auto",
            Choice("auto", "Automatic"), Choice("touch", "Pointer / touch"),
            Choice("joystick", "Right stick"));
        yield return FallbackOption(
            "melonds_show_cursor",
            "Touch cursor visibility",
            "Control when the stylus cursor is drawn over the touch screen.",
            "timeout",
            Choice("disabled", "Hidden"), Choice("touching", "While touching"),
            Choice("timeout", "Until timeout"), Choice("always", "Always"));
        yield return FallbackOption(
            "melonds_mic_input",
            "Microphone input",
            "Select the source used when the Nintendo DS microphone input is active.",
            "blow",
            Choice("blow", "Blow noise"), Choice("noise", "White noise"),
            Choice("microphone", "Physical microphone"));
    }

    private static LibretroCoreOptionDefinition FallbackOption(string key, string description,
        string info, string defaultValue, params LibretroCoreOptionChoice[] choices) =>
        new(key, description, info, "system", "System-specific options", choices, defaultValue);

    private static LibretroCoreOptionChoice Choice(string value, string label) => new(value, label);

    private static readonly LibretroCoreOptionChoice[] NintendoDsLayoutChoices =
    {
        Choice("top-bottom", "Top / Bottom"),
        Choice("bottom-top", "Bottom / Top"),
        Choice("left-right", "Left / Right"),
        Choice("right-left", "Right / Left"),
        Choice("top", "Top screen only"),
        Choice("bottom", "Touch screen only"),
        Choice("hybrid-top", "Hybrid — top focus"),
        Choice("hybrid-bottom", "Hybrid — touch focus"),
        Choice("flipped-hybrid-top", "Flipped hybrid — top focus"),
        Choice("flipped-hybrid-bottom", "Flipped hybrid — touch focus"),
        Choice("rotate-left", "Rotate left"),
        Choice("rotate-right", "Rotate right"),
        Choice("upside-down", "Upside down"),
    };

    private void DrawInputSettingsPage(EmulatorSystemDefinition system)
    {
        DrawSettingsPageTitle("Input",
            $"Keyboard and controller mappings for {system.Name}. Click a binding and press a key, button or stick direction.");
        var bindings = configuration.InputFor(system);

        DrawSettingsSection("Automatic controller setup", () =>
        {
            DrawMutedWrapped("Fill the Secondary slots with a standard controller layout. Primary bindings stay unchanged, and Automatic works with controllers already recognized by FFXIV.");
            ImGui.Dummy(new Vector2(1f, 5f * ImGuiHelpers.GlobalScale));
            if (ImGui.Button("Auto-map controller", new Vector2(MathF.Min(240f * ImGuiHelpers.GlobalScale,
                    ImGui.GetContentRegionAvail().X), 0f)))
            {
                controllerAutoMapPresetIndex = (int)configuration.ControllerPreset;
                controllerAutoMapAllSystems = true;
                controllerAutoMapPopupOpen = true;
                controllerAutoMapPopupRequested = true;
            }
            if (!string.IsNullOrWhiteSpace(controllerAutoMapMessage))
            {
                ImGui.Dummy(new Vector2(1f, 5f * ImGuiHelpers.GlobalScale));
                ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Success);
                ImGui.TextWrapped(controllerAutoMapMessage);
                ImGui.PopStyleColor();
            }
        });

        if (system.InputProfile is EmulatorInputProfile.PlayStation or EmulatorInputProfile.Nintendo64 or
            EmulatorInputProfile.PlayStationPortable)
        {
            DrawSettingsSection("Controller", () => DrawControllerTypeSettings(system));
        }

        if (system.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            DrawSettingsSection("Touch screen with controller", () =>
                DrawNintendoDsTouchControllerSettings(system, bindings));
        }

        DrawSettingsSection("Player 1 controls", () =>
        {
            DrawBindingTable("input-bindings", InputButtonRows
                .Where(row => (system.Controls & row.Button) != 0)
                .Where(row => system.InputProfile != EmulatorInputProfile.NintendoDs ||
                    row.Button != EmulatorButtons.L2 && row.Button != EmulatorButtons.R2 &&
                    row.Button != EmulatorButtons.L3 && row.Button != EmulatorButtons.R3)
                .Select(row => (Label: row.Label, Id: $"binding-{row.Button}",
                    Binding: bindings.For(row.Button))));
        });

        var usesLeftAnalog = system.InputProfile is EmulatorInputProfile.Nintendo64 or
            EmulatorInputProfile.PlayStationPortable ||
            system.InputProfile == EmulatorInputProfile.PlayStation && configuration.UsesAnalogController(system);
        if (usesLeftAnalog)
        {
            DrawSettingsSection(system.InputProfile == EmulatorInputProfile.Nintendo64
                ? "Analog stick"
                : "Left analog stick", () =>
            {
                DrawBindingTable("left-analog-bindings", AnalogBindingRows("LeftStick", bindings));
            });
        }

        if (system.InputProfile == EmulatorInputProfile.PlayStation && configuration.UsesAnalogController(system))
        {
            DrawSettingsSection("Right analog stick", () =>
            {
                DrawBindingTable("right-analog-bindings", AnalogBindingRows("RightStick", bindings));
            });
        }

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            DrawSettingsSection("C buttons", () =>
            {
                DrawBindingTable("n64-c-bindings", new (string Label, string Id, InputActionBinding Binding)[]
                {
                    ("C Up", "binding-c-up", bindings.For("CUp")),
                    ("C Down", "binding-c-down", bindings.For("CDown")),
                    ("C Left", "binding-c-left", bindings.For("CLeft")),
                    ("C Right", "binding-c-right", bindings.For("CRight")),
                });
            });
        }

        if (system.InputProfile == EmulatorInputProfile.NintendoDs)
        {
            DrawSettingsSection("Nintendo DS shortcuts", () =>
            {
                DrawBindingTable("nds-shortcuts", new (string Label, string Id, InputActionBinding Binding)[]
                {
                    ("Use microphone", "nds-microphone", bindings.For(EmulatorButtons.L2)),
                    ("Next screen layout", "nds-next-layout", bindings.For(EmulatorButtons.R2)),
                    ("Close or open lid", "nds-lid", bindings.For(EmulatorButtons.L3)),
                });
                DrawMutedWrapped("Optional shortcuts for games that use the microphone, screen-layout switching, or lid controls.");
            });
        }

        DrawSettingsSection("Frontend hotkeys", () =>
        {
            DrawBindingTable("frontend-hotkeys", new (string Label, string Id, InputActionBinding Binding)[]
            {
                ("Quick menu", "quick-menu", configuration.QuickMenuBinding),
                ("Fast-forward (hold)", "fast-forward-hold", configuration.FastForwardBinding),
                ("Fast-forward toggle", "fast-forward-toggle", configuration.FastForwardToggleBinding),
            });
            DrawMutedWrapped("Each Primary or Secondary slot accepts keyboard keys, controller buttons, stick directions, or a combination of up to three inputs.");
        });

        if (ImGui.Button("Reset this system mapping"))
        {
            configuration.CoreInputs.Remove(system.Id);
            _ = configuration.InputFor(system);
            configuration.Save();
        }
    }

    private void DrawNintendoDsTouchControllerSettings(EmulatorSystemDefinition system,
        InputBindings bindings)
    {
        var options = configuration.OptionsFor(system);
        var modeValues = new[] { "auto", "touch", "joystick" };
        var modeLabels = new[]
        {
            "Mouse or right stick (recommended)",
            "Mouse only",
            "Right stick only",
        };
        var configuredMode = options.TryGetValue("melonds_touch_mode", out var touchMode)
            ? touchMode
            : "auto";
        var modeIndex = Array.FindIndex(modeValues, value =>
            string.Equals(value, configuredMode, StringComparison.OrdinalIgnoreCase));
        if (modeIndex < 0) modeIndex = 0;

        ImGui.SetNextItemWidth(MathF.Min(430f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
        if (ImGui.Combo("Touch screen control", ref modeIndex, modeLabels, modeLabels.Length))
        {
            options["melonds_touch_mode"] = modeValues[modeIndex];
            configuration.Save();
        }

        var cursorValues = new[] { "timeout", "touching", "always", "disabled" };
        var cursorLabels = new[] { "Until idle (recommended)", "While touching", "Always", "Hidden" };
        var configuredCursor = options.TryGetValue("melonds_show_cursor", out var cursorMode)
            ? cursorMode
            : "timeout";
        var cursorIndex = Array.FindIndex(cursorValues, value =>
            string.Equals(value, configuredCursor, StringComparison.OrdinalIgnoreCase));
        if (cursorIndex < 0) cursorIndex = 0;

        ImGui.SetNextItemWidth(MathF.Min(430f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
        if (ImGui.Combo("Cursor visibility", ref cursorIndex, cursorLabels, cursorLabels.Length))
        {
            options["melonds_show_cursor"] = cursorValues[cursorIndex];
            configuration.Save();
        }

        DrawMutedWrapped("Move the virtual stylus with the right analog stick and press the Touch screen binding to tap. In the recommended mode, the mouse continues to work too. This choice is applied the next time the game starts.");

        if (modeIndex == 1)
        {
            DrawMutedWrapped("Right-stick touch control is disabled while Mouse only is selected.");
            return;
        }

        ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
        ImGui.TextColored(FrontendTheme.Muted, "Cursor movement");
        DrawBindingTable("nds-touch-cursor", AnalogBindingRows("RightStick", bindings));

        ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
        ImGui.TextColored(FrontendTheme.Muted, "Tap the touch screen");
        DrawBindingTable("nds-touch-press", new (string Label, string Id, InputActionBinding Binding)[]
        {
            ("Touch screen", "nds-touch", bindings.For(EmulatorButtons.R3)),
        });
    }

    private void DrawControllerTypeSettings(EmulatorSystemDefinition system)
    {
        if (system.InputProfile == EmulatorInputProfile.PlayStation)
        {
            var values = new[] { "dualshock", "digital" };
            var labels = new[] { "DualShock / Analog Controller", "Digital Controller" };
            var current = Array.FindIndex(values, value => string.Equals(value,
                configuration.ControllerTypeFor(system), StringComparison.OrdinalIgnoreCase));
            if (current < 0) current = 0;
            ImGui.SetNextItemWidth(MathF.Min(360f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
            if (ImGui.Combo("Controller type", ref current, labels, labels.Length))
            {
                configuration.ControllerTypes[system.Id] = values[current];
                configuration.Save();
            }
            DrawMutedWrapped("DualShock is recommended. Use Digital Controller only for games that do not support analog controllers. The change takes effect the next time a game starts.");
            return;
        }

        if (system.InputProfile == EmulatorInputProfile.Nintendo64)
        {
            ImGui.TextColored(FrontendTheme.Muted, "Controller type");
            ImGui.SameLine();
            ImGui.TextUnformatted("Nintendo 64 Controller");
            var options = configuration.OptionsFor(system);
            var values = new[] { "memory", "none" };
            var labels = new[] { "Controller Pak", "No accessory" };
            var configured = options.TryGetValue("mupen64plus-pak1", out var pak)
                ? pak
                : "memory";
            var current = Array.FindIndex(values, value =>
                string.Equals(value, configured, StringComparison.OrdinalIgnoreCase));
            if (current < 0) current = 0;
            ImGui.SetNextItemWidth(MathF.Min(360f * ImGuiHelpers.GlobalScale,
                ImGui.GetContentRegionAvail().X));
            if (ImGui.Combo("Controller accessory", ref current, labels, labels.Length))
            {
                options["mupen64plus-pak1"] = values[current];
                configuration.Save();
            }
            DrawMutedWrapped("Controller Pak is recommended because some games save data to the controller. Other accessory types remain available under Advanced settings.");
            return;
        }

        ImGui.TextColored(FrontendTheme.Muted, "Controller type");
        ImGui.SameLine();
        ImGui.TextUnformatted("PSP controls");
        DrawMutedWrapped("The PSP analog stick can be mapped independently from the D-pad.");
    }

    private static IEnumerable<(string Label, string Id, InputActionBinding Binding)> AnalogBindingRows(
        string prefix, InputBindings bindings)
    {
        yield return ("Up", $"{prefix}-up", bindings.For($"{prefix}Up"));
        yield return ("Down", $"{prefix}-down", bindings.For($"{prefix}Down"));
        yield return ("Left", $"{prefix}-left", bindings.For($"{prefix}Left"));
        yield return ("Right", $"{prefix}-right", bindings.For($"{prefix}Right"));
    }

    private static readonly (EmulatorButtons Button, string Label)[] InputButtonRows =
    {
        (EmulatorButtons.Up, "D-pad Up"),
        (EmulatorButtons.Down, "D-pad Down"),
        (EmulatorButtons.Left, "D-pad Left"),
        (EmulatorButtons.Right, "D-pad Right"),
        (EmulatorButtons.A, "A"),
        (EmulatorButtons.B, "B"),
        (EmulatorButtons.X, "X"),
        (EmulatorButtons.Y, "Y"),
        (EmulatorButtons.L, "L"),
        (EmulatorButtons.R, "R"),
        (EmulatorButtons.L2, "L2"),
        (EmulatorButtons.R2, "R2"),
        (EmulatorButtons.L3, "L3"),
        (EmulatorButtons.R3, "R3"),
        (EmulatorButtons.Start, "Start"),
        (EmulatorButtons.Select, "Select"),
    };

    private void DrawBindingTable(string id,
        IEnumerable<(string Label, string Id, InputActionBinding Binding)> rows)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var compact = ImGui.GetContentRegionAvail().X < 540f * scale;
        if (compact)
        {
            foreach (var row in rows)
            {
                ImGui.PushID(row.Id);
                ImGui.TextUnformatted(row.Label);
                DrawBindingButton("Primary", "primary", row.Label, row.Binding.Primary);
                DrawBindingButton("Secondary", "secondary", row.Label, row.Binding.Secondary);
                ImGui.Dummy(new Vector2(1f, 7f * scale));
                ImGui.PopID();
            }
            return;
        }

        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersOuter |
            ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg;
        if (!ImGui.BeginTable(id, 3, flags)) return;

        ImGui.TableSetupColumn("Control", ImGuiTableColumnFlags.WidthStretch, 0.75f);
        ImGui.TableSetupColumn("Primary", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn("Secondary", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableHeadersRow();
        foreach (var row in rows)
        {
            ImGui.PushID(row.Id);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextWrapped(row.Label);
            ImGui.TableSetColumnIndex(1);
            DrawBindingButton(string.Empty, "primary", row.Label, row.Binding.Primary);
            ImGui.TableSetColumnIndex(2);
            DrawBindingButton(string.Empty, "secondary", row.Label, row.Binding.Secondary);
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawBindingButton(string prefix, string id, string actionLabel, InputChord chord)
    {
        var name = InputRouter.ChordName(chord, configuration.ControllerPreset);
        var label = string.IsNullOrEmpty(prefix) ? name : $"{prefix}: {name}";
        if (ImGui.Button($"{label}##{id}", new Vector2(-1f, 0f)))
        {
            bindingCaptureLabel = $"{actionLabel} — {(id == "primary" ? "Primary" : "Secondary")}";
            bindingCaptureTarget = chord;
            bindingCaptureTokens = chord.Inputs.Select(static token => token.Clone()).ToList();
            bindingCaptureArmed = false;
            bindingCaptureOpen = true;
            bindingPopupRequested = true;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(name);
    }

    private void DrawBindingCapturePopup()
    {
        if (bindingPopupRequested)
        {
            ImGui.OpenPopup("Set input binding");
            bindingPopupRequested = false;
        }

        var popupCenter = applicationWindowPos + applicationWindowSize * 0.5f;
        ImGui.SetNextWindowPos(popupCenter, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        if (!ImGui.BeginPopupModal("Set input binding", ref bindingCaptureOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            if (!bindingCaptureOpen && bindingCaptureTarget is not null) CloseBindingCapture();
            return;
        }

        input.SetCaptured(true);
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TextUnformatted(bindingCaptureLabel);
        DrawMutedWrapped("Release held inputs, then press a keyboard key, controller button, or move a stick in the desired direction. Up to three simultaneous inputs are accepted.");
        ImGui.Dummy(new Vector2(1f, 8f * scale));

        var pressed = input.ReadPressedBindingTokens().Take(3).ToList();
        if (!bindingCaptureArmed)
        {
            if (pressed.Count == 0) bindingCaptureArmed = true;
        }
        else if (pressed.Count > 0)
        {
            bindingCaptureTokens = pressed.Select(static token => token.Clone()).ToList();
        }

        var preview = new InputChord
        {
            Inputs = bindingCaptureTokens.Select(static token => token.Clone()).ToList(),
        };
        ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Accent);
        ImGui.TextWrapped(bindingCaptureArmed
            ? InputRouter.ChordName(preview, configuration.ControllerPreset)
            : "Release all keys, buttons and sticks to begin…");
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(1f, 10f * scale));

        ImGui.BeginDisabled(bindingCaptureTokens.Count == 0 || !bindingCaptureArmed);
        if (ImGui.Button("Save", new Vector2(110f * scale, 0f)))
        {
            if (bindingCaptureTarget is not null)
            {
                bindingCaptureTarget.Inputs = bindingCaptureTokens
                    .Select(static token => token.Clone()).Take(3).ToList();
                bindingCaptureTarget.Normalize();
                configuration.Save();
            }
            ImGui.CloseCurrentPopup();
            CloseBindingCapture();
        }
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button("Clear", new Vector2(110f * scale, 0f)))
        {
            if (bindingCaptureTarget is not null)
            {
                bindingCaptureTarget.Inputs.Clear();
                configuration.Save();
            }
            ImGui.CloseCurrentPopup();
            CloseBindingCapture();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(110f * scale, 0f)))
        {
            ImGui.CloseCurrentPopup();
            CloseBindingCapture();
        }
        ImGui.EndPopup();
    }

    private void CloseBindingCapture()
    {
        input.SetCaptured(false);
        bindingCaptureTarget = null;
        bindingCaptureTokens.Clear();
        bindingCaptureArmed = false;
        bindingCaptureOpen = false;
        bindingCaptureLabel = string.Empty;
    }

    private void DrawControllerAutoMapPopup(EmulatorSystemDefinition currentSystem)
    {
        if (controllerAutoMapPopupRequested)
        {
            ImGui.OpenPopup("Auto-map controller");
            controllerAutoMapPopupRequested = false;
        }

        var popupCenter = applicationWindowPos + applicationWindowSize * 0.5f;
        var popupScale = ImGuiHelpers.GlobalScale;
        var popupWidth = MathF.Min(500f * popupScale,
            MathF.Max(320f * popupScale, applicationWindowSize.X - 48f * popupScale));
        ImGui.SetNextWindowPos(popupCenter, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(popupWidth, 0f), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal("Auto-map controller", ref controllerAutoMapPopupOpen,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        ImGui.TextUnformatted("Set up your controller");
        DrawMutedWrapped("Choose the controller family and Allagan Retro Pocket will fill the Secondary controller slots. Primary bindings are not changed.");
        ImGui.Dummy(new Vector2(1f, 8f * scale));

        controllerAutoMapPresetIndex = Math.Clamp(controllerAutoMapPresetIndex, 0,
            ControllerAutoMapper.PresetLabels.Length - 1);
        ImGui.SetNextItemWidth(-1f);
        ImGui.Combo("##controller-preset", ref controllerAutoMapPresetIndex,
            ControllerAutoMapper.PresetLabels, ControllerAutoMapper.PresetLabels.Length);
        DrawMutedWrapped("Automatic is recommended. FFXIV provides a common button layout, so supported Xbox, PlayStation, Nintendo and DirectInput controllers use the same reliable positions.");

        ImGui.Dummy(new Vector2(1f, 5f * scale));
        ImGui.Checkbox("Apply to every console", ref controllerAutoMapAllSystems);
        DrawMutedWrapped(controllerAutoMapAllSystems
            ? "Updates the Secondary controller slots for every system."
            : $"Updates only {currentSystem.Name}.");
        ImGui.Dummy(new Vector2(1f, 10f * scale));

        var buttonWidth = 116f * scale;
        if (ImGui.Button("Apply", new Vector2(buttonWidth, 0f)))
        {
            var preset = (ControllerAutoMapPreset)controllerAutoMapPresetIndex;
            configuration.ControllerPreset = preset;
            IEnumerable<EmulatorSystemDefinition> targets = controllerAutoMapAllSystems
                ? EmulatorSystemCatalog.All
                : new[] { currentSystem };
            foreach (var target in targets)
            {
                ControllerAutoMapper.Apply(target, configuration.InputFor(target));
            }

            // Analog-capable PlayStation games should use the DualShock device after
            // automatic setup. Players can still switch back to Digital Controller.
            configuration.ControllerTypes["ps1"] = "dualshock";
            configuration.Save();
            controllerAutoMapMessage = controllerAutoMapAllSystems
                ? $"Controller mapped for all systems ({ControllerAutoMapper.Label(preset)})."
                : $"Controller mapped for {currentSystem.Name}.";
            ImGui.CloseCurrentPopup();
            controllerAutoMapPopupOpen = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0f)))
        {
            ImGui.CloseCurrentPopup();
            controllerAutoMapPopupOpen = false;
        }
        ImGui.EndPopup();
    }

    private void DrawVideoSettingsPage()
    {
        DrawSettingsPageTitle("Video", "Image scaling, aspect ratio and filtering.");
        DrawSettingsSection("Display", () =>
        {
            var scaleMode = (int)configuration.GameplayScale;
            if (ImGui.Combo("Scaling", ref scaleMode, GameplayScaleLabels, GameplayScaleLabels.Length))
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
            if (ImGui.Combo("Video filter", ref filter, VideoFilterLabels, VideoFilterLabels.Length))
            {
                configuration.VideoFilter = (EmulatorVideoFilter)filter;
                configuration.Save();
            }
        });
        DrawSettingsSection("Behavior", () =>
        {
            var pauseUnfocused = configuration.PauseWhenUnfocused;
            if (ImGui.Checkbox("Pause when the plugin window loses focus", ref pauseUnfocused))
            {
                configuration.PauseWhenUnfocused = pauseUnfocused;
                configuration.Save();
            }
        });
    }

    private void DrawAudioAndSpeedSettingsPage()
    {
        DrawSettingsPageTitle("Audio & speed", "Volume, audio latency and fast-forward behavior.");
        DrawSettingsSection("Audio", () =>
        {
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

            var latency = configuration.AudioLatencyMs;
            if (ImGui.SliderInt("Output latency", ref latency, 30, 250, "%d ms"))
            {
                configuration.AudioLatencyMs = latency;
                configuration.Save();
            }
            DrawMutedWrapped("Latency changes are applied the next time a game starts.");
        });

        DrawSettingsSection("Fast-forward", () =>
        {
            var speed = configuration.FastForwardSpeed;
            if (ImGui.SliderInt("Speed multiplier", ref speed, 2, 8, "%dx"))
            {
                configuration.FastForwardSpeed = speed;
                configuration.Save();
            }

            DrawMutedWrapped("Set separate Hold and Toggle shortcuts under Input → Frontend hotkeys.");

            var muteFastForward = configuration.MuteFastForward;
            if (ImGui.Checkbox("Mute while fast-forwarding", ref muteFastForward))
            {
                configuration.MuteFastForward = muteFastForward;
                configuration.Save();
            }
        });
    }

    private void DrawStorageAndMediaSettingsPage(EmulatorSystemDefinition system)
    {
        DrawSettingsPageTitle("Storage & media",
            "Memory cards, cartridge saves, automatic resume and disc switching.");

        var persistentSectionTitle = string.Equals(system.Id, "ps1", StringComparison.OrdinalIgnoreCase)
            ? "Memory cards"
            : "Persistent save data";
        DrawSettingsSection(persistentSectionTitle, () =>
        {
            ImGui.TextWrapped(system.SaveDescription);
            DrawMutedWrapped("The emulator writes this data automatically, like the original console. It is kept separately for each game.");
            if (string.Equals(system.Id, "ps1", StringComparison.OrdinalIgnoreCase))
            {
                DrawMutedWrapped("Memory card 1 is stored separately for each game. The optional second card is shared by PlayStation games and can be enabled below under Console storage. There is no manual card-swap button because the core manages both files automatically.");
            }
            var saveFolder = Path.Combine(emulatorRoot, "saves", system.Id);
            DrawMutedWrapped($"Stored in: {saveFolder}");
        });

        DrawSettingsSection("Automatic resume", () =>
        {
            DrawMutedWrapped("These preferences apply to every system.");
            DrawMutedWrapped("A resume point is a save state created automatically. It is separate from the game's normal memory card or cartridge save.");
            var autoSave = configuration.AutoSaveState;
            if (ImGui.Checkbox("Save a resume point when leaving a game", ref autoSave))
            {
                configuration.AutoSaveState = autoSave;
                configuration.Save();
            }
            DrawMutedWrapped("Used when you return to the library, stop the game or close the plugin.");

            var autoLoad = configuration.AutoLoadState;
            if (ImGui.Checkbox("Load the resume point when starting a game", ref autoLoad))
            {
                configuration.AutoLoadState = autoLoad;
                configuration.Save();
            }
            DrawMutedWrapped("Starts from the last automatic resume point when one is available.");

            var protect = configuration.ProtectSaveMemoryOnStateLoad;
            if (ImGui.Checkbox("Keep memory-card saves when loading a save state", ref protect))
            {
                configuration.ProtectSaveMemoryOnStateLoad = protect;
                configuration.Save();
            }
            DrawMutedWrapped("Recommended. Prevents an older save state from replacing newer memory-card or battery-save progress.");
        });

        DrawStorageCoreOptions(system);

        if (system.DiscBased)
        {
            DrawSettingsSection("Disc media", () => DrawDiscMediaSettings(system));
        }
    }

    private void DrawStorageCoreOptions(EmulatorSystemDefinition system)
    {
        var definitions = LoadCoreOptionDefinitions(system, false, out var loading);
        if (loading)
        {
            DrawSettingsSection("Console storage", () =>
            {
                ImGui.TextUnformatted("Loading storage settings...");
                DrawMutedWrapped("You can keep using the plugin while the core is inspected.");
            });
            return;
        }

        var storageOptions = definitions
            .Where(static option => option.Visible && IsStorageCoreOption(option))
            .Take(8)
            .ToArray();
        if (storageOptions.Length == 0) return;

        DrawSettingsSection("Console storage", () =>
        {
            foreach (var option in storageOptions)
                DrawCoreOption(system, option, false);
        });
    }

    private static bool IsStorageCoreOption(LibretroCoreOptionDefinition option)
    {
        var text = $"{option.Key} {option.Description} {option.CategoryDescription}";
        return text.Contains("memory card", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("memory_card", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("memcard", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("memory stick", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("memstick", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("save ram", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("saveram", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("backup ram", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("pak1", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("controller pak", StringComparison.OrdinalIgnoreCase);
    }

    private void DrawDiscMediaSettings(EmulatorSystemDefinition system)
    {
        if (string.Equals(storageOperationSystemId, system.Id, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(storageOperationMessage))
        {
            DrawColoredWrapped(FrontendTheme.Warning, storageOperationMessage);
            ImGui.Dummy(new Vector2(1f, 5f * ImGuiHelpers.GlobalScale));
        }

        var activeForSystem = session is not null && activeGame is not null &&
            string.Equals(activeGame.System.Id, system.Id, StringComparison.OrdinalIgnoreCase);
        if (activeForSystem && session!.DiskCount > 1)
        {
            var disk = Math.Clamp(session.DiskIndex, 0, session.DiskCount - 1);
            var labels = Enumerable.Range(1, session.DiskCount)
                .Select(static number => $"Disc {number}")
                .ToArray();
            ImGui.TextUnformatted("Current disc");
            ImGui.SetNextItemWidth(MathF.Min(360f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X));
            if (ImGui.Combo("##storage-current-disc", ref disk, labels, labels.Length))
            {
                try
                {
                    session.SetDiskIndex(disk);
                    storageOperationSystemId = system.Id;
                    storageOperationMessage = string.Empty;
                    ShowStateMessage($"Disc {disk + 1} selected.");
                }
                catch (Exception exception)
                {
                    storageOperationSystemId = system.Id;
                    storageOperationMessage = $"Disc change failed: {exception.Message}";
                }
            }
            DrawMutedWrapped("The same selector is available while playing in Quick Menu → Media.");
            return;
        }

        if (string.Equals(system.Id, "ps1", StringComparison.OrdinalIgnoreCase))
        {
            DrawMutedWrapped("For multi-disc PlayStation games, add an .m3u playlist that lists the disc images in order. Start the playlist, then open Quick Menu → Media to change discs.");
        }
        else
        {
            DrawMutedWrapped("When a running game exposes more than one disc, the disc selector appears in Quick Menu → Media.");
        }
    }

    private void DrawLibrarySettingsPage()
    {
        var system = EmulatorSystemCatalog.ById(selectedSystemId) ?? EmulatorSystemCatalog.All.First();
        DrawSettingsPageTitle("Library", $"Add {system.Name} games individually or scan ROM folders.");
        DrawSettingsSection("Game files", () => DrawGameFileSettings(system));
        DrawSettingsSection("ROM folders", DrawRomFolderSettings);
        DrawSettingsSection("Library maintenance", () =>
        {
            DrawMutedWrapped("Rescan the library after moving files or changing a folder outside the plugin.");
            if (ImGui.Button("Rescan library", new Vector2(180f * ImGuiHelpers.GlobalScale, 0f)))
            {
                RefreshLibrary();
            }
        });
    }

    private void DrawGameFileSettings(EmulatorSystemDefinition system)
    {
        DrawMutedWrapped("Choose one or more game files. The files are referenced from their current location and are not copied. A file may still appear after removal if it is also inside an added ROM folder.");
        if (ImGui.Button($"Add {system.Name} games", new Vector2(-1f, 0f)))
        {
            OpenGameFileDialog(system);
        }

        var records = configuration.RomFiles
            .Where(record => string.Equals(record.SystemId, system.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (records.Count == 0)
        {
            ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
            DrawMutedWrapped($"No {system.Name} files were added individually.");
            return;
        }

        ImGui.Dummy(new Vector2(1f, 8f * ImGuiHelpers.GlobalScale));
        var flags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerH |
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("explicit-game-files", 2, flags)) return;
        ImGui.TableSetupColumn("Game file", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 84f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        foreach (var record in records)
        {
            ImGui.PushID(record.Path);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextWrapped(record.Path);
            if (!File.Exists(record.Path))
            {
                ImGui.TextColored(FrontendTheme.Warning, "File not found");
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.SmallButton("Remove"))
            {
                configuration.RomFiles.Remove(record);
                configuration.Save();
                RefreshLibrary();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawRomFolderSettings()
    {
        DrawMutedWrapped("Folders are scanned recursively for every supported system. Symbolic links are skipped.");
        if (ImGui.Button("Add ROM folder", new Vector2(-1f, 0f)))
        {
            OpenRomFolderDialog();
        }

        if (configuration.RomFolders.Count == 0)
        {
            ImGui.Dummy(new Vector2(1f, 6f * ImGuiHelpers.GlobalScale));
            DrawMutedWrapped("No ROM folders have been added.");
            return;
        }

        ImGui.Dummy(new Vector2(1f, 8f * ImGuiHelpers.GlobalScale));
        var flags = ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerH |
                    ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("rom-folders", 2, flags)) return;
        ImGui.TableSetupColumn("Folder", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 84f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        for (var index = 0; index < configuration.RomFolders.Count; index++)
        {
            ImGui.PushID(index);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextWrapped(configuration.RomFolders[index]);
            if (!Directory.Exists(configuration.RomFolders[index]))
            {
                ImGui.TextColored(FrontendTheme.Warning, "Folder not found");
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.SmallButton("Remove"))
            {
                configuration.RomFolders.RemoveAt(index);
                configuration.Save();
                RefreshLibrary();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.EndTable();
    }

}
