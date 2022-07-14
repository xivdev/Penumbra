using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace Penumbra;

public class Dalamud
{
    public static void Initialize( DalamudPluginInterface pluginInterface )
        => pluginInterface.Create< Dalamud >();

        // @formatter:off
        [PluginService][RequiredVersion("1.0")] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static CommandManager         Commands        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static DataManager            GameData        { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ClientState            ClientState     { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ChatGui                Chat            { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static Framework              Framework       { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static Condition              Conditions      { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static TargetManager          Targets         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static ObjectTable            Objects         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static TitleScreenMenu        TitleScreenMenu { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static GameGui                GameGui         { get; private set; } = null!;
        [PluginService][RequiredVersion("1.0")] public static KeyState               KeyState        { get; private set; } = null!;
    // @formatter:on
}