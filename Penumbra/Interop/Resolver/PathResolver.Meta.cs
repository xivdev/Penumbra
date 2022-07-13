using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using Penumbra.Collections;
using Penumbra.Meta.Files;
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
// and RspSetupCharacter, which is hooked here.

// GMP Entries seem to be only used by "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", which has a DrawObject as its first parameter.

public unsafe partial class PathResolver
{
    public delegate void UpdateModelDelegate( IntPtr drawObject );

    [Signature( "48 8B ?? 56 48 83 ?? ?? ?? B9", DetourName = "UpdateModelsDetour" )]
    public Hook< UpdateModelDelegate >? UpdateModelsHook;

    private void UpdateModelsDetour( IntPtr drawObject )
    {
        // Shortcut because this is called all the time.
        // Same thing is checked at the beginning of the original function.
        if( *( int* )( drawObject + 0x90c ) == 0 )
        {
            return;
        }

        var collection = GetCollection( drawObject );
        if( collection != null )
        {
            using var eqp  = MetaChanger.ChangeEqp( collection );
            using var eqdp = MetaChanger.ChangeEqdp( collection );
            UpdateModelsHook!.Original.Invoke( drawObject );
        }
        else
        {
            UpdateModelsHook!.Original.Invoke( drawObject );
        }
    }

    [Signature( "40 ?? 48 83 ?? ?? ?? 81 ?? ?? ?? ?? ?? 48 8B ?? 74 ?? ?? 83 ?? ?? ?? ?? ?? ?? 74 ?? 4C",
        DetourName = "GetEqpIndirectDetour" )]
    public Hook< OnModelLoadCompleteDelegate >? GetEqpIndirectHook;

    private void GetEqpIndirectDetour( IntPtr drawObject )
    {
        // Shortcut because this is also called all the time.
        // Same thing is checked at the beginning of the original function.
        if( ( *( byte* )( drawObject + 0xa30 ) & 1 ) == 0 || *( ulong* )( drawObject + 0xa28 ) == 0 )
        {
            return;
        }

        using var eqp = MetaChanger.ChangeEqp( this, drawObject );
        GetEqpIndirectHook!.Original( drawObject );
    }

    public Hook< OnModelLoadCompleteDelegate >? OnModelLoadCompleteHook;

    private void OnModelLoadCompleteDetour( IntPtr drawObject )
    {
        var collection = GetCollection( drawObject );
        if( collection != null )
        {
            using var eqp  = MetaChanger.ChangeEqp( collection );
            using var eqdp = MetaChanger.ChangeEqdp( collection );
            OnModelLoadCompleteHook!.Original.Invoke( drawObject );
        }
        else
        {
            OnModelLoadCompleteHook!.Original.Invoke( drawObject );
        }
    }

    // GMP. This gets called every time when changing visor state, and it accesses the gmp file itself,
    // but it only applies a changed gmp file after a redraw for some reason.
    public delegate byte SetupVisorDelegate( IntPtr drawObject, ushort modelId, byte visorState );

    [Signature( "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", DetourName = "SetupVisorDetour" )]
    public Hook< SetupVisorDelegate >? SetupVisorHook;

    private byte SetupVisorDetour( IntPtr drawObject, ushort modelId, byte visorState )
    {
        using var gmp = MetaChanger.ChangeGmp( this, drawObject );
        return SetupVisorHook!.Original( drawObject, modelId, visorState );
    }

    // RSP
    public delegate void RspSetupCharacterDelegate( IntPtr drawObject, IntPtr unk2, float unk3, IntPtr unk4, byte unk5 );

    [Signature( "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 88 54 24 ?? 57 41 56", DetourName = "RspSetupCharacterDetour" )]
    public Hook< RspSetupCharacterDelegate >? RspSetupCharacterHook;

    private void RspSetupCharacterDetour( IntPtr drawObject, IntPtr unk2, float unk3, IntPtr unk4, byte unk5 )
    {
        using var rsp = MetaChanger.ChangeCmp( this, drawObject );
        RspSetupCharacterHook!.Original( drawObject, unk2, unk3, unk4, unk5 );
    }

    private void SetupMetaHooks()
    {
        OnModelLoadCompleteHook =
            Hook< OnModelLoadCompleteDelegate >.FromAddress( DrawObjectHumanVTable[ OnModelLoadCompleteIdx ], OnModelLoadCompleteDetour );
    }

    private void EnableMetaHooks()
    {
        GetEqpIndirectHook?.Enable();
        UpdateModelsHook?.Enable();
        OnModelLoadCompleteHook?.Enable();
        SetupVisorHook?.Enable();
        RspSetupCharacterHook?.Enable();
    }

    private void DisableMetaHooks()
    {
        GetEqpIndirectHook?.Disable();
        UpdateModelsHook?.Disable();
        OnModelLoadCompleteHook?.Disable();
        SetupVisorHook?.Disable();
        RspSetupCharacterHook?.Disable();
    }

    private void DisposeMetaHooks()
    {
        GetEqpIndirectHook?.Dispose();
        UpdateModelsHook?.Dispose();
        OnModelLoadCompleteHook?.Dispose();
        SetupVisorHook?.Dispose();
        RspSetupCharacterHook?.Dispose();
    }

    private ModCollection? GetCollection( IntPtr drawObject )
    {
        var parent = FindParent( drawObject, out var collection );
        if( parent == null || collection == Penumbra.CollectionManager.Default )
        {
            return null;
        }

        return collection.HasCache ? collection : null;
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

        public static MetaChanger ChangeEqp( PathResolver resolver, IntPtr drawObject )
        {
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                return ChangeEqp( collection );
            }
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        // We only need to change anything if it is actually equipment here.
        public static MetaChanger ChangeEqdp( PathResolver resolver, IntPtr drawObject, uint modelType )
        {
            if( modelType < 10 )
            {
                var collection = resolver.GetCollection( drawObject );
                if( collection != null )
                {
                    return ChangeEqdp( collection );
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
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                collection.SetGmpFiles();
                return new MetaChanger( MetaManipulation.Type.Gmp );
            }
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEst( PathResolver resolver, IntPtr drawObject )
        {
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                collection.SetEstFiles();
                return new MetaChanger( MetaManipulation.Type.Est );
            }
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeCmp( PathResolver resolver, out ModCollection? collection )
        {
            if( resolver.LastGameObject != null )
            {
                collection = IdentifyCollection( resolver.LastGameObject );
                if( collection != Penumbra.CollectionManager.Default && collection.HasCache )
                {
                    collection.SetCmpFiles();
                    return new MetaChanger( MetaManipulation.Type.Rsp );
                }
            }
            else
            {
                collection = null;
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeCmp( PathResolver resolver, IntPtr drawObject )
        {
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                collection.SetCmpFiles();
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