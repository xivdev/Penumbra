using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Penumbra.Collections;
using Penumbra.Meta.Manipulations;

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
    public unsafe class MetaState : IDisposable
    {
        private readonly PathResolver _parent;

        public MetaState( PathResolver parent, IntPtr* humanVTable )
        {
            SignatureHelper.Initialise( this );
            _parent                  = parent;
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
            var collection = GetResolveData( drawObject );
            if( collection.Valid )
            {
                using var eqp  = MetaChanger.ChangeEqp( collection.ModCollection );
                using var eqdp = MetaChanger.ChangeEqdp( collection.ModCollection );
                _onModelLoadCompleteHook.Original.Invoke( drawObject );
            }
            else
            {
                _onModelLoadCompleteHook.Original.Invoke( drawObject );
            }
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

            var collection = GetResolveData( drawObject );
            if( collection.Valid )
            {
                using var eqp  = MetaChanger.ChangeEqp( collection.ModCollection );
                using var eqdp = MetaChanger.ChangeEqdp( collection.ModCollection );
                _updateModelsHook.Original.Invoke( drawObject );
            }
            else
            {
                _updateModelsHook.Original.Invoke( drawObject );
            }
        }

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

            using var eqp = MetaChanger.ChangeEqp( _parent, drawObject );
            _getEqpIndirectHook.Original( drawObject );
        }


        // GMP. This gets called every time when changing visor state, and it accesses the gmp file itself,
        // but it only applies a changed gmp file after a redraw for some reason.
        private delegate byte SetupVisorDelegate( IntPtr drawObject, ushort modelId, byte visorState );

        [Signature( "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", DetourName = nameof( SetupVisorDetour ) )]
        private readonly Hook< SetupVisorDelegate > _setupVisorHook = null!;

        private byte SetupVisorDetour( IntPtr drawObject, ushort modelId, byte visorState )
        {
            using var gmp = MetaChanger.ChangeGmp( _parent, drawObject );
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
                using var rsp = MetaChanger.ChangeCmp( _parent, drawObject );
                _rspSetupCharacterHook.Original( drawObject, unk2, unk3, unk4, unk5 );
            }
        }

        // ChangeCustomize calls RspSetupCharacter, so skip the additional cmp change.
        private          bool _inChangeCustomize = false;
        private delegate bool ChangeCustomizeDelegate( IntPtr human, IntPtr data, byte skipEquipment );

        [Signature( "E8 ?? ?? ?? ?? 41 0F B6 C5 66 41 89 86", DetourName = nameof( ChangeCustomizeDetour ) )]
        private readonly Hook< ChangeCustomizeDelegate > _changeCustomize = null!;

        private bool ChangeCustomizeDetour( IntPtr human, IntPtr data, byte skipEquipment )
        {
            _inChangeCustomize = true;
            using var rsp = MetaChanger.ChangeCmp( _parent, human );
            return _changeCustomize.Original( human, data, skipEquipment );
        }
    }

    // Small helper to handle setting metadata and reverting it at the end of the function.
    // Since eqp and eqdp may be called multiple times in a row, we need to count them,
    // so that we do not reset the files too early.
    private readonly struct MetaChanger : IDisposable
    {
        private static   int                   _eqpCounter;
        private static   int                   _eqdpCounter;
        private readonly MetaManipulation.Type _type;

        private MetaChanger( MetaManipulation.Type type )
        {
            _type = type;
            if( type == MetaManipulation.Type.Eqp )
            {
                ++_eqpCounter;
            }
            else if( type == MetaManipulation.Type.Eqdp )
            {
                ++_eqdpCounter;
            }
        }

        public static MetaChanger ChangeEqp( ModCollection collection )
        {
            collection.SetEqpFiles();
            return new MetaChanger( MetaManipulation.Type.Eqp );
        }

        public static MetaChanger ChangeEqp( PathResolver _, IntPtr drawObject )
        {
            var resolveData = GetResolveData( drawObject );
            if( resolveData.Valid )
            {
                return ChangeEqp( resolveData.ModCollection );
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        // We only need to change anything if it is actually equipment here.
        public static MetaChanger ChangeEqdp( PathResolver _, IntPtr drawObject, uint modelType )
        {
            if( modelType < 10 )
            {
                var collection = GetResolveData( drawObject );
                if( collection.Valid )
                {
                    return ChangeEqdp( collection.ModCollection );
                }
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEqdp( ModCollection collection )
        {
            collection.SetEqdpFiles();
            return new MetaChanger( MetaManipulation.Type.Eqdp );
        }

        public static MetaChanger ChangeGmp( PathResolver resolver, IntPtr drawObject )
        {
            var resolveData = GetResolveData( drawObject );
            if( resolveData.Valid )
            {
                resolveData.ModCollection.SetGmpFiles();
                return new MetaChanger( MetaManipulation.Type.Gmp );
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEst( PathResolver resolver, IntPtr drawObject )
        {
            var resolveData = GetResolveData( drawObject );
            if( resolveData.Valid )
            {
                resolveData.ModCollection.SetEstFiles();
                return new MetaChanger( MetaManipulation.Type.Est );
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeCmp( GameObject* gameObject, out ResolveData resolveData )
        {
            if( gameObject != null )
            {
                resolveData = IdentifyCollection( gameObject );
                if( resolveData.ModCollection != Penumbra.CollectionManager.Default && resolveData.ModCollection.HasCache )
                {
                    resolveData.ModCollection.SetCmpFiles();
                    return new MetaChanger( MetaManipulation.Type.Rsp );
                }
            }
            else
            {
                resolveData = ResolveData.Invalid;
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeCmp( PathResolver resolver, IntPtr drawObject )
        {
            var resolveData = GetResolveData( drawObject );
            if( resolveData.Valid )
            {
                resolveData.ModCollection.SetCmpFiles();
                return new MetaChanger( MetaManipulation.Type.Rsp );
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public void Dispose()
        {
            switch( _type )
            {
                case MetaManipulation.Type.Eqdp:
                    if( --_eqdpCounter == 0 )
                    {
                        Penumbra.CollectionManager.Default.SetEqdpFiles();
                    }

                    break;
                case MetaManipulation.Type.Eqp:
                    if( --_eqpCounter == 0 )
                    {
                        Penumbra.CollectionManager.Default.SetEqpFiles();
                    }

                    break;
                case MetaManipulation.Type.Est:
                    Penumbra.CollectionManager.Default.SetEstFiles();
                    break;
                case MetaManipulation.Type.Gmp:
                    Penumbra.CollectionManager.Default.SetGmpFiles();
                    break;
                case MetaManipulation.Type.Rsp:
                    Penumbra.CollectionManager.Default.SetCmpFiles();
                    break;
            }
        }
    }
}