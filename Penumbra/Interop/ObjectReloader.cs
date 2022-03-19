using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.Mods;

namespace Penumbra.Interop;

public unsafe class ObjectReloader : IDisposable
{
    private delegate void ManipulateDraw( IntPtr actor );

    private const uint NpcObjectId    = unchecked( ( uint )-536870912 );
    public const  int  GPosePlayerIdx = 201;
    public const  int  GPoseEndIdx    = GPosePlayerIdx + 48;

    private readonly ModManager                                         _mods;
    private readonly Queue< (uint actorId, string name, RedrawType s) > _actorIds = new();

    private int        _currentFrame;
    private bool       _changedSettings;
    private uint       _currentObjectId         = uint.MaxValue;
    private DrawState  _currentObjectStartState = 0;
    private RedrawType _currentRedrawType       = RedrawType.Unload;
    private string?    _currentObjectName;
    private bool       _wasTarget;
    private bool       _inGPose;

    public static DrawState* ActorDrawState( GameObject actor )
        => ( DrawState* )( actor.Address + 0x0104 );

    private static void DisableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 17 ]( actor.Address );

    private static void EnableDraw( GameObject actor )
        => ( ( delegate* unmanaged< IntPtr, void >** )actor.Address )[ 0 ][ 16 ]( actor.Address );

    public ObjectReloader( ModManager mods )
        => _mods = mods;

    private void ChangeSettings()
    {
        if( _currentObjectName != null && _mods.Collections.CharacterCollection.TryGetValue( _currentObjectName, out var collection ) )
        {
            _changedSettings = true;
            _mods.Collections.SetActiveCollection( collection, _currentObjectName );
        }
    }

    private void RestoreSettings()
    {
        _mods.Collections.ResetActiveCollection();
        _changedSettings = false;
    }

    private void WriteInvisible( GameObject actor, int actorIdx )
    {
        _currentObjectStartState =  *ActorDrawState( actor );
        *ActorDrawState( actor ) |= DrawState.Invisibility;

        if( _inGPose )
        {
            DisableDraw( actor );
        }
    }

    private bool StillLoading( DrawState* renderPtr )
    {
        const DrawState stillLoadingFlags = DrawState.SomeNpcFlag
          | DrawState.MaybeCulled
          | DrawState.MaybeHiddenMinion
          | DrawState.MaybeHiddenSummon;

        if( renderPtr != null )
        {
            var loadingFlags = *( DrawState* )renderPtr;
            if( loadingFlags == _currentObjectStartState )
            {
                return false;
            }

            return !( loadingFlags == 0 || ( loadingFlags & stillLoadingFlags ) != 0 );
        }

        return false;
    }

    private void WriteVisible( GameObject actor, int actorIdx )
    {
        *ActorDrawState( actor ) &= ~DrawState.Invisibility;

        if( _inGPose )
        {
            EnableDraw( actor );
        }
    }

    private bool CheckObject( GameObject actor )
    {
        if( _currentObjectId != actor.ObjectId )
        {
            return false;
        }

        if( _currentObjectId != NpcObjectId )
        {
            return true;
        }

        return _currentObjectName == actor.Name.ToString();
    }

    private bool CheckObjectGPose( GameObject actor )
        => actor.ObjectId == NpcObjectId && _currentObjectName == actor.Name.ToString();

    private (GameObject?, int) FindCurrentObject()
    {
        if( _inGPose )
        {
            for( var i = GPosePlayerIdx; i < GPoseEndIdx; ++i )
            {
                var actor = Dalamud.Objects[ i ];
                if( actor == null )
                {
                    break;
                }

                if( CheckObjectGPose( actor ) )
                {
                    return ( actor, i );
                }
            }
        }

        for( var i = 0; i < Dalamud.Objects.Length; ++i )
        {
            if( i == GPosePlayerIdx )
            {
                i = GPoseEndIdx;
            }

            var actor = Dalamud.Objects[ i ];
            if( actor != null && CheckObject( actor ) )
            {
                return ( actor, i );
            }
        }

        return ( null, -1 );
    }

    private void PopObject()
    {
        if( _actorIds.Count > 0 )
        {
            var (id, name, s)  = _actorIds.Dequeue();
            _currentObjectName = name;
            _currentObjectId   = id;
            _currentRedrawType = s;
            var (actor, _)     = FindCurrentObject();
            if( actor == null )
            {
                return;
            }

            _wasTarget = actor.Address == Dalamud.Targets.Target?.Address;

            ++_currentFrame;
        }
        else
        {
            Dalamud.Framework.Update -= OnUpdateEvent;
        }
    }

    private void ApplySettingsOrRedraw()
    {
        var (actor, idx) = FindCurrentObject();
        if( actor == null )
        {
            _currentFrame = 0;
            return;
        }

        switch( _currentRedrawType )
        {
            case RedrawType.Unload:
                WriteInvisible( actor, idx );
                _currentFrame = 0;
                break;
            case RedrawType.RedrawWithSettings:
                ChangeSettings();
                ++_currentFrame;
                break;
            case RedrawType.RedrawWithoutSettings:
                WriteVisible( actor, idx );
                _currentFrame = 0;
                break;
            case RedrawType.WithoutSettings:
                WriteInvisible( actor, idx );
                ++_currentFrame;
                break;
            case RedrawType.WithSettings:
                ChangeSettings();
                WriteInvisible( actor, idx );
                ++_currentFrame;
                break;
            case RedrawType.OnlyWithSettings:
                ChangeSettings();
                if( !_changedSettings )
                {
                    return;
                }

                WriteInvisible( actor, idx );
                ++_currentFrame;
                break;
            case RedrawType.AfterGPoseWithSettings:
            case RedrawType.AfterGPoseWithoutSettings:
                if( _inGPose )
                {
                    _actorIds.Enqueue( ( _currentObjectId, _currentObjectName!, _currentRedrawType ) );
                    _currentFrame = 0;
                }
                else
                {
                    _currentRedrawType = _currentRedrawType == RedrawType.AfterGPoseWithSettings
                        ? RedrawType.WithSettings
                        : RedrawType.WithoutSettings;
                }

                break;
            default: throw new InvalidEnumArgumentException();
        }
    }

    private void StartRedrawAndWait()
    {
        var (actor, idx) = FindCurrentObject();
        if( actor == null )
        {
            RevertSettings();
            return;
        }

        WriteVisible( actor, idx );
        _currentFrame = _changedSettings || _wasTarget ? _currentFrame + 1 : 0;
    }

    private void RevertSettings()
    {
        var (actor, _) = FindCurrentObject();
        if( actor != null )
        {
            if( !StillLoading( ActorDrawState( actor ) ) )
            {
                RestoreSettings();
                if( _wasTarget && Dalamud.Targets.Target == null )
                {
                    Dalamud.Targets.SetTarget( actor );
                }

                _currentFrame = 0;
            }
        }
        else
        {
            _currentFrame = 0;
        }
    }

    private void OnUpdateEvent( object framework )
    {
        if( Dalamud.Conditions[ ConditionFlag.BetweenAreas51 ]
        || Dalamud.Conditions[ ConditionFlag.BetweenAreas ]
        || Dalamud.Conditions[ ConditionFlag.OccupiedInCutSceneEvent ] )
        {
            return;
        }

        _inGPose = Dalamud.Objects[ GPosePlayerIdx ] != null;

        switch( _currentFrame )
        {
            case 0:
                PopObject();
                break;
            case 1:
                ApplySettingsOrRedraw();
                break;
            case 2:
                StartRedrawAndWait();
                break;
            case 3:
                RevertSettings();
                break;
            default:
                _currentFrame = 0;
                break;
        }
    }

    private void RedrawObjectIntern( uint objectId, string actorName, RedrawType settings )
    {
        if( _actorIds.Contains( ( objectId, actorName, settings ) ) )
        {
            return;
        }

        _actorIds.Enqueue( ( objectId, actorName, settings ) );
        if( _actorIds.Count == 1 )
        {
            Dalamud.Framework.Update += OnUpdateEvent;
        }
    }

    public void RedrawObject( GameObject? actor, RedrawType settings = RedrawType.WithSettings )
    {
        if( actor != null )
        {
            RedrawObjectIntern( actor.ObjectId, actor.Name.ToString(), RedrawType.WithoutSettings ); // TODO settings );
        }
    }

    private GameObject? GetLocalPlayer()
    {
        var gPosePlayer = Dalamud.Objects[ GPosePlayerIdx ];
        return gPosePlayer ?? Dalamud.Objects[ 0 ];
    }

    private GameObject? GetName( string name )
    {
        var lowerName = name.ToLowerInvariant();
        return lowerName switch
        {
            ""          => null,
            "<me>"      => GetLocalPlayer(),
            "self"      => GetLocalPlayer(),
            "<t>"       => Dalamud.Targets.Target,
            "target"    => Dalamud.Targets.Target,
            "<f>"       => Dalamud.Targets.FocusTarget,
            "focus"     => Dalamud.Targets.FocusTarget,
            "<mo>"      => Dalamud.Targets.MouseOverTarget,
            "mouseover" => Dalamud.Targets.MouseOverTarget,
            _ => Dalamud.Objects.FirstOrDefault(
                a => string.Equals( a.Name.ToString(), lowerName, StringComparison.InvariantCultureIgnoreCase ) ),
        };
    }

    public void RedrawObject( string name, RedrawType settings = RedrawType.WithSettings )
        => RedrawObject( GetName( name ), settings );

    public void RedrawAll( RedrawType settings = RedrawType.WithSettings )
    {
        Clear();
        foreach( var actor in Dalamud.Objects )
        {
            RedrawObject( actor, settings );
        }
    }

    public void Clear()
    {
        RestoreSettings();
        _currentFrame = 0;
    }

    public void Dispose()
    {
        RevertSettings();
        _actorIds.Clear();
        Dalamud.Framework.Update -= OnUpdateEvent;
    }
}