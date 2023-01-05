using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.Api;
using Penumbra.Api.Enums;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop;

public unsafe partial class ObjectReloader
{
    public const int GPosePlayerIdx = 201;
    public const int GPoseSlots     = 42;
    public const int GPoseEndIdx    = GPosePlayerIdx + GPoseSlots;

    private readonly string?[] _gPoseNames       = new string?[GPoseSlots];
    private          int       _gPoseNameCounter = 0;
    private          bool      _inGPose          = false;

    // VFuncs that disable and enable draw, used only for GPose actors.
    private static void DisableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 17 ]( actor.Address );

    private static void EnableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 16 ]( actor.Address );

    // Check whether we currently are in GPose.
    // Also clear the name list.
    private void SetGPose()
    {
        _inGPose          = Dalamud.Objects[ GPosePlayerIdx ] != null;
        _gPoseNameCounter = 0;
    }

    private static bool IsGPoseActor( int idx )
        => idx is >= GPosePlayerIdx and < GPoseEndIdx;

    // Return whether an object has to be replaced by a GPose object.
    // If the object does not exist, is already a GPose actor
    // or no actor of the same name is found in the GPose actor list,
    // obj will be the object itself (or null) and false will be returned.
    // If we are in GPose and a game object with the same name as the original actor is found,
    // this will be in obj and true will be returned.
    private bool FindCorrectActor( int idx, out GameObject? obj )
    {
        obj = Dalamud.Objects[ idx ];
        if( !_inGPose || obj == null || IsGPoseActor( idx ) )
        {
            return false;
        }

        var name = obj.Name.ToString();
        for( var i = 0; i < _gPoseNameCounter; ++i )
        {
            var gPoseName = _gPoseNames[ i ];
            if( gPoseName == null )
            {
                break;
            }

            if( name == gPoseName )
            {
                obj = Dalamud.Objects[ GPosePlayerIdx + i ];
                return true;
            }
        }

        for( ; _gPoseNameCounter < GPoseSlots; ++_gPoseNameCounter )
        {
            var gPoseName = Dalamud.Objects[ GPosePlayerIdx + _gPoseNameCounter ]?.Name.ToString();
            _gPoseNames[ _gPoseNameCounter ] = gPoseName;
            if( gPoseName == null )
            {
                break;
            }

            if( name == gPoseName )
            {
                obj = Dalamud.Objects[ GPosePlayerIdx + _gPoseNameCounter ];
                return true;
            }
        }

        return obj;
    }

    // Do not ever redraw any of the five UI Window actors.
    private static bool BadRedrawIndices( GameObject? actor, out int tableIndex )
    {
        if( actor == null )
        {
            tableIndex = -1;
            return true;
        }

        tableIndex = ObjectTableIndex( actor );
        return tableIndex is >= 240 and < 245;
    }
}

public sealed unsafe partial class ObjectReloader : IDisposable
{
    private readonly List< int > _queue           = new(100);
    private readonly List< int > _afterGPoseQueue = new(GPoseSlots);
    private          int         _target          = -1;

    public event GameObjectRedrawnDelegate? GameObjectRedrawn;

    public ObjectReloader()
        => Dalamud.Framework.Update += OnUpdateEvent;

    public void Dispose()
        => Dalamud.Framework.Update -= OnUpdateEvent;

    public static DrawState* ActorDrawState( GameObject actor )
        => ( DrawState* )( actor.Address + 0x0104 );

    private static int ObjectTableIndex( GameObject actor )
        => ( ( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* )actor.Address )->ObjectIndex;

    private static void WriteInvisible( GameObject? actor )
    {
        if( BadRedrawIndices( actor, out var tableIndex ) )
        {
            return;
        }

        *ActorDrawState( actor! ) |= DrawState.Invisibility;

        var gPose = IsGPoseActor( tableIndex );
        if( gPose )
        {
            DisableDraw( actor! );
        }

        if( actor is PlayerCharacter && Dalamud.Objects[ tableIndex + 1 ] is { ObjectKind: ObjectKind.MountType } mount )
        {
            *ActorDrawState( mount ) |= DrawState.Invisibility;
            if( gPose )
            {
                DisableDraw( mount );
            }
        }
    }

    private void WriteVisible( GameObject? actor )
    {
        if( BadRedrawIndices( actor, out var tableIndex ) )
        {
            return;
        }

        *ActorDrawState( actor! ) &= ~DrawState.Invisibility;

        var gPose = IsGPoseActor( tableIndex );
        if( gPose )
        {
            EnableDraw( actor! );
        }

        if( actor is PlayerCharacter && Dalamud.Objects[ tableIndex + 1 ] is { ObjectKind: ObjectKind.MountType } mount )
        {
            *ActorDrawState( mount ) &= ~DrawState.Invisibility;
            if( gPose )
            {
                EnableDraw( mount );
            }
        }

        GameObjectRedrawn?.Invoke( actor!.Address, tableIndex );
    }

