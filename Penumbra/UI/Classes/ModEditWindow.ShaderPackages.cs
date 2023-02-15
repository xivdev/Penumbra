using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui;
using Penumbra.GameData.Data;
using Penumbra.GameData.Files;
using Penumbra.Util;
using Lumina.Data.Parsing;
using static OtterGui.Raii.ImRaii;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileEditor<ShpkFile> _shaderPackageTab;

    private readonly FileDialogManager _shaderPackageFileDialog = ConfigWindow.SetupFileManager();

    private uint _shaderPackageNewMaterialParamId = 0;
    private ushort _shaderPackageNewMaterialParamStart = 0;
    private ushort _shaderPackageNewMaterialParamEnd = 0;

    private bool DrawShaderPackagePanel( ShpkFile file, bool disabled )
    {
        var ret = DrawShaderPackageSummary( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawShaderPackageShaderArray( "Vertex Shader", file.VertexShaders, file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawShaderPackageShaderArray( "Pixel Shader", file.PixelShaders, file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawShaderPackageMaterialParamLayout( file, disabled );

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
        ret |= DrawOtherShaderPackageDetails( file, disabled );

        _shaderPackageFileDialog.Draw();

        ret |= file.IsChanged();

        return !disabled && ret;
    }

    private static bool DrawShaderPackageSummary( ShpkFile file, bool _ )
    {
        ImGui.Text( $"Shader Package for DirectX {( int )file.DirectXVersion}" );

        return false;
    }

    private bool DrawShaderPackageShaderArray( string objectName, ShpkFile.Shader[] shaders, ShpkFile file, bool disabled )
    {
        if( shaders.Length == 0 )
        {
            return false;
        }

        if( !ImGui.CollapsingHeader( $"{objectName}s" ) )
        {
            return false;
        }

        var ret = false;

        foreach( var (shader, idx) in shaders.WithIndex() )
        {
            using var t = ImRaii.TreeNode( $"{objectName} #{idx}" );
            if( t )
            {
                if( ImGui.Button( $"Export Shader Blob ({shader.Blob.Length} bytes)" ) )
                {
                    var extension = file.DirectXVersion switch
                    {
                        ShpkFile.DXVersion.DirectX9  => ".cso",
                        ShpkFile.DXVersion.DirectX11 => ".dxbc",
                        _                            => throw new NotImplementedException(),
                    };
                    var defaultName = new string( objectName.Where( char.IsUpper ).ToArray() ).ToLower() + idx.ToString();
                    var blob = shader.Blob;
                    _shaderPackageFileDialog.SaveFileDialog( $"Export {objectName} #{idx} Blob to...", extension, defaultName, extension, ( success, name ) =>
                    {
                        if( !success )
                        {
                            return;
                        }

                        try
                        {
                            File.WriteAllBytes( name, blob );
                        }
                        catch( Exception e )
                        {
                            Penumbra.Log.Error( $"Could not export {defaultName}{extension} to {name}:\n{e}" );
                            ChatUtil.NotificationMessage( $"Could not export {defaultName}{extension} to {Path.GetFileName( name )}:\n{e.Message}", "Penumbra Advanced Editing", NotificationType.Error );
                            return;
                        }
                        ChatUtil.NotificationMessage( $"Shader Blob {defaultName}{extension} exported successfully to {Path.GetFileName( name )}", "Penumbra Advanced Editing", NotificationType.Success );
                    } );
                }
                if( !disabled )
                {
                    ImGui.SameLine();
                    if( ImGui.Button( "Replace Shader Blob" ) )
                    {
                        _shaderPackageFileDialog.OpenFileDialog( $"Replace {objectName} #{idx} Blob...", "Shader Blobs{.o,.cso,.dxbc,.dxil}", ( success, name ) =>
                        {
                            if( !success )
                            {
                                return;
                            }

                            try
                            {
                                shaders[idx].Blob = File.ReadAllBytes( name );
                            }
                            catch( Exception e )
                            {
                                Penumbra.Log.Error( $"Could not import Shader Blob {name}:\n{e}" );
                                ChatUtil.NotificationMessage( $"Could not import {Path.GetFileName( name )}:\n{e.Message}", "Penumbra Advanced Editing", NotificationType.Error );
                                return;
                            }
                            try
                            {
                                shaders[idx].UpdateResources( file );
                                file.UpdateResources();
                            }
                            catch( Exception e )
                            {
                                file.SetInvalid();
                                Penumbra.Log.Error( $"Failed to update resources after importing Shader Blob {name}:\n{e}" );
                                ChatUtil.NotificationMessage( $"Failed to update resources after importing {Path.GetFileName( name )}:\n{e.Message}", "Penumbra Advanced Editing", NotificationType.Error );
                                return;
                            }
                            file.SetChanged();
                            ChatUtil.NotificationMessage( $"Shader Blob {Path.GetFileName( name )} imported successfully", "Penumbra Advanced Editing", NotificationType.Success );
                        } );
                    }
                }

                ret |= DrawShaderPackageResourceArray( "Constant Buffers", "slot", true, shader.Constants, disabled );
                ret |= DrawShaderPackageResourceArray( "Samplers", "slot", false, shader.Samplers, disabled );
                ret |= DrawShaderPackageResourceArray( "Unknown Type X Resources", "slot", true, shader.UnknownX, disabled );
                ret |= DrawShaderPackageResourceArray( "Unknown Type Y Resources", "slot", true, shader.UnknownY, disabled );

                if( shader.AdditionalHeader.Length > 0 )
                {
                    using var t2 = ImRaii.TreeNode( $"Additional Header (Size: {shader.AdditionalHeader.Length})###AdditionalHeader" );
                    if( t2 )
                    {
                        ImGuiUtil.TextWrapped( string.Join( ' ', shader.AdditionalHeader.Select( c => $"{c:X2}" ) ) );
                    }
                }

                using( var t2 = ImRaii.TreeNode( "Raw Disassembly" ) )
                {
                    if( t2 )
                    {
                        using( var font = ImRaii.PushFont( UiBuilder.MonoFont ) )
                        {
                            ImGui.TextUnformatted( shader.Disassembly!.RawDisassembly );
                        }
                    }
                }
            }
        }

        return ret;
    }

    private bool DrawShaderPackageMaterialParamLayout( ShpkFile file, bool disabled )
    {
        var ret = false;

        var materialParams = file.GetConstantById( ShpkFile.MaterialParamsConstantId );

        if( !ImGui.CollapsingHeader( $"{materialParams?.Name ?? "Material Parameter"} Layout" ) )
        {
            return false;
        }

        var isSizeWellDefined = ( file.MaterialParamsSize & 0xF ) == 0 && ( !materialParams.HasValue || file.MaterialParamsSize == ( materialParams.Value.Size << 4 ) );

        if( !isSizeWellDefined )
        {
            if( materialParams.HasValue )
            {
                ImGui.Text( $"Buffer size mismatch: {file.MaterialParamsSize} bytes ≠ {materialParams.Value.Size} registers ({materialParams.Value.Size << 4} bytes)" );
            }
            else
            {
                ImGui.Text( $"Buffer size mismatch: {file.MaterialParamsSize} bytes, not a multiple of 16" );
            }
        }

        var parameters = new (uint, bool)?[( ( file.MaterialParamsSize + 0xFu ) & ~0xFu) >> 2];
        var orphanParameters = new IndexSet( parameters.Length, true );
        var definedParameters = new HashSet< uint >();
        var hasMalformedParameters = false;

        foreach( var param in file.MaterialParams )
        {
            definedParameters.Add( param.Id );
            if( ( param.ByteOffset & 0x3 ) == 0 && ( param.ByteSize & 0x3 ) == 0
            && ( param.ByteOffset + param.ByteSize ) <= file.MaterialParamsSize )
            {
                var valueOffset = param.ByteOffset >> 2;
                var valueCount  = param.ByteSize >> 2;
                orphanParameters.RemoveRange( valueOffset, valueCount );

                parameters[valueOffset] = (param.Id, true);

                for( var i = 1; i < valueCount; ++i )
                {
                    parameters[valueOffset + i] = (param.Id, false);
                }
            }
            else
            {
                hasMalformedParameters = true;
            }
        }

        ImGui.Text( "Parameter positions (continuations are grayed out, unused values are red):" );

        using( var table = ImRaii.Table( "##MaterialParamLayout", 5,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg ) )
        {
            if( table )
            {
                ImGui.TableNextColumn();
                ImGui.TableHeader( string.Empty );
                ImGui.TableNextColumn();
                ImGui.TableHeader( "x" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( "y" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( "z" );
                ImGui.TableNextColumn();
                ImGui.TableHeader( "w" );

                var textColorStart = ImGui.GetColorU32( ImGuiCol.Text );
                var textColorCont = ( textColorStart & 0xFFFFFFu ) | ( ( textColorStart & 0xFE000000u ) >> 1 ); // Half opacity
                var textColorUnusedStart = ( textColorStart & 0xFF000000u ) | ( ( textColorStart & 0xFEFEFE ) >> 1 ) | 0x80u; // Half red
                var textColorUnusedCont = ( textColorUnusedStart & 0xFFFFFFu ) | ( ( textColorUnusedStart & 0xFE000000u ) >> 1 );

                for( var idx = 0; idx < parameters.Length; idx += 4 )
                {
                    var usedComponents = materialParams?.Used?[idx >> 2] ?? DisassembledShader.VectorComponents.All;
                    ImGui.TableNextColumn();
                    ImGui.Text( $"[{idx >> 2}]" );
                    for( var col = 0; col < 4; ++col )
                    {
                        var cell = parameters[idx + col];
                        ImGui.TableNextColumn();
                        var start = cell.HasValue && cell.Value.Item2;
                        var used = ( ( byte )usedComponents & ( 1 << col ) ) != 0;
                        using var c = ImRaii.PushColor( ImGuiCol.Text, used ? ( start ? textColorStart : textColorCont ) : ( start ? textColorUnusedStart : textColorUnusedCont ) );
                        ImGui.Text( cell.HasValue ? $"0x{cell.Value.Item1:X8}" : "(none)" );
                    }
                    ImGui.TableNextRow();
                }
            }
        }

        if( hasMalformedParameters )
        {
            using var t = ImRaii.TreeNode( "Misaligned / Overflowing Parameters" );
            if( t )
            {
                foreach( var param in file.MaterialParams )
                {
                    if( ( param.ByteOffset & 0x3 ) != 0 || ( param.ByteSize & 0x3 ) != 0 )
                    {
                        ImRaii.TreeNode( $"ID: 0x{param.Id:X8}, offset: 0x{param.ByteOffset:X4}, size: 0x{param.ByteSize:X4}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                    }
                    else if( ( param.ByteOffset + param.ByteSize ) > file.MaterialParamsSize )
                    {
                        ImRaii.TreeNode( $"{MaterialParamRangeName( materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2 )} (ID: 0x{param.Id:X8})", ImGuiTreeNodeFlags.Leaf ).Dispose();
                    }
                }
            }
        }
        else if( !disabled && isSizeWellDefined )
        {
            using var t = ImRaii.TreeNode( "Add / Remove Parameters" );
            if( t )
            {
                for( var i = 0; i < file.MaterialParams.Length; ++i )
                {
                    var param = file.MaterialParams[i];
                    using var t2 = ImRaii.TreeNode( $"{MaterialParamRangeName(materialParams?.Name ?? string.Empty, param.ByteOffset >> 2, param.ByteSize >> 2).Item1} (ID: 0x{param.Id:X8})" );
                    if( t2 )
                    {
                        if( ImGui.Button( "Remove" ) )
                        {
                            ArrayRemove( ref file.MaterialParams, i );
                            ret = true;
                        }
                    }
                }
                if( orphanParameters.Count > 0 )
                {
                    using var t2 = ImRaii.TreeNode( "New Parameter" );
                    if( t2 )
                    {
                        var starts = orphanParameters.ToArray();
                        if( !orphanParameters[_shaderPackageNewMaterialParamStart] )
                        {
                            _shaderPackageNewMaterialParamStart = ( ushort )starts[0];
                        }
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 225.0f );
                        var startName = MaterialParamName( false, _shaderPackageNewMaterialParamStart )!;
                        using( var c = ImRaii.Combo( "Start", $"{materialParams?.Name ?? ""}{startName}" ) )
                        {
                            if( c )
                            {
                                foreach( var start in starts )
                                {
                                    var name = MaterialParamName( false, start )!;
                                    if( ImGui.Selectable( $"{materialParams?.Name ?? ""}{name}" ) )
                                    {
                                        _shaderPackageNewMaterialParamStart = ( ushort )start;
                                    }
                                }
                            }
                        }
                        var lastEndCandidate = ( int )_shaderPackageNewMaterialParamStart;
                        var ends = starts.SkipWhile( i => i < _shaderPackageNewMaterialParamStart ).TakeWhile( i => {
                            var ret = i <= lastEndCandidate + 1;
                            lastEndCandidate = i;
                            return ret;
                        } ).ToArray();
                        if( Array.IndexOf(ends, _shaderPackageNewMaterialParamEnd) < 0 )
                        {
                            _shaderPackageNewMaterialParamEnd = ( ushort )ends[0];
                        }
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 225.0f );
                        var endName = MaterialParamName( false, _shaderPackageNewMaterialParamEnd )!;
                        using( var c = ImRaii.Combo( "End", $"{materialParams?.Name ?? ""}{endName}" ) )
                        {
                            if( c )
                            {
                                foreach( var end in ends )
                                {
                                    var name = MaterialParamName( false, end )!;
                                    if( ImGui.Selectable( $"{materialParams?.Name ?? ""}{name}" ) )
                                    {
                                        _shaderPackageNewMaterialParamEnd = ( ushort )end;
                                    }
                                }
                            }
                        }
                        var id = ( int )_shaderPackageNewMaterialParamId;
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( ImGui.InputInt( "ID", ref id, 0, 0, ImGuiInputTextFlags.CharsHexadecimal ) )
                        {
                            _shaderPackageNewMaterialParamId = ( uint )id;
                        }
                        if( ImGui.Button( "Add" ) )
                        {
                            if( definedParameters.Contains( _shaderPackageNewMaterialParamId ) )
                            {
                                ChatUtil.NotificationMessage( $"Duplicate parameter ID 0x{_shaderPackageNewMaterialParamId:X8}", "Penumbra Advanced Editing", NotificationType.Error );
                            }
                            else
                            {
                                ArrayAdd( ref file.MaterialParams, new ShpkFile.MaterialParam
                                {
                                    Id = _shaderPackageNewMaterialParamId,
                                    ByteOffset = ( ushort )( _shaderPackageNewMaterialParamStart << 2 ),
                                    ByteSize = ( ushort )( ( _shaderPackageNewMaterialParamEnd + 1 - _shaderPackageNewMaterialParamStart ) << 2 ),
                                } );
                                ret = true;
                            }
                        }
                    }
                }
            }
        }

        return ret;
    }

    private static bool DrawShaderPackageResourceArray( string arrayName, string slotLabel, bool withSize, ShpkFile.Resource[] resources, bool _ )
    {
        if( resources.Length == 0 )
        {
            return false;
        }

        using var t = ImRaii.TreeNode( arrayName );
        if( !t )
        {
            return false;
        }

        var ret = false;

        foreach( var (buf, idx) in resources.WithIndex() )
        {
            using var t2 = ImRaii.TreeNode( $"#{idx}: {buf.Name} (ID: 0x{buf.Id:X8}), {slotLabel}: {buf.Slot}" + ( withSize ? $", size: {buf.Size} registers" : string.Empty ), ( buf.Used != null ) ? 0 : ImGuiTreeNodeFlags.Leaf );
            if( t2 )
            {
                var used = new List< string >();
                if( withSize )
                {
                    foreach( var (components, i) in ( buf.Used ?? Array.Empty<DisassembledShader.VectorComponents>() ).WithIndex() )
                    {
                        switch( components )
                        {
                            case 0:
                                break;
                            case DisassembledShader.VectorComponents.All:
                                used.Add( $"[{i}]" );
                                break;
                            default:
                                used.Add( $"[{i}].{new string( components.ToString().Where( char.IsUpper ).ToArray() ).ToLower()}" );
                                break;
                        }
                    }
                    switch( buf.UsedDynamically ?? 0 )
                    {
                        case 0:
                            break;
                        case DisassembledShader.VectorComponents.All:
                            used.Add( "[*]" );
                            break;
                        default:
                            used.Add( $"[*].{new string( buf.UsedDynamically!.Value.ToString().Where( char.IsUpper ).ToArray() ).ToLower()}" );
                            break;
                    }
                }
                else
                {
                    var components = ( ( buf.Used != null && buf.Used.Length > 0 ) ? buf.Used[0] : 0 ) | (buf.UsedDynamically ?? 0);
                    if( ( components & DisassembledShader.VectorComponents.X ) != 0 )
                    {
                        used.Add( "Red" );
                    }
                    if( ( components & DisassembledShader.VectorComponents.Y ) != 0 )
                    {
                        used.Add( "Green" );
                    }
                    if( ( components & DisassembledShader.VectorComponents.Z ) != 0 )
                    {
                        used.Add( "Blue" );
                    }
                    if( ( components & DisassembledShader.VectorComponents.W ) != 0 )
                    {
                        used.Add( "Alpha" );
                    }
                }
                if( used.Count > 0 )
                {
                    ImRaii.TreeNode( $"Used: {string.Join(", ", used)}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
                else
                {
                    ImRaii.TreeNode( "Unused", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        return ret;
    }

    private static bool DrawOtherShaderPackageDetails( ShpkFile file, bool disabled )
    {
        var ret = false;

        if( !ImGui.CollapsingHeader( "Further Content" ) )
        {
            return false;
        }

        ret |= DrawShaderPackageResourceArray( "Constant Buffers", "type", true, file.Constants, disabled );
        ret |= DrawShaderPackageResourceArray( "Samplers", "type", false, file.Samplers, disabled );

        if( file.UnknownA.Length > 0 )
        {
            using var t = ImRaii.TreeNode( $"Unknown Type A Structures ({file.UnknownA.Length})" );
            if( t )
            {
                foreach( var (unk, idx) in file.UnknownA.WithIndex() )
                {
                    ImRaii.TreeNode( $"#{idx}: 0x{unk.Item1:X8}, 0x{unk.Item2:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        if( file.UnknownB.Length > 0 )
        {
            using var t = ImRaii.TreeNode( $"Unknown Type B Structures ({file.UnknownB.Length})" );
            if( t )
            {
                foreach( var (unk, idx) in file.UnknownB.WithIndex() )
                {
                    ImRaii.TreeNode( $"#{idx}: 0x{unk.Item1:X8}, 0x{unk.Item2:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        if( file.UnknownC.Length > 0 )
        {
            using var t = ImRaii.TreeNode( $"Unknown Type C Structures ({file.UnknownC.Length})" );
            if( t )
            {
                foreach( var (unk, idx) in file.UnknownC.WithIndex() )
                {
                    ImRaii.TreeNode( $"#{idx}: 0x{unk.Item1:X8}, 0x{unk.Item2:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                }
            }
        }

        using( var t = ImRaii.TreeNode( $"Misc. Unknown Fields" ) )
        {
            if( t )
            {
                ImRaii.TreeNode( $"#1 (at 0x0004): 0x{file.Unknown1:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                ImRaii.TreeNode( $"#2 (at 0x003C): 0x{file.Unknown2:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                ImRaii.TreeNode( $"#3 (at 0x0040): 0x{file.Unknown3:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
                ImRaii.TreeNode( $"#4 (at 0x0044): 0x{file.Unknown4:X8}", ImGuiTreeNodeFlags.Leaf ).Dispose();
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

        using( var t = ImRaii.TreeNode( $"String Pool" ) )
        {
            if( t )
            {
                foreach( var offset in file.Strings.StartingOffsets )
                {
                    ImGui.Text( file.Strings.GetNullTerminatedString( offset ) );
                }
            }
        }

        return ret;
    }
}