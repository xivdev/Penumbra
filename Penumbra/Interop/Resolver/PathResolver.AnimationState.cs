using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.GameData;
using Penumbra.GameData.Enums;
using Penumbra.Interop.Structs;
using Penumbra.String;
using Penumbra.String.Classes;
using Penumbra.Util;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace Penumbra.Interop.Resolver;

public unsafe partial class PathResolver
{
    public class AnimationState
    {
        private readonly DrawObjectState _drawObjectState;

        private ResolveData _animationLoadData  = ResolveData.Invalid;
        private ResolveData _characterSoundData = ResolveData.Invalid;

        public AnimationState( DrawObjectState drawObjectState )
        {
            _drawObjectState = drawObjectState;
            SignatureHelper.Initialise( this );
        }

        public bool HandleFiles( ResourceType type, Utf8GamePath _, out ResolveData resolveData )
        {
            switch( type )
            {
                case ResourceType.Scd:
                    if( _characterSoundData.Valid )
                    {
                        resolveData = _characterSoundData;
                        return true;
                    }

                    if( _animationLoadData.Valid )
                    {
                        resolveData = _animationLoadData;
                        return true;
                    }

                    break;
                case ResourceType.Tmb:
                case ResourceType.Pap:
                case ResourceType.Avfx:
                case ResourceType.Atex:
                    if( _animationLoadData.Valid )
                    {
                        resolveData = _animationLoadData;
                        return true;
                    }

                    break;
            }

            if( _drawObjectState.LastGameObject != null )
            {
                resolveData = _drawObjectState.LastCreatedCollection;
                return true;
            }

            resolveData = ResolveData.Invalid;
            return false;
        }

        public void Enable()
        {
            _loadTimelineResourcesHook.Enable();
            _characterBaseLoadAnimationHook.Enable();
            _loadSomePapHook.Enable();
            _someActionLoadHook.Enable();
            _loadCharacterSoundHook.Enable();
            _loadCharacterVfxHook.Enable();
            _loadAreaVfxHook.Enable();
            _scheduleClipUpdateHook.Enable();

            //_loadSomeAvfxHook.Enable();
            //_someOtherAvfxHook.Enable();
        }

        public void Disable()
        {
            _loadTimelineResourcesHook.Disable();
            _characterBaseLoadAnimationHook.Disable();
            _loadSomePapHook.Disable();
            _someActionLoadHook.Disable();
            _loadCharacterSoundHook.Disable();
            _loadCharacterVfxHook.Disable();
            _loadAreaVfxHook.Disable();
            _scheduleClipUpdateHook.Disable();

            //_loadSomeAvfxHook.Disable();
            //_someOtherAvfxHook.Disable();
        }

        public void Dispose()
        {
            _loadTimelineResourcesHook.Dispose();
            _characterBaseLoadAnimationHook.Dispose();
            _loadSomePapHook.Dispose();
            _someActionLoadHook.Dispose();
            _loadCharacterSoundHook.Dispose();
            _loadCharacterVfxHook.Dispose();
            _loadAreaVfxHook.Dispose();
            _scheduleClipUpdateHook.Dispose();

            //_loadSomeAvfxHook.Dispose();
            //_someOtherAvfxHook.Dispose();
        }

        // Characters load some of their voice lines or whatever with this function.
        private delegate IntPtr LoadCharacterSound( IntPtr character, int unk1, int unk2, IntPtr unk3, ulong unk4, int unk5, int unk6, ulong unk7 );

        [Signature( Sigs.LoadCharacterSound, DetourName = nameof( LoadCharacterSoundDetour ) )]
        private readonly Hook< LoadCharacterSound > _loadCharacterSoundHook = null!;

