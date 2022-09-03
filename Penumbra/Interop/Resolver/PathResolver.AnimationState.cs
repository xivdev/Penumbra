using System;
using System.Diagnostics.CodeAnalysis;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData.ByteString;
using Penumbra.GameData.Enums;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    public class AnimationState
    {
        private readonly DrawObjectState _drawObjectState;

        private LinkedModCollection? _animationLoadCollection;
        private LinkedModCollection? _lastAvfxCollection;

        public AnimationState( DrawObjectState drawObjectState )
        {
            _drawObjectState = drawObjectState;
            SignatureHelper.Initialise( this );
        }

        public bool HandleFiles( ResourceType type, Utf8GamePath _, [NotNullWhen( true )] out LinkedModCollection? collection )
        {
            switch( type )
            {
                case ResourceType.Tmb:
                case ResourceType.Pap:
                case ResourceType.Scd:
                    if( _animationLoadCollection != null )
                    {
                        collection = _animationLoadCollection;
                        return true;
                    }

                    break;
                case ResourceType.Avfx:
                    _lastAvfxCollection = _animationLoadCollection ?? new LinkedModCollection(Penumbra.CollectionManager.Default);
                    if( _animationLoadCollection != null )
                    {
                        collection = _animationLoadCollection;
                        return true;
                    }

                    break;
                case ResourceType.Atex:
                    if( _lastAvfxCollection != null )
                    {
                        collection = _lastAvfxCollection;
                        return true;
                    }

                    if( _animationLoadCollection != null )
                    {
                        collection = _animationLoadCollection;
                        return true;
                    }

                    break;
            }

            collection = null;
            return false;
        }

        public void Enable()
        {
            _loadTimelineResourcesHook.Enable();
            _characterBaseLoadAnimationHook.Enable();
            _loadSomeAvfxHook.Enable();
            _loadSomePapHook.Enable();
            _someActionLoadHook.Enable();
            _someOtherAvfxHook.Enable();
        }

        public void Disable()
        {
            _loadTimelineResourcesHook.Disable();
            _characterBaseLoadAnimationHook.Disable();
            _loadSomeAvfxHook.Disable();
            _loadSomePapHook.Disable();
            _someActionLoadHook.Disable();
            _someOtherAvfxHook.Disable();
        }

        public void Dispose()
        {
            _loadTimelineResourcesHook.Dispose();
            _characterBaseLoadAnimationHook.Dispose();
            _loadSomeAvfxHook.Dispose();
            _loadSomePapHook.Dispose();
            _someActionLoadHook.Dispose();
            _someOtherAvfxHook.Dispose();
        }

        // The timeline object loads the requested .tmb and .pap files. The .tmb files load the respective .avfx files.
        // We can obtain the associated game object from the timelines 28'th vfunc and use that to apply the correct collection.
        private delegate ulong LoadTimelineResourcesDelegate( IntPtr timeline );

        [Signature( "E8 ?? ?? ?? ?? 83 7F ?? ?? 75 ?? 0F B6 87", DetourName = nameof( LoadTimelineResourcesDetour ) )]
        private readonly Hook< LoadTimelineResourcesDelegate > _loadTimelineResourcesHook = null!;

        private ulong LoadTimelineResourcesDetour( IntPtr timeline )
        {
            ulong ret;
            var   old = _animationLoadCollection;
            try
            {
                if( timeline != IntPtr.Zero )
                {
                    var getGameObjectIdx = ( ( delegate* unmanaged< IntPtr, int >** )timeline )[ 0 ][ 28 ];
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
            }
            finally
            {
                ret = _loadTimelineResourcesHook.Original( timeline );
            }

            _animationLoadCollection = old;

            return ret;
        }

        // Probably used when the base idle animation gets loaded.
        // Make it aware of the correct collection to load the correct pap files.
        private delegate void CharacterBaseNoArgumentDelegate( IntPtr drawBase );

        [Signature( "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CF 44 8B C2 E8 ?? ?? ?? ?? 48 8B 05",
            DetourName = nameof( CharacterBaseLoadAnimationDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _characterBaseLoadAnimationHook = null!;

        private void CharacterBaseLoadAnimationDetour( IntPtr drawObject )
        {
            var last = _animationLoadCollection;
            _animationLoadCollection = _drawObjectState.LastCreatedCollection
             ?? ( FindParent( drawObject, out var collection ) != null ? collection : new LinkedModCollection(Penumbra.CollectionManager.Default) );
            _characterBaseLoadAnimationHook.Original( drawObject );
            _animationLoadCollection = last;
        }


        public delegate ulong LoadSomeAvfx( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 );

        [Signature( "E8 ?? ?? ?? ?? 45 0F B6 F7", DetourName = nameof( LoadSomeAvfxDetour ) )]
        private readonly Hook< LoadSomeAvfx > _loadSomeAvfxHook = null!;

        private ulong LoadSomeAvfxDetour( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 )
        {
            var last = _animationLoadCollection;
            _animationLoadCollection = IdentifyCollection( ( GameObject* )gameObject );
            var ret = _loadSomeAvfxHook.Original( a1, gameObject, gameObject2, unk1, unk2, unk3 );
            _animationLoadCollection = last;
            return ret;
        }

        // Unknown what exactly this is but it seems to load a bunch of paps.
        private delegate void LoadSomePap( IntPtr a1, int a2, IntPtr a3, int a4 );

        [Signature( "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC ?? 41 8B D9 89 51",
            DetourName = nameof( LoadSomePapDetour ) )]
        private readonly Hook< LoadSomePap > _loadSomePapHook = null!;

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

            _loadSomePapHook.Original( a1, a2, a3, a4 );
            _animationLoadCollection = last;
        }

        // Seems to load character actions when zoning or changing class, maybe.
        [Signature( "E8 ?? ?? ?? ?? C6 83 ?? ?? ?? ?? ?? 8B 8E", DetourName = nameof( SomeActionLoadDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _someActionLoadHook = null!;

        private void SomeActionLoadDetour( IntPtr gameObject )
        {
            var last = _animationLoadCollection;
            _animationLoadCollection = IdentifyCollection( ( GameObject* )gameObject );
            _someActionLoadHook.Original( gameObject );
            _animationLoadCollection = last;
        }

        [Signature( "E8 ?? ?? ?? ?? 44 84 A3", DetourName = nameof( SomeOtherAvfxDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _someOtherAvfxHook = null!;

        private void SomeOtherAvfxDetour( IntPtr unk )
        {
            var last       = _animationLoadCollection;
            var gameObject = ( GameObject* )( unk - 0x8D0 );
            _animationLoadCollection = IdentifyCollection( gameObject );
            _someOtherAvfxHook.Original( unk );
            _animationLoadCollection = last;
        }
    }
}