using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Files;
using Penumbra.Mods;
using Functions = Penumbra.GameData.Util.Functions;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileEditor< MtrlFile > _materialTab;
    private readonly FileEditor< MdlFile >  _modelTab;

    private class FileEditor< T > where T : class, IWritable
    {
        private readonly string                                           _tabName;
        private readonly string                                           _fileType;
        private readonly Func< IReadOnlyList< Mod.Editor.FileRegistry > > _getFiles;
        private readonly Func< T, bool, bool >                            _drawEdit;

        private Mod.Editor.FileRegistry? _currentPath;
        private T?                       _currentFile;
        private bool                     _changed;

        private string _defaultPath = string.Empty;
        private bool   _inInput     = false;
        private T?     _defaultFile;

        private IReadOnlyList< Mod.Editor.FileRegistry > _list = null!;

        public FileEditor( string tabName, string fileType, Func< IReadOnlyList< Mod.Editor.FileRegistry > > getFiles,
            Func< T, bool, bool > drawEdit )
        {
            _tabName  = tabName;
            _fileType = fileType;
            _getFiles = getFiles;
            _drawEdit = drawEdit;
        }

        public void Draw()
        {
            _list = _getFiles();
            if( _list.Count == 0 )
            {
                return;
            }

            using var tab = ImRaii.TabItem( _tabName );
            if( !tab )
            {
                return;
            }

            ImGui.NewLine();
            DrawFileSelectCombo();
            SaveButton();
            ImGui.SameLine();
            ResetButton();
            ImGui.SameLine();
            DefaultInput();
            ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );

            DrawFilePanel();
        }

        private void DefaultInput()
        {
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
            ImGui.InputTextWithHint( "##defaultInput", "Input game path to compare...", ref _defaultPath, Utf8GamePath.MaxGamePathLength );
            _inInput = ImGui.IsItemActive();
            if( ImGui.IsItemDeactivatedAfterEdit() && _defaultPath.Length > 0 )
            {
                try
                {
                    var file = Dalamud.GameData.GetFile( _defaultPath );
                    if( file != null )
                    {
                        _defaultFile = Activator.CreateInstance( typeof( T ), file.Data ) as T;
                    }
                }
                catch
                {
                    _defaultFile = null;
                }
            }
        }

        public void Reset()
        {
            _currentPath = null;
            _currentFile = null;
            _changed     = false;
        }

        private void DrawFileSelectCombo()
        {
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
            using var combo = ImRaii.Combo( "##fileSelect", _currentPath?.RelPath.ToString() ?? $"Select {_fileType} File..." );
            if( !combo )
            {
                return;
            }

            foreach( var file in _list )
            {
                if( ImGui.Selectable( file.RelPath.ToString(), ReferenceEquals( file, _currentPath ) ) )
                {
                    UpdateCurrentFile( file );
                }
            }
        }

        private void UpdateCurrentFile( Mod.Editor.FileRegistry path )
        {
            if( ReferenceEquals( _currentPath, path ) )
            {
                return;
            }

            _changed     = false;
            _currentPath = path;
            try
            {
                var bytes = File.ReadAllBytes( _currentPath.File.FullName );
                _currentFile = Activator.CreateInstance( typeof( T ), bytes ) as T;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not parse {_fileType} file {_currentPath.File.FullName}:\n{e}" );
                _currentFile = null;
            }
        }

        private void SaveButton()
        {
            if( ImGuiUtil.DrawDisabledButton( "Save to File", Vector2.Zero,
                   $"Save the selected {_fileType} file with all changes applied. This is not revertible.", !_changed ) )
            {
                File.WriteAllBytes( _currentPath!.File.FullName, _currentFile!.Write() );
                _changed = false;
            }
        }

        private void ResetButton()
        {
            if( ImGuiUtil.DrawDisabledButton( "Reset Changes", Vector2.Zero,
                   $"Reset all changes made to the {_fileType} file.", !_changed ) )
            {
                var tmp = _currentPath;
                _currentPath = null;
                UpdateCurrentFile( tmp! );
            }
        }

        private void DrawFilePanel()
        {
            using var child = ImRaii.Child( "##filePanel", -Vector2.One, true );
            if( !child )
            {
                return;
            }

            if( _currentPath != null )
            {
                if( _currentFile == null )
                {
                    ImGui.TextUnformatted( $"Could not parse selected {_fileType} file." );
                }
                else
                {
                    using var id = ImRaii.PushId( 0 );
                    _changed |= _drawEdit( _currentFile, false );
                }
            }

            if( !_inInput && _defaultPath.Length > 0 )
            {
                if( _currentPath != null )
                {
                    ImGui.NewLine();
                    ImGui.NewLine();
                    ImGui.TextUnformatted( $"Preview of {_defaultPath}:" );
                    ImGui.Separator();
                }

                if( _defaultFile == null )
                {
                    ImGui.TextUnformatted( $"Could not parse provided  {_fileType} game file." );
                }
                else
                {
                    using var id = ImRaii.PushId( 1 );
                    _drawEdit( _defaultFile, true );
                }
            }
        }
    }

    private static bool DrawModelPanel( MdlFile file, bool disabled )
    {
        var ret = false;
        for( var i = 0; i < file.Materials.Length; ++i )
        {
            using var id  = ImRaii.PushId( i );
            var       tmp = file.Materials[ i ];
            if( ImGui.InputText( string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None )
            && tmp.Length > 0
            && tmp        != file.Materials[ i ] )
            {
                file.Materials[ i ] = tmp;
                ret                 = true;
            }
        }

        return !disabled && ret;
    }


    private static bool DrawMaterialPanel( MtrlFile file, bool disabled )
    {
        var ret = DrawMaterialTextureChange( file, disabled );


        ImGui.NewLine();
        ret |= DrawMaterialColorSetChange( file, disabled );

        return !disabled && ret;
    }

    private static bool DrawMaterialTextureChange( MtrlFile file, bool disabled )
    {
        using var id  = ImRaii.PushId( "Textures" );
        var       ret = false;
        for( var i = 0; i < file.Textures.Length; ++i )
        {
            using var _   = ImRaii.PushId( i );
            var       tmp = file.Textures[ i ].Path;
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X );
            if( ImGui.InputText( string.Empty, ref tmp, Utf8GamePath.MaxGamePathLength,
                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None )
            && tmp.Length > 0
            && tmp        != file.Textures[ i ].Path )
            {
                ret                     = true;
                file.Textures[ i ].Path = tmp;
            }
        }

        return ret;
    }

    private static bool DrawMaterialColorSetChange( MtrlFile file, bool disabled )
    {
        if( file.ColorSets.Length == 0 )
        {
            return false;
        }

        using var table = ImRaii.Table( "##ColorSets", 10,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV );
        if( !table )
        {
            return false;
        }

        ImGui.TableNextColumn();
        ImGui.TableHeader( string.Empty );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Row" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Diffuse" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Specular" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Emissive" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Gloss" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Tile" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Repeat" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Skew" );
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Dye" );

        var ret = false;
        for( var j = 0; j < file.ColorSets.Length; ++j )
        {
            using var _ = ImRaii.PushId( j );
            for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
            {
                ret |= DrawColorSetRow( file, j, i, disabled );
                ImGui.TableNextRow();
            }
        }

        return ret;
    }

    private static unsafe void ColorSetCopyClipboardButton( MtrlFile.ColorSet.Row row, MtrlFile.ColorDyeSet.Row dye )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Clipboard.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Export this row to your clipboard.", false, true ) )
        {
            try
            {
                var data = new byte[MtrlFile.ColorSet.Row.Size + 2];
                fixed( byte* ptr = data )
                {
                    Functions.MemCpyUnchecked( ptr, &row, MtrlFile.ColorSet.Row.Size );
                    Functions.MemCpyUnchecked( ptr + MtrlFile.ColorSet.Row.Size, &dye, 2 );
                }

                var text = Convert.ToBase64String( data );
                ImGui.SetClipboardText( text );
            }
            catch
            {
                // ignored
            }
        }
    }

    private static unsafe bool ColorSetPasteFromClipboardButton( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled )
    {
        if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Paste.ToIconString(), ImGui.GetFrameHeight() * Vector2.One,
               "Import an exported row from your clipboard onto this row.", disabled, true ) )
        {
            try
            {
                var text = ImGui.GetClipboardText();
                var data = Convert.FromBase64String( text );
                if( data.Length          != MtrlFile.ColorSet.Row.Size + 2
                || file.ColorSets.Length <= colorSetIdx )
                {
                    return false;
                }

                fixed( byte* ptr = data )
                {
                    file.ColorSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorSet.Row* )ptr;
                    if( file.ColorDyeSets.Length <= colorSetIdx )
                    {
                        file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] = *( MtrlFile.ColorDyeSet.Row* )( ptr + MtrlFile.ColorSet.Row.Size );
                    }
                }

                return true;
            }
            catch
            {
                // ignored
            }
        }

        return false;
    }

    private static bool DrawColorSetRow( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled )
    {
        using var id        = ImRaii.PushId( rowIdx );
        var       row       = file.ColorSets[ colorSetIdx ].Rows[ rowIdx ];
        var       hasDye    = file.ColorDyeSets.Length > colorSetIdx;
        var       dye       = hasDye ? file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ] : new MtrlFile.ColorDyeSet.Row();
        var       floatSize = 70 * ImGuiHelpers.GlobalScale;
        var       intSize   = 45 * ImGuiHelpers.GlobalScale;
        ImGui.TableNextColumn();
        ColorSetCopyClipboardButton( row, dye );
        ImGui.SameLine();
        var ret = ColorSetPasteFromClipboardButton( file, colorSetIdx, rowIdx, disabled );

        ImGui.TableNextColumn();
        ImGui.TextUnformatted( $"#{rowIdx + 1:D2}" );

        ImGui.TableNextColumn();
        using var dis = ImRaii.Disabled(disabled);
        ret |= ColorPicker( "##Diffuse", "Diffuse Color", row.Diffuse, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = c );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeDiffuse", "Apply Diffuse Color on Dye", dye.Diffuse,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Diffuse = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Specular", "Specular Color", row.Specular, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Specular = c );
        ImGui.SameLine();
        var tmpFloat = row.SpecularStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SpecularStrength", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.SpecularStrength )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = tmpFloat;
            ret                                                           = true;
        }

        ImGuiUtil.HoverTooltip( "Specular Strength", ImGuiHoveredFlags.AllowWhenDisabled );

        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecular", "Apply Specular Color on Dye", dye.Specular,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Specular = b, ImGuiHoveredFlags.AllowWhenDisabled );
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeSpecularStrength", "Apply Specular Strength on Dye", dye.SpecularStrength,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].SpecularStrength = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        ret |= ColorPicker( "##Emissive", "Emissive Color", row.Emissive, c => file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = c );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeEmissive", "Apply Emissive Color on Dye", dye.Emissive,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Emissive = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        tmpFloat = row.GlossStrength;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##GlossStrength", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.GlossStrength )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].GlossStrength = tmpFloat;
            ret                                                        = true;
        }

        ImGuiUtil.HoverTooltip( "Gloss Strength", ImGuiHoveredFlags.AllowWhenDisabled );
        if( hasDye )
        {
            ImGui.SameLine();
            ret |= ImGuiUtil.Checkbox( "##dyeGloss", "Apply Gloss Strength on Dye", dye.Gloss,
                b => file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Gloss = b, ImGuiHoveredFlags.AllowWhenDisabled );
        }

        ImGui.TableNextColumn();
        int tmpInt = row.TileSet;
        ImGui.SetNextItemWidth( intSize );
        if( ImGui.InputInt( "##TileSet", ref tmpInt, 0, 0 ) && tmpInt != row.TileSet && tmpInt is >= 0 and <= ushort.MaxValue )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].TileSet = ( ushort )tmpInt;
            ret                                                  = true;
        }

        ImGuiUtil.HoverTooltip( "Tile Set", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialRepeat.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatX", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.MaterialRepeat.X )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { X = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat X", ImGuiHoveredFlags.AllowWhenDisabled );
        ImGui.SameLine();
        tmpFloat = row.MaterialRepeat.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatY", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.MaterialRepeat.Y )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { Y = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialSkew.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewX", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.MaterialSkew.X )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { X = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew X", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.SameLine();
        tmpFloat = row.MaterialSkew.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewY", ref tmpFloat, 0.1f, 0f ) && tmpFloat != row.MaterialSkew.Y )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { Y = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        if( hasDye )
        {
            tmpInt = dye.Template;
            ImGui.SetNextItemWidth( intSize );
            if( ImGui.InputInt( "##DyeTemplate", ref tmpInt, 0, 0 )
            && tmpInt != dye.Template
            && tmpInt is >= 0 and <= ushort.MaxValue )
            {
                file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Template = ( ushort )tmpInt;
                ret                                                      = true;
            }

            ImGuiUtil.HoverTooltip( "Dye Template", ImGuiHoveredFlags.AllowWhenDisabled );
        }


        return ret;
    }

    private static bool ColorPicker( string label, string tooltip, Vector3 input, Action< Vector3 > setter )
    {
        var ret = false;
        var tmp = input;
        if( ImGui.ColorEdit3( label, ref tmp,
               ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.InputRGB | ImGuiColorEditFlags.NoTooltip )
        && tmp != input )
        {
            setter( tmp );
            ret = true;
        }

        ImGuiUtil.HoverTooltip( tooltip, ImGuiHoveredFlags.AllowWhenDisabled );

        return ret;
    }

    private void DrawMaterialReassignmentTab()
    {
        if( _editor!.ModelFiles.Count == 0 )
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
        foreach( var (info, idx) in _editor.ModelFiles.WithIndex() )
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
            ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
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
                ImGui.SetNextItemWidth( 400 * ImGuiHelpers.GlobalScale );
                tmp = info.CurrentMaterials[ i ];
                if( ImGui.InputText( $"##{i}", ref tmp, 64 ) )
                {
                    info.SetMaterial( tmp, i );
                }
            }
        }
    }
}