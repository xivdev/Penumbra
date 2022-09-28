using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Penumbra.Interop.Resolver;

public class CutsceneCharacters : IDisposable
{
    public const int CutsceneStartIdx = 200;
    public const int CutsceneSlots    = 40;
    public const int CutsceneEndIdx   = CutsceneStartIdx + CutsceneSlots;

    private readonly short[] _copiedCharacters = Enumerable.Repeat( ( short )-1, CutsceneSlots ).ToArray();

    public IEnumerable< KeyValuePair< int, global::Dalamud.Game.ClientState.Objects.Types.GameObject > > Actors
        => Enumerable.Range( CutsceneStartIdx, CutsceneSlots )
           .Where( i => Dalamud.Objects[ i ] != null )
           .Select( i => KeyValuePair.Create( i, this[ i ] ?? Dalamud.Objects[ i ]! ) );

    public CutsceneCharacters()
    {
        SignatureHelper.Initialise( this );
        Dalamud.Conditions.ConditionChange += Reset;
    }

    // Get the related actor to a cutscene actor.
    // Does not check for valid input index.
    // Returns null if no connected actor is set or the actor does not exist anymore.
    public global::Dalamud.Game.ClientState.Objects.Types.GameObject? this[ int idx ]
    {
        get
        {
            Debug.Assert( idx is >= CutsceneStartIdx and < CutsceneEndIdx );
            idx = _copiedCharacters[ idx - CutsceneStartIdx ];
            return idx < 0 ? null : Dalamud.Objects[ idx ];
        }
    }

    // Return the currently set index of a parent or -1 if none is set or the index is invalid.
    public int GetParentIndex( int idx )
    {
        if( idx is >= CutsceneStartIdx and < CutsceneEndIdx )
        {
            return _copiedCharacters[ idx - CutsceneStartIdx ];
        }

        return -1;
    }

    public void Reset( ConditionFlag flag, bool value )
    {
        switch( flag )
        {
            case ConditionFlag.BetweenAreas:
            case ConditionFlag.BetweenAreas51:
                if( !value )
                {
                    return;
                }

                break;
            case ConditionFlag.OccupiedInCutSceneEvent:
            case ConditionFlag.WatchingCutscene:
            case ConditionFlag.WatchingCutscene78:
                if( value )
                {
                    return;
                }

                break;
            default: return;
        }

        for( var i = 0; i < _copiedCharacters.Length; ++i )
        {
            _copiedCharacters[ i ] = -1;
        }
    }

    public void Enable()
        => _copyCharacterHook.Enable();

    public void Disable()
        => _copyCharacterHook.Disable();

    public void Dispose()
    {
        _copyCharacterHook.Dispose();
        Dalamud.Conditions.ConditionChange -= Reset;
    }

    private unsafe delegate ulong CopyCharacterDelegate( GameObject* target, GameObject* source, uint unk );

    [Signature( "E8 ?? ?? ?? ?? 0F B6 9F ?? ?? ?? ?? 48 8D 8F", DetourName = nameof( CopyCharacterDetour ) )]
    private readonly Hook< CopyCharacterDelegate > _copyCharacterHook = null!;

    private unsafe ulong CopyCharacterDetour( GameObject* target, GameObject* source, uint unk )
    {
        try
        {
            if( target != null && target->ObjectIndex is >= CutsceneStartIdx and < CutsceneEndIdx )
            {
                var parent = source == null || source->ObjectIndex is < 0 or >= CutsceneStartIdx
                    ? -1
                    : source->ObjectIndex;
                _copiedCharacters[ target->ObjectIndex - CutsceneStartIdx ] = ( short )parent;
                Penumbra.Log.Debug( $"Set cutscene character {target->ObjectIndex} to {parent}." );
            }
        }
        catch
        {
            // ignored
        }

        return _copyCharacterHook.Original( target, source, unk );
    }
}