        private IntPtr LoadCharacterSoundDetour( IntPtr character, int unk1, int unk2, IntPtr unk3, ulong unk4, int unk5, int unk6, ulong unk7 )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadSound );
            var       last        = _characterSoundData;
            _characterSoundData = IdentifyCollection( ( GameObject* )character, true );
            var ret = _loadCharacterSoundHook.Original( character, unk1, unk2, unk3, unk4, unk5, unk6, unk7 );
            _characterSoundData = last;
            return ret;
        }

        private static ResolveData GetDataFromTimeline( IntPtr timeline )
        {
            try
            {
                if( timeline != IntPtr.Zero )
                {
                    var getGameObjectIdx = ( ( delegate* unmanaged< IntPtr, int >** )timeline )[ 0 ][ Offsets.GetGameObjectIdxVfunc ];
                    var idx              = getGameObjectIdx( timeline );
                    if( idx >= 0 && idx < Dalamud.Objects.Length )
                    {
                        var obj = Dalamud.Objects[ idx ];
                        return obj != null ? IdentifyCollection( ( GameObject* )obj.Address, true ) : ResolveData.Invalid;
                    }
                }
            }
            catch( Exception e )
            {
                Penumbra.Log.Error( $"Error getting timeline data for 0x{timeline:X}:\n{e}" );
            }

            return ResolveData.Invalid;
        }

        // The timeline object loads the requested .tmb and .pap files. The .tmb files load the respective .avfx files.
        // We can obtain the associated game object from the timelines 28'th vfunc and use that to apply the correct collection.
        private delegate ulong LoadTimelineResourcesDelegate( IntPtr timeline );

        [Signature( Sigs.LoadTimelineResources, DetourName = nameof( LoadTimelineResourcesDetour ) )]
        private readonly Hook< LoadTimelineResourcesDelegate > _loadTimelineResourcesHook = null!;

        private ulong LoadTimelineResourcesDetour( IntPtr timeline )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.TimelineResources );
            var       old         = _animationLoadData;
            _animationLoadData = GetDataFromTimeline( timeline );
            var ret = _loadTimelineResourcesHook.Original( timeline );
            _animationLoadData = old;
            return ret;
        }

        // Probably used when the base idle animation gets loaded.
        // Make it aware of the correct collection to load the correct pap files.
        private delegate void CharacterBaseNoArgumentDelegate( IntPtr drawBase );

        [Signature( Sigs.CharacterBaseLoadAnimation, DetourName = nameof( CharacterBaseLoadAnimationDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _characterBaseLoadAnimationHook = null!;

        private void CharacterBaseLoadAnimationDetour( IntPtr drawObject )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadCharacterBaseAnimation );
            var       last        = _animationLoadData;
            _animationLoadData = _drawObjectState.LastCreatedCollection.Valid
                ? _drawObjectState.LastCreatedCollection
                : FindParent( drawObject, out var collection ) != null
                    ? collection
                    : Penumbra.CollectionManager.Default.ToResolveData();
            _characterBaseLoadAnimationHook.Original( drawObject );
            _animationLoadData = last;
        }

        // Unknown what exactly this is but it seems to load a bunch of paps.
        private delegate void LoadSomePap( IntPtr a1, int a2, IntPtr a3, int a4 );

        [Signature( Sigs.LoadSomePap, DetourName = nameof( LoadSomePapDetour ) )]
        private readonly Hook< LoadSomePap > _loadSomePapHook = null!;

        private void LoadSomePapDetour( IntPtr a1, int a2, IntPtr a3, int a4 )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadPap );
            var       timelinePtr = a1 + Offsets.TimeLinePtr;
            var       last        = _animationLoadData;
            if( timelinePtr != IntPtr.Zero )
            {
                var actorIdx = ( int )( *( *( ulong** )timelinePtr + 1 ) >> 3 );
                if( actorIdx >= 0 && actorIdx < Dalamud.Objects.Length )
                {
                    _animationLoadData = IdentifyCollection( ( GameObject* )( Dalamud.Objects[ actorIdx ]?.Address ?? IntPtr.Zero ), true );
                }
            }

            _loadSomePapHook.Original( a1, a2, a3, a4 );
            _animationLoadData = last;
        }

        // Seems to load character actions when zoning or changing class, maybe.
        [Signature( Sigs.LoadSomeAction, DetourName = nameof( SomeActionLoadDetour ) )]
        private readonly Hook< CharacterBaseNoArgumentDelegate > _someActionLoadHook = null!;

        private void SomeActionLoadDetour( IntPtr gameObject )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadAction );
            var       last        = _animationLoadData;
            _animationLoadData = IdentifyCollection( ( GameObject* )gameObject, true );
            _someActionLoadHook.Original( gameObject );
            _animationLoadData = last;
        }

        private delegate IntPtr LoadCharacterVfxDelegate( byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4 );

        [Signature( Sigs.LoadCharacterVfx, DetourName = nameof( LoadCharacterVfxDetour ) )]
        private readonly Hook< LoadCharacterVfxDelegate > _loadCharacterVfxHook = null!;

        private global::Dalamud.Game.ClientState.Objects.Types.GameObject? GetOwnedObject( uint id )
        {
            var owner = Dalamud.Objects.SearchById( id );
            if( owner == null )
            {
                return null;
            }

            var idx = ( ( GameObject* )owner.Address )->ObjectIndex;
            return Dalamud.Objects[ idx + 1 ];
        }

        private IntPtr LoadCharacterVfxDetour( byte* vfxPath, VfxParams* vfxParams, byte unk1, byte unk2, float unk3, int unk4 )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadCharacterVfx );
            var       last        = _animationLoadData;
            if( vfxParams != null && vfxParams->GameObjectId != unchecked( ( uint )-1 ) )
            {
                var obj = vfxParams->GameObjectType switch
                {
                    0 => Dalamud.Objects.SearchById( vfxParams->GameObjectId ),
                    2 => Dalamud.Objects[ ( int )vfxParams->GameObjectId ],
                    4 => GetOwnedObject( vfxParams->GameObjectId ),
                    _ => null,
                };
                _animationLoadData = obj != null
                    ? IdentifyCollection( ( GameObject* )obj.Address, true )
                    : ResolveData.Invalid;
            }
            else
            {
                _animationLoadData = ResolveData.Invalid;
            }

            var ret = _loadCharacterVfxHook.Original( vfxPath, vfxParams, unk1, unk2, unk3, unk4 );
