using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    private ModCollection? _animationLoadCollection;
    private ModCollection? _lastAvfxCollection = null;

    public delegate ulong LoadTimelineResourcesDelegate( IntPtr timeline );

    // The timeline object loads the requested .tmb and .pap files. The .tmb files load the respective .avfx files.
    // We can obtain the associated game object from the timelines 28'th vfunc and use that to apply the correct collection.
    [Signature( "E8 ?? ?? ?? ?? 83 7F ?? ?? 75 ?? 0F B6 87", DetourName = nameof( LoadTimelineResourcesDetour ) )]
    public Hook< LoadTimelineResourcesDelegate >? LoadTimelineResourcesHook;

    private ulong LoadTimelineResourcesDetour( IntPtr timeline )
    {
        ulong ret;
        var   old = _animationLoadCollection;
        try
        {
            var getGameObjectIdx = ( ( delegate* unmanaged < IntPtr, int>** )timeline )[ 0 ][ 28 ];
            var idx              = getGameObjectIdx( timeline );
            if( idx >= 0 && idx < Dalamud.Objects.Length )
            {
                var obj = Dalamud.Objects[ idx ];
                _animationLoadCollection = obj != null ? IdentifyCollection( ( GameObject* )obj.Address ) : null;
            }
            else
            {
                _animationLoadCollection = null;
            }
        }
        finally
        {
            ret = LoadTimelineResourcesHook!.Original( timeline );
        }

        _animationLoadCollection = old;

        return ret;
    }

    // Probably used when the base idle animation gets loaded.
    // Make it aware of the correct collection to load the correct pap files.
    [Signature( "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CF 44 8B C2 E8 ?? ?? ?? ?? 48 8B 05", DetourName = "CharacterBaseLoadAnimationDetour" )]
    public Hook< CharacterBaseDestructorDelegate >? CharacterBaseLoadAnimationHook;

    private void CharacterBaseLoadAnimationDetour( IntPtr drawObject )
    {
        var last = _animationLoadCollection;
        _animationLoadCollection = _lastCreatedCollection
         ?? ( FindParent( drawObject, out var collection ) != null ? collection : Penumbra.CollectionManager.Default );
        CharacterBaseLoadAnimationHook!.Original( drawObject );
        _animationLoadCollection = last;
    }

    public delegate ulong LoadSomeAvfx( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 );

    [Signature( "E8 ?? ?? ?? ?? 45 0F B6 F7", DetourName = nameof( LoadSomeAvfxDetour ) )]
    public Hook< LoadSomeAvfx >? LoadSomeAvfxHook;

    private ulong LoadSomeAvfxDetour( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 )
    {
        var last = _animationLoadCollection;
        _animationLoadCollection = IdentifyCollection( ( GameObject* )gameObject );
        var ret = LoadSomeAvfxHook!.Original( a1, gameObject, gameObject2, unk1, unk2, unk3 );
        _animationLoadCollection = last;
        return ret;
    }

    // Unknown what exactly this is but it seems to load a bunch of paps.
    public delegate void LoadSomePap( IntPtr a1, int a2, IntPtr a3, int a4 );

    [Signature( "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC ?? 41 8B D9 89 51" )]
    public Hook< LoadSomePap >? LoadSomePapHook;

    private void LoadSomePapDetour( IntPtr a1, int a2, IntPtr a3, int a4 )
    {
        var timelinePtr = a1 + 0x50;
        var last        = _animationLoadCollection;
        if( timelinePtr != IntPtr.Zero )
        {
            var actorIdx = ( int )( *( *( ulong** )timelinePtr + 1 ) >> 3 );
            if( actorIdx >= 0 && actorIdx < Dalamud.Objects.Length )
            {
                _animationLoadCollection = IdentifyCollection( ( GameObject* )( Dalamud.Objects[ actorIdx ]?.Address ?? IntPtr.Zero ) );
            }
        }

        LoadSomePapHook!.Original( a1, a2, a3, a4 );
        _animationLoadCollection = last;
    }

    // Seems to load character actions when zoning or changing class, maybe.
    [Signature( "E8 ?? ?? ?? ?? C6 83 ?? ?? ?? ?? ?? 8B 8E", DetourName = nameof( SomeActionLoadDetour ) )]
    public Hook< CharacterBaseDestructorDelegate >? SomeActionLoadHook;

    private void SomeActionLoadDetour( IntPtr gameObject )
    {
        var last = _animationLoadCollection;
        _animationLoadCollection = IdentifyCollection( ( GameObject* )gameObject );
        SomeActionLoadHook!.Original( gameObject );
        _animationLoadCollection = last;
    }

    [Signature( "E8 ?? ?? ?? ?? 44 84 BB", DetourName = nameof( SomeOtherAvfxDetour ) )]
    public Hook< CharacterBaseDestructorDelegate >? SomeOtherAvfxHook;

    private void SomeOtherAvfxDetour( IntPtr unk )
    {
        var last       = _animationLoadCollection;
        var gameObject = ( GameObject* )( unk - 0x8B0 );
        _animationLoadCollection = IdentifyCollection( gameObject );
        SomeOtherAvfxHook!.Original( unk );
        _animationLoadCollection = last;
    }
}