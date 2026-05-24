using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace BazookaLens;

internal static class PluginServices
{
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    public static ICommandManager CommandManager { get; private set; } = null!;

    public static IChatGui ChatGui { get; private set; } = null!;

    public static IPluginLog Log { get; private set; } = null!;

    public static IFramework Framework { get; private set; } = null!;

    public static ITextureProvider TextureProvider { get; private set; } = null!;

    public static ITextureReadbackProvider TextureReadbackProvider { get; private set; } = null!;

    public static IGameConfig GameConfig { get; private set; } = null!;

    public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    public static IKeyState KeyState { get; private set; } = null!;

    public static void Initialize(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IPluginLog log,
        IFramework framework,
        ITextureProvider textureProvider,
        ITextureReadbackProvider textureReadbackProvider,
        IGameConfig gameConfig,
        IGameInteropProvider gameInteropProvider,
        IKeyState keyState)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        ChatGui = chatGui;
        Log = log;
        Framework = framework;
        TextureProvider = textureProvider;
        TextureReadbackProvider = textureReadbackProvider;
        GameConfig = gameConfig;
        GameInteropProvider = gameInteropProvider;
        KeyState = keyState;
    }
}
