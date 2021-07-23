using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using ImGuiNET;
using Penumbra.Game;
using Penumbra.Game.Enums;
using Penumbra.Interop;
using Penumbra.Meta;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private void DrawDebugTabActors()
        {
            if( !ImGui.CollapsingHeader( "Actors##Debug" ) )
            {
                return;
            }

            var actors = ( Dictionary< string, CharEquipment >? )_plugin.PlayerWatcher.GetType()
                   .GetField( "_equip", BindingFlags.Instance | BindingFlags.NonPublic )
                  ?.GetValue( _plugin.PlayerWatcher )
             ?? new Dictionary< string, CharEquipment >();
            if( !actors.Any() )
            {
                return;
            }

            if( ImGui.BeginTable( "##ActorTable", 13, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX,
                new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 4 * actors.Count ) ) )
            {
                var identifier = Service< ObjectIdentification >.Get();
                foreach( var actor in actors )
                {
                    // @formatter:off
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text( actor.Key );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Mainhand}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Head}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Body}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Hands}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Legs}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Feet}" );
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (actor.Value.IsSet == 0)
                    {
                        ImGui.Text( "(not set)" );
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Mainhand.Set, actor.Value.Mainhand.Type, actor.Value.Mainhand.Variant, EquipSlot.MainHand )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Head.Set, actor.Value.Head.Variant, 0, EquipSlot.Head )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Body.Set, actor.Value.Body.Variant, 0, EquipSlot.Body )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Hands.Set, actor.Value.Hands.Variant, 0, EquipSlot.Hands )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Legs.Set, actor.Value.Legs.Variant, 0, EquipSlot.Legs )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Feet.Set, actor.Value.Feet.Variant, 0, EquipSlot.Feet )?.Name.ToString() ?? "Unknown" );

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Offhand}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Ear}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Neck}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.Wrist}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.LFinger}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{actor.Value.RFinger}" );

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Offhand.Set, actor.Value.Offhand.Type, actor.Value.Offhand.Variant, EquipSlot.Offhand )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Ear.Set, actor.Value.Ear.Variant, 0, EquipSlot.Ears )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Neck.Set, actor.Value.Neck.Variant, 0, EquipSlot.Neck )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.Wrist.Set, actor.Value.Wrist.Variant, 0, EquipSlot.Wrists )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.LFinger.Set, actor.Value.LFinger.Variant, 0, EquipSlot.RingL )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( actor.Value.RFinger.Set, actor.Value.RFinger.Variant, 0, EquipSlot.RingL )?.Name.ToString() ?? "Unknown" );
                    // @formatter:on
                }

                ImGui.EndTable();
            }
        }

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

            var manager = Service< ModManager >.Get();
            PrintValue( "Active Collection", manager.Collections.ActiveCollection.Name );
            PrintValue( "Mod Manager BasePath", manager.BasePath.Name );
            PrintValue( "Mod Manager BasePath-Full", manager.BasePath.FullName );
            PrintValue( "Mod Manager BasePath IsRooted", Path.IsPathRooted( _plugin.Configuration.ModDirectory ).ToString() );
            PrintValue( "Mod Manager BasePath Exists", Directory.Exists( manager.BasePath.FullName ).ToString() );
            PrintValue( "Mod Manager Valid", manager.Valid.ToString() );

            ImGui.EndTable();
        }

        private void DrawDebugTabRedraw()
        {
            if( !ImGui.CollapsingHeader( "Redrawing##Debug" ) )
            {
                return;
            }

            var queue = ( Queue< (int, string, Redraw) >? )_plugin.ActorRefresher.GetType()
                   .GetField( "_actorIds", BindingFlags.Instance | BindingFlags.NonPublic )
                  ?.GetValue( _plugin.ActorRefresher )
             ?? new Queue< (int, string, Redraw) >();

            var currentFrame = ( int? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentFrame", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var changedSettings = ( bool? )_plugin.ActorRefresher.GetType()
               .GetField( "_changedSettings", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var currentActorId = ( int? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorId", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var currentActorName = ( string? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorName", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var currentActorStartState = ( ActorRefresher.LoadingFlags? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorStartState", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var currentActorRedraw = ( Redraw? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorRedraw", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var (currentActor, currentActorIdx) = ( (Actor?, int) )_plugin.ActorRefresher.GetType()
               .GetMethod( "FindCurrentActor", BindingFlags.NonPublic | BindingFlags.Instance )?
               .Invoke( _plugin.ActorRefresher, Array.Empty< object >() )!;

            var currentRender = currentActor != null
                ? ( ActorRefresher.LoadingFlags? )Marshal.ReadInt32( ActorRefresher.RenderPtr( currentActor ) )
                : null;

            if( ImGui.BeginTable( "##RedrawData", 2, ImGuiTableFlags.SizingFixedFit,
                new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 7 ) ) )
            {
                PrintValue( "Current Frame", currentFrame?.ToString()                                         ?? "null" );
                PrintValue( "Current Changed Settings", changedSettings?.ToString()                           ?? "null" );
                PrintValue( "Current Actor Id", currentActorId?.ToString( "X8" )                              ?? "null" );
                PrintValue( "Current Actor Name", currentActorName                                            ?? "null" );
                PrintValue( "Current Actor Start State", ( ( int? )currentActorStartState )?.ToString( "X8" ) ?? "null" );
                PrintValue( "Current Actor Redraw", currentActorRedraw?.ToString()                            ?? "null" );
                PrintValue( "Current Actor Address", currentActor?.Address.ToString( "X16" )                  ?? "null" );
                PrintValue( "Current Actor Index", currentActorIdx >= 0 ? currentActorIdx.ToString() : "null" );
                PrintValue( "Current Actor Render Flags", ( ( int? )currentRender )?.ToString( "X8" ) ?? "null" );
                ImGui.EndTable();
            }

            if( queue.Any()
             && ImGui.BeginTable( "##RedrawTable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX,
                    new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * queue.Count ) ) )
            {
                foreach( var (actorId, actorName, redraw) in queue )
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text( actorName );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"0x{actorId:X8}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( redraw.ToString() );
                }

                ImGui.EndTable();
            }

            if( queue.Any() && ImGui.Button( "Clear" ) )
            {
                queue.Clear();
                _plugin.ActorRefresher.GetType()
                   .GetField( "_currentFrame", BindingFlags.Instance | BindingFlags.NonPublic )?.SetValue( _plugin.ActorRefresher, 0 );
            }
        }

        private void DrawDebugTabTempFiles()
        {
            if( !ImGui.CollapsingHeader( "Temporary Files##Debug" ) )
            {
                return;
            }

            if( !ImGui.BeginTable( "##tempFileTable", 4, ImGuiTableFlags.SizingFixedFit ) )
            {
                return;
            }

            foreach( var collection in Service< ModManager >.Get().Collections.Collections.Values.Where( c => c.Cache != null ) )
            {
                var manip = collection.Cache!.MetaManipulations;
                var files = ( Dictionary< GamePath, MetaManager.FileInformation >? )manip.GetType()
                       .GetField( "_currentFiles", BindingFlags.NonPublic | BindingFlags.Instance )?.GetValue( manip )
                 ?? new Dictionary< GamePath, MetaManager.FileInformation >();


                foreach( var file in files )
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text( file.Value.CurrentFile?.FullName ?? "None" );
                    ImGui.TableNextColumn();
                    ImGui.Text( file.Key );
                    ImGui.TableNextColumn();
                    file.Value.CurrentFile?.Refresh();
                    ImGui.Text( file.Value.CurrentFile?.Exists ?? false ? "Exists" : "Missing" );
                    ImGui.TableNextColumn();
                    ImGui.Text( file.Value.Changed ? "Data Changed" : "Unchanged" );
                }
            }

            ImGui.EndTable();
        }

        private void DrawDebugTab()
        {
            if( !ImGui.BeginTabItem( "Debug Tab" ) )
            {
                return;
            }

            DrawDebugTabGeneral();
            ImGui.NewLine();
            DrawDebugTabRedraw();
            ImGui.NewLine();
            DrawDebugTabActors();
            ImGui.NewLine();
            DrawDebugTabTempFiles();
            ImGui.NewLine();

            ImGui.EndTabItem();
        }
    }
}