namespace AllaganPocket.Emulation.Libretro;

internal sealed record LibretroCoreOptionChoice(string Value, string Label);

internal sealed record LibretroCoreOptionDefinition(
    string Key,
    string Description,
    string Info,
    string CategoryKey,
    string CategoryDescription,
    IReadOnlyList<LibretroCoreOptionChoice> Choices,
    string DefaultValue,
    bool Visible = true);
