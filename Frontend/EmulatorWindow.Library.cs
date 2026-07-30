using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using AllaganPocket.Emulation;
using System.Globalization;

namespace AllaganPocket.Frontend;

internal sealed partial class EmulatorWindow
{
    private void DrawLibrary()
    {
        var filtered = FilteredGames();
        DrawLibraryHeader(filtered.Count);
        var available = ImGui.GetContentRegionAvail();
        var scale = ImGuiHelpers.GlobalScale;
        var libraryOrigin = ImGui.GetCursorScreenPos();
        var viewport = new Vector2(MathF.Max(1f, available.X), MathF.Max(1f, available.Y));
        selectedLibraryItemRect = null;

        DrawLibraryItems(filtered, viewport);
        if (selectedGame is null) return;

        var detailsRect = ResolveContextDetailsRect(libraryOrigin, viewport, scale);
        DrawGameDetailsPanel(selectedGame, detailsRect, "game-details-context");
        DismissGameDetailsWhenClickedOutside(detailsRect);
    }

    private void DrawLibraryItems(IReadOnlyList<RomEntry> filtered, Vector2 size)
    {
        ImGui.BeginChild("library-scroll", size, false);
        ImGui.SetCursorPosX(24f * ImGuiHelpers.GlobalScale);
        if (filtered.Count == 0) DrawEmptyLibrary();
        else if (listView) DrawGameList(filtered);
        else DrawGameGrid(filtered);
        ImGui.EndChild();
    }

    private UiRect ResolveContextDetailsRect(Vector2 libraryOrigin, Vector2 viewport, float scale)
    {
        var bounds = new UiRect(libraryOrigin, libraryOrigin + viewport);
        var margin = 10f * scale;
        var usableSize = Vector2.Max(Vector2.One, bounds.Size - new Vector2(margin * 2f));
        var panelSize = MeasureDetailsPanel(selectedGame!, usableSize, scale);

        // Behave like a Windows context menu: use the exact click point as the anchor.
        // Flip left or upward only when the panel would leave the visible library area.
        var anchor = selectedGameClickPosition
            ?? selectedLibraryItemRect?.Min
            ?? new Vector2(bounds.Max.X - margin - panelSize.X, bounds.Min.Y + margin);
        anchor = Vector2.Clamp(anchor,
            bounds.Min + new Vector2(margin),
            bounds.Max - new Vector2(margin));

        var x = anchor.X;
        var y = anchor.Y;
        if (x + panelSize.X > bounds.Max.X - margin)
            x = anchor.X - panelSize.X;
        if (y + panelSize.Y > bounds.Max.Y - margin)
            y = anchor.Y - panelSize.Y;

        x = Math.Clamp(x, bounds.Min.X + margin, bounds.Max.X - margin - panelSize.X);
        y = Math.Clamp(y, bounds.Min.Y + margin, bounds.Max.Y - margin - panelSize.Y);
        return new UiRect(new Vector2(x, y), new Vector2(x + panelSize.X, y + panelSize.Y));
    }

    private Vector2 MeasureDetailsPanel(RomEntry game, Vector2 available, float scale)
    {
        var padding = 12f * scale;
        var closeSize = 24f * scale;
        var lastPlayedLabelWidth = 82f * scale;
        var actionGap = 8f * scale;
        var minimumWidth = 320f * scale;
        var maximumWidth = MathF.Min(620f * scale, available.X);
        var title = NormalizeDisplayText(game.Title);
        var lastPlayed = configuration.LastPlayed(game.Path) is { } played
            ? played.ToLocalTime().ToString("g")
            : "Never";

        var headingWidth = ImGui.CalcTextSize(game.System.Name).X + closeSize + 8f * scale;
        var titleWidth = ImGui.CalcTextSize(title).X;
        var lastPlayedWidth = lastPlayedLabelWidth + ImGui.CalcTextSize(lastPlayed).X;
        var actionsWidth = 260f * scale + actionGap;
        var desiredWidth = MathF.Max(minimumWidth,
            MathF.Max(headingWidth, MathF.Max(titleWidth, MathF.Max(lastPlayedWidth, actionsWidth)))
            + padding * 2f);
        var panelWidth = Math.Clamp(desiredWidth, MathF.Min(minimumWidth, maximumWidth), maximumWidth);
        var contentWidth = MathF.Max(80f * scale, panelWidth - padding * 2f);

        var headingHeight = MathF.Max(closeSize, ImGui.GetTextLineHeight());
        var titleHeight = MathF.Max(ImGui.GetTextLineHeight(), ImGui.CalcTextSize(title, false, contentWidth).Y);
        var active = session is not null && activeGame is not null &&
            string.Equals(activeGame.Path, game.Path, StringComparison.OrdinalIgnoreCase);
        var stackActions = contentWidth < 230f * scale;
        var actionHeight = stackActions ? 72f * scale : 34f * scale;
        var resumeHeight = active ? 39f * scale : 0f;
        var metadataHeight = ImGui.GetTextLineHeight();
        // Reserve a full metadata row plus bottom breathing room. The previous estimate
        // did not account for ImGui item spacing after the action buttons, which could
        // clip the Last played row at the bottom of the child window.
        var metadataBlockHeight = metadataHeight + 36f * scale;
        var panelHeight = padding + headingHeight + 4f * scale + titleHeight + 8f * scale +
            1f * scale + 8f * scale + resumeHeight + actionHeight + 9f * scale +
            metadataBlockHeight + padding;

        return new Vector2(panelWidth, MathF.Min(panelHeight, available.Y));
    }

