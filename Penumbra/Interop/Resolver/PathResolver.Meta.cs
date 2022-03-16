using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;

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

// RSP entries seem to be obtained by "E8 ?? ?? ?? ?? 48 8B 8E ?? ?? ?? ?? 44 8B CF", or maybe "E8 ?? ?? ?? ?? 0F 28 F0 48 8B 05",
// possibly also "E8 ?? ?? ?? ?? F2 0F 10 44 24 ?? 8B 44 24 ?? F2 0F 11 45 ?? 89 45 ?? 83 FF"
// which is called by a lot of functions, but the mostly relevant is probably Human.SetupFromCharacterData, which is only called by CharacterBase.Create.

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

    // GMP
    public delegate void ApplyVisorDelegate( IntPtr drawObject, IntPtr unk1, float unk2, IntPtr unk3, ushort unk4, char unk5 );

    [Signature( "48 8B ?? 53 55 57 48 83 ?? ?? 48 8B", DetourName = "ApplyVisorDetour" )]
    public Hook< ApplyVisorDelegate >? ApplyVisorHook;

    private void ApplyVisorDetour( IntPtr drawObject, IntPtr unk1, float unk2, IntPtr unk3, ushort unk4, char unk5 )
    {
        using var gmp = MetaChanger.ChangeGmp( this, drawObject );
        ApplyVisorHook!.Original( drawObject, unk1, unk2, unk3, unk4, unk5 );
    }

    private void SetupMetaHooks()
    {
        OnModelLoadCompleteHook =
            new Hook< OnModelLoadCompleteDelegate >( DrawObjectHumanVTable[ OnModelLoadCompleteIdx ], OnModelLoadCompleteDetour );
    }

    private void EnableMetaHooks()
    {
#if USE_EQP
        GetEqpIndirectHook?.Enable();
#endif
#if USE_EQP || USE_EQDP
        UpdateModelsHook?.Enable();
        OnModelLoadCompleteHook?.Enable();
#endif
#if USE_GMP
        ApplyVisorHook?.Enable();
#endif
    }

    private void DisableMetaHooks()
    {
        GetEqpIndirectHook?.Disable();
        UpdateModelsHook?.Disable();
        OnModelLoadCompleteHook?.Disable();
        ApplyVisorHook?.Disable();
    }

    private void DisposeMetaHooks()
    {
        GetEqpIndirectHook?.Dispose();
        UpdateModelsHook?.Dispose();
        OnModelLoadCompleteHook?.Dispose();
        ApplyVisorHook?.Dispose();
    }

    private ModCollection? GetCollection( IntPtr drawObject )
    {
        var parent = FindParent( drawObject, out var collection );
        if( parent == null || collection == Penumbra.ModManager.Collections.DefaultCollection )
        {
            return null;
        }

        return collection.Cache == null ? Penumbra.ModManager.Collections.ForcedCollection : collection;
    }


    // Small helper to handle setting metadata and reverting it at the end of the function.
    private readonly struct MetaChanger : IDisposable
    {
        private readonly MetaManipulation.Type _type;

        private MetaChanger( MetaManipulation.Type type )
            => _type = type;

        public static MetaChanger ChangeEqp( ModCollection collection )
        {
#if USE_EQP
            collection.SetEqpFiles();
            return new MetaChanger( MetaManipulation.Type.Eqp );
#else
            return new MetaChanger( MetaManipulation.Type.Unknown );
#endif
        }

        public static MetaChanger ChangeEqp( PathResolver resolver, IntPtr drawObject )
        {
#if USE_EQP
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                return ChangeEqp( collection );
            }
#endif
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEqdp( PathResolver resolver, IntPtr drawObject )
        {
#if USE_EQDP
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                return ChangeEqdp( collection );
            }
#endif
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEqdp( ModCollection collection )
        {
#if USE_EQDP
            collection.SetEqdpFiles();
            return new MetaChanger( MetaManipulation.Type.Eqdp );
#else
            return new MetaChanger( MetaManipulation.Type.Unknown );
#endif
        }

        public static MetaChanger ChangeGmp( PathResolver resolver, IntPtr drawObject )
        {
#if USE_GMP
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                collection.SetGmpFiles();
                return new MetaChanger( MetaManipulation.Type.Gmp );
            }
#endif
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeEst( PathResolver resolver, IntPtr drawObject )
        {
#if USE_EST
            var collection = resolver.GetCollection( drawObject );
            if( collection != null )
            {
                collection.SetEstFiles();
                return new MetaChanger( MetaManipulation.Type.Est );
            }
#endif
            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public static MetaChanger ChangeCmp( PathResolver resolver, out ModCollection? collection )
        {
            if( resolver.LastGameObject != null )
            {
                collection = IdentifyCollection( resolver.LastGameObject );
#if USE_CMP
                if( collection != Penumbra.ModManager.Collections.DefaultCollection && collection.Cache != null )
                {
                    collection.SetCmpFiles();
                    return new MetaChanger( MetaManipulation.Type.Rsp );
                }
#endif
            }
            else
            {
                collection = null;
            }

            return new MetaChanger( MetaManipulation.Type.Unknown );
        }

        public void Dispose()
        {
            switch( _type )
            {
                case MetaManipulation.Type.Eqdp:
                    Penumbra.ModManager.Collections.DefaultCollection.SetEqdpFiles();
                    break;
                case MetaManipulation.Type.Eqp:
                    Penumbra.ModManager.Collections.DefaultCollection.SetEqpFiles();
                    break;
                case MetaManipulation.Type.Est:
                    Penumbra.ModManager.Collections.DefaultCollection.SetEstFiles();
                    break;
                case MetaManipulation.Type.Gmp:
                    Penumbra.ModManager.Collections.DefaultCollection.SetGmpFiles();
                    break;
                case MetaManipulation.Type.Rsp:
                    Penumbra.ModManager.Collections.DefaultCollection.SetCmpFiles();
                    break;
            }
        }
    }
}