using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Penumbra.Collections;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    private ModCollection? _animationLoadCollection;

    public delegate byte LoadTimelineResourcesDelegate( IntPtr timeline );

    // The timeline object loads the requested .tmb and .pap files. The .tmb files load the respective .avfx files.
    // We can obtain the associated game object from the timelines 28'th vfunc and use that to apply the correct collection.
    [Signature( "E8 ?? ?? ?? ?? 83 7F ?? ?? 75 ?? 0F B6 87", DetourName = nameof( LoadTimelineResourcesDetour ) )]
    public Hook< LoadTimelineResourcesDelegate >? LoadTimelineResourcesHook;

    private byte LoadTimelineResourcesDetour( IntPtr timeline )
    {
        byte ret;
        var  old = _animationLoadCollection;
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

    public delegate ulong LoadSomeAvfx( uint a1, IntPtr gameObject, IntPtr gameObject2 );

    [Signature( "E8 ?? ?? ?? ?? 45 0F B6 F7", DetourName = nameof( LoadSomeAvfxDetour ) )]
    public Hook< LoadSomeAvfx >? LoadSomeAvfxHook;

    private ulong LoadSomeAvfxDetour( uint a1, IntPtr gameObject, IntPtr gameObject2 )
    {
        var last = _animationLoadCollection;
        _animationLoadCollection = IdentifyCollection( ( GameObject* )gameObject );
        var ret = LoadSomeAvfxHook!.Original( a1, gameObject, gameObject2 );
        _animationLoadCollection = last;
        return ret;
    }
}