    private void DrawGameDetailsPanel(RomEntry game, UiRect rect, string id, bool floating = true)
    {
        if (floating)
        {
            var shadow = ImGui.GetWindowDrawList();
            var shadowPadding = new Vector2(4f, 4f) * ImGuiHelpers.GlobalScale;
            shadow.AddRectFilled(rect.Min - shadowPadding, rect.Max + shadowPadding,
                ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.34f)));
        }

        ImGui.SetCursorScreenPos(rect.Min);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, FrontendTheme.DialogPanel);
        ImGui.BeginChild(id, rect.Size, true,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var closeRequested = DrawGameDetails(game);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        if (closeRequested) ClearSelectedGameDetails();
    }

    private void DismissGameDetailsWhenClickedOutside(UiRect detailsRect)
    {
        if (selectedGame is null) return;

        if (preserveGameDetailsForCurrentClick)
        {
            preserveGameDetailsForCurrentClick = false;
            return;
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left)) return;
        var mouse = ImGui.GetMousePos();
        var inside = mouse.X >= detailsRect.Min.X && mouse.X <= detailsRect.Max.X &&
            mouse.Y >= detailsRect.Min.Y && mouse.Y <= detailsRect.Max.Y;
        if (!inside) ClearSelectedGameDetails();
    }

    private void ClearSelectedGameDetails()
    {
        selectedGame = null;
        selectedGameClickPosition = null;
        preserveGameDetailsForCurrentClick = false;
    }

    private void DrawLibraryHeader(int gameCount)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var contentWidth = ImGui.GetContentRegionAvail().X;
        var compact = contentWidth < 650f * scale;
        var veryCompact = contentWidth < 480f * scale;
        ImGui.SetCursorPos(new Vector2(24f, 20f) * scale);
        var currentSystem = section == LibrarySection.System
            ? EmulatorSystemCatalog.ById(selectedSystemId)
            : null;
        var title = section switch
        {
            LibrarySection.Recent => "Recently played",
            LibrarySection.Favorites => "Favorites",
            LibrarySection.System => currentSystem?.Name ?? "System",
            _ => "Library",
        };

        if (currentSystem is not null)
        {
            var iconSize = new Vector2(26f, 26f) * scale;
            if (DrawSystemIcon(currentSystem, iconSize))
            {
                ImGui.SameLine(0f, 10f * scale);
            }
        }
        ImGui.TextUnformatted(title);
        ImGui.SameLine();
        ImGui.TextColored(FrontendTheme.Muted, $"{gameCount}");

        if (currentSystem is not null && !compact)
        {
            var buttonWidth = 190f * scale;
            ImGui.SameLine(ImGui.GetWindowWidth() - buttonWidth - 20f * scale);
            if (ImGui.Button("Settings", new Vector2(buttonWidth, 0f)))
            {
                page = FrontendPage.Settings;
            }
        }

        ImGui.SetCursorPosX(24f * scale);
        var innerWidth = MathF.Max(220f * scale, ImGui.GetContentRegionAvail().X - 24f * scale);
        var actionWidth = 322f * scale;
        ImGui.SetNextItemWidth(compact
            ? innerWidth
            : MathF.Max(170f * scale, ImGui.GetContentRegionAvail().X - actionWidth));
        ImGui.InputTextWithHint("##search", "Search games or systems...", ref search, 128);
        if (compact)
        {
            ImGui.SetCursorPosX(24f * scale);
            if (currentSystem is not null)
            {
                if (ImGui.Button("Settings", new Vector2(110f * scale, 0f)))
                {
                    page = FrontendPage.Settings;
                }
                if (!veryCompact) ImGui.SameLine();
            }
        }
        else
        {
            ImGui.SameLine();
        }

        if (veryCompact && currentSystem is not null)
            ImGui.SetCursorPosX(24f * scale);
        DrawLibraryViewToggle(scale);
        ImGui.SameLine();
        if (ImGui.Button("Add games", new Vector2(96f * scale, 0f)))
        {
            addGamesPopupRequested = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Refresh", new Vector2(84f * scale, 0f)))
        {
            RefreshLibrary();
        }

        ImGui.SetCursorPosX(24f * scale);
        ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Muted);
        ImGui.PushTextWrapPos(0f);
        ImGui.TextWrapped(status);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        ImGui.Dummy(new Vector2(1f, 4f * scale));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(1f, 6f * scale));
    }

    private IReadOnlyList<RomEntry> FilteredGames()
    {
        IEnumerable<RomEntry> query = games;
        query = section switch
        {
            LibrarySection.Recent => RecentGames(),
            LibrarySection.Favorites => query.Where(game => configuration.IsFavorite(game.Path)),
            LibrarySection.System => query.Where(game => string.Equals(game.System.Id, selectedSystemId,
                StringComparison.OrdinalIgnoreCase)),
            _ => query,
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(game => game.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                game.System.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
        return query.ToArray();
    }

    private IEnumerable<RomEntry> RecentGames()
    {
        foreach (var recent in configuration.RecentGames)
        {
            var game = games.FirstOrDefault(item => string.Equals(item.Path, recent.Path,
                StringComparison.OrdinalIgnoreCase));
            if (game is not null) yield return game;
        }
    }

    private void DrawEmptyLibrary()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail();
        var boxWidth = MathF.Min(520f * scale, MathF.Max(260f * scale, available.X - 60f * scale));
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var showFolderAction = section == LibrarySection.System && !hasSearch;
        var boxHeight = (showFolderAction ? 190f : 150f) * scale;
        var system = showFolderAction ? EmulatorSystemCatalog.ById(selectedSystemId) : null;
        var (title, description) = hasSearch
            ? ("No matching games", "Try a different game title or system name.")
            : section switch
        {
            LibrarySection.Recent => ("No recently played games",
                "Games you launch will appear here automatically."),
            LibrarySection.Favorites => ("You have no favorites",
                "Mark a game as a favorite and it will appear here."),
            LibrarySection.System => ($"No {system?.Name ?? "system"} games",
                $"Add a folder containing {system?.Name ?? "this system"} games to your library."),
            _ => ("No games in your library",
                "Games from your configured system folders will appear here."),
        };
        var origin = ImGui.GetCursorScreenPos() + new Vector2(
            MathF.Max(0f, (available.X - boxWidth) * 0.5f),
            MathF.Max(24f * scale, (available.Y - boxHeight) * 0.32f));
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(origin, origin + new Vector2(boxWidth, boxHeight),
            ImGui.GetColorU32(FrontendTheme.PanelRaised), 0f);
        draw.AddRect(origin, origin + new Vector2(boxWidth, boxHeight),
            ImGui.GetColorU32(FrontendTheme.Border), 0f);
        ImGui.SetCursorScreenPos(origin + new Vector2(24f, 22f) * scale);
        ImGui.BeginGroup();
        ImGui.TextUnformatted(title);
        ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Muted);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + boxWidth - 48f * scale);
        ImGui.TextWrapped(description);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        if (showFolderAction)
        {
            ImGui.Dummy(new Vector2(1f, 12f * scale));
            if (ImGui.Button("Add games", new Vector2(200f * scale, 38f * scale)))
            {
                addGamesPopupRequested = true;
            }
        }
        ImGui.EndGroup();
    }

    private void DrawLibraryViewToggle(float scale)
    {
        const float gap = 3f;
        var buttonWidth = 52f * scale;
        var active = FrontendTheme.WithAlpha(FrontendTheme.Accent, 0.28f);
        var gridActive = !listView;
        var listActive = listView;

        if (gridActive) ImGui.PushStyleColor(ImGuiCol.Button, active);
        if (ImGui.Button("Grid##library-view", new Vector2(buttonWidth, 0f)))
            SetLibraryListView(false);
        if (gridActive) ImGui.PopStyleColor();

        ImGui.SameLine(0f, gap * scale);
        if (listActive) ImGui.PushStyleColor(ImGuiCol.Button, active);
        if (ImGui.Button("List##library-view", new Vector2(buttonWidth, 0f)))
            SetLibraryListView(true);
        if (listActive) ImGui.PopStyleColor();
    }

    private void SetLibraryListView(bool enabled)
    {
        if (listView == enabled) return;
        listView = enabled;
        selectedGameClickPosition = null;
        configuration.StartInListView = enabled;
        configuration.Save();
    }

    private void DrawGameGrid(IReadOnlyList<RomEntry> filtered)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var cardScale = configuration.LibraryCardScale;
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X - 18f * scale);
        var preferredWidth = 228f * scale * cardScale;
        var minimumWidth = MathF.Min(188f * scale * cardScale, width);
        var gap = 16f * scale;
        var columns = Math.Max(1, (int)MathF.Floor((width + gap) / (preferredWidth + gap)));
        var candidateWidth = (width - gap * (columns - 1)) / columns;

        // Do not leave one oversized card when two readable cards fit. This keeps
        // filenames useful in narrow windows without returning to the cramped v9 grid.
        if (candidateWidth > preferredWidth * 1.42f && columns < filtered.Count)
        {
            var nextWidth = (width - gap * columns) / (columns + 1);
            if (nextWidth >= minimumWidth) columns++;
        }

        var cardWidth = MathF.Max(minimumWidth, (width - gap * (columns - 1)) / columns);
        var cardHeight = 158f * scale * cardScale;
        var gridStartX = ImGui.GetCursorPosX();
        for (var index = 0; index < filtered.Count; index++)
        {
            if (index % columns == 0)
            {
                // ImGui resets the next line to the child window's content origin.
                // Restore the library inset so every grid row begins at the same X position.
                ImGui.SetCursorPosX(gridStartX);
            }
            else
            {
                ImGui.SameLine(0f, gap);
            }

            DrawGameCard(filtered[index], cardWidth, cardHeight, cardScale);
        }
        ImGui.Dummy(new Vector2(1f, 18f * scale));
    }

    private void DrawGameCard(RomEntry game, float width, float height, float cardScale)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var unit = scale * cardScale;
        var position = ImGui.GetCursorScreenPos();
        var selected = selectedGame is not null && string.Equals(selectedGame.Path, game.Path,
            StringComparison.OrdinalIgnoreCase);
        ImGui.InvisibleButton($"game-card-{game.Path}", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            selectedGame = game;
            selectedGameClickPosition = ImGui.GetMousePos();
            preserveGameDetailsForCurrentClick = true;
            selected = true;
        }
        if (selected && ImGui.IsItemVisible())
            selectedLibraryItemRect = new UiRect(position, position + new Vector2(width, height));
        if (hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) StartGame(game);

        var draw = ImGui.GetWindowDrawList();
        var background = selected
            ? FrontendTheme.AccentSoft
            : hovered ? FrontendTheme.PanelHover : FrontendTheme.PanelRaised;
        draw.AddRectFilled(position, position + new Vector2(width, height), ImGui.GetColorU32(background), 0f);
        draw.AddRect(position, position + new Vector2(width, height),
            ImGui.GetColorU32(selected ? FrontendTheme.Accent : FrontendTheme.Border), 0f, ImDrawFlags.None,
            selected ? 2f : 1f);

        var color = FrontendTheme.SystemColor(game.System.Id);
        var headerHeight = 48f * unit;
        draw.AddRectFilled(position, position + new Vector2(width, headerHeight),
            ImGui.GetColorU32(FrontendTheme.WithAlpha(color, hovered ? 0.95f : 0.82f)), 0f);
        draw.AddRectFilled(position, new Vector2(position.X + 5f * scale, position.Y + height),
            ImGui.GetColorU32(color), 0f);

        var favorite = configuration.IsFavorite(game.Path);
        var iconSize = new Vector2(22f, 22f) * unit;
        var iconPosition = position + new Vector2(13f, 13f) * unit;
        var hasIcon = DrawSystemIconAt(game.System, iconPosition, iconSize);
        var headerTextX = position.X + (hasIcon ? 43f : 14f) * unit;
        var favoriteReserve = favorite ? 30f * scale : 0f;
        var headerTextWidth = MathF.Max(12f, position.X + width - 13f * unit - favoriteReserve - headerTextX);
        var systemLabel = FitTextToWidth(game.System.Name, headerTextWidth, out var systemTruncated);
        draw.AddText(new Vector2(headerTextX, position.Y + 15f * unit),
            ImGui.GetColorU32(Vector4.One), systemLabel);

        if (favorite)
        {
            var star = "★";
            var starWidth = ImGui.CalcTextSize(star).X;
            draw.AddText(new Vector2(position.X + width - starWidth - 12f * scale, position.Y + 15f * unit),
                ImGui.GetColorU32(FrontendTheme.AccentGold), star);
        }

        var bodyLeft = position.X + 15f * unit;
        var bodyWidth = MathF.Max(24f, width - 30f * unit);
        var titleLines = WrapTextToTwoLines(game.Title, bodyWidth);
        draw.AddText(new Vector2(bodyLeft, position.Y + 65f * unit),
            ImGui.GetColorU32(FrontendTheme.Text), titleLines.First);
        if (!string.IsNullOrEmpty(titleLines.Second))
        {
            draw.AddText(new Vector2(bodyLeft, position.Y + 86f * unit),
                ImGui.GetColorU32(FrontendTheme.Text), titleLines.Second);
        }

        var lastPlayed = configuration.LastPlayed(game.Path);
        if (lastPlayed is not null)
        {
            var playedText = FormatLastPlayed(lastPlayed.Value);
            draw.AddText(new Vector2(bodyLeft, position.Y + height - 25f * unit),
                ImGui.GetColorU32(FrontendTheme.Muted), playedText);
        }

        if (hovered && (systemTruncated || titleLines.Truncated))
            ImGui.SetTooltip($"{game.Title}\n{game.System.Name}");
    }

    private void DrawGameList(IReadOnlyList<RomEntry> filtered)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var originalStartX = ImGui.GetCursorPosX();
        var listStartX = MathF.Max(0f, originalStartX - 12f * scale);
        ImGui.SetCursorPosX(listStartX);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f * scale);

        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X - 12f * scale);
        var rowHeight = 54f * scale;
        var gap = 3f * scale;
        var wide = width >= 700f * scale;

        DrawGameListHeader(width, wide, scale, listStartX);
        foreach (var game in filtered)
        {
            // Keep rows and the header on the exact same horizontal grid. Without
            // this, a new ImGui line falls back to the child content origin.
            ImGui.SetCursorPosX(listStartX);
            var position = ImGui.GetCursorScreenPos();
            var selected = selectedGame is not null && string.Equals(selectedGame.Path, game.Path,
                StringComparison.OrdinalIgnoreCase);
            ImGui.PushID(game.Path);
            ImGui.InvisibleButton("game-list-row", new Vector2(width, rowHeight));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemClicked())
            {
                selectedGame = game;
                selectedGameClickPosition = ImGui.GetMousePos();
                preserveGameDetailsForCurrentClick = true;
                selected = true;
            }
            if (selected && ImGui.IsItemVisible())
                selectedLibraryItemRect = new UiRect(position, position + new Vector2(width, rowHeight));
            if (hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) StartGame(game);

            var draw = ImGui.GetWindowDrawList();
            var background = selected
                ? FrontendTheme.AccentSoft
                : hovered ? FrontendTheme.PanelHover : FrontendTheme.Panel;
            draw.AddRectFilled(position, position + new Vector2(width, rowHeight),
                ImGui.GetColorU32(background), 0f);
            draw.AddRect(position, position + new Vector2(width, rowHeight),
                ImGui.GetColorU32(selected ? FrontendTheme.Accent : FrontendTheme.Border), 0f,
                ImDrawFlags.None, selected ? 2f : 1f);

            var color = FrontendTheme.SystemColor(game.System.Id);
            draw.AddRectFilled(position, position + new Vector2(4f * scale, rowHeight),
                ImGui.GetColorU32(color), 0f);
            var iconSize = new Vector2(22f, 22f) * scale;
            var iconPosition = position + new Vector2(14f, 16f) * scale;
            _ = DrawSystemIconAt(game.System, iconPosition, iconSize);

            var textX = position.X + 50f * scale;
            var rightPadding = 14f * scale;
            var favorite = configuration.IsFavorite(game.Path);
            var favoriteReserve = favorite ? 28f * scale : 0f;
            var titleTruncated = false;
            var systemTruncated = false;

            if (wide)
            {
                var gameColumnWidth = width * 0.48f;
                var systemColumnX = position.X + width * 0.53f;
                var systemColumnWidth = width * 0.27f;
                var lastColumnX = position.X + width * 0.82f;
                var lastColumnWidth = MathF.Max(20f, position.X + width - rightPadding - favoriteReserve - lastColumnX);
                var title = FitTextToWidth(game.Title, MathF.Max(20f, gameColumnWidth - (textX - position.X)),
                    out titleTruncated);
                var system = FitTextToWidth(game.System.Name, systemColumnWidth, out systemTruncated);
                draw.AddText(new Vector2(textX, position.Y + 17f * scale),
                    ImGui.GetColorU32(FrontendTheme.Text), title);
                draw.AddText(new Vector2(systemColumnX, position.Y + 17f * scale),
                    ImGui.GetColorU32(FrontendTheme.Muted), system);

                var lastPlayed = configuration.LastPlayed(game.Path);
                var played = lastPlayed is null ? "Never" : FormatLastPlayed(lastPlayed.Value);
                played = FitTextToWidth(played, lastColumnWidth, out _);
                draw.AddText(new Vector2(lastColumnX, position.Y + 17f * scale),
                    ImGui.GetColorU32(FrontendTheme.Muted), played);
            }
            else
            {
                var contentWidth = MathF.Max(20f, position.X + width - rightPadding - favoriteReserve - textX);
                var title = FitTextToWidth(game.Title, contentWidth, out titleTruncated);
                var system = FitTextToWidth(game.System.Name, contentWidth, out systemTruncated);
                draw.AddText(new Vector2(textX, position.Y + 8f * scale),
                    ImGui.GetColorU32(FrontendTheme.Text), title);
                draw.AddText(new Vector2(textX, position.Y + 30f * scale),
                    ImGui.GetColorU32(FrontendTheme.Muted), system);
            }

            if (favorite)
            {
                var star = "★";
                var starWidth = ImGui.CalcTextSize(star).X;
                draw.AddText(new Vector2(position.X + width - starWidth - rightPadding, position.Y + 17f * scale),
                    ImGui.GetColorU32(FrontendTheme.AccentGold), star);
            }

            if (hovered && (titleTruncated || systemTruncated))
                ImGui.SetTooltip($"{game.Title}\n{game.System.Name}");
            ImGui.PopID();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + gap);
        }
        ImGui.Dummy(new Vector2(1f, 12f * scale));
    }

    private static void DrawGameListHeader(float width, bool wide, float scale, float listStartX)
    {
        if (!wide) return;
        ImGui.SetCursorPosX(listStartX);
        var position = ImGui.GetCursorScreenPos();
        var height = 30f * scale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(position, position + new Vector2(width, height),
            ImGui.GetColorU32(FrontendTheme.PanelRaised), 0f);
        draw.AddRect(position, position + new Vector2(width, height),
            ImGui.GetColorU32(FrontendTheme.Border), 0f);
        draw.AddText(position + new Vector2(50f, 7f) * scale,
            ImGui.GetColorU32(FrontendTheme.Muted), "Game");
        draw.AddText(new Vector2(position.X + width * 0.53f, position.Y + 7f * scale),
            ImGui.GetColorU32(FrontendTheme.Muted), "System");
        draw.AddText(new Vector2(position.X + width * 0.82f, position.Y + 7f * scale),
            ImGui.GetColorU32(FrontendTheme.Muted), "Last played");
        ImGui.Dummy(new Vector2(width, height + 5f * scale));
    }

    private readonly record struct WrappedCardTitle(string First, string Second, bool Truncated);

    private static WrappedCardTitle WrapTextToTwoLines(string value, float maximumWidth)
    {
        value = NormalizeDisplayText(value);
        if (string.IsNullOrEmpty(value)) return new WrappedCardTitle("Untitled game", string.Empty, false);
        if (ImGui.CalcTextSize(value).X <= maximumWidth)
            return new WrappedCardTitle(value, string.Empty, false);

        var split = FindFittingPrefix(value, maximumWidth);
        if (split <= 0)
        {
            var only = FitTextToWidth(value, maximumWidth, out var truncatedOnly);
            return new WrappedCardTitle(only, string.Empty, truncatedOnly);
        }

        var whitespace = value.LastIndexOfAny([' ', '\t', '-', '_'], Math.Min(split - 1, value.Length - 1));
        if (whitespace >= Math.Max(1, split / 2)) split = whitespace + 1;
        var first = value[..split].TrimEnd(' ', '\t', '-', '_');
        var remaining = value[split..].TrimStart(' ', '\t', '-', '_');
        var second = FitTextToWidth(remaining, maximumWidth, out var truncated);
        return new WrappedCardTitle(first, second, truncated);
    }

    private static string FitTextToWidth(string value, float maximumWidth, out bool truncated)
    {
        value = NormalizeDisplayText(value);
        if (string.IsNullOrEmpty(value) || maximumWidth <= 0f)
        {
            truncated = value.Length > 0;
            return string.Empty;
        }

        if (ImGui.CalcTextSize(value).X <= maximumWidth)
        {
            truncated = false;
            return value;
        }

        const string ellipsis = "…";
        var ellipsisWidth = ImGui.CalcTextSize(ellipsis).X;
        if (ellipsisWidth > maximumWidth)
        {
            truncated = true;
            return string.Empty;
        }

        var prefix = FindFittingPrefix(value, maximumWidth - ellipsisWidth);
        truncated = true;
        if (prefix <= 0) return ellipsis;
        return value[..prefix].TrimEnd() + ellipsis;
    }

    private static int FindFittingPrefix(string value, float maximumWidth)
    {
        if (maximumWidth <= 0f || string.IsNullOrEmpty(value)) return 0;
        var elements = StringInfo.ParseCombiningCharacters(value);
        var low = 0;
        var high = elements.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            var end = middle == elements.Length ? value.Length : elements[middle];
            if (ImGui.CalcTextSize(value[..end]).X <= maximumWidth) low = middle;
            else high = middle - 1;
        }
        return low == elements.Length ? value.Length : elements[low];
    }

    private static string NormalizeDisplayText(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private bool DrawGameDetails(RomEntry game)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padding = 12f * scale;
        var closeSize = 24f * scale;
        var panelWidth = ImGui.GetWindowWidth();
        var contentWidth = MathF.Max(1f, panelWidth - padding * 2f);
        var closeRequested = DrawDetailsCloseButton(panelWidth, padding, closeSize, scale);

        ImGui.SetCursorPos(new Vector2(padding, 11f * scale));
        var headingWidth = MathF.Max(40f, panelWidth - padding * 2f - closeSize - 7f * scale);
        var systemLabel = FitTextToWidth(game.System.Name, headingWidth, out var systemTruncated);
        ImGui.TextColored(FrontendTheme.SystemColor(game.System.Id), systemLabel);
        if (systemTruncated && ImGui.IsItemHovered()) ImGui.SetTooltip(game.System.Name);

        ImGui.SetCursorPosX(padding);
        ImGui.PushTextWrapPos(panelWidth - padding);
        ImGui.TextWrapped(NormalizeDisplayText(game.Title));
        ImGui.PopTextWrapPos();

        ImGui.Dummy(new Vector2(1f, 4f * scale));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(1f, 5f * scale));

        var active = activeGame is not null && session is not null &&
            string.Equals(activeGame.Path, game.Path, StringComparison.OrdinalIgnoreCase);
        if (active)
        {
            ImGui.SetCursorPosX(padding);
            ImGui.PushStyleColor(ImGuiCol.Button, FrontendTheme.AccentSoft);
            if (ImGui.Button("Resume", new Vector2(contentWidth, 34f * scale))) ResumeGame();
            ImGui.PopStyleColor();
            ImGui.Dummy(new Vector2(1f, 3f * scale));
        }

        ImGui.SetCursorPosX(padding);
        var actionGap = 8f * scale;
        var stackActions = contentWidth < 230f * scale;
        if (stackActions)
        {
            if (ImGui.Button(session is null ? "Play" : "Switch", new Vector2(contentWidth, 32f * scale)))
                StartGame(game);
            ImGui.SetCursorPosX(padding);
            DrawFavoriteButton(game, contentWidth, 32f * scale);
        }
        else
        {
            var actionWidth = MathF.Max(80f * scale, (contentWidth - actionGap) * 0.5f);
            if (ImGui.Button(session is null ? "Play" : "Switch", new Vector2(actionWidth, 34f * scale)))
                StartGame(game);
            ImGui.SameLine(0f, actionGap);
            DrawFavoriteButton(game, actionWidth, 34f * scale);
        }

        ImGui.Dummy(new Vector2(1f, 6f * scale));
        DrawCompactMetadataRow("Last played", configuration.LastPlayed(game.Path) is { } lastPlayed
            ? lastPlayed.ToLocalTime().ToString("g")
            : "Never", padding, panelWidth, scale);


        return closeRequested;
    }

    private bool DrawDetailsCloseButton(float panelWidth, float padding, float size, float scale)
    {
        var localPosition = new Vector2(panelWidth - size - padding, 8f * scale);
        ImGui.SetCursorPos(localPosition);
        ImGui.InvisibleButton("##close-game-details", new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();
        var screenPosition = ImGui.GetItemRectMin();
        var draw = ImGui.GetWindowDrawList();
        if (hovered)
            draw.AddRectFilled(screenPosition, screenPosition + new Vector2(size),
                ImGui.GetColorU32(FrontendTheme.PanelHover));

        const string glyph = "×";
        var glyphSize = ImGui.CalcTextSize(glyph);
        var glyphPosition = screenPosition + (new Vector2(size) - glyphSize) * 0.5f;
        draw.AddText(glyphPosition, ImGui.GetColorU32(FrontendTheme.Text), glyph);
        if (hovered) ImGui.SetTooltip("Close details");
        return clicked;
    }

    private void DrawCompactMetadataRow(string label, string value, float padding,
        float panelWidth, float scale, string? tooltip = null)
    {
        var labelWidth = 82f * scale;
        var valueX = padding + labelWidth;
        var valueWidth = MathF.Max(20f, panelWidth - padding - valueX);

        ImGui.SetCursorPosX(padding);
        ImGui.TextColored(FrontendTheme.Muted, label);
        ImGui.SameLine(valueX);
        var display = FitTextToWidth(value, valueWidth, out var truncated);
        ImGui.TextUnformatted(display);
        if ((truncated || tooltip is not null) && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip ?? value);
    }

    private void DrawFavoriteButton(RomEntry game, float width, float height)
    {
        var favorite = configuration.IsFavorite(game.Path);
        if (ImGui.Button(favorite ? "Unfavorite" : "Favorite", new Vector2(width, height)))
        {
            configuration.SetFavorite(game.Path, !favorite);
            configuration.Save();
        }
    }

    private void DrawAddGamesPopup()
    {
        if (addGamesPopupRequested)
        {
            ImGui.OpenPopup("Add games##AllaganPocket");
            addGamesPopupRequested = false;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var edgeMargin = new Vector2(24f, 24f) * scale;
        var availablePopupSize = Vector2.Max(new Vector2(1f), applicationWindowSize - edgeMargin * 2f);
        var popupSize = Vector2.Min(new Vector2(520f, 320f) * scale, availablePopupSize);
        var popupCenter = applicationWindowPos + applicationWindowSize * 0.5f;
        ImGui.SetNextWindowPos(popupCenter, ImGuiCond.Always, new Vector2(0.5f));
        ImGui.SetNextWindowSize(popupSize, ImGuiCond.Always);

        FrontendTheme.PushModalLayout(scale);
        var popupOpen = true;
        var visible = ImGui.BeginPopupModal(
            "Add games##AllaganPocket",
            ref popupOpen,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!visible)
        {
            FrontendTheme.PopModalLayout();
            return;
        }

        try
        {
            var system = section == LibrarySection.System
                ? EmulatorSystemCatalog.ById(selectedSystemId)
                : null;
            var footerHeight = ImGui.GetFrameHeight() + 16f * scale;

            ImGui.BeginChild("add-games-body", new Vector2(0f, -footerHeight), false);
            try
            {
                ImGui.TextUnformatted(system is null ? "Add games to your library" : $"Add {system.Name} games");
                ImGui.PushStyleColor(ImGuiCol.Text, FrontendTheme.Muted);
                ImGui.PushTextWrapPos(0f);
                ImGui.TextWrapped("Choose individual game files or add a folder that Allagan Retro Pocket will scan. Files stay in their original location.");
                ImGui.PopTextWrapPos();
                ImGui.PopStyleColor();
                ImGui.Dummy(new Vector2(1f, 8f * scale));

                if (ImGui.Button("Choose game files", new Vector2(-1f, 44f * scale)))
                {
                    ImGui.CloseCurrentPopup();
                    OpenGameFileDialog(system);
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Search and select one or more supported game files.");

                if (ImGui.Button("Add ROM folder", new Vector2(-1f, 44f * scale)))
                {
                    ImGui.CloseCurrentPopup();
                    OpenRomFolderDialog();
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Scan this folder and its subfolders for supported games.");
            }
            finally
            {
                ImGui.EndChild();
            }

            ImGui.Separator();
            var cancelWidth = 110f * scale;
            ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetWindowContentRegionMax().X - cancelWidth));
            if (ImGui.Button("Cancel", new Vector2(cancelWidth, 0f)))
                ImGui.CloseCurrentPopup();
        }
        finally
        {
            ImGui.EndPopup();
            FrontendTheme.PopModalLayout();
        }
    }

    private void OpenGameFileDialog(EmulatorSystemDefinition? forcedSystem)
    {
        var title = forcedSystem is null ? "Choose game files" : $"Choose {forcedSystem.Name} games";
        var filters = BuildRomFileFilters(forcedSystem);
        var startPath = LastLibraryLocation();
        fileDialogOpen = true;
        fileDialogs.OpenFileDialog(title, filters, (accepted, paths) =>
        {
            fileDialogOpen = false;
            if (!accepted || paths.Count == 0) return;
            AddGameFiles(paths, forcedSystem);
        }, 0, startPath, true);
    }

    private void OpenRomFolderDialog()
    {
        fileDialogOpen = true;
        fileDialogs.OpenFolderDialog("Choose ROM folder", (accepted, path) =>
        {
            fileDialogOpen = false;
            if (!accepted || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                status = $"Could not add ROM folder: {exception.Message}";
                return;
            }
            if (!configuration.RomFolders.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                configuration.RomFolders.Add(fullPath);
            configuration.Save();
            RefreshLibrary();
            status = $"Added ROM folder: {fullPath}";
        }, LastLibraryLocation(), true);
    }

    private void AddGameFiles(IEnumerable<string> paths, EmulatorSystemDefinition? forcedSystem)
    {
        var added = 0;
        var skipped = 0;
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                var fullPath = Path.GetFullPath(path);
                var resolved = forcedSystem;
                if (resolved is not null && !resolved.Supports(fullPath))
                {
                    skipped++;
                    continue;
                }

                resolved ??= EmulatorSystemCatalog.ResolveWithFolderHint(fullPath);
                if (resolved is null)
                {
                    // Disc images such as .iso and .cue can belong to multiple systems.
                    // Asking the user to add them from a specific system page is safer
                    // than silently assigning the wrong emulator.
                    skipped++;
                    continue;
                }

                var existing = configuration.RomFiles.FirstOrDefault(record =>
                    string.Equals(record.Path, fullPath, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    configuration.RomFiles.Add(new RomFileRecord
                    {
                        Path = fullPath,
                        SystemId = resolved.Id,
                    });
                    added++;
                }
                else if (!string.Equals(existing.SystemId, resolved.Id, StringComparison.OrdinalIgnoreCase))
                {
                    existing.SystemId = resolved.Id;
                    added++;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                skipped++;
                EmulatorLog.Warning($"[Allagan Retro Pocket] Could not add '{path}': {exception.Message}");
            }
        }

        if (added > 0)
        {
            configuration.Save();
            RefreshLibrary();
        }

        status = (added, skipped) switch
        {
            (0, 0) => "Those game files are already in the library.",
            (_, 0) => $"Added {added} game file(s).",
            _ => $"Added {added} game file(s); skipped {skipped}. Add ambiguous disc images from the correct system page.",
        };
    }

    private string LastLibraryLocation()
    {
        var lastFile = configuration.RomFiles.LastOrDefault()?.Path;
        if (!string.IsNullOrWhiteSpace(lastFile))
        {
            var directory = Path.GetDirectoryName(lastFile);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }

        var lastFolder = configuration.RomFolders.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(lastFolder) && Directory.Exists(lastFolder))
            return lastFolder;

        const string preferredDrive = @"C:\";
        return Directory.Exists(preferredDrive) ? preferredDrive : Environment.CurrentDirectory;
    }

    private static string BuildRomFileFilters(EmulatorSystemDefinition? system)
    {
        var extensions = (system is null ? EmulatorSystemCatalog.All.SelectMany(static item => item.Extensions) : system.Extensions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return $"Supported games{{{string.Join(",", extensions)}}}";
    }

    private static string FormatLastPlayed(DateTime utc)
    {
        var elapsed = DateTime.UtcNow - utc;
        if (elapsed.TotalMinutes < 2) return "Just now";
        if (elapsed.TotalHours < 1) return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours} h ago";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays} d ago";
        return utc.ToLocalTime().ToString("d");
    }

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..Math.Max(1, maximum - 1)] + "…";
}
