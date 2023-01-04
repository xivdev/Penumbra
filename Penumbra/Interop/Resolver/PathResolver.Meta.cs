using System;
using System.Linq;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using OtterGui.Classes;
using Penumbra.Collections;
using Penumbra.GameData.Enums;
using Penumbra.Util;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;
using static Penumbra.GameData.Enums.GenderRace;

namespace Penumbra.Interop.Resolver;

// State: 6.08 Hotfix.
// GetSlotEqpData seems to be the only function using the EQP table.
// It is only called by CheckSlotsForUnload (called by UpdateModels),
// SetupModelAttributes (called by UpdateModels and OnModelLoadComplete)
// and a unnamed function called by UpdateRender.
// It seems to be enough to change the EQP entries for UpdateModels.

// GetEqdpDataFor[Adults|Children|Other] seem to be the only functions using the EQDP tables.
// They are called by ResolveMdlPath, UpdateModels and SetupConnectorModelAttributes,
// which is called by SetupModelAttributes, which is called by OnModelLoadComplete and UpdateModels.
// It seems to be enough to change EQDP on UpdateModels and ResolveMDLPath.

// EST entries seem to be obtained by "44 8B C9 83 EA ?? 74", which is only called by
// ResolveSKLBPath, ResolveSKPPath, ResolvePHYBPath and indirectly by ResolvePAPPath.

// RSP height entries seem to be obtained by "E8 ?? ?? ?? ?? 48 8B 8E ?? ?? ?? ?? 44 8B CF"
// RSP tail entries seem to be obtained by "E8 ?? ?? ?? ?? 0F 28 F0 48 8B 05"
// RSP bust size entries seem to be obtained by  "E8 ?? ?? ?? ?? F2 0F 10 44 24 ?? 8B 44 24 ?? F2 0F 11 45 ?? 89 45 ?? 83 FF"
// they all are called by many functions, but the most relevant seem to be Human.SetupFromCharacterData, which is only called by CharacterBase.Create,
// ChangeCustomize and RspSetupCharacter, which is hooked here.

// GMP Entries seem to be only used by "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", which has a DrawObject as its first parameter.
public unsafe partial class PathResolver
{
    public class MetaState : IDisposable
    {
        public MetaState( IntPtr* humanVTable )
        {
            SignatureHelper.Initialise( this );
            _onModelLoadCompleteHook = Hook< OnModelLoadCompleteDelegate >.FromAddress( humanVTable[ 58 ], OnModelLoadCompleteDetour );
        }

        public void Enable()
        {
            _getEqpIndirectHook.Enable();
            _updateModelsHook.Enable();
            _onModelLoadCompleteHook.Enable();
            _setupVisorHook.Enable();
            _rspSetupCharacterHook.Enable();
            _changeCustomize.Enable();
        }

        public void Disable()
        {
            _getEqpIndirectHook.Disable();
            _updateModelsHook.Disable();
            _onModelLoadCompleteHook.Disable();
            _setupVisorHook.Disable();
            _rspSetupCharacterHook.Disable();
            _changeCustomize.Disable();
        }

        public void Dispose()
        {
            _getEqpIndirectHook.Dispose();
            _updateModelsHook.Dispose();
            _onModelLoadCompleteHook.Dispose();
            _setupVisorHook.Dispose();
            _rspSetupCharacterHook.Dispose();
            _changeCustomize.Dispose();
        }

        private delegate void                                OnModelLoadCompleteDelegate( IntPtr drawObject );
        private readonly Hook< OnModelLoadCompleteDelegate > _onModelLoadCompleteHook;

        private void OnModelLoadCompleteDetour( IntPtr drawObject )
        {
            var       collection = GetResolveData( drawObject );
            using var eqp        = collection.ModCollection.TemporarilySetEqpFile();
            using var eqdp       = ResolveEqdpData( collection.ModCollection, GetDrawObjectGenderRace( drawObject ), true, true );
            _onModelLoadCompleteHook.Original.Invoke( drawObject );
        }

        private delegate void UpdateModelDelegate( IntPtr drawObject );

        [Signature( "48 8B ?? 56 48 83 ?? ?? ?? B9", DetourName = nameof( UpdateModelsDetour ) )]
        private readonly Hook< UpdateModelDelegate > _updateModelsHook = null!;

        private void UpdateModelsDetour( IntPtr drawObject )
        {
            // Shortcut because this is called all the time.
            // Same thing is checked at the beginning of the original function.
            if( *( int* )( drawObject + 0x90c ) == 0 )
            {
                return;
            }
            using var performance = Penumbra.Performance.Measure( PerformanceType.UpdateModels );

            var       collection = GetResolveData( drawObject );
            using var eqp        = collection.ModCollection.TemporarilySetEqpFile();
            using var eqdp       = ResolveEqdpData( collection.ModCollection, GetDrawObjectGenderRace( drawObject ), true, true );
            _updateModelsHook.Original.Invoke( drawObject );
        }

