using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;

namespace Penumbra.Interop;

public unsafe class ObjectReloader : IDisposable
{
    public const  int  GPosePlayerIdx = 201;
    public const  int  GPoseSlots     = 42;
    public const  int  GPoseEndIdx    = GPosePlayerIdx + GPoseSlots;

    private readonly List< int > _unloadedObjects   = new(Dalamud.Objects.Length);
    private readonly List< int > _afterGPoseObjects = new(GPoseSlots);
    private          int         _target            = -1;
    private          int         _waitFrame         = 0;

    public ObjectReloader()
        => Dalamud.Framework.Update += OnUpdateEvent;

    public void Dispose()
        => Dalamud.Framework.Update -= OnUpdateEvent;

    public static DrawState* ActorDrawState( GameObject actor )
        => ( DrawState* )( actor.Address + 0x0104 );

    private static void DisableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 17 ]( actor.Address );

    private static void EnableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 16 ]( actor.Address );

    private static int ObjectTableIndex( GameObject actor )
        => ( ( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* )actor.Address )->ObjectIndex;

    private static void WriteInvisible( GameObject? actor )
    {
        if( actor == null )
        {
            return;
        }

        *ActorDrawState( actor ) |= DrawState.Invisibility;

        if( ObjectTableIndex( actor ) is >= GPosePlayerIdx and < GPoseEndIdx )
        {
            DisableDraw( actor );
        }
    }

    private static void WriteVisible( GameObject? actor )
    {
        if( actor == null )
        {
            return;
        }

        *ActorDrawState( actor ) &= ~DrawState.Invisibility;

        if( ObjectTableIndex( actor ) is >= GPosePlayerIdx and < GPoseEndIdx )
        {
            EnableDraw( actor );
        }
    }

    private void ReloadActor( GameObject? actor )
    {
        if( actor != null )
        {
            WriteInvisible( actor );
            var idx = ObjectTableIndex( actor );
            if( actor.Address == Dalamud.Targets.Target?.Address )
            {
                _target = idx;
            }

            _unloadedObjects.Add( idx );
            _waitFrame = 1;
        }
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
            _afterGPoseObjects.Add( ObjectTableIndex( actor ) );
            _waitFrame = 1;
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
            _target = -1;
            return;
        }

        if( ( ( FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* )actor.Address )->DrawObject == null )
        {
            return;
        }

        Dalamud.Targets.SetTarget( actor );
        _target = -1;
    }

    private void HandleRedraw()
    {
        if( _unloadedObjects.Count == 0 )
        {
            return;
        }

        foreach( var idx in _unloadedObjects )
        {
            WriteVisible( Dalamud.Objects[ idx ] );
        }

        _unloadedObjects.Clear();
    }

    private void HandleAfterGPose()
    {
        if( _afterGPoseObjects.Count == 0 || Dalamud.Objects[ GPosePlayerIdx ] != null )
        {
            return;
        }

        foreach( var idx in _afterGPoseObjects )
        {
            WriteVisible( Dalamud.Objects[ idx ] );
        }

        _afterGPoseObjects.Clear();
    }

    private void OnUpdateEvent( object framework )
    {
        if( Dalamud.Conditions[ ConditionFlag.BetweenAreas51 ]
        || Dalamud.Conditions[ ConditionFlag.BetweenAreas ]
        || Dalamud.Conditions[ ConditionFlag.OccupiedInCutSceneEvent ] )
        {
            return;
        }

        if( _waitFrame > 0 )
        {
            --_waitFrame;
            return;
        }

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
            case RedrawType.Unload:
                WriteInvisible( actor );
                break;
            case RedrawType.Load:
                WriteVisible( actor );
                break;
            case RedrawType.AfterGPose:
                ReloadActorAfterGPose( actor );
                break;
            default: throw new ArgumentOutOfRangeException( nameof( settings ), settings, null );
        }
    }

    private GameObject? GetLocalPlayer()
    {
        var gPosePlayer = Dalamud.Objects[ GPosePlayerIdx ];
        return gPosePlayer ?? Dalamud.Objects[ 0 ];
    }

    private bool GetName( string lowerName, out GameObject? actor )
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