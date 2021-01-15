using System;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Reloaded.Hooks.Definitions.X64;

namespace Penumbra.Game
{
    public class GameUtils
    {
        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* LoadPlayerResourcesPrototype( IntPtr pResourceManager );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* UnloadPlayerResourcesPrototype( IntPtr pResourceManager );


        public LoadPlayerResourcesPrototype LoadPlayerResources { get; private set; }
        public UnloadPlayerResourcesPrototype UnloadPlayerResources { get; private set; }

        // Object addresses
        private IntPtr _playerResourceManagerAddress;
        public IntPtr PlayerResourceManagerPtr => Marshal.ReadIntPtr( _playerResourceManagerAddress );

        public GameUtils( DalamudPluginInterface pluginInterface )
        {
            var scanner = pluginInterface.TargetModuleScanner;

            var loadPlayerResourcesAddress =
                scanner.ScanText(
                    "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B 48 30 48 8B 01 FF 50 10 48 85 C0 74 0A " );
            var unloadPlayerResourcesAddress =
                scanner.ScanText( "41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B E9 48 83 C1 08" );

            _playerResourceManagerAddress = scanner.GetStaticAddressFromSig( "0F 44 FE 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 05" );

            LoadPlayerResources = Marshal.GetDelegateForFunctionPointer< LoadPlayerResourcesPrototype >( loadPlayerResourcesAddress );
            UnloadPlayerResources = Marshal.GetDelegateForFunctionPointer< UnloadPlayerResourcesPrototype >( unloadPlayerResourcesAddress );
        }

        public unsafe void ReloadPlayerResources()
        {
            UnloadPlayerResources( PlayerResourceManagerPtr );
            LoadPlayerResources( PlayerResourceManagerPtr );
        }
    }
}