        private static GenderRace GetDrawObjectGenderRace( IntPtr drawObject )
        {
            var draw = ( DrawObject* )drawObject;
            if( draw->Object.GetObjectType() == ObjectType.CharacterBase )
            {
                var c = ( CharacterBase* )drawObject;
                if( c->GetModelType() == CharacterBase.ModelType.Human )
                {
                    return GetHumanGenderRace( drawObject );
                }
            }

            return Unknown;
        }

        public static GenderRace GetHumanGenderRace( IntPtr human )
            => ( GenderRace )( ( Human* )human )->RaceSexId;

        [Signature( "40 ?? 48 83 ?? ?? ?? 81 ?? ?? ?? ?? ?? 48 8B ?? 74 ?? ?? 83 ?? ?? ?? ?? ?? ?? 74 ?? 4C",
            DetourName = nameof( GetEqpIndirectDetour ) )]
        private readonly Hook< OnModelLoadCompleteDelegate > _getEqpIndirectHook = null!;

        private void GetEqpIndirectDetour( IntPtr drawObject )
        {
            // Shortcut because this is also called all the time.
            // Same thing is checked at the beginning of the original function.
            if( ( *( byte* )( drawObject + 0xa30 ) & 1 ) == 0 || *( ulong* )( drawObject + 0xa28 ) == 0 )
            {
                return;
            }
            using var performance = Penumbra.Performance.Measure( PerformanceType.GetEqp );
            var       resolveData = GetResolveData( drawObject );
            using var eqp         = resolveData.ModCollection.TemporarilySetEqpFile();
            _getEqpIndirectHook.Original( drawObject );
        }


        // GMP. This gets called every time when changing visor state, and it accesses the gmp file itself,
        // but it only applies a changed gmp file after a redraw for some reason.
        private delegate byte SetupVisorDelegate( IntPtr drawObject, ushort modelId, byte visorState );

        [Signature( "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", DetourName = nameof( SetupVisorDetour ) )]
        private readonly Hook< SetupVisorDelegate > _setupVisorHook = null!;

        private byte SetupVisorDetour( IntPtr drawObject, ushort modelId, byte visorState )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.SetupVisor );
            var       resolveData = GetResolveData( drawObject );
            using var gmp         = resolveData.ModCollection.TemporarilySetGmpFile();
            return _setupVisorHook.Original( drawObject, modelId, visorState );
        }

        // RSP
        private delegate void RspSetupCharacterDelegate( IntPtr drawObject, IntPtr unk2, float unk3, IntPtr unk4, byte unk5 );

        [Signature( "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 88 54 24 ?? 57 41 56", DetourName = nameof( RspSetupCharacterDetour ) )]
        private readonly Hook< RspSetupCharacterDelegate > _rspSetupCharacterHook = null!;

        private void RspSetupCharacterDetour( IntPtr drawObject, IntPtr unk2, float unk3, IntPtr unk4, byte unk5 )
        {
            if( _inChangeCustomize )
            {
                _rspSetupCharacterHook.Original( drawObject, unk2, unk3, unk4, unk5 );
            }
            else
            {
                using var performance = Penumbra.Performance.Measure( PerformanceType.SetupCharacter );
                var       resolveData = GetResolveData( drawObject );
                using var cmp         = resolveData.ModCollection.TemporarilySetCmpFile();
                _rspSetupCharacterHook.Original( drawObject, unk2, unk3, unk4, unk5 );
            }
        }

        // ChangeCustomize calls RspSetupCharacter, so skip the additional cmp change.
        private          bool _inChangeCustomize;
        private delegate bool ChangeCustomizeDelegate( IntPtr human, IntPtr data, byte skipEquipment );

        [Signature( "E8 ?? ?? ?? ?? 41 0F B6 C5 66 41 89 86", DetourName = nameof( ChangeCustomizeDetour ) )]
        private readonly Hook< ChangeCustomizeDelegate > _changeCustomize = null!;

        private bool ChangeCustomizeDetour( IntPtr human, IntPtr data, byte skipEquipment )
        {
            using var performance = Penumbra.Performance.Measure( PerformanceType.ChangeCustomize );
            _inChangeCustomize = true;
            var       resolveData = GetResolveData( human );
            using var cmp         = resolveData.ModCollection.TemporarilySetCmpFile();
            using var decals      = new CharacterUtility.DecalReverter( resolveData.ModCollection, DrawObjectState.UsesDecal( 0, data ) );
            var       ret         = _changeCustomize.Original( human, data, skipEquipment );
            _inChangeCustomize = false;
            return ret;
        }

        public static DisposableContainer ResolveEqdpData( ModCollection collection, GenderRace race, bool equipment, bool accessory )
        {
            var races = race.Dependencies();
            if( races.Length == 0 )
            {
                return DisposableContainer.Empty;
            }

            var equipmentEnumerable = equipment
                ? races.Select( r => collection.TemporarilySetEqdpFile( r, false ) )
                : Array.Empty< IDisposable? >().AsEnumerable();
            var accessoryEnumerable = accessory
                ? races.Select( r => collection.TemporarilySetEqdpFile( r, true ) )
                : Array.Empty< IDisposable? >().AsEnumerable();
            return new DisposableContainer( equipmentEnumerable.Concat( accessoryEnumerable ) );
        }
    }
}