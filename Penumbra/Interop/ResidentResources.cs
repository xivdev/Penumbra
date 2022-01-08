using System;
using System.Runtime.InteropServices;
using Dalamud.Logging;
using Penumbra.Structs;
using Penumbra.Util;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop
{
    public class ResidentResources
    {
        private const int NumResources = 85;

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* LoadPlayerResourcesPrototype( IntPtr pResourceManager );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* UnloadPlayerResourcesPrototype( IntPtr pResourceManager );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* LoadCharacterResourcesPrototype( CharacterUtility* pCharacterResourceManager );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* UnloadCharacterResourcePrototype( IntPtr resource );


        public LoadPlayerResourcesPrototype LoadPlayerResources { get; }
        public UnloadPlayerResourcesPrototype UnloadPlayerResources { get; }
        public LoadCharacterResourcesPrototype LoadDataFiles { get; }
        public UnloadCharacterResourcePrototype UnloadCharacterResource { get; }

        // Object addresses
        private readonly IntPtr _residentResourceManagerAddress;

        public IntPtr ResidentResourceManager
            => Marshal.ReadIntPtr( _residentResourceManagerAddress );

        private readonly IntPtr _characterUtilityAddress;

        public unsafe CharacterUtility* CharacterUtility
            => ( CharacterUtility* )Marshal.ReadIntPtr( _characterUtilityAddress ).ToPointer();

        public ResidentResources()
        {
            var module = Dalamud.SigScanner.Module.BaseAddress.ToInt64();
            var loadPlayerResourcesAddress =
                Dalamud.SigScanner.ScanText(
                    "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B 48 30 48 8B 01 FF 50 10 48 85 C0 74 0A" );
            GeneralUtil.PrintDebugAddress( "LoadPlayerResources", loadPlayerResourcesAddress );

            var unloadPlayerResourcesAddress =
                Dalamud.SigScanner.ScanText(
                    "41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B E9 48 83 C1 08" );
            GeneralUtil.PrintDebugAddress( "UnloadPlayerResources", unloadPlayerResourcesAddress );

            var loadDataFilesAddress = Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" );
            GeneralUtil.PrintDebugAddress( "LoadDataFiles", loadDataFilesAddress );

            var unloadCharacterResourceAddress =
                Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? FF 4C 89 37 48 83 C7 08 48 83 ED 01 75 ?? 48 8B CB" );
            GeneralUtil.PrintDebugAddress( "UnloadCharacterResource", unloadCharacterResourceAddress );

            _residentResourceManagerAddress = Dalamud.SigScanner.GetStaticAddressFromSig( "0F 44 FE 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 05" );
            GeneralUtil.PrintDebugAddress( "ResidentResourceManager", _residentResourceManagerAddress );

            _characterUtilityAddress =
                Dalamud.SigScanner.GetStaticAddressFromSig( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" );
            GeneralUtil.PrintDebugAddress( "CharacterUtility", _characterUtilityAddress );

            LoadPlayerResources    = Marshal.GetDelegateForFunctionPointer< LoadPlayerResourcesPrototype >( loadPlayerResourcesAddress );
            UnloadPlayerResources  = Marshal.GetDelegateForFunctionPointer< UnloadPlayerResourcesPrototype >( unloadPlayerResourcesAddress );
            LoadDataFiles = Marshal.GetDelegateForFunctionPointer< LoadCharacterResourcesPrototype >( loadDataFilesAddress );
            UnloadCharacterResource =
                Marshal.GetDelegateForFunctionPointer< UnloadCharacterResourcePrototype >( unloadCharacterResourceAddress );
        }

        // Forces the reload of a specific set of 85 files, notably containing the eqp, eqdp, gmp and est tables, by filename.
        public unsafe void ReloadResidentResources()
        {
            ReloadCharacterResources();

            UnloadPlayerResources( ResidentResourceManager );
            LoadPlayerResources( ResidentResourceManager );
        }

        public unsafe string ResourceToPath( byte* resource )
            => Marshal.PtrToStringAnsi( new IntPtr( *( char** )( resource + 9 * 8 ) ) )!;

        private unsafe void ReloadCharacterResources()
        {
            var oldResources = new IntPtr[NumResources];
            var resources    = new IntPtr( &CharacterUtility->Resources );
            var pResources   = ( void** )resources.ToPointer();

            Marshal.Copy( resources, oldResources, 0, NumResources );

            LoadDataFiles( CharacterUtility );

            for( var i = 0; i < NumResources; i++ )
            {
                var handle = ( ResourceHandle* )oldResources[ i ];
                if( oldResources[ i ].ToPointer() == pResources[ i ] )
                {
                    PluginLog.Verbose( $"Unchanged resource: {ResourceToPath( ( byte* )oldResources[ i ].ToPointer() )}" );
                    ( ( ResourceHandle* )oldResources[ i ] )->DecRef();
                    continue;
                }

                PluginLog.Debug( "Freeing "
                  + $"{ResourceToPath( ( byte* )oldResources[ i ].ToPointer() )}, replaced with "
                  + $"{ResourceToPath( ( byte* )pResources[ i ] )}" );

                UnloadCharacterResource( oldResources[ i ] );

                // Temporary fix against crashes?
                if( handle->RefCount <= 0 )
                {
                    handle->RefCount = 1;
                    handle->IncRef();
                    handle->RefCount = 1;
                }
            }
        }
    }
}
