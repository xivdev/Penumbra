using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Files;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Resolver;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.Util;
using CharacterUtility = Penumbra.Interop.CharacterUtility;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Penumbra.UI;

public partial class ConfigWindow
{
    private class DebugTab
    {
        private readonly ConfigWindow _window;

        public DebugTab( ConfigWindow window )
            => _window = window;

#if DEBUG
        private const string DebugVersionString = "(Debug)";
#else
        private const string DebugVersionString = "(Release)";
#endif

        public void Draw()
        {
            if( !Penumbra.Config.DebugMode )
            {
                return;
            }

            using var tab = ImRaii.TabItem( "Debug" );
            if( !tab )
            {
                return;
            }

            using var child = ImRaii.Child( "##DebugTab", -Vector2.One );
            if( !child )
            {
                return;
            }

            DrawDebugTabGeneral();
            DrawPerformanceTab();
            ImGui.NewLine();
            DrawDebugTabReplacedResources();
            ImGui.NewLine();
            DrawPathResolverDebug();
            ImGui.NewLine();
            DrawActorsDebug();
            ImGui.NewLine();
            DrawDebugCharacterUtility();
            ImGui.NewLine();
            DrawStainTemplates();
            ImGui.NewLine();
            DrawDebugTabMetaLists();
            ImGui.NewLine();
            DrawDebugResidentResources();
            ImGui.NewLine();
            DrawResourceProblems();
            ImGui.NewLine();
            DrawPlayerModelInfo();
            ImGui.NewLine();
            DrawDebugTabIpc();
            ImGui.NewLine();
        }

        // Draw general information about mod and collection state.
        private void DrawDebugTabGeneral()
        {
            if( !ImGui.CollapsingHeader( "General" ) )
            {
                return;
            }

            using var table = ImRaii.Table( "##DebugGeneralTable", 2, ImGuiTableFlags.SizingFixedFit,
                new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 1 ) );
            if( !table )
            {
                return;
            }

            var manager = Penumbra.ModManager;
            PrintValue( "Penumbra Version", $"{Penumbra.Version} {DebugVersionString}" );
            PrintValue( "Git Commit Hash", Penumbra.CommitHash );
            PrintValue( SelectedCollection, Penumbra.CollectionManager.Current.Name );
            PrintValue( "    has Cache", Penumbra.CollectionManager.Current.HasCache.ToString() );
            PrintValue( DefaultCollection, Penumbra.CollectionManager.Default.Name );
            PrintValue( "    has Cache", Penumbra.CollectionManager.Default.HasCache.ToString() );
            PrintValue( "Mod Manager BasePath", manager.BasePath.Name );
            PrintValue( "Mod Manager BasePath-Full", manager.BasePath.FullName );
            PrintValue( "Mod Manager BasePath IsRooted", Path.IsPathRooted( Penumbra.Config.ModDirectory ).ToString() );
            PrintValue( "Mod Manager BasePath Exists", Directory.Exists( manager.BasePath.FullName ).ToString() );
            PrintValue( "Mod Manager Valid", manager.Valid.ToString() );
            PrintValue( "Path Resolver Enabled", _window._penumbra.PathResolver.Enabled.ToString() );
            PrintValue( "Web Server Enabled", ( _window._penumbra.WebServer != null ).ToString() );
        }

        [Conditional("DEBUG")]
        private static void DrawPerformanceTab()
        {
            ImGui.NewLine();
            if( !ImGui.CollapsingHeader( "Performance" ) )
            {
                return;
            }

            Penumbra.Performance.Draw( "##performance", "Enable Performance Tracking", PerformanceTypeExtensions.ToName );
        }

