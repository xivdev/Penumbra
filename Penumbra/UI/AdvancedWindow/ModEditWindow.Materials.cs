using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.AdvancedWindow;

public partial class ModEditWindow
{
    private readonly FileEditor< MtrlTab > _materialTab;

    private bool DrawMaterialPanel( MtrlTab tab, bool disabled )
    {
        var ret = DrawMaterialTextureChange( tab, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawBackFaceAndTransparency( tab.Mtrl, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawMaterialColorSetChange( tab.Mtrl, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawMaterialShaderResources( tab, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        DrawOtherMaterialDetails( tab.Mtrl, disabled );

        return !disabled && ret;
    }

    private static bool DrawMaterialTextureChange( MtrlTab tab, bool disabled )
    {
        var       ret   = false;
        using var table = ImRaii.Table( "##Textures", 2 );
        ImGui.TableSetupColumn( "Path", ImGuiTableColumnFlags.WidthStretch );
        ImGui.TableSetupColumn( "Name", ImGuiTableColumnFlags.WidthFixed, tab.TextureLabelWidth * UiHelpers.Scale );
        for( var i = 0; i < tab.Mtrl.Textures.Length; ++i )
        {
            using var _   = ImRaii.PushId( i );
            var       tmp = tab.Mtrl.Textures[ i ].Path;
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
            if( ImGui.InputText( string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None )
            && tmp.Length > 0
            && tmp        != tab.Mtrl.Textures[ i ].Path )
            {
                ret                     = true;
                tab.Mtrl.Textures[ i ].Path = tmp;
            }

            ImGui.TableNextColumn();
            using var font = ImRaii.PushFont( UiBuilder.MonoFont );
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted( tab.TextureLabels[ i ] );
        }

        return ret;
    }

    private static bool DrawBackFaceAndTransparency( MtrlFile file, bool disabled )
    {
        const uint transparencyBit = 0x10;
        const uint backfaceBit     = 0x01;

        var ret = false;

        using var dis = ImRaii.Disabled( disabled );

        var tmp = ( file.ShaderPackage.Flags & transparencyBit ) != 0;
        if( ImGui.Checkbox( "Enable Transparency", ref tmp ) )
        {
            file.ShaderPackage.Flags = tmp ? file.ShaderPackage.Flags | transparencyBit : file.ShaderPackage.Flags & ~transparencyBit;
            ret                      = true;
        }

        ImGui.SameLine( 200 * UiHelpers.Scale + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X );
        tmp = ( file.ShaderPackage.Flags & backfaceBit ) != 0;
        if( ImGui.Checkbox( "Hide Backfaces", ref tmp ) )
        {
            file.ShaderPackage.Flags = tmp ? file.ShaderPackage.Flags | backfaceBit : file.ShaderPackage.Flags & ~backfaceBit;
            ret                      = true;
        }

        return ret;
    }

    private static void DrawOtherMaterialDetails( MtrlFile file, bool _ )
    {
        if( !ImGui.CollapsingHeader( "Further Content" ) )
        {
            return;
        }

        using( var sets = ImRaii.TreeNode( "UV Sets", ImGuiTreeNodeFlags.DefaultOpen ) )
        {
            if( sets )
            {
                foreach( var set in file.UvSets )
                {
                    ImRaii.TreeNode( $"#{set.Index:D2} - {set.Name}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        if( file.AdditionalData.Length <= 0 )
        {
            return;
        }

        using var t = ImRaii.TreeNode( $"Additional Data (Size: {file.AdditionalData.Length})###AdditionalData" );
        if( t )
        {
            ImGuiUtil.TextWrapped( string.Join( ' ', file.AdditionalData.Select( c => $"{c:X2}" ) ) );
        }
    }

    private void DrawMaterialReassignmentTab()
    {
        if( _editor.Files.Mdl.Count == 0 )
        {
            return;
        }

        using var tab = ImRaii.TabItem( "Material Reassignment" );
        if( !tab )
        {
            return;
        }

        ImGui.NewLine();
        MaterialSuffix.Draw( _editor, ImGuiHelpers.ScaledVector2( 175, 0 ) );

        ImGui.NewLine();
        using var child = ImRaii.Child( "##mdlFiles", -Vector2.One, true );
        if( !child )
        {
            return;
        }

        using var table = ImRaii.Table( "##files", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One );
        if( !table )
        {
            return;
        }

        var iconSize = ImGui.GetFrameHeight() * Vector2.One;
        foreach( var (info, idx) in _editor.MdlMaterialEditor.ModelFiles.WithIndex() )
        {
            using var id = ImRaii.PushId( idx );
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Save.ToIconString(), iconSize,
                   "Save the changed mdl file.\nUse at own risk!", !info.Changed, true ) )
            {
                info.Save();
            }

            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Recycle.ToIconString(), iconSize,
                   "Restore current changes to default.", !info.Changed, true ) )
            {
                info.Restore();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted( info.Path.FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ] );
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth( 400 * UiHelpers.Scale );
            var tmp = info.CurrentMaterials[ 0 ];
            if( ImGui.InputText( "##0", ref tmp, 64 ) )
            {
                info.SetMaterial( tmp, 0 );
            }

            for( var i = 1; i < info.Count; ++i )
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth( 400 * UiHelpers.Scale );
                tmp = info.CurrentMaterials[ i ];
                if( ImGui.InputText( $"##{i}", ref tmp, 64 ) )
                {
                    info.SetMaterial( tmp, i );
                }
            }
        }
    }
}