using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.String.Classes;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    public class AnimationState
    {
        private readonly DrawObjectState _drawObjectState;

        private ResolveData _animationLoadData = ResolveData.Invalid;
        private ResolveData _lastAvfxData      = ResolveData.Invalid;

        public AnimationState( DrawObjectState drawObjectState )
        {
            _drawObjectState = drawObjectState;
            SignatureHelper.Initialise( this );
        }

        public bool HandleFiles( ResourceType type, Utf8GamePath _, out ResolveData resolveData )
        {
            switch( type )
            {
                case ResourceType.Tmb:
                case ResourceType.Pap:
                case ResourceType.Scd:
                    if( _animationLoadData.Valid )
                    {
                        resolveData = _animationLoadData;
                        return true;
                    }

                    break;
                case ResourceType.Avfx:
                    _lastAvfxData = _animationLoadData.Valid
                        ? _animationLoadData
                        : Penumbra.CollectionManager.Default.ToResolveData();
                    if( _animationLoadData.Valid )
                    {
                        resolveData = _animationLoadData;
                        return true;
                    }

                    break;
                case ResourceType.Atex:
                    if( _lastAvfxData.Valid )
                    {
                        resolveData = _lastAvfxData;
                        return true;
                    }

                    if( _animationLoadData.Valid )
                    {
                        resolveData = _animationLoadData;
                        return true;
                    }

                    break;
            }

            resolveData = ResolveData.Invalid;
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
            var   old = _animationLoadData;
            try
            {
                if( timeline != IntPtr.Zero )
                {
                    var getGameObjectIdx = ( ( delegate* unmanaged< IntPtr, int >** )timeline )[ 0 ][ 28 ];
                    var idx              = getGameObjectIdx( timeline );
                    if( idx >= 0 && idx < Dalamud.Objects.Length )
                    {
                        var obj = Dalamud.Objects[ idx ];
                        _animationLoadData = obj != null ? IdentifyCollection( ( GameObject* )obj.Address ) : ResolveData.Invalid;
                    }
                    else
                    {
                        _animationLoadData = ResolveData.Invalid;
                    }
                }
            }
            finally
            {
                ret = _loadTimelineResourcesHook.Original( timeline );
            }

            _animationLoadData = old;

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
            var last = _animationLoadData;
            _animationLoadData = _drawObjectState.LastCreatedCollection.Valid
                ? _drawObjectState.LastCreatedCollection
                : FindParent( drawObject, out var collection ) != null
                    ? collection
                    : Penumbra.CollectionManager.Default.ToResolveData();
            _characterBaseLoadAnimationHook.Original( drawObject );
            _animationLoadData = last;
        }


        public delegate ulong LoadSomeAvfx( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 );

        [Signature( "E8 ?? ?? ?? ?? 45 0F B6 F7", DetourName = nameof( LoadSomeAvfxDetour ) )]
        private readonly Hook< LoadSomeAvfx > _loadSomeAvfxHook = null!;

        private ulong LoadSomeAvfxDetour( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 )
        {
            var last = _animationLoadData;
            _animationLoadData = IdentifyCollection( ( GameObject* )gameObject );
            var ret = _loadSomeAvfxHook.Original( a1, gameObject, gameObject2, unk1, unk2, unk3 );
            _animationLoadData = last;
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
            var last        = _animationLoadData;
            if( timelinePtr != IntPtr.Zero )
            {
                var actorIdx = ( int )( *( *( ulong** )timelinePtr + 1 ) >> 3 );
                if( actorIdx >= 0 && actorIdx < Dalamud.Objects.Length )
                {
                    _animationLoadData = IdentifyCollection( ( GameObject* )( Dalamud.Objects[ actorIdx ]?.Address ?? IntPtr.Zero ) );
                }
            }

            _loadSomePapHook.Original( a1, a2, a3, a4 );
            _animationLoadData = last;
        }

        // Seems to load character actions when zoning or changing class, maybe.
        [Signature( "E8 ?? ?? ?? ?? C6 83 ?? ?? ?? ?? ?? 8B 8E", DetourName = nameof( SomeActionLoadDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _someActionLoadHook = null!;

        private void SomeActionLoadDetour( IntPtr gameObject )
        {
            var last = _animationLoadData;
            _animationLoadData = IdentifyCollection( ( GameObject* )gameObject );
            _someActionLoadHook.Original( gameObject );
            _animationLoadData = last;
        }

        [Signature( "E8 ?? ?? ?? ?? 44 84 A3", DetourName = nameof( SomeOtherAvfxDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _someOtherAvfxHook = null!;

        private void SomeOtherAvfxDetour( IntPtr unk )
        {
            var last       = _animationLoadData;
            var gameObject = ( GameObject* )( unk - 0x8D0 );
            _animationLoadData = IdentifyCollection( gameObject );
            _someOtherAvfxHook.Original( unk );
            _animationLoadData = last;
        }
    }
}