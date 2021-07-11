using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.Util;
using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using FileMode = Penumbra.Structs.FileMode;

namespace Penumbra.Interop
{
    public class ResourceLoader : IDisposable
    {
        public Plugin Plugin { get; set; }

        public bool IsEnabled { get; set; }

        public Crc32 Crc32 { get; }


        // Delegate prototypes
        [Function( CallingConventions.Microsoft )]
        public unsafe delegate byte ReadFilePrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate byte ReadSqpackPrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* GetResourceSyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
            , uint* pResourceHash, char* pPath, void* pUnknown );

        [Function( CallingConventions.Microsoft )]
        public unsafe delegate void* GetResourceAsyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
            , uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown );

        // Hooks
        public IHook< GetResourceSyncPrototype >? GetResourceSyncHook { get; private set; }
        public IHook< GetResourceAsyncPrototype >? GetResourceAsyncHook { get; private set; }
        public IHook< ReadSqpackPrototype >? ReadSqpackHook { get; private set; }

        // Unmanaged functions
        public ReadFilePrototype? ReadFile { get; private set; }


        public bool   LogAllFiles   = false;
        public Regex? LogFileFilter = null;


        public ResourceLoader( Plugin plugin )
        {
            Plugin = plugin;
            Crc32  = new Crc32();
        }

        public unsafe void Init()
        {
            var scanner = Plugin!.PluginInterface!.TargetModuleScanner;

            var readFileAddress =
                scanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" );

            var readSqpackAddress =
                scanner.ScanText( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3" );

            var getResourceSyncAddress =
                scanner.ScanText( "E8 ?? ?? 00 00 48 8D 8F ?? ?? 00 00 48 89 87 ?? ?? 00 00" );

            var getResourceAsyncAddress =
                scanner.ScanText( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00" );


            ReadSqpackHook       = new Hook< ReadSqpackPrototype >( ReadSqpackHandler, ( long )readSqpackAddress );
            GetResourceSyncHook  = new Hook< GetResourceSyncPrototype >( GetResourceSyncHandler, ( long )getResourceSyncAddress );
            GetResourceAsyncHook = new Hook< GetResourceAsyncPrototype >( GetResourceAsyncHandler, ( long )getResourceAsyncAddress );

            ReadFile = Marshal.GetDelegateForFunctionPointer< ReadFilePrototype >( readFileAddress );
        }


        private unsafe void* GetResourceSyncHandler(
            IntPtr pFileManager,
            uint* pCategoryId,
            char* pResourceType,
            uint* pResourceHash,
            char* pPath,
            void* pUnknown
        )
            => GetResourceHandler( true, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, false );

        private unsafe void* GetResourceAsyncHandler(
            IntPtr pFileManager,
            uint* pCategoryId,
            char* pResourceType,
            uint* pResourceHash,
            char* pPath,
            void* pUnknown,
            bool isUnknown
        )
            => GetResourceHandler( false, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );

        private unsafe void* CallOriginalHandler(
            bool isSync,
            IntPtr pFileManager,
            uint* pCategoryId,
            char* pResourceType,
            uint* pResourceHash,
            char* pPath,
            void* pUnknown,
            bool isUnknown
        )
        {
            if( isSync )
            {
                if( GetResourceSyncHook == null )
                {
                    PluginLog.Error( "[GetResourceHandler] GetResourceSync is null." );
                    return null;
                }

                return GetResourceSyncHook.OriginalFunction( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown );
            }

            if( GetResourceAsyncHook == null )
            {
                PluginLog.Error( "[GetResourceHandler] GetResourceAsync is null." );
                return null;
            }

            return GetResourceAsyncHook.OriginalFunction( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
        }

        private unsafe void* GetResourceHandler(
            bool isSync,
            IntPtr pFileManager,
            uint* pCategoryId,
            char* pResourceType,
            uint* pResourceHash,
            char* pPath,
            void* pUnknown,
            bool isUnknown
        )
        {
            string file;
            var    modManager = Service< ModManager >.Get();

            if( !Plugin!.Configuration!.IsEnabled || modManager == null )
            {
                if( LogAllFiles )
                {
                    file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
                    if( LogFileFilter == null || LogFileFilter.IsMatch( file ) )
                    {
                        PluginLog.Log( "[GetResourceHandler] {0}", file );
                    }
                }

                return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
            }

            file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
            var gameFsPath      = GamePath.GenerateUncheckedLower( file );
            var replacementPath = modManager.ResolveSwappedOrReplacementPath( gameFsPath );
            if( LogAllFiles && ( LogFileFilter == null || LogFileFilter.IsMatch( file ) ) )
            {
                PluginLog.Log( "[GetResourceHandler] {0}", file );
            }

            // path must be < 260 because statically defined array length :(
            if( replacementPath == null )
            {
                return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
            }

            var path = Encoding.ASCII.GetBytes( replacementPath );

            var bPath = stackalloc byte[path.Length + 1];
            Marshal.Copy( path, 0, new IntPtr( bPath ), path.Length );
            pPath = ( char* )bPath;

            Crc32.Init();
            Crc32.Update( path );
            *pResourceHash = Crc32.Checksum;

#if DEBUG
            PluginLog.Log( "[GetResourceHandler] resolved {GamePath} to {NewPath}", gameFsPath, replacementPath );
#endif

            return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
        }


        private unsafe byte ReadSqpackHandler( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync )
        {
            if( ReadFile == null || pFileDesc == null || pFileDesc->ResourceHandle == null )
            {
                return ReadSqpackHook?.OriginalFunction( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
            }

            var gameFsPath = Marshal.PtrToStringAnsi( new IntPtr( pFileDesc->ResourceHandle->FileName ) );

            var isRooted = Path.IsPathRooted( gameFsPath );

            if( gameFsPath == null || gameFsPath.Length >= 260 || !isRooted )
            {
                return ReadSqpackHook?.OriginalFunction( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
            }

#if DEBUG
            PluginLog.Log( "loading modded file: {GameFsPath}", gameFsPath );
#endif

            pFileDesc->FileMode = FileMode.LoadUnpackedResource;

            // note: must be utf16
            var utfPath = Encoding.Unicode.GetBytes( gameFsPath );

            Marshal.Copy( utfPath, 0, new IntPtr( &pFileDesc->UtfFileName ), utfPath.Length );

            var fd = stackalloc byte[0x20 + utfPath.Length + 0x16];
            Marshal.Copy( utfPath, 0, new IntPtr( fd + 0x21 ), utfPath.Length );

            pFileDesc->FileDescriptor = fd;

            return ReadFile( pFileHandler, pFileDesc, priority, isSync );
        }

        public void Enable()
        {
            if( IsEnabled )
            {
                return;
            }

            if( ReadSqpackHook == null || GetResourceSyncHook == null || GetResourceAsyncHook == null )
            {
                PluginLog.Error( "[GetResourceHandler] Could not activate hooks because at least one was not set." );
                return;
            }

            ReadSqpackHook.Activate();
            GetResourceSyncHook.Activate();
            GetResourceAsyncHook.Activate();

            ReadSqpackHook.Enable();
            GetResourceSyncHook.Enable();
            GetResourceAsyncHook.Enable();

            IsEnabled = true;
        }

        public void Disable()
        {
            if( !IsEnabled )
            {
                return;
            }

            ReadSqpackHook?.Disable();
            GetResourceSyncHook?.Disable();
            GetResourceAsyncHook?.Disable();

            IsEnabled = false;
        }

        public void Dispose()
        {
            Disable();
        }
    }
}