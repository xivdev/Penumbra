using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Components;
using Dalamud.Logging;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;
using Penumbra.UI.Classes;
using Penumbra.UI.Custom;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private          string            _settingsNewModDirectory = string.Empty;
    private readonly FileDialogManager _dialogManager           = new();
    private          bool              _dialogOpen;

    private static bool DrawPressEnterWarning( string old, float width )
    {
        using var color = ImRaii.PushColor( ImGuiCol.Button, Colors.PressEnterWarningBg );
        var       w     = new Vector2( width, 0 );
        return ImGui.Button( $"Press Enter or Click Here to Save (Current Directory: {old})", w );
    }

    private void DrawDirectoryPickerButton()
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Folder.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Select a directory via dialog.", false, true ) )
        {
            if( _dialogOpen )
            {
                _dialogManager.Reset();
                _dialogOpen = false;
            }
            else
            {
                //_dialogManager.OpenFolderDialog( "Choose Mod Directory", ( b, s ) =>
                //{
                //    _newModDirectory = b ? s : _newModDirectory;
                //    _dialogOpen      = false;
                //}, _newModDirectory, false);
                _dialogOpen = true;
            }
        }

        _dialogManager.Draw();
    }

    private static void DrawOpenDirectoryButton( int id, DirectoryInfo directory, bool condition )
    {
        using var _   = ImRaii.PushId( id );
        var       ret = ImGui.Button( "Open Directory" );
        ImGuiUtil.HoverTooltip( "Open this directory in your configured file explorer." );
        if( ret && condition && Directory.Exists( directory.FullName ) )
        {
            Process.Start( new ProcessStartInfo( directory.FullName )
            {
                UseShellExecute = true,
            } );
        }
    }

    private void DrawRootFolder()
    {
        // Initialize first time.
        if( _settingsNewModDirectory.Length == 0 )
        {
            _settingsNewModDirectory = Penumbra.Config.ModDirectory;
        }

        var       spacing = 3 * ImGuiHelpers.GlobalScale;
        using var group   = ImRaii.Group();
        ImGui.SetNextItemWidth( _inputTextWidth.X - spacing - ImGui.GetFrameHeight() );
        var       save  = ImGui.InputText( "##rootDirectory", ref _settingsNewModDirectory, 255, ImGuiInputTextFlags.EnterReturnsTrue );
        using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, new Vector2( spacing, 0 ) );
        ImGui.SameLine();
        DrawDirectoryPickerButton();
        style.Pop();
        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Root Directory", "This is where Penumbra will store your extracted mod files.\n"
          + "TTMP files are not copied, just extracted.\n"
          + "This directory needs to be accessible and you need write access here.\n"
          + "It is recommended that this directory is placed on a fast hard drive, preferably an SSD.\n"
          + "It should also be placed near the root of a logical drive - the shorter the total path to this folder, the better.\n"
          + "Definitely do not place it in your Dalamud directory or any sub-directory thereof." );
        group.Dispose();
        ImGui.SameLine();
        var pos = ImGui.GetCursorPosX();
        ImGui.NewLine();

        if( Penumbra.Config.ModDirectory == _settingsNewModDirectory || _settingsNewModDirectory.Length == 0 )
        {
            return;
        }

        if( save || DrawPressEnterWarning( Penumbra.Config.ModDirectory, pos ) )
        {
            Penumbra.ModManager.DiscoverMods( _settingsNewModDirectory );
        }
    }


    private static void DrawRediscoverButton()
    {
        DrawOpenDirectoryButton( 0, Penumbra.ModManager.BasePath, Penumbra.ModManager.Valid );
        ImGui.SameLine();
        if( ImGui.Button( "Rediscover Mods" ) )
        {
            Penumbra.ModManager.DiscoverMods();
        }

        ImGuiUtil.HoverTooltip( "Force Penumbra to completely re-scan your root directory as if it was restarted." );
    }

    private void DrawEnabledBox()
    {
        var enabled = Penumbra.Config.EnableMods;
        if( ImGui.Checkbox( "Enable Mods", ref enabled ) )
        {
            _penumbra.SetEnabled( enabled );
        }
    }

    private static void DrawShowAdvancedBox()
    {
        var showAdvanced = Penumbra.Config.ShowAdvanced;
        if( ImGui.Checkbox( "##showAdvanced", ref showAdvanced ) )
        {
            Penumbra.Config.ShowAdvanced = showAdvanced;
            Penumbra.Config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker( "Show Advanced Settings", "Enable some advanced options in this window and in the mod selector.\n"
          + "This is required to enable manually editing any mod information." );
    }

    private static void DrawColorSettings()
    {
        if( !ImGui.CollapsingHeader( "Colors" ) )
        {
            return;
        }

        foreach( var color in Enum.GetValues< ColorId >() )
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = Penumbra.Config.Colors.TryGetValue( color, out var current ) ? current : defaultColor;
            if( Widget.ColorPicker( name, description, currentColor, c => Penumbra.Config.Colors[ color ] = c, defaultColor ) )
            {
                Penumbra.Config.Save();
            }
        }

        ImGui.NewLine();
    }


    public void DrawSettingsTab()
    {
        using var tab = ImRaii.TabItem( "Settings" );
        if( !tab )
        {
            return;
        }

        using var child = ImRaii.Child( "##SettingsTab", -Vector2.One, false );
        if( !child )
        {
            return;
        }

        DrawEnabledBox();
        DrawShowAdvancedBox();
        ImGui.NewLine();
        DrawRootFolder();
        DrawRediscoverButton();
        ImGui.NewLine();

        DrawModSelectorSettings();
        DrawColorSettings();
        DrawAdvancedSettings();
    }
}