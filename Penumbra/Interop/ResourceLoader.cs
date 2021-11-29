using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Penumbra.GameData.Util;
using Penumbra.Mods;
using Penumbra.Structs;
using Penumbra.Util;
using FileMode = Penumbra.Structs.FileMode;

namespace Penumbra.Interop
{
    public class ResourceLoader : IDisposable
    {
        public Penumbra Penumbra { get; set; }

        public bool IsEnabled { get; set; }

        public Crc32 Crc32 { get; }


        // Delegate prototypes
        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate byte ReadFilePrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate byte ReadSqpackPrototype( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* GetResourceSyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
            , uint* pResourceHash, char* pPath, void* pUnknown );

        [UnmanagedFunctionPointer( CallingConvention.ThisCall )]
        public unsafe delegate void* GetResourceAsyncPrototype( IntPtr pFileManager, uint* pCategoryId, char* pResourceType
            , uint* pResourceHash, char* pPath, void* pUnknown, bool isUnknown );

        // Hooks
        public Hook< GetResourceSyncPrototype >? GetResourceSyncHook { get; private set; }
        public Hook< GetResourceAsyncPrototype >? GetResourceAsyncHook { get; private set; }
        public Hook< ReadSqpackPrototype >? ReadSqpackHook { get; private set; }

        // Unmanaged functions
        public ReadFilePrototype? ReadFile { get; private set; }


        public bool   LogAllFiles   = false;
        public Regex? LogFileFilter = null;


        public ResourceLoader( Penumbra penumbra )
        {
            Penumbra = penumbra;
            Crc32    = new Crc32();
        }

        public unsafe void Init()
        {
            var readFileAddress =
                Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3 BA 05" );
            GeneralUtil.PrintDebugAddress( "ReadFile", readFileAddress );

            var readSqpackAddress =
                Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? ?? EB 05 E8 ?? ?? ?? ?? 84 C0 0F 84 ?? 00 00 00 4C 8B C3" );
            GeneralUtil.PrintDebugAddress( "ReadSqPack", readSqpackAddress );

            var getResourceSyncAddress =
                Dalamud.SigScanner.ScanText( "E8 ?? ?? 00 00 48 8D 8F ?? ?? 00 00 48 89 87 ?? ?? 00 00" );
            GeneralUtil.PrintDebugAddress( "GetResourceSync", getResourceSyncAddress );

            var getResourceAsyncAddress =
                Dalamud.SigScanner.ScanText( "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00" );
            GeneralUtil.PrintDebugAddress( "GetResourceAsync", getResourceAsyncAddress );


            ReadSqpackHook       = new Hook< ReadSqpackPrototype >( readSqpackAddress, ReadSqpackHandler );
            GetResourceSyncHook  = new Hook< GetResourceSyncPrototype >( getResourceSyncAddress, GetResourceSyncHandler );
            GetResourceAsyncHook = new Hook< GetResourceAsyncPrototype >( getResourceAsyncAddress, GetResourceAsyncHandler );

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

                return GetResourceSyncHook.Original( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown );
            }

            if( GetResourceAsyncHook == null )
            {
                PluginLog.Error( "[GetResourceHandler] GetResourceAsync is null." );
                return null;
            }

            return GetResourceAsyncHook.Original( pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
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

            if( !Penumbra.Config.IsEnabled || modManager == null )
            {
                if( LogAllFiles )
                {
                    file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
                    if( LogFileFilter == null || LogFileFilter.IsMatch( file ) )
                    {
                        PluginLog.Information( "[GetResourceHandler] {0}", file );
                    }
                }

                return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
            }

            file = Marshal.PtrToStringAnsi( new IntPtr( pPath ) )!;
            var gameFsPath      = GamePath.GenerateUncheckedLower( file );
            var replacementPath = modManager.ResolveSwappedOrReplacementPath( gameFsPath );
            if( LogAllFiles && ( LogFileFilter == null || LogFileFilter.IsMatch( file ) ) )
            {
                PluginLog.Information( "[GetResourceHandler] {0}", file );
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

            PluginLog.Verbose( "[GetResourceHandler] resolved {GamePath} to {NewPath}", gameFsPath, replacementPath );

            return CallOriginalHandler( isSync, pFileManager, pCategoryId, pResourceType, pResourceHash, pPath, pUnknown, isUnknown );
        }


        private unsafe byte ReadSqpackHandler( IntPtr pFileHandler, SeFileDescriptor* pFileDesc, int priority, bool isSync )
        {
            var modManager = Service<ModManager>.Get();

            if( ReadFile == null || pFileDesc == null || pFileDesc->ResourceHandle == null )
            {
                PluginLog.Error( "THIS SHOULD NOT HAPPEN" );
                return ReadSqpackHook?.Original( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
            }

            var gameFsPath = Marshal.PtrToStringAnsi( new IntPtr( pFileDesc->ResourceHandle->FileName() ) );

            var isRooted = Path.IsPathRooted( gameFsPath );

            // check if there is a file which is being refreshed
            if( gameFsPath != null && !isRooted )
            {
                var replacementPath = modManager.ResolveSwappedOrReplacementPath( GamePath.GenerateUncheckedLower( gameFsPath ) );

                if( replacementPath != null && Path.IsPathRooted( replacementPath ) && replacementPath.Length < 260 )
                {
                    gameFsPath = replacementPath;
                    isRooted = Path.IsPathRooted( gameFsPath );
                }
            }

            if( gameFsPath == null || gameFsPath.Length >= 260 || !isRooted )
            {
                return ReadSqpackHook?.Original( pFileHandler, pFileDesc, priority, isSync ) ?? 0;
            }

            PluginLog.Debug( "loading modded file: {GameFsPath}", gameFsPath );

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
            ReadSqpackHook?.Dispose();
            GetResourceSyncHook?.Dispose();
            GetResourceAsyncHook?.Dispose();
        }
    }
}