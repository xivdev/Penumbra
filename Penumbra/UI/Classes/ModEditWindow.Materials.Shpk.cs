using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using ImGuiNET;
using Lumina.Data.Parsing;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData;
using Penumbra.GameData.Files;
using Penumbra.String.Classes;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private readonly FileDialogManager _materialFileDialog = ConfigWindow.SetupFileManager();

    private bool DrawPackageNameInput( MtrlTab tab, bool disabled )
    {
        var ret = false;
        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        if( ImGui.InputText( "Shader Package Name", ref tab.Mtrl.ShaderPackage.Name, 63, disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
        {
            ret                = true;
            tab.AssociatedShpk = null;
            tab.LoadedShpkPath = FullPath.Empty;
        }

        if( ImGui.IsItemDeactivatedAfterEdit() )
        {
            tab.LoadShpk( tab.FindAssociatedShpk( out _, out _ ) );
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

    /// <summary>
    /// Show the currently associated shpk file, if any, and the buttons to associate
    /// a specific shpk from your drive, the modded shpk by path or the default shpk.
    /// </summary>
    private void DrawCustomAssociations( MtrlTab tab )
    {
        var text = tab.AssociatedShpk == null
            ? "Associated .shpk file: None"
            : $"Associated .shpk file: {tab.LoadedShpkPathName}";

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );

        if( ImGui.Selectable( text ) )
        {
            ImGui.SetClipboardText( tab.LoadedShpkPathName );
        }

        ImGuiUtil.HoverTooltip( "Click to copy file path to clipboard." );

        if( ImGui.Button( "Associate Custom .shpk File" ) )
        {
            _materialFileDialog.OpenFileDialog( "Associate Custom .shpk File...", ".shpk", ( success, name ) =>
            {
                if( !success )
                {
                    return;
                }

                tab.LoadShpk( new FullPath( name ) );
            } );
        }

        var moddedPath = tab.FindAssociatedShpk( out var defaultPath, out var gamePath );
        ImGui.SameLine();
        if( ImGuiUtil.DrawDisabledButton( "Associate Default .shpk File", Vector2.Zero, moddedPath.ToPath(), moddedPath.Equals( tab.LoadedShpkPath ) ) )
        {
            tab.LoadShpk( moddedPath );
        }

        if( !gamePath.Path.Equals( moddedPath.InternalName ) )
        {
            ImGui.SameLine();
            if( ImGuiUtil.DrawDisabledButton( "Associate Unmodded .shpk File", Vector2.Zero, defaultPath, gamePath.Path.Equals( tab.LoadedShpkPath.InternalName ) ) )
            {
                tab.LoadShpk( new FullPath( gamePath ) );
            }
        }

        ImGui.Dummy( new Vector2( ImGui.GetTextLineHeight() / 2 ) );
    }


    private static bool DrawShaderKey( MtrlTab tab, bool disabled, ShaderKey key, int idx )
    {
        var       ret = false;
        using var t2  = ImRaii.TreeNode( tab.ShaderKeyLabels[ idx ], disabled ? ImGuiTreeNodeFlags.Leaf : 0 );
        if( !t2 || disabled )
        {
            return ret;
        }

        var shpkKey = tab.AssociatedShpk?.GetMaterialKeyById( key.Category );
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
                        tab.Mtrl.ShaderPackage.ShaderKeys[ idx ].Value = value;
                        ret                                            = true;
                    }
                }
            }
        }

        if( ImGui.Button( "Remove Key" ) )
        {
            tab.Mtrl.ShaderPackage.ShaderKeys = tab.Mtrl.ShaderPackage.ShaderKeys.RemoveItems( idx );
            ret                               = true;
        }

        return ret;
    }

    private static bool DrawNewShaderKey( MtrlTab tab )
    {
        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
        using( var c = ImRaii.Combo( "##NewConstantId", $"ID: 0x{tab.MaterialNewKeyId:X8}" ) )
        {
            if( c )
            {
                foreach( var idx in tab.MissingShaderKeyIndices )
                {
                    var key = tab.AssociatedShpk!.MaterialKeys[ idx ];

                    if( ImGui.Selectable( $"ID: 0x{key.Id:X8}", key.Id == tab.MaterialNewKeyId ) )
                    {
                        tab.MaterialNewKeyDefault = key.DefaultValue;
                        tab.MaterialNewKeyId      = key.Id;
                    }
                }
            }
        }

        ImGui.SameLine();
        if( ImGui.Button( "Add Key" ) )
        {
            tab.Mtrl.ShaderPackage.ShaderKeys = tab.Mtrl.ShaderPackage.ShaderKeys.AddItem( new ShaderKey
            {
                Category = tab.MaterialNewKeyId,
                Value    = tab.MaterialNewKeyDefault,
            } );
            return true;
        }

        return false;
    }

    private static bool DrawMaterialShaderKeys( MtrlTab tab, bool disabled )
    {
        if( tab.Mtrl.ShaderPackage.ShaderKeys.Length <= 0 && ( disabled || tab.AssociatedShpk == null || tab.AssociatedShpk.MaterialKeys.Length <= 0 ) )
        {
            return false;
        }

        using var t = ImRaii.TreeNode( "Shader Keys" );
        if( !t )
        {
            return false;
        }

        var ret = false;
        foreach( var (key, idx) in tab.Mtrl.ShaderPackage.ShaderKeys.WithIndex() )
        {
            ret |= DrawShaderKey( tab, disabled, key, idx );
        }

        if( !disabled && tab.AssociatedShpk != null && tab.MissingShaderKeyIndices.Count != 0 )
        {
            ret |= DrawNewShaderKey( tab );
        }

        if( ret )
        {
            tab.UpdateShaderKeyLabels();
        }

        return ret;
    }


    private static void DrawMaterialShaders( MtrlTab tab )
    {
        if( tab.AssociatedShpk == null )
        {
            return;
        }

        ImRaii.TreeNode( tab.VertexShaders, ImGuiTreeNodeFlags.Leaf ).Dispose();
        ImRaii.TreeNode( tab.PixelShaders, ImGuiTreeNodeFlags.Leaf ).Dispose();
    }

    private bool DrawMaterialConstants( MtrlTab tab, bool disabled )
    {
        var ret = false;
        if( tab.Mtrl.ShaderPackage.Constants.Length   <= 0
        && tab.Mtrl.ShaderPackage.ShaderValues.Length <= 0
        && ( disabled || tab.AssociatedShpk == null || tab.AssociatedShpk.Constants.Length <= 0 ) )
        {
            return ret;
        }

        var materialParams = tab.AssociatedShpk?.GetConstantById( ShpkFile.MaterialParamsConstantId );

        using var t = ImRaii.TreeNode( materialParams?.Name ?? "Constants" );
        if( t )
        {
            var orphanValues          = new IndexSet( tab.Mtrl.ShaderPackage.ShaderValues.Length, true );
            var aliasedValueCount     = 0;
            var definedConstants      = new HashSet< uint >();
            var hasMalformedConstants = false;

            foreach( var constant in tab.Mtrl.ShaderPackage.Constants )
            {
                definedConstants.Add( constant.Id );
                var values = tab.Mtrl.GetConstantValues( constant );
                if( tab.Mtrl.GetConstantValues( constant ).Length > 0 )
                {
                    var unique = orphanValues.RemoveRange( constant.ByteOffset >> 2, values.Length );
                    aliasedValueCount += values.Length - unique;
                }
                else
                {
                    hasMalformedConstants = true;
                }
            }

            foreach( var (constant, idx) in tab.Mtrl.ShaderPackage.Constants.WithIndex() )
            {
                var values           = tab.Mtrl.GetConstantValues( constant );
                var paramValueOffset = -values.Length;
                if( values.Length > 0 )
                {
                    var shpkParam       = tab.AssociatedShpk?.GetMaterialParamById( constant.Id );
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
                        tab.Mtrl.ShaderPackage.ShaderValues = tab.Mtrl.ShaderPackage.ShaderValues.RemoveItems( constant.ByteOffset >> 2, constant.ByteSize >> 2 );
                        tab.Mtrl.ShaderPackage.Constants    = tab.Mtrl.ShaderPackage.Constants.RemoveItems( idx );
                        for( var i = 0; i < tab.Mtrl.ShaderPackage.Constants.Length; ++i )
                        {
                            if( tab.Mtrl.ShaderPackage.Constants[ i ].ByteOffset >= constant.ByteOffset )
                            {
                                tab.Mtrl.ShaderPackage.Constants[ i ].ByteOffset -= constant.ByteSize;
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
                               ref tab.Mtrl.ShaderPackage.ShaderValues[ idx ], 0.0f, 0.0f, "%.3f",
                               disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                        {
                            ret = true;
                        }
                    }
                }
            }
            else if( !disabled && !hasMalformedConstants && tab.AssociatedShpk != null )
            {
                var missingConstants = tab.AssociatedShpk.MaterialParams.Where( constant
                    => ( constant.ByteOffset & 0x3 ) == 0 && ( constant.ByteSize & 0x3 ) == 0 && !definedConstants.Contains( constant.Id ) ).ToArray();
                if( missingConstants.Length > 0 )
                {
                    var selectedConstant = Array.Find( missingConstants, constant => constant.Id == tab.MaterialNewConstantId );
                    if( selectedConstant.ByteSize == 0 )
                    {
                        selectedConstant          = missingConstants[ 0 ];
                        tab.MaterialNewConstantId = selectedConstant.Id;
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
                                if( ImGui.Selectable( $"{constantName} (ID: 0x{constant.Id:X8})", constant.Id == tab.MaterialNewConstantId ) )
                                {
                                    selectedConstant          = constant;
                                    tab.MaterialNewConstantId = constant.Id;
                                }
                            }
                        }
                    }

                    ImGui.SameLine();
                    if( ImGui.Button( "Add Constant" ) )
                    {
                        tab.Mtrl.ShaderPackage.ShaderValues = tab.Mtrl.ShaderPackage.ShaderValues.AddItem( 0.0f, selectedConstant.ByteSize >> 2 );
                        tab.Mtrl.ShaderPackage.Constants = tab.Mtrl.ShaderPackage.Constants.AddItem( new MtrlFile.Constant
                        {
                            Id         = tab.MaterialNewConstantId,
                            ByteOffset = ( ushort )( tab.Mtrl.ShaderPackage.ShaderValues.Length << 2 ),
                            ByteSize   = selectedConstant.ByteSize,
                        } );
                        ret = true;
                    }
                }
            }
        }

        return ret;
    }

    private bool DrawMaterialSamplers( MtrlTab tab, bool disabled )
    {
        var ret = false;
        if( tab.Mtrl.ShaderPackage.Samplers.Length > 0
        || tab.Mtrl.Textures.Length                > 0
        || !disabled && tab.AssociatedShpk != null && tab.AssociatedShpk.Samplers.Any( sampler => sampler.Slot == 2 ) )
        {
            using var t = ImRaii.TreeNode( "Samplers" );
            if( t )
            {
                var orphanTextures      = new IndexSet( tab.Mtrl.Textures.Length, true );
                var aliasedTextureCount = 0;
                var definedSamplers     = new HashSet< uint >();

                foreach( var sampler in tab.Mtrl.ShaderPackage.Samplers )
                {
                    if( !orphanTextures.Remove( sampler.TextureIndex ) )
                    {
                        ++aliasedTextureCount;
                    }

                    definedSamplers.Add( sampler.SamplerId );
                }

                foreach( var (sampler, idx) in tab.Mtrl.ShaderPackage.Samplers.WithIndex() )
                {
                    var       shpkSampler = tab.AssociatedShpk?.GetSamplerById( sampler.SamplerId );
                    using var t2          = ImRaii.TreeNode( $"#{idx}{( shpkSampler.HasValue ? ": " + shpkSampler.Value.Name : "" )} (ID: 0x{sampler.SamplerId:X8})" );
                    if( t2 )
                    {
                        ImRaii.TreeNode( $"Texture: #{sampler.TextureIndex} - {Path.GetFileName( tab.Mtrl.Textures[ sampler.TextureIndex ].Path )}", ImGuiTreeNodeFlags.Leaf )
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
                        if( InputHexUInt16( "Texture Flags", ref tab.Mtrl.Textures[ sampler.TextureIndex ].Flags,
                               disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) )
                        {
                            ret = true;
                        }

                        var sampFlags = ( int )sampler.Flags;
                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 150.0f );
                        if( ImGui.InputInt( "Sampler Flags", ref sampFlags, 0, 0,
                               ImGuiInputTextFlags.CharsHexadecimal | ( disabled ? ImGuiInputTextFlags.ReadOnly : ImGuiInputTextFlags.None ) ) )
                        {
                            tab.Mtrl.ShaderPackage.Samplers[ idx ].Flags = ( uint )sampFlags;
                            ret                                          = true;
                        }

                        if( !disabled
                        && orphanTextures.Count == 0
                        && aliasedTextureCount  == 0
                        && ImGui.Button( "Remove Sampler" ) )
                        {
                            tab.Mtrl.Textures               = tab.Mtrl.Textures.RemoveItems( sampler.TextureIndex );
                            tab.Mtrl.ShaderPackage.Samplers = tab.Mtrl.ShaderPackage.Samplers.RemoveItems( idx );
                            for( var i = 0; i < tab.Mtrl.ShaderPackage.Samplers.Length; ++i )
                            {
                                if( tab.Mtrl.ShaderPackage.Samplers[ i ].TextureIndex >= sampler.TextureIndex )
                                {
                                    --tab.Mtrl.ShaderPackage.Samplers[ i ].TextureIndex;
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
                            ImRaii.TreeNode( $"#{idx}: {Path.GetFileName( tab.Mtrl.Textures[ idx ].Path )} - {tab.Mtrl.Textures[ idx ].Flags:X4}", ImGuiTreeNodeFlags.Leaf )
                               .Dispose();
                        }
                    }
                }
                else if( !disabled && tab.AssociatedShpk != null && aliasedTextureCount == 0 && tab.Mtrl.Textures.Length < 255 )
                {
                    var missingSamplers = tab.AssociatedShpk.Samplers.Where( sampler => sampler.Slot == 2 && !definedSamplers.Contains( sampler.Id ) ).ToArray();
                    if( missingSamplers.Length > 0 )
                    {
                        var selectedSampler = Array.Find( missingSamplers, sampler => sampler.Id == tab.MaterialNewSamplerId );
                        if( selectedSampler.Name == null )
                        {
                            selectedSampler          = missingSamplers[ 0 ];
                            tab.MaterialNewSamplerId = selectedSampler.Id;
                        }

                        ImGui.SetNextItemWidth( ImGuiHelpers.GlobalScale * 450.0f );
                        using( var c = ImRaii.Combo( "##NewSamplerId", $"{selectedSampler.Name} (ID: 0x{selectedSampler.Id:X8})" ) )
                        {
                            if( c )
                            {
                                foreach( var sampler in missingSamplers )
                                {
                                    if( ImGui.Selectable( $"{sampler.Name} (ID: 0x{sampler.Id:X8})", sampler.Id == tab.MaterialNewSamplerId ) )
                                    {
                                        selectedSampler          = sampler;
                                        tab.MaterialNewSamplerId = sampler.Id;
                                    }
                                }
                            }
                        }

                        ImGui.SameLine();
                        if( ImGui.Button( "Add Sampler" ) )
                        {
                            tab.Mtrl.Textures = tab.Mtrl.Textures.AddItem( new MtrlFile.Texture
                            {
                                Path  = string.Empty,
                                Flags = 0,
                            } );
                            tab.Mtrl.ShaderPackage.Samplers = tab.Mtrl.ShaderPackage.Samplers.AddItem( new Sampler
                            {
                                SamplerId    = tab.MaterialNewSamplerId,
                                TextureIndex = ( byte )tab.Mtrl.Textures.Length,
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

    private bool DrawMaterialShaderResources( MtrlTab tab, bool disabled )
    {
        var ret = false;
        if( !ImGui.CollapsingHeader( "Advanced Shader Resources" ) )
        {
            return ret;
        }

        ret |= DrawPackageNameInput( tab, disabled );
        ret |= DrawShaderFlagsInput( tab.Mtrl, disabled );
        DrawCustomAssociations( tab );
        ret |= DrawMaterialShaderKeys( tab, disabled );
        DrawMaterialShaders( tab );
        ret |= DrawMaterialConstants( tab, disabled );
        ret |= DrawMaterialSamplers( tab, disabled );
        return ret;
    }


    private static (string?, bool) MaterialParamRangeName( string prefix, int valueOffset, int valueLength )
    {
        if( valueLength == 0 || valueOffset < 0 )
        {
            return ( null, false );
        }

        var firstVector    = valueOffset                       >> 2;
        var lastVector     = ( valueOffset + valueLength - 1 ) >> 2;
        var firstComponent = valueOffset                       & 0x3;
        var lastComponent  = ( valueOffset + valueLength - 1 ) & 0x3;

        static string VectorSwizzle( int firstComponent, int numComponents )
            => numComponents == 4 ? "" : string.Concat( ".", "xyzw".AsSpan( firstComponent, numComponents ) );

        if( firstVector == lastVector )
        {
            return ( $"{prefix}[{firstVector}]{VectorSwizzle( firstComponent, lastComponent + 1 - firstComponent )}", true );
        }

        var parts = new string[lastVector + 1 - firstVector];
        parts[ 0 ]  = $"{prefix}[{firstVector}]{VectorSwizzle( firstComponent, 4 - firstComponent )}";
        parts[ ^1 ] = $"[{lastVector}]{VectorSwizzle( 0, lastComponent           + 1 )}";
        for( var i = firstVector + 1; i < lastVector; ++i )
        {
            parts[ i - firstVector ] = $"[{i}]";
        }

        return ( string.Join( ", ", parts ), false );
    }

    private static string? MaterialParamName( bool componentOnly, int offset )
    {
        if( offset < 0 )
        {
            return null;
        }

        var component = "xyzw"[ offset & 0x3 ];

        return componentOnly ? new string( component, 1 ) : $"[{offset >> 2}].{component}";
    }
}