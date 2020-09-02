using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Penumbra.Structs;
using Penumbra.Util;
using FileMode = Penumbra.Structs.FileMode;
using Penumbra.Extensions;

namespace Penumbra
{
    public class ResourceLoader : IDisposable
    {
        public Plugin Plugin { get; set; }

        public bool IsEnabled { get; set; }

        public Crc32 Crc32 { get; }


        // Delegate prototypes
        public unsafe delegate byte ReadFilePrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        public unsafe delegate byte ReadSqpackPrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        public unsafe delegate void* GetResourceSyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType,
            uint* pResourceHash, char* pPath, void* pUnknown );

        public unsafe delegate void* GetResourceAsyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType,
            uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown );

        public unsafe delegate void* LoadPlayerResourcesPrototype( IntPtr pResourceManager );

        public unsafe delegate void* UnloadPlayerResourcesPrototype( IntPtr pResourceManager );


        // Hooks
        public Hook< GetResourceSyncPrototype > GetResourceSyncHook { get; private set; }
        public Hook< GetResourceAsyncPrototype > GetResourceAsyncHook { get; private set; }
        public Hook< ReadSqpackPrototype > ReadSqpackHook { get; private set; }

        // Unmanaged functions
        public ReadFilePrototype ReadFile { get; private set; }


        public LoadPlayerResourcesPrototype LoadPlayerResources { get; private set; }
        public UnloadPlayerResourcesPrototype UnloadPlayerResources { get; private set; }

        // Object addresses
        private IntPtr _playerResourceManagerAddress;
        public IntPtr PlayerResourceManagerPtr => Marshal.ReadIntPtr( _playerResourceManagerAddress );

        
        public bool LogAllFiles = false;


        public ResourceLoader( Plugin plugin )
        {
            Plugin = plugin;
            Crc32 = new Crc32();
        }

        public unsafe void Init()
        {
            var scanner = Plugin.PluginInterface.TargetModuleScanner;

            var readFileAddress =
                scanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" );

            var readSqpackAddress =
                scanner.ScanText( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3" );

            var getResourceSyncAddress =
                scanner.ScanText( "E8 ?? ?? 00 00 48 8D 4F ?? 48 89 87 ?? ?? 00 00" );

            var getResourceAsyncAddress =
                scanner.ScanText( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00" );


            ReadSqpackHook = new Hook< ReadSqpackPrototype >( readSqpackAddress, new ReadSqpackPrototype( ReadSqpackHandler ) );

            GetResourceSyncHook = new Hook< GetResourceSyncPrototype >( getResourceSyncAddress,
                new GetResourceSyncPrototype( GetResourceSyncHandler ) );

            GetResourceAsyncHook = new Hook< GetResourceAsyncPrototype >( getResourceAsyncAddress,
                new GetResourceAsyncPrototype( GetResourceAsyncHandler ) );

            ReadFile = Marshal.GetDelegateForFunctionPointer< ReadFilePrototype >( readFileAddress );

            /////

            var loadPlayerResourcesAddress =
                scanner.ScanText(
                    "E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? BA ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8B 48 30 48 8B 01 FF 50 10 48 85 C0 74 0A " );
            var unloadPlayerResourcesAddress =
                scanner.ScanText( "41 55 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4C 8B E9 48 83 C1 08" );

            _playerResourceManagerAddress = scanner.GetStaticAddressFromSig( "0F 44 FE 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 05" );

            LoadPlayerResources =
                Marshal.GetDelegateForFunctionPointer< LoadPlayerResourcesPrototype >( loadPlayerResourcesAddress );
            UnloadPlayerResources =
                Marshal.GetDelegateForFunctionPointer< UnloadPlayerResourcesPrototype >( unloadPlayerResourcesAddress );
            ReadFile = Marshal.GetDelegateForFunctionPointer< ReadFilePrototype >( readFileAddress );
        }


        public unsafe void* GetResourceSyncHandler( IntPtr pFileManager, uint* pCategoryId,
            char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown )
        {
            return GetResourceHandler( true, pFileManager, pCategoryId, pResourceType,
                pResourceHash, pPath, pUnknown, false );
        }

        public unsafe void* GetResourceAsyncHandler( IntPtr pFileManager, uint* pCategoryId,
            char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown )
        {
            return GetResourceHandler( false, pFileManager, pCategoryId, pResourceType,
                pResourceHash, pPath, pUnknown, isUnknown );
        }

        private unsafe void* GetResourceHandler( bool isSync, IntPtr pFileManager, uint* pCategoryId,
            char* pResourceType, uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown )
        {
            var gameFsPath = Marshal.PtrToStringAnsi( new IntPtr( pPath ) );

            if( LogAllFiles )
                PluginLog.Log( "[ReadSqPack] {0}", gameFsPath );

            var candidate = Plugin.ModManager.GetCandidateForGameFile( gameFsPath );

            // path must be < 260 because statically defined array length :(
            if( candidate == null || candidate.FullName.Length >= 260 || !candidate.Exists )
            {
                return isSync
                    ? GetResourceSyncHook.Original( pFileManager, pCategoryId, pResourceType,
                        pResourceHash, pPath, pUnknown )
                    : GetResourceAsyncHook.Original( pFileManager, pCategoryId, pResourceType,
                        pResourceHash, pPath, pUnknown, isUnknown );
            }

            var cleanPath = candidate.FullName.Replace( '\\', '/' );
            var asciiPath = Encoding.ASCII.GetBytes( cleanPath );

            var bPath = stackalloc byte[asciiPath.Length + 1];
            Marshal.Copy( asciiPath, 0, new IntPtr( bPath ), asciiPath.Length );
            pPath = ( char* )bPath;

            Crc32.Init();
            Crc32.Update( asciiPath );
            *pResourceHash = Crc32.Checksum;

            return isSync
                ? GetResourceSyncHook.Original( pFileManager, pCategoryId, pResourceType,
                    pResourceHash, pPath, pUnknown )
                : GetResourceAsyncHook.Original( pFileManager, pCategoryId, pResourceType,
                    pResourceHash, pPath, pUnknown, isUnknown );
        }


        public unsafe byte ReadSqpackHandler( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync )
        {
            var gameFsPath = Marshal.PtrToStringAnsi( new IntPtr( pFileDesc->ResourceHandle->FileName ) );

            var isRooted = Path.IsPathRooted( gameFsPath );

            if( gameFsPath == null || gameFsPath.Length >= 260 || !isRooted )
            {
                return ReadSqpackHook.Original( pFileHandler, pFileDesc, priority, isSync );
            }

#if DEBUG
            PluginLog.Log( "loading modded file: {GameFsPath}", gameFsPath );
#endif

            pFileDesc->FileMode = FileMode.LoadUnpackedResource;

            var utfPath = Encoding.Unicode.GetBytes( gameFsPath );

            Marshal.Copy( utfPath, 0, new IntPtr( &pFileDesc->UtfFileName ), utfPath.Length );

            var fd = stackalloc byte[0x20 + utfPath.Length + 0x16];
            Marshal.Copy( utfPath, 0, new IntPtr( fd + 0x21 ), utfPath.Length );

            pFileDesc->FileDescriptor = fd;


            return ReadFile( pFileHandler, pFileDesc, priority, isSync );
        }

        public unsafe void ReloadPlayerResource()
        {
            UnloadPlayerResources( PlayerResourceManagerPtr );
            LoadPlayerResources( PlayerResourceManagerPtr );
        }

        public void Enable()
        {
            if( IsEnabled )
                return;

            ReadSqpackHook.Enable();
            GetResourceSyncHook.Enable();
            GetResourceAsyncHook.Enable();

            IsEnabled = true;
        }

        public void Disable()
        {
            if( !IsEnabled )
                return;

            ReadSqpackHook.Disable();
            GetResourceSyncHook.Disable();
            GetResourceAsyncHook.Disable();

            IsEnabled = false;
        }

        public void Dispose()
        {
            if( IsEnabled )
                Disable();

            ReadSqpackHook.Dispose();
            GetResourceSyncHook.Dispose();
            GetResourceAsyncHook.Dispose();
        }
    }
}