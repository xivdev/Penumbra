using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI.Classes;

public class SubModEditWindow : Window
{
    private const    string               WindowBaseLabel = "###SubModEdit";
    private          Mod?                 _mod;
    private          int                  _groupIdx  = -1;
    private          int                  _optionIdx = -1;
    private          IModGroup?           _group;
    private          ISubMod?             _subMod;
    private readonly List< FilePathInfo > _availableFiles = new();

    private readonly struct FilePathInfo
    {
        public readonly FullPath                         File;
        public readonly Utf8RelPath                      RelFile;
        public readonly long                             Size;
        public readonly List< (int, int, Utf8GamePath) > SubMods;

        public FilePathInfo( FileInfo file, Mod mod )
        {
            File    = new FullPath( file );
            RelFile = Utf8RelPath.FromFile( File, mod.BasePath, out var f ) ? f : Utf8RelPath.Empty;
            Size    = file.Length;
            SubMods = new List< (int, int, Utf8GamePath) >();
            var path = File;
            foreach( var (group, groupIdx) in mod.Groups.WithIndex() )
            {
                foreach( var (subMod, optionIdx) in group.WithIndex() )
                {
                    SubMods.AddRange( subMod.Files.Where( kvp => kvp.Value.Equals( path ) ).Select( kvp => ( groupIdx, optionIdx, kvp.Key ) ) );
                }
            }
            SubMods.AddRange( mod.Default.Files.Where( kvp => kvp.Value.Equals( path ) ).Select( kvp => (-1, 0, kvp.Key) )  );
        }
    }

    private readonly HashSet< MetaManipulation >          _manipulations = new();
    private readonly Dictionary< Utf8GamePath, FullPath > _files         = new();
    private readonly Dictionary< Utf8GamePath, FullPath > _fileSwaps     = new();

    public void Activate( Mod mod, int groupIdx, int optionIdx )
    {
        IsOpen     = true;
        _mod       = mod;
        _groupIdx  = groupIdx;
        _group     = groupIdx >= 0 ? mod.Groups[ groupIdx ] : null;
        _optionIdx = optionIdx;
        _subMod    = groupIdx >= 0 ? _group![ optionIdx ] : _mod.Default;
        _availableFiles.Clear();
        _availableFiles.AddRange( mod.BasePath.EnumerateDirectories()
           .SelectMany( d => d.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
           .Select( f => new FilePathInfo( f, _mod ) ) );

        _manipulations.Clear();
        _manipulations.UnionWith( _subMod.Manipulations );
        _files.SetTo( _subMod.Files );
        _fileSwaps.SetTo( _subMod.FileSwaps );

        WindowName = $"{_mod.Name}: {(_group != null ? $"{_group.Name} - " : string.Empty)}{_subMod.Name}";
    }

    public override bool DrawConditions()
        => _subMod != null;

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
    }

    private void Save()
    {
        if( _mod != null )
        {
            Penumbra.ModManager.OptionUpdate( _mod, _groupIdx, _optionIdx, _files, _manipulations, _fileSwaps );
        }
    }

    public override void OnClose()
    {
        _subMod = null;
    }

    private void DrawFileTab()
    {
        using var tab = ImRaii.TabItem( "File Redirections" );
        if( !tab )
        {
            return;
        }

        using var list = ImRaii.Table( "##files", 3 );
        if( !list )
        {
            return;
        }

        foreach( var file in _availableFiles )
        {
            ImGui.TableNextColumn();
            ConfigWindow.Text( file.RelFile.Path );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( file.Size.ToString() );
            ImGui.TableNextColumn();
            if( file.SubMods.Count == 0 )
            {
                ImGui.TextUnformatted( "Unused" );
            }

            foreach( var (groupIdx, optionIdx, gamePath) in file.SubMods )
            {
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                var group  = groupIdx >= 0 ? _mod!.Groups[ groupIdx ] : null;
                var option = groupIdx >= 0 ? group![ optionIdx ] : _mod!.Default;
                var text = groupIdx >= 0
                    ? $"{group!.Name} - {option.Name}"
                    : option.Name;
                ImGui.TextUnformatted( text );
                ImGui.TableNextColumn();
                ConfigWindow.Text( gamePath.Path );
            }
        }

        ImGui.TableNextRow();
        foreach( var (gamePath, fullPath) in _files )
        {
            ImGui.TableNextColumn();
            ConfigWindow.Text( gamePath.Path );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( fullPath.FullName );
            ImGui.TableNextColumn();
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

        foreach( var manip in _manipulations )
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

        foreach( var (from, to) in _fileSwaps )
        {
            ImGui.TableNextColumn();
            ConfigWindow.Text( from.Path );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( to.FullName );
            ImGui.TableNextColumn();
        }
    }

    public SubModEditWindow()
        : base( WindowBaseLabel )
    { }
}