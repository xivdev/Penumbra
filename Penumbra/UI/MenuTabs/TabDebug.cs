using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Api;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.UI.Custom;
using CharacterUtility = Penumbra.Interop.CharacterUtility;
using ResourceHandle = Penumbra.Interop.Structs.ResourceHandle;

namespace Penumbra.UI;

public partial class SettingsInterface
{
    private static void PrintValue( string name, string value )
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Text( name );
        ImGui.TableNextColumn();
        ImGui.Text( value );
    }

    private void DrawDebugTabGeneral()
    {
        if( !ImGui.CollapsingHeader( "General##Debug" ) )
        {
            return;
        }

        if( !ImGui.BeginTable( "##DebugGeneralTable", 2, ImGuiTableFlags.SizingFixedFit,
               new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 1 ) ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTable );

        var manager = Penumbra.ModManager;
        PrintValue( "Current Collection", manager.Collections.CurrentCollection.Name );
        PrintValue( "    has Cache", ( manager.Collections.CurrentCollection.Cache != null ).ToString() );
        PrintValue( "Default Collection", manager.Collections.DefaultCollection.Name );
        PrintValue( "    has Cache", ( manager.Collections.DefaultCollection.Cache != null ).ToString() );
        PrintValue( "Forced Collection", manager.Collections.ForcedCollection.Name );
        PrintValue( "    has Cache", ( manager.Collections.ForcedCollection.Cache != null ).ToString() );
        PrintValue( "Mod Manager BasePath", manager.BasePath?.Name          ?? "NULL" );
        PrintValue( "Mod Manager BasePath-Full", manager.BasePath?.FullName ?? "NULL" );
        PrintValue( "Mod Manager BasePath IsRooted", Path.IsPathRooted( Penumbra.Config.ModDirectory ).ToString() );
        PrintValue( "Mod Manager BasePath Exists",
            manager.BasePath != null ? Directory.Exists( manager.BasePath.FullName ).ToString() : false.ToString() );
        PrintValue( "Mod Manager Valid", manager.Valid.ToString() );
        //PrintValue( "Resource Loader Enabled", _penumbra.ResourceLoader.IsEnabled.ToString() );
    }

    private unsafe void DrawDebugTabRedraw()
    {
        if( !ImGui.CollapsingHeader( "Redrawing##Debug" ) )
        {
            return;
        }

        var queue = ( Queue< (int, string, RedrawType) >? )_penumbra.ObjectReloader.GetType()
               .GetField( "_objectIds", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _penumbra.ObjectReloader )
         ?? new Queue< (int, string, RedrawType) >();

        var currentFrame = ( int? )_penumbra.ObjectReloader.GetType()
           .GetField( "_currentFrame", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var changedSettings = ( bool? )_penumbra.ObjectReloader.GetType()
           .GetField( "_changedSettings", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var currentObjectId = ( uint? )_penumbra.ObjectReloader.GetType()
           .GetField( "_currentObjectId", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var currentObjectName = ( string? )_penumbra.ObjectReloader.GetType()
           .GetField( "_currentObjectName", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var currentObjectStartState = ( DrawState? )_penumbra.ObjectReloader.GetType()
           .GetField( "_currentObjectStartState", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var currentRedrawType = ( RedrawType? )_penumbra.ObjectReloader.GetType()
           .GetField( "_currentRedrawType", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var (currentObject, currentObjectIdx) = ( (GameObject?, int) )_penumbra.ObjectReloader.GetType()
           .GetMethod( "FindCurrentObject", BindingFlags.NonPublic | BindingFlags.Instance )?
           .Invoke( _penumbra.ObjectReloader, Array.Empty< object >() )!;

        var currentRender = currentObject != null
            ? ObjectReloader.ActorDrawState( currentObject )
            : null;

        var waitFrames = ( int? )_penumbra.ObjectReloader.GetType()
           .GetField( "_waitFrames", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var wasTarget = ( bool? )_penumbra.ObjectReloader.GetType()
           .GetField( "_wasTarget", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        var gPose = ( bool? )_penumbra.ObjectReloader.GetType()
           .GetField( "_inGPose", BindingFlags.Instance | BindingFlags.NonPublic )
          ?.GetValue( _penumbra.ObjectReloader );

        using var raii = new ImGuiRaii.EndStack();
        if( ImGui.BeginTable( "##RedrawData", 2, ImGuiTableFlags.SizingFixedFit,
               new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 7 ) ) )
        {
            raii.Push( ImGui.EndTable );
            PrintValue( "Current Wait Frame", waitFrames?.ToString()                                        ?? "null" );
            PrintValue( "Current Frame", currentFrame?.ToString()                                           ?? "null" );
            PrintValue( "Currently in GPose", gPose?.ToString()                                             ?? "null" );
            PrintValue( "Current Changed Settings", changedSettings?.ToString()                             ?? "null" );
            PrintValue( "Current Object Id", currentObjectId?.ToString( "X8" )                              ?? "null" );
            PrintValue( "Current Object Name", currentObjectName                                            ?? "null" );
            PrintValue( "Current Object Start State", ( ( int? )currentObjectStartState )?.ToString( "X8" ) ?? "null" );
            PrintValue( "Current Object Was Target", wasTarget?.ToString()                                  ?? "null" );
            PrintValue( "Current Object Redraw", currentRedrawType?.ToString()                              ?? "null" );
            PrintValue( "Current Object Address", currentObject?.Address.ToString( "X16" )                  ?? "null" );
            PrintValue( "Current Object Index", currentObjectIdx >= 0 ? currentObjectIdx.ToString() : "null" );
            PrintValue( "Current Object Render Flags", ( ( int? )currentRender )?.ToString( "X8" ) ?? "null" );
        }

        if( queue.Any()
        && ImGui.BeginTable( "##RedrawTable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX,
               new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * queue.Count ) ) )
        {
            raii.Push( ImGui.EndTable );
            foreach( var (objectId, objectName, redraw) in queue )
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text( objectName );
                ImGui.TableNextColumn();
                ImGui.Text( $"0x{objectId:X8}" );
                ImGui.TableNextColumn();
                ImGui.Text( redraw.ToString() );
            }
        }

        if( queue.Any() && ImGui.Button( "Clear" ) )
        {
            queue.Clear();
            _penumbra.ObjectReloader.GetType()
               .GetField( "_currentFrame", BindingFlags.Instance | BindingFlags.NonPublic )?.SetValue( _penumbra.ObjectReloader, 0 );
        }
    }

    private void DrawDebugTabIpc()
    {
        if( !ImGui.CollapsingHeader( "IPC##Debug" ) )
        {
            return;
        }

        var ipc = _penumbra.Ipc;
        ImGui.Text( $"API Version: {ipc.Api.ApiVersion}" );
        ImGui.Text( "Available subscriptions:" );
        using var indent = ImGuiRaii.PushIndent();
        if( ipc.ProviderApiVersion != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderApiVersion );
        }

        if( ipc.ProviderRedrawName != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderRedrawName );
        }

        if( ipc.ProviderRedrawObject != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderRedrawObject );
        }

        if( ipc.ProviderRedrawAll != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderRedrawAll );
        }

        if( ipc.ProviderResolveDefault != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderResolveDefault );
        }

        if( ipc.ProviderResolveCharacter != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderResolveCharacter );
        }

        if( ipc.ProviderChangedItemTooltip != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderChangedItemTooltip );
        }

        if( ipc.ProviderChangedItemClick != null )
        {
            ImGui.Text( PenumbraIpc.LabelProviderChangedItemClick );
        }
    }

    private void DrawDebugTabMissingFiles()
    {
        if( !ImGui.CollapsingHeader( "Missing Files##Debug" ) )
        {
            return;
        }

        var cache = Penumbra.ModManager.Collections.CurrentCollection.Cache;
        if( cache == null || !ImGui.BeginTable( "##MissingFilesDebugList", 1, ImGuiTableFlags.RowBg, -Vector2.UnitX ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTable );

        foreach( var file in cache.MissingFiles )
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            if( ImGui.Selectable( file.FullName ) )
            {
                ImGui.SetClipboardText( file.FullName );
            }

            ImGuiCustom.HoverTooltip( "Click to copy to clipboard." );
        }
    }

    private unsafe void DrawDebugTabReplacedResources()
    {
        if( !ImGui.CollapsingHeader( "Replaced Resources##Debug" ) )
        {
            return;
        }

        Penumbra.ResourceLoader.UpdateDebugInfo();

        if( Penumbra.ResourceLoader.DebugList.Count == 0
        || !ImGui.BeginTable( "##ReplacedResourcesDebugList", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.UnitX ) )
        {
            return;
        }

        using var end = ImGuiRaii.DeferredEnd( ImGui.EndTable );

        foreach( var data in Penumbra.ResourceLoader.DebugList.Values.ToArray() )
        {
            var refCountManip = data.ManipulatedResource == null ? 0 : data.ManipulatedResource->RefCount;
            var refCountOrig  = data.OriginalResource    == null ? 0 : data.OriginalResource->RefCount;
            ImGui.TableNextColumn();
            ImGui.Text( data.ManipulatedPath.ToString() );
            ImGui.TableNextColumn();
            ImGui.Text( ( ( ulong )data.ManipulatedResource ).ToString( "X" ) );
            ImGui.TableNextColumn();
            ImGui.Text( refCountManip.ToString() );
            ImGui.TableNextColumn();
            ImGui.Text( data.OriginalPath.ToString() );
            ImGui.TableNextColumn();
            ImGui.Text( ( ( ulong )data.OriginalResource ).ToString( "X" ) );
            ImGui.TableNextColumn();
            ImGui.Text( refCountOrig.ToString() );
        }
    }

    public unsafe void DrawDebugCharacterUtility()
    {
        if( !ImGui.CollapsingHeader( "Character Utility##Debug" ) )
        {
            return;
        }

        if( !ImGui.BeginTable( "##CharacterUtilityDebugList", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit, -Vector2.UnitX ) )
        {
            return;
        }

        using var end = ImGuiRaii.DeferredEnd( ImGui.EndTable );

        for( var i = 0; i < CharacterUtility.RelevantIndices.Length; ++i )
        {
            var idx      = CharacterUtility.RelevantIndices[ i ];
            var resource = ( ResourceHandle* )Penumbra.CharacterUtility.Address->Resources[ idx ];
            ImGui.TableNextColumn();
            ImGui.Text( $"0x{( ulong )resource:X}" );
            ImGui.TableNextColumn();
            ImGuiNative.igTextUnformatted( resource->FileName(), resource->FileName() + resource->FileNameLength );
            ImGui.TableNextColumn();
            ImGui.Text( $"0x{resource->GetData().Data:X}" );
            if( ImGui.IsItemClicked() )
            {
                var (data, length) = resource->GetData();
                ImGui.SetClipboardText( string.Join( " ",
                    new ReadOnlySpan< byte >( ( byte* )data, length ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
            }

            ImGui.TableNextColumn();
            ImGui.Text( $"{resource->GetData().Length}" );
            ImGui.TableNextColumn();
            ImGui.Text( $"0x{Penumbra.CharacterUtility.DefaultResources[ i ].Address:X}" );
            if( ImGui.IsItemClicked() )
            {
                ImGui.SetClipboardText( string.Join( " ",
                    new ReadOnlySpan< byte >( ( byte* )Penumbra.CharacterUtility.DefaultResources[ i ].Address,
                        Penumbra.CharacterUtility.DefaultResources[ i ].Size ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
            }

            ImGui.TableNextColumn();
            ImGui.Text( $"{Penumbra.CharacterUtility.DefaultResources[ i ].Size}" );
        }
    }

    private unsafe void DrawPathResolverDebug()
    {
        if( !ImGui.CollapsingHeader( "Path Resolver##Debug" ) )
        {
            return;
        }

        if( ImGui.TreeNodeEx( "Draw Object to Object" ) )
        {
            using var end = ImGuiRaii.DeferredEnd( ImGui.TreePop );
            if( ImGui.BeginTable( "###DrawObjectResolverTable", 5, ImGuiTableFlags.SizingFixedFit ) )
            {
                end.Push( ImGui.EndTable );
                foreach( var (ptr, (c, idx)) in _penumbra.PathResolver.DrawObjectToObject )
                {
                    ImGui.TableNextColumn();
                    ImGui.Text( ptr.ToString( "X" ) );
                    ImGui.TableNextColumn();
                    ImGui.Text( idx.ToString() );
                    ImGui.TableNextColumn();
                    ImGui.Text( Dalamud.Objects[ idx ]?.Address.ToString() ?? "NULL" );
                    ImGui.TableNextColumn();
                    ImGui.Text( Dalamud.Objects[ idx ]?.Name.ToString() ?? "NULL" );
                    ImGui.TableNextColumn();
                    ImGui.Text( c.Name );
                }
            }
        }

        if( ImGui.TreeNodeEx( "Path Collections" ) )
        {
            using var end = ImGuiRaii.DeferredEnd( ImGui.TreePop );
            if( ImGui.BeginTable( "###PathCollectionResolverTable", 2, ImGuiTableFlags.SizingFixedFit ) )
            {
                end.Push( ImGui.EndTable );
                foreach( var (path, collection) in _penumbra.PathResolver.PathCollections )
                {
                    ImGui.TableNextColumn();
                    ImGuiNative.igTextUnformatted( path.Path, path.Path + path.Length );
                    ImGui.TableNextColumn();
                    ImGui.Text( collection.Name );
                }
            }
        }
    }

    private void DrawDebugTab()
    {
        if( !ImGui.BeginTabItem( "Debug Tab" ) )
        {
            return;
        }

        using var raii = ImGuiRaii.DeferredEnd( ImGui.EndTabItem );

        if( !ImGui.BeginChild( "##DebugChild", -Vector2.One ) )
        {
            ImGui.EndChild();
            return;
        }

        raii.Push( ImGui.EndChild );

        DrawDebugTabGeneral();
        ImGui.NewLine();
        DrawDebugTabReplacedResources();
        ImGui.NewLine();
        DrawResourceProblems();
        ImGui.NewLine();
        DrawDebugTabMissingFiles();
        ImGui.NewLine();
        DrawPlayerModelInfo();
        ImGui.NewLine();
        DrawPathResolverDebug();
        ImGui.NewLine();
        DrawDebugCharacterUtility();
        ImGui.NewLine();
        DrawDebugTabRedraw();
        ImGui.NewLine();
        DrawDebugTabIpc();
        ImGui.NewLine();
    }
}