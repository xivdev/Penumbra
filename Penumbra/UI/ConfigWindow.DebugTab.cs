using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.Api;
using Penumbra.Interop.Loader;
using Penumbra.Interop.Structs;
using CharacterUtility = Penumbra.Interop.CharacterUtility;

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
        private const bool   DefaultVisibility  = true;
#else
        private const string DebugVersionString = "(Release)";
        private const bool DefaultVisibility = false;
#endif

        public bool DebugTabVisible = DefaultVisibility;

        public void Draw()
        {
            if( !DebugTabVisible )
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
            ImGui.NewLine();
            DrawDebugTabReplacedResources();
            ImGui.NewLine();
            DrawPathResolverDebug();
            ImGui.NewLine();
            DrawDebugCharacterUtility();
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
            PrintValue( "Current Collection", Penumbra.CollectionManager.Current.Name );
            PrintValue( "    has Cache", Penumbra.CollectionManager.Current.HasCache.ToString() );
            PrintValue( "Default Collection", Penumbra.CollectionManager.Default.Name );
            PrintValue( "    has Cache", Penumbra.CollectionManager.Default.HasCache.ToString() );
            PrintValue( "Mod Manager BasePath", manager.BasePath.Name );
            PrintValue( "Mod Manager BasePath-Full", manager.BasePath.FullName );
            PrintValue( "Mod Manager BasePath IsRooted", Path.IsPathRooted( Penumbra.Config.ModDirectory ).ToString() );
            PrintValue( "Mod Manager BasePath Exists", Directory.Exists( manager.BasePath.FullName ).ToString() );
            PrintValue( "Mod Manager Valid", manager.Valid.ToString() );
            PrintValue( "Path Resolver Enabled", _window._penumbra.PathResolver.Enabled.ToString() );
            PrintValue( "Music Manager Streaming Disabled", ( !_window._penumbra.MusicManager.StreamingEnabled ).ToString() );
            PrintValue( "Web Server Enabled", ( _window._penumbra.WebServer != null ).ToString() );
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

        // Draw information about which draw objects correspond to which game objects
        // and which paths are due to be loaded by which collection.
        private unsafe void DrawPathResolverDebug()
        {
            if( !ImGui.CollapsingHeader( "Path Resolver" ) )
            {
                return;
            }

            using var drawTree = ImRaii.TreeNode( "Draw Object to Object" );
            if( drawTree )
            {
                using var table = ImRaii.Table( "###DrawObjectResolverTable", 5, ImGuiTableFlags.SizingFixedFit );
                if( table )
                {
                    foreach( var (ptr, (c, idx)) in _window._penumbra.PathResolver.DrawObjectToObject )
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

            drawTree.Dispose();

            using var pathTree = ImRaii.TreeNode( "Path Collections" );
            if( pathTree )
            {
                using var table = ImRaii.Table( "###PathCollectionResolverTable", 2, ImGuiTableFlags.SizingFixedFit );
                if( table )
                {
                    foreach( var (path, collection) in _window._penumbra.PathResolver.PathCollections )
                    {
                        ImGui.TableNextColumn();
                        ImGuiNative.igTextUnformatted( path.Path, path.Path + path.Length );
                        ImGui.TableNextColumn();
                        ImGui.Text( collection.Name );
                    }
                }
            }
        }

        // Draw information about the character utility class from SE,
        // displaying all files, their sizes, the default files and the default sizes.
        public unsafe void DrawDebugCharacterUtility()
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
                var resource = ( ResourceHandle* )Penumbra.CharacterUtility.Address->Resources[ idx ];
                ImGui.TableNextColumn();
                ImGui.Text( $"0x{( ulong )resource:X}" );
                ImGui.TableNextColumn();
                Text( resource );
                ImGui.TableNextColumn();
                ImGui.Selectable( $"0x{resource->GetData().Data:X}" );
                if( ImGui.IsItemClicked() )
                {
                    var (data, length) = resource->GetData();
                    if( data != IntPtr.Zero && length > 0 )
                    {
                        ImGui.SetClipboardText( string.Join( " ",
                            new ReadOnlySpan< byte >( ( byte* )data, length ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                    }
                }

                ImGuiUtil.HoverTooltip( "Click to copy bytes to clipboard." );

                ImGui.TableNextColumn();
                ImGui.Text( $"{resource->GetData().Length}" );
                ImGui.TableNextColumn();
                ImGui.Selectable( $"0x{Penumbra.CharacterUtility.DefaultResources[ i ].Address:X}" );
                if( ImGui.IsItemClicked() )
                {
                    ImGui.SetClipboardText( string.Join( " ",
                        new ReadOnlySpan< byte >( ( byte* )Penumbra.CharacterUtility.DefaultResources[ i ].Address,
                            Penumbra.CharacterUtility.DefaultResources[ i ].Size ).ToArray().Select( b => b.ToString( "X2" ) ) ) );
                }

                ImGuiUtil.HoverTooltip( "Click to copy bytes to clipboard." );

                ImGui.TableNextColumn();
                ImGui.Text( $"{Penumbra.CharacterUtility.DefaultResources[ i ].Size}" );
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
                ImGui.Text( $"0x{( ulong )resource:X}" );
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
                ImGui.Text( $"Slot {i}" );
                ImGui.TableNextColumn();
                ImGui.Text( imc == null ? "NULL" : $"0x{( ulong )imc:X}" );
                ImGui.TableNextColumn();
                if( imc != null )
                {
                    Text( imc );
                }

                var mdl = ( RenderModel* )model->ModelArray[ i ];
                ImGui.TableNextColumn();
                ImGui.Text( mdl == null ? "NULL" : $"0x{( ulong )mdl:X}" );
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
                ImGui.Text( r->Category.ToString() );
                ImGui.TableNextColumn();
                ImGui.Text( r->FileType.ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.Text( r->Id.ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.Text( ( ( ulong )r ).ToString( "X" ) );
                ImGui.TableNextColumn();
                ImGui.Text( r->RefCount.ToString() );
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
                return;
            }

            var ipc = _window._penumbra.Ipc;
            ImGui.Text( $"API Version: {ipc.Api.ApiVersion}" );
            ImGui.Text( "Available subscriptions:" );
            using var indent = ImRaii.PushIndent();
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

            if( ipc.ProviderGetChangedItems != null )
            {
                ImGui.Text( PenumbraIpc.LabelProviderGetChangedItems );
            }
        }

        // Helper to print a property and its value in a 2-column table.
        private static void PrintValue( string name, string value )
        {
            ImGui.TableNextColumn();
            ImGui.Text( name );
            ImGui.TableNextColumn();
            ImGui.Text( value );
        }
    }
}