#if DEBUG
            var path = new ByteString( vfxPath );
            Penumbra.Log.Verbose(
                $"Load Character VFX: {path}  {vfxParams->GameObjectId:X} {vfxParams->TargetCount} {unk1} {unk2} {unk3} {unk4} -> {ret:X} {_animationLoadData.ModCollection.Name} {_animationLoadData.AssociatedGameObject} {last.ModCollection.Name} {last.AssociatedGameObject}" );
#endif
            _animationLoadData = last;
            return ret;
        }

        private delegate IntPtr LoadAreaVfxDelegate( uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3 );

        [Signature( Sigs.LoadAreaVfx, DetourName = nameof( LoadAreaVfxDetour ) )]
        private readonly Hook< LoadAreaVfxDelegate > _loadAreaVfxHook = null!;

        private IntPtr LoadAreaVfxDetour( uint vfxId, float* pos, GameObject* caster, float unk1, float unk2, byte unk3 )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.LoadAreaVfx );
            var       last        = _animationLoadData;
            if( caster != null )
            {
                _animationLoadData = IdentifyCollection( caster, true );
            }
            else
            {
                _animationLoadData = ResolveData.Invalid;
            }

            var ret = _loadAreaVfxHook.Original( vfxId, pos, caster, unk1, unk2, unk3 );
#if DEBUG
            Penumbra.Log.Verbose(
                $"Load Area VFX: {vfxId}, {pos[ 0 ]} {pos[ 1 ]} {pos[ 2 ]} {( caster != null ? new ByteString( caster->GetName() ).ToString() : "Unknown" )} {unk1} {unk2} {unk3} -> {ret:X} {_animationLoadData.ModCollection.Name} {_animationLoadData.AssociatedGameObject} {last.ModCollection.Name} {last.AssociatedGameObject}" );
#endif
            _animationLoadData = last;
            return ret;
        }


        private delegate void ScheduleClipUpdate( ClipScheduler* x );

        [Signature( Sigs.ScheduleClipUpdate, DetourName = nameof( ScheduleClipUpdateDetour ) )]
        private readonly Hook< ScheduleClipUpdate > _scheduleClipUpdateHook = null!;

        private void ScheduleClipUpdateDetour( ClipScheduler* x )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.ScheduleClipUpdate );
            var       old         = _animationLoadData;
            var       timeline    = x->SchedulerTimeline;
            _animationLoadData = GetDataFromTimeline( timeline );
            _scheduleClipUpdateHook.Original( x );
            _animationLoadData = old;
        }

        // ========== Those hooks seem to be superseded by LoadCharacterVfx =========

        // public delegate ulong LoadSomeAvfx( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 );
        // 
        // [Signature( "E8 ?? ?? ?? ?? 45 0F B6 F7", DetourName = nameof( LoadSomeAvfxDetour ) )]
        // private readonly Hook<LoadSomeAvfx> _loadSomeAvfxHook = null!;
        // 
        // private ulong LoadSomeAvfxDetour( uint a1, IntPtr gameObject, IntPtr gameObject2, float unk1, IntPtr unk2, IntPtr unk3 )
        // {
        //     var last = _animationLoadData;
        //     _animationLoadData = IdentifyCollection( ( GameObject* )gameObject, true );
        //     var ret = _loadSomeAvfxHook.Original( a1, gameObject, gameObject2, unk1, unk2, unk3 );
        //     _animationLoadData = last;
        //     return ret;
        // }
        // 
        // [Signature( "E8 ?? ?? ?? ?? 44 84 A3", DetourName = nameof( SomeOtherAvfxDetour ) )]
        // private readonly Hook<CharacterBaseNoArgumentDelegate> _someOtherAvfxHook = null!;
        // 
        // private void SomeOtherAvfxDetour( IntPtr unk )
        // {
        //     var last       = _animationLoadData;
        //     var gameObject = ( GameObject* )( unk - 0x8D0 );
        //     _animationLoadData = IdentifyCollection( gameObject, true );
        //     _someOtherAvfxHook.Original( unk );
        //     _animationLoadData = last;
        // }
    }
}