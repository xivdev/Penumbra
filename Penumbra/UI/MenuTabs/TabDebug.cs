using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using ImGuiNET;
using Penumbra.Api;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.GameData.Util;
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

            var actors = _plugin.PlayerWatcher.WatchedPlayers().ToArray();
            if( !actors.Any() )
            {
                return;
            }

            if( ImGui.BeginTable( "##ActorTable", 13, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX,
                new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 4 * actors.Length ) ) )
            {
                var identifier = GameData.GameData.GetIdentifier( _plugin.PluginInterface );

                foreach( var (actor, equip) in actors )
                {
                    // @formatter:off
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text( actor );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.MainHand}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Head}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Body}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Hands}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Legs}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Feet}" );
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    if (equip.IsSet == 0)
                    {
                        ImGui.Text( "(not set)" );
                    }
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.MainHand.Set, equip.MainHand.Type, equip.MainHand.Variant, EquipSlot.MainHand )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Head.Set, 0, equip.Head.Variant, EquipSlot.Head )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Body.Set, 0, equip.Body.Variant, EquipSlot.Body )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Hands.Set, 0, equip.Hands.Variant, EquipSlot.Hands )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Legs.Set, 0, equip.Legs.Variant, EquipSlot.Legs )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Feet.Set, 0, equip.Feet.Variant, EquipSlot.Feet )?.Name.ToString() ?? "Unknown" );

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.OffHand}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Ears}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Neck}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.Wrists}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.LFinger}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"{equip.RFinger}" );

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.OffHand.Set, equip.OffHand.Type, equip.OffHand.Variant, EquipSlot.OffHand )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Ears.Set, 0, equip.Ears.Variant, EquipSlot.Ears )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Neck.Set, 0, equip.Neck.Variant, EquipSlot.Neck )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.Wrists.Set, 0, equip.Wrists.Variant, EquipSlot.Wrists )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.LFinger.Set, 0, equip.LFinger.Variant, EquipSlot.LFinger )?.Name.ToString() ?? "Unknown" );
                    ImGui.TableNextColumn();
                    ImGui.Text( identifier.Identify( equip.RFinger.Set, 0, equip.RFinger.Variant, EquipSlot.LFinger )?.Name.ToString() ?? "Unknown" );
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
            PrintValue( "Mod Manager Temp Path", manager.TempPath.FullName );
            PrintValue( "Mod Manager Temp Path IsRooted",
                ( !_plugin.Configuration.TempDirectory.Any() || Path.IsPathRooted( _plugin.Configuration.TempDirectory ) ).ToString() );
            PrintValue( "Mod Manager Temp Path Exists", Directory.Exists( manager.TempPath.FullName ).ToString() );
            PrintValue( "Mod Manager Temp Path IsWritable", manager.TempWritable.ToString() );

            ImGui.EndTable();
        }

        private void DrawDebugTabRedraw()
        {
            if( !ImGui.CollapsingHeader( "Redrawing##Debug" ) )
            {
                return;
            }

            var queue = ( Queue< (int, string, RedrawType) >? )_plugin.ActorRefresher.GetType()
                   .GetField( "_actorIds", BindingFlags.Instance | BindingFlags.NonPublic )
                  ?.GetValue( _plugin.ActorRefresher )
             ?? new Queue< (int, string, RedrawType) >();

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

            var currentActorRedraw = ( RedrawType? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorRedraw", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var (currentActor, currentActorIdx) = ( (Actor?, int) )_plugin.ActorRefresher.GetType()
               .GetMethod( "FindCurrentActor", BindingFlags.NonPublic | BindingFlags.Instance )?
               .Invoke( _plugin.ActorRefresher, Array.Empty< object >() )!;

            var currentRender = currentActor != null
                ? ( ActorRefresher.LoadingFlags? )Marshal.ReadInt32( ActorRefresher.RenderPtr( currentActor ) )
                : null;

            var waitFrames = ( int? )_plugin.ActorRefresher.GetType()
               .GetField( "_waitFrames", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var wasTarget = ( bool? )_plugin.ActorRefresher.GetType()
               .GetField( "_wasTarget", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var gPose = ( bool? )_plugin.ActorRefresher.GetType()
               .GetField( "_inGPose", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            if( ImGui.BeginTable( "##RedrawData", 2, ImGuiTableFlags.SizingFixedFit,
                new Vector2( -1, ImGui.GetTextLineHeightWithSpacing() * 7 ) ) )
            {
                PrintValue( "Current Wait Frame", waitFrames?.ToString()                                      ?? "null" );
                PrintValue( "Current Frame", currentFrame?.ToString()                                         ?? "null" );
                PrintValue( "Currently in GPose", gPose?.ToString()                                           ?? "null" );
                PrintValue( "Current Changed Settings", changedSettings?.ToString()                           ?? "null" );
                PrintValue( "Current Actor Id", currentActorId?.ToString( "X8" )                              ?? "null" );
                PrintValue( "Current Actor Name", currentActorName                                            ?? "null" );
                PrintValue( "Current Actor Start State", ( ( int? )currentActorStartState )?.ToString( "X8" ) ?? "null" );
                PrintValue( "Current Actor Was Target", wasTarget?.ToString()                                 ?? "null" );
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