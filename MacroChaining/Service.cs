using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace MacroChaining
{
    public class Service
    {
        [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IChatGui Chat { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider HookProvider { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    }
}