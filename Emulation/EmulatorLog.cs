using Dalamud.Plugin.Services;

namespace AllaganPocket.Emulation;

internal static class EmulatorLog
{
    private static IPluginLog? log;

    public static void Initialize(IPluginLog pluginLog) => log = pluginLog;
    public static void Debug(string message) => log?.Debug(message);
    public static void Info(string message) => log?.Information(message);
    public static void Warning(string message) => log?.Warning(message);
    public static void Error(string message) => log?.Error(message);
}