        // Draw all resources currently replaced by Penumbra and (if existing) the resources they replace.
        // Resources are collected by iterating through the
        private static unsafe void DrawDebugTabReplacedResources()
        {
            if( !ImGui.CollapsingHeader( "Replaced Resources" ) )
            {
                return;
            }

            Penumbra.ResourceLoader.UpdateDebugInfo();

            if( Penumbra.ResourceLoader.DebugList.Count == 0 )
            {
                return;
            }

            using var table = ImRaii.Table( "##ReplacedResources", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
                -Vector2.UnitX );
            if( !table )
            {
                return;
            }

            foreach( var data in Penumbra.ResourceLoader.DebugList.Values.ToArray() )
            {
                if( data.ManipulatedPath.Crc64 == 0 )
                {
                    continue;
                }

                var refCountManip = data.ManipulatedResource == null ? 0 : data.ManipulatedResource->RefCount;
                var refCountOrig  = data.OriginalResource    == null ? 0 : data.OriginalResource->RefCount;
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( data.ManipulatedPath.ToString() );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( ( ( ulong )data.ManipulatedResource ).ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( refCountManip.ToString() );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( data.OriginalPath.ToString() );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( ( ( ulong )data.OriginalResource ).ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( refCountOrig.ToString() );
            }
        }

        private static unsafe void DrawActorsDebug()
        {
            if( !ImGui.CollapsingHeader( "Actors" ) )
            {
                return;
            }

            using var table = ImRaii.Table( "##actors", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
                -Vector2.UnitX );
            if( !table )
            {
                return;
            }

            static void DrawSpecial( string name, ActorIdentifier id )
            {
                if( !id.IsValid )
                {
                    return;
                }

                ImGuiUtil.DrawTableColumn( name );
                ImGuiUtil.DrawTableColumn( string.Empty );
                ImGuiUtil.DrawTableColumn( Penumbra.Actors.ToString( id ) );
                ImGuiUtil.DrawTableColumn( string.Empty );
            }

            DrawSpecial( "Current Player", Penumbra.Actors.GetCurrentPlayer() );
            DrawSpecial( "Current Inspect", Penumbra.Actors.GetInspectPlayer() );
            DrawSpecial( "Current Card", Penumbra.Actors.GetCardPlayer() );
            DrawSpecial( "Current Glamour", Penumbra.Actors.GetGlamourPlayer() );

            foreach( var obj in Dalamud.Objects )
            {
                ImGuiUtil.DrawTableColumn( $"{( ( GameObject* )obj.Address )->ObjectIndex}" );
                ImGuiUtil.DrawTableColumn( $"0x{obj.Address:X}" );
                var identifier = Penumbra.Actors.FromObject( obj, false, true );
                ImGuiUtil.DrawTableColumn( Penumbra.Actors.ToString( identifier ) );
                var id = obj.ObjectKind == ObjectKind.BattleNpc ? $"{identifier.DataId} | {obj.DataId}" : identifier.DataId.ToString();
                ImGuiUtil.DrawTableColumn( id );
            }
        }

        // Draw information about which draw objects correspond to which game objects
        // and which paths are due to be loaded by which collection.
        private unsafe void DrawPathResolverDebug()
        {
            if( !ImGui.CollapsingHeader( "Path Resolver" ) )
            {
                return;
            }

            using( var drawTree = ImRaii.TreeNode( "Draw Object to Object" ) )
            {
                if( drawTree )
                {
                    using var table = ImRaii.Table( "###DrawObjectResolverTable", 5, ImGuiTableFlags.SizingFixedFit );
                    if( table )
                    {
                        foreach( var (ptr, (c, idx)) in _window._penumbra.PathResolver.DrawObjectMap )
                        {
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( ptr.ToString( "X" ) );
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( idx.ToString() );
                            ImGui.TableNextColumn();
                            var obj = ( GameObject* )Dalamud.Objects.GetObjectAddress( idx );
                            var (address, name) =
                                obj != null ? ( $"0x{( ulong )obj:X}", new ByteString( obj->Name ).ToString() ) : ( "NULL", "NULL" );
                            ImGui.TextUnformatted( address );
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( name );
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( c.ModCollection.Name );
                        }
                    }
                }
            }

            using( var pathTree = ImRaii.TreeNode( "Path Collections" ) )
            {
                if( pathTree )
                {
                    using var table = ImRaii.Table( "###PathCollectionResolverTable", 3, ImGuiTableFlags.SizingFixedFit );
                    if( table )
                    {
                        foreach( var (path, collection) in _window._penumbra.PathResolver.PathCollections )
                        {
                            ImGui.TableNextColumn();
                            ImGuiNative.igTextUnformatted( path.Path, path.Path + path.Length );
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( collection.ModCollection.Name );
                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted( collection.AssociatedGameObject.ToString( "X" ) );
                        }
                    }
                }
            }

            using( var resourceTree = ImRaii.TreeNode( "Subfile Collections" ) )
            {
                if( resourceTree )
                {
                    using var table = ImRaii.Table( "###ResourceCollectionResolverTable", 3, ImGuiTableFlags.SizingFixedFit );
                    if( table )
                    {
                        ImGuiUtil.DrawTableColumn( "Current Mtrl Data" );
                        ImGuiUtil.DrawTableColumn( _window._penumbra.PathResolver.CurrentMtrlData.ModCollection.Name );
                        ImGuiUtil.DrawTableColumn( $"0x{_window._penumbra.PathResolver.CurrentMtrlData.AssociatedGameObject:X}" );

                        ImGuiUtil.DrawTableColumn( "Current Avfx Data" );
                        ImGuiUtil.DrawTableColumn( _window._penumbra.PathResolver.CurrentAvfxData.ModCollection.Name );
                        ImGuiUtil.DrawTableColumn( $"0x{_window._penumbra.PathResolver.CurrentAvfxData.AssociatedGameObject:X}" );

                        foreach( var (resource, resolve) in _window._penumbra.PathResolver.ResourceCollections )
                        {
                            ImGuiUtil.DrawTableColumn( $"0x{resource:X}" );
                            ImGuiUtil.DrawTableColumn( resolve.ModCollection.Name );
                            ImGuiUtil.DrawTableColumn( $"0x{resolve.AssociatedGameObject:X}" );
                        }
                    }
                }
            }

            using( var identifiedTree = ImRaii.TreeNode( "Identified Collections" ) )
            {
                if( identifiedTree )
                {
                    using var table = ImRaii.Table( "##PathCollectionsIdentifiedTable", 3, ImGuiTableFlags.SizingFixedFit );
                    if( table )
                    {
                        foreach( var (address, identifier, collection) in PathResolver.IdentifiedCache )
                        {
                            ImGuiUtil.DrawTableColumn( $"0x{address:X}" );
                            ImGuiUtil.DrawTableColumn( identifier.ToString() );
                            ImGuiUtil.DrawTableColumn( collection.Name );
                        }
                    }
                }
            }

            using var cutsceneTree = ImRaii.TreeNode( "Cutscene Actors" );
            if( cutsceneTree )
            {
                using var table = ImRaii.Table( "###PCutsceneResolverTable", 2, ImGuiTableFlags.SizingFixedFit );
                if( table )
                {
                    foreach( var (idx, actor) in _window._penumbra.PathResolver.CutsceneActors )
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted( $"Cutscene Actor {idx}" );
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted( actor.Name.ToString() );
                    }
                }
            }
        }

        private static unsafe void DrawStainTemplates()
        {
            if( !ImGui.CollapsingHeader( "Staining Templates" ) )
            {
                return;
            }

            foreach( var (key, data) in Penumbra.StainManager.StmFile.Entries )
            {
                using var tree = ImRaii.TreeNode( $"Template {key}" );
                if( !tree )
                {
                    continue;
                }

                using var table = ImRaii.Table( "##table", 5, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg );
                if( !table )
                {
                    continue;
                }

                for( var i = 0; i < StmFile.StainingTemplateEntry.NumElements; ++i )
                {
                    var (r, g, b) = data.DiffuseEntries[ i ];
                    ImGuiUtil.DrawTableColumn( $"{r:F6} | {g:F6} | {b:F6}" );

                    ( r, g, b ) = data.SpecularEntries[ i ];
                    ImGuiUtil.DrawTableColumn( $"{r:F6} | {g:F6} | {b:F6}" );

                    ( r, g, b ) = data.EmissiveEntries[ i ];
                    ImGuiUtil.DrawTableColumn( $"{r:F6} | {g:F6} | {b:F6}" );

                    var a = data.SpecularPowerEntries[ i ];
                    ImGuiUtil.DrawTableColumn( $"{a:F6}" );

                    a = data.GlossEntries[ i ];
                    ImGuiUtil.DrawTableColumn( $"{a:F6}" );
                }
            }
        }

        // Draw information about the character utility class from SE,
        // displaying all files, their sizes, the default files and the default sizes.
        public static unsafe void DrawDebugCharacterUtility()
        {
            if( !ImGui.CollapsingHeader( "Character Utility" ) )
            {
                return;
            }

            using var table = ImRaii.Table( "##CharacterUtility", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
                -Vector2.UnitX );
            if( !table )
            {
                return;
            }

            for( var i = 0; i < CharacterUtility.RelevantIndices.Length; ++i )
            {
                var idx      = CharacterUtility.RelevantIndices[ i ];
                var intern   = new CharacterUtility.InternalIndex( i );
                var resource = ( ResourceHandle* )Penumbra.CharacterUtility.Address->Resource( idx );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( $"0x{( ulong )resource:X}" );
                ImGui.TableNextColumn();
                Text( resource );
                ImGui.TableNextColumn();
                ImGui.Selectable( $"0x{resource->GetData().Data:X}" );
                if( ImGui.IsItemClicked() )
                {
                    var (data, length) = resource->GetData();
                    if( data != IntPtr.Zero && length > 0 )
                    {
                        ImGui.SetClipboardText( string.Join( "\n",
                            new ReadOnlySpan< byte >( ( byte* )data, length ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                    }
                }

                ImGuiUtil.HoverTooltip( "Click to copy bytes to clipboard." );

                ImGui.TableNextColumn();
                ImGui.TextUnformatted( $"{resource->GetData().Length}" );
                ImGui.TableNextColumn();
                ImGui.Selectable( $"0x{Penumbra.CharacterUtility.DefaultResource( intern ).Address:X}" );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( string.Join( "\n",
                        new ReadOnlySpan< byte >( ( byte* )Penumbra.CharacterUtility.DefaultResource( intern ).Address,
                            Penumbra.CharacterUtility.DefaultResource( intern ).Size ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                }

                ImGuiUtil.HoverTooltip( "Click to copy bytes to clipboard." );

                ImGui.TableNextColumn();
                ImGui.TextUnformatted( $"{Penumbra.CharacterUtility.DefaultResource( intern ).Size}" );
            }
        }

        private static void DrawDebugTabMetaLists()
        {
            if( !ImGui.CollapsingHeader( "Metadata Changes" ) )
            {
                return;
            }

            using var table = ImRaii.Table( "##DebugMetaTable", 3, ImGuiTableFlags.SizingFixedFit );
            if( !table )
            {
                return;
            }

            foreach( var list in Penumbra.CharacterUtility.Lists )
            {
                ImGuiUtil.DrawTableColumn( list.GlobalIndex.ToString() );
                ImGuiUtil.DrawTableColumn( list.Entries.Count.ToString() );
                ImGuiUtil.DrawTableColumn( string.Join( ", ", list.Entries.Select( e => $"0x{e.Data:X}" ) ) );
            }
        }

        // Draw information about the resident resource files.
        public unsafe void DrawDebugResidentResources()
        {
            if( !ImGui.CollapsingHeader( "Resident Resources" ) )
            {
                return;
            }

            if( Penumbra.ResidentResources.Address == null || Penumbra.ResidentResources.Address->NumResources == 0 )
            {
                return;
            }

            using var table = ImRaii.Table( "##ResidentResources", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit,
                -Vector2.UnitX );
            if( !table )
            {
                return;
            }

            for( var i = 0; i < Penumbra.ResidentResources.Address->NumResources; ++i )
            {
                var resource = Penumbra.ResidentResources.Address->ResourceList[ i ];
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( $"0x{( ulong )resource:X}" );
                ImGui.TableNextColumn();
                Text( resource );
            }
        }

        // Draw information about the models, materials and resources currently loaded by the local player.
        private static unsafe void DrawPlayerModelInfo()
        {
            var player = Dalamud.ClientState.LocalPlayer;
            var name   = player?.Name.ToString() ?? "NULL";
            if( !ImGui.CollapsingHeader( $"Player Model Info: {name}##Draw" ) || player == null )
            {
                return;
            }

            var model = ( CharacterBase* )( ( Character* )player.Address )->GameObject.GetDrawObject();
            if( model == null )
            {
                return;
            }

            using var table = ImRaii.Table( $"##{name}DrawTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit );
            if( !table )
            {
                return;
            }

            ImGui.TableNextColumn();
            ImGui.TableHeader( "Slot" );
            ImGui.TableNextColumn();
            ImGui.TableHeader( "Imc Ptr" );
            ImGui.TableNextColumn();
            ImGui.TableHeader( "Imc File" );
            ImGui.TableNextColumn();
            ImGui.TableHeader( "Model Ptr" );
            ImGui.TableNextColumn();
            ImGui.TableHeader( "Model File" );

            for( var i = 0; i < model->SlotCount; ++i )
            {
                var imc = ( ResourceHandle* )model->IMCArray[ i ];
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( $"Slot {i}" );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( imc == null ? "NULL" : $"0x{( ulong )imc:X}" );
                ImGui.TableNextColumn();
                if( imc != null )
                {
                    Text( imc );
                }

                var mdl = ( RenderModel* )model->ModelArray[ i ];
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( mdl == null ? "NULL" : $"0x{( ulong )mdl:X}" );
                if( mdl == null || mdl->ResourceHandle == null || mdl->ResourceHandle->Category != ResourceCategory.Chara )
                {
                    continue;
                }

                ImGui.TableNextColumn();
                {
                    Text( mdl->ResourceHandle );
                }
            }
        }

        // Draw resources with unusual reference count.
        private static unsafe void DrawResourceProblems()
        {
            var header = ImGui.CollapsingHeader( "Resource Problems" );
            ImGuiUtil.HoverTooltip( "Draw resources with unusually high reference count to detect overflows." );
            if( !header )
            {
                return;
            }

            using var table = ImRaii.Table( "##ProblemsTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit );
            if( !table )
            {
                return;
            }

            ResourceLoader.IterateResources( ( _, r ) =>
            {
                if( r->RefCount < 10000 )
                {
                    return;
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted( r->Category.ToString() );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( r->FileType.ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( r->Id.ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( ( ( ulong )r ).ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.TextUnformatted( r->RefCount.ToString() );
                ImGui.TableNextColumn();
                ref var name = ref r->FileName;
                if( name.Capacity > 15 )
                {
                    ImGuiNative.igTextUnformatted( name.BufferPtr, name.BufferPtr + name.Length );
                }
                else
                {
                    fixed( byte* ptr = name.Buffer )
                    {
                        ImGuiNative.igTextUnformatted( ptr, ptr + name.Length );
                    }
                }
            } );
        }


        // Draw information about IPC options and availability.
        private void DrawDebugTabIpc()
        {
            if( !ImGui.CollapsingHeader( "IPC" ) )
            {
                _window._penumbra.IpcProviders.Tester.UnsubscribeEvents();
                return;
            }

            _window._penumbra.IpcProviders.Tester.Draw();
        }

        // Helper to print a property and its value in a 2-column table.
        private static void PrintValue( string name, string value )
        {
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( name );
            ImGui.TableNextColumn();
            ImGui.TextUnformatted( value );
        }
    }
}