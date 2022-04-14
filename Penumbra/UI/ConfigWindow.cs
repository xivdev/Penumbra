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
            MinimumSize = new Vector2( 1024, 768 ),
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
        DrawEffectiveChangesTab();
        DrawDebugTab();
        DrawResourceManagerTab();
    }

    public void Dispose()
    {
        Selector.Dispose();
    }

    private static string GetLabel()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        return version.Length == 0
            ? "Penumbra###PenumbraConfigWindow"
            : $"Penumbra v{version}###PenumbraConfigWindow";
    }

    private Vector2 _verticalSpace;
    private Vector2 _inputTextWidth;

    private void SetupSizes()
    {
        _verticalSpace  = new Vector2( 0, 20f * ImGuiHelpers.GlobalScale );
        _inputTextWidth = new Vector2( 450f   * ImGuiHelpers.GlobalScale, 0 );
    }
}