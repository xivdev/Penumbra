using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Actors.Types;
using ImGuiNET;
using Penumbra.Game;
using Penumbra.Game.Enums;
using Penumbra.Interop;
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

            if( actors.Any() && ImGui.BeginTable( "##ActorTable", 13, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX ) )
            {
                var identifier = Service< ObjectIdentification >.Get();
                ImGui.TableNextRow();
                foreach( var actor in actors )
                {
                    ImGui.TableNextColumn();
                    ImGui.Text( actor.Key );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Mainhand} {identifier.Identify( actor.Value.Mainhand._1, actor.Value.Mainhand._2, actor.Value.Mainhand._3, EquipSlot.MainHand )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Offhand} {identifier.Identify( actor.Value.Offhand._1, actor.Value.Offhand._2, actor.Value.Offhand._3, EquipSlot.Offhand )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Head} {identifier.Identify( actor.Value.Head._1, actor.Value.Head._2, 0, EquipSlot.Head )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Body} {identifier.Identify( actor.Value.Body._1, actor.Value.Body._2, 0, EquipSlot.Body )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Hands} {identifier.Identify( actor.Value.Hands._1, actor.Value.Hands._2, 0, EquipSlot.Hands )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Legs} {identifier.Identify( actor.Value.Legs._1, actor.Value.Legs._2, 0, EquipSlot.Legs )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Feet} {identifier.Identify( actor.Value.Feet._1, actor.Value.Feet._2, 0, EquipSlot.Feet )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Ear} {identifier.Identify( actor.Value.Ear._1, actor.Value.Ear._2, 0, EquipSlot.Ears )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Neck} {identifier.Identify( actor.Value.Neck._1, actor.Value.Neck._2, 0, EquipSlot.Neck )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.Wrist} {identifier.Identify( actor.Value.Wrist._1, actor.Value.Wrist._2, 0, EquipSlot.Wrists )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.LFinger} {identifier.Identify( actor.Value.LFinger._1, actor.Value.LFinger._2, 0, EquipSlot.RingL )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextColumn();
                    ImGui.Text(
                        $"{actor.Value.RFinger} {identifier.Identify( actor.Value.RFinger._1, actor.Value.RFinger._2, 0, EquipSlot.RingL )?.Name.ToString() ?? "Unknown"}" );
                    ImGui.TableNextRow();
                }

                ImGui.EndTable();
            }
        }

        private void DrawDebugTabGeneral()
        {
            if( !ImGui.CollapsingHeader( "General##Debug" ) )
            {
                return;
            }

            ImGui.Text( $"Active Collection: {Service< ModManager >.Get().Collections.ActiveCollection.Name}" );
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

            var currentActorRedraw = ( Redraw? )_plugin.ActorRefresher.GetType()
               .GetField( "_currentActorRedraw", BindingFlags.Instance | BindingFlags.NonPublic )
              ?.GetValue( _plugin.ActorRefresher );

            var currentActor = ( Actor? )_plugin.ActorRefresher.GetType()
               .GetMethod( "FindCurrentActor", BindingFlags.NonPublic | BindingFlags.Instance )?
               .Invoke( _plugin.ActorRefresher, Array.Empty< object >() );

            var currentRender = currentActor != null
                ? ( ActorRefresher.LoadingFlags? )Marshal.ReadInt32( ActorRefresher.RenderPtr( currentActor ) )
                : null;

            ImGui.Text( $"Current Frame: {currentFrame?.ToString()                                 ?? "null"}" );
            ImGui.Text( $"Current Changed Settings: {changedSettings?.ToString()                   ?? "null"}" );
            ImGui.Text( $"Current Actor Id: {currentActorId?.ToString( "X8" )                      ?? "null"}" );
            ImGui.Text( $"Current Actor Name: {currentActorName                                    ?? "null"}" );
            ImGui.Text( $"Current Actor Redraw: {currentActorRedraw.ToString()                     ?? "null"}" );
            ImGui.Text( $"Current Actor Address: {currentActor?.Address.ToString( "X16" )          ?? "null"}" );
            ImGui.Text( $"Current Actor Render Flags: {( ( int? )currentRender )?.ToString( "X8" ) ?? "null"}" );

            if( queue.Any() && ImGui.BeginTable( "##RedrawTable", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollX ) )
            {
                ImGui.TableNextRow();
                foreach( var (actorId, actorName, redraw) in queue )
                {
                    ImGui.TableNextColumn();
                    ImGui.Text( actorName );
                    ImGui.TableNextColumn();
                    ImGui.Text( $"0x{actorId:X8}" );
                    ImGui.TableNextColumn();
                    ImGui.Text( redraw.ToString() );
                    ImGui.TableNextRow();
                }

                ImGui.EndTable();
            }
        }

        private void DrawDebugTab()
        {
            if( !ImGui.BeginTabItem( "Debug Tab" ) )
            {
                return;
            }

            DrawDebugTabGeneral();
            DrawDebugTabRedraw();
            DrawDebugTabActors();

            ImGui.EndTabItem();
        }
    }
}