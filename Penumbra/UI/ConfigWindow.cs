using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.Mods;
using Penumbra.UI.Classes;

namespace Penumbra.UI;

public sealed partial class ConfigWindow : Window, IDisposable
{
    private readonly Penumbra              _penumbra;
    public readonly  ModFileSystemSelector Selector;

    public ConfigWindow( Penumbra penumbra )
        : base( GetLabel() )
    {
        _penumbra = penumbra;
        Selector = new ModFileSystemSelector( _penumbra.ModFileSystem, new HashSet< Mod2 >() ); // TODO
        Dalamud.PluginInterface.UiBuilder.DisableGposeUiHide = true;
        Dalamud.PluginInterface.UiBuilder.DisableCutsceneUiHide = true;
        Dalamud.PluginInterface.UiBuilder.DisableUserUiHide = true;
        RespectCloseHotkey = true;
        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2( 800, 600 ),
            MaximumSize = new Vector2( 4096, 2160 ),
        };
    }

    public override void Draw()
    {
        using var bar = ImRaii.TabBar( string.Empty, ImGuiTabBarFlags.NoTooltip );
        SetupSizes();
        DrawSettingsTab();
        DrawModsTab();
        DrawCollectionsTab();
        DrawChangedItemTab();
        DrawEffectiveChangesTab();
        DrawDebugTab();
        DrawResourceManagerTab();
    }

    public void Dispose()
    {
        Selector.Dispose();
    }

    private static string GetLabel()
        => Penumbra.Version.Length == 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{Penumbra.Version}###PenumbraConfigWindow";

    private Vector2 _inputTextWidth;

    private void SetupSizes()
    {
        _inputTextWidth = new Vector2( 350f   * ImGuiHelpers.GlobalScale, 0 );
    }
}