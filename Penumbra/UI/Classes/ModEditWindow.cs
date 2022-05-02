using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

namespace Penumbra.UI.Classes;

public class ModEditWindow : Window, IDisposable
{
    private const string      WindowBaseLabel = "###SubModEdit";
    private       Mod.Editor? _editor;
    private       Mod?        _mod;

    public void ChangeMod( Mod mod )
    {
        if( mod == _mod )
        {
            return;
        }

        _editor?.Dispose();
        _editor    = new Mod.Editor( mod );
        _mod       = mod;
        WindowName = $"{mod.Name}{WindowBaseLabel}";
    }

    public void ChangeOption( int groupIdx, int optionIdx )
        => _editor?.SetSubMod( groupIdx, optionIdx );

    public override bool DrawConditions()
        => _editor != null;

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar( "##tabs" );
        if( !tabBar )
        {
            return;
        }

        DrawFileTab();
        DrawMetaTab();
        DrawSwapTab();
        DrawMissingFilesTab();
        DrawUnusedFilesTab();
        DrawDuplicatesTab();
    }

    private void DrawMissingFilesTab()
    {
        using var tab = ImRaii.TabItem( "Missing Files" );
        if( !tab )
        {
            return;
        }

        if( _editor!.MissingPaths.Count == 0 )
        {
            ImGui.TextUnformatted( "No missing files detected." );
        }
        else
        {
            if( ImGui.Button( "Remove Missing Files from Mod" ) )
            {
                _editor.RemoveMissingPaths();
            }

            using var table = ImRaii.Table( "##missingFiles", 1, ImGuiTableFlags.RowBg, -Vector2.One );
            if( !table )
            {
                return;
            }

            foreach( var path in _editor.MissingPaths )
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( path.FullName );
            }
        }
    }

    private void DrawDuplicatesTab()
    {
        using var tab = ImRaii.TabItem( "Duplicates" );
        if( !tab )
        {
            return;
        }

        var buttonText = _editor!.DuplicatesFinished ? "Scan for Duplicates###ScanButton" : "Scanning for Duplicates...###ScanButton";
        if( ImGuiUtil.DrawDisabledButton( buttonText, Vector2.Zero, "Search for identical files in this mod. This may take a while.",
               !_editor.DuplicatesFinished ) )
        {
            _editor.StartDuplicateCheck();
        }

        if( !_editor.DuplicatesFinished )
        {
            ImGui.SameLine();
            if( ImGui.Button( "Cancel" ) )
            {
                _editor.Cancel();
            }

            return;
        }

        if( _editor.Duplicates.Count == 0 )
        {
            ImGui.TextUnformatted( "No duplicates found." );
        }

        if( ImGui.Button( "Delete and Redirect Duplicates" ) )
        {
            _editor.DeleteDuplicates();
        }

        if( _editor.SavedSpace > 0 )
        {
            ImGui.SameLine();
            ImGui.TextUnformatted( $"Frees up {Functions.HumanReadableSize( _editor.SavedSpace )} from your hard drive." );
        }

        using var child = ImRaii.Child( "##duptable", -Vector2.One, true );
        if( !child )
        {
            return;
        }

        using var table = ImRaii.Table( "##duplicates", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.One );
        if( !table )
        {
            return;
        }

        var width = ImGui.CalcTextSize( "NNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNNN " ).X;
        ImGui.TableSetupColumn( "file", ImGuiTableColumnFlags.WidthStretch );
        ImGui.TableSetupColumn( "size", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize( "NNN.NNN  " ).X );
        ImGui.TableSetupColumn( "hash", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetWindowWidth() > 2 * width ? width : ImGui.CalcTextSize( "NNNNNNNN... " ).X );
        foreach( var (set, size, hash) in _editor.Duplicates.Where( s => s.Paths.Length > 1 ) )
        {
            ImGui.TableNextColumn();
            using var tree = ImRaii.TreeNode( set[ 0 ].FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ],
                ImGuiTreeNodeFlags.NoTreePushOnOpen );
            ImGui.TableNextColumn();
            ImGuiUtil.RightAlign( Functions.HumanReadableSize( size ) );
            ImGui.TableNextColumn();
            using( var font = ImRaii.PushFont( UiBuilder.MonoFont ) )
            {
                if( ImGui.GetWindowWidth() > 2 * width )
                {
                    ImGuiUtil.RightAlign( string.Concat( hash.Select( b => b.ToString( "X2" ) ) ) );
                }
                else
                {
                    ImGuiUtil.RightAlign( string.Concat( hash.Take( 4 ).Select( b => b.ToString( "X2" ) ) ) + "..." );
                }
            }

            if( !tree )
            {
                continue;
            }

            using var indent = ImRaii.PushIndent();
            foreach( var duplicate in set.Skip( 1 ) )
            {
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, 0x40000080 );
                using var node = ImRaii.TreeNode( duplicate.FullName[ ( _mod!.ModPath.FullName.Length + 1 ).. ], ImGuiTreeNodeFlags.Leaf );
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, 0x40000080 );
                ImGui.TableNextColumn();
                ImGui.TableSetBgColor( ImGuiTableBgTarget.CellBg, 0x40000080 );
            }
        }
    }

    private void DrawUnusedFilesTab()
    {
        using var tab = ImRaii.TabItem( "Unused Files" );
        if( !tab )
        {
            return;
        }

        if( _editor!.UnusedFiles.Count == 0 )
        {
            ImGui.TextUnformatted( "No unused files detected." );
        }
        else
        {
            if( ImGui.Button( "Add Unused Files to Default" ) )
            {
                _editor.AddUnusedPathsToDefault();
            }

            if( ImGui.Button( "Delete Unused Files from Filesystem" ) )
            {
                _editor.DeleteUnusedPaths();
            }

            using var table = ImRaii.Table( "##unusedFiles", 1, ImGuiTableFlags.RowBg, -Vector2.One );
            if( !table )
            {
                return;
            }

            foreach( var path in _editor.UnusedFiles )
            {
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( path.FullName );
            }
        }
    }


    private void DrawFileTab()
    {
        using var tab = ImRaii.TabItem( "File Redirections" );
        if( !tab )
        {
            return;
        }

        using var list = ImRaii.Table( "##files", 2 );
        if( !list )
        {
            return;
        }

        foreach( var (gamePath, file) in _editor!.CurrentFiles )
        {
            ImGui.TableNextColumn();
            ConfigWindow.Text( gamePath.Path );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( file.FullName );
        }
    }

    private void DrawMetaTab()
    {
        using var tab = ImRaii.TabItem( "Meta Manipulations" );
        if( !tab )
        {
            return;
        }

        using var list = ImRaii.Table( "##meta", 3 );
        if( !list )
        {
            return;
        }

        foreach( var manip in _editor!.CurrentManipulations )
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( manip.ManipulationType.ToString() );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( manip.ManipulationType switch
            {
                MetaManipulation.Type.Imc  => manip.Imc.ToString(),
                MetaManipulation.Type.Eqdp => manip.Eqdp.ToString(),
                MetaManipulation.Type.Eqp  => manip.Eqp.ToString(),
                MetaManipulation.Type.Est  => manip.Est.ToString(),
                MetaManipulation.Type.Gmp  => manip.Gmp.ToString(),
                MetaManipulation.Type.Rsp  => manip.Rsp.ToString(),
                _                          => string.Empty,
            } );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( manip.ManipulationType switch
            {
                MetaManipulation.Type.Imc  => manip.Imc.Entry.ToString(),
                MetaManipulation.Type.Eqdp => manip.Eqdp.Entry.ToString(),
                MetaManipulation.Type.Eqp  => manip.Eqp.Entry.ToString(),
                MetaManipulation.Type.Est  => manip.Est.Entry.ToString(),
                MetaManipulation.Type.Gmp  => manip.Gmp.Entry.ToString(),
                MetaManipulation.Type.Rsp  => manip.Rsp.Entry.ToString(),
                _                          => string.Empty,
            } );
        }
    }

    private void DrawSwapTab()
    {
        using var tab = ImRaii.TabItem( "File Swaps" );
        if( !tab )
        {
            return;
        }

        using var list = ImRaii.Table( "##swaps", 3 );
        if( !list )
        {
            return;
        }

        foreach( var (gamePath, file) in _editor!.CurrentSwaps )
        {
            ImGui.TableNextColumn();
            ConfigWindow.Text( gamePath.Path );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( file.FullName );
        }
    }

    public ModEditWindow()
        : base( WindowBaseLabel )
    { }

    public void Dispose()
    {
        _editor?.Dispose();
    }
}