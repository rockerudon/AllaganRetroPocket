using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using AllaganPocket.Emulation;
using AllaganPocket.Frontend;

namespace AllaganPocket;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static IGamepadState GamepadState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal const string SupportUrl = "https://buymeacoffee.com/rockmizx";
    private const string MainCommand = "/retro";

    private readonly WindowSystem windows = new("AllaganPocket");
    private readonly EmulatorWindow mainWindow;
    internal static Configuration Config { get; private set; } = null!;

    public Plugin()
    {
        EmulatorLog.Initialize(Log);
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Normalize();
        Config.Save();

        mainWindow = new EmulatorWindow(
            PluginInterface.ConfigDirectory,
            PluginInterface.AssemblyLocation,
            TextureProvider,
            KeyState,
            GamepadState,
            Config);

        windows.AddWindow(mainWindow);
        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Allagan Retro Pocket.",
        });

        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMain;
        PluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
    }

    private void OnCommand(string command, string arguments)
    {
        ToggleMain();
    }

    private void ToggleMain()
    {
        mainWindow.IsOpen = !mainWindow.IsOpen;
    }

    private void OpenSettings()
    {
        mainWindow.OpenSettings();
        mainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMain;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
        CommandManager.RemoveHandler(MainCommand);
        windows.RemoveAllWindows();
        mainWindow.Dispose();
    }
}
