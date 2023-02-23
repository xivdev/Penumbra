using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using Penumbra.Util;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileDialogManager _materialFileDialog = ConfigWindow.SetupFileManager();

    private FullPath FindAssociatedShpk( MtrlFile mtrl )
    {
        if( !Utf8GamePath.FromString( $"shader/sm5/shpk/{mtrl.ShaderPackage.Name}", out var shpkPath, true ) )
        {
            return FullPath.Empty;
        }

        return FindBestMatch( shpkPath );
    }

    private void LoadAssociatedShpk( MtrlFile mtrl )
    {
        try
        {
            _mtrlTabState.LoadedShpkPath = FindAssociatedShpk( mtrl );
            var data = _mtrlTabState.LoadedShpkPath.IsRooted
                ? File.ReadAllBytes( _mtrlTabState.LoadedShpkPath.FullName )
                : Dalamud.GameData.GetFile( _mtrlTabState.LoadedShpkPath.InternalName.ToString() )?.Data;
            if( data?.Length > 0 )
            {
                mtrl.AssociatedShpk = new ShpkFile( data );
            }
        }
        catch( Exception e )
        {
            Penumbra.Log.Debug( $"Could not parse associated file {_mtrlTabState.LoadedShpkPath} to Shpk:\n{e}" );
            _mtrlTabState.LoadedShpkPath = FullPath.Empty;
            mtrl.AssociatedShpk          = null;
        }

        UpdateTextureLabels( mtrl );
    }

    private void UpdateTextureLabels( MtrlFile file )
    {
        var samplers = file.GetSamplersByTexture();
        _mtrlTabState.TextureLabels.Clear();
        _mtrlTabState.TextureLabelWidth = 50f * ImGuiHelpers.GlobalScale;
        using( var font = ImRaii.PushFont( UiBuilder.MonoFont ) )
        {
            for( var i = 0; i < file.Textures.Length; ++i )
            {
                var (sampler, shpkSampler) = samplers[ i ];
                var name = shpkSampler.HasValue ? shpkSampler.Value.Name : sampler.HasValue ? $"0x{sampler.Value.SamplerId:X8}" : $"#{i}";
                _mtrlTabState.TextureLabels.Add( name );
                _mtrlTabState.TextureLabelWidth = Math.Max( _mtrlTabState.TextureLabelWidth, ImGui.CalcTextSize( name ).X );
            }
        }

        _mtrlTabState.TextureLabelWidth = _mtrlTabState.TextureLabelWidth / ImGuiHelpers.GlobalScale + 4;
    }

    private bool DrawPackageNameInput( MtrlFile file, bool disabled )
    {
        var ret = false;
        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        if( ImGui.InputText( "Shader Package Name", ref file.ShaderPackage.Name, 63, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
        {
            ret                          = true;
            file.AssociatedShpk          = null;
            _mtrlTabState.LoadedShpkPath = FullPath.Empty;
        }

        if( ImGui.IsItemDeactivatedAfterEdit() )
        {
            LoadAssociatedShpk( file );
        }

        return ret;
    }

    private static bool DrawShaderFlagsInput( MtrlFile file, bool disabled )
    {
        var ret       = false;
        var shpkFlags = ( int )file.ShaderPackage.Flags;
        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        if( ImGui.InputInt( "Shader Package Flags", ref shpkFlags, 0, 0,
               ImGuiInputTextFlags.CharsHexadecimal | ( disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) ) )
        {
            file.ShaderPackage.Flags = ( uint )shpkFlags;
            ret                      = true;
        }

        return ret;
    }

    private void DrawCustomAssociations( MtrlFile file, bool disabled )
    {
        var text = file.AssociatedShpk == null
            ? "Associated .shpk file: None"
            : $"Associated .shpk file: {_mtrlTabState.LoadedShpkPath}";

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ImGui.Selectable( text );

        if( disabled )
        {
            return;
        }

        if( ImGui.Button( "Associate custom ShPk file" ) )
        {
            _materialFileDialog.OpenFileDialog( "Associate custom .shpk file...", ".shpk", ( success, name ) =>
            {
                if( !success )
                {
                    return;
                }

                try
                {
                    file.AssociatedShpk          = new ShpkFile( File.ReadAllBytes( name ) );
                    _mtrlTabState.LoadedShpkPath = new FullPath( name );
                }
                catch( Exception e )
                {
                    Penumbra.Log.Error( $"Could not load .shpk file {name}:\n{e}" );
                    ChatUtil.NotificationMessage( $"Could not load {Path.GetFileName( name )}:\n{e.Message}", "Penumbra Advanced Editing", NotificationType.Error );
                }

                ChatUtil.NotificationMessage( $"Advanced Shader Resources for this material will now be based on the supplied {Path.GetFileName( name )}",
                    "Penumbra Advanced Editing", NotificationType.Success );
            });
        }

        var defaultFile = FindAssociatedShpk( file );
        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( "Associate default ShPk file", Vector2.Zero, defaultFile.FullName, defaultFile.Equals( _mtrlTabState.LoadedShpkPath ) ) )
        {
            LoadAssociatedShpk( file );
            if( file.AssociatedShpk != null )
            {
                ChatUtil.NotificationMessage( $"Advanced Shader Resources for this material will now be based on the default {file.ShaderPackage.Name}",
                    "Penumbra Advanced Editing", NotificationType.Success );
            }
            else
            {
                ChatUtil.NotificationMessage( $"Could not load default {file.ShaderPackage.Name}", "Penumbra Advanced Editing", NotificationType.Error );
            }
        }

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
    }

    private bool DrawMaterialShaderResources( MtrlFile file, bool disabled )
    {
        var ret = false;
        if( !ImGui.CollapsingHeader( "Advanced Shader Resources" ) )
        {
            return ret;
        }

        ret |= DrawPackageNameInput( file, disabled );
        ret |= DrawShaderFlagsInput( file, disabled );
        DrawCustomAssociations( file, disabled );

        if( file.ShaderPackage.ShaderKeys.Length > 0 || !disabled && file.AssociatedShpk != null && file.AssociatedShpk.MaterialKeys.Length > 0 )
        {
            using var t = ImRaii.TreeNode( "Shader Keys" );
            if( t )
            {
                var definedKeys = new HashSet< uint >();

                foreach( var (key, idx) in file.ShaderPackage.ShaderKeys.WithIndex() )
                {
                    definedKeys.Add( key.Category );
                    using var t2 = ImRaii.TreeNode( $"#{idx}: 0x{key.Category:X8} = 0x{key.Value:X8}###{idx}: 0x{key.Category:X8}", disabled ? ImGuiTreeNodeFlags.Leaf : 0 );
                    if( t2 )
                    {
                        if( !disabled )
                        {
                            var shpkKey = file.AssociatedShpk?.GetMaterialKeyById( key.Category );
                            if( shpkKey.HasValue )
                            {
                                ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                                using var c = ImRaii.Combo( "Value", $"0x{key.Value:X8}" );
                                if( c )
                                {
                                    foreach( var value in shpkKey.Value.Values )
                                    {
                                        if( ImGui.Selectable( $"0x{value:X8}", value == key.Value ) )
                                        {
                                            file.ShaderPackage.ShaderKeys[ idx ].Value = value;
                                            ret                                        = true;
                                        }
                                    }
                                }
                            }

                            if( ImGui.Button( "Remove Key" ) )
                            {
                                file.ShaderPackage.ShaderKeys = file.ShaderPackage.ShaderKeys.RemoveItems( idx );
                                ret                           = true;
                            }
                        }
                    }
                }

                if( !disabled && file.AssociatedShpk != null )
                {
                    var missingKeys = file.AssociatedShpk.MaterialKeys.Where( key => !definedKeys.Contains( key.Id ) ).ToArray();
                    if( missingKeys.Length > 0 )
                    {
                        var selectedKey = Array.Find( missingKeys, key => key.Id == _mtrlTabState.MaterialNewKeyId );
                        if( Array.IndexOf( missingKeys, selectedKey ) < 0 )
                        {
                            selectedKey                    = missingKeys[ 0 ];
                            _mtrlTabState.MaterialNewKeyId = selectedKey.Id;
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        using( var c = ImRaii.Combo( "##NewConstantId", $"ID: 0x{selectedKey.Id:X8}" ) )
                        {
                            if( c )
                            {
                                foreach( var key in missingKeys )
                                {
                                    if( ImGui.Selectable( $"ID: 0x{key.Id:X8}", key.Id == _mtrlTabState.MaterialNewKeyId ) )
                                    {
                                        selectedKey                    = key;
                                        _mtrlTabState.MaterialNewKeyId = key.Id;
                                    }
                                }
                            }
                        }

                        ImGui.SameLine();
                        if( ImGui.Button( "Add Key" ) )
                        {
                            file.ShaderPackage.ShaderKeys = file.ShaderPackage.ShaderKeys.AddItem( new ShaderKey
                            {
                                Category = selectedKey.Id,
                                Value    = selectedKey.DefaultValue,
                            } );
                            ret = true;
                        }
                    }
                }
            }
        }

        if( file.AssociatedShpk != null )
        {
            var definedKeys = new Dictionary< uint, uint >();
            foreach( var key in file.ShaderPackage.ShaderKeys )
            {
                definedKeys[ key.Category ] = key.Value;
            }

            var materialKeys = Array.ConvertAll( file.AssociatedShpk.MaterialKeys, key =>
            {
                if( definedKeys.TryGetValue( key.Id, out var value ) )
                {
                    return value;
                }
                else
                {
                    return key.DefaultValue;
                }
            } );
            var vertexShaders = new IndexSet( file.AssociatedShpk.VertexShaders.Length, false );
            var pixelShaders  = new IndexSet( file.AssociatedShpk.PixelShaders.Length, false );
            foreach( var node in file.AssociatedShpk.Nodes )
            {
                if( node.MaterialKeys.WithIndex().All( key => key.Value == materialKeys[ key.Index ] ) )
                {
                    foreach( var pass in node.Passes )
                    {
                        vertexShaders.Add( ( int )pass.VertexShader );
                        pixelShaders.Add( ( int )pass.PixelShader );
                    }
                }
            }

            ImRaii.TreeNode( $"Vertex Shaders: {( vertexShaders.Count > 0 ? string.Join( ", ", vertexShaders.Select( i => $"#{i}" ) ) : "???" )}", ImGuiTreeNodeFlags.Leaf )
               .Dispose();
            ImRaii.TreeNode( $"Pixel Shaders: {( pixelShaders.Count > 0 ? string.Join( ", ", pixelShaders.Select( i => $"#{i}" ) ) : "???" )}", ImGuiTreeNodeFlags.Leaf ).Dispose();
        }

        if( file.ShaderPackage.Constants.Length   > 0
        || file.ShaderPackage.ShaderValues.Length > 0
        || !disabled && file.AssociatedShpk != null && file.AssociatedShpk.Constants.Length > 0 )
        {
            var materialParams = file.AssociatedShpk?.GetConstantById( ShpkFile.MaterialParamsConstantId );

            using var t = ImRaii.TreeNode( materialParams?.Name ?? "Constants" );
            if( t )
            {
                var orphanValues          = new IndexSet( file.ShaderPackage.ShaderValues.Length, true );
                var aliasedValueCount     = 0;
                var definedConstants      = new HashSet< uint >();
                var hasMalformedConstants = false;

                foreach( var constant in file.ShaderPackage.Constants )
                {
                    definedConstants.Add( constant.Id );
                    var values = file.GetConstantValues( constant );
                    if( file.GetConstantValues( constant ).Length > 0 )
                    {
                        var unique = orphanValues.RemoveRange( constant.ByteOffset >> 2, values.Length );
                        aliasedValueCount += values.Length - unique;
                    }
                    else
                    {
                        hasMalformedConstants = true;
                    }
                }

                foreach( var (constant, idx) in file.ShaderPackage.Constants.WithIndex() )
                {
                    var values           = file.GetConstantValues( constant );
                    var paramValueOffset = -values.Length;
                    if( values.Length > 0 )
                    {
                        var shpkParam       = file.AssociatedShpk?.GetMaterialParamById( constant.Id );
                        var paramByteOffset = shpkParam.HasValue ? shpkParam.Value.ByteOffset : -1;
                        if( ( paramByteOffset & 0x3 ) == 0 )
                        {
                            paramValueOffset = paramByteOffset >> 2;
                        }
                    }

                    var (constantName, componentOnly) = MaterialParamRangeName( materialParams?.Name ?? "", paramValueOffset, values.Length );

                    using var t2 = ImRaii.TreeNode( $"#{idx}{( constantName != null ? ": " + constantName : "" )} (ID: 0x{constant.Id:X8})" );
                    if( t2 )
                    {
                        if( values.Length > 0 )
                        {
                            var valueOffset = constant.ByteOffset >> 2;

                            for( var valueIdx = 0; valueIdx < values.Length; ++valueIdx )
                            {
                                ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                                if( ImGui.InputFloat(
                                       $"{MaterialParamName( componentOnly, paramValueOffset + valueIdx ) ?? $"#{valueIdx}"} (at 0x{( valueOffset + valueIdx ) << 2:X4})",
                                       ref values[ valueIdx ], 0.0f, 0.0f, "%.3f",
                                       disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                                {
                                    ret = true;
                                }
                            }
                        }
                        else
                        {
                            ImRaii.TreeNode( $"Offset: 0x{constant.ByteOffset:X4}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                            ImRaii.TreeNode( $"Size: 0x{constant.ByteSize:X4}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                        }

                        if( !disabled
                        && !hasMalformedConstants
                        && orphanValues.Count == 0
                        && aliasedValueCount  == 0
                        && ImGui.Button( "Remove Constant" ) )
                        {
                            file.ShaderPackage.ShaderValues = file.ShaderPackage.ShaderValues.RemoveItems( constant.ByteOffset >> 2, constant.ByteSize >> 2 );
                            file.ShaderPackage.Constants    = file.ShaderPackage.Constants.RemoveItems( idx );
                            for( var i = 0; i < file.ShaderPackage.Constants.Length; ++i )
                            {
                                if( file.ShaderPackage.Constants[ i ].ByteOffset >= constant.ByteOffset )
                                {
                                    file.ShaderPackage.Constants[ i ].ByteOffset -= constant.ByteSize;
                                }
                            }

                            ret = true;
                        }
                    }
                }

                if( orphanValues.Count > 0 )
                {
                    using var t2 = ImRaii.TreeNode( $"Orphan Values ({orphanValues.Count})" );
                    if( t2 )
                    {
                        foreach( var idx in orphanValues )
                        {
                            ImGui.SetNextItemWidth( ImGui.GetFontSize() * 10.0f );
                            if( ImGui.InputFloat( $"#{idx} (at 0x{idx << 2:X4})",
                                   ref file.ShaderPackage.ShaderValues[ idx ], 0.0f, 0.0f, "%.3f",
                                   disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                            {
                                ret = true;
                            }
                        }
                    }
                }
                else if( !disabled && !hasMalformedConstants && file.AssociatedShpk != null )
                {
                    var missingConstants = file.AssociatedShpk.MaterialParams.Where( constant
                        => ( constant.ByteOffset & 0x3 ) == 0 && ( constant.ByteSize & 0x3 ) == 0 && !definedConstants.Contains( constant.Id ) ).ToArray();
                    if( missingConstants.Length > 0 )
                    {
                        var selectedConstant = Array.Find( missingConstants, constant => constant.Id == _mtrlTabState.MaterialNewConstantId );
                        if( selectedConstant.ByteSize == 0 )
                        {
                            selectedConstant                    = missingConstants[ 0 ];
                            _mtrlTabState.MaterialNewConstantId = selectedConstant.Id;
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 450.0f );
                        var (selectedConstantName, _) = MaterialParamRangeName( materialParams?.Name ?? "", selectedConstant.ByteOffset >> 2, selectedConstant.ByteSize >> 2 );
                        using( var c = ImRaii.Combo( "##NewConstantId", $"{selectedConstantName} (ID: 0x{selectedConstant.Id:X8})" ) )
                        {
                            if( c )
                            {
                                foreach( var constant in missingConstants )
                                {
                                    var (constantName, _) = MaterialParamRangeName( materialParams?.Name ?? "", constant.ByteOffset >> 2, constant.ByteSize >> 2 );
                                    if( ImGui.Selectable( $"{constantName} (ID: 0x{constant.Id:X8})", constant.Id == _mtrlTabState.MaterialNewConstantId ) )
                                    {
                                        selectedConstant                    = constant;
                                        _mtrlTabState.MaterialNewConstantId = constant.Id;
                                    }
                                }
                            }
                        }

                        ImGui.SameLine();
                        if( ImGui.Button( "Add Constant" ) )
                        {
                            file.ShaderPackage.ShaderValues = file.ShaderPackage.ShaderValues.AddItem( 0.0f, selectedConstant.ByteSize >> 2 );
                            file.ShaderPackage.Constants = file.ShaderPackage.Constants.AddItem( new MtrlFile.Constant
                            {
                                Id         = _mtrlTabState.MaterialNewConstantId,
                                ByteOffset = ( ushort )( file.ShaderPackage.ShaderValues.Length << 2 ),
                                ByteSize   = selectedConstant.ByteSize,
                            } );
                            ret = true;
                        }
                    }
                }
            }
        }

        if( file.ShaderPackage.Samplers.Length > 0
        || file.Textures.Length                > 0
        || !disabled && file.AssociatedShpk != null && file.AssociatedShpk.Samplers.Any( sampler => sampler.Slot == 2 ) )
        {
            using var t = ImRaii.TreeNode( "Samplers" );
            if( t )
            {
                var orphanTextures      = new IndexSet( file.Textures.Length, true );
                var aliasedTextureCount = 0;
                var definedSamplers     = new HashSet< uint >();

                foreach( var sampler in file.ShaderPackage.Samplers )
                {
                    if( !orphanTextures.Remove( sampler.TextureIndex ) )
                    {
                        ++aliasedTextureCount;
                    }

                    definedSamplers.Add( sampler.SamplerId );
                }

                foreach( var (sampler, idx) in file.ShaderPackage.Samplers.WithIndex() )
                {
                    var       shpkSampler = file.AssociatedShpk?.GetSamplerById( sampler.SamplerId );
                    using var t2          = ImRaii.TreeNode( $"#{idx}{( shpkSampler.HasValue ? ": " + shpkSampler.Value.Name : "" )} (ID: 0x{sampler.SamplerId:X8})" );
                    if( t2 )
                    {
                        ImRaii.TreeNode( $"Texture: #{sampler.TextureIndex} - {Path.GetFileName( file.Textures[ sampler.TextureIndex ].Path )}", ImGuiTreeNodeFlags.Leaf )
                           .Dispose();

                        // FIXME this probably doesn't belong here
                        static unsafe bool InputHexUInt16( string label, ref ushort v, ImGuiInputTextFlags flags )
                        {
                            fixed( ushort* v2 = &v )
                            {
                                return ImGui.InputScalar( label, ImGuiDataType.U16, ( nint )v2, nint.Zero, nint.Zero, "%04X", flags );
                            }
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( InputHexUInt16( "Texture Flags", ref file.Textures[ sampler.TextureIndex ].Flags, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                        {
                            ret = true;
                        }

                        var sampFlags = ( int )sampler.Flags;
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( ImGui.InputInt( "Sampler Flags", ref sampFlags, 0, 0,
                               ImGuiInputTextFlags.CharsHexadecimal | ( disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) ) )
                        {
                            file.ShaderPackage.Samplers[ idx ].Flags = ( uint )sampFlags;
                            ret                                      = true;
                        }

                        if( !disabled
                        && orphanTextures.Count == 0
                        && aliasedTextureCount  == 0
                        && ImGui.Button( "Remove Sampler" ) )
                        {
                            file.Textures               = file.Textures.RemoveItems( sampler.TextureIndex );
                            file.ShaderPackage.Samplers = file.ShaderPackage.Samplers.RemoveItems( idx );
                            for( var i = 0; i < file.ShaderPackage.Samplers.Length; ++i )
                            {
                                if( file.ShaderPackage.Samplers[ i ].TextureIndex >= sampler.TextureIndex )
                                {
                                    --file.ShaderPackage.Samplers[ i ].TextureIndex;
                                }
                            }

                            ret = true;
                        }
                    }
                }

                if( orphanTextures.Count > 0 )
                {
                    using var t2 = ImRaii.TreeNode( $"Orphan Textures ({orphanTextures.Count})" );
                    if( t2 )
                    {
                        foreach( var idx in orphanTextures )
                        {
                            ImRaii.TreeNode( $"#{idx}: {Path.GetFileName( file.Textures[ idx ].Path )} - {file.Textures[ idx ].Flags:X4}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                        }
                    }
                }
                else if( !disabled && file.AssociatedShpk != null && aliasedTextureCount == 0 && file.Textures.Length < 255 )
                {
                    var missingSamplers = file.AssociatedShpk.Samplers.Where( sampler => sampler.Slot == 2 && !definedSamplers.Contains( sampler.Id ) ).ToArray();
                    if( missingSamplers.Length > 0 )
                    {
                        var selectedSampler = Array.Find( missingSamplers, sampler => sampler.Id == _mtrlTabState.MaterialNewSamplerId );
                        if( selectedSampler.Name == null )
                        {
                            selectedSampler                    = missingSamplers[ 0 ];
                            _mtrlTabState.MaterialNewSamplerId = selectedSampler.Id;
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 450.0f );
                        using( var c = ImRaii.Combo( "##NewSamplerId", $"{selectedSampler.Name} (ID: 0x{selectedSampler.Id:X8})" ) )
                        {
                            if( c )
                            {
                                foreach( var sampler in missingSamplers )
                                {
                                    if( ImGui.Selectable( $"{sampler.Name} (ID: 0x{sampler.Id:X8})", sampler.Id == _mtrlTabState.MaterialNewSamplerId ) )
                                    {
                                        selectedSampler                    = sampler;
                                        _mtrlTabState.MaterialNewSamplerId = sampler.Id;
                                    }
                                }
                            }
                        }

                        ImGui.SameLine();
                        if( ImGui.Button( "Add Sampler" ) )
                        {
                            file.Textures = file.Textures.AddItem( new MtrlFile.Texture
                            {
                                Path  = string.Empty,
                                Flags = 0,
                            } );
                            file.ShaderPackage.Samplers = file.ShaderPackage.Samplers.AddItem( new Sampler
                            {
                                SamplerId    = _mtrlTabState.MaterialNewSamplerId,
                                TextureIndex = ( byte )file.Textures.Length,
                                Flags        = 0,
                            } );
                            ret = true;
                        }
                    }
                }
            }
        }

        return ret;
    }
}