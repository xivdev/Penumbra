using System;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Penumbra.Structs;
using Reloaded.Hooks.Definitions.X64;
using ResourceHandle = FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ResourceHandle;

namespace Penumbra.Interop
{
    public class GameResourceManagement
    {
        private const int NumResources = 85;

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* LoadPlayerResourcesPrototype( IntPtr pResourceManager );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* UnloadPlayerResourcesPrototype( IntPtr pResourceManager );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* LoadCharacterResourcesPrototype( CharacterResourceManager* pCharacterResourceManager );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* UnloadCharacterResourcePrototype( IntPtr resource );


        public LoadPlayerResourcesPrototype LoadPlayerResources { get; }
        public UnloadPlayerResourcesPrototype UnloadPlayerResources { get; }
        public LoadCharacterResourcesPrototype LoadCharacterResources { get; }
        public UnloadCharacterResourcePrototype UnloadCharacterResource { get; }

        // Object addresses
        private readonly IntPtr _playerResourceManagerAddress;

        public IntPtr PlayerResourceManagerPtr
            => Marshal.ReadIntPtr( _playerResourceManagerAddress );

        private readonly IntPtr _characterResourceManagerAddress;

        public unsafe CharacterResourceManager* CharacterResourceManagerPtr
            => ( CharacterResourceManager* )Marshal.ReadIntPtr( _characterResourceManagerAddress ).ToPointer();

        public GameResourceManagement( DalamudPluginInterface pluginInterface )
        {
            var scanner = pluginInterface.TargetModuleScanner;

            var loadPlayerResourcesAddress =
                scanner.ScanText(
                    "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B 48 30 48 8B 01 FF 50 10 48 85 C0 74 0A " );
            var unloadPlayerResourcesAddress =
                scanner.ScanText( "41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B E9 48 83 C1 08" );
            var loadCharacterResourcesAddress  = scanner.ScanText( "E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" );
            var unloadCharacterResourceAddress = scanner.ScanText( "E8 ?? ?? ?? FF 4C 89 37 48 83 C7 08 48 83 ED 01 75 ?? 48 8B CB" );

            _playerResourceManagerAddress = scanner.GetStaticAddressFromSig( "0F 44 FE 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 05" );
            _characterResourceManagerAddress =
                scanner.GetStaticAddressFromSig( "48 8B 0D ?? ?? ?? ?? E8 ?? ?? ?? 00 48 8D 8E ?? ?? 00 00 E8 ?? ?? ?? 00 33 D2" );

            LoadPlayerResources    = Marshal.GetDelegateForFunctionPointer< LoadPlayerResourcesPrototype >( loadPlayerResourcesAddress );
            UnloadPlayerResources  = Marshal.GetDelegateForFunctionPointer< UnloadPlayerResourcesPrototype >( unloadPlayerResourcesAddress );
            LoadCharacterResources = Marshal.GetDelegateForFunctionPointer< LoadCharacterResourcesPrototype >( loadCharacterResourcesAddress );
            UnloadCharacterResource =
                Marshal.GetDelegateForFunctionPointer< UnloadCharacterResourcePrototype >( unloadCharacterResourceAddress );
        }

        // Forces the reload of a specific set of 85 files, notably containing the eqp, eqdp, gmp and est tables, by filename.
        public unsafe void ReloadPlayerResources()
        {
            ReloadCharacterResources();

            UnloadPlayerResources( PlayerResourceManagerPtr );
            LoadPlayerResources( PlayerResourceManagerPtr );
        }

        public unsafe string ResourceToPath( byte* resource )
            => Marshal.PtrToStringAnsi( new IntPtr( *( char** )( resource + 9 * 8 ) ) )!;

        private unsafe void ReloadCharacterResources()
        {
            var oldResources = new IntPtr[NumResources];
            var resources    = new IntPtr( &CharacterResourceManagerPtr->Resources );
            var pResources   = ( void** )resources.ToPointer();

            Marshal.Copy( resources, oldResources, 0, NumResources );

            LoadCharacterResources( CharacterResourceManagerPtr );

            for( var i = 0; i < NumResources; i++ )
            {
                var handle = ( ResourceHandle* )oldResources[ i ];
                if( oldResources[ i ].ToPointer() == pResources[ i ] )
                {
                    PluginLog.Debug( $"Unchanged resource: {ResourceToPath( ( byte* )oldResources[ i ].ToPointer() )}" );
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