    private void ReloadActor( GameObject? actor )
    {
        if( BadRedrawIndices( actor, out var tableIndex ) )
        {
            return;
        }

        if( actor!.Address == Dalamud.Targets.Target?.Address )
        {
            _target = tableIndex;
        }

        _queue.Add( ~tableIndex );
    }

    private void ReloadActorAfterGPose( GameObject? actor )
    {
        if( Dalamud.Objects[ GPosePlayerIdx ] != null )
        {
            ReloadActor( actor );
            return;
        }

        if( actor != null )
        {
            WriteInvisible( actor );
            _afterGPoseQueue.Add( ~ObjectTableIndex( actor ) );
        }
    }

    private void HandleTarget()
    {
        if( _target < 0 )
        {
            return;
        }

        var actor = Dalamud.Objects[ _target ];
        if( actor == null || Dalamud.Targets.Target != null )
        {
            return;
        }

        Dalamud.Targets.SetTarget( actor );
        _target = -1;
    }

    private void HandleRedraw()
    {
        if( _queue.Count == 0 )
        {
            return;
        }

        var numKept = 0;
        for( var i = 0; i < _queue.Count; ++i )
        {
            var idx = _queue[ i ];
            if( FindCorrectActor( idx < 0 ? ~idx : idx, out var obj ) )
            {
                _afterGPoseQueue.Add( idx < 0 ? idx : ~idx );
            }

            if( obj != null )
            {
                if( idx < 0 )
                {
                    WriteInvisible( obj );
                    _queue[ numKept++ ] = ObjectTableIndex( obj );
                }
                else
                {
                    WriteVisible( obj );
                }
            }
        }

        _queue.RemoveRange( numKept, _queue.Count - numKept );
    }

    private void HandleAfterGPose()
    {
        if( _afterGPoseQueue.Count == 0 || _inGPose )
        {
            return;
        }

        var numKept = 0;
        for( var i = 0; i < _afterGPoseQueue.Count; ++i )
        {
            var idx = _afterGPoseQueue[ i ];
            if( idx < 0 )
            {
                var newIdx = ~idx;
                WriteInvisible( Dalamud.Objects[ newIdx ] );
                _afterGPoseQueue[ numKept++ ] = newIdx;
            }
            else
            {
                WriteVisible( Dalamud.Objects[ idx ] );
            }
        }

        _afterGPoseQueue.RemoveRange( numKept, _afterGPoseQueue.Count - numKept );
    }

    private void OnUpdateEvent( object framework )
    {
        if( Dalamud.Conditions[ ConditionFlag.BetweenAreas51 ]
        || Dalamud.Conditions[ ConditionFlag.BetweenAreas ]
        || Dalamud.Conditions[ ConditionFlag.OccupiedInCutSceneEvent ] )
        {
            return;
        }

        SetGPose();
        HandleRedraw();
        HandleAfterGPose();
        HandleTarget();
    }

    public void RedrawObject( GameObject? actor, RedrawType settings )
    {
        switch( settings )
        {
            case RedrawType.Redraw:
                ReloadActor( actor );
                break;
            case RedrawType.AfterGPose:
                ReloadActorAfterGPose( actor );
                break;
            default: throw new ArgumentOutOfRangeException( nameof( settings ), settings, null );
        }
    }

    private static GameObject? GetLocalPlayer()
    {
        var gPosePlayer = Dalamud.Objects[ GPosePlayerIdx ];
        return gPosePlayer ?? Dalamud.Objects[ 0 ];
    }

    public static bool GetName( string lowerName, out GameObject? actor )
    {
        ( actor, var ret ) = lowerName switch
        {
            ""          => ( null, true ),
            "<me>"      => ( GetLocalPlayer(), true ),
            "self"      => ( GetLocalPlayer(), true ),
            "<t>"       => ( Dalamud.Targets.Target, true ),
            "target"    => ( Dalamud.Targets.Target, true ),
            "<f>"       => ( Dalamud.Targets.FocusTarget, true ),
            "focus"     => ( Dalamud.Targets.FocusTarget, true ),
            "<mo>"      => ( Dalamud.Targets.MouseOverTarget, true ),
            "mouseover" => ( Dalamud.Targets.MouseOverTarget, true ),
            _           => ( null, false ),
        };
        return ret;
    }

    public void RedrawObject( int tableIndex, RedrawType settings )
    {
        if( tableIndex >= 0 && tableIndex < Dalamud.Objects.Length )
        {
            RedrawObject( Dalamud.Objects[ tableIndex ], settings );
        }
    }

    public void RedrawObject( string name, RedrawType settings )
    {
        var lowerName = name.ToLowerInvariant();
        if( GetName( lowerName, out var target ) )
        {
            RedrawObject( target, settings );
        }
        else
        {
            foreach( var actor in Dalamud.Objects.Where( a => a.Name.ToString().ToLowerInvariant() == lowerName ) )
            {
                RedrawObject( actor, settings );
            }
        }
    }

    public void RedrawAll( RedrawType settings )
    {
        foreach( var actor in Dalamud.Objects )
        {
            RedrawObject( actor, settings );
        }
    }
}