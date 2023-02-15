using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal.Notifications;
using ImGuiNET;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;
using Penumbra.String.Functions;
using Penumbra.Util;
using static Penumbra.GameData.Files.ShpkFile;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileEditor< MtrlFile > _materialTab;

    private readonly FileDialogManager _materialFileDialog = ConfigWindow.SetupFileManager();

    private uint _materialNewKeyId = 0;
    private uint _materialNewConstantId = 0;
    private uint _materialNewSamplerId = 0;

    private bool DrawMaterialPanel( MtrlFile file, bool disabled )
    {
        var ret = DrawMaterialTextureChange( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawBackFaceAndTransparency( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawMaterialColorSetChange( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawMaterialShaderResources( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawOtherMaterialDetails( file, disabled );

        _materialFileDialog.Draw();

        return !disabled && ret;
    }

    private static bool DrawMaterialTextureChange( MtrlFile file, bool disabled )
    {
        var samplers = file.GetSamplersByTexture();
        var names = new List<string>();
        var maxWidth = 0.0f;
        for( var i = 0; i < file.Textures.Length; ++i )
        {
            var (sampler, shpkSampler) = samplers[i];
            var name = shpkSampler.HasValue ? shpkSampler.Value.Name : sampler.HasValue ? $"0x{sampler.Value.SamplerId:X8}" : $"#{i}";
            names.Add( name );
            maxWidth = Math.Max( maxWidth, ImGui.CalcTextSize( name ).X );
        }

        using var id  = ImRaii.PushId( "Textures" );
        var       ret = false;
        for( var i = 0; i < file.Textures.Length; ++i )
        {
            using var _   = ImRaii.PushId( i );
            var       tmp = file.Textures[ i ].Path;
            ImGui.SetNextItemWidth( ImGui.GetContentRegionAvail().X - maxWidth );
            if( ImGui.InputText( names[i], ref tmp, Utf8GamePath.MaxGamePathLength,
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
        if( !file.ColorSets.Any( c => c.HasRows ) )
        {
            return false;
        }

        ColorSetCopyAllClipboardButton( file, 0 );
        ImGui.SameLine();
        var ret = ColorSetPasteAllClipboardButton( file, 0 );
        ImGui.SameLine();
        ImGui.Dummy( ImGuiHelpers.ScaledVector2( 20, 0 ) );
        ImGui.SameLine();
        ret |= DrawPreviewDye( file, disabled );

        using var table = ImRaii.Table( "##ColorSets", 11,
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
        ImGui.TableNextColumn();
        ImGui.TableHeader( "Dye Preview" );

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

        ImGui.SameLine( 200 * ImGuiHelpers.GlobalScale + ImGui.GetStyle().ItemSpacing.X + ImGui.GetStyle().WindowPadding.X );
        tmp = ( file.ShaderPackage.Flags & backfaceBit ) != 0;
        if( ImGui.Checkbox( "Hide Backfaces", ref tmp ) )
        {
            file.ShaderPackage.Flags = tmp ? file.ShaderPackage.Flags | backfaceBit : file.ShaderPackage.Flags & ~backfaceBit;
            ret                      = true;
        }

        return ret;
    }

    private bool DrawMaterialShaderResources( MtrlFile file, bool disabled )
    {
        var ret = false;

        if( !ImGui.CollapsingHeader( "Advanced Shader Resources" ) )
        {
            return false;
        }

        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        if( ImGui.InputText( "Shader Package Name", ref file.ShaderPackage.Name, 63, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
        {
            ret = true;
        }
        var shpkFlags = ( int )file.ShaderPackage.Flags;
        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        if( ImGui.InputInt( "Shader Package Flags", ref shpkFlags, 0, 0, ImGuiInputTextFlags.CharsHexadecimal | ( disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) ) )
        {
            file.ShaderPackage.Flags = ( uint )shpkFlags;
            ret = true;
        }
        ImRaii.TreeNode( $"Has associated ShPk file (for advanced editing): {( file.AssociatedShpk != null ? "Yes" : "No" )}", ImGuiTreeNodeFlags.Leaf ).Dispose();
        if( !disabled )
        {
            if( ImGui.Button( "Associate custom ShPk file" ) )
            {
                _materialFileDialog.OpenFileDialog( $"Associate custom ShPk file...", ".shpk", ( success, name ) =>
                {
                    if( !success )
                    {
                        return;
                    }

                    try
                    {
                        file.AssociatedShpk = new ShpkFile( File.ReadAllBytes( name ) );
                    }
                    catch( Exception e )
                    {
                        Penumbra.Log.Error( $"Could not load ShPk file {name}:\n{e}" );
                        ChatUtil.NotificationMessage( $"Could not load {Path.GetFileName( name )}:\n{e.Message}", "Penumbra Advanced Editing", NotificationType.Error );
                        return;
                    }
                    ChatUtil.NotificationMessage( $"Advanced Shader Resources for this material will now be based on the supplied {Path.GetFileName( name )}", "Penumbra Advanced Editing", NotificationType.Success );
                } );
            }
            ImGui.SameLine();
            if( ImGui.Button( "Associate default ShPk file" ) )
            {
                var shpk = LoadAssociatedShpk( file.ShaderPackage.Name );
                if( null != shpk )
                {
                    file.AssociatedShpk = shpk;
                    ChatUtil.NotificationMessage( $"Advanced Shader Resources for this material will now be based on the default {file.ShaderPackage.Name}", "Penumbra Advanced Editing", NotificationType.Success );
                }
                else
                {
                    ChatUtil.NotificationMessage( $"Could not load default {file.ShaderPackage.Name}", "Penumbra Advanced Editing", NotificationType.Error );
                }
            }
        }

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
                                            file.ShaderPackage.ShaderKeys[idx].Value = value;
                                            ret = true;
                                        }
                                    }
                                }
                            }
                            if( ImGui.Button( "Remove Key" ) )
                            {
                                ArrayRemove( ref file.ShaderPackage.ShaderKeys, idx );
                                ret = true;
                            }
                        }
                    }
                }

                if( !disabled && file.AssociatedShpk != null )
                {
                    var missingKeys = file.AssociatedShpk.MaterialKeys.Where( key => !definedKeys.Contains( key.Id ) ).ToArray();
                    if( missingKeys.Length > 0 )
                    {
                        var selectedKey = Array.Find( missingKeys, key => key.Id == _materialNewKeyId );
                        if( Array.IndexOf( missingKeys, selectedKey ) < 0 )
                        {
                            selectedKey = missingKeys[0];
                            _materialNewKeyId = selectedKey.Id;
                        }
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        using( var c = ImRaii.Combo( "##NewConstantId", $"ID: 0x{selectedKey.Id:X8}" ) )
                        {
                            if( c )
                            {
                                foreach( var key in missingKeys )
                                {
                                    if( ImGui.Selectable( $"ID: 0x{key.Id:X8}", key.Id == _materialNewKeyId ) )
                                    {
                                        selectedKey = key;
                                        _materialNewKeyId = key.Id;
                                    }
                                }
                            }
                        }
                        ImGui.SameLine();
                        if( ImGui.Button( "Add Key" ) )
                        {
                            ArrayAdd( ref file.ShaderPackage.ShaderKeys, new ShaderKey
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
                definedKeys[key.Category] = key.Value;
            }
            var materialKeys = Array.ConvertAll(file.AssociatedShpk.MaterialKeys, key =>
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
            var pixelShaders = new IndexSet( file.AssociatedShpk.PixelShaders.Length, false );
            foreach( var node in file.AssociatedShpk.Nodes )
            {
                if( node.MaterialKeys.WithIndex().All( key => key.Value == materialKeys[key.Index] ) )
                {
                    foreach( var pass in node.Passes )
                    {
                        vertexShaders.Add( ( int )pass.VertexShader );
                        pixelShaders.Add( ( int )pass.PixelShader );
                    }
                }
            }
            ImRaii.TreeNode( $"Vertex Shaders: {( vertexShaders.Count > 0 ? string.Join( ", ", vertexShaders.Select( i => $"#{i}" ) ) : "???" )}", ImGuiTreeNodeFlags.Leaf ).Dispose();
            ImRaii.TreeNode( $"Pixel Shaders: {( pixelShaders.Count > 0 ? string.Join( ", ", pixelShaders.Select( i => $"#{i}" ) ) : "???" )}", ImGuiTreeNodeFlags.Leaf ).Dispose();
        }

        if( file.ShaderPackage.Constants.Length > 0 || file.ShaderPackage.ShaderValues.Length > 0
                || !disabled && file.AssociatedShpk != null && file.AssociatedShpk.Constants.Length > 0 )
        {
            var materialParams = file.AssociatedShpk?.GetConstantById( ShpkFile.MaterialParamsConstantId );

            using var t = ImRaii.TreeNode( materialParams?.Name ?? "Constants" );
            if( t )
            {
                var orphanValues = new IndexSet( file.ShaderPackage.ShaderValues.Length, true );
                var aliasedValueCount = 0;
                var definedConstants = new HashSet< uint >();
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
                    var values = file.GetConstantValues( constant );
                    var paramValueOffset = -values.Length;
                    if( values.Length > 0 )
                    {
                        var shpkParam = file.AssociatedShpk?.GetMaterialParamById( constant.Id );
                        var paramByteOffset = shpkParam.HasValue ? shpkParam.Value.ByteOffset : -1;
                        if( ( paramByteOffset & 0x3 ) == 0 )
                        {
                            paramValueOffset = paramByteOffset >> 2;
                        }
                    }
                    var (constantName, componentOnly) = MaterialParamRangeName( materialParams?.Name ?? "", paramValueOffset, values.Length );

                    using var t2 = ImRaii.TreeNode( $"#{idx}{( constantName != null ? ( ": " + constantName ) : "" )} (ID: 0x{constant.Id:X8})" );
                    if( t2 )
                    {
                        if( values.Length > 0 )
                        {
                            var valueOffset = constant.ByteOffset >> 2;

                            for( var valueIdx = 0; valueIdx < values.Length; ++valueIdx )
                            {
                                ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                                if( ImGui.InputFloat( $"{MaterialParamName( componentOnly, paramValueOffset + valueIdx ) ?? $"#{valueIdx}"} (at 0x{( ( valueOffset + valueIdx ) << 2 ):X4})",
                                       ref values[valueIdx], 0.0f, 0.0f, "%.3f",
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

                        if( !disabled && !hasMalformedConstants && orphanValues.Count == 0 && aliasedValueCount == 0
                        && ImGui.Button( "Remove Constant" ) )
                        {
                            ArrayRemove( ref file.ShaderPackage.ShaderValues, constant.ByteOffset >> 2, constant.ByteSize >> 2 );
                            ArrayRemove( ref file.ShaderPackage.Constants, idx );
                            for( var i = 0; i < file.ShaderPackage.Constants.Length; ++i )
                            {
                                if( file.ShaderPackage.Constants[i].ByteOffset >= constant.ByteOffset )
                                {
                                    file.ShaderPackage.Constants[i].ByteOffset -= constant.ByteSize;
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
                            if( ImGui.InputFloat( $"#{idx} (at 0x{( idx << 2 ):X4})",
                                    ref file.ShaderPackage.ShaderValues[idx], 0.0f, 0.0f, "%.3f",
                                    disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                            {
                                ret = true;
                            }
                        }
                    }
                }
                else if ( !disabled && !hasMalformedConstants && file.AssociatedShpk != null )
                {
                    var missingConstants = file.AssociatedShpk.MaterialParams.Where( constant => ( constant.ByteOffset & 0x3 ) == 0 && ( constant.ByteSize & 0x3 ) == 0 && !definedConstants.Contains( constant.Id ) ).ToArray();
                    if( missingConstants.Length > 0 )
                    {
                        var selectedConstant = Array.Find( missingConstants, constant => constant.Id == _materialNewConstantId );
                        if( selectedConstant.ByteSize == 0 )
                        {
                            selectedConstant = missingConstants[0];
                            _materialNewConstantId = selectedConstant.Id;
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
                                    if( ImGui.Selectable( $"{constantName} (ID: 0x{constant.Id:X8})", constant.Id == _materialNewConstantId ) )
                                    {
                                        selectedConstant = constant;
                                        _materialNewConstantId = constant.Id;
                                    }
                                }
                            }
                        }
                        ImGui.SameLine();
                        if( ImGui.Button( "Add Constant" ) )
                        {
                            var valueOffset = ArrayAdd( ref file.ShaderPackage.ShaderValues, 0.0f, selectedConstant.ByteSize >> 2 );
                            ArrayAdd( ref file.ShaderPackage.Constants, new MtrlFile.Constant
                            {
                                Id         = _materialNewConstantId,
                                ByteOffset = ( ushort )( valueOffset << 2 ),
                                ByteSize   = selectedConstant.ByteSize,
                            } );
                            ret = true;
                        }
                    }
                }
            }
        }

        if( file.ShaderPackage.Samplers.Length > 0 || file.Textures.Length > 0
        || !disabled && file.AssociatedShpk != null && file.AssociatedShpk.Samplers.Any( sampler => sampler.Slot == 2 ) )
        {
            using var t = ImRaii.TreeNode( "Samplers" );
            if( t )
            {
                var orphanTextures = new IndexSet( file.Textures.Length, true );
                var aliasedTextureCount = 0;
                var definedSamplers = new HashSet< uint >();

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
                    var shpkSampler = file.AssociatedShpk?.GetSamplerById( sampler.SamplerId );
                    using var t2 = ImRaii.TreeNode( $"#{idx}{( shpkSampler.HasValue ? ( ": " + shpkSampler.Value.Name ) : "" )} (ID: 0x{sampler.SamplerId:X8})" );
                    if( t2 )
                    {
                        ImRaii.TreeNode( $"Texture: #{sampler.TextureIndex} - {Path.GetFileName( file.Textures[sampler.TextureIndex].Path )}", ImGuiTreeNodeFlags.Leaf ).Dispose();

                        // FIXME this probably doesn't belong here
                        static unsafe bool InputHexUInt16( string label, ref ushort v, ImGuiInputTextFlags flags )
                        {
                            fixed( ushort* v2 = &v )
                            {
                                return ImGui.InputScalar( label, ImGuiDataType.U16, new nint( v2 ), nint.Zero, nint.Zero, "%04X", flags );
                            }
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( InputHexUInt16( "Texture Flags", ref file.Textures[sampler.TextureIndex].Flags, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                        {
                            ret = true;
                        }
                        var sampFlags = ( int )sampler.Flags;
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( ImGui.InputInt( "Sampler Flags", ref sampFlags, 0, 0, ImGuiInputTextFlags.CharsHexadecimal | ( disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) ) )
                        {
                            file.ShaderPackage.Samplers[idx].Flags = ( uint )sampFlags;
                            ret = true;
                        }

                        if( !disabled && orphanTextures.Count == 0 && aliasedTextureCount == 0
                        && ImGui.Button( "Remove Sampler" ) )
                        {
                            ArrayRemove( ref file.Textures, sampler.TextureIndex );
                            ArrayRemove( ref file.ShaderPackage.Samplers, idx );
                            for( var i = 0; i < file.ShaderPackage.Samplers.Length; ++i )
                            {
                                if( file.ShaderPackage.Samplers[i].TextureIndex >= sampler.TextureIndex )
                                {
                                    --file.ShaderPackage.Samplers[i].TextureIndex;
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
                            ImRaii.TreeNode( $"#{idx}: {Path.GetFileName( file.Textures[idx].Path )} - {file.Textures[idx].Flags:X4}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                        }
                    }
                }
                else if( !disabled && file.AssociatedShpk != null && aliasedTextureCount == 0 && file.Textures.Length < 255 )
                {
                    var missingSamplers = file.AssociatedShpk.Samplers.Where( sampler => sampler.Slot == 2 && !definedSamplers.Contains( sampler.Id ) ).ToArray();
                    if( missingSamplers.Length > 0 )
                    {
                        var selectedSampler = Array.Find( missingSamplers, sampler => sampler.Id == _materialNewSamplerId );
                        if( selectedSampler.Name == null )
                        {
                            selectedSampler = missingSamplers[0];
                            _materialNewSamplerId = selectedSampler.Id;
                        }
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 450.0f );
                        using( var c = ImRaii.Combo( "##NewSamplerId", $"{selectedSampler.Name} (ID: 0x{selectedSampler.Id:X8})" ) )
                        {
                            if( c )
                            {
                                foreach( var sampler in missingSamplers )
                                {
                                    if( ImGui.Selectable( $"{sampler.Name} (ID: 0x{sampler.Id:X8})", sampler.Id == _materialNewSamplerId ) )
                                    {
                                        selectedSampler = sampler;
                                        _materialNewSamplerId = sampler.Id;
                                    }
                                }
                            }
                        }
                        ImGui.SameLine();
                        if( ImGui.Button( "Add Sampler" ) )
                        {
                            var texIndex = ArrayAdd( ref file.Textures, new MtrlFile.Texture
                            {
                                Path  = string.Empty,
                                Flags = 0,
                            } );
                            ArrayAdd( ref file.ShaderPackage.Samplers, new Sampler
                            {
                                SamplerId    = _materialNewSamplerId,
                                TextureIndex = ( byte )texIndex,
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

    private bool DrawOtherMaterialDetails( MtrlFile file, bool disabled )
    {
        var ret = false;

        if( !ImGui.CollapsingHeader( "Further Content" ) )
        {
            return false;
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

        if( file.AdditionalData.Length > 0 )
        {
            using var t = ImRaii.TreeNode( $"Additional Data (Size: {file.AdditionalData.Length})###AdditionalData" );
            if( t )
            {
                ImGuiUtil.TextWrapped( string.Join( ' ', file.AdditionalData.Select( c => $"{c:X2}" ) ) );
            }
        }

        return ret;
    }

    private static void ColorSetCopyAllClipboardButton( MtrlFile file, int colorSetIdx )
    {
        if( !ImGui.Button( "Export All Rows to Clipboard", ImGuiHelpers.ScaledVector2( 200, 0 ) ) )
        {
            return;
        }

        try
        {
            var data1 = file.ColorSets[ colorSetIdx ].Rows.AsBytes();
            var data2 = file.ColorDyeSets.Length > colorSetIdx ? file.ColorDyeSets[ colorSetIdx ].Rows.AsBytes() : ReadOnlySpan< byte >.Empty;
            var array = new byte[data1.Length + data2.Length];
            data1.TryCopyTo( array );
            data2.TryCopyTo( array.AsSpan( data1.Length ) );
            var text = Convert.ToBase64String( array );
            ImGui.SetClipboardText( text );
        }
        catch
        {
            // ignored
        }
    }

    private static bool DrawPreviewDye( MtrlFile file, bool disabled )
    {
        var (dyeId, (name, dyeColor, _)) = Penumbra.StainManager.StainCombo.CurrentSelection;
        var tt = dyeId == 0 ? "Select a preview dye first." : "Apply all preview values corresponding to the dye template and chosen dye where dyeing is enabled.";
        if( ImGuiUtil.DrawDisabledButton( "Apply Preview Dye", Vector2.Zero, tt, disabled || dyeId == 0 ) )
        {
            var ret = false;
            for( var j = 0; j < file.ColorDyeSets.Length; ++j )
            {
                for( var i = 0; i < MtrlFile.ColorSet.RowArray.NumRows; ++i )
                {
                    ret |= file.ApplyDyeTemplate( Penumbra.StainManager.StmFile, j, i, dyeId );
                }
            }

            return ret;
        }

        ImGui.SameLine();
        var label = dyeId == 0 ? "Preview Dye###previewDye" : $"{name} (Preview)###previewDye";
        Penumbra.StainManager.StainCombo.Draw( label, dyeColor, string.Empty, true );
        return false;
    }

    private static unsafe bool ColorSetPasteAllClipboardButton( MtrlFile file, int colorSetIdx )
    {
        if( !ImGui.Button( "Import All Rows from Clipboard", ImGuiHelpers.ScaledVector2( 200, 0 ) ) || file.ColorSets.Length <= colorSetIdx )
        {
            return false;
        }

        try
        {
            var text = ImGui.GetClipboardText();
            var data = Convert.FromBase64String( text );
            if( data.Length < Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() )
            {
                return false;
            }

            ref var rows = ref file.ColorSets[ colorSetIdx ].Rows;
            fixed( void* ptr = data, output = &rows )
            {
                MemoryUtility.MemCpyUnchecked( output, ptr, Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() );
                if( data.Length             >= Marshal.SizeOf< MtrlFile.ColorSet.RowArray >() + Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >()
                && file.ColorDyeSets.Length > colorSetIdx )
                {
                    ref var dyeRows = ref file.ColorDyeSets[ colorSetIdx ].Rows;
                    fixed( void* output2 = &dyeRows )
                    {
                        MemoryUtility.MemCpyUnchecked( output2, ( byte* )ptr + Marshal.SizeOf< MtrlFile.ColorSet.RowArray >(), Marshal.SizeOf< MtrlFile.ColorDyeSet.RowArray >() );
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
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
                    MemoryUtility.MemCpyUnchecked( ptr, &row, MtrlFile.ColorSet.Row.Size );
                    MemoryUtility.MemCpyUnchecked( ptr + MtrlFile.ColorSet.Row.Size, &dye, 2 );
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
                    if( colorSetIdx < file.ColorDyeSets.Length )
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
        static bool FixFloat( ref float val, float current )
        {
            val = ( float )( Half )val;
            return val != current;
        }

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
        using var dis = ImRaii.Disabled( disabled );
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
        if( ImGui.DragFloat( "##SpecularStrength", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.SpecularStrength ) )
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
        if( ImGui.DragFloat( "##GlossStrength", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.GlossStrength ) )
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
        if( ImGui.DragFloat( "##RepeatX", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialRepeat.X ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { X = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat X", ImGuiHoveredFlags.AllowWhenDisabled );
        ImGui.SameLine();
        tmpFloat = row.MaterialRepeat.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##RepeatY", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialRepeat.Y ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialRepeat = row.MaterialRepeat with { Y = tmpFloat };
            ret                                                         = true;
        }

        ImGuiUtil.HoverTooltip( "Repeat Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        tmpFloat = row.MaterialSkew.X;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewX", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialSkew.X ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { X = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew X", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.SameLine();
        tmpFloat = row.MaterialSkew.Y;
        ImGui.SetNextItemWidth( floatSize );
        if( ImGui.DragFloat( "##SkewY", ref tmpFloat, 0.1f, 0f ) && FixFloat( ref tmpFloat, row.MaterialSkew.Y ) )
        {
            file.ColorSets[ colorSetIdx ].Rows[ rowIdx ].MaterialSkew = row.MaterialSkew with { Y = tmpFloat };
            ret                                                       = true;
        }

        ImGuiUtil.HoverTooltip( "Skew Y", ImGuiHoveredFlags.AllowWhenDisabled );

        ImGui.TableNextColumn();
        if( hasDye )
        {
            if( Penumbra.StainManager.TemplateCombo.Draw( "##dyeTemplate", dye.Template.ToString(), string.Empty, intSize
                 + ImGui.GetStyle().ScrollbarSize / 2, ImGui.GetTextLineHeightWithSpacing(), ImGuiComboFlags.NoArrowButton ) )
            {
                file.ColorDyeSets[ colorSetIdx ].Rows[ rowIdx ].Template = Penumbra.StainManager.TemplateCombo.CurrentSelection;
                ret                                                      = true;
            }

            ImGuiUtil.HoverTooltip( "Dye Template", ImGuiHoveredFlags.AllowWhenDisabled );

            ImGui.TableNextColumn();
            ret |= DrawDyePreview( file, colorSetIdx, rowIdx, disabled, dye, floatSize );
        }
        else
        {
            ImGui.TableNextColumn();
        }


        return ret;
    }

    private static bool DrawDyePreview( MtrlFile file, int colorSetIdx, int rowIdx, bool disabled, MtrlFile.ColorDyeSet.Row dye, float floatSize )
    {
        var stain = Penumbra.StainManager.StainCombo.CurrentSelection.Key;
        if( stain == 0 || !Penumbra.StainManager.StmFile.Entries.TryGetValue( dye.Template, out var entry ) )
        {
            return false;
        }

        var       values = entry[ ( int )stain ];
        using var style  = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2 );

        var ret = ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.PaintBrush.ToIconString(), new Vector2( ImGui.GetFrameHeight() ),
            "Apply the selected dye to this row.", disabled, true );

        ret = ret && file.ApplyDyeTemplate( Penumbra.StainManager.StmFile, colorSetIdx, rowIdx, stain );

        ImGui.SameLine();
        ColorPicker( "##diffusePreview", string.Empty, values.Diffuse, _ => { }, "D" );
        ImGui.SameLine();
        ColorPicker( "##specularPreview", string.Empty, values.Specular, _ => { }, "S" );
        ImGui.SameLine();
        ColorPicker( "##emissivePreview", string.Empty, values.Emissive, _ => { }, "E" );
        ImGui.SameLine();
        using var dis = ImRaii.Disabled();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##gloss", ref values.Gloss, 0, 0, 0, "%.2f G" );
        ImGui.SameLine();
        ImGui.SetNextItemWidth( floatSize );
        ImGui.DragFloat( "##specularStrength", ref values.SpecularPower, 0, 0, 0, "%.2f S" );

        return ret;
    }

    private static bool ColorPicker( string label, string tooltip, Vector3 input, Action< Vector3 > setter, string letter = "" )
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

        if( letter.Length > 0 && ImGui.IsItemVisible() )
        {
            var textSize  = ImGui.CalcTextSize( letter );
            var center    = ImGui.GetItemRectMin() + ( ImGui.GetItemRectSize() - textSize ) / 2;
            var textColor = input.LengthSquared() < 0.25f ? 0x80FFFFFFu : 0x80000000u;
            ImGui.GetWindowDrawList().AddText( center, textColor, letter );
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

    // FIXME this probably doesn't belong here
    // Also used in ShaderPackages
    private static int ArrayAdd<T>( ref T[] array, T element, int count = 1 )
    {
        var length = array.Length;
        var newArray = new T[array.Length + count];
        Array.Copy( array, newArray, length );
        for( var i = 0; i < count; ++i )
        {
            newArray[length + i] = element;
        }
        array = newArray;
        return length;
    }

    private static void ArrayRemove<T>( ref T[] array, int offset, int count = 1 )
    {
        var newArray = new T[array.Length - count];
        Array.Copy( array, newArray, offset );
        Array.Copy( array, offset + count, newArray, offset, newArray.Length - offset );
        array = newArray;
    }

    private static (string?, bool) MaterialParamRangeName( string prefix, int valueOffset, int valueLength )
    {
        if( valueLength == 0 || valueOffset < 0 )
        {
            return (null, false);
        }

        var firstVector = valueOffset >> 2;
        var lastVector = ( valueOffset + valueLength - 1 ) >> 2;
        var firstComponent = valueOffset & 0x3;
        var lastComponent = ( valueOffset + valueLength - 1 ) & 0x3;

        static string VectorSwizzle( int firstComponent, int numComponents )
            => ( numComponents == 4 ) ? "" : string.Concat( ".", "xyzw".AsSpan( firstComponent, numComponents ) );

        if( firstVector == lastVector )
        {
            return ($"{prefix}[{firstVector}]{VectorSwizzle( firstComponent, lastComponent + 1 - firstComponent )}", true);
        }

        var parts = new string[lastVector + 1 - firstVector];
        parts[0] = $"{prefix}[{firstVector}]{VectorSwizzle( firstComponent, 4 - firstComponent )}";
        parts[^1] = $"[{lastVector}]{VectorSwizzle( 0, lastComponent + 1 )}";
        for( var i = firstVector + 1; i < lastVector; ++i )
        {
            parts[i - firstVector] = $"[{i}]";
        }

        return (string.Join( ", ", parts ), false);
    }

    private static string? MaterialParamName( bool componentOnly, int offset )
    {
        if( offset < 0 )
        {
            return null;
        }
        var component = "xyzw"[offset & 0x3];

        return componentOnly ? new string( component, 1 ) : $"[{offset >> 2}].{component}";